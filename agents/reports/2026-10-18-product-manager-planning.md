# Product Manager planning

## Task and owner

Product Manager. Confirm the backlog from the Technical Lead's planning report. No product code. `README.md`, `ACCEPTANCE.md`, `status.md`, and `board.md` were not edited.

## Verified facts

The Technical Lead's report is `agents/reports/2026-10-18-tech-lead-planning.md`. B-02 is accepted there: 145 passed on `net8.0` and on `net6.0`. B-03 is done. Its report records the evidence inventory in `status.md`. B-04 is on the board. The Signing Engineer is implementing the nonce overload. The existing `Create(ReadOnlySpan<byte>)` bytes stay. B-06 is in progress with QA.

`AGENTS.md` and `README.md` say password and signature values set `/NeedAppearances`. `ACCEPTANCE.md` says those fields throw `PdfFieldValueException`. `SetText` throws for the password flag and for a signature field before a write. `AcroFormTests.simple_text_generates_helvetica_and_other_values_request_appearances` asserts both throws. Comb, rich text, and non-WinAnsi set the flag.

`RequireOption` returns when the choice list is empty, including when Edit is clear. The same test fills `Choice`, which has no `/Opt`, with `Yes` and expects `Generated`.

Three acceptance clauses have no assertion, from the Core audit and B-03: an info edit refreshing `ModDate` in UTC; the 3 GiB metadata appendix under 8 KiB; a revision 4 stream cipher different from the string cipher. `revision4_can_mix_aes_strings_with_rc4_streams` asserts the title only, and that file has no stream.

D-O4 and the five D-15 rewordings were not reopened. No qualification-gate item was added.

## Assumptions

A choice value is "not listed" when `/Opt` is absent. That is the reading that makes B-08 a defect. The other reading stays recorded in the Technical Lead's report and is not adopted here.

## Defects confirmed

B-08 is a candidate defect against the existing choice sentence. The password and signature disagreement is D-O5, not a defect assigned to an implementer.

## Evidence missing

B-09, B-10, and B-11. The other B-03 clauses stay unfiled until the B-06 report.

## Changes made or proposed

`agents/state/decisions.md`: added open decision D-O5. Options:

1. The charter and the README win. A password or signature value is stored, `/NeedAppearances` is set, and no Helvetica appearance is generated. Signing stays blocked while that flag is true. `ACCEPTANCE.md` and the test change to match. The wrong-setter, read-only, and `/MaxLen` throws stay.
2. The acceptance row and the code win. Password and signature fills throw `PdfFieldValueException` and do not dirty the document. The owner changes `AGENTS.md` and `README.md`. Comb, rich text, and non-WinAnsi stay on `/NeedAppearances`.

Recommendation: option 1. No document changes until the owner decides.

`agents/state/backlog.md`: B-02 and B-03 marked closed in place. B-04, B-05, and B-06 were not rewritten. New items, none on the board:

- **B-08.** Choice field with no `/Opt` and Edit clear rejects the value. The existing choice sentence stays.
- **B-09.** Evidence that an info edit refreshes `ModDate` in UTC.
- **B-10.** Evidence that the 3 GiB metadata appendix stays under 8 KiB.
- **B-11.** Evidence that a revision 4 stream uses a different cipher from its strings.

B-04 stays the first code change and claims `ACCEPTANCE.md`. B-08 through B-11 do not edit that file.

## Decisions needed

The owner decides D-O5. Recommendation is option 1.

D-O4 and the D-15 rewordings remain as already written.

## Handoff

The owner decides D-O5. The Signing Engineer continues B-04. QA continues B-06. The Technical Lead boards B-08 through B-11 later. Nothing else is accepted.
