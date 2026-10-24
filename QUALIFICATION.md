# Production Qualification Standard

jaytwo.Pdf and jaytwo.Ergonomics.Pdf. Owner: human owner. Adopted 2026-10-18; revised for the Phase 1 scope under D-38; release track suspended under D-54.

`docs/spec/phase-1.md` states the target behavior and `ACCEPTANCE.md` the shipped behavior. This document states the evidence a release needs before that behavior can be called production-qualified. Where it disagrees with `AGENTS.md`, `AGENTS.md` wins. Phase 2 capabilities get their gates when the owner adopts `docs/spec/phase-2.md`.

**Suspended (D-54).** No release is being qualified during the architectural reset. Evidence under `agents/evidence/` was recorded against the pre-reset project and API. It is historical input and does not count toward a gate for `jaytwo.Pdf` or the new API. The decision that adopts packaging and versioning for both libraries re-adopts these gates. Until then, work items verify under D-53, and the gates below describe the eventual bar.

## 1. Definition

A capability is **Production-Qualified** when it has passed every applicable gate in this document. That means it is suitable for real enterprise documents **within its documented support boundaries**. It does not mean that every valid PDF is supported, every PDF feature is implemented, every malformed PDF can be recovered, every viewer renders identically, or every performance characteristic is optimal.

The library is **Production-Qualified 1.0** only when every capability `docs/spec/phase-1.md` advertises passes its gates. A capability that is not qualified is not advertised as production-supported.

## 2. Gates

| Gate | Requirement | Release blocking? |
| --- | --- | --- |
| G1 | Documented capability and compatibility boundaries | Yes |
| G2 | Interoperability with real-world PDFs | Yes |
| G3 | Correctness of supported operations | Yes |
| G4 | Independent signature verification | Yes |
| G5 | Independent RFC 3161 timestamp verification | Yes, for timestamped signing |
| G6 | Independent rendering of generated appearances | Yes, for fill and flatten |
| G7 | Large-file resource qualification | Yes |
| G8 | Security and adversarial-input testing | Yes |
| G9 | Failure handling and output integrity | Yes |
| G10 | API, build, and documentation | Yes |

A gate passes on recorded evidence, not developer confidence. A check that did not run is not a pass.

## 3. Evidence and freshness

Evidence records name the commit, framework, build configuration, environment, tool versions, fixtures with their hashes, the command, the property established, and the result: pass, fail, not run, not applicable, or expired. They live under `agents/evidence/`.

Evidence expires when the relevant production code, test oracle, fixture, tool configuration, or gate definition changes. Expired evidence does not count toward a gate.

`pdf-change-verification` maintains the evidence inventory and its freshness check. The release-gate summary is generated from that inventory; it is not maintained as a second board.

This inventory is maintained while qualifying a release or executing an explicit qualification work item. Routine feature and defect work records its own focused verification in the work item and does not refresh gate status, generate a report, or rerun qualification evidence unless the owner requests it.

## 4. G1: Documented support boundaries

`README.md` carries a support matrix for shipped behavior. It separates read compatibility from modification compatibility; opening a signed PDF does not establish that the library can safely modify it. It names supported PDF versions and structures, encryption, field types and appearances, signing algorithms and formats, metadata and attachment operations, resource limits, every tolerated writer quirk, and what is unsupported. It states that no PDF/A conformance is claimed.

**Acceptance:** every advertised capability has explicit supported behavior; known limitations are documented; unsupported operations fail predictably; no known case silently produces an incorrect result while reporting success.

## 5. G2: Interoperability with real-world PDFs

**Corpus:**

- At least 30 distinct, meaningful files. No padding with trivial variants.
- At least 5 in each category: business and government forms (including hybrid XFA, Reader-extended, and signed forms); Office and browser exports (Word, Excel, LibreOffice, Chrome or Edge); scans and image-heavy files (including linearized files); generated reports (including thousands of pages).
- At least two thirds authored by tools other than the repository's own test generators (pypdf, qpdf, pyHanko). A file those tools rewrite or sign is recorded as transformed, not as a new producer.
- Coverage of classic and stream cross-references, object streams, hybrid files, multiple revisions, inherited page attributes, supported encryption, form and signature fields, metadata, and embedded files.

