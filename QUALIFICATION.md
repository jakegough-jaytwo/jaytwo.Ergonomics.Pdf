# Production Qualification Standard

jaytwo.Ergonomics.Pdf, Release 1.0. Owner: human owner. Adopted 2026-10-18.

`ACCEPTANCE.md` states the required behavior. This document states the evidence a release needs before that behavior can be called production-qualified. Where it disagrees with `AGENTS.md`, `AGENTS.md` wins.

## 1. Definition

A capability may be designated **Production-Qualified** when it has passed all applicable acceptance gates in this document.

Production qualification means the capability is suitable for use with real enterprise documents **within its explicitly documented support boundaries**.

It does not mean:

- Every valid PDF is supported.
- Every feature in the PDF specification is implemented.
- Every malformed PDF can be recovered.
- Every PDF viewer renders identically.
- Every performance characteristic is optimal.
- Every future integration or deployment environment is automatically qualified.

The library as a whole may be designated **Production-Qualified 1.0** only when all capabilities advertised as production-supported pass their applicable gates.

Partially implemented or experimental capabilities must be clearly distinguished from production-supported functionality.

## 2. Mandatory release gates

| Gate | Requirement | Release blocking? |
|---|---|---|
| G1 | Documented capability and compatibility boundaries | Yes |
| G2 | Independent PDF structural interoperability | Yes |
| G3 | Correctness of supported document operations | Yes |
| G4 | Independent cryptographic signature verification | Yes |
| G5 | Independent RFC 3161 timestamp verification | Yes, for timestamped signing |
| G6 | Independent form-appearance rendering | Yes, for field filling |
| G7 | Large-file resource qualification | Yes |
| G8 | Security and adversarial-input testing | Yes |
| G9 | Failure handling and output integrity | Yes |
| G10 | Stable API, reproducible build, and documentation | Yes |

A gate passes on recorded evidence, not developer confidence.

## 3. G1: Documented support boundaries

Before qualification, establish an accurate support matrix.

It must identify:

- Supported PDF versions and structural representations.
- Supported cross-reference and object-stream structures.
- Supported encryption types and algorithms.
- Supported AcroForm field types and appearances.
- Supported signing algorithms and signature profiles.
- Supported metadata and attachment operations.
- Input, output, and resource limits.
- Features that are unsupported or experimental.

The support matrix must distinguish read-only compatibility from modification compatibility.

For example, successfully opening an existing signed PDF does not establish that the library can safely modify it.

**Acceptance:**

- Every advertised capability has explicit supported behavior.
- Known limitations are documented.
- Unsupported operations reject predictably.
- No known case silently produces an incorrect result while reporting success.

No requirement exists to support XFA, rendering, stamping, general page-content editing, or other excluded functionality.

## 4. G2: Independent structural interoperability

The library must demonstrate compatibility with independently generated PDF documents.

**Initial qualification corpus:**

- At least 50 distinct, meaningful PDF fixtures.
- At least four independent producers or PDF toolchains.
- Classic xref tables, xref streams, object streams, and hybrid references.
- Multiple incremental revisions.
- Nested page trees and inherited properties.
- Supported encrypted PDFs.
- Existing form fields and signature fields.
- Metadata and embedded files.
- At least some real-world documents not designed specifically around this library.

The 50-fixture target is an initial engineering acceptance threshold, not a statistical guarantee of universal PDF compatibility. Avoid padding the corpus with trivial variations.

For every relevant fixture, test the supported operations and independently inspect the result.

**Acceptance:**

- 100% of fixtures classified as supported pass their applicable correctness checks.
- 100% of deliberately unsupported cases tested fail predictably and without corruption.
- Modified output passes `qpdf --check` without unexpected warnings.
- At least one additional independent PDF implementation confirms relevant object semantics.
- Original document bytes remain unchanged when an incremental revision is promised.
- Any interoperability failure is fixed, explicitly excluded from the supported matrix with justification, or escalated as a release blocker.

