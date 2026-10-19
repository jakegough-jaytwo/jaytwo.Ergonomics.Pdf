# B-32 QA implementation

## 1. Task and owner

QA Engineer, as the implementer. The task is B-32, "Honor `JAYTWO_QPDF` in the qpdf interop test", within `agents/reports/2026-10-18-tech-lead-B-32-board.md`. The work is uncommitted on `wip`. The Technical Lead reviews it, because QA is the implementer. G8 is not passed.

## 2. Verified facts

- On today's code, configuration A fails on both frameworks with "qpdf is not on PATH. where.exe qpdf exited 1." In that configuration, `JAYTWO_QPDF` points at `qpdf.exe` and qpdf is off `PATH`.
- With the fix, configuration A passes and logs `qpdf <JAYTWO_QPDF path>`. qpdf is not on `PATH` in that run, so all four `qpdf --check` calls ran from the located path.
- Configuration B, with qpdf on `PATH` only, passes on both frameworks, before and after the fix. It logs the location that `where.exe` returned.
- Configuration C, with neither available, fails without skipping (1 total, 1 failed, 0 skipped) with "qpdf.exe was not found. Set JAYTWO_QPDF or put it on PATH."

## 6. Changes made

`test/jaytwo.Ergonomics.Pdf.Tests/InteropTests.cs` only:

- The test calls `ExternalTool.Locate("JAYTWO_QPDF", "qpdf.exe")` and writes the path to the test output.
- `Check(qpdf, path)` runs `ExternalTool.Start(qpdf, "--check", path)`.
- I removed `TryLocateQpdf`, `ProbeQpdf`, the now-unused local `Start`, and `using System.ComponentModel`.

`git diff --stat` for this task shows 1 file changed, 9 insertions and 79 deletions. The other modified files in the tree belong to B-30 and B-31, and I did not touch them. There is no `src/`, `ExternalTool.cs`, or package change, and no `ACCEPTANCE.md` row changed.

## 7. Verification run

Environment:

- .NET SDK 10.0.100-rc.1.25451.107 and qpdf 12.4.2 at `%LOCALAPPDATA%\Temp\qpdf-bin\qpdf-12.4.2-msvc64\bin`.
- `JAYTWO_TOOLS` was unset in every run.
- `PATH` was set for the `dotnet test` process only. The base `PATH` has no qpdf entry.

Build: `dotnet build test/jaytwo.Ergonomics.Pdf.Tests --no-incremental` gave 0 warnings and 0 errors.

Test command: `dotnet test test/jaytwo.Ergonomics.Pdf.Tests --filter "FullyQualifiedName~InteropTests.qpdf_checks_built_incremental_filled_and_signed_pdfs" -f <tfm> --logger "console;verbosity=detailed"`.

| Config | `where.exe qpdf` | Today's code (net8.0 / net6.0) | With fix (net8.0 / net6.0) |
|---|---|---|---|
| A: `JAYTWO_QPDF` only | exit 1 | Fail / Fail | Pass / Pass, logs the `JAYTWO_QPDF` path |
| B: `PATH` only | exit 0, the qpdf path | Pass / Pass | Pass / Pass, logs the `PATH` location |
| C: neither | exit 1 | Fail / Fail | Fail / Fail, "Set JAYTWO_QPDF or put it on PATH." |

The full suite was not run. `pypdf_reads_...` in the same class was built but not run.

## 9. Handoff

The Technical Lead reviews the evidence, then the Product Manager accepts. G8 is not passed.
