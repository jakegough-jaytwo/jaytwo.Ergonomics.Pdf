# W-18 independent security review, 2026-10-22

## Reviewer context

The reviewer ran in a fresh agent context, had no implementation involvement in the reviewed code, worked single-agent, and made no repository changes. The owner explicitly authorized this independent review. The reviewed product revision was `2a620ea4755687fd6f2db1c1df5dc29c85ab47c9`; changes through the campaign-plan commit `3812174` affected only W-18 workflow and evidence documents, not product source.

The reviewer began with the required Graphify query, then used pinned source and test inspection and call/data-flow analysis against `AGENTS.md`, `ACCEPTANCE.md`, and `QUALIFICATION.md`. A targeted test execution was attempted, but NuGet network access was unavailable in that context, so no dynamic proof completed there. The parent context separately ran the fixed-seed smoke suite and campaign; those results are in `2026-10-22-W-18-fuzz.md`.

## Scope

Parser, xref and object streams, filters and decompression limits, standard security revisions 2 through 6, DER and CMS, signature construction and inspection, RFC 3161, DocMDP and FieldMDP, allocation timing, output rollback, and diagnostic-secret handling.

## Findings

### SR-1: Host signer output is not cryptographically checked

- Review classification: Confirmed, Critical, High confidence under the security-review skill's hostile-signer-callback model.
- Locations: `CmsDetachedSignatureProvider.cs` lines 96-114 and 141-177; `PdfEngine.cs` lines 2012-2056; existing reproducer at `SigningInteropTests.cs` lines 695-718.
- Trigger: `IPdfExternalSigner.SignHashAsync` returns any nonempty RSA value, or a correctly sized but invalid P1363 ECDSA value, including a value made with a key different from `CertificateDer`.
- Impact: `Sign` completes with a CMS signature that identifies the supplied certificate but does not verify.
- Evidence: the provider checks only nonempty RSA output and ECDSA length. The existing test signs with another RSA key and confirms that external verifiers reject the completed output.
- Boundary: the hostile-callback review model calls this misleading success. The binding charter and Phase 1 spec exclude cryptographic signature validation, the README assigns signing to the trusted host contract, and an existing test deliberately preserves the distinction. Remediation would change a settled signer/validation boundary and requires an owner decision. Until then this is an explicit G8 release blocker under the review model, not authorization to add validation.

### SR-2: RFC 3161 token signatures are not cryptographically checked

- Review classification: Confirmed, Critical, High confidence under the security-review skill's hostile-TSA-response model.
- Locations: `Rfc3161TimeStampToken.cs` lines 46-73 and 94-132; `CmsDetachedSignatureProvider.cs` lines 120-175; `DocTimeStampSignatureProvider.cs` lines 55-64; evidence in `SignatureTests.cs` lines 1528-1603 and 1621-1677.
- Trigger: a structurally valid token carries the requested imprint and nonce and a matching ESS certificate, but its CMS signature is forged or arbitrary.
- Impact: signature timestamps and document timestamps complete even though cryptographic verification of the token would fail.
- Evidence: `RequireSigningCertificate` documents that it is not signature or trust validation and only requires a nonempty signature value. Test tokens intentionally use an all-zero 256-byte value.
- Boundary: the hostile-response review model calls this misleading success, while the binding charter and Phase 1 spec exclude cryptographic signature validation and acceptance separates certificate inclusion from validity and trust. Remediation would change that settled boundary and requires an owner decision. Until then this is an explicit G8 release blocker under the review model, not authorization to add validation.

### SR-3: Certificate-set parsing can amplify a bounded CMS input into a large node list

- Review classification: Suspected, High, Medium confidence.
- Locations: `PdfParser.cs` lines 1015-1170 and its 32 MiB structural string cap; `CmsSignatureReader.cs` lines 245-261 and 289-304.
- Trigger: a near-cap CMS `/Contents` whose certificate-set container holds millions of minimum-size TLVs.
- Impact: signature inspection materializes one `Node` per child before filtering certificate choices, potentially causing severe managed-memory pressure or `OutOfMemoryException`.
- Evidence: a repeated `05 00` sequence can create roughly 16 million children; the final node array alone is hundreds of MiB before list growth and the content buffer. Dynamic reproduction was not completed.
- Boundary: G7 resource ceilings and G8's uncontrolled-memory/crash rule. This requires confirmation or a bounded parser change before disposition.

### SR-4: Object-stream xref indices are ignored

- Review classification: Confirmed, Medium, High confidence.
- Locations: `PdfEngine.cs` lines 2475-2500; `PdfParser.cs` lines 540-577.
- Trigger: a type-2 xref entry names an index different from the object's position in the object-stream header, or the header repeats an object number.
- Impact: resolution selects by header object number rather than the xref index; duplicate IDs overwrite earlier values, allowing malformed input to resolve differently from index-based readers.
- Boundary: strict ISO reading and the closed writer-quirk list. Add a deterministic regression and reject the mismatch.

### SR-5: Direct PDF null can fail open in DocMDP and FieldMDP

- Review classification: Confirmed, Medium, High confidence.
- Locations: `PdfEngine.cs` lines 2667-2674, 3515-3565, and 3632-3673.
- Trigger: direct `/TransformParams null`, direct `/Fields null`, or a direct null entry in `/Fields`.
- Impact: DocMDP defaults to `P=2`, or FieldMDP obtains an empty name set, permitting a write that malformed policy data should block. Dangling indirect references already fail closed.
- Boundary: certification and FieldMDP guarantees. Add deterministic regressions and treat malformed direct null like unresolved policy data.

### SR-6: Finalization failures can bypass destination rollback

- Review classification: Confirmed, Medium, High confidence.
- Locations: `PdfEngine.cs` lines 1838-1896, 1900-2063, and 4486-4514.
- Trigger: `FinishWrite` fails after the appendix is written, such as a throwing destination `Length` getter or a cross-stream source-disposal failure in `SwitchStream`.
- Impact: the destination can remain extended and engine state can be partially updated after the operation reports failure.
- Boundary: G9 and the failed-write destination-integrity guarantee. Add fault-injection regressions and restore/truncate state on finalization failure as far as the stream permits.

## Positive observations

The parser has explicit nesting, token, container, xref-map, structural-stream, and decompression bounds. Object-resolution cycles and xref-chain depth are bounded. AES/RC4 object selection, revision 6 hashing bounds, constant-time password comparisons, signature `/Contents` plaintext treatment, generic signer/TSA callback exception messages, and pre-destination signature buffering exposed no additional confirmed Critical or High defect in this review.

## Coverage limitations

The reviewer did not complete a dynamic proof because restore required unavailable NuGet access. There was no independent cryptanalysis of framework primitives, trust-chain or revocation analysis, transport/TLS review, fuzz-campaign execution, or platform-specific fault injection. SR-3's severity remains environment-dependent and is Suspected.

## Review result

The fresh review found two Confirmed Critical findings under the mandated hostile-callback/response model, one Suspected High resource finding, and three Confirmed Medium findings. G8 cannot be recorded as having no unmitigated Critical or High issue until SR-1 and SR-2 receive an owner disposition and SR-3 is confirmed or mitigated.
