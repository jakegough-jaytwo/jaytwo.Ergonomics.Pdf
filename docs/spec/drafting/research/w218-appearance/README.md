# W-218 independent PDF appearance measurement

Research fixtures only. They are **not** outputs of `jaytwo.Pdf`, target refusal/restoration tests, or adopted form rules. Source revision: `509af167b4257646b330c61d95a381cfff2cf014`.

Run on Windows with `C:\Windows\Fonts\arial.ttf` and `timesi.ttf`:

```
dotnet build src/jaytwo.Pdf/jaytwo.Pdf.csproj -f net10.0 --no-restore -v:q
python docs/spec/drafting/research/w218-appearance/generate.py
python docs/spec/drafting/research/w218-appearance/analyze.py
pdftoppm -f 1 -l 1 -r 144 -png -singlefile docs/spec/drafting/research/w218-appearance/Arial.pdf docs/spec/drafting/research/w218-appearance/Arial-first
pdftoppm -f 15 -l 15 -r 288 -png -singlefile docs/spec/drafting/research/w218-appearance/Arial.pdf docs/spec/drafting/research/w218-appearance/Arial-size24
```

Inputs and output hashes are in `measurements.json`; generated PDFs, raster examples, and unhinted outline calculations remain in this directory. Versions: .NET SDK 10.0.402, pypdf 6.21.1, fontTools 4.67.0, Pillow 12.2.0 (installed but unused here), Poppler `pdftoppm` 26.07.0. The net10.0 source build passed with zero warnings/errors. The current public `PdfAcroFormEditor.Fill` accepts values but no font argument and documents Helvetica/WinAnsi paint; it cannot perform the target supplied-font operation.

`generate.py` uses pypdf to write `/V` and a separate `/AP/N` for each `/Tx` widget. Each AP has an embedded Type0/CIDFontType2 TrueType resource, Identity-H glyph codes and a ToUnicode map. pypdf independently reread 19 fields and 19 pages per PDF and confirmed `/AP/N` font subtype `/Type0`; this checks structure/stored values, not visual fitness. Poppler rendered the two saved examples, showing actual text from the embedded font; the 24 pt Arial example visually approaches the right edge. No second independent PDF renderer was available for these Arial fixtures: **not run** (the later exact-width fixture below was rendered in three renderers). PDFium/MuPDF/Acrobat visual comparison, target framework matrix, engine API operation, refusal/restoration and DA-zero selection: **not run**.

The AP placement is deliberately fixed to expose boundaries; it does not implement the proposed wrapping/search algorithm. `/MK/R` carries 0/90/270 rotations, `/BS` carries solid/dashed/underline styles, and borderless cases omit paint in AP. Thus the stream and geometry can be inspected, but a single Poppler raster example does not establish every reader's rotation, dash, underline or border-padding semantics. `outline-analysis.json` is fontTools unhinted outline arithmetic, **not** observed raster ink. Some synthetic APs clip: they are negative probes, not claimed successful layouts. Edge/repeated spaces and NBSP are retained in `/V` and encoded in AP, but no target wrap choice is tested.

## Focused R1 render check (2026-10-31)

After the owner withheld R1 adoption and requested rotation/border/inset/precision renders, run:

```
python docs/spec/drafting/research/w218-appearance/profile_render.py
pdftoppm -f 1 -l 14 -r 288 -png docs/spec/drafting/research/w218-appearance/profile-render.pdf tmp/w218-adoption-review/r1
pdftoppm -f 6 -l 7 -r 720 -png docs/spec/drafting/research/w218-appearance/profile-render.pdf tmp/w218-adoption-review/r1hi
```

`profile_render.py` is a separate pypdf/Arial research generator; it does not call `jaytwo.Pdf`. It writes 14 fields/pages and explicit AP streams, with proposed `hhea` baseline, 1-point inset and half-stroke reservation (the half-stroke term was later shown insufficient for a full-width inside stroke; see the exact-width section below, superseded pending owner review). The generated PDF is retained for reproduction. pypdf 6.21.1 reread 14 fields/pages. Poppler `pdftoppm` 26.07.0 rendered all 14 at 288 dpi and pages 6–7 at 720 dpi. Visual inspection found text in each 0/90/180/270 transformed case, distinct solid/dashed/underline boundaries, and the >24-point example. The absent-`/MK` and explicit `/MK/R 0` pages were pixel-identical. Solid-border width 36.978 versus 36.979 and 6-point-floor width 19.989 versus 19.990 were pixel-identical at 288 dpi; the first pair was also identical at 720 dpi. These images cannot resolve the 0.001-point acceptance boundary, and both sides painted text rather than testing refusal. They do not establish cross-reader inset sufficiency or the target library's behavior.

An initial exploratory 90-degree AP used an exchanged `/BBox` without an explicit stream transform; Poppler visibly clipped that example. The retained generator applies explicit transforms for 90/180/270 and uses the physical widget `/BBox`. This is an observed construction correction, not proof of PDF reader agreement. No second reader was available. The proposed 0.001-point precision and inset remain unvalidated adoption choices.

Key outline results (margins are `[left,bottom,right,top]` in pt): Arial `WWW` at width 36 has right margin 0.1562; Times Italic has 2.6816. The Arial 65.5 pt field has 1.1914 right margin at size 24.0 and 0.9318 at 24.1. Arial at width 82 has right margins 2.1143, 1.9844 and 1.8546 at sizes 30.0, 30.05 and 30.1. At 12 pt text, `Wgj`/`ill` ink separation is 0.8848 pt Arial and 1.0782 pt Times Italic with 12 pt leading; 14 pt raises those to 2.8848 and 3.0782. At height 26 pt, the two-line bottom margin is 1.4 pt Arial and 1.2594 pt Times Italic, so a trial 1 pt inset fits while 2 pt does not. Font-dependent results do not establish a PDF-wide rounding tolerance, border padding, 14 pt minimum leading, 24 pt DA-zero maximum or 0.1 pt grid.

