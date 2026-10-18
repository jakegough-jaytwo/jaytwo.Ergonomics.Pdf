# B-22 and B-23 board

## 1. Task and owner

Technical Lead. Boarded the evidence-inventory refresh and the independent metadata reader. No `backlog.md` edit and no product code. No qualification gate is passed.

## 2. Verified facts

The board had no active rows. B-22 and B-23 are in `agents/state/backlog.md`. Their files do not overlap.

B-13, B-14, B-15, B-17, and B-18 stay off the board. They still need a fourth PDF producer, a second PDF-aware signature verifier, a timestamp authority that is not pyHanko, an approved memory budget, or a long fuzz run.

## 6. Changes made or proposed

`agents/state/board.md` has two active rows.

B-22. QA Engineer. The evidence inventory and the release-readiness note in `agents/state/status.md` only. Bring those sections up to date with B-04, B-09, B-10, B-11, B-12, B-16, B-19, B-20, and B-21. No gate becomes passed. A missing tool stays "not run." Do not edit `ACCEPTANCE.md`. Verifier: Technical Lead.

B-23. Core Engineer. `test/jaytwo.Ergonomics.Pdf.Tests/InteropTests.cs` only. No `src/`, no new fixture file, and no new package. Use pypdf 6.21.1 or qpdf 12.4.2 to confirm an Info edit, an XMP edit, and exact attachment bytes. `qpdf --check` is not that reader. If a check fails, stop with a defect report. Do not pass G3. Do not edit `README.md` or `ACCEPTANCE.md`. Verifier: QA Engineer.

## 9. Handoff

QA writes the inventory. The Core Engineer writes the reader check. This report does not pass a gate.
