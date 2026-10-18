# B-23 Independent reader for Info, XMP, and attachment bytes

## 1. Task and owner

Independent reader for an Info edit, an XMP edit, and exact attachment bytes. Core Engineer. `InteropTests.cs` only. No `src/` edits, no fixture script, no new package, no commit. `ACCEPTANCE.md` was not changed. G3 is not passed.

## 2. Verified facts

The reader is pypdf 6.21.1, started as `C:\Python313\python.exe`. The version command is `python -c "import pypdf; print(pypdf.__version__)"`. The reader program is inlined in `InteropTests` and passed on stdin (`python - <pdf>`). `qpdf --check` was not used.

The PDF is built with `PdfBuilder.Basic`, then one incremental save sets the Info title to `InfoEdited`, replaces the XMP packet so `dc:title` `x-default` is `XmpEdited`, and adds `notes.bin`. pypdf reported:

- Info title: `InfoEdited`
- XMP `dc:title` `x-default`: `XmpEdited`
- `notes.bin`: `0001027F80FEFF422D3233` (`00 01 02 7F 80 FE FF` then ASCII `B-23`)

Test: `InteropTests.pypdf_reads_an_info_edit_an_xmp_edit_and_exact_attachment_bytes`.

## 6. Changes made or proposed

`test/jaytwo.Ergonomics.Pdf.Tests/InteropTests.cs`: the reader check above. No other file.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`. pypdf 6.21.1.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --nologo --filter FullyQualifiedName~pypdf_reads_an_info_edit_an_xmp_edit_and_exact_attachment_bytes --logger "console;verbosity=detailed"
```

`net8.0` host `8.0.31`. Passed 1, Failed 0. The test took 788 ms.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --nologo --filter FullyQualifiedName~pypdf_reads_an_info_edit_an_xmp_edit_and_exact_attachment_bytes --logger "console;verbosity=detailed"
```

`net6.0` host `6.0.36`. Passed 1, Failed 0. The test took 728 ms.

Both runs printed `pypdf 6.21.1`, `Info title InfoEdited`, `XMP dc:title XmpEdited`, and `attachment 0001027f80feff422d3233`.

Evidence level: independently validated for these three properties. G3 is not passed. The rest of the suite was not run.

## 9. Handoff

QA Engineer verifies this test. The check passed, so there is no defect report.