## Exact-width R1 fixture and three-renderer check (2026-10-31)

Portable, checked-in successor to the scratch `boxfix.py` prototype. Research only: it is **not** `jaytwo.Pdf` output and is not a target-library, refusal/restoration or adopted-contract test. Files are in [exact-width/](exact-width/):

| File | Role |
| --- | --- |
| `exact_width_fixture.py` | Builds the synthetic font and two PDFs; holds the case table and a hand-stated verdict per case (the script asserts its own arithmetic agrees). |
| `exact_width_check.py` | Re-reads the PDF bytes and verifies widths, positions, border and content box without using generator constants; recomputes every verdict and the DA-zero search by linear scan. |
| `exact_width_render.py` | Renders in Poppler, MuPDF and PDFium; measures visible ink edges, ink-to-border clearance and pixel identity around each boundary. |
| `exact_width_summary.py`, `run_all.ps1` | Print the tables quoted below; run all steps. |
| `exact-width-fit.pdf`, `exact-width-precision.pdf`, `exact-width-manifest.json`, `exact-width-structural.json`, `exact-width-render.json` | Generated outputs. |

No scratch path, drive letter or environment variable is assumed. Dependencies: Python 3.13 with `fonttools`, `pypdf`, `pymupdf`, `pypdfium2`, `numpy`, `Pillow`, plus a Poppler `pdftoppm` on `PATH` or passed with `--pdftoppm`.

```
python docs/spec/drafting/research/w218-appearance/exact-width/exact_width_fixture.py
python docs/spec/drafting/research/w218-appearance/exact-width/exact_width_check.py
python docs/spec/drafting/research/w218-appearance/exact-width/exact_width_render.py --pdftoppm <path-to-pdftoppm>
python docs/spec/drafting/research/w218-appearance/exact-width/exact_width_summary.py
```

**Fixture.** The embedded TrueType has 1000 units per em; `hhea` ascent/descent/line gap are 800/−200/100. Glyph `B` is a 1000×1000-unit box (advance 1000) that fills its whole advance and the whole line box, so ink, advance and line box coincide. Space and NBSP have advance 500 and no ink; `J` has advance 1000 and ink 100 units *before* its origin (negative bearing). Every serialized `/W` entry is an integer equal to `hmtx` (checked for every glyph), so the advance used for fit arithmetic, the number in the PDF and the position a renderer uses are the same number. Sizes, dimensions and positions are exact decimals of at most six places. 64 pages hold one widget each (page 160×100 pt, widget origin (40,30)); a second PDF holds 7 precision probes.

**Hashes** (generation is deterministic: two runs were byte-identical): `exact-width-fit.pdf` `dde2293239a03337f9c8ae1f063bf03c4dcbef0a710b9fd55492ba92b6ac9244`; `exact-width-precision.pdf` `5460174262046145bb61bd1836c3a254838972ecd16b60831264ca85598c4ebf`; embedded font program `ceb976ed26b95d66ef61ea7af958356743e11f3bdfd137850c6866bc7ffe467b`.

**Versions and settings** (also in `exact-width-render.json`). Windows 11 (10.0.26100); Python 3.13.1; pypdf 6.21.4; fontTools 4.68.0; numpy 2.5.4; Pillow 13.0.0. Poppler `pdftoppm` 26.07.0, `-r 576 -png`, default anti-aliasing. MuPDF 1.28.5 via PyMuPDF 2.0, `get_pixmap(matrix=Matrix(8,8), alpha=False, annots=True)`, default AA level. PDFium 156.0.8076.0 via pypdfium2 5.14.0, `init_forms()` then `render(scale=8, may_draw_forms=True, draw_annots=True)`. 576 dpi is 8 px/pt, so one pixel is 0.125 pt. The precision probe uses 72,000 dpi (1000 px/pt, one pixel is 0.001 pt) on a cropped window. The earlier independent report used Poppler 26.08.0; this session had 26.07.0.

**Cases** (12 pt unless stated; below/at/above are the exact edge −0.001/0/+0.001 pt): width with a solid 1-pt border (edge 39.000); height, borderless (14.000); two lines around an explicit blank line (40.400); edge and repeated spaces `" B  B "` under left, center and right alignment (50.000); NBSP (32.000); rotation absent/0/90/180/270, with 90 and 270 width triplets; solid, dashed and underline borders of 1, 2 and 3 points at a tight fit on all four content edges; a stroke centred on the widget edge and clipped by `/BBox`; the candidate "full-footprint" inset (below); DA-zero sizes 26.665/26.666/26.667 in an 82-point box (above 24 pt), a caller maximum of 20, and the 6-point floor (20.999/21.000/21.001, and 22.500 giving 6.500); and `J` with a negative bearing.

### Structural result (exact arithmetic, from the PDF bytes)

`exact_width_check.py` passed all 64 pages and all 7 precision rows. For each page: the physical `/BBox` equals the widget `/Rect`; the explicit `cm` equals the rotation matrix (absent `/MK` resolves to 0); `Tf` equals the `DA` size; the decoded emitted text equals `/V`; each `Tm` origin equals the profile's alignment and baseline value; border width, dash and path equal `/BS`; and the verdict (advance, line box, outward-rounded ink, line overlap) equals the hand-stated one. The checker's independent linear scan of the DA-zero grid agrees with the generator's binary search: 26.666 (maximum 35, 82-point box, above 24 pt), 20.000 (maximum 20), refusal for 20.999 with a solid 1-point border, 6.000 for 21.000 and 21.001, and 6.500 for 22.500. Width, height, blank-line, edge-space, NBSP, rotated and floor boundaries all flip between `x−0.001` (refuse) and `x` (fit). Invisible advances matter: `" B  B "` refuses at 49.999 under every alignment even though its ink alone would fit, because the spaces have no ink. `J` is refused by the ink gate (ink 1.2 pt before the content left) although its advance fits.

