# B-31 acceptance, and B-32 logged

## 1. Task and owner

Product Manager. This report covers two items:

- Accept or return B-31, "Cap the timestamp response size", against `agents/state/backlog.md` and `team.md` section 8. The work is uncommitted on `wip`.
- Log B-32.

G8 is not passed.

## 2. Verified facts

- **The diff is the version QA tested.** The SHA-256 of `HttpTimestampAuthority.cs` is `d3ef690f…a8d2` and the SHA-256 of `SignatureTests.cs` is `dd8f2ee2…89ab`. These match QA's re-verification.
- **Criteria** (source: `agents/reports/2026-10-18-qa-engineer-B-31.md`):
  - A 16 MiB body throws `PdfTimestampException` after at most 2,179,072 bytes, which is the 2 MiB cap plus one 81,920-byte buffer.
  - A declared oversized body is refused after 0 bytes.
  - Through `Sign`, the stream bytes are unchanged.
  - A body of 2,097,152 bytes returns its token.
  - The existing `HttpTimestampAuthority` tests pass.
- **Section 8:**
  1. The criteria are met at the internally tested level on `net8.0` and `net6.0`. `SignatureTests` passed 37/37.
  2. The Release build is clean on all four targets.
  3. There are regression tests for the size defect and for both timeout regressions found in review. Each test was red before its fix.
  4. The refusal is `PdfTimestampException`, and the file is not extended. A timeout keeps the `TaskCanceledException` shape with an inner `TimeoutException`.
  5. `README.md` and `ACCEPTANCE.md` each gain one sentence on the cap. Nothing was removed or loosened, so no criterion was weakened.
  6. The limitations are recorded in the implementer, QA, and re-review reports.
  7. QA signed off. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-31-re-review.md`.
- **B-32 source.** `InteropTests.TryLocateQpdf` (`InteropTests.cs:196`) uses `where.exe qpdf`, and `Check` starts `qpdf` by name. `ForeignPdfTests.LocateQpdf` reads `JAYTWO_QPDF` first. `AGENTS.md` says `JAYTWO_QPDF` or `PATH`.

## 5. Evidence missing

- The .NET Framework abort, which disposes the response, is implemented, not tested.
- The full suite was not run. Acceptance rests on the targeted `SignatureTests` runs.

## 6. Changes made

`agents/state/backlog.md`:

- B-31 is closed.
- B-32, "Honor `JAYTWO_QPDF` in the qpdf interop test", is added. It is test-only, the QA Engineer implements it, and it is not boarded.
- The header paragraph is updated.

## 9. Handoff

- B-31 is accepted. The Technical Lead commits it.
- The Technical Lead boards B-32 when it is scheduled.
- B-18 stays open. G8 is not passed.
