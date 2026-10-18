# B-05 Audit: AcroForm

## 1. Task and owner

Read-only audit of AcroForm against `ACCEPTANCE.md`. Owner: AcroForm Engineer. No source, test, or `agents/state/` edits. The suite was not run. A test cited below is an assertion in the source, not a result.

Area: field discovery, inheritance, values and setters, appearance generation and `/NeedAppearances`, widget geometry returned to callers, and signature-field discovery. Code: `src/jaytwo.Ergonomics.Pdf/AcroForm/` and the form methods in `src/jaytwo.Ergonomics.Pdf/Internal/PdfEngine.cs`.

DocMDP and FieldMDP enforcement was not audited. Fills call `EnsureFieldChange` before they write (`PdfEngine.cs`). That is the gate.

## 2. Verified facts

`git rev-parse HEAD` is `11da851` (`genesis`). `git ls-files` lists `AcroForm/` and the AcroForm tests, and `git status --short` on those paths is empty. `agents/state/status.md` still says `wip` `HEAD` is `b832f22` and that `src/` and `test/` are untracked. The tree wins. The code-map row for this area matches: `AcroForm/`, including `AppearanceBuilder.cs`, with fills in the engine.

D-O4 is open. Widgets return the `/Rect` numbers as `PdfRect` lower-left and upper-right (`PdfField.Rect`, `PdfEngine.RectOf`). Page rotation and media box are separate properties. Nothing in this area reads `/UserUnit` or converts to a top-left viewport. That is not a defect.

### Rows that match

**No AcroForm means no fields. Widgets report dotted name, partial name, widget index, raw rectangle, 1-based page, rotation, and media box. `GetFields` returns every widget with that name.**

`CollectFields` returns an empty list when `/AcroForm` or `/Fields` is missing (`PdfEngine.cs`). Each terminal widget is one `PdfField`. `GetFields` filters `ListFields` by full name. `empty_form_has_no_fields` asserts `HasFields` is false and `Fields` is empty. `merged_split_radio_and_inherited_fields_report_geometry` asserts `Person`, partial `City`, dotted `Address.City`, radio widget indexes 0 and 1, `Rect` width 100 and `Llx` 1 from the stored numbers, page 1, rotation 90, and the inherited media box. `thousand_page_form_does_not_read_content_holes` asserts page 1000, rotation 90, and the inherited media box for `Late`. That test also asserts the content hole was not touched; the unread-hole claim is the Scale criterion, not re-audited here.

**A parent with kids is not a row. Kids inherit `/FT`, `/V`, and `/Ff`. A widget with no `/P` is found through `/Annots`. Radio widgets that share a name are separate rows. The on-state is the `/AP` `/N` key other than `Off`. A check box is checked when its value is a name other than `Off`. A radio is checked when the group value equals its on-state. `Pages` reports number, media box, crop box, and rotation. An omitted crop box is the inherited crop box, or the media box.**

`WalkField` does not add a row when `/Kids` is non-empty. Inherited `/FT`, `/V`, `/Ff`, and `/DA` are copied onto the child. Page comes from `/P` when that object is in the page tree, otherwise from the page `/Annots` index. `OnState` returns the first `/AP` `/N` key that is not `Off`. `PdfField.IsChecked` uses the check-box and radio rules above. `WalkPages` keeps an ancestor crop box and substitutes the media box when none is set; `ListPages` exposes number, media box, crop box, and rotation. The page walk is the page tree. It is included because this sentence is in the AcroForm section.

`merged_split_radio_and_inherited_fields_report_geometry` asserts `Address` is not a row, `Person` inherits value `Ada` and the multiline flag, the kid with no `/P` is on page 1, radios `Red` and `Blue` are separate rows, `Red` is checked, and `Blue` is not. The page crop box on that fixture is `[5 6 105 206]`. `crop_box_inherits_and_defaults_to_the_media_box` asserts an inherited crop box, a page that replaces it, and a page that omits crop and keeps the ancestor crop. `thousand_page_form_does_not_read_content_holes` asserts an omitted crop box equals the media box. `signature_parent_keeps_its_widget_and_receives_the_value` asserts a signature kid inherits `/FT` `/Sig` from a parent that is not itself a row.

