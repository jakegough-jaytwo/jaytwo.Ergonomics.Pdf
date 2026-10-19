# B-32 boarding

## 1. Task and owner

Technical Lead. Board B-32, "Honor `JAYTWO_QPDF` in the qpdf interop test". Implementer: QA Engineer. QA owns the external-tool harness. QA cannot verify its own change independently, so the Technical Lead reviews the evidence. Branch `wip`, no worktree. No code edits. No test runs. No commit. G8 is not passed.

## 2. Verified facts

These facts come from reading the code. I did not execute it.

- `InteropTests.qpdf_checks_built_incremental_filled_and_signed_pdfs` (`InteropTests.cs:45`) asserts `TryLocateQpdf` (`:196`). That method runs `where.exe qpdf`, and on a `Win32Exception` it probes `qpdf --version` by name (`ProbeQpdf`, `:224`). `Check` (`:242`) runs `qpdf --check` by name. These are the only qpdf invocations in the file. `JAYTWO_QPDF` is never read.
- A missing qpdf already fails the test through `Assert.True`, not by skipping. The local variable named `skip` is misleading.
- `ExternalTool.Locate(variable, fileName)` (`ExternalTool.cs`) checks three places in order: the variable when its file exists, then `JAYTWO_TOOLS\<fileName>`, then `where.exe <fileName>`. When none of them finds qpdf, it fails with "qpdf.exe was not found. Set JAYTWO_QPDF or put it on PATH." That message names both, as the acceptance criteria require. `DenseMemoryTests` already uses it for qpdf. Reusing it is enough. B-32 needs no new helper and no change to `ExternalTool.cs`.
- B-32 is in the charter. It is test-only, adds no package, and leaves which tests require qpdf unchanged.

## 6. Changes proposed

`test/jaytwo.Ergonomics.Pdf.Tests/InteropTests.cs` only:

- At the start of the test, call `string qpdf = ExternalTool.Locate("JAYTWO_QPDF", "qpdf.exe");` and write it with `_output.WriteLine("qpdf " + qpdf)`.
- Pass that path to `Check`, so all four `qpdf --check` calls run from it. `ExternalTool.Start` and `ExternalTool.Output` may replace the local process code. Keep the local `Start` if anything else still uses it.
- Delete `TryLocateQpdf` and `ProbeQpdf`. Remove any `using` directive left unused, such as `System.ComponentModel`, if nothing else needs it.
- Do not change `ExternalTool.cs`, `ForeignPdfTests.cs`, `DenseMemoryTests.cs`, `src/`, or the fixtures generator. If the change needs another file, stop and report.

## Evidence required

Run each configuration on `net8.0` and `net6.0` with this command:

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests --filter "FullyQualifiedName~InteropTests.qpdf_checks_built_incremental_filled_and_signed_pdfs" -f <tfm> --logger "console;verbosity=detailed"`

For every run, set `PATH` for the `dotnet test` process only. Record `where.exe qpdf` under that `PATH`. Unset `JAYTWO_TOOLS` in every run, because `Locate` also checks it and it would otherwise mask configuration C.

| Configuration | Environment | Expected with the fix | Expected on today's code |
|---|---|---|---|
| A | `JAYTWO_QPDF` set to `qpdf.exe`. The qpdf directory is removed from `PATH`, and `where.exe qpdf` fails. | Pass. The output shows the `JAYTWO_QPDF` path. | Fail. This is the red-first evidence. |
| B | `JAYTWO_QPDF` unset. qpdf is on `PATH`. | Pass. The output shows the `PATH` location. | Pass. |
| C | Neither is available. `JAYTWO_QPDF` and `JAYTWO_TOOLS` are unset, and qpdf is off `PATH`. | Fail, not skip, with the message naming `JAYTWO_QPDF` and `PATH`. | Fail. |

Also record the following:

- the qpdf version, from `qpdf --version`;
- the build, `dotnet build test/jaytwo.Ergonomics.Pdf.Tests` with 0 warnings;
- the `git diff --stat` showing only `InteropTests.cs` changed.

Do not run the full suite.

## 9. Handoff

- **QA Engineer:** implement within `InteropTests.cs` and write the report with the evidence table above.
- **Technical Lead:** review.
- **Product Manager:** accept.

G8 is not passed.
