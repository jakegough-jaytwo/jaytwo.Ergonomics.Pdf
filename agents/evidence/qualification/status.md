# Qualification status

Regenerated under W-62, 2026-10-20, against `QUALIFICATION.md` as revised for Phase 1 (D-38). The previous status (B-22, 2026-10-18) is in git history.

No gate is passed. No capability is production-qualified. Under `QUALIFICATION.md` section 3, the gate redefinition expires all earlier gate evidence. The prior evidence named below is the starting point for each gate: it counts again only when re-run and recorded against the revised gate at a named commit. A missing or unexecuted check is not a pass.

| Gate | Prior evidence (expired, reusable when re-run) | Newly required by the revised gate |
| --- | --- | --- |
| G1 | `README.md` support matrix separating read from modify compatibility (B-12). | Matrix entries for every Phase 1 capability as it ships; every tolerated quirk; the PDF/A non-claim (added under W-62). |
| G2 | Six committed foreign fixtures from pypdf, qpdf, and pyHanko; `qpdf --check` on modified output. | At least 30 files, at least 5 in each of the four categories, two thirds from tools other than the test generators, a manifest entry per file, and a manifest validator. Rescoped W-13. |
| G3 | Positive and negative tests for extraction, fill, metadata, attachments, and incremental writes; pypdf read Info, XMP, and attachment bytes (B-23). | Tests for every new Phase 1 operation; the cross-cutting write checks (encryption, prefix, failure, certification, usage-rights cases, sync and async equivalence). |
| G4 | pyHanko on signed output, including SoftHSM, intermediate chains, and certification levels; the throwing-signer check (B-19). | Poppler `pdfsig` as the second PDF-aware verifier (W-14); ECDSA P-256 and P-384; SHA-384 and SHA-512; PAdES B-B; the async signer; flatten and rotate after an approval signature. |
| G5 | pyHanko trusted an embedded token; OpenSSL 3.2.4 document timestamp verified by pyHanko and `openssl ts -verify` (B-15); imprint, algorithm, and nonce rejections. | A CMS signature timestamp from an authority other than the code under test, verified by pyHanko and `openssl ts -verify`; PAdES B-T validated against the profile if claimed. |
| G6 | Poppler `pdftotext` and `pdftoppm` checks of fill appearances (B-16). | MuPDF as a required second renderer; flatten output; auto-size and `/MK` colors (W-47). |
| G7 | Sparse 3 GiB; dense 1 GiB; 250 MiB attachment; 500-page form, within the D-20 budgets (B-17); 886-byte metadata appendix (B-10). | 1 GiB compressed attachment; 500-field bulk fill; 1,000-page flatten; signature inspection and revision listing on 1 GiB without reading signed ranges; repeated use and disposal; encrypted xref-stream discovery; owner-approved budgets for each new workload; a timing baseline. |
| G8 | Fuzz harness, fixed-seed smoke run, and a first campaign triage (`fuzz-2026-10-19-triage.md`); fixed parser and signer findings (W-28 to W-41). | One campaign after every Phase 1 input surface lands, with a predeclared allocation or stop condition per target, complete triage, and reruns from fixed code (W-18); one independent review of the parser and signer. |
| G9 | Failure tests: non-seekable input, size caps, signer and TSA failures leave the destination unchanged (B-19); diagnostics omit secrets (W-25). | Fault-injecting destination tests for both restoration branches; cancellation of async writes; flatten failure. |
| G10 | 309/309 tests on Windows `net8.0`, `net6.0`, `net48` and Linux `net8.0`, `net6.0`; package 0.1.0 with only `System.Memory` on `netstandard2.0` (B-20). | Target frameworks `netstandard2.0`, `net8.0`, `net10.0` (item 2); `README.md` examples compiled and run; semantic versioning documented; a reproducible build and package check. |

The release-gate summary is to be generated from the evidence inventory once the inventory and its freshness check are re-homed under `pdf-change-verification` (reset plan B1, item 3). Until then this file is maintained by hand.
