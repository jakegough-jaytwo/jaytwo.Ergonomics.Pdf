# B-16 Remaining G6 appearance checks

## 1. Task and owner

AcroForm Engineer. Poppler checks for the `QUALIFICATION.md` section 8 cases the existing row did not cover. No `src/` edit. `README.md`, `ACCEPTANCE.md`, and `agents/state/` were not edited. G6 is not passed.

## 2. Verified facts

Poppler 26.09.0, `pdftotext` and `pdftoppm` from `%LOCALAPPDATA%\jaytwo-tools\poppler\poppler-26.09.0\Library\bin`. `pdftotext -v` prints `pdftotext version 26.09.0`. `pdftocairo` was not used. The test still finds Poppler through `JAYTWO_POPPLER` or `pdftotext` on `PATH`, and a missing tool fails the test.

The existing fact `poppler_renders_generated_text_choice_checkbox_and_radio_appearances` is unchanged in what it checks: unfilled text has no `Ada`, filled text has `Ada` and `Blue`, and the four rectangles change. That fact still covers checkbox and radio by the raster change.

`poppler_checks_visibility_encoding_alignment_clipping_rotation_and_crop` builds the PDF with `PdfBuilder` and fills it. No new fixture file.

Section 8 cases covered, all by this Poppler build:

- Text visibility. `Seen` is absent before the fill and present after. The field rectangle changes in the 72 dpi raster.
- Supported encodings. `Café` and `A•B` (WinAnsi) are extracted.
- Left, center, and right alignment. The three `Align` fields are each `[40 340]`. The test requires the left word's XMin in 30–60, the center word's XMin in 174–180, and the right word's XMin in 280–340.
- Long values. `ABCDEFGHIJKLMNOPQRSTUVWXYZ` is extracted whole. The tall field's `One`, `Two`, and `Three` are extracted.
- Checkbox and radio. Kept on the existing fact. Not repeated.
- Field boundary clipping. `WIDEMARK` in a 50-point field leaves ink inside the rectangle and none in the band just to its right.
- Rotated pages. `Rotated` is extracted. The 72 dpi raster is 792 by 612, and the filled page has ink the blank page does not.
- CropBox offsets. `pdftoppm -cropbox` renders the crop, 400 by 540, not the 612 by 792 media box. `Cropped` is extracted. The leftmost ink is in the crop-relative x range 30–70, not at the media-box x.
- Representative font sizes and field dimensions. Word-box height for 18-point `Large` is more than 1.5 times the box for 8-point `Small`. The run also uses 12-point and 14-point fields, a 50-point-wide field, a 480-point-wide field, and a 120-point-tall field.

No case was stopped. None needed a library change or a new fixture file.

Not covered, and why G6 stays unpassed: section 8 says a second renderer is desirable. This task does not add one. `pdftocairo` is not that renderer. The gate's acceptance is wider than this initial set.

## 6. Changes made or proposed

`test/jaytwo.Ergonomics.Pdf.Tests/AppearanceRenderTests.cs` only. The new fact and the helpers it uses. `pdftotext` stdout is read as UTF-8 so the WinAnsi text is not decoded with the console code page.

## 7. Verification run

`JAYTWO_POPPLER` was the Poppler 26.09.0 `Library\bin` directory. `pdftotext -v` → `pdftotext version 26.09.0`.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~AppearanceRenderTests" --nologo
```

Passed. Failed 0, Passed 2, Skipped 0, Total 2. Duration 600 ms. `jaytwo.Ergonomics.Pdf.Tests.dll` (`net8.0`).

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~AppearanceRenderTests" --nologo
```

Passed. Failed 0, Passed 2, Skipped 0, Total 2. Duration 536 ms. `jaytwo.Ergonomics.Pdf.Tests.dll` (`net6.0`).

## 9. Handoff

QA Engineer verifies. G6 is not passed.
