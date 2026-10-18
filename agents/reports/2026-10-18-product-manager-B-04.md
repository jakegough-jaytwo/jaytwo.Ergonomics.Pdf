# B-04 acceptance

## Task and owner

Product Manager. Accepted B-04. Edited `agents/state/backlog.md` only. B-08 and B-11 are not accepted.

## Verified facts

QA signed off in `agents/reports/2026-10-18-qa-engineer-B-04.md`. `SignatureTests` passed 24/24 on `net8.0` and on `net6.0`. `pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature` passed on `net8.0`. pyHanko 0.38.0 reported the token intact, valid, and trusted.

The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-04-review.md`, with no defects. That review had not seen `SignatureTests` on `net6.0`. The QA report supplies that run. The existing `Create(ReadOnlySpan<byte>)` bytes were not changed.

`git diff HEAD -- ACCEPTANCE.md` adds three throw-and-do-not-extend rows. The request-encoding row still requires the 56-byte SHA-256("abc") vector, and now also says that vector has no nonce and that `certReq` stays omitted. No existing row was removed or loosened.

The local-TSA fact was not repeated on `net6.0`. The acceptance criterion asks for that test to pass, and it passed on `net8.0`. That is not a gap.

## Handoff

B-04 is closed. B-08 and B-11 stay open. Their reviews are not done.
