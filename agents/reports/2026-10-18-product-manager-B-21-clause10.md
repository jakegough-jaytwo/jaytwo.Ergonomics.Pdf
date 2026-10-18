# B-21 clause 10

## Task and owner

Product Manager. Corrected the Poppler row in `ACCEPTANCE.md`. Clause 10 of B-21 is closed. The other B-21 clauses stay open.

## Verified facts

`agents/reports/2026-10-18-qa-engineer-B-21-clause10.md`: Poppler 26.09.0 `pdftotext` of unfilled `pypdf-acroform.pdf` prints `Red` and `Blue`. `Ada` is absent. The library did not write `Blue`. The after-fill extraction and the raster checks were not the failure.

Sentence replaced:

The unfilled file does not contain those words.

Sentence written:

The unfilled file does not contain `Ada`, and `Blue` is already present.

The after-fill claim and the four raster checks stay in the same row.

## Handoff

Clauses 1–3 and 5–9 of B-21 remain open.