`/DA` is inherited in `WalkField`. No test fills a child whose `/DA` exists only on the parent and checks the appearance.

**WinAnsi text, including multiline and quadding, gets a Helvetica appearance and `/NeedAppearances false`. `W` is wider than `i`. `/MaxLen` overflow throws `PdfFieldValueException` and does not dirty the document. Non-WinAnsi text and comb set `/NeedAppearances`. A WinAnsi choice value gets a Helvetica appearance of the display text. A check box with an on-state selects it. A check box with none gets a generated `Yes`/`Off` appearance. The wrong setter, read-only, and an unknown name throw the exceptions the criterion names. The wrong setter does not dirty the document.**

`SetText` builds a Helvetica form XObject when the value is WinAnsi, the field is not comb, rich text, file select, or password, and the rectangle has area. It then sets `/NeedAppearances false` unless the flag is already sticky. `AppearanceBuilder` measures with the embedded Helvetica widths. Right quadding places a wider glyph further left. Multiline wrap uses those advances. `/MaxLen` is checked before any object is edited.

`simple_text_generates_helvetica_and_other_values_request_appearances` asserts `Generated` and `/NeedAppearances false` for `Name`, `Generated` for multiline centered `Notes`, `NeedAppearances` for comb and `日本語`, `ExistingAppearanceSelected` for `Agree` (on-state `Checked`), and `Generated` for bare check box `Bare` and for choice `Choice`. The reopen asserts `Checked` and `IsChecked`. `right_aligned_w_starts_left_of_i` and `multiline_www_wraps_sooner_than_iii` assert the advance difference. `maxlen_overflow_does_not_dirty_the_document` asserts the length is unchanged. `fills_reject_the_wrong_field_type_without_writing` asserts `PdfFieldValueException` and an unchanged length. The same simple-text test asserts `PdfFieldReadOnlyException` for `Locked` and `PdfFieldNotFoundException` for `Missing`.

The `Yes` and `Off` keys are written in `TryWriteCheckBoxAppearances`. No test reads those keys back. The radio operator clause is the next gap.

**A radio mark is drawn with cubic curves. `arc` is not used.**

`AppearanceBuilder.AppendCurve` writes the `c` operator. The file does not contain `arc`. `poppler_renders_generated_text_choice_checkbox_and_radio_appearances` asserts `SetRadio("Station", "Yes")` returns `Generated` and that the radio rectangle's raster changes. No test inspects the content operators.

**Choice fields expose `/Opt`, including export/display pairs. A value that is not in a present list throws `PdfFieldValueException` and does not dirty the document, unless Edit is set. Several values on a single-select field throw. A multi-select value is a PDF array. Required is reported.**

`ReadOptions` keeps export/display pairs and uses the string for both when `/Opt` is strings. `RequireOption` throws when the export value is missing, the list is non-empty, and Edit is clear. `SetChoice` rejects a multi-value write on a single-select field before it edits. Multi-select `/V` is an array. `choice_options_reject_unknown_values_and_multi_select_is_an_array` asserts Required, the `Red`/`Blue` strings, the `a`/`Alpha` pair, the Edit and multi-select flags, the `A`/`C` selection, the three throws, an unchanged length, `Generated` for `Blue` and for display text `Beta`, and `[(B)]` in the appendix.

The absent-`/Opt` exception is under decisions. It is not covered by this match.

**Fields in several object streams return the same values on a second read. Editing one field round-trips, and the others keep their values.**

`field_walk_releases_object_streams_and_rereads_them` asserts `Alpha`, `Beta`, `Gamma`, and `Delta` on two reads, then `Edited` for `A` with `B`, `C`, and `D` unchanged, including after reopen. The retained-stream-count asserts in that test are cache behavior and were not audited.

