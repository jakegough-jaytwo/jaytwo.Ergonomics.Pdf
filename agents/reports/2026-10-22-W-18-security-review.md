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
- Owner disposition, 2026-10-22: accepted as a documented limitation. The current implementation is approved (W-107).
- Ecosystem comparison, gathered for the owner's decision:
  - The repository tests already check validity independently. pyHanko and Poppler `pdfsig` verify every signing capability's output, and the wrong-key reproducer above shows both rejecting a file that `Sign` completed. The gap is a runtime check, not a test gap.
  - EU DSS, the reference implementation whose external-signing model is closest to this library's, does not verify an externally produced signature value when it signs. `PAdESService.signDocument` and `ExternalCMSService.buildCMS` pass the value through `SignatureValueChecker.ensureSignatureValue`, which only normalizes the value to the signature algorithm, then build the CMS. DSS offers a separate opt-in `AbstractSignatureService.isValidSignatureValue(toBeSigned, signatureValue, signingCertificate)`, which returns a boolean and which the signing path does not call. This was read from DSS source on its master branch on 2026-10-22, not from a tagged release.
  - iText (`PdfSigner.signDetached` with an `IExternalSignature` and a separate chain) and Apache PDFBox (external signing returns the finished CMS) also embed the value without verifying it against the certificate. This is from general knowledge of those APIs, not a source reading in this session.
  - Acrobat signs through the operating system key store or PKCS#11, where key and certificate come paired, and by default validates signatures when a document opens, so a mismatched signature is visible at once. That is viewer behavior, which the charter excludes.
  - If protection against a key and certificate mismatch is wanted later, the consistent design is DSS's: an opt-in check the caller invokes, not a default refusal. That would be new public API and a charter exception, so it is not planned for Phase 1.
  - Sources: [DSS SignatureValueChecker javadoc](https://dss.harica.gr/apidocs/eu/europa/esig/dss/signature/SignatureValueChecker.html), [DSS AbstractSignatureService.java](https://github.com/esig/dss/blob/master/dss-document/src/main/java/eu/europa/esig/dss/signature/AbstractSignatureService.java), [DSS PAdESService.java](https://github.com/esig/dss/blob/master/dss-pades/src/main/java/eu/europa/esig/dss/pades/signature/PAdESService.java), [DSS ExternalCMSService.java](https://github.com/esig/dss/blob/master/dss-pades/src/main/java/eu/europa/esig/dss/pades/signature/ExternalCMSService.java).

### SR-2: RFC 3161 token signatures are not cryptographically checked

- Review classification: Confirmed, Critical, High confidence under the security-review skill's hostile-TSA-response model.
- Locations: `Rfc3161TimeStampToken.cs` lines 46-73 and 94-132; `CmsDetachedSignatureProvider.cs` lines 120-175; `DocTimeStampSignatureProvider.cs` lines 55-64; evidence in `SignatureTests.cs` lines 1528-1603 and 1621-1677.
- Trigger: a structurally valid token carries the requested imprint and nonce and a matching ESS certificate, but its CMS signature is forged or arbitrary.
- Impact: signature timestamps and document timestamps complete even though cryptographic verification of the token would fail.
- Evidence: `RequireSigningCertificate` documents that it is not signature or trust validation and only requires a nonempty signature value. Test tokens intentionally use an all-zero 256-byte value.
- Boundary: the hostile-response review model calls this misleading success, while the binding charter and Phase 1 spec exclude cryptographic signature validation and acceptance separates certificate inclusion from validity and trust. Remediation would change that settled boundary and requires an owner decision. Until then this is an explicit G8 release blocker under the review model, not authorization to add validation.
- Owner disposition, 2026-10-22: accepted as a documented limitation, on the same basis as SR-1 (W-107). `openssl ts -verify` and pyHanko verify OpenSSL tokens in the tests. Checking a token's signature without trust would only show the token is self-consistent, since a forged token can carry its own certificate.

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

## Dispositions

Recorded by the implementing context after the review. These dispositions are not part of the independent review.

| Finding | Disposition | Regression |
|---|---|---|
| SR-1 | Accepted by the owner on 2026-10-22 as a documented limitation (W-107); see SR-1 above. Previously: open release blocker for G8 under the review model. The charter excludes signature validation and assigns signing to the trusted host contract, so the owner must decide whether to accept this as a documented limitation or to change that boundary. No validation was added. | Existing `SigningInteropTests` case that signs with another key. |
| SR-2 | Accepted by the owner on 2026-10-22 as a documented limitation (W-107). Previously: open release blocker for G8 under the review model, on the same boundary as SR-1. The owner must decide. No validation was added. | Existing `SignatureTests` timestamp cases with an all-zero token signature. |
| SR-3 | Confirmed by a deterministic allocation regression that failed on the pre-fix code, then fixed. The certificate set is now walked one choice at a time, so non-certificate choices no longer create a node each. It is capped at 65,536 choices, and a larger set throws `PdfParseException`. Mitigated; it no longer blocks G8. | `SignatureInspectionTests.non_certificate_choices_are_streamed_without_a_node_per_choice`, `certificate_choice_count_is_bounded`. |
| SR-4 | Fixed. An object stream is held in header order. The xref index selects the member, and a member whose object number differs from the xref entry throws `PdfParseException`. A header that repeats an object number throws `PdfParseException`. | `ObjectStreamCacheTests.object_stream_xref_index_must_match_the_header_position`, `duplicate_object_numbers_in_an_object_stream_are_rejected`. |
| SR-5 | Fixed. A direct `null` for DocMDP `/TransformParams` is treated as `P` 1. A direct `null` for FieldMDP `/TransformParams`, `/Fields`, or a `/Fields` entry locks the field, as an unresolved reference already did. An absent `/TransformParams` still means DocMDP `P` 2. | `NullReferenceTests` data rows for direct `null`. |
| SR-6 | Fixed. The steps of finalization that can fail run before the engine records the revision. These steps are reading the destination length and, for a cross-stream write, disposing an owned source. A failure in either truncates the destination to its starting length and keeps the pending edits, and a retry writes the bytes an unfailed write would. The copy-only cross-stream save switches streams inside its rollback block. | `AsyncTests.a_length_failure_during_finalization_*`, `a_source_dispose_failure_during_finalization_*`, `a_source_dispose_failure_during_a_copy_only_save_*`. |

Red baseline: in a scratch worktree at `e2d6176` (pre-fix product code) with only the four changed test files copied in, all 21 new regression cases failed on net10.0. The 10 cases in that selection that passed were the pre-existing rows of the same `NullReferenceTests` theories. After the fixes, the same 21 cases pass. The full suite then passed 4,309 of 4,309 on net10.0, net8.0, net6.0, and net48 on Windows.

G8 review status: SR-3 through SR-6 are mitigated. On 2026-10-22 the owner accepted SR-1 and SR-2 as documented limitations (W-107). No review finding remains a release blocker.
