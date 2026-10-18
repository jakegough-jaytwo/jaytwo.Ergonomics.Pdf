# B-29 acceptance

## 1. Task and owner

Product Manager. Accept or return B-29. Record the unbounded TSA response buffer. G8 is not passed. B-18 stays open.

## 2. Verified facts

- **Boarding.** `2026-10-18-tech-lead-B-29-board.md`.
- **Signing Engineer** (`2026-10-18-signing-engineer-B-29.md`). The committed negative-status test passed even with its fix reverted. The engineer tightened it to assert the refusal phrase. The `SignatureTests.cs` change is uncommitted.
- **QA** (`2026-10-18-qa-engineer-B-29.md`). Signed off. `SignatureTests` passed 31/31 on `net8.0` and `net6.0`. With only its own fix reverted, each test fails on both frameworks. Each test leaves the stream bytes equal to the input.
- **Technical Lead** (`2026-10-18-tech-lead-B-29-review.md`). Approved. The four library targets built with 0 warnings and 0 errors.
- **`team.md` section 8:**
  1. Both criteria are met at the internally tested level.
  2. The build is clean.
  3. Both B-27 defects have regression tests that discriminate.
  4. Both inputs throw `PdfTimestampException` before the appendix is written.
  5. `README.md` already states status 0 or 1. `ACCEPTANCE.md` was not changed, and no criterion was weakened.
  6. The limitations are recorded.
  7. QA signed off, the Technical Lead approved, and the Product Manager accepts here.

**Accepted.**

## 6. Changes made

`agents/state/backlog.md`:
- Header line updated.
- B-29 closed.
- B-31 added: cap the `HttpTimestampAuthority` response size. It goes to the Signing Engineer. It is not boarded and not started.

B-31 is a separate item and is not folded into B-30. B-30 is the Core Engineer's parser and decoder fix. B-31 is in the signer, which the Signing Engineer owns.

## 9. Handoff

- Orchestrator: commit the `SignatureTests.cs` tightening with B-29.
- Technical Lead: release the B-29 row and its claims on the board.