**Signing while `/NeedAppearances` is true throws `PdfSignException` and does not extend the file. A generated text appearance can be signed in the same revision.**

`Sign` throws on `NeedAppearances` before it builds the revision (`PdfEngine.Sign`). `need_appearances_blocks_signing_without_writing` fills `日本語` and asserts the throw and an unchanged length. `fill_and_metadata_share_one_revision_and_can_be_signed_together` asserts `SetText("Name", "Ada")` returns `Generated` and that `Sign` runs in that same save. The rest of that sealing bullet is not this area.

**An empty signature field is found by name and reused. A second sign of that field throws. A text-field name throws `PdfSignException`. A read-only signature field throws `PdfFieldReadOnlyException`.**

`FindNamedFields` uses the same full-name walk as `GetFields`. `SealExistingField` requires `/FT` `/Sig`, rejects read-only, rejects a `/V` that is already a dictionary, and writes `/V` on the existing value owner. It does not allocate another widget. `HasSignatureDictionary` treats PDF `null` as empty.

`empty_signature_field_is_reused_and_a_second_sign_does_not_grow` asserts one field, the original rectangle and page, a second `PdfSignException`, and an unchanged length. `null_signature_value_is_signable` asserts `/V null` can be signed and the rectangle stays. `signature_parent_keeps_its_widget_and_receives_the_value` asserts the parent keeps the kid's rectangle and the appendix adds no new widget. `text_field_name_is_rejected_without_writing` and `read_only_signature_field_is_rejected_without_writing` assert the two throws and an unchanged length. `pyhanko_signature_field_is_signed_and_accepted` asserts `ExistingSig` is `PdfFieldType.Signature` with a null value before `Sign`. The pyHanko trust result and the signature-dictionary bytes are the Signing Engineer's.

**A pypdf text field `Name` and check box `Agree`, neither of which has an appearance, generate appearances. `Ada` and the checked state round-trip, and `/NeedAppearances` stays false.**

`Fixtures/generate_foreign_pdfs.py` writes those two fields without `/AP`. `Agree` starts as `/Off`. `pypdf_form_fill_is_checked_by_qpdf` asserts both setters return `Generated`, the flag stays false, and the reopen sees `Ada` and `IsChecked`. The `qpdf --check` call in that test was not audited.

**Poppler extracts `Ada` and `Blue` after those appearances are generated, and the text, choice, check box, and radio rectangles change. The unfilled file does not contain those words.**

`poppler_renders_generated_text_choice_checkbox_and_radio_appearances` asserts `Ada` is absent before and present after, `Blue` is present after, and all four rectangles change. It does not assert `Blue` is absent before.

### Rows that do not match

**Password and signature fields.**

`ACCEPTANCE.md` says both throw `PdfFieldValueException`. `SetText` does that for the password flag and for any non-text field, including `/Sig`, and it does so before a write. `simple_text_generates_helvetica_and_other_values_request_appearances` asserts both throws.

`AGENTS.md` and `README.md` say comb, rich text, non-WinAnsi, password, and signature values set `/NeedAppearances true`. Comb, rich text, and non-WinAnsi do. Password and signature throw instead, so the flag is not set by those setters. `team.md` section 1 says not to choose. No defect is recorded here.

**A choice value that is not listed throws unless Edit is set.**

`RequireOption` also returns without throwing when `Options` is empty, Edit clear. `simple_text_generates_helvetica_and_other_values_request_appearances` fills choice `Choice`, which has no `/Opt`, with `Yes`, and asserts `Generated`. Whether "not listed" includes an absent `/Opt` is a decision.

### Not audited

The AcroForm DocMDP / FieldMDP bullet. The tests `docmdp_blocks_writes_and_still_allows_reads`, `docmdp_form_fill_permission_allows_fill_and_signing_but_not_metadata`, and `fieldmdp_all_blocks_a_fill_and_still_allows_a_read` exist. Enforcement belongs to the Signing Engineer.

