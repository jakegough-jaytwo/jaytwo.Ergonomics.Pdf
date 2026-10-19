# B-32 review

## 1. Task and owner

Technical Lead. Review of B-32, "Honor `JAYTWO_QPDF` in the qpdf interop test", uncommitted on `wip`. I am also verifying it, because QA implemented it. Inputs: `2026-10-18-tech-lead-B-32-board.md` and `2026-10-18-qa-engineer-B-32.md`. G8 is not passed.

## 2. Verified facts

- `git diff test/jaytwo.Ergonomics.Pdf.Tests/InteropTests.cs` matches the brief:
  - qpdf is located once with `ExternalTool.Locate("JAYTWO_QPDF", "qpdf.exe")`, and the located path is logged.
  - All four `Check` calls run `ExternalTool.Start(qpdf, "--check", path)`. No qpdf call by name remains.
  - `TryLocateQpdf`, `ProbeQpdf`, the local `Start`, and `using System.ComponentModel` were removed. `System.Diagnostics` is still used by `RunPython`.
  - A missing tool fails through `Locate`'s assertion and does not skip.
- No other B-32 file changed. The other modified files in the tree belong to B-30 and B-31, which are already accepted.
- QA's evidence covers every configuration in the brief, on `net8.0` and `net6.0`:
  - **A, `JAYTWO_QPDF` only:** red on today's code; passes with the fix and logs the located path.
  - **B, `PATH` only:** passes before and after.
  - **C, neither:** fails, with 0 skipped and "qpdf.exe was not found. Set JAYTWO_QPDF or put it on PATH."
  - `JAYTWO_TOOLS` was unset in every run. The build gave 0 warnings.

## 7. Verification run

I ran configuration A independently on `net8.0` with .NET SDK 10.0.100-rc.1 and qpdf 12.4.2:

- **Environment:** `PATH` had every qpdf entry removed, and `where.exe qpdf` exited 1. `JAYTWO_QPDF` was set to `C:\Users\Jake\AppData\Local\Temp\qpdf-bin\qpdf-12.4.2-msvc64\bin\qpdf.exe`. `JAYTWO_TOOLS` was unset.
- **Command:** `dotnet test test/jaytwo.Ergonomics.Pdf.Tests --filter "FullyQualifiedName~InteropTests.qpdf_checks_built_incremental_filled_and_signed_pdfs" -f net8.0 --logger "console;verbosity=detailed"`.
- **Result:** 1 of 1 passed. The output logged `qpdf C:\Users\Jake\AppData\Local\Temp\qpdf-bin\qpdf-12.4.2-msvc64\bin\qpdf.exe`.

I did not rerun configurations B and C, or `net6.0`. Those results rest on QA's runs.

## 6. Changes made

- Approved.
- `agents/state/board.md`: I removed the B-32 row and released its claim. The board notes that B-32 awaits Product Manager acceptance.

## 9. Handoff

The Product Manager accepts B-32 or returns it. Then `agents/state/status.md`, Repository section, should drop the `JAYTWO_QPDF` note, which belongs to the state-file owner. Commit after acceptance. G8 is not passed.
