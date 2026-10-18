# B-10 acceptance

## Task and owner

Product Manager. Accepted B-10. Edited `agents/state/backlog.md` only. B-19 and B-20 are not accepted.

## Verified facts

QA signed off in `agents/reports/2026-10-18-qa-engineer-B-10.md`. `three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle` passed on `net8.0` and on `net6.0`, 1 passed and 0 failed each time. Both runs printed `metadata appendix 886` for the revision before the signature. 886 is under 8 KiB. The hole stayed unread, bytes read stayed under 2 MiB, and both `/ByteRange` offsets stayed past 3 GiB. The scale row in `ACCEPTANCE.md` was not changed.

The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-10-review.md`, with no defects. That review also records B-19 and B-20 on the board. They stay open.

## Handoff

B-10 is closed. B-19 and B-20 stay open.