`RequireFill` is called on every setter. Encryption permissions were not audited.

### Behavior no criterion describes

- `GetFields` throws `PdfFieldNotFoundException` when the name is absent. `simple_text_generates_helvetica_and_other_values_request_appearances` asserts that.
- After a fill sets `/NeedAppearances` true, `_stickyNeedAppearances` stops a later generated appearance from clearing it. The simple-text test asserts the flag is still true after `Bare` and `Choice` return `Generated`.
- Rich text, file select, and a zero-area rectangle take the `/NeedAppearances` path. Rich text is named in `AGENTS.md` and `README.md`. File select and zero area are not named in `ACCEPTANCE.md`. No test was found for either flag.
- One radio widget with no on-state gets a mark named for the export value (`TryWriteRadioAppearances`). More than one such widget sets `/NeedAppearances`. `README.md` states the one-widget rule. The Poppler fixture has one `Station` widget.
- An unknown radio export value throws `PdfFieldValueException`. No acceptance sentence and no test were found for that throw.
- Setters update every widget with that full name. There is no duplicate-name error.
- `WalkField` does not keep a seen-set for `/Kids`. The page walk rejects a page-tree cycle. `NearestFieldValue` rejects a `/Parent` cycle.
- `PdfField.Value` is null when a signature `/V` is missing and when `/V` is a dictionary. There is no public already-signed flag. `Sign` distinguishes them.
- `PdfAcroForm.SetChoice` documents that it always returns `NeedAppearances`. A WinAnsi display value returns `Generated`.
- A widget with no resolved page is reported as page 0.
- Kids also inherit `/Q`, `/MaxLen`, and `/Opt`.
- A terminal widget that has its own `/V` while `/FT` is only on an ancestor is read from the widget and written on the ancestor.
- Push buttons are `PdfButtonKind.Push` and are rejected by `SetCheckBox`.

## 3. Assumptions

- "A value that is not listed" was read as including a choice field with no `/Opt`. If that sentence applies only when `/Opt` has entries, the `Choice` fill in the simple-text test is not a mismatch.
- `OnState` returns the first non-`Off` key in dictionary order. The fixtures used here have one on-state. No widget with two on-states was found in the tests.

## 5. Evidence missing

The suite was not run.

No test asserts inherited `/DA` on a generated appearance, the generated check-box keys `Yes` and `Off`, or that a radio appearance uses `c` and does not use `arc`. The Poppler test does not assert that `Blue` is absent before the fill. No test was found for rich text, file select, a zero-area widget, or several radio widgets that have no on-state.

## 6. Changes made or proposed

No files under `src/`, `test/`, `README.md`, `ACCEPTANCE.md`, `QUALIFICATION.md`, or `agents/state/` were edited. This report is the only file added.

## 7. Verification run

- `git rev-parse HEAD` → `11da851c8e3b9c2d9ce9071078b607b117715047`
- `git log -1 --oneline` → `11da851 genesis`
- `git ls-files` on `src/jaytwo.Ergonomics.Pdf/AcroForm` and the two AcroForm test files → those paths are tracked
- `git status --short` on those paths → empty
- Test suite: not run

## 8. Decisions needed

Product Manager, with the Technical Lead. The owner if the charter sentence changes.

1. Password and signature fills. `ACCEPTANCE.md` and the tests say `PdfFieldValueException`. `AGENTS.md` and `README.md` say `/NeedAppearances true`. The code throws.
2. A choice field with no `/Opt`. The code and `simple_text_generates_helvetica_and_other_values_request_appearances` accept a value with Edit clear. The choice bullet names only the Edit flag as the exception.

## 9. Handoff

QA Engineer verifies this audit. Technical Lead reconciles it into the planning report. The page-box sentences overlap the Core Engineer's page tree. The DocMDP bullet and the signature-dictionary bytes overlap the Signing Engineer.
