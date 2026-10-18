# B-23. Independent reader for Info, XMP, and attachment bytes

## Task and owner

QA Engineer. Read-only except this report. `InteropTests.cs` and product code were not edited. No commit.

**Signed off** for the three properties. **G3 is not passed.**

## Verified facts

`pypdf_reads_an_info_edit_an_xmp_edit_and_exact_attachment_bytes` requires all three, exactly:

- Info title `InfoEdited`, from `PdfReader.metadata.title`.
- XMP `dc:title` `x-default` `XmpEdited`, from `xmp_metadata.dc_title`.
- `notes.bin` bytes equal to `00 01 02 7F 80 FE FF` then ASCII `B-23` (`0001027F80FEFF422D3233`).

The reader program is inlined and sent to `C:\Python313\python.exe` on stdin. This test does not call `qpdf --check`.

`git diff -- src` is one line in `PdfEngine.RequireOption` from the earlier choice-field fix. It is not an Info, XMP, or attachment change for this task.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --nologo --filter "FullyQualifiedName~pypdf_reads_an_info_edit_an_xmp_edit_and_exact_attachment_bytes" --logger "console;verbosity=detailed"`

Passed 1, failed 0. Duration 930 ms. Host `net8.0` 8.0.31. Printed `pypdf 6.21.1`, `Info title InfoEdited`, `XMP dc:title XmpEdited`, `attachment 0001027f80feff422d3233`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --nologo --filter "FullyQualifiedName~pypdf_reads_an_info_edit_an_xmp_edit_and_exact_attachment_bytes" --logger "console;verbosity=detailed"`

Passed 1, failed 0. Duration 781 ms. Host `net6.0` 6.0.36. Printed the same four lines.

Not run: the rest of `InteropTests`, and the rest of the suite.

## Changes made or proposed

This report only.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone. G3 stays unpassed.
