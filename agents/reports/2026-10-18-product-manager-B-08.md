# B-08 acceptance

## Task and owner

Product Manager. Accepted B-08. Edited `agents/state/backlog.md` only. B-09, B-10, and B-11 are not accepted.

## Verified facts

QA signed off in `agents/reports/2026-10-18-qa-engineer-B-08.md`. `AcroFormTests` passed 14/14 on `net8.0` and on `net6.0`. A choice field with no `/Opt` and Edit clear throws `PdfFieldValueException` and does not dirty the document. `SetChoice("Editable", "custom")` still returns `Generated` when the Edit flag is set.

The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-08-review.md`, with no defects. The only product change in `RequireOption` is the empty-list early return.

`git diff HEAD -- ACCEPTANCE.md` is the accepted B-04 nonce rows. It does not touch the choice sentence. That sentence still says a value that is not listed throws `PdfFieldValueException` and does not dirty the document, unless the Edit flag is set.

## Handoff

B-08 is closed. B-09, B-10, and B-11 stay open.
