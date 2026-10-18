# B-21 clauses 8 and 9

## Task and owner

Product Manager. Accepted clauses 8 and 9. Edited `agents/state/backlog.md` only. B-21 is fully closed.

## Verified facts

QA signed off in `agents/reports/2026-10-18-qa-engineer-B-21-clauses-8-9.md`.

Clause 8. `sign_to_another_stream_leaves_the_opened_stream_unchanged` passed on `net8.0` and on `net6.0`. The opened stream's bytes stay equal to the input. The other destination is longer, and its prefix equals the input.

Clause 9. `SignedCms.CheckSignature(verifySignatureOnly: true)` is compiled only for `net8.0`, inside `#if NET8_0_OR_GREATER`. The `System.Security.Cryptography.Pkcs` reference is `net8.0` only. No new test was added.

The Technical Lead approved clause 8 and confirmed clause 9 in `agents/reports/2026-10-18-tech-lead-B-21-sign-review.md`. That review had not seen the clause 8 `net6.0` run. The QA report supplies it.

Clauses 1, 2, 3, 5, 6, 7, and 10 were already closed. Clauses 4, 11, and 12 are B-10, B-11, and B-09.

## Handoff

B-21 is fully closed.