Do not treat `qpdf --check` as proof of content semantics or visual correctness.

## 5. G3: Functional correctness

Each production-supported operation must be independently verified.

**AcroForm extraction**

Verify names, types, values, widget associations, page numbers, bounds, and applicable inherited properties.

Include hierarchical fields, separate and merged widgets, multiple placements, rotated pages, and nonzero page origins.

**AcroForm filling**

Verify supported text, checkbox, radio, and choice-field operations.

Values, appearance states, and generated appearances must agree.

**Metadata**

Verify Info and XMP updates with an independent reader. Unrelated metadata and document content must remain intact.

**Attachments**

Verify enumeration, extraction, addition, replacement, and removal.

Extracted attachment bytes must match the expected bytes exactly, including for large binary inputs.

**Incremental revisions**

Verify that changed objects are visible in the latest revision without corrupting unrelated objects or previous signed revisions.

**Acceptance:**

Every production-supported operation has positive, negative, and interoperability tests.

No known case may silently produce incorrect values, geometry, attachment bytes, or unintended modifications.

## 6. G4: Digital signature correctness

This gate is mandatory before production deployment of the signing capability.

At least two independent cryptographic/PDF verification implementations must be used. One should understand PDF signature semantics, not only CMS syntax.

Example verification tools include pyHanko and pdfsig, with `SignedCms` or OpenSSL as additional independent CMS checks.

**Required scenarios:**

- Signing a new signature field.
- Signing an existing empty signature field.
- Correct CMS signed-attributes encoding.
- Correct `/ByteRange` and revision coverage.
- Correct certificate-chain inclusion.
- Trusted validation, by the independent verifier, using explicitly configured test trust anchors.
- Signing through an actual PKCS#11 provider, such as SoftHSM.
- Signing encrypted PDFs within the documented support matrix.
- Preserving existing signatures during subsequent permitted operations.
- Rejecting prohibited modifications or unsupported signing conditions.
- Rejecting tampered signed data.
- Correct handling of external signer failure.

For a newly signed final document, independent validation must confirm cryptographic integrity, expected revision coverage, and trust under the configured test validation policy.

For earlier signatures, report cryptographic integrity, coverage, trust, and modification-policy compliance separately.

**Acceptance:**

- All required signing scenarios pass.
- The signed bytes are exactly those intended.
- The signer certificate matches the signing key.
- Independently verified CMS cryptographic integrity is established.
- No false success is reported for invalid signatures or prohibited operations.
- Supported signed output can be opened and verified by at least two independent implementations.
- No known cryptographic correctness defect remains open.

A successful CMS digest check alone is insufficient.

## 7. G5: RFC 3161 timestamping

A signature timestamp and a separate PDF document timestamp are distinct capabilities.

The production-supported timestamped-signing workflow must be tested against an independently implemented RFC 3161 timestamp authority.

**Acceptance:**

- A genuine RFC 3161 response is obtained and incorporated.
- The timestamp message imprint matches the intended cryptographic signature value for a CMS signature timestamp.
- The resulting timestamp is independently verified.
- Timestamp trust is validated by the independent verifier under an explicit test trust configuration. The library does not validate TSA trust, because `AGENTS.md` excludes signature validation.
- The library rejects malformed responses, incorrect imprints, and mismatched nonces. The independent verifier rejects untrusted timestamps.
- TSA failures do not result in an incorrectly reported successful timestamped signature.
- The resulting PDF signature remains independently verifiable.

If PAdES B-T compliance is claimed, independently validate the resulting document against the requirements of that profile.

Do not claim B-LT or B-LTA support unless their separate requirements are implemented and independently verified. `AGENTS.md` currently excludes PAdES-LTV.

## 8. G6: AcroForm appearance correctness

Generated appearances must be verified by at least one independent PDF renderer.

