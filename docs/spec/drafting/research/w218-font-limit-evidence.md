# W-218 supplied-font size-limit evidence

**Independent research, 2026-10-31. Executed measurements only; not target-library (.NET) evidence; no limit is adopted and no number is selected as policy.** This feeds ledger A4 option (d'), [FORM-FONT-05/09/10](../../forms.md#supplied-and-retained-font-byte-budgets-form-font-05) and the [R4 protocol](../../forms.md#r4-measurement-protocol-single-size-limit-and-proportional-growth). It does not advance A1 beyond the evidence below. Raw records: [w218-font-limit-evidence.json](w218-font-limit-evidence.json). No font program is committed or redistributed; downloads sit in the untracked `tmp/w218-font-limit/fonts/`.

## What each result shows (property, not more)

| Measurement | Property it shows | What it does not show |
| --- | --- | --- |
| Header-only classification and fontTools facts of 18 downloaded and 6 installed fonts | Real static-glyf (FORM-FONT-03-shaped) input sizes across scripts; composite nesting depth in real fonts | Shaping, glyph correctness, P1 coverage, embedding rights |
| Header-only scan of 346 installed font files | Size distribution of what ships on one Windows 11 machine | Other platforms or user-installed fonts |
| Read-only scan of 33 corpus PDFs | Which font programs real business PDFs embed, and at what size | What callers would supply as new fonts |
| Six-tool run (five PDF writers plus the fontTools subsetter) on one script-appropriate string per font (3 runs) | Whether established embedders accept each font and how time and working-set peak vary with font bytes | Layout, shaping, rendering or visual correctness. Only `qpdf --check` and `pdffonts` ran on the outputs, as structural checks |
| Synthetic size ladder 8 to 512 MiB | How each tool's time, memory and output respond to file size alone, glyph data, text and cmap held fixed | Real fonts of that size (none exist in this range) |

## Environment and tool versions

Windows 11 10.0.26100 x64, 8 logical CPUs, 63.3 GiB RAM. Python 3.13.1 in a new venv (`tmp/w218-font-limit/venv`): fontTools 4.68.0, pypdf 6.21.1 (installed, unused in the final run), PyMuPDF 2.0 with MuPDF 1.28.5, pypdfium2 5.14.0 with PDFium 156.0.8076.0, reportlab 5.1.0, fpdf2 2.9.0, psutil 7.2.2. iText 9.8.0 on .NET 10.0.13 (SDK 10.0.402), restored from the repo's `.tools/nuget-packages` and used only as an AGPL comparator on local files. Checkers: qpdf 12.3.2, poppler `pdffonts` 26.08.0 and mutool 1.28.5 from `.tools/msys64/ucrt64`. Other tools were absent: Ghostscript and LibreOffice were not available and were not run.

## Commands (exact, executed from the repository root)

```text
python -m venv tmp/w218-font-limit/venv
tmp/w218-font-limit/venv/Scripts/python.exe -m pip install fonttools==4.68.0 pypdf==6.21.1 pymupdf pypdfium2 reportlab fpdf2 psutil
# fonts: curl -sL -o <name> <url> from inside tmp/w218-font-limit/fonts (URLs in the JSON download_url field)
tmp/w218-font-limit/venv/Scripts/python.exe -I docs/spec/drafting/research/w218-font-limit-evidence.py --repo . --fonts tmp/w218-font-limit/fonts --sysfonts C:/Windows/Fonts --work tmp/w218-font-limit/work --out tmp/w218-font-limit/evidence-run1.json --runs 3
dotnet build -c Release -o ../itext-bin   # in tmp/w218-font-limit/itext-harness (source: w218-font-limit-itext.cs.txt)
tmp/w218-font-limit/venv/Scripts/python.exe -I docs/spec/drafting/research/w218-font-limit-itext.py --evidence <evidence.py> --harness <itext-harness.exe> --fonts tmp/w218-font-limit/fonts --sysfonts C:/Windows/Fonts --work tmp/w218-font-limit/work --out tmp/w218-font-limit/itext-run1.json --runs 3
tmp/w218-font-limit/venv/Scripts/python.exe -I docs/spec/drafting/research/w218-font-limit-padding.py --evidence <evidence.py> --base tmp/w218-font-limit/fonts/NotoSans-Regular.ttf --work tmp/w218-font-limit/ladder2 --out tmp/w218-font-limit/padding-run2.json --sizes-mib 8,16,32,64,128,256,512 --harness <itext-harness.exe> --runs 1
tmp/w218-font-limit/venv/Scripts/python.exe -I docs/spec/drafting/research/w218-font-limit-merge.py --evidence ...evidence-run1.json --itext ...itext-run1.json --padding ...padding-run2.json --out docs/spec/drafting/research/w218-font-limit-evidence.json
```

Script SHA-256 (also in the JSON): evidence `2791c423711e7f1d73cbbbee58105fe80b3b1f4e381ebba61e24fff33f31b713` (matches the file now on disk). The other scripts' hashes are recorded in their run records. A first padding run died at the 512 MiB rung from a `random.randbytes` overflow in my script. The script was fixed (chunked generation) and the whole ladder re-run; only the second run is recorded. A first iText run failed on a relative harness path and was re-run with an absolute path. Fonts are untrusted: Python ran with `-I`, paths were arguments and nothing was executed from the font directory. No download was refused with 403. Two guessed URLs (DejaVu, Noto Serif) returned 404 and were discarded. The tests below were run by me; no Docker, repository build or test run applies and none was run.

## 1. Acquired standalone fonts

All are single-face static `glyf`/`loca` sfnt files except the two marked variable. Bytes are exact; SHA-256 is truncated here and complete in the JSON. Licence is the font's own name table (ID 13/14) unless noted. Coverage = how many of a fixed sample range per script the best cmap maps (full counts in the JSON).

| Font (version) | Bytes (MiB) | SHA-256 | Licence | Scripts covered | Glyphs / composites / max depth |
| --- | --- | --- | --- | --- | --- |
| Noto Sans Regular 2.015 | 825,628 (0.79) | f5f552c8c5ed | OFL 1.1 | Latin, Greek, Cyrillic, Devanagari (partial) | 4,515 / 1,848 / 1 |
| Liberation Sans 2.1.5 | 410,712 (0.39) | 76d04c18ea24 | OFL 1.1 | Latin, Greek, Cyrillic | 2,620 / 1,076 / 1 |
| Noto Sans Hebrew 3.001 | 66,156 (0.06) | 671951828bd5 | OFL 1.1 | Hebrew | 470 / 200 / 1 |
| Noto Sans Arabic 2.013 | 271,652 (0.26) | 7ed3fe069312 | OFL 1.1 | Arabic | 1,702 / 1,149 / 1 |
| Noto Naskh Arabic 2.021 | 291,980 (0.28) | c9a039ce48a4 | OFL 1.1 | Arabic | 1,719 / 1,154 / 1 |
| Amiri 1.002 | 431,116 (0.41) | ab391c4147d0 | OFL 1.1 | Arabic | 6,710 / 5,925 / 1 |
| Noto Sans Devanagari 2.007 | 277,680 (0.26) | 573e525d9f49 | OFL 1.1 | Devanagari | 1,117 / 377 / 1 |
| Hind Regular 2.001 | 299,532 (0.29) | 01de158022f5 | OFL 1.1 | Devanagari | 1,006 / 0 / 0 |
| Noto Sans Tamil 2.004 | 115,856 (0.11) | 0afbc2219642 | OFL 1.1 | Tamil | 576 / 223 / 1 |
| Noto Sans Thai 2.002 | 72,812 (0.07) | 2101faa47194 | OFL 1.1 | Thai | 467 / 182 / 1 |
| Sarabun 1.000 | 90,220 (0.09) | 226d4f368fbc | OFL 1.1 | Thai | 777 / 343 / 2 |
| Kosugi 4.002 | 2,288,848 (2.18) | f5e81d6a6b86 | Apache 2.0 | Japanese kana, Han | 7,576 / 0 / 0 |
| M+ 1p Regular 1.062 | 1,758,688 (1.68) | 2f294ad49643 | OFL 1.1 | Japanese, Han, Hebrew | 8,676 / 244 / 1 |
| Nanum Gothic 3.020 | 2,054,744 (1.96) | 76f45ef4a6bc | OFL 1.1 per google/fonts `ofl/` directory; name table names NHN only, unverified | Hangul | 13,297 / 8,825 / 1 |
| Droid Sans Fallback Full 1.00c (Android 4.4) | 4,529,044 (4.32) | 97320619b0f1 | Apache 2.0 | Han, kana, Greek, Cyrillic | 50,713 / 26,263 / 1 |
| LXGW WenKai Regular 1.521 | 25,486,932 (24.31) | b64b7add2976 | OFL 1.1 | Han, kana, Hangul, Greek, Cyrillic | 46,788 / 4 / 1 |
| Noto Sans SC `[wght]` 2.004 (variable, ineligible, comparator) | 17,772,300 (16.95) | a3041811a78c | OFL 1.1 | Han, kana | 31,036 / n/a / n/a |
| Roboto `[wdth,wght]` 3.015 (variable, ineligible, comparator) | 488,584 (0.47) | d7598e12c5db | OFL 1.1 | Latin, Greek, Cyrillic | 1,326 / n/a / n/a |

Download sources: Noto fonts from `notofonts/notofonts.github.io` (`fonts/<Family>/full/ttf`), Amiri, Hind, Sarabun, M+ 1p, Kosugi, Nanum Gothic, Noto Sans SC and Roboto from `google/fonts` (`main`), Liberation from the 2.1.5 release tarball, LXGW WenKai from release v1.521, Droid Sans Fallback from the `aosp-mirror` frameworks tree at tag `android-4.4_r1`. Exact URLs are in the JSON. All were fetched on 2026-10-31 from the unversioned branch tips named, so the hash, not the URL, is the identity.

Installed Windows fonts, compared in place (not copied, not redistributable): `arial.ttf` 1,045,720 (v7.06, composites 2,030, depth 2), `times.ttf` 1,190,736, `calibri.ttf` 1,650,632, `segoeui.ttf` 960,076, `malgun.ttf` 13,459,196 (12.84 MiB, v6.69, Korean, Han, Greek, Cyrillic), `simsunb.ttf` 21,894,056 (20.88 MiB, v5.08, SimSun-ExtB, 60,455 glyphs, no composites).

Observation about the library's own needs: the real Latin fonts all have composite glyphs (up to 2,288 in Segoe UI) with maximum nesting depth 1 or 2. The deepest nesting found in 24 fonts is 2. This is an observation about these fonts only. It shows ordinary composites are common and shallow, so a nesting refusal point is not what sets the size limit.

## 2. Business-PDF pairings (corpus, read-only)

`test/jaytwo.Pdf.Tests/Fixtures/corpus`: 33 PDFs scanned (one public-key encrypted file would not open in PyMuPDF and one RC4 file's streams cannot be decoded without a password, recorded). 25 PDFs embed fonts: 113 embedded programs, 74 `FontFile2` (TrueType) and 39 `FontFile3` (CFF/Type1C/OpenType). 111 of 113 carry a subset tag. Decoded embedded-program sizes: median 25,160 bytes, maximum 806,504 bytes (`CourierNewPSMT` in `va-21-4138.pdf`, untagged full-face embedding of 0.77 MiB). The next largest are Calibri subsets of 352,240, 326,884 and 301,008 bytes from Word and Excel exports. The embedded families are Calibri, Arial, Times New Roman, Segoe UI, Georgia, Consolas, Courier New, Helvetica Neue and Franklin Gothic. **Property shown:** real forms and documents embed Latin subsets of at most 0.35 MiB, plus one whole 0.77 MiB face. None embeds CJK, Arabic or Indic fonts, so the corpus contains no evidence for large fonts. That evidence comes only from sections 1, 3 and 4.

## 3. Established-embedder comparison (real fonts)

Each cell is the minimum to maximum of 3 fresh-process runs. Text is a script-appropriate string of 95 to about 390 scalars chosen from the font's own cmap by direct mapping (no shaping). "Peak" is the child's `peak_wset` delta over the post-import baseline and **includes lazy library imports** (fontTools is imported inside PyMuPDF's subsetting; PyMuPDF's 106 MiB floor on tiny fonts is that import). Only the differences between fonts of one tool are meaningful. Times are wall seconds. The PyMuPDF cell covers both its unsubsetted save and its `subset_fonts()` save. The fontTools column is a subsetting operation and produced no PDF.

| Font (MiB) | reportlab (subset) | fpdf2 (subset) | PyMuPDF whole / subset | PDFium (whole) | iText full / subset | fontTools subset |
| --- | --- | --- | --- | --- | --- | --- |
| Noto Sans Hebrew (0.06) | 0.10 to 0.13 s, 7 MiB | 0.73 to 0.90 s, 40 MiB | 1.31 to 1.41 s, 106 MiB | 0.09 to 0.10 s, 10 MiB | 0.38 to 0.47 s, 24 MiB / 22 MiB | 0.27 to 0.41 s, 16 MiB |
| Noto Sans (0.79) | 0.11 to 0.12 s, 10 MiB | 0.75 to 0.93 s, 44 MiB | 1.26 to 1.39 s, 106 MiB | 0.12 to 0.16 s, 15 MiB | 0.57 to 0.74 s, 38 MiB / 37 MiB | 0.75 to 0.99 s, 40 MiB |
| arial.ttf (1.00) | 0.12 to 0.14 s, 11 MiB | 0.89 to 0.93 s, 46 MiB | 1.27 to 1.40 s, 106 MiB | 0.12 to 0.15 s, 16 MiB | 0.57 to 0.59 s, 40 MiB / 37 MiB | 0.82 to 0.95 s, 41 MiB |
| Droid Sans Fallback (4.32) | 0.16 to 0.17 s, 29 MiB | 1.05 to 1.34 s, 79 MiB | 1.59 to 1.66 s, 117 MiB | 0.34 to 0.42 s, 38 MiB | 1.06 to 1.17 s, 78 MiB / 48 MiB | 2.27 to 2.38 s, 110 MiB |
| malgun.ttf (12.84) | 0.17 to 0.20 s, 45 MiB | 1.01 to 1.11 s, 74 MiB | 1.42 to 1.67 s, 133 MiB | 0.50 to 0.54 s, 79 MiB | 1.35 to 1.53 s, 93 to 99 MiB / 42 MiB | 3.00 to 3.12 s, 121 MiB |
| simsunb.ttf (20.88) | 0.17 to 0.18 s, 71 MiB | 1.05 to 1.28 s, 96 MiB | 1.47 to 1.57 s, 145 MiB | 0.76 to 0.88 s, 123 MiB | 2.27 to 2.64 s, 149 MiB / 49 MiB | 9.37 to 10.34 s, 260 MiB |
| LXGW WenKai (24.31) | 0.20 to 0.21 s, 76 MiB | 1.17 to 1.32 s, 102 MiB | 1.66 to 1.96 s, 157 MiB | 0.81 to 0.87 s, 141 MiB | 2.08 to 2.44 s, 158 MiB / 60 MiB | 11.68 to 12.03 s, 288 MiB |

All 24 fonts, per tool and run, are in the JSON (`embedder_and_font_run.embedder_comparison`, `itext_run.runs`). Results:

* **Every static-glyf font was accepted by every tool in every run**, from 0.06 MiB to 24.31 MiB. No tool refused, failed or timed out. All 96 run-0 output PDFs (24 fonts times reportlab, fpdf2, PyMuPDF and PDFium) passed `qpdf --check` (exit 0) and `pdffonts` (exit 0); iText outputs were checked only by re-reading the embedded font streams. Both checks are structural only. fontTools is a font subsetter, not a PDF embedder, so it produced no PDF. iText and PDFium also accepted the variable fonts except iText on Noto Sans SC `[wght]`, which threw `ArgumentOutOfRangeException` (variable fonts are outside FORM-FONT-03 anyway). The mainstream embedders therefore showed no font-size refusal in this range.
* **Subsetters embed small fonts regardless of input size.** Embedded `FontFile2` streams from the three subsetting PDF writers are about 6 to 93 KB for every font; for example Malgun (12.84 MiB) yields 65,823 bytes in reportlab and LXGW (24.31 MiB) yields 49,693 bytes. **Property:** the output tracks requested text, not file size, when subsetting.
* **Whole-font embedders embed roughly the whole file.** PDFium (Flate) embeds 7.45 MB for Malgun and 12.7 MB for LXGW; iText full 7.44 MB and 12.8 MB; PyMuPDF's unsubsetted save is 13.6 MB and 25.7 MB. **Property:** the no-subsetting case (FORM-FONT-07) scales output and memory with font bytes. On the 24.3 MiB LXGW font that was about 6.3× in iText full managed allocation (152 MiB) and about 5.8× in PDFium working-set delta (141 MiB).
* **Time and peak memory grow roughly in proportion to input bytes for tools that read the whole file or the glyf table** (fontTools: 0.27 s at 0.06 MiB, 11.7 s and 288 MiB at 24.3 MiB; PDFium whole: 0.09 s to 0.81 s, 10 MiB to 141 MiB) **and are nearly flat for tools that read only needed glyphs** (reportlab 0.2 s; fpdf2 about 1 s; iText subset 0.4 to 0.6 s). Fonts of different structure confound the comparison, so no coefficient is claimed. These are not .NET target measurements.

## 4. Synthetic size ladder (valid sfnt padded with an unreferenced table)

`NotoSans-Regular` (825,628 bytes) plus a seeded pseudo-random private `PADD` table to the stated size; cmap, glyf and the text string are unchanged. **Synthetic, not a real font.** One run per rung. Each cell is wall seconds / working-set peak delta in MiB. PDF size was constant (30 to 48 KB) for subsetting tools.

| MiB | fontTools subset | reportlab | fpdf2 | PyMuPDF | PDFium whole | iText subset | iText full |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 8 | 0.95 / 46 | 0.15 / 17 | 0.94 / 44 | 1.97 / 118 | 0.32 / 50 | 0.48 / 37 | 1.24 / 70 |
| 16 | 1.14 / 54 | 0.16 / 25 | 0.96 / 45 | 1.95 / 126 | 0.62 / 91 | 0.58 / 37 | 1.30 / 103 |
| 32 | 1.00 / 71 | 0.16 / 41 | 0.94 / 45 | 1.79 / 154 | 0.93 / 171 | 0.46 / 37 | 1.95 / 166 |
| 64 | 0.97 / 103 | 0.18 / 73 | 1.21 / 44 | 1.97 / 215 | 1.83 / 330 | 0.47 / 37 | 3.19 / 295 |
| 128 | 0.97 / 167 | 0.21 / 137 | 1.02 / 44 | 2.85 / 352 | 3.36 / 651 | 0.44 / 37 | 5.50 / 553 |
| 256 | 1.03 / 295 | 0.24 / 265 | 0.88 / 45 | 3.52 / 478 | 6.59 / 1291 | 0.50 / 37 | 10.43 / 1068 |
| 512 | 1.18 / 551 | 0.42 / 521 | 0.88 / 45 | 5.25 / 946 | 13.44 / 2571 | 0.44 / 37 | 20.80 / 2098 |

**Properties shown.** No tool refused or failed at any rung up to 512 MiB, so none of the six mainstream embedders has a fixed font-size ceiling at or below 512 MiB on this 63 GiB machine; the practical ceiling is memory. Memory for reportlab, fontTools and PDFium rises about linearly with file bytes (about 1.0×, 1.1× and 5×); PyMuPDF about 1.8×; iText full about 4×. fpdf2 and iText subset are flat, because they do not read the unused data. Whole-font embedding makes the output PDF as large as the font (536 MB for 512 MiB). The ladder shows tool behaviour on padding; it says nothing about real glyph data, composites or hostile structure.

## 5. Size distribution

* **Largest legitimate standalone static-glyf font found:** LXGW WenKai Regular, 25,486,932 bytes (24.31 MiB), OFL 1.1, acquired and embedded successfully by all six tools. Next: SimSun-ExtB 21,894,056 (20.88 MiB, installed), Malgun Gothic 13,459,196 (12.84 MiB, installed), Malgun Gothic Bold 12.0 MiB (installed, header only), Segoe UI Emoji 12.3 MiB (colour font, outside FORM-FONT-03), Droid Sans Fallback 4.32 MiB.
* **Largest font a mainstream embedder accepted:** a real 24.31 MiB static-glyf font in all six tools and a synthetic 512 MiB in all six. The tools do not impose a ceiling in that range.
* **This machine's 346 font files:** 325 static glyf, 15 collections (largest `mingliub.ttc` 35.4 MiB, `msjh.ttc` 20.4 MiB, `msyh.ttc` 18.8 MiB, `simsun.ttc` 17.5 MiB, all refused by FORM-FONT-03), 6 variable (largest 1.8 MiB), 0 CFF. Static-glyf files at or above 1, 2, 4, 8 and 16 MiB: 19, 8, 5, 4 and 1 of 325; none at or above 32 MiB. Only 4 of 325 are 8 MiB or larger, all CJK or emoji.
* **Fonts embedded in the corpus** are 0.34 MiB or smaller as subsets, apart from one untagged 0.77 MiB Courier New; the corresponding source faces are 0.9 to 1.7 MiB (Segoe UI, Arial, Times New Roman, Calibri).
* **Reference points inside the library:** other fixed input limits in `src` are 8 MiB (signature appendix, assembled appearance graph), 32 MiB (XMP document), 2 MiB (TSA response), 1 MiB (reserved signature contents) and 64 KiB (token).

## 6. Candidate limits

"Refuses" lists fonts in this evidence that FORM-FONT-05's gate (at or above L) would refuse, among fonts that pass FORM-FONT-03.

| Candidate L | Accepts | Refuses | Refuses a font established tools embed? |
| --- | --- | --- | --- |
| 8 MiB (matches signature-appendix and assembly limits) | all Latin, Greek, Cyrillic, Hebrew, Arabic, Indic, Thai fonts; Droid Sans Fallback 4.32 MiB; Kosugi, M+ 1p, Nanum Gothic | Malgun Gothic 12.84 MiB and Bold 12.0 MiB, SimSun-ExtB 20.88 MiB, LXGW WenKai 24.31 MiB | **Yes: Malgun Gothic** ships with Windows as the default Korean UI font and all tools processed it in every run. **A4 trigger.** |
| 16 MiB | everything above plus Malgun Gothic | SimSun-ExtB 20.88 MiB, LXGW WenKai 24.31 MiB | **Yes, probably:** both are accepted by every tool; SimSun-ExtB ships with Windows and LXGW is a widely distributed OFL CJK font. Whether they are "representative" is an owner call. Treat as an **A4 trigger** unless the owner rules them unrepresentative. |
| 32 MiB (matches XMP limit) | all 22 eligible fonts found, up to 24.31 MiB | none found | **No font in this evidence.** 1.3× headroom over the largest found. Not shown: no font between 24 and 32 MiB was found or measured. |
| 64 MiB | same | none found | No. 2.6× headroom. Doubles the worst-case whole-font working set (see below). |

Resource cost at a candidate (observed, not guaranteed): a whole-font, no-subsetting embed at 32 MiB measured at about 170 MiB working-set delta in PDFium and about 166 MiB in iText full on the padded ladder, with a 33 MB output; at 64 MiB about 330 MiB and 295 MiB.

## 7. Recommendation (for the primary agent to merge; nothing adopted)

**32 MiB is the smallest conventional limit that refuses no font found and is consistent with an existing fixed limit (XMP, 32 MiB).** Reasoning: (1) it accepts every eligible font in the evidence, including the largest legitimate standalone font found, so it does not narrow acceptance on this evidence; (2) it is the library's own largest fixed byte limit, which A4's "consistent with the library's other fixed input limits" asks for, and the borderline fonts are CJK fonts above 8 MiB that the 8 MiB limits would refuse; (3) memory at that size is about 170 MiB for a whole-font embed in two established tools, proportional to font bytes rather than glyph nesting, which matches A4's proportionality clause; (4) established embedders showed no size ceiling to match, so the limit is a library policy, not an embedder compatibility floor. 64 MiB is the alternative if the owner wants more than 1.3× headroom over the largest font found; it costs twice the worst-case working set. 8 MiB and 16 MiB are not recommended because they refuse real fonts that every tested embedder accepts.

## 8. A4 triggers and proposed wording

**No trigger arises if 32 MiB or 64 MiB is chosen on this evidence.** If the owner prefers a smaller limit the following returns under A4 with this wording:

> A supplied font program of 8 MiB or more refuses with a resource-limit failure. This refuses real-world fonts that every established embedder tested accepts, including Malgun Gothic (12.84 MiB, the default Windows Korean UI font) and SimSun-ExtB (20.88 MiB). This is a narrowing of accepted fonts under ledger A4.

Alternatives: (a) 16 MiB, refusing SimSun-ExtB and LXGW WenKai (same A4 route, owner decides whether they are representative); (b) 32 MiB, refusing none found; (c) 64 MiB, refusing none found. Proposed boundary text for FORM-FONT-05 if 32 MiB is chosen: "A supplied font whose actual size is 32 MiB (33,554,432 bytes) or more is refused." The XMP code refuses only when it exceeds 32 MiB; FORM-FONT-05 refuses at or above L, so the boundary differs by one byte. The owner may wish to align them.

## 9. Material limitations

* **Not target evidence.** Nothing here runs the jaytwo library, .NET resource accounting, or restoration/cancellation. Peaks are working-set deltas from child processes, include lazy imports and runtime start-up, and are not allocation or retained-memory measurements. Python tools dominate the table. Only iText reports managed allocation, and not as a library-comparable figure.
* **Representativeness is thin where it matters.** The acquired CJK sample is eight fonts. Only one font above 13 MiB is eligible and openly licensed (LXGW). Fonts I did not obtain (for example Arial Unicode MS, HanaMin, BabelStone Han) may lie between 24 and 32 MiB or above; my belief that Arial Unicode MS is about 22 MiB is from memory and unverified. This evidence cannot rule out a representative eligible font above 24.31 MiB.
* **Not measured:** shaping for Arabic/Indic/Thai (direct mapping only), glyph correctness, visual rendering of any output (no `mutool draw` or image comparison ran), hostile composite or cyclic fonts, truncated or misleading-length files, library behaviour at or around the limit, multi-font documents, and cold-versus-warm effects beyond three fresh processes. Corpus coverage is 33 English-language US federal and Office-generated PDFs; it says nothing about CJK or Arabic business forms.
* **Interaction with existing limits (read from source, not tested).** `PdfAppendixBuffer.MaxLength` is 8 MiB ("Call SaveIncremental before Sign") and `PdfAssemblyAppearances.MaxBytes` is 8 MiB and its resource-key whitelist includes `FontFile2`. If an unsubsetted font above about 8 MiB reaches either path, an 8 MiB limit elsewhere may refuse it regardless of FORM-FONT-05's L. The owner and implementer should decide whether those paths can carry a supplied font before a number is adopted.
* Licence for Nanum Gothic is recorded from the repository location, not the font file. Downloads are unversioned branch tips plus two tagged releases and a tagged AOSP tree; identity is the SHA-256.
* The padding ladder is synthetic and single-run. fpdf2 and iText subset being flat shows they skip unused table data; it does not show that a real 500 MiB font would behave the same.

## Files

[w218-font-limit-evidence.py](w218-font-limit-evidence.py) (stages, child modes), [w218-font-limit-itext.py](w218-font-limit-itext.py) and its [harness source](w218-font-limit-itext.cs.txt), [w218-font-limit-padding.py](w218-font-limit-padding.py), [w218-font-limit-merge.py](w218-font-limit-merge.py), and the raw [JSON](w218-font-limit-evidence.json). The harness source is stored as `.cs.txt` so that no build globs it up. Intermediate files are in untracked `tmp/w218-font-limit/` and may be deleted.
