# B-21 Core and AcroForm acceptance

## Task and owner

Product Manager. Accepted the Core and AcroForm clauses of B-21. Edited `agents/state/backlog.md` only. Clauses 8 and 9 stay open. Clause 10 was already closed.

## Verified facts

QA signed off in `agents/reports/2026-10-18-qa-engineer-B-21.md`. Core passed 7/7 and AcroForm passed 3/3 on `net8.0` and on `net6.0`.

Closed:

- Clause 1. A header at offset 1016 opens. A header at offset 1024 throws `PdfParseException`.
- Clause 2. A revision 2 user password with fill-in set and annotate clear cannot fill a field, and the stream length stays unchanged.
- Clause 3. Incremental saves for RC4 revisions 2, 3, and 4, and for revision 5, repeat `/ID` `0102030405060708090A0B0C0D0E0F10`.
- Clause 5. Kids inherit `/DA`.
- Clause 6. A bare check box appearance is named `/Yes` and `/Off`.
- Clause 7. A radio mark uses the `c` operator and does not contain `arc`.

The Technical Lead approved those clauses in `agents/reports/2026-10-18-tech-lead-B-21-review.md`, with no defects.

## Handoff

Clauses 8 and 9 stay open. Clause 10 stays closed.