A second renderer is desirable for representative cases.

The initial test set should cover:

- Text visibility.
- Supported character encodings.
- Left, center, and right alignment.
- Long values.
- Checkbox and radio appearances.
- Field boundary clipping.
- Rotated pages.
- CropBox offsets.
- Representative font sizes and field dimensions.

Use deterministic reference fixtures and automated visual comparisons where practical.

**Acceptance:**

- Every production-supported field type renders its populated value correctly.
- No known cases of missing, displaced, incorrectly selected, or unexpectedly clipped appearances remain within the supported matrix.
- Supported fields do not depend on a downstream viewer generating missing appearances.
- Unsupported font or text features are rejected or explicitly classified rather than silently rendered incorrectly.

Pixel-identical rendering across every viewer is not required.

## 9. G7: Large-document performance and memory

The library must demonstrate resource consumption consistent with its stream-native architecture.

**Initial benchmark suite:**

| Workload | Qualification objective |
|---|---|
| 500-page representative PDF | Form extraction, metadata editing, and signing complete correctly |
| Dense PDF of at least 1 GiB | No whole-document materialization |
| Sparse PDF of at least 3 GiB | Correct 64-bit offsets, reads, and signature coverage |
| Large attachment of at least 250 MiB | Streaming addition and extraction |
| Repeated object-stream access | No unacceptable retained-memory growth |
| Encrypted xref-stream PDF | No unnecessary full-file discovery scan |

Use real file-backed inputs. Sparse tests alone are insufficient.

**Proposed starting memory budget:** For the benchmark fixtures above, target no more than 256 MiB of peak managed heap growth for simple inspect/edit/sign operations, with a 512 MiB exception ceiling for more complex structural traversals. These are proposed product thresholds, not PDF specification requirements. The Technical Lead and QA must measure and approve the final fixture-specific budgets before release.

Also establish repeatable timing baselines and investigate material regressions. Do not impose arbitrary throughput targets without accounting for storage, cryptographic providers, TSA latency, and the benchmark environment.

**Acceptance:**

- No operation unexpectedly loads the entire input PDF into managed memory.
- Measured peak memory remains within the approved workload budgets.
- Memory does not grow indefinitely during repeated use or document disposal.
- Large attachments are processed without whole-attachment buffering where streaming is supported.
- Output limits are documented and fail safely.
- Resource limits protect against excessive allocations and decompression.
- No known serious performance regression remains unexplained.

The 8 MiB signing-appendix limit and classic-xref output-offset limit may remain if documented and if they do not violate an advertised production workflow.

## 10. G8: Security and adversarial robustness

The library processes potentially untrusted PDF inputs.

Qualification requires deliberate negative testing and an independent security-focused review of the PDF parsing and signing paths.

Include:

- Malformed xref structures.
- Invalid indirect references.
- Recursive or cyclic object relationships.
- Truncated files.
- Incorrect stream lengths.
- Decompression amplification.
- Excessive nesting.
- Unsupported encryption.
- Malformed CMS/DER.
- Malformed TSA responses.
- Integer overflow conditions.
- Object-count and allocation pressure.

Introduce repeatable fuzz testing for high-risk parsing and decoding entry points.

**Initial fuzzing target:** At least 24 aggregate CPU-hours over the selected high-risk entry points before release, followed by a smaller continuous CI suite. This is a minimum exercise target, not proof of security; coverage quality and discovered defects matter more than elapsed time.

**Acceptance:**

- Zero known unmitigated Critical or High security defects affecting supported operations.
- No reproducible uncontrolled memory growth, infinite loops, or process crashes in the qualified adversarial corpus.
- Confirmed defects become permanent regression cases.
- Input limits and security-sensitive error behavior are documented.
- Cryptographic signing and PDF security handling have received an independent review by an agent or reviewer who did not implement the relevant code.

Resource-limit exceptions are acceptable. Uncontrolled failure is not.