### Rendered result (what renderers show; not the acceptance threshold)

* **Orientation and ink.** Absent `/MK` and explicit `/MK/R 0` are pixel-identical in all three renderers. The 90/180/270 appearances put ink where the exact transform predicts. Measured ink area is 0.9989–1.0001 (Poppler), 0.9994–1.0000 (MuPDF) and 0.9918–1.0052 (PDFium) of the exact area, so no case is visibly clipped.
* **Edge placement.** Largest |measured ink edge − exact edge| at 8 px/pt: Poppler 0.125 pt, MuPDF 0.051 pt, PDFium 0.101 pt. Poppler's 12-point text sits 0.1 pt high in every case (consistent with whole-pixel baseline placement; not confirmed). The renderers disagree on a single ink edge by up to 0.159 pt (`z_borderless`).
* **Boundary triplets.** Pages 0.001 pt apart are often pixel-identical (every Poppler width, height and blank-line triplet) but not always. Between 96 and 1,068 pixels differ in some MuPDF, PDFium and Poppler triplets, with per-channel differences from 2 to 255 (for example Poppler `sp_center` and `sp_right`, 388 pixels; PDFium `h_blank`, 96 pixels). Differences occur on either side of the exact boundary and do not follow it.
* **Precision probe (1000 px/pt).** A 0.001-pt separation between an ink edge and a vector bar is tracked within one pixel by all three. A 0.0005-pt step is not resolved by Poppler or PDFium (the reading repeats a neighbor); MuPDF resolves it to within 34 µpt. Poppler reads a constant one-pixel offset. This shows only that a renderer can place an edge to about 0.001 pt at extreme resolution, not that 0.001 is the right acceptance grid.
* **PDFium without form drawing** (`init_forms` not called) draws no widget ink at all; the recorded PDFium numbers use form drawing.

### Does a 1-point inset keep ink clear of borders?

Clearance is the distance from the inner painted edge of the border (or the widget edge where nothing is painted) to ink that sits exactly on the content-box edge, minimum over the four sides. Nominal is exact; measured is Poppler / MuPDF / PDFium, in points:

| Construction | Nominal | Measured |
| --- | --- | --- |
| Borderless, 1 pt inset | 1.0 | 0.900 / 0.975 / 1.000 |
| Solid or dashed, stroke inside the widget, width 1, **R1 wording** (1 pt + half the width) | 0.5 | 0.400 / 0.475 / 0.500 |
| Same, width 2 | 0.0 (touching) | −0.100 / −0.025 / 0.000 |
| Same, width 3 | −0.5 (overlap) | −0.600 / −0.525 / −0.500 |
| Underline, stroke inside the widget, widths 1 / 2 / 3 (bottom), R1 wording | 0.5 / 0.0 / −0.5 | Poppler 0.6 / 0.1 / −0.4; MuPDF 0.475 / −0.025 / −0.525; PDFium 0.5 / 0.0 / −0.5 |
| Solid, stroke centred on the edge and clipped by `/BBox` (half visible), widths 1 and 2, R1 wording | 1.0 | 0.900 / 0.975 / 1.000 |
| Solid, dashed and underline, stroke inside, **candidate revision** (1 pt + the whole painted width), widths 1 / 2 / 3 | 1.0 | 0.900 / 0.975 / 1.000 |
| `J` negative-bearing control (ink 0.3 pt from the edge, on a 1-pt inside stroke) | −0.7 | −0.700 / −0.700 / −0.750 |

The control shows the measurement detects an overlap of this size in all three renderers. Measured left-stroke widths equal the drawn widths (1, 2, 3 inside; 0.5 and 1.0 visible when centred), except the width-3 R1-wording case where ink hides 0.5 pt of the stroke. The three renderers agree with the exact geometry to within their edge-placement errors above, and none disagrees about which cases touch or overlap. So the 1-point term holds; the *half-stroke* term does not, for an ordinary stroke painted wholly inside the widget with width of 2 or more. Poppler's favorable underline readings are its 0.1-pt vertical shift, not extra room.

### Limits

Synthetic box glyphs prove placement and containment, not shaping, outline curves, hinting or real-font bearings; the earlier Arial and Times Italic outline rows remain the only real-font evidence. Poppler and MuPDF here are not the versions in the earlier independent report; PDFium matches. No Acrobat, no other viewer, no printing. Raster edge estimates use straight-edge partial coverage and are only as good as each renderer's anti-aliasing: the arithmetic is exact to 0.001 pt, the rendered measurements are good to roughly 0.1 pt. Border colors and the `[3]` dash are fixture choices. **Not run:** any `jaytwo.Pdf` appearance generation, DA-zero search, refusal, restoration, Save/reopen, compiled-client or target-framework test; Docker.

## Real-font both-sides boundaries (2026-10-31, arithmetic only)

`real-font/real_font_boundary.py` (run `python real_font_boundary.py real-font-boundary.json`; output checked in) evaluates the **proposed, unadopted** R1 profile for installed Arial and Times Italic (font SHA-256 values in the JSON, matching the earlier inputs). Advances are the integer `/W` values the PDF serializes; the script confirmed them equal to the `/W` arrays in `Arial.pdf` and `TimesItalic.pdf` for the 11 characters used. Ink is the unhinted outline; every containment test is exact (`Fraction`) with ink rounded outward to 0.001 pt; width and height thresholds are found by bisection and checked monotonic over ±40 thousandths. Each threshold row records the verdict one grid step below and at the boundary.

