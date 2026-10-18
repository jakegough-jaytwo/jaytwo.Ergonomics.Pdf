# B-16 board

## 1. Task and owner

Technical Lead. Boarded the remaining G6 appearance checks. No product code, `README.md`, `ACCEPTANCE.md`, or `backlog.md` edit. No qualification gate is passed.

## 2. Verified facts

`agents/reports/2026-10-18-product-manager-B-21-clauses-8-9.md` accepts clauses 8 and 9 and says B-21 is fully closed. That row is off the board. `SignatureTests.cs` is released.

`poppler_renders_generated_text_choice_checkbox_and_radio_appearances` already uses Poppler `pdftotext` and `pdftoppm`. Unfilled text does not contain `Ada`. Filled text contains `Ada` and `Blue`. The raster changes in four rectangles. Those checks stay.

`QUALIFICATION.md` section 8 still asks for supported encodings, left, center, and right alignment, long values, clipping, rotated pages, CropBox offsets, and representative font sizes and field dimensions. The existing row does not establish those. Checkbox and radio already have a raster change, and that change stays.

This machine has Poppler 26.09.0 `pdftotext` and `pdftoppm`. It does not have `pdfsig`. `pdftocairo` is not a second renderer. Python 3.13 has pypdf and pyHanko only. reportlab, fitz, pikepdf, pdfrw, and borb are not installed.

## 6. Changes made or proposed

`agents/state/board.md` has one active row.

B-16. AcroForm Engineer. `test/jaytwo.Ergonomics.Pdf.Tests/AppearanceRenderTests.cs` only. No `src/`. Add Poppler `pdftotext` or `pdftoppm` checks for the section 8 cases the existing test does not already cover. Keep the `Ada`, `Blue`, and raster checks. A missing Poppler tool fails the test. If a case needs a library change or a new fixture file, stop and report that case. Do not edit `README.md` or `ACCEPTANCE.md`. Do not mark G6 passed.

B-13, B-14, B-15, B-17, and B-18 are not boarded. They are blocked on a missing producer, a missing verifier, a missing timestamp authority, an unapproved memory budget, or a long fuzz run.

## 9. Handoff

The AcroForm Engineer implements B-16 inside that file. QA verifies it. This report does not pass G6.
