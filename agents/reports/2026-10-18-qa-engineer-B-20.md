# B-20. netstandard builds and package verification

## Task and owner

QA Engineer. Read-only except this report. No `src/`, `test/`, `README.md`, `ACCEPTANCE.md`, or `agents/state/` edits. No target was dropped. No package was added.

This is the G10 build and package record. **G10 is not passed.**

## Verified facts

- `jaytwo.Ergonomics.Pdf.csproj` still targets `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0`.
- Debug builds of `netstandard2.0` and `netstandard2.1` each succeeded with 0 warnings and 0 errors.
- `dotnet pack` uses Release. It rebuilt all four target frameworks and wrote `jaytwo.Ergonomics.Pdf.0.1.0.nupkg` and `jaytwo.Ergonomics.Pdf.0.1.0.snupkg`.
- The nupkg contains `lib/net8.0`, `lib/net6.0`, `lib/netstandard2.1`, and `lib/netstandard2.0`, each with the assembly and its XML doc file, plus `README.md`.
- Nuspec dependency groups: `net6.0` empty, `net8.0` empty, `netstandard2.1` empty, `netstandard2.0` has `System.Memory` 4.5.5 only. That is the settled runtime dependency.
- The nuspec and the package file list do not name qpdf, pyHanko, Poppler, SoftHSM, or Pkcs11Interop. Those tools are not dependencies of the shipped package. `Pkcs11Interop` remains a test-project reference only. `StyleCop.Analyzers` is not in the nuspec.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj -f netstandard2.0 --nologo`

Build succeeded. 0 Warning(s). 0 Error(s). Time 00:00:01.00. Output `src/jaytwo.Ergonomics.Pdf/bin/Debug/netstandard2.0/jaytwo.Ergonomics.Pdf.dll`.

`dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj -f netstandard2.1 --nologo`

Build succeeded. 0 Warning(s). 0 Error(s). Time 00:00:00.99. Output `src/jaytwo.Ergonomics.Pdf/bin/Debug/netstandard2.1/jaytwo.Ergonomics.Pdf.dll`.

`dotnet pack src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj --nologo -o %TEMP%\jaytwo-b20`

Succeeded. Release builds of `net6.0`, `netstandard2.0`, `netstandard2.1`, and `net8.0`. Package `C:\Users\Jake\AppData\Local\Temp\jaytwo-b20\jaytwo.Ergonomics.Pdf.0.1.0.nupkg` (317261 bytes) and the matching `.snupkg` (161260 bytes).

Not run, and not counted: the test suite on `netstandard2.0` or `netstandard2.1`. The test project targets `net8.0` and `net6.0` only.

## Changes made or proposed

This report only. Pack wrote Release outputs under `src/jaytwo.Ergonomics.Pdf/bin/Release/` and the packages under `%TEMP%\jaytwo-b20`.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone. G10 stays unpassed.