| Case (12 pt unless stated) | Minimum width | Minimum height |
| --- | --- | --- |
| Arial `WWW`, left, 1-pt solid border (whole-stroke footprint) | 37.984 | 17.407 |
| Arial `WWW`, left, borderless | 35.984 | 15.407 |
| Arial `jAy`, centre, 1-pt solid | 21.770 | 17.407 |
| Arial `gyp`, right, 1-pt solid | 23.344 | 17.407 |
| Arial `" W  i "`, left, borderless | 29.336 | 15.407 |
| Arial `W` NBSP `i`, left, borderless | 19.328 | 15.407 |
| Arial `Wgj` / blank / `Wgj`, left, borderless | 22.664 | 43.004 |
| Arial `Wgj ill`, 28 pt, left, borderless | 76.648 | 33.282 |
| Times Italic `WWW`, left, 1-pt solid | 35.319 | 17.290 |
| Times Italic `jAy`, centre, 1-pt solid | 24.121 | 17.290 |
| Times Italic `Wgj` / blank / `Wgj`, left, borderless | 21.336 | 42.887 |

Agreement with earlier arithmetic: borderless edge-space (29.336) and blank-line height (43.004) match the adoption review's figures; the `WWW` solid-border width is exactly 1 pt more per side than the review's half-stroke table (36.984 → 37.984 for a whole 1-pt stroke), which is the proposed border revision. In Arial the line box, not ink, set every height; in Times Italic `WWW` the outline ink, not the advance, set the width.

**Finding to review:** Times Italic `jAy` and `fff` left-aligned and `fff` right-aligned are refused at **every** width. The glyph ink (negative left bearing of `j`; right overhang of `f`) lies outside the emitted advance span, and R1 aligns the advance span flush to the content edge while requiring ink inside the content box, so no box size can satisfy it. The 1-pt inset is not available to ink outside the span. Centred, the same text fits. This is a consequence of the current wording, not a measured renderer behaviour; whether such text should be refused, or the span shifted by the bearing, changes acceptance and is an owner matter.

**Limits:** exact arithmetic from font data; no render, no second reader, no hinting, no target-library appearance, refusal or restoration. Two fonts and 14 cases are not font-wide coverage. Independent-renderer evidence for these boundaries is **not run** (the synthetic exact-width fixture remains the only rendered boundary evidence).

### Option (d) and option (b) variants (2026-10-31, arithmetic only)

For R1 handoff decision 5 (ink outside the advance span). `real-font/real_font_boundary_d.py` (run `python real_font_boundary_d.py real-font-boundary-d.json`; output checked in) reuses the script above unchanged except for the rule being compared, adds five cases, and reports each case under three rules:

- **current**: advances, line boxes and ink all inside the content box (the table above);
- **option (d)**: advances and line boxes inside the content box; ink only needs to stay inside the border's inner edge, so it may use the 1-pt inset;
- **option (b)**: ink inside the content box, but a left- or right-aligned line is shifted inward by its edge ink overhang (rounded up to 0.001 pt).

Minimum widths in points (heights are unchanged wherever a case fits; "refused" means refused at every width):

| Case (12 pt unless stated) | Edge overhang (pt) | Current | Option (d) | Option (b) |
| --- | --- | --- | --- | --- |
| Arial `jAy`, left, 1-pt solid | 0.551 left | refused | 20.668 | 21.219 |
| Arial `jAy`, centre, 1-pt solid | — | 21.770 | 20.668 | 21.770 |
| Times Italic `WWW`, left, 1-pt solid | 1.330 right | 35.319 | 34.319 | 35.319 |
| Times Italic `Jeff`, left, borderless | 0.410 left | refused | 20.647 | 22.058 |
| Times Italic `Jeff`, right, borderless | 2.318 right | refused | refused | 22.058 |
| Times Italic `jAy`, left, 1-pt solid | 2.062 left | refused | refused | 22.059 |
| Times Italic `fff`, left, borderless | 2.092 left | refused | refused | 16.419 |
| Times Italic `fff`, right, borderless | 2.318 right | refused | refused | 16.419 |
| Times Italic `fff`, 24 pt, left | 4.184 left | refused | refused | 30.837 |
| Times Italic `jAy`, 24 pt, left | 4.125 left | refused | refused | 40.117 |

All other cases in the table above are unchanged under both options (Times Italic `Wgj`/blank/`Wgj` gains 0.004 pt under (d)). Every threshold was monotonic over ±40 thousandths.

**Findings.** (1) The current wording also refuses ordinary *upright* text: Arial `jAy` left-aligned is refused at every width, because Arial `j` has a 0.551-pt negative left bearing at 12 pt. (2) Under (d) a left- or right-aligned line is refused at every width once its edge overhang exceeds 1 pt; overhang scales with size, so Times Italic `f` (right, 0.193 em) is refused above 5.18 pt and `j`/`f` on the left (about 0.17 em) above 5.74–5.82 pt — that is, at every eligible size from the 6-pt floor up. Arial `j` (0.046 em) passes until 21.79 pt and Times Italic `J` on the left (0.034 em) until 29.26 pt. (3) Option (b) fits every case at some width; the cost is that an overhanging line sits up to its overhang (about 2 pt for Times Italic at 12 pt) further in from the aligned edge than a line without overhang.

**Limits:** exact arithmetic from font data, two fonts, 19 cases; no render, no second reader, no hinting, no target-library appearance, refusal or restoration. How other generators handle edge overhang is sourced from reader code in the next subsection; it was not rendered.

### Reader handling of ink outside the advance span (2026-10-31, sourced)

For R1 handoff decision 5. Read from the same reader source files and hashes listed under "Beveled and inset border footprint" below (hashes re-checked this pass), for the case where the reader generates a single-line text-field appearance itself. Not built, run or rendered; Acrobat and iText **not run**.

