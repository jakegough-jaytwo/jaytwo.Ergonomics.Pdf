# B-21 AcroForm assertions

## 1. Task and owner

AcroForm Engineer. Add the three AcroForm assertions from B-21. No `src/` edits. `ACCEPTANCE.md` was not changed.

## 2. Verified facts

All three clauses passed on `net8.0`. No defect report.

- Kids inherit `/DA`. `kids_inherit_default_appearance` fills `Person`. `/DA` is only on the parent. The kid's rectangle is 20 points tall, so a missing `/DA` would size the font at 14. The appendix content stream contains `/Helv 12 Tf` and `0 0 1 rg`, the parent's default appearance.
- A check box with no appearance gets a generated `Yes`/`Off` appearance. `bare_check_box_appearance_is_named_yes_and_off` asserts `Generated`, the normal appearance keys `/Yes` and `/Off`, and on reopen `ExportValue` `Yes`, value `Yes`, and `IsChecked`.
- A radio mark is cubic Bezier curves, and `arc` is not an operator. `radio_mark_is_cubic_curves_without_arc` fills a single radio that has no appearance. The appendix contains the `c` operator and does not contain `arc`.

## 6. Changes made or proposed

`test/jaytwo.Ergonomics.Pdf.Tests/AcroFormTests.cs` only. The three facts above.

## 7. Verification run

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~kids_inherit_default_appearance|FullyQualifiedName~bare_check_box_appearance_is_named_yes_and_off|FullyQualifiedName~radio_mark_is_cubic_curves_without_arc" --nologo
```

Result: Passed. Failed 0, Passed 3, Skipped 0, Total 3. Duration 62 ms. `jaytwo.Ergonomics.Pdf.Tests.dll` (`net8.0`).

## 9. Handoff

QA Engineer verifies these assertions.