## 11. G9: Failure and output integrity

Test failures during:

- Reading.
- Parsing.
- Object resolution.
- Incremental writing.
- Attachment copying.
- External signing.
- Timestamping.
- Stream disposal and cleanup.

**Acceptance:**

- Input PDFs remain unchanged unless the API explicitly documents in-place modification.
- No operation reports success when output finalization fails.
- Cryptographic and timestamp failures are reported accurately.
- No invalid output is silently published as a completed signed document.
- Partial destination writes are either rolled back where guaranteed or explicitly documented as requiring caller-managed staging.
- Stream ownership and cleanup follow documented behavior.
- Failure handling does not expose private keys, PINs, passwords, or sensitive cryptographic material through diagnostics.

Transactional guarantees must be accurately stated. The library is not required to provide atomic publication to an object store, but it must not imply such a guarantee when it does not exist.

## 12. G10: API, build, and operational readiness

The library must be suitable for consumption by other .NET applications.

**Acceptance:**

- Public API is documented.
- The documented operations compile and execute as examples.
- All advertised target frameworks build successfully.
- Automated tests pass on the maintained target-runtime matrix.
- Independent verification jobs fail or explicitly report unexecuted checks when required tools are unavailable.
- Runtime package dependencies match the settled set in `AGENTS.md`.
- No accidental dependency on test-time PDF tools exists in the shipped package.
- Package versioning and compatibility expectations are documented.
- Known limitations and resource constraints are discoverable.
- Reproducible release build and package verification are demonstrated.

No requirement exists for an elaborate framework or fluent API.

## 13. Defect policy

A release candidate cannot be production-qualified with:

- Any known unresolved Critical defect.
- Any known unresolved High defect affecting a production-supported operation.
- Any unexplained independent interoperability failure in the supported corpus.
- Any known false-positive success or signature-validity result.
- Any known silent document corruption.
- Any unmet mandatory acceptance gate.

Medium and Low defects may be accepted when they do not violate an explicit production guarantee and their impact, workaround, and release disposition are documented.

Unsupported-feature rejections are not automatically defects. They are defects when they contradict advertised support or behave unsafely.

## 14. Qualification is capability-specific

Maintain a status for each supported capability.

| Capability | Qualification evidence required |
|---|---|
| Field extraction | Foreign input fixtures, independently checked values and geometry |
| Field population | Logical correctness plus independent rendering |
| Metadata | Independent round-trip verification |
| Attachments | Independent extraction and byte equality |
| Incremental writing | Independent structural validation and preservation tests |
| PDF signing | Independent cryptographic, trust, coverage, and permission checks |
| Timestamping | Real TSA integration and independent token validation |
| Large-file support | Measured memory, I/O, and resource behavior |

A capability may be qualified even when an unrelated capability remains experimental.

However, the library must not be marketed as production-qualified for an incomplete feature.

## 15. Release decision

The QA Engineer owns the evidence inventory and verification results.

The Technical Lead owns technical review, integration quality, and engineering disposition of defects.

The Product Manager owns acceptance tracking, release-scope recommendations, and confirmation that the required gates have been addressed.

The human product owner retains final release authority.

A release decision must include:

1. The exact supported-capability matrix.
2. The status of each mandatory qualification gate.
3. Independent verification results and relevant tool versions.
4. Outstanding defects and documented limitations.
5. Performance measurements.
6. Security review findings.
7. A clear recommendation: **Qualified**, **Qualified with documented limitations**, or **Not qualified**.

"Qualified with documented limitations" is acceptable only when those limitations do not violate any advertised production-supported behavior or mandatory safety gate.

## 16. Guiding principle

**Production qualification is not a statement that the library can do everything. It is evidence that the things we promise it can do are correct, safe, interoperable, and operationally reliable.**

The team should prioritize satisfying these guarantees for the narrow intended product before expanding the scope of the library.
