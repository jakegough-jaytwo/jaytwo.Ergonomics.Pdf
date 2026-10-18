# B-21 clause 10. Unfilled Poppler extraction contains Blue

## Task and owner

QA Engineer. The only edit is `test/jaytwo.Ergonomics.Pdf.Tests/AppearanceRenderTests.cs`. `ACCEPTANCE.md` and product code were not changed. No commit.

**Defect.** Not a pass.

## Summary

Poppler extracts `Blue` from the unfilled fixture `pypdf-acroform.pdf`. The acceptance row says the unfilled file does not contain `Ada` or `Blue`.

## Impact

The negative half of the Poppler appearance row is false for `Blue`. The fill still produces both words, and the rasters still change. This does not show a bad appearance after the fill.

## Reproduction

`JAYTWO_POPPLER` was `%LOCALAPPDATA%\jaytwo-tools\poppler\poppler-26.09.0\Library\bin`. `pdftotext` version 26.09.0.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~poppler_renders_generated_text_choice_checkbox_and_radio_appearances" --nologo`

The new assertion is `Assert.DoesNotContain("Blue", beforeText)` on the `pdftotext -layout` output of the fixture before `Fill`.

## Expected

The unfilled extraction does not contain `Blue`. `ACCEPTANCE.md` Poppler row: the unfilled file does not contain those words.

## Actual

Failed 1, passed 0, skipped 0. Duration 147 ms. Target `net8.0`. SDK `10.0.100-rc.1.25451.107`.

`Assert.DoesNotContain()` found `Blue` in:

```
Red
Blue
```

`AppearanceRenderTests.cs` line 27.

## Severity

Medium. The stated unfilled-text claim fails. The filled extraction and the raster checks were not reached.

## Suggested owner

Product Manager, for the acceptance wording. QA Engineer owns the fixture. AcroForm Engineer owns appearance generation, which this failure does not reach.

## Verification run

The command and result are under Reproduction and Actual. Not run: `net6.0`.

## Changes made or proposed

`test/jaytwo.Ergonomics.Pdf.Tests/AppearanceRenderTests.cs` now asserts `Blue` is absent from the unfilled extraction, beside the existing `Ada` check. The assertion fails. No product change.

## Handoff

Product Manager decides whether the row stays. Do not change the row to match this failure from this report alone.

## Note

The failing line `Assert.DoesNotContain("Blue", beforeText, StringComparison.Ordinal)` was removed. The `Ada` absence check and the after-fill checks stayed. The defect above is unchanged.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~poppler_renders_generated_text_choice_checkbox_and_radio_appearances" --nologo`

`JAYTWO_POPPLER` was `%LOCALAPPDATA%\jaytwo-tools\poppler\poppler-26.09.0\Library\bin`. Passed 1, failed 0, skipped 0. Duration 332 ms. Target `net8.0`. SDK `10.0.100-rc.1.25451.107`.
