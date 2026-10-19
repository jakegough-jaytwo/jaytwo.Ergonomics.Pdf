# B-32 acceptance

## 1. Task and owner

Product Manager. Accept or return B-32, "Honor `JAYTWO_QPDF` in the qpdf interop test", against `agents/state/backlog.md` and `team.md` section 8. Uncommitted on `wip`.

## 2. Verified facts

- `git diff` of `InteropTests.cs` shows qpdf located through `ExternalTool.Locate("JAYTWO_QPDF", "qpdf.exe")`. All four `Check` calls run from that path, and no call by name remains.
- **Criteria** (source: `agents/reports/2026-10-18-qa-engineer-B-32.md`, `net8.0` and `net6.0`):
  - With `JAYTWO_QPDF` only, the test was red before the fix and passes after it.
  - With `PATH` only, it passes.
  - With neither, it fails, with no skip, with a message that names `JAYTWO_QPDF` and `PATH`.
  - The Technical Lead independently reproduced the `JAYTWO_QPDF`-only pass on `net8.0` (`agents/reports/2026-10-18-tech-lead-B-32-review.md`).
- **Section 8:**
  1. Met at the internally tested level.
  2. The test project built with 0 warnings. `src/` is unchanged.
  3. The configuration A run is the regression evidence.
  4. A missing tool fails and does not skip.
  5. No `README.md` or `ACCEPTANCE.md` change was needed.
  6. The limitations are below.
  7. The Technical Lead reviewed and verified in place of QA, as the board set, because QA implemented the change.

## 5. Evidence missing

- The `PATH`-only and neither cases, and every `net6.0` run, rest on the implementer's runs alone.
- The full suite was not run.

## 6. Changes made

`agents/state/backlog.md`: B-32 is closed, and the header paragraph is updated.

## 9. Handoff

- B-32 is accepted. The Technical Lead commits.
- The owner of `agents/state/status.md` removes the stale `JAYTWO_QPDF` note in the Repository section.
