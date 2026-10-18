# B-28 Bound the seven confirmed parser paths

## 1. Task and owner

Core Engineer. Bound the seven parser paths confirmed by B-26 in `PdfParser.cs` and `PdfEngine.cs`, with regressions in `OpenTests.cs`. No parser rewrite. G8 is not passed, and B-18 remains open.

## 2. Verified facts

1. Consecutive comments are handled iteratively and refused after 256, rather than recursively consuming the process stack.
2. Distinct `/Prev` and hybrid-xref traversal is refused after 256 active xref sections; the existing repeated-offset check remains.
3. `Load` records in-progress objects and refuses a cycle or a dependency depth above 256. This covers indirect `/Length` cycles and re-entry through object streams.
4. `Resolve` records object ids and refuses cycles and chains above 256 references.
5. Field-tree traversal records field ids and has a depth cap.
6. Embedded-file name-tree traversal records node ids separately from file-spec ids and has a depth cap.
7. Odd `/Index` arrays, unsupported `/W` widths, overflowing object-stream counts, and invalid object-stream header integers throw `PdfParseException`.

Each regression asserts the original stream remains byte-for-byte unchanged after refusal.

## 6. Changes made or proposed

- `src/jaytwo.Ergonomics.Pdf/Internal/PdfParser.cs`: bounded comment handling and checked xref index/width reads.
- `src/jaytwo.Ergonomics.Pdf/Internal/PdfEngine.cs`: xref, object-load, reference, field-tree, and name-tree guards; checked object-stream headers.
- `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs`: seven regression facts covering all findings, including both cyclic stream forms and four malformed integer forms.

The pre-existing `RequireOption` change in `PdfEngine.cs` and the pre-existing header-boundary test in `OpenTests.cs` were preserved and are not B-28 changes.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`.

- Focused B-28 filter: `net8.0` passed 7/7; `net6.0` passed 7/7.
- All `OpenTests`: `net8.0` passed 31/31; `net6.0` passed 31/31.
- Full test project with qpdf 12.4.2, Poppler 26.09.0, and SoftHSM 2 paths configured: `net8.0` passed 171/171; `net6.0` passed 171/171.
- `dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj --nologo --no-restore`: succeeded for `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0`, with 0 warnings and 0 errors.
- `git diff --check`: no whitespace errors.

Evidence level: internally tested. This is not the required fuzz run and does not pass G8.

## 9. Handoff

QA Engineer independently verifies the seven regressions. Technical Lead reviews the shared `PdfEngine.cs` changes. Product Manager keeps B-18 open and G8 unpassed.