Each file has a manifest entry: ID and SHA-256, provenance and acquisition date, license and redistributability, producer and version, transformations, PDF version and features, supported or unsupported classification with justification, the operations run, expected results, and the validators with the property each establishes. A file that cannot be committed is fetched by a script with a pinned hash; when it is unavailable, its checks report not run.

**Acceptance:**

- Every file classified as supported passes its operations' correctness checks.
- Every deliberately unsupported file fails predictably and without corruption.
- Modified output passes `qpdf --check` without unexpected warnings, and at least one other independent implementation confirms the relevant object semantics. `qpdf --check` alone is not proof of content or visual correctness.
- The original bytes stay a prefix of every incremental output.
- Each interoperability failure is fixed, excluded from the support matrix with justification, or a release blocker.

## 6. G3: Correctness of supported operations

Every Phase 1 operation has positive, negative, and independent-reader tests (pypdf, qpdf, Poppler, or pyHanko, for the property each tool exposes reliably):

- **Reads:** pages and boxes; fields with names, types, values, widgets, pages, and geometry, including hierarchical fields, merged and separate widgets, rotated pages, and nonzero origins; form-data export; Info and XMP; attachments with relationship and dates; signature inspection; revision listing, including linearized files.
- **Form writes:** single and bulk fill, where a bulk fill with one invalid value changes nothing; values, appearance states, and appearances agree; hybrid XFA removal; flatten of all and of named fields.
- **Other writes:** rotation; Info and XMP edits with unrelated metadata intact; attachment add, replace, and remove with exact byte equality, including large binaries and the compressed form; catalog version raising up to 1.7 only.

Every write also covers: encrypted input under the owner and the user password, including refusal by the permission bits; new strings and streams encrypted with the document's cipher, with `/Encrypt` and `/ID` kept; the original bytes kept as a prefix and the destination unchanged on every documented failure; certification, approval-signature, FieldMDP, and usage-rights behavior where it applies. Usage-rights cases include permitted, absent, unresolvable, malformed, unknown, and insufficient `/UR3`. On an unsigned document, except for a structural parse failure, every non-permitting case removes `/UR3` in the same revision and proceeds when password permissions, DocMDP, and FieldMDP otherwise allow the write; on a document that already has a signature, every write leaves `/Perms` unchanged (spec §7.3). Async writes also demonstrate sync/async equivalence and cancellation leaving the destination unchanged.

**Acceptance:** no known case silently produces incorrect values, geometry, bytes, or unintended modifications.

## 7. G4: Independent signature verification

Two PDF-aware verifiers check signed output: pyHanko and Poppler `pdfsig`. `SignedCms` or OpenSSL may add CMS checks; a CMS-only check does not count as one of the two.

**Scenarios:** a new and an existing empty signature field; RSA and ECDSA P-256 and P-384; SHA-256, SHA-384, and SHA-512; `adbe.pkcs7.detached` and PAdES baseline B-B; correct signed attributes for each format; `/ByteRange` and revision coverage; certificate-chain inclusion; trust under explicitly configured test anchors; signing through SoftHSM; signing encrypted PDFs; the async signer; existing signatures preserved by permitted later writes; prohibited writes refused; tampered data rejected by the verifiers; signer failure. A flatten or rotation after an approval signature or document timestamp, with the caller's opt-in, leaves the earlier signature's bytes cryptographically intact while a verifier reports the later modification.

For earlier signatures, report integrity, coverage, trust, and modification-policy compliance separately. Signature inspection is checked against the verifiers' reports for the same files.

