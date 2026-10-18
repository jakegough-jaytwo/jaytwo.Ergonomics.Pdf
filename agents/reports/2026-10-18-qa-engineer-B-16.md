# B-16. Remaining appearance checks

## Task and owner

QA Engineer. Read-only except this report. No product-code edits. `AppearanceRenderTests.cs` was not edited. No commit.

**Not signed off.** The alignment claim is looser than the report. The other listed properties are established. **G6 is not passed.**

## Verified facts

Poppler 26.09.0. `pdftotext -v` prints `pdftotext version 26.09.0`. The test uses `pdftotext` and `pdftoppm`. `pdftocairo` is not called.

`poppler_renders_generated_text_choice_checkbox_and_radio_appearances` still asserts that unfilled text has no `Ada`, filled text has `Ada` and `Blue`, and the four rectangles change.

These assertions in `poppler_checks_visibility_encoding_alignment_clipping_rotation_and_crop` match the report:

- `Seen` is absent before the fill and present after. `ChangedPixels` covers the field rectangle `[72 700 280 724]`.
- `Café` (`U+00E9`) and `A•B` (`U+2022`) are extracted. `pdftotext` stdout is read as UTF-8.
- `ABCDEFGHIJKLMNOPQRSTUVWXYZ` is extracted whole. `One`, `Two`, and `Three` are extracted.
- `WIDEMARK` is in a 50-point field, x 40–90. The raster requires ink in x 44–86 and none in the band x 94–140.
- `Rotated` is extracted. Page 2 is `/Rotate 90`. The 72 dpi raster is 792 by 612. The blank page has fewer than 5 dark pixels and the filled page has more than 20.
- `pdftoppm -cropbox` renders page 3 at 400 by 540, the crop `[80 100 480 640]`, not the 612 by 792 media box. `Cropped` is extracted. The leftmost ink is in crop-relative x 30–70.
- The 18-point `Large` word box is more than 1.5 times the height of the 8-point `Small` word box.

The alignment claim is not established at the reported precision. The three `Align` fields are each 300 points wide, from x 40 to 340, with quadding 0, 1, and 2. The test only requires the first word box in x 30–60, the third in x 280–340, and the middle more than 80 points from each neighbor. A middle start from just over 110 to just under 260 can pass. That does not require the reported starts near 42, 177, and 311.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice). `JAYTWO_POPPLER` was `%LOCALAPPDATA%\jaytwo-tools\poppler\poppler-26.09.0\Library\bin`.

`pdftotext -v` → `pdftotext version 26.09.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~AppearanceRenderTests" --nologo`

Passed 2, failed 0, skipped 0. Duration 499 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~AppearanceRenderTests" --nologo`

Passed 2, failed 0, skipped 0. Duration 540 ms. Target `net6.0`.

## Changes made or proposed

This report only.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone. G6 stays unpassed. A second renderer was not added.
