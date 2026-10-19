# Technical Lead review: B-39

## Task and owner

B-39, FieldMDP follows ISO 32000 (D-24, audit SA-08). The Technical Lead implemented it, because the defect was a two-line inversion. QA verified in `2026-10-19-qa-engineer-B-39.md`, with its Re-verification section. This review checks the final integration.

## Verified facts

- `FieldMdpBlocks` in `PdfEngine.cs` follows ISO 32000-1 12.8.2.4. `Include` locks the named fields, and `Exclude` locks every field except them. A named field covers its descendants (the `Names` helper matches the full name or any prefix that ends at a dot), which is how pyHanko applies it. An unknown `/Action` now locks every field, the same fail-closed rule as an unresolved certification. `All` is unchanged. No public API changed.
- QA-B39-01 (a listed parent did not lock its children, so the library allowed a fill that pyHanko rejected as an illegal modification) and the unknown-action part of O1 are folded into B-39 under D-22.
- Evidence: `AcroFormTests` covers exact names, near misses, empty lists, parent names, and the DocMDP `P` 2 combination. pyHanko 0.38.0 independently confirms five lock specifications in `pyhanko_fieldmdp_include_and_exclude_lock_the_same_fields_and_allowed_fills_keep_the_signature`. The library locks the same fields pyHanko does, and every fill the library allows leaves the original signature intact with `docmdp_ok` and `FORM_FILLING`. The new tests failed against the pre-fix engine.
- `README.md` (two places) and `ACCEPTANCE.md` describe the corrected rule. The only criterion changed is the reversed text, which was wrong.

## Evidence missing

O1 remainder (Low): `Include` with no `/Fields` locks nothing. ISO 32000 requires the key and does not define its absence.

## Handoff

Approved. Product Manager to accept.
