# Task board

Owner: Technical Lead. One row per active task. A task may change only the files listed for it. As of 2026-10-18.

## Active

| Task | Implementer | May change | Branch or worktree | Waits on | Verifier |
|---|---|---|---|---|---|
| B-25 Failure diagnostics omit secrets, returned | Core Engineer | `test/jaytwo.Ergonomics.Pdf.Tests/EncryptionTests.cs` and `SignatureTests.cs` only. No `src/`. The narrowed password and file-key checks passed QA recheck. Technical Lead review refused approval (`agents/reports/2026-10-18-tech-lead-B-25-review.md`): the PIN criterion cannot be met by a test-only change, because no PIN reaches the `ThrowingSigner` failure path. Do not pass G9. | `wip` | Product Manager and QA Engineer: re-scope the PIN criterion or cite accepted prior evidence, then Technical Lead re-review | Product Manager |
| B-28 Bound the seven confirmed parser paths, returned | Core Engineer | `src/jaytwo.Ergonomics.Pdf/Internal/PdfParser.cs`, `src/jaytwo.Ergonomics.Pdf/Internal/PdfEngine.cs`, and `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs` only. Review found unchecked narrowing in xref `/Index` values and object-stream `/N` and `/First`. Add regressions and normalize these malformed values to `PdfParseException`, then repeat QA and Technical Lead review. Do not pass G8. | `wip` | Core Engineer rework | QA Engineer |

## Resume point

Checkpointed 2026-10-18. The mixed B-04 through B-29 working tree is committed on `wip`, so later tasks can be cut as worktrees under `team.md` section 5. At the checkpoint the full suite passed 174/174 on `net8.0` and 174/174 on `net6.0` with qpdf 12.4.2, Poppler 26.09.0, and SoftHSM 2 configured. The library built for all four targets with 0 warnings. No code changed after the B-25 and B-28 reviews.

Next actions, in order:

1. Core Engineer: B-28 rework on `/Index`, `/N`, and `/First` per `agents/reports/2026-10-18-tech-lead-B-28-review.md`. Then QA verification and Technical Lead re-review.
2. Product Manager with QA Engineer: decide the B-25 PIN criterion. Then Technical Lead re-review and Product Manager acceptance. That releases `SignatureTests.cs`.
3. Technical Lead: once B-25 releases `SignatureTests.cs`, board B-29. Its proposed code and tests are already committed. Verify them, do not rewrite them.

## Kickoff plan

Under decision D-11:

1. B-02 and the three B-05 audits are finished. The planning report is `agents/reports/2026-10-18-tech-lead-planning.md`.
2. The Product Manager confirms or reorders `state/backlog.md` from that report.
3. B-01 acceptance is met on `wip`. B-03, B-06, and B-08 through B-12, B-16, and B-19 through B-27 are done. B-25 was returned by Technical Lead review on the PIN criterion. B-28 was returned for the unchecked narrowing paths recorded above. B-29 is not boarded, although its proposed code and tests are already committed in the `wip` checkpoint; do not extend or accept them until B-25 releases `SignatureTests.cs` and the Technical Lead boards B-29. B-18 remains open. G8 and G9 stay unpassed. No qualification gate is passed.
4. B-13, B-14, B-15, B-17, and B-18 stay off the board. B-13 needs a fourth PDF producer. B-14 needs a second PDF-aware signature verifier. B-15 needs a timestamp authority that is not pyHanko. B-17 needs an approved memory budget. B-18 needs a long fuzz run. `pdfsig.exe` is not installed. The Poppler 26.09.0 build has `pdftotext` and `pdftoppm` and does not include `pdfsig`. Python 3.13 has pypdf and pyHanko only.

## Shared-file claims

| Claim | Task |
|---|---|
| `test/jaytwo.Ergonomics.Pdf.Tests/EncryptionTests.cs`, `SignatureTests.cs` | B-25 |
| `src/jaytwo.Ergonomics.Pdf/Internal/PdfParser.cs`, `PdfEngine.cs`, and `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs` | B-28 |

Nobody claims `README.md`, `ACCEPTANCE.md`, `agents/state/status.md`, `agents/state/backlog.md`, the signer files, `InteropTests.cs`, `AppearanceRenderTests.cs`, `AcroFormTests.cs`, `ScaleTests.cs`, `MetadataTests.cs`, `StandardEncryptor.cs`, or `PdfBuilder.cs`. B-27 changes nothing. The claims do not overlap.
