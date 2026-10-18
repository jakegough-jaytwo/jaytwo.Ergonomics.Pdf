# B-06 backlog

## Task and owner

Product Manager. Filed the backlog half of `agents/reports/2026-10-18-qa-engineer-B-06.md`. Edited `agents/state/backlog.md` only. No new item is on the board. No gate is marked out of scope for 1.0.

## Verified facts

The QA evidence list has ten candidates. No gate passed. No capability is production-qualified.

Merged into existing items:

- Library-side imprint, digest algorithm, and nonce checks stay in B-04. B-15 is the rest of that candidate: a timestamp authority that is not pyHanko, and an independent check of a document timestamp.
- B-03 clause 4 stays in B-10. Clause 11 stays in B-11. Clause 12 stays in B-09. B-08 through B-11 were not rewritten.

New items, each not on the board:

- **B-12.** G1 support matrix. Evidence level: the G1 document.
- **B-13.** Foreign corpus for G2. Independently validated.
- **B-14.** Second PDF-aware verifier. Independently validated. `SignedCms` does not count.
- **B-15.** Timestamp authority outside pyHanko. Independently validated.
- **B-16.** Remaining G6 appearance checks, including a second renderer for representative cases. Independently validated.
- **B-17.** G7 file-backed workloads and timing. Internally tested measurements. Budgets stay unapproved.
- **B-18.** Fixed-seed fuzz suite and an independent review of the parser and the signer. Internally tested regressions, and an independent review.
- **B-19.** Throwing `IPdfExternalSigner`, and a timestamp failure that is not reported as success. Internally tested.
- **B-20.** `netstandard2.0` and `netstandard2.1` builds, pack, and package verification. G10 build record.
- **B-21.** Remaining B-03 clauses 1, 2, 3, 5, 6, 7, 8, 9, and 10. Internally tested, except clause 10, which is independently validated with Poppler.

B-06 is closed. None of these are production-qualified.

## Handoff

The Technical Lead boards these when they are ready. QA does not sign off on release readiness. No gate is accepted.