| Reader (function) | Horizontal position from | Clip on the text | Room for edge ink before the clip |
| --- | --- | --- | --- |
| Poppler 26.08.0 (`AnnotAppearanceBuilder::drawText`; `drawFieldBorder`) | Sum of glyph advances (`HorizontalTextLayouter::totalWidth`); left at `W + 2`, right at `dx − W − 2 − usedWidth` | Always, at the border's inner edge (`w w dx−2w dy−2w re W n`, emitted after any border) | 2 pt (the padding) |
| MuPDF 1.28.5 (`write_variable_text`) | Sum of `fz_advance_glyph`; left at padding 2W, right at `w − tw` | Always, at W (`b b w−2b h−2b re W n`) | W (padding 2W minus the clip at W) |
| PDFium 8076 form filler (`CPDFSDK_AppStream::SetAsTextField`) and core (`GenerateTextFieldAP` / `kTextField`) | Variable-text engine laid out in the client rect (border-deflated, no padding); its content rect is advance-based (**inference**: `CPVT_VariableText` itself not read) | Only when the content rect is wider or taller than the client/body rect; otherwise none inside the widget | No clip: edge ink paints over the border and is cut only at the widget `/BBox` |
| pdf.js 6.4.299 (`_renderText`, `_getTextWidth`) | Sum of `glyph.width`; left at 2 pt, right at `totalWidth − width − 2` | None in the text stream (only the widget `/BBox`) | 2 pt plus the border's visible half, then the `/BBox` |

No reader reads glyph bounding boxes or side bearings when aligning, none shifts a line for its overhang, and none refuses. They converge on **aligning the advance span**; they diverge on what happens to ink beyond it.

Illustration (**inference**, arithmetic on the source formulas with the overhangs measured above; not rendered, and MuPDF substitutes a base-14 font whose metrics differ slightly), 12 pt, 1-pt solid border:

| Case (overhang) | Poppler | MuPDF | PDFium | pdf.js |
| --- | --- | --- | --- | --- |
| Arial `jAy` left (0.551) | ink in padding, intact | ink in padding, intact | ink over the border, intact | ink in padding, intact |
| Times Italic `jAy` left (2.062) | 0.062 pt clipped | 1.062 pt clipped | ink over the border; 1.062 pt cut at the widget edge | ink over the border; 0.062 pt cut at the widget edge |
| Times Italic `fff` right (2.318) | 0.318 pt clipped | 1.318 pt clipped | ink over the border; 1.318 pt cut at the widget edge | ink over the border; 0.318 pt cut at the widget edge |

**Plain answer:** established readers accept such text and let a little italic or `j` ink run into the padding, the border, or off the field. Option (b) differs from all four only by moving the overhanging line inward so nothing is clipped or overpainted; options (a)/(c) refuse text every reader accepts.

### Reader text padding, line height and vertical placement (2026-10-31, sourced)

For R1 handoff decision 4. Read from the same reader source files listed under "Beveled and inset border footprint" below (same versions and hashes), for the case where the reader generates a text-field appearance itself. Not built, run or measured; Acrobat and iText **not run**.

| Reader | Clear space beyond border (horizontal) | Line height | Single-line vertical placement | Multiline start |
| --- | --- | --- | --- | --- |
| Poppler 26.08.0 | 2 pt (`x = W + 2`) | font size (`Td 0 -fontSize`) | centred (`y = 0.5·dy − 0.4·size`) | top, `dy − 3` |
| MuPDF 1.28.5 | W (padding 2W from the edge, border W) | 1.116 × size (multiline); 1.2 × size otherwise | centred (`ty = (h − size)/2`) | top, baseline 1.116 × size |
| PDFium 8076 (form filler) | 0 beyond the border footprint (`GetClientRect` deflates by the border only) | font ascent − descent (variable-text engine) | centred (`SetAlignmentV(1)`) | top |
| pdf.js 6.4.299 | 2 pt horizontal, 1 pt vertical, independent of W | `LINE_FACTOR` × size (1.35; constant defined outside the file read, value from memory) | centred (`vShift`) | top |

**Convergence:** all four centre single-line text vertically and start multiline text at the top. None refuses text that does not fit; they overflow, clip or auto-size. The clear space beyond the border varies from 0 to 2 pt. Line height varies from 1.0 to 1.35 × size, and none uses `hhea` ascent − descent + line gap (about 1.15 × size for Arial and Times). The draft R1 profile (first baseline at content top minus ascent) top-aligns single-line text, unlike all four.

## Beveled and inset border footprint (2026-10-31)

