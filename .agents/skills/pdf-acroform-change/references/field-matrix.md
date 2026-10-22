# Field matrix

A checklist of field topology, inheritance, and appearance variants that real producers emit. For each row a change touches, name the test that covers it and, for an appearance, the rendered property and the independent renderer that checks it (Poppler `pdftotext`/`pdftoppm`; MuPDF `mutool draw` as a second renderer).

## Topology

- Terminal and nonterminal fields; kids without `/T` are widgets of their parent.
- Widget kids that carry field entries such as `/FT` (LibreOffice) are still widgets: the value is read from and written to the field dictionary.
- Merged field/widget dictionaries, separate widgets, and several widgets for one field.
- Widgets reached through `/P` and widgets reachable only through page `/Annots`.
- Dotted and Unicode partial names. Duplicate fully qualified names are reported, not guessed.
- `/Kids` and `/Parent` cycles and excessive depth are rejected.

## Inheritance

`/FT`, `/V`, `/DV`, `/Ff`, `/DA`, `/Q`, and `/Opt` may be inherited from an ancestor field. `/DA` and `/DR` fall back to the AcroForm dictionary.

## Buttons

- Check-box on-state comes from the widget's `/AP /N` keys, not an assumed `Yes`.
- Check boxes sharing one name with different on-state names: checking the field is refused with `PdfFieldValueException` and the document stays clean (F-2).
- `/Opt` on buttons maps widget index to export value; appearance-state names may be `0`, `1`, ... Export values come from appearance-state names; `/Opt` mapping is a documented limitation (F-3).
- Radio groups, `RadiosInUnison`, and `NoToggleToOff`.

## Choices

- Combo box versus list box (`/Ff` bit 18). Export value versus display text in `/Opt` pairs.
- `/I` (selected indices) must agree with `/V` or be removed; `/TI` is the list-box top index (F-4).
- Edit-capable combo boxes may hold a value not in `/Opt`.

## Text and appearances

- `/DA` font size zero means auto size. The generator fits height only; width fitting and `/MK` drawing are documented limitations awaiting an owner decision on widening the subset (F-5, B-47).
- Fonts outside the supported Helvetica/WinAnsi subset, comb, rich text, and non-WinAnsi values set `/NeedAppearances` instead of a generated stream.
- `/MK` border, background, and rotation; `/Rect` with a nonzero origin; widget rotation and the appearance `/Matrix`; clipping to the box.
- Pre-existing `/NeedAppearances true` is kept unless the operation documents otherwise.

## Fill invariants by field kind

| Field kind | `/V` | `/AS` | `/AP` | `/I` | `/NeedAppearances` |
|---|---|---|---|---|---|
| Text | set on the field | n/a | generated per widget in the subset | n/a | set when outside the subset |
| Check box | on-state name or `/Off` | the shared on-state or `/Off`; differing on-states refuse | kept; generated `Yes`/`Off` only when none exists | n/a | unchanged |
| Radio | export value's state name; no value: `/Off` | the matching widget on, others `/Off`; no value: every widget `/Off` | kept; generated for a single widget with no on-state | n/a | unchanged |
| Choice | export value(s); no value or an empty list: removed | n/a | generated in the subset; empty appearance for no value or an empty list | updated to match `/V`; removed for no value or an empty list | set when outside the subset |

Any row a change alters is a contract change: check it against `ACCEPTANCE.md` and `README.md` before editing.
