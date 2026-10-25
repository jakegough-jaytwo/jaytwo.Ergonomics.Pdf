# R4 protected-document wording proposal

2026-10-25, W-172, D-64. **Owner-approved with the five corrections from the 2026-10-25 review, incorporated below.** W-172 is complete as the design and wording proposal. W-179 applies this approved wording; W-180 ships the workflow behavior. Existing non-signing behavior remains shipped. The external review is design input, not independent validation. The owner's explicit approval authorizes the corrected protected-document edits, while preserving one editor session and existing engine interfaces and defaults.

## Repository facts used in the plan

- `PdfDocumentEditor.SaveIncremental(Stream, ...)` makes the destination the document's source. `PdfIncrementalWriter.FinishWrite` updates the source, cross-reference state and length and clears dirty objects. The same editor can then call `Sign` or `SignAsync` on that private stream. There is no demonstrated need for a second document/editor lifetime or a new engine API. W-180 must test this composition, including encrypted inputs.
- `PdfSigner.CreateSignatureDictionary` selects `ETSI.CAdES.detached` for `PadesBaselineB`. `CmsDetachedSignatureProvider` includes signing-certificate-v2 and omits CMS signing-time in that mode. The legacy default is intentionally `adbe.pkcs7.detached`; it is not a demonstrated PAdES defect. W-103 still must obtain independent classification.
- Signer and timestamp interfaces remain `IPdfExternalSigner`, `IPdfSignatureProvider`, and `ITimestampAuthority`. The review's `IRfc3161TimestampAuthority` was a placeholder. Broken citation placeholders and claims about an evolving standard edition are not qualification evidence; W-103 verifies its pinned tool and standard/policy details from primary sources.

## 1. AGENTS.md: authorize the test-only tools

In the existing external-tool paragraph, replace the sentence beginning `Tests may use these test-only external tools:` with:

> Tests may use these test-only external tools: qpdf; Python with pyHanko and pypdf; Poppler `pdftotext`, `pdftoppm`, and `pdfinfo`; Poppler `pdfsig` with NSS `certutil` as a second signature verifier; MuPDF `mutool` as a second renderer; SoftHSM 2; OpenSSL 3 as a local RFC 3161 authority and token verifier; and pinned EU DSS with a pinned JDK for independent PAdES profile classification.

Append to that paragraph:

> DSS and its JDK are test-only tools and never runtime dependencies. Locate Java with `JAYTWO_JAVA` and the DSS wrapper with `JAYTWO_DSS` through the common external-tool locator. Install pinned copies through the Docker base stage and the Windows test-tool installer. A test requiring DSS fails when it is missing.

## 2. docs/spec/phase-1.md section 6.7: target workflow requirements

Replace F4 with this row, marked Change until W-180 ships it:

| ID | Requirement | Status |
| --- | --- | --- |
| F4 | **One editor session.** A run opens the original input once and applies every operation through one `PdfDocumentEditor`. Non-signing runs save once and add at most one revision. Signing and document-timestamp runs use F18 to F21. The output shall preserve the original source bytes as an exact prefix and produce results semantically equivalent to the corresponding direct-engine operations. | Change |

Append these rows after F17, marked New until W-180 ships them:

| ID | Requirement | Status |
| --- | --- | --- |
| F18 | **Signing terminals.** `Sign` and `SignAsync` execute the described ordinary edits and one CMS signature. They accept an `IPdfExternalSigner` with an optional `ITimestampAuthority`, or the existing `IPdfSignatureProvider`, and `PdfSignatureOptions`. `Timestamp` and `TimestampAsync` execute the described ordinary edits and one document timestamp through the existing timestamp authority/provider. Each terminal offers separate stream and file-path outputs; async forms take a `CancellationToken`. Existing engine interfaces, options, algorithms, and defaults remain unchanged. A CMS signature may include an RFC 3161 signature timestamp. A run adds exactly one CMS signature or one document timestamp. Adding multiple signatures, adding a signature followed by a separate document timestamp, and signing-to-source conveniences are not supported. Existing signatures in the input remain subject to the engine's preservation and permission checks. F1 to F3 and F5 to F17 continue to apply where relevant. | New |
| F19 | **Staging and revisions.** A signing or document-timestamp terminal uses a private readable, writable, seekable staging file. It saves the ordinary edits there through the run's editor, then appends the signature or document timestamp through that same editor without reopening the document. Ordinary edits add at most one revision; the signature or document timestamp adds a separate revision. With no ordinary changes, only the signature or document-timestamp revision is added. An embedded signature timestamp adds no PDF revision. The run never buffers a whole PDF or attachment in managed memory. Temporary disk use may follow output size; staging is disposed and cleanup attempted after success, failure, or cancellation. | New |
| F20 | **Signed-output publication and failures.** Complete the staged signature and any embedded signature timestamp before publishing a file or copying any bytes to a caller's destination stream. A processing, signer, authority, size, validation, or cancellation failure before final copying or publication leaves the caller's destination unchanged. Stream outputs require an empty writable seekable destination. Failure or cancellation during final copying attempts to restore its starting length on a best-effort basis; an output failure may prevent rollback. File outputs retain F17's alias, overwrite, flush, and publication rules and F12's failure guarantees. Borrowed resources remain caller-owned, engine exceptions retain their categories, and host exception details remain sanitized by the engine. | New |
| F21 | **Validation and external calls.** Argument and workflow-shape errors detectable without opening the document are rejected before the input opens. Existing engine checks enforce signature fields, appearances, certification, field locks, encryption permissions, and source consistency. A terminal consumes the workflow once under F3 and never automatically retries a signer, provider, or timestamp-authority invocation, including after reservation overflow. A caller may start a new workflow with different options after a failure. | New |

