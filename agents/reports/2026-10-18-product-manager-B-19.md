# B-19 acceptance

## Task and owner

Product Manager. Accepted B-19. Edited `agents/state/backlog.md` only. B-12, B-20, and the remaining B-21 clauses are not closed.

## Verified facts

QA signed off in `agents/reports/2026-10-18-qa-engineer-B-19.md`. Both new signature failure tests passed 2/2 on `net8.0` and on `net6.0`. A throwing `IPdfExternalSigner` leaves the destination bytes unchanged. A throwing timestamp authority throws `PdfTimestampException`, leaves the bytes unchanged, and a reopen finds no signature field. This is not the B-04 imprint, digest-algorithm, or nonce check.

The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-19-review.md`, with no defects. That review had not seen the `net6.0` run. The QA report supplies it.

Gates G4, G5, and G9 are not passed.

## Handoff

B-19 is closed. B-12, B-20, and the remaining B-21 clauses stay open.