Research only, for R1 [owner decision 1](../../form-adoption-review.md#owner-decisions-needed-listed-for-review-adoption-is-not-requested-here) (reserve the whole painted border footprint plus a 1-pt inset). It asks whether beveled (`/BS /S /B`) and inset (`/BS /S /I`) borders have a footprint that can be stated exactly. It is **not** `jaytwo.Pdf` output, a target-library test or an adopted rule, and it makes no adoption recommendation. Labels: **sourced** (quoted text or reader source at a stated version), **measured** (executed here, scope stated), **inference**, **not run**.

### 1. Normative text (sourced)

ISO 32000-1:2008 (Adobe-hosted copy, SHA-256 `9de0ca9e8570d6209e8bd48a355be8eb6ec376acfc3fc3ae97cd8730351417ff`), 12.5.4, Table 166, entry `S`:

> B (Beveled) A simulated embossed rectangle that appears to be raised above the surface of the page.
> I (Inset) A simulated engraved rectangle that appears to be recessed below the surface of the page.

The same subclause says "If present, the border shall be drawn completely inside the annotation rectangle", and `W` is "The border width in points. If this value is 0, no border shall drawn. Default value: 1." Table 188 (widget) gives `BS` as "specifying the width and dash pattern that shall be used in drawing the annotation's border", adding "The annotation dictionary's AP entry, if present, shall take precedence over the L and BS entries". Table 189 (`/MK`) defines `R` (counter-clockwise multiple of 90, default 0), `BC` ("the colour of the widget annotation's border") and `BG` ("the colour of the widget annotation's background"). 12.7.3.3 (variable text) defines `/BBox` and the `/Tx` marked content but says nothing about borders, padding or band geometry.

ISO 32000-2:2020 with Errata Collection 3 (sponsored copy, local reference library; licensed text paraphrased, not reproduced): 12.5.4 Table 168 (PDF p. 488) gives B and I the same two definitions word for word, adds that `S` is the default style, and keeps "drawn completely inside the annotation rectangle". Table 169 (p. 489) limits `/BE` to square, circle, polygon and free-text annotations. Table 191 (p. 514), widget `BS`, keeps "width and dash pattern" but **drops** the 32000-1 sentence giving `AP` precedence over `BS`. Table 192 (p. 515) keeps `R`, `BC` and `BG` unchanged. 12.7.4.3 (variable text) again specifies no border geometry. The EC3 errata index has no entry for bevel, inset or border style.

**Plain answer:** neither edition specifies a band width, a relation between `/BS/W` and any band, light or dark colours, which edges are light, how `/MK/BG` or `/MK/BC` feed shading, or whether shading follows `/MK/R`. "Drawn completely inside the annotation rectangle" is the only geometric constraint. Every reader convention below is therefore implementation choice, not conformance.

### 2. Reader conventions when they generate field appearances (sourced, read at the stated versions)

Files were fetched from the upstream tags and kept in scratch storage only (`tmp/w218-bevel/src`, not committed). SHA-256 of each file as read: Poppler `poppler-26.08.0` `poppler/Annot.cc` `e1276a728386e414cfba512a4d70ce8aa2f6e67cfb5a91d15bfd1e26d481eecf`; MuPDF `1.28.5` `source/pdf/pdf-appearance.c` `6ebd1e84230439f973967b9c995ff97ccf2c9d2273e55acfe9cd99b8820e34c2` (plus `pdf-annot.c`); PDFium `chromium/8076` (the build pypdfium2 5.14.0 ships) `core/fpdfdoc/cpdf_generateap.cpp` `ca055cdd19197d23eec86b44af9118f1dd347041178862f49261cb366e647446` (plus `fpdfsdk/cpdfsdk_appstream.cpp`, `fpdfsdk/cpdfsdk_widget.cpp`, `core/fxge/cfx_color.cpp`); pdf.js `v6.4.299` `src/core/annotation.js` `197818005b61b749855087fd088f610c95a6efeec22a9d308d055a126e68e33a` (plus `src/display/annotation_layer.js`).

| Reader (path) | Band width vs `/BS/W` | Footprint inside widget | Light / dark colours | Light edges | Rotation | Text inset |
| --- | --- | --- | --- | --- | --- | --- |
| Poppler 26.08.0 (`AnnotAppearanceBuilder::drawFieldBorder`) | Two filled L-bands of width W from the widget edge; no separate ring | W | `BC`, or `BG` if `BC` is absent, lightened to `0.5·c+0.5` and darkened to `0.5·c` (direction reversed for CMYK); nothing drawn if both are absent. B: light top/left, dark bottom/right; I: the reverse | B top/left; I bottom/right | Border drawn **before** the `/MK/R` `cm`, in the physical box: shading follows the **page** | Clip at W; single-line text at `x = W + 2` |
| PDFium form filler (`CPDFSDK_AppStream::GetBorderAppStream`, used with `init_forms`) | Width doubled for B/I: outer `BC` ring of W, then bands of W | **2W** | B: light `1 g`, dark `BG/2`; `BG` absent gives 0.5 (transparent ÷ 2 is defined as RGB 0.5). I: light `0.5 g`, dark `0.75 g` regardless of colours. Ring omitted if `BC` is absent | Top/left in display space | Drawn in the rotated (display) rect with `/Matrix`: shading follows **`/MK/R`** | `GetClientRect` deflates by 2W |
| PDFium core (`CPDF_GenerateAP::GenerateFormAP`, `NeedAppearances` without the form filler) | Same 2W geometry | 2W | B: `1 g` / `0.5 g`; I: `0.5 g` / `0.75 g`; `BG` not used for shading | Display top/left | `/Matrix` from `/MK/R` | Body deflated by 2W |
| MuPDF 1.28.5 (`pdf_write_tx_widget_appearance`) | `/BS/S` is parsed (`pdf-annot.c`) but never used for text widgets: solid `BC` stroke of W at `W/2` | W (solid) | `BC` only; nothing if `BC` is absent | none | Whole AP in the display box with rotated `/Matrix` | Clip at W; text padding 2W |
| pdf.js 6.4.299 (`getBorderAndBackgroundAppearances`) | Style ignored: solid stroke of W (or 1) on `0 0 w h re`, centred on the edge | W/2 visible, half clipped by `/BBox` | `BC` only | none | Rect in display dimensions; `/Matrix` from rotation | Fixed 1 pt vertical / 2 pt horizontal, independent of W |

Widths 1, 2 and 3 scale linearly in all five (W, 2W, 2W, W, W/2). For widths larger than half the box: Poppler's L-bands cross, PDFium's even-odd ring inverts (inner rect deflated past the centre), MuPDF's solid stroke fills the box, and pdf.js replaces any W larger than half the width or height with 1 (`AnnotationBorderStyle.setWidth`). The pdf.js HTML form layer logs "Unimplemented border style: beveled/inset" and draws no bevel. A source-only aside: Poppler's text path returns `rect.x2 - rect.y2` as the display width for `/MK/R 180` (not a border effect; not measured).

**Agreement:** no two readers agree on all of footprint, colour rule and rotation. PDFium (both paths) uses 2W, Poppler W, MuPDF a solid W and pdf.js a centred W. Only PDFium follows `/MK/R` for shading; only Poppler derives shading from `BC`; only PDFium's form filler uses `BG`.

**Acrobat / Adobe:** Acrobat not run (unavailable). No Adobe document describing Acrobat's bevel geometry was consulted in this pass. That PDFium's 2W, white / `BG`÷2 convention imitates Acrobat is **inference**, unverified. MuPDF's only Acrobat claim concerns its cloud constants, not bevels.

### 3. Fixture and three-renderer measurement (measured)

Sibling scripts in [exact-width/](exact-width/) reuse `exact_width_fixture.py`'s synthetic font, so ink, advance and line box of `B` coincide:

| File | Role |
| --- | --- |
| `bevel_inset_fixture.py` | Builds `bevel-inset-fit.pdf` (presentation colours), `bevel-inset-measure.pdf` (same geometry, measurement palette), `bevel-inset-regen.pdf` (no `/AP`) and `bevel-inset-manifest.json`. |
| `bevel_inset_check.py` | From the PDF bytes: `/BBox`, rotation `cm`, painted-footprint depth on each display side (exact polygon/mid-line intersections), ink box from `/W` and `glyf`, clearance. Writes `bevel-inset-structural.json`. |
| `bevel_inset_render.py` | Poppler, MuPDF, PDFium at 576 dpi (settings as in the exact-width section). Writes `bevel-inset-render.json`. |
| `bevel_inset_summary.py`, `run_bevel_inset.ps1` | Print the tables below; run all steps. |

```
python docs/spec/drafting/research/w218-appearance/exact-width/bevel_inset_fixture.py
python docs/spec/drafting/research/w218-appearance/exact-width/bevel_inset_check.py
python docs/spec/drafting/research/w218-appearance/exact-width/bevel_inset_render.py --pdftoppm <path-to-pdftoppm>
python docs/spec/drafting/research/w218-appearance/exact-width/bevel_inset_summary.py
```

**Fixture choices (not reader conventions, not proposals).** Because the spec gives no geometry, the fixture paints the PDFium construction: an outer `/MK/BC` ring of W plus light (top/left) and dark (bottom/right) bands of W, a **2W** footprint, shaded in display coordinates (following `/MK/R`). Presentation colours follow the PDFium form filler: B light `1 g`, dark `BG/2`, or `0.5 g` without `BG`; I `0.5 g` / `0.75 g`; ring red; `BG` (0.6, 0.8, 1). The measurement PDF paints ring red, light band green and dark band blue on white, with black text, so footprint and ink edges separate by colour. `BBB` at 12 pt touches all four content edges under the candidate rule (content side = 1 + 2W). Cases: B and I × W 1, 2, 3 × `/MK/R` absent/0/90/180/270 × `/MK/BG` present/absent (60). Controls: a **ring-only** rule (1 + W, as if the border were solid) at W 1 and 2, and a Poppler-like **band-only** construction (footprint W) under the candidate rule for B and I at W 2.

**Versions and hashes.** Windows 11 10.0.26100; Python 3.13.1 (scratch venv); pypdf 6.21.4; fontTools 4.68.0; numpy 2.5.4; Pillow 13.0.0; Poppler `pdftoppm` 26.08.0 (repo `.tools/msys64`); MuPDF 1.28.5 via PyMuPDF 2.0; PDFium 8076 via pypdfium2 5.14.0 (`init_forms`, `may_draw_forms=True`). Two generations were byte-identical. `bevel-inset-fit.pdf` `e3df04de5c69c9b9b24b9ff4ab67375af5c89b22290f50932a2158aba5e48886`; `bevel-inset-measure.pdf` `89e3e904c621ca213a11022e2456b96bdd9d5da640eeb55abb79c8ef9bfaebf4`; `bevel-inset-regen.pdf` `8d05f6d6824992d60ed11baddf43b740ee2669bf1b27a8bbc6164981b96c5955`; font program `ceb976ed26b95d66ef61ea7af958356743e11f3bdfd137850c6866bc7ffe467b` (identical to the exact-width font). The exact-width section used Poppler 26.07.0; this pass used 26.08.0.

**Structural result (exact).** `bevel_inset_check.py` passed 64/64 pages of each PDF. On every side of every case the painted footprint is exactly 2W (W for band-only), ink touches the content box, `/BBox` and `cm` match `/MK/R`, and clearance equals the nominal value exactly: 1 under the candidate rule; 0 and −1 for the ring-only controls at W 1 and 2.

**Rendered result.** Medians over the middle 40 % of each side, all four physical sides, in points. P / M / F = Poppler / MuPDF / PDFium:

| Construction / rule / W | Pages | Nominal footprint, clearance | Visible footprint P / M / F | Ink depth from edge P / M / F | Clearance P / M / F |
| --- | --- | --- | --- | --- | --- |
| ring + band / candidate / 1 | 20 | 2, 1 | 1.984–2.016 / 2.000 / 2.000 | 2.900–3.100 / 2.975–3.025 / 3.000 | 0.884–1.115 / 0.975–1.025 / 1.000 |
| ring + band / candidate / 2 | 20 | 4, 1 | 3.984–4.016 / 4.000 / 4.000 | 4.900–5.100 / 4.975–5.025 / 5.000 | 0.884–1.115 / 0.975–1.025 / 1.000 |
| ring + band / candidate / 3 | 20 | 6, 1 | 5.984–6.016 / 6.000 / 6.000 | 6.900–7.100 / 6.975–7.025 / 7.000 | 0.884–1.115 / 0.975–1.025 / 1.000 |
| band only / candidate / 2 | 2 | 2, 1 | 2.000–2.016 / 2.000 / 2.000 | 2.900–3.100 / 2.975–3.025 / 3.000 | 0.884–1.100 / 0.975–1.025 / 1.000 |
| ring + band / ring-only control / 1 | 1 | 2, 0 | 1.900–2.000 / 1.975–2.000 / 2.000 | 1.900–2.100 / 1.975–2.025 / 2.000 | 0.000–0.115 / 0.000–0.025 / 0.000 |
| ring + band / ring-only control / 2 | 1 | 4, −1 | 2.900–3.084 / 2.975–3.025 / 3.000 | 2.900–3.100 / 2.975–3.025 / 3.000 | 0.000–0.016 / 0.000 / 0.000 |

* **Candidate rule:** all 62 candidate-rule pages keep a measured clearance within each renderer's placement error of the nominal 1.0 pt (Poppler ±0.12, consistent with its whole-pixel 0.1-pt text shift in the exact-width section; MuPDF ±0.025; PDFium exact) at W 1, 2 and 3, every rotation and both `BG` states. Measured bands equal 2W (W band-only). No renderer disagrees about touching or overlap.
* **Ring-only control:** counting only `/BS/W` leaves 0 pt at W 1 (text touches the band). At W 2, ink starts at 3 pt while the band runs to 4 pt, so 1 pt of band is hidden under the text (visible footprint 3, clearance 0). All three renderers show this the same way. Treating this 2W bevel as a W stroke fails at every W ≥ 1.
* **Rotation and shading:** all three renderers paint the light bands where the AP puts them: physical left/top for absent and 0, right/bottom for 180, left/bottom for 90, right/top for 270. Absent and explicit 0 agree. This shows only that readers honour the AP's shading; display-space shading is the fixture's choice.
* **Colours** (presentation PDF, band mid-points, W 2, rotation 0): all three paint the AP's colours to within 1/255. B with `BG`: light (1, 1, 1), dark (0.30, 0.40, 0.50) = `BG`/2. B without `BG`: dark 0.50 grey. I: 0.50 / 0.75 grey. Ring (1, 0, 0).
* **Measurement artefacts:** anti-aliased blends of two palette colours (at the mitred corners, and at Poppler's off-grid ring/band boundary on top and bottom) are dark but not black. Ink is therefore searched for only from the visible footprint's inner edge, within the mid-side strip. Two earlier runs with a whole-page ink box, and with the search starting at the widget edge, misread those blends as ink and are superseded.

### 4. Reader-generated look-alike reference (measured; not a target)

`bevel-inset-regen.pdf` has 24 text widgets: 60 × 24 pt (14 pt high for the oversize case), value `Ab`, `/Helv 6 Tf`, `/MK/BC` (1, 0, 0), `/MK/BG` (0.6, 0.8, 1) where stated, no `/AP`, `NeedAppearances true`. Colour runs were read from each physical edge inward on the mid-line (full per-case runs are in `bevel-inset-render.json`):

| Case | Poppler 26.08.0 | MuPDF 1.28.5 | PDFium 8076 (`init_forms`) |
| --- | --- | --- | --- |
| B, W 1/2/3, `BG` | light (1, 0.5, 0.5) on left/top, dark (0.5, 0, 0) on bottom/right, each W; then `BG` | solid red W on all sides (style ignored) | red ring W, then a W band: white (left/top) or (0.3, 0.4, 0.5) = `BG`/2 (bottom/right). Footprint 2W |
| B, W 2, no `BG` | as with `BG` (shading comes from `BC`) | solid red 2 | red ring 2, then white / 0.5 grey band 2 |
| I, W 2, `BG` | (0.5, 0, 0) left/top, (1, 0.5, 0.5) bottom/right, each 2 | solid red 2 | red ring 2, then 0.5 grey (left/top) / 0.75 grey (bottom/right) band 2 |
| B and I, W 2, `/MK/R` 90 | light still on physical left/top (page-following); 180 and 270 likewise | solid red 2 | light on physical left/bottom = display top/left (follows `/MK/R`); 180 and 270 likewise |
| B, W 2, no `BC`, no `BG` | nothing painted | nothing painted | ring and white band invisible; 0.5 grey band at 2–4 pt on bottom/right |
| B and I, W 8, 14-pt-high box | bands cross: about 7 pt of one shade, then the other | box filled red | nearly the whole box red (inverted even-odd ring) |

These agree with the source reading in section 2. PDFium without `init_forms` drew nothing for these AP-less widgets, so its core `cpdf_generateap` path was **not observed** (source only). Poppler printed "No display font for 'Symbol' / 'ArialUnicode'" while rendering this file; borders were drawn. pdf.js rendering: **not run** (source only). Acrobat: **not run**.

### 5. Cloudy `/BE` (sourced; no fixture)

Both editions define `/BE /S /C` ("cloudy", intensity `I` 0–2, `BS` width and dash honoured) only for square, circle, polygon and (PDF 1.6) free-text annotations. Neither lists `BE` for widgets, and neither gives curve radius, spacing or phase. MuPDF 1.28.5 draws clouds for exactly Circle, FreeText, Polygon and Square (`border_effect_subtypes`), with radius `4·I + W/2` and spacing `6.67·I + 0.83·W`, under the source comment "Constants measured from Acrobat Reader" (unverified). The fetched Poppler, PDFium and pdf.js files contain no cloud drawing. **Inference:** a cloudy widget border has no normative footprint. The only implemented footprint is one reader's Acrobat imitation, for other annotation types, so a footprint could be determined only by inventing a construction.

### Limits

Synthetic box glyphs prove placement and containment, not real-font shaping. The fixture's 2W construction and colours are one reader's convention, chosen so the candidate rule can be exercised. Poppler's W bands or a centred stroke give other footprints. The candidate rule holds whenever the footprint is computed from the AP actually written. Reader source was read, not built or instrumented. Raster readings are good to about 0.1 pt. **Not run:** `jaytwo.Pdf` generation, DA-zero search with bevel insets, refusal, restoration, Save/reopen, compiled clients, Docker, pdf.js rendering, Acrobat, printing, grey or CMYK `BC`/`BG`, page `/Rotate`.
