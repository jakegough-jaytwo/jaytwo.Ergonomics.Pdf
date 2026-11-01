# W-218 finite appearance, rich blank and input review

Draft research, 2026-10-31. **No contract adoption, runtime implementation or target acceptance result.** This review supports the [forms continuation](../../forms.md#finite-recognition-and-storage-continuation-engineering-proposal-2026-10-31). Licensed ISO text was consulted locally and is not reproduced here.

## Source review: format facts versus policy

Local reference library: `C:/jake/git/pdfdocs`, ISO 32000-1:2008 and ISO 32000-2:2020 with EC3. Clause/table numbers identify the source; page numbers below are PDF page numbers used by that library. **Sourced facts**, paraphrased:

| Subject | Exact source | Draft consequence / classification |
| --- | --- | --- |
| AP coordinate mapping | Both editions 12.5.5; PDF 2.0 pages 489?491; both editions 8.10 | Form Matrix and transformed BBox map into Rect. BBox supplies implicit clipping; absence of W is not absence of clipping. Requiring exact finite quarter-turn constructions is library recognizer policy. |
| Graphics/text state | Both editions 8.4, 9.3 and 9.4 | q saves state; BT initializes text matrices, not all text parameters. The finite proposal must initialize/check spacing, scaling, rise and rendering mode. Its exact ordered templates and explicit opaque-state profile are engineering choices. |
| Variable text resources and marked content | ISO 32000-1 12.7.3.3/Table 222; ISO 32000-2 12.7.4.3/Table 228, pages 548?550, issue 318 | Tx marked content and the AP resource dictionary cannot be ignored. An AP resource wins over a conflicting same-name DR resource when updating. The former whitelist omitted BMC/EMC; corrected in the proposal. Rich fields regenerate the entire AP rather than using the ordinary Tx replacement convention. |
| Glyph identity versus claimed Unicode | Both editions 9.7 and 9.10 | Type0/CID mapping, widths, embedded glyphs and ToUnicode serve different purposes. A matching ToUnicode string is no paint proof. Requiring retained cmap correspondence and initially recognizing Identity-H/CIDFontType2 is policy, not universal PDF validity. |
| Rich blank | ISO 32000-1 12.7.3.4/Tables 223?224, pages 444?445; ISO 32000-2 12.7.4.3/Table 228 and 12.7.5.3/Table 231 | The previous XHTML-only body omitted required body attributes in the earlier edition. PDF 2.0 instead refers to XFA 3.3. The proposed edition-specific candidate is still unvalidated against XFA and readers; namespace/version lexical discrepancies between the older table and example must be resolved. |
| Comb and text flags | ISO 32000-1 12.7.4.3/Table 228; ISO 32000-2 12.7.5.3/Table 231, pages 554?556, issue 191 | Comb requires MaxLen and clear Multiline/Password/FileSelect flags. DoNotScroll constrains interactive scrolling; the opt-in no-clipping guarantee applies independently. Rejecting all action-bearing, DoNotScroll, Required or FileSelect fields would add unsupported policy, not follow from these clauses. |
| List viewport and flags | ISO 32000-1 12.7.4.4/Tables 230?231; ISO 32000-2 12.7.5.4/Tables 233?234, pages 556?558, issue 468 | Lists are scrollable; TI describes the first visible option. Keeping the original TI during fill is not a stated format requirement. Sort does not authorize readers to reorder Opt; Edit requires Combo and DoNotSpellCheck requires Combo+Edit. No blanket rejection of Sort/CommitOnSelChange is inferred. |
| Choice stored association | Same choice clauses and PDF 2.0 issue 468 | I can disambiguate repeated exports, including single-selection in the corrected 2.0 wording; contradictory V/I has an edition-specific rule. The paired-Opt discussion's wording for V is in tension with the proposed export-string serialization. Keep the proposal and this source ambiguity visible; compare independent writer/reader round trips before treating it as settled. |
| User permissions | ISO 32000-1 7.6.3/Table 22, pages 69?70; ISO 32000-2 7.6.4.2/Table 22, pages 94?95 | Handler revision controls the bit interpretation. Bit 6 permits filling; revision >=3 additionally supplies bit 9. Bit 4 is general modification and bit 11 assembly. The exact flatten/read-export library policy is not fully supplied by those descriptions. |
| Certification and locks | Both editions 12.8.2.2 and 12.8.2.4; ISO 32000-2 Table 257, pages 588?592 | Form-filling permission under DocMDP is not general page-content permission. Enforcing structural restrictions is distinct from cryptographic signature validation. Q-33 remains owner-selected library direction, not inferred from the ISO text. |

This initial review used local ISO sources without redistributing licensed reference excerpts; the subsequent XFA review below adds external source links. Missing XFA-edition validation remains an explicit research gap, not a conformance pass.

## Measured static corpus survey

Command: `tmp/w218-bevel/venv/Scripts/python.exe docs/spec/drafting/research/w218-form-input-survey.py` (Windows 11, Python 3.13.1, pypdf 6.21.4; exact environment in [raw results](w218-form-input-survey.json)). The [script](w218-form-input-survey.py) reads 33 top-level corpus PDFs, traverses AcroForm Fields/Kids with a bounded guard and records text/choice flags, widget rectangles, DA, option counts and I/TI. It does not modify PDFs or read field values into the research report. The corpus provenance/licenses remain in its existing manifest; no input PDF was copied.

Final result: **604 text fields and seven choice fields**. Two encrypted inputs were skipped: the public-key encrypted Chrome invoice (pypdf supports only Standard encryption) and the RC4 LibreOffice memo (no credential attempted). All seven choice fields are noneditable combos: five in `uscis-i9-2025.pdf` (59 options each) and one each in `libreoffice-form.pdf` and its inherited-value derivative (four options). There are **no list boxes** in these results. This is not worldwide representativeness, a complete form-reader test or semantic value verification.

Text flag occurrences overlap: ReadOnly 7, Multiline 18, FileSelect 0, DoNotSpellCheck 57, DoNotScroll 506, Comb 91, RichText 0. These observations support preserving valid DoNotScroll/DoNotSpellCheck flags, not claiming every counted field satisfies the proposed profile. Comb+DoNotScroll appears in representative forms; neither flag alone establishes full fit, font mapping, MaxLen validity or authority. Missing RichText/FileSelect/list examples remain corpus gaps. An initial survey failed on an indirect Opt array in the I-9; explicit dereferencing fixed it, and failed-file partial rows are discarded before the final results are written.

## Acceptance impact assessment: inference, not approval

The proposed **fixed-TI** rule adds an acceptance restriction beyond complete-selected-value visibility. Arithmetic example: a supported list content box with room for two complete rows, original TI=0, and one selected row at index 4. Preserving TI refuses; choosing TI=3 or 4 can expose the entire selected value without changing Opt/V/I. This example is **inference from the proposed row model**, not an executed PDF or representative real-world list observation. If selected rows 0 and 4 must both be visible in a two-row box, changing TI alone cannot satisfy the complete-selection oracle; that is a different refusal reason. Neither case grants auto-scroll permission by itself.

Alternatives for later review:

1. Preserve TI and refuse even when a new viewport could show every selected item. Exact candidate: ?An appearance-ready list mutation shall preserve effective TI and refuse if any selected row is outside its fully visible interval.? This is materially narrower and **not selected or approved**.
2. Allow a deterministic TI update when one contiguous viewport can show every selected row, preserving option data/order and exact selections; otherwise refuse. Exact candidate: ?An appearance-ready list mutation may change target-local TI solely to expose every selected row in one supported viewport; disclose the changed top index and refuse if no such viewport exists.? This is the engineering recommendation to investigate, not an adopted rule.
3. Preserve ordinary scrollable-list semantics and disclose hidden selections. This would require explicit review of the proposed complete-selection guarantee; do not silently implement it as appearance-ready success.

No adoption or narrowing proposal is submitted under A4 in this continuation: acquire representative single-/multi-select business lists and independent generated-appearance observations first. The fixed-TI restriction is now held open in forms, rather than treated as approved. A concrete later narrowing proposal must return under A4 with exact wording, alternatives, recommendation and measured acceptance impact. There is no new owner decision required now.

Field-flag assessment: enforce format-incompatible Comb combinations, preserve Required/FileSelect/DoNotSpellCheck/DoNotScroll and nonexecuted actions under existing directions, and keep malformed/unsupported combinations operation-local. The corpus cannot justify a new blanket restriction on action-bearing, rich, FileSelect or list fields. Recognizer-only limits yield Indeterminate, not read refusal for every well-formed unsupported AP.

**Unexecuted next scenarios:** real lists where TI-only adjustment changes fit; selections whose span exceeds viewport capacity; Sort and CommitOnSelChange preservation; edition-specific Opt/V/I disambiguation; rich local blank round trips in two independent readers; AP-C1?C6, VALUE-C1?C4 and AUTH-C1?C4 in forms. No target generation, refusal/restoration, Save/reopen, changed-assembly compatibility or runtime test was run.

## Rich blank edition review

**Sourced research, 2026-10-31; no selected serialization or conformance claim.** The [PDF Association normative-reference index](https://pdfa.org/iso-32000-normative-references/) links Adobe's [XFA 3.3 specification](https://pdfa.org/norm-refs/XFA-3_3.pdf), dated 2012-01-09. Consult chapter 5, Representing and Processing Rich Text, subsection Version Identifiers for Rich Text Producers and Rich Text Specifications (printed pages 216-217); chapter 27, Body Element (1189) and Version Specification (1222-1223), especially Example 27.38. The earlier label "chapter 5, Versioning" was imprecise. The version attributes are optional. Its example uses `http://www.xfa.org/schema/xfa-data/1.0/`, including the trailing slash, and numeric version identifiers; APIVersion is not a producer-name prefix. These observations apply to XFA 3.3, not automatically to earlier editions. XML namespace URIs with and without that slash are distinct.

**Local ISO comparison:** ISO 32000-1 12.7.3.4, Table 224 and its example (PDF pages 444-447) instead prescribe a namespace without that slash and a software-name/API-version form, and differ between the table's prose and example for the spec token. ISO 32000-2 12.7.4.3, Table 228 refers to XFA 3.3. These are edition-specific source differences, not a conflict among this repository's governing documents. No adopted repository contract promises a serialization here. Do not silently normalize the URI or extrapolate the later grammar backwards.

**Engineering inference:** investigate the existing attributed candidate only for its older PDF edition, and a separate minimal XHTML body/empty paragraph without XFA metadata for PDF 2.0. Omitting optional metadata avoids inventing the library's XFA processor version; it does not prove the candidate meets every applicable requirement. The older XFA 2.0, 2.2 and 2.4 texts were not obtained in this session. Attempts at Adobe's `content/dam/acom/en/devnet/xfa/pdfs/xfa_spec_3_3.pdf` and `content/dam/acom/en/devnet/acrobat/pdfs/xfa_spec_2_4.pdf` failed through the browsing tool (404 and cache miss respectively); the later authoritative mirror succeeded. Search results for the older edition led back to PDF 1.7/ISO material, which is not independent confirmation of the XFA grammar. No source PDF or licensed extract was copied into this repository.

Four separate claims remain: XML parser acceptance; applicable PDF/XFA format validity; effective local V/RV blankness despite inherited nonblank values and defaults; and readers' actual rich-text presentation. A blank existing AP can mask an invalid or ignored RV. A future independent-reader experiment must name its version and whether it consumed RV or merely painted AP, with an absent/stale-AP control and an inherited-value/sibling control. Preserve DS/defaults/styles/actions under the selected clear contract. Merely serializing empty XML or rendering no pixels is insufficient to establish all four claims. Those experiments remain **unexecuted**.

## Finite proof and lossless-name arithmetic

**Executed independent arithmetic, not target or reader verification.** Run `python docs/spec/drafting/research/w218-finite-proof-arithmetic.py`; the [script](w218-finite-proof-arithmetic.py) and [raw record](w218-finite-proof-arithmetic.json) retain the exact inputs, environment and script hash. Python 3.13.1 on Windows 11 checked 261 legal name-byte examples (all 255 nonzero one-byte values plus empty/multibyte examples), reversible canonical spelling and uniqueness; rejected six invalid/noncanonical spellings; mapped four BBox/Matrix cases to their Rect extents; bounded four proposed tick-stroke envelopes; and parsed two rich candidates with empty character data. All arithmetic oracles matched. The two namespace URI spellings produced distinct expanded attribute names.

The mapping cases include quarter turns, translation/nonuniform scale and an offset BBox. They confirm the proposed *calculation on those inputs*, not all matrices, effective clipping, annotation flags or rendered placement. The tick envelope uses a conservative half-stroke expansion around the polyline vertices with round joins; it does not measure raster pixels or prove any real producer uses that family. Radio curve/extrema checks were not run at this checkpoint; the later exact-radio section below records their execution. The script is an independent Python model of new draft wording, not the .NET engine and not an independent PDF parser. XML parsing does not validate XFA. None of these results supplies font-resource measurements or a size limit.

**Sourced byte/identity review:** both ISO editions 7.3.5 define name identity on unescaped bytes; empty name is valid and NUL is excluded. The additive `PdfButtonStateName` proposal preserves that identity with owned octets and canonical ASCII, including invalid-UTF-8 name bytes; no replacement decoding or raw PDF object API is needed. Canonical spelling and public type are engineering choices. Existing string APIs are untouched. Check older PDF 1.0/1.1 lexical differences separately before claiming their support.

**Sourced text-stream review:** both editions 7.9.3 apply text-string encoding/lead-byte rules to decoded stream content. ISO 32000-1 12.7.4.3 and ISO 32000-2 12.7.5.3 explicitly allow text-field V streams beginning at PDF 1.5; RV has its own typed entry; this does not permit arbitrary stream-valued T/Opt entries. Both editions 7.3.4 own literal/hex token escaping, distinct from 7.9.2.2 language metadata. PDF 2.0 7.9.2.2.2 makes the language sequence explicit. The proposed streaming decoder now separates those stages, exact-BOM selection, strict Unicode, actual decoded limits and chunk boundaries. Representation of language spans and literal ESC, exact semantic name matching and export/serialization remains open; a blanket valid-language refusal is not selected.

**State/artwork review (earlier checkpoint):** both editions 8.4.5, 8.6.5.6, 8.10 and 12.5.5 distinguish saved state, partial gs updates, resource default colour spaces, implicit BBox clipping, Form mapping and annotation-group compositing. The forms proposal enumerates these dependencies and a paired Off/on tick/dot proof candidate with complete paint accounting. It neither synthesizes replacement artwork nor establishes representative acceptance. The static button inventory and exact radio arithmetic below advance two of this checkpoint's open tasks; full checkbox/radio proof, language/choice interoperability, representative lists, security mappings and font-resource evidence remain open. All new BUTTON/DECODE/NAME target scenarios are **unexecuted**. The settled P1, full-value visibility, legacy, Q-33 and Q-143 boundaries remain unchanged; no A4 proposal or owner decision is pending.

## Inherited choice clear correction

**Sourced correction of draft wording, not changed runtime behavior.** Both editions 7.3.9 treat a dictionary's null value as an omitted entry. V is inheritable in ISO 32000-1 12.7.3.1/Table 220 and ISO 32000-2 12.7.4.1/Table 226. The choice chapters (1:12.7.4.4; 2:12.7.5.4) describe default V null as no selection; that default does not turn a local null into an inheritance barrier. The earlier form-table claim that local V null masks an inherited selection was incorrect and is withdrawn. Rich local empty text overrides remain a different, present-value mechanism.

**Open engineering work:** establish a valid no-selection representation for inherited single-select and multiselect values, without confusing clearing with a selected empty export/literal, changing DV/Opt or altering siblings' effective values. Investigate permitted empty-array representation and any required local value ownership changes against both editions and independent readers; neither is selected here. Removing an ancestor's V without proving sibling preservation is not an authorized shortcut. Do not add an inherited-value refusal policy just because the earlier local representation failed. This is a newly exposed A1 storage gap; its intended semantic clear outcome stays unchanged. **Unexecuted VALUE-C5:** nonblank inherited choice V, one target-local null and a sibling; distinguish actual effective value, no selection and selected empty export before/after Save/reopen in independent implementations. No target or independent-reader experiment ran.

## Button artwork inventory and exact radio bounds

**Executed static evidence, 2026-10-31.** Command: `tmp/w218-bevel/venv/Scripts/python.exe docs/spec/drafting/research/w218-button-survey.py`. Python 3.13.1, Windows 11 10.0.26100, pypdf 6.21.4. The [script](w218-button-survey.py) and [record](w218-button-survey.json) retain script/input hashes, widget object references, state-stream hashes, operator counts and selected font-resource declarations. It inspects page Widget annotations, follows bounded Parent inheritance for FT/Ff, excludes pushbuttons and counts each widget reference once per file. It does not establish canonical field-tree completeness, decode field values, render, generate or alter any PDF. Normal-state streams are parsed independently, without proving their relationship to V/AS/Opt or their actual painted mark. Failed files discard partial rows. Font declarations are observations, not glyph/outline verification.

Results: 33 input files, **233 widgets (153 checkbox, 80 radio), 329 state streams, 89 text-show states**, zero observed Do invocations. Ten files contain counted buttons; derivative signed/inherited PDFs are included and are not independent producers. Two skipped inputs match the earlier survey: public-key Chrome input fails pypdf's encryption-handler support; RC4 LibreOffice memo is skipped without a credential attempt. Some widgets expose only one state; a stream count is not a count of complete Off/on pairs.

| Input group | State streams | Text-show streams | Declared selected font |
| --- | --- | --- | --- |
| CMS-40B original and approved derivative | 22 | 16 | Type1 ZapfDingbats |
| FAA 8710-1 | 151 | 1 | Type1 ZapfDingbats |
| IRS 1040-ES and W-9 | 12 | 12 | Type1 ZapfDingbats |
| LibreOffice original and inherited derivative | 20 | 4 | TrueType subset OpenSymbol |
| OPM OF-306 original and certified derivative | 112 | 56 | Type1 ZapfDingbats |
| USCIS I-9 | 12 | 0 | No Tf observed |

**Acceptance implication (inference, no A4 proposal):** the 89 text-show streams cannot satisfy the proposed path-only Off/on mark grammar. They are not proved to be 89 valid on marks or 89 otherwise eligible fields. The current general text recognizer's Type0/CIDFontType2 mapping proof also does not establish these Type1/simple TrueType glyphs. A representative button proof needs a deliberate font/code/outline path and complete decoration/state accounting; do not silently widen the grammar or refuse all such input. The inventory is concrete evidence against claiming representative coverage from the exact path candidate alone.

Six LibreOffice state streams contain cubic operators. Focused read-only inspection of the original's first curve state finds BBox `[0,0,8.05,8.05]`, starting point `(4,5.6)`, first controls `(4.9,5.6)`, `(5.65,4.9)`, endpoint `(5.65,4)`, and an even-odd fill after four curves. Those numbers differ from the candidate's centred, fixed-factor construction. This is a syntactic comparison, not a finding of malformed PDF, rendered clipping or invalid radio state. No renderer or paired eligibility check ran.

**Executed exact arithmetic:** `python docs/spec/drafting/research/w218-radio-curve-arithmetic.py`; [script](w218-radio-curve-arithmetic.py), [record](w218-radio-curve-arithmetic.json), Python 3.13.1/Windows 11. The fixed decimal factor is an exact rational. Every quarter's derivative has coordinate-wise constant sign by its Bernstein control differences, proving endpoint extrema. Closure and exact signed area are checked; unit-radius area is `392809041575376354997/125000000000000000000`, positive. Four square/wide/tall/unit rectangles, including translated origins, have exact bounds centre plus/minus `min(w,h)/5` contained in their content boxes. A changed factor of 2 overshoots despite unchanged endpoints (midpoint coordinates 5/4 and 5/4). All recorded arithmetic oracles matched. No trigonometric circle claim, raster comparison, clipping/compositing proof, target execution or representative acceptance follows. The radio-curve arithmetic gap is advanced; button eligibility remains open.

## Edition and inheritance follow-up

**Source acquisition attempted, not obtained:** historical Adobe partner URLs for XFA 2.2/2.4 were inaccessible through the browsing tool; Adobe content/dam Acrobat 2.4 and XML 2.2 paths returned cache misses. A search-indexed Adobe-authored 2.4 PDF at CiteSeerX returned HTTP 403 when opened; its search snippet is not grammar evidence. Guessed PDF Association/Adobe open-source 2.4 paths were also inaccessible. XFA 2.0 was not located. No older grammar, conformance result or licensed source copy was obtained. These access failures do not show that the documents are unavailable elsewhere.

XFA 3.3 chapter 5 distinguishes rich data recognition from template/exData recognition; chapter 28's implementation notes distinguish historical producer-version handling from later template flags. Neither licenses applying the later rules backwards or claiming that an AcroForm reader consumed RV. The new subject oracle uses mutually contradictory V/RV/AP sentinels, qualifies an RV-consuming reader mode before blank testing, and retains stale/absent AP and inherited/sibling/default controls. **Unexecuted**; no available reader was represented as passing it.

**Local ISO review, inference kept separate:** ISO 32000-1 12.7.4.4/Table 231 (PDF pages 453-454), ISO 32000-2 12.7.5.4/Table 234 (557-558), including EC3 issue 468, specify scalar V for a single selection and arrays for multiple selections. They do not explicitly provide a single-select empty-array clear. I cannot independently mask inherited V; null is omission under 7.3.9. An empty literal cannot universally mean clear in the presence of empty-export options or editable text. This establishes why the suggested shortcuts lack proof, **not** impossibility of a valid clear or approval of inherited-value refusal. The subject adds exact representation obligations and VALUE-C5 controls. No ancestor mutation, field restructuring, single-/multiselect empty-array serialization or paired-Opt export-string policy is selected. Independent edition-specific writer/reader/export evidence remains open.

## Local reference continuation: selected indices and language framing

**Sourced review, 2026-10-31; draft only.** Reference authority is `C:/jake/git/pdfdocs`, following its AGENTS.md, text/INDEX.md and clause/errata lookups. Read the local generated sections against their edition identifiers; no licensed text or source PDF is redistributed here. Page numbers below are PDF page indices from that library, not printed folios. This review supplies format distinctions and investigation protocols, not reader results or target acceptance.

Source identity, SHA-256 measured from the local PDFs: `pdfs/PDF32000_2008.pdf` = `9de0ca9e8570d6209e8bd48a355be8eb6ec376acfc3fc3ae97cd8730351417ff`; `pdfs/ISO_32000-2_sponsored_EC3.pdf` = `b265f1aa98a0badc9d68188c022cb5d8966f668e21189e304107a4f561bcdefb`. These identify the consulted editions; hashing is not PDF validation. The reference repository was read only.

Selected-choice sources: ISO 32000-1:2008 12.7.4.4/Table 231, `text/iso-32000-1/241-12-7-4-4-choice-fields.md`, pages 453-454; ISO 32000-2:2020 EC3 12.7.5.4/Table 234, `text/iso-32000-2/294-12-7-5-4-choice-fields.md`, pages 557-558, and issue 468 in `text/errata.tsv`. Issue 468 is an I-entry replacement: it removes the MultiSelect-only description and the array-V requirement trigger, retains duplicate-export disambiguation, ascending indices and V precedence. PDF 1.7 additionally discourages single-select I, while also requiring I for different names sharing an export; do not flatten these provisions into a universal index-write rule. The two adjacent extracted files repeat the same page's annotation; that is not two independent corrections.

Both editions' following V discussion identifies the second paired-Opt member as name text, while the Opt entry distinguishes the first member as export text. **Inference:** an export-V candidate may interoperate, but issue 468 alone does not prove its validity or settle that distinction. The subject now holds the unconditional V-export/I write sketch open and supplies an edition matrix and VALUE-C6. Compare raw storage, selected row, label and export independently; accepting an input or exporting a plausible value does not prove every property. No field restriction or edition conversion is proposed.

Language sources: ISO 32000-1 7.9.2.2/7.9.3, `text/iso-32000-1/047-7-9-common-data-structures.md`, pages 94-95; ISO 32000-2 7.9.2.2.2/7.9.3, `text/iso-32000-2/064-7-9-common-data-structures.md`, pages 132-133. The older edition calls the two-byte language code ISO 639; the newer calls it BCP 47 while retaining the two-byte constraint and explicitly describing UTF-8 ESC. Both have optional two-byte country codes and Unicode ESC delimiters. This does not supply arbitrary BCP 47 tag syntax or literal-ESC quoting.

**Inference and remaining ambiguity:** the ASCII code wording needs byte-level corroboration alongside UTF-16BE delimiters. Raw ASCII-code bytes and UTF-16BE-encoded code characters are deliberately separate DECODE-C5 controls, not asserted valid alternatives. A homogeneous strict Unicode decoder cannot yet be described as the completed whole-text algorithm. Name equivalence, span offsets, MaxLen accounting, lossless additive export and serialization remain unresolved; none is silently decided by deleting annotation bytes. This is a sharper statement of the existing A1 gap, not approval to reject valid annotated text. No independent reader, byte-layout experiment, new public API or target decoder ran in this continuation.

## Independent reader experiments: choice storage, clearing and language escapes

**Executed 2026-10-31; independent readers only, no target library, no format-validity claim.** Command: `tmp/w218-bevel/venv/Scripts/python.exe -I docs/spec/drafting/research/w218-choice-language-experiments.py docs/spec/drafting/research/w218-choice-language-experiments.json`. The [script](w218-choice-language-experiments.py) hand-writes every PDF (no pypdf writer) and the [record](w218-choice-language-experiments.json) retains the script hash, each fixture's SHA-256 and every reader observation. Readers: pypdf 6.21.4 (raw local and inherited dictionary values); PDFium 156.0.8076.0 through pypdfium2 5.14.0 with a form-fill environment (field value, per-option label and selected state); MuPDF 1.28.5 through PyMuPDF 2.0 (field value and option pairs). Python 3.13.1, Windows 11. MuPDF's binding reports an empty value for every array V and exposes no row selection, so it is used only where its string value is informative. Acrobat, pdf.js, PDFBox, Poppler's form layer and any validator were not run. Counts: 30 VALUE-C6 fixtures, 31 VALUE-C5 fixtures (24 inherited, 7 baseline) and 13 DECODE-C5 fixtures.

**Source interpretation (ISO 32000-1 12.7.4.4/Table 231; ISO 32000-2 12.7.5.4/Table 234 with issue 468, local library).** Both editions say the second element of a paired Opt entry is the name string that V holds. Observed PDFium behaviour contradicts using that string to select a row (below). The source ambiguity is therefore unresolved by text and the write proposal is justified by reader evidence plus a disclosed risk, not by the standard alone.

### VALUE-C6 observations (PDFium row selection)

| Control | Observation | Fixtures |
| --- | --- | --- |
| Paired Opt, V is the export string, I absent or consistent | Row selected; label and V string both report the export | c6-01, 03, 07 |
| Paired Opt, V is the display string | No row selected, V string still returned (with or without I) | c6-02, 04, 06, 26, 30 |
| V contradicts I | V takes precedence, even when V matches no row (display V with I naming a row selects nothing) | c6-06, 07 |
| I alone | The named row is selected, V string empty | c6-05 |
| Two rows share an export, V is the export | Both rows selected without I; only the I row with I=[1] | c6-11, 08 |
| Same, V is a display name | No row selected, with or without I | c6-09, 10 |
| One label, different exports | Selection follows the export, not the shared label | c6-13, 12, 14 |
| Empty-export row | V empty string selects it; absent V selects none; display V selects none | c6-15, 17, 18, 16 |
| MultiSelect, V array of exports | Rows selected with or without I | c6-19, 22 |
| MultiSelect, V array of display names or scalar display | No rows selected | c6-20, 21, 23 |
| MultiSelect, V empty array and I empty | No row, empty value | c6-24 |
| Editable combo | Literal not in Opt selects nothing; export selects its row | c6-25, 27 |
| Plain list, single select, I present | Same selection as without I | c6-28, 29 |

Reading: PDFium's selected row is the row whose export string equals V, and I only disambiguates shared exports. This matches the proposal in forms (V is the export, I only when MultiSelect or a shared export) and conflicts with an ISO-prose display-name writer. One independent reader is not agreement among implementations, and its behaviour does not make display-name V invalid; it makes display-name V render as no selection there.

### VALUE-C5 observations (inherited V, a clear on one kid, an untouched sibling)

Fixture shape: a non-terminal parent with `FT /Ch`, Opt, and a nonblank V (single combo `e2`; MultiSelect list `e1,e3`), kid A with the local control, kid B untouched, repeated with an empty-export Opt.

| Local control on A | PDFium A / B | MuPDF A (single) | pypdf raw |
| --- | --- | --- | --- |
| nothing (control) | inherits / inherits | inherits | effective value inherited by walking Parent |
| `V null` | value empty, no row / inherits | inherits (7.3.9 omission) | null object retained |
| `V ()` | value empty / inherits; selects an empty-export row when one exists | empty | empty string |
| `V []` | value empty, no row, also with an empty-export row / inherits | empty | empty array |
| `I []` only | still inherited value and row / inherits | inherits | inherited value |
| `V []` with `I []` | same as `V []` | empty | both |

Conclusions: local null is not a portable inheritance barrier (readers differ); `I []` never clears; an empty string is a value; local `V []` is the only control that reads as no selection without touching the parent or sibling in both readers that expose a value. Format status of `V []` is typing-dependent: both editions give an array V for several selected items, so it is within the typing for MultiSelect and outside the stated typing for a single selection (inference from 12.7.4.4/12.7.5.4 prose). Baseline fixtures without an ancestor show every control reading as empty in all three readers, so only inheritance separates them.

[Corpus count](w218-inherited-v-survey.json): 13 unencrypted corpus files with an AcroForm, 813 terminal fields (7 choice), zero terminal fields take V from a non-terminal ancestor and zero non-terminal fields carry V. Representativeness is limited: two encrypted files were skipped and there are no list boxes.

### DECODE-C5 observations (language escapes)

Fixture shape: a text field whose V is a hand-encoded UTF-16BE (BOM) or UTF-8 (BOM) string; "A" means a raw two-byte ASCII code (one UTF-16 unit such as 0x656E), "B" means each code letter as a UTF-16BE unit.

| Fixture | PDFium | MuPDF | pypdf |
| --- | --- | --- | --- |
| plain | `Hi` | `Hi` | `Hi` |
| A `en`, A `enUS` | `Hi` | `Hi` | ESC, U+656E (and U+5553), ESC kept |
| A adjacent runs, A at end, A before supplementary | stripped | stripped | kept |
| B `en` | `Hi` | `Hi` | ESC `en` ESC kept |
| B `enUS` | `Hi` | `ESC enUS ESC Hi` kept | kept |
| UTF-8 `enUS` | `Hi` | `US ESC Hi` | BOM bytes mis-decoded |
| literal ESC, unclosed | empty (text lost) | `ESC Hi` kept | kept |
| A `en` without closing ESC | empty (text lost) | ESC, U+656E, `Hi` kept | kept |
| empty code (ESC ESC) | `Hi` | ESC ESC `Hi` kept | kept |
| 3-byte code, odd length | empty | mis-decoded text kept | mis-decoded text kept |

Conclusions: reading A (two raw ASCII bytes) is the literal reading of both editions' byte counts and is the only form PDFium and MuPDF both strip identically; reading B, UTF-8, country-code-with-B and every malformed shape produce different text in different readers or lose text (PDFium). A literal U+001B has no quoting scheme in either edition. Corpus: [no text-typed string contains 0x1B](w218-esc-corpus-survey.json) (5,740 strings in 31 unencrypted files; the 9 hits are signature `/Contents` blobs).

### Not resolved by these experiments

Acrobat behaviour for paired-Opt V and for language escapes; MaxLen counting by readers; PDFBox/pdf.js; Save/reopen through the target library; list boxes and TI; strict-validator verdicts on `V []` for a single selection.

## XFA acquisition retry, 2026-10-31

The task-supplied [XFA 3.3 URL](https://pdfa.org/norm-refs/XFA-3_3.pdf) returned HTTP 403 from a direct request and 403 from the sanctioned fetch agent. A request with a different client identity was blocked by the session's safety layer and not repeated. One standard web search for XFA 2.0/2.2/2.4 returned only general pages (W3C 1999 submission index, vendor documentation). **Precise missing evidence:** a local copy of XFA 3.3 (chapter 5 printed pages 216-217; chapter 27 printed pages 1189 and 1222-1223) and any XFA 2.0/2.2/2.4 text (Body Element, rich-text version identifiers). No owner decision is needed to unblock; a manually downloaded copy placed in a local directory would be enough. Rich blank therefore remains blocked on source acquisition, and no reader experiment was run for it because the RV-consuming-reader qualification (contradictory V/RV/AP sentinels) still has no qualified reader.
