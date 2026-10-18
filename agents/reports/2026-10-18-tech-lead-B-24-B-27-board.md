# B-24 through B-27 board

## 1. Task and owner

Technical Lead. Boarded the G3 inventory update, the secret-diagnostic checks, and the two read-only reviews. No `backlog.md` edit and no product code. No qualification gate is passed.

## 2. Verified facts

The board had no active rows. B-24, B-25, B-26, and B-27 are in `agents/state/backlog.md`. Their claims do not overlap.

B-13, B-14, B-15, B-17, and B-18 stay off the board. B-26 and B-27 do not replace the B-18 fuzz run.

## 6. Changes made or proposed

`agents/state/board.md` has four active rows.

B-24. QA Engineer. The G3 row and the metadata and attachment capability rows in `agents/state/status.md` only. Name pypdf 6.21.1, Info title `InfoEdited`, XMP `dc:title` `XmpEdited`, and the exact `notes.bin` bytes. G3 stays not passed. Do not edit the release-readiness note or `ACCEPTANCE.md`. Verifier: Technical Lead.

B-25. Core Engineer. `EncryptionTests.cs` and `SignatureTests.cs` only. No `src/`. A failing open and a failing sign must show that the diagnostic text does not contain the password, the PIN, or key material. The test output must not print those values. If a diagnostic contains one, stop with a defect report. Do not pass G9. Verifier: QA Engineer.

B-26. Signing Engineer. Read-only review of the parser. No `src/` and no tests. Report confirmed defects, or state that none were confirmed, with the files read. Do not pass G8. This is not the fuzz run. Verifier: Technical Lead.

B-27. AcroForm Engineer. Read-only review of the signer. No `src/` and no tests. Report confirmed defects, or state that none were confirmed, with the files read. Do not pass G8. Verifier: Technical Lead.

## 9. Handoff

Each seat works inside its claim. This report does not pass G3, G8, or G9.