W-180 changes F4 and F18 to F21 to Exists in the same transaction as their implementation and shipped README/ACCEPTANCE updates under D-43. W-179 does not advertise them as shipped. No existing engine behavior, including direct signing with pending edits, changes.

## 3. QUALIFICATION.md: R4 evidence without resuming release qualification

Insert this paragraph after the suspension paragraph in the introduction:

> **R4 signing evidence (D-64).** Stage R4 refreshes signing evidence for the final engine and workflow architecture without resuming release qualification or packaging. Its exit checkpoint requires direct-engine and workflow interoperability checks, pinned test-only EU DSS/JDK classification of PAdES B-B and B-T, direct CMS fuzz campaign evidence, and the existing signing and timestamp failure and resource guarantees. Record profile classification, cryptographic verification, and trust-policy indication separately under explicit test trust configuration. Retain the raw DSS reports and record the input hashes, DSS/JDK versions, validation policy, trust anchors, validation time, and relevant network/revocation settings. Record the reported signature format/profile separately from digest/signature verification and validation indication/sub-indication. A failed check must not be relabeled unsupported merely because its cause is environmental. A missing or unsupported check is not a pass. Existing G4 and G5 scenarios remain required; the R4 records do not by themselves claim a release gate or production qualification.

No gate is weakened or re-adopted. Observed R4 results go in `agents/evidence/` and work-item closeouts; W-181 does not edit protected wording opportunistically.

## 4. .agents/skills/pdf-fuzz-qualification/references/targets.md

Append this row to the existing target table. Keep the `open` row's signature-field walk, the `der` target, and all existing limits unchanged:

| Target | Entry point | Exercises | Expected exceptions | Seeds |
| --- | --- | --- | --- | --- |
| `cms` | `CmsSignatureReader.Read` on the input as a CMS signature and as an `ETSI.RFC3161` token | BER and DER lengths and indefinite-length nesting, ContentInfo and SignedData structure, certificate set, SignerInfo, signed and unsigned attributes, signing-time and genTime parsing, issuer-serial and subject-key-identifier matching | `PdfException` | CMS extracted from signed fixtures and corpus files; library-generated `adbe.pkcs7.detached` and `ETSI.CAdES.detached` signatures; issuer/serial and subject-key-identifier signer identifiers; embedded signature-timestamp and document-timestamp tokens; valid indefinite-length BER re-encodings; and malformed/truncated length and nesting variants. |

Immediately after the target table, add:

> The `cms` target accepts inputs of at most 64 KiB. For each accepted input, invoke the ordinary-signature and document-timestamp parse modes independently; an expected exception in one mode must not suppress the other. Catch only the documented expected `PdfException` failures for each invocation. Unexpected exceptions, hangs, and resource-limit failures are findings.

> For valid BER seeds, preserve DER encoding of signed attributes; RFC 5652 requires that even when the surrounding CMS uses BER.

W-179 adopts this target description before W-104 implements it. It describes a scheduled target, not executed evidence. No other skill wording change is proposed.

## Execution and evidence ownership

1. W-179: obtain exact-wording approval and apply only the approved protected edits.
2. W-180: implement and test dedicated signing/timestamp terminals, file staging, and publication through one editor session.
3. W-103: pinned DSS/JDK integration and independent direct-engine/workflow profile classification, preserving its supported key/digest matrix and negative controls.
4. W-104: direct CMS target, representative seeds and deterministic smoke; no claim that a smoke run is a campaign.
5. W-181: fresh signing interoperability and bounded CMS/RFC 3161 campaign evidence against the final implementation. Preserve W-18's historical record and the separate security phase. Record the tested revision, harness/tool versions, seed-corpus hashes, exact invocation, campaign duration and execution count, timeout/resource limits, and finding dispositions. Report incomplete runs explicitly.
6. W-182: explicit R4 integration exit checkpoint before any remaining security-phase item.

No demonstrated engine API migration is outstanding. If implementation reveals a missing public capability, record the specific evidence and rescope the affected item before an engine change; do not broaden the interface speculatively.

## Approval record

- 2026-10-25: The owner approved D-64 and this R4 direction with five wording corrections: newly added signature cardinality; reproducible raw DSS evidence and environmental-failure classification; a 64 KiB CMS bound with independently executed parse modes; explicit CMS seed classes with DER signed attributes in valid BER seeds; and reviewable W-181 campaign records. The AGENTS tool authorization, F4, F19 to F21, and adoption/implementation ownership remain as proposed. W-179 is authorized to apply the corrected protected wording; W-172 stays complete as design/proposal work.
- Supporting references: [DSS validation documentation](https://ec.europa.eu/digital-building-blocks/DSS/webapp-demo/doc/dss-documentation.html) and [RFC 5652 section 5.3](https://www.rfc-editor.org/rfc/rfc5652.html#section-5.3). These support evidence separation and the valid-BER seed rule; they are not executed tool evidence.