**Acceptance:** every positive signed-output scenario passes both verifiers; both verifiers reject each tampered case; refusal and failure scenarios meet their prescribed negative assertions without producing a completed output; the signed bytes are exactly those intended; the signer certificate matches the key; no false success for an invalid signature or a prohibited write; no known cryptographic correctness defect is open. A successful CMS digest check alone is insufficient.

## 8. G5: RFC 3161 timestamps

A signature timestamp and a document timestamp are distinct capabilities. Each is tested against a timestamp authority other than the code under test.

**Acceptance:** a genuine RFC 3161 response is obtained and embedded; its imprint matches the signature value (for a signature timestamp) or the signed ranges (for a document timestamp); pyHanko and `openssl ts -verify` verify it under an explicit test trust configuration; the library rejects malformed responses, wrong imprints, wrong digest algorithms, and mismatched nonces; a TSA failure never yields a reported success. A timestamped PAdES B-B signature is claimed as PAdES B-T only when independently validated against that profile. PAdES B-LT and B-LTA are not claimed.

## 9. G6: Rendering of generated appearances

Poppler and MuPDF render the output of fill and of flatten. Cover visibility, WinAnsi characters, alignment, auto-sized and long values, check box and radio states, clipping, `/MK` colors, rotated pages, crop-box offsets, and flatten placement through the appearance `/Matrix` and `/BBox`.

**Acceptance:** every supported field type renders its value correctly in both renderers, filled and flattened; no missing, displaced, wrongly selected, or unexpectedly clipped appearance remains in the supported matrix; supported fields never depend on a viewer generating appearances; values the library cannot draw are reported, and flatten and signing refuse them. Pixel-identical rendering is not required.

## 10. G7: Large-file resources

Workloads use real file-backed inputs. Sparse files alone are insufficient.

| Workload | Objective |
| --- | --- |
| Sparse PDF of at least 3 GiB | Correct 64-bit offsets, reads, saves, and signature coverage |
| Dense PDF of at least 1 GiB | Inspect, edit, and sign without whole-document materialization |
| 1 GiB PDF | Signature inspection and revision listing without reading signed ranges |
| Attachment of at least 1 GiB | Streaming add with compression, and extraction |
| 500-page form | Extraction, bulk fill of 500 fields, and signing |
| 1,000-page form | Flatten |
| Repeated object-stream access, and repeated open and dispose | No retained-memory growth |
| Encrypted xref-stream PDF | No full-file discovery scan |

Budgets are approved by the owner and recorded with the workload in `agents/evidence/qualification/memory-budgets.md` (D-20 sets the ceilings). Each new workload gets its approved budget before it counts. Measurements use Release builds, coverage off, and process isolation where counters are process-wide. Timings are recorded as a repeatable baseline, not as targets; a material regression is investigated.

**Acceptance:** no operation loads the whole input into memory; peak memory stays within the approved budgets; memory does not grow across repeated use and disposal; large attachments stream; output limits and decompression limits are documented and fail safely; no serious performance regression is unexplained. The 8 MiB signature appendix and the classic-xref offset limit remain where documented.

## 11. G8: Security and adversarial input

The library parses untrusted input. Fuzzing covers the parser and every Phase 1 input surface: object parsing, cross-references, object streams, filters and predictors, encryption, field walks, XMP, attachment parameters, DER and CMS (including signature inspection), RFC 3161 tokens, usage-rights parameters, XFA detection, and tolerated quirks.

**Campaign:** one campaign within D-23's aggregate cap, run after every Phase 1 input surface has landed. Its plan predeclares a bounded allocation or stop condition for every target, with per-input time, memory, and size limits. The campaign is complete only when every target reaches its allocation or condition (otherwise the gate records that target as failed or incomplete), every artifact is triaged, and affected targets are rerun from fixed code. Its record names targets, seeds and corpus hashes, limits, coverage or health observations, and triage and rerun results. There is no fixed minimum duration. A smaller fixed-seed smoke suite runs in CI.

