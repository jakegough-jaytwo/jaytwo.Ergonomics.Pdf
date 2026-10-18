# B-02. Run the full suite with the required tools

## Task and owner

QA Engineer. Read-only. No product code and no `agents/state/` edits. This report is the B-02 result. It is not product acceptance.

## Verified facts

- `dotnet test` on `test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj` executed every discovered test on `net8.0` and `net6.0`. No class filter. No skips.
- Both runs: 145 executed, 145 passed, 0 failed, 0 not run. The same 15 test classes ran on both frameworks, with the same per-class counts.
- The build that `dotnet test` performed succeeded on both frameworks. `Directory.Build.props` sets `TreatWarningsAsErrors` to true.
- Tool-backed tests passed on both frameworks, including `ForeignPdfTests` (6), `SigningInteropTests` (4), `AppearanceRenderTests` (1), `DenseMemoryTests` (1), and `InteropTests` (1). TRX outcomes for those tests are `Passed`.
- Before the run, `JAYTWO_QPDF`, `JAYTWO_POPPLER`, `JAYTWO_PKCS11`, and `JAYTWO_SOFTHSM_UTIL` were unset, and `qpdf`, `pdftotext`, and `softhsm2-util` were not on `PATH`. The process used the copies below.
- `InteropTests` locates qpdf with `where.exe qpdf` and then invokes `qpdf` from `PATH`. It does not read `JAYTWO_QPDF`. This run prepended the qpdf `bin` directory to `PATH` as well as setting `JAYTWO_QPDF`.
- Results files: `%LOCALAPPDATA%\Temp\jaytwo-b02\b02-net8.trx` and `b02-net6.trx`. Machine `JAKE-PC22`. `net8.0` run 2026-10-18 13:51:43–13:51:56 (-06:00). `net6.0` run 2026-10-18 13:52:27–13:52:39 (-06:00). Console duration 11 s each.

## Assumptions

- The framework-dependent host requested `Microsoft.NETCore.App` 8.0.0 and 6.0.0. The highest installed patches at run time were 8.0.31 and 6.0.36, so the host rolls forward to those patches. The test process did not print its own patch version.
- qpdf was only found under `%LOCALAPPDATA%\Temp\qpdf-bin\`. A later run needs that file, or another qpdf, on `PATH` and in `JAYTWO_QPDF`.
- This task does not run `netstandard2.0` or `netstandard2.1`. That remains B-06.

## Defects confirmed

None.

## Changes made or proposed

None. `dotnet test` wrote gitignored coverage under `out/coverage/` because `test/Directory.Build.props` sets `CollectCoverage`.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice). Installed `Microsoft.NETCore.App`: 6.0.36, 8.0.13, 8.0.31.

| Tool | Version | How the run found it |
|---|---|---|
| qpdf | 12.4.2 | `JAYTWO_QPDF` and `PATH`: `%LOCALAPPDATA%\Temp\qpdf-bin\qpdf-12.4.2-msvc64\bin\qpdf.exe` |
| Python | 3.13.1 (`C:\Python313\python.exe`, first on `PATH`) | `python` |
| pyHanko | 0.38.0 | import from that Python |
| pypdf | 6.21.1 | import from that Python |
| Poppler `pdftotext` and `pdftoppm` | 26.09.0 | `JAYTWO_POPPLER`: `%LOCALAPPDATA%\jaytwo-tools\poppler\poppler-26.09.0\Library\bin` |
| SoftHSM 2 | 2.5.0 (`softhsm2-util.exe --version` printed `2.5.0`) | `JAYTWO_PKCS11`: `%LOCALAPPDATA%\jaytwo-tools\softhsm\SoftHSM2\lib\softhsm2-x64.dll`. `softhsm2-util.exe` is in the sibling `bin` directory |

Commands, from the repo root, with those three variables set and the qpdf `bin` directory prepended to `PATH`:

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj --framework net8.0 --logger "trx;LogFileName=b02-net8.trx" --results-directory %LOCALAPPDATA%\Temp\jaytwo-b02 --nologo
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj --framework net6.0 --logger "trx;LogFileName=b02-net6.trx" --results-directory %LOCALAPPDATA%\Temp\jaytwo-b02 --nologo
```

Console: `Passed! - Failed: 0, Passed: 145, Skipped: 0, Total: 145` on each framework. Exit code 0.

Per class, identical on `net8.0` and `net6.0`:

| Class | Pass | Fail | Not run |
|---|---:|---:|---:|
| AcroFormTests | 13 | 0 | 0 |
| AppearanceRenderTests | 1 | 0 | 0 |
| AttachmentTests | 3 | 0 | 0 |
| DenseMemoryTests | 1 | 0 | 0 |
| EncryptionTests | 33 | 0 | 0 |
| ForeignPdfTests | 6 | 0 | 0 |
| InteropTests | 1 | 0 | 0 |
| LzwTests | 14 | 0 | 0 |
| MetadataTests | 8 | 0 | 0 |
| ObjectStreamCacheTests | 1 | 0 | 0 |
| OpenTests | 23 | 0 | 0 |
| RunLengthTests | 14 | 0 | 0 |
| ScaleTests | 3 | 0 | 0 |
| SignatureTests | 20 | 0 | 0 |
| SigningInteropTests | 4 | 0 | 0 |
| **Total** | **145** | **0** | **0** |

## Handoff

Technical Lead verifies this run. QA then does B-03. Product Manager does not accept anything from this report alone.