**Review:** an agent or person who did not implement the code reviews the parser, decoders, PDF encryption, CMS and DER handling, signature construction, and their failure paths.

**Acceptance:** no known unmitigated Critical or High security defect affects a supported operation; no reproducible uncontrolled memory growth, infinite loop, or crash in the qualified corpus; confirmed defects become permanent regression cases; input limits are documented. Resource-limit exceptions are acceptable. Uncontrolled failure is not.

## 12. G9: Failure and output integrity

Test failures during reading, parsing, object resolution, incremental writing, attachment copying, flattening, external signing, timestamping, cancellation of async operations, and disposal.

**Acceptance:** inputs stay unchanged unless an API documents in-place appending; no operation reports success when output finalization fails; a failed or cancelled write leaves the destination unchanged, except when the destination stream itself fails (for example an I/O error or a full disk), in which case the library attempts to restore the destination's original length, rethrows, and `README.md` documents that this restoration cannot be guaranteed; no invalid output is presented as a completed signed document; stream ownership follows documented behavior; diagnostics never expose private keys, PINs, passwords, or other secrets. The library does not provide atomic publication to an object store and must not imply that it does. A fault-injecting destination verifies both branches: when writing fails after N bytes and length restoration remains available, the original length is restored; when restoration also fails, the operation still reports failure and the documented limitation applies.

## 13. G10: API, build, and documentation

**Acceptance:** the public API is documented; `README.md` examples compile and run; every advertised target framework builds with warnings as errors; tests pass on the maintained runtime matrix; verification jobs report unexecuted checks when tools are missing; runtime dependencies match `AGENTS.md`; the package carries no test-tool dependency; semantic versioning applies from 1.0 and is documented; limits are discoverable; a release build and package verification are reproducible.

## 14. Defect policy

A release candidate is not production-qualified with any known unresolved Critical defect, any unresolved High defect affecting a supported operation, any unexplained interoperability failure in the supported corpus, any known false success or false signature-validity result, any known silent corruption, or any unmet gate.

Medium and Low defects may be accepted when they violate no explicit guarantee and their impact, workaround, and disposition are documented. Rejecting an unsupported feature is not a defect unless it contradicts advertised support or behaves unsafely.

## 15. Capabilities

Each capability carries its own status. A capability may be qualified while an unrelated one is not, but an incomplete capability is never advertised as production-qualified.

| Capability | Evidence required |
| --- | --- |
| Field and page reads, form-data export | Corpus files with independently checked values and geometry |
| Fill and bulk fill | Logical correctness, atomicity, and rendering in two renderers |
| Flatten | Placement and content in two renderers; signature and certification behavior |
| Rotation | Independent reader and renderer checks; signature behavior |
| Metadata | Independent round trip |
| Attachments | Independent extraction, byte equality, relationship, dates, and checksum |
| Incremental writing | Structural validation, prefix preservation, version rules |
| Encrypted documents | Owner and user password coverage for every operation |
| Usage rights and XFA | Kept or removed exactly as the spec states, on real forms |
| Signing | Two PDF-aware verifiers across algorithms, digests, and formats |
| Signature inspection and revisions | Agreement with independent verifiers and readers |
| Timestamping | Real TSA integration and independent token validation |
| Large files | Measured memory, I/O, and timing baselines |

## 16. Release decision

The release decision is built from the generated gate summary and includes: the supported-capability matrix; the status of every gate; independent verification results with tool versions; outstanding defects and documented limitations; performance measurements; security review findings; and a recommendation of **Qualified**, **Qualified with documented limitations**, or **Not qualified**. "Qualified with documented limitations" is acceptable only when no limitation violates advertised behavior or a safety gate.

Skills do not turn self-review into independent review. Any gate that requires an independent reviewer needs a person or a fresh context that did not implement the affected code. The owner holds final release authority.

**Production qualification is not a statement that the library can do everything. It is evidence that the things we promise it can do are correct, safe, interoperable, and operationally reliable.**
