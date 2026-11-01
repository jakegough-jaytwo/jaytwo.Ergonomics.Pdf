# W-218 closure concerns: internal limits, large-font search, forms proportion

**Independent research, 2026-10-31. Executed or read evidence only. Nothing adopted; no governing, adopted, spec, runtime, test or index file was edited; nothing staged or committed.** Raw records: [w218-closure-concerns.json](w218-closure-concerns.json) (includes script SHA-256 values and every input record). Builds on [w218-font-limit-evidence.md](w218-font-limit-evidence.md). Repository HEAD `509af167b4257646b330c61d95a381cfff2cf014`, branch `wip`, dirty working tree.

## Summary

| Concern | Status | One line |
| --- | --- | --- |
| 1. Do the 8 MiB internal limits block a font between 8 and 32 MiB? | **Settled** (executed against the current `src`) | `SaveIncremental`: no. `Sign`: yes, a documented refusal with a documented remedy. `ExtractPages`/`Concatenate`: the filled widget is omitted and reported. Implementation gap, no governing conflict, no owner decision needed. |
| 2. Is "nothing between 24.31 and 32 MiB" real? | **Settled: it was a search gap, and it hides an A4 trigger** | Eligible, openly licensed fonts exist at 27.2 to 49.6 MiB. Source Han Sans (TrueType-outline conversion, OFL) is 31.8 to 32.3 MiB, so a 32 MiB limit splits one family. Every embedder accepted all of them. |
| 3. Is forms disproportionate; what is the smallest honest path? | **Measured; owner approvals marked** | Forms is 51.5% of spec bytes. The A1 exit as recorded needs five items; the "live gaps" are the review's own, not the exit wording. |

## New A4 trigger (needs the owner)

The 32 MiB refusal threshold (ledger A4 option d') refuses real, eligible fonts that every embedder tested accepts. Per the ledger's own rule ("a limit that would refuse a representative real-world font that established tools embed returns under A4"):

> A complete supplied font program of 33,554,432 bytes or more refuses with a resource-limit failure. This refuses Source Han Sans in its TrueType-outline distribution (be5invis/source-han-sans-ttf v2.002.1: `SourceHanSans-Regular.ttf` 33,673,672 bytes, `SourceHanSansSC-Regular.ttf` 33,723,904, `SourceHanSansSC-Bold.ttf` 33,867,188; 30 of the 35 TrueType files in the archive (sizes 33,316,600 to 33,867,188 bytes, from `7z l -slt`) are at or above 33,554,432 bytes and only five lighter weights fall below, for example `SourceHanSansSC-ExtraLight.ttf` 33,333,528) and BabelStone Han 16.0.3 (51,971,284 bytes). PDFium, PyMuPDF, reportlab, fpdf2 and iText embedded each of them in every run.

(The archive-wide count is from the archive listing; only the files named were extracted and classified. See Concern 2.)

Alternatives for the owner: (a) keep 32 MiB and accept that Source Han Sans (a very widely used CJK family) and BabelStone Han are refused; (b) 64 MiB, which accepts everything found (largest 49.56 MiB, 1.29x headroom) and about doubles the worst-case whole-font working set; (c) 128 MiB. The earlier evidence measured whole-font embedding at roughly 5x font bytes in PDFium and 4x in iText on padded fonts; this run saw 209 MiB iText managed allocation for the 32.1 MiB Source Han Sans and 255 MiB for the 49.6 MiB BabelStone Han (full embedding). Which of these is "representative" is the owner's call; the evidence only shows the fonts are real, eligible and embeddable. Under your convergence rule I would call this converged: the charter ("compatibility with representative real-world business PDFs", "predictable resources"), the established tools (no ceiling) and the path of least resistance (raising one constant) all point to 64 MiB. It is still an owner item because AGENTS.md routes resource defaults that change acceptance through A4.

## Concern 1: do the existing 8 MiB limits block a supplied font between 8 and 32 MiB?

### Where each limit lives (read, with file:line)

| Limit | Location | Scope |
| --- | --- | --- |
| Signature appendix buffer 8 MiB | [PdfAppendixBuffer.cs:11](../../../../src/jaytwo.Pdf/Internal/PdfAppendixBuffer.cs#L11), enforced at `:37` and `:53` | Used only by `Sign` ([PdfSigner.cs:125](../../../../src/jaytwo.Pdf/Internal/PdfSigner.cs#L125)) |
| Sign pre-check | [PdfSigner.cs:93-106](../../../../src/jaytwo.Pdf/Internal/PdfSigner.cs#L93) | Sums every dirty stream body length plus the contents gap; refuses above 8 MiB with `PdfSignException` |
| Appearance graph 8 MiB | [PdfAssemblyAppearances.cs:10](../../../../src/jaytwo.Pdf/Internal/PdfAssemblyAppearances.cs#L10); counted at `:150-156` (`dictionary.Body?.Length`, the stored length) and `:194` | One annotation's `/AP` per `Supported()` call (`_bytes` resets at `:48`); used only by [PdfPageExtractor.cs:163](../../../../src/jaytwo.Pdf/Internal/PdfPageExtractor.cs#L163) |
| `FontFile2` allowed in that inspector | `ResourceKeys`, [PdfAssemblyAppearances.cs:23](../../../../src/jaytwo.Pdf/Internal/PdfAssemblyAppearances.cs#L23) | Font programs pass classification but count toward the 8 MiB |
| 32 MiB structural stream cap | [PdfStreamDecoder.cs:11](../../../../src/jaytwo.Pdf/Internal/PdfStreamDecoder.cs#L11); callers [PdfObjectStore.cs:257](../../../../src/jaytwo.Pdf/Internal/PdfObjectStore.cs#L257), [PdfInfoMetadata.cs:310](../../../../src/jaytwo.Pdf/Internal/PdfInfoMetadata.cs#L310) | Object streams and XMP only; no font-stream caller. Not a font limit. |

### Every path a font program can take into saved output

There is no public full-rewrite `Save`. The engine's public writers are `SaveIncremental` and the new-file assembly writer (`ExtractPages`, `Concatenate`), plus `Sign`. No caller-supplied-font API exists yet ([forms.md](../../forms.md) row "Caller-supplied TrueType generation is intended scope but absent from the current engine").

| Path | 8 MiB limit applies? | Unsubsetted 12 to 25 MiB font | Evidence |
| --- | --- | --- | --- |
| `SaveIncremental` ([PdfIncrementalWriter.cs:31-90](../../../../src/jaytwo.Pdf/Internal/PdfIncrementalWriter.cs#L31)) | No. Writes straight to the destination; no `PdfAppendixBuffer`. | Accepted | Executed: succeeded for 0.8 to 25.5 MiB streams |
| `Sign` after dirty font (direct editor) | Yes, via the pre-check and the buffer | **Refused** with `PdfSignException: The signature appendix would exceed 8 MiB. Call SaveIncremental before Sign.` Remedy: `SaveIncremental` first. Documented in [behavior.md:50](../../../behavior.md). | Executed (table below) |
| Workflow signing terminal | Not reached | Accepted: it saves edits to a temporary file first, so "the 8 MiB signature appendix never holds a large edit" ([behavior.md:249](../../../behavior.md)) | Read only |
| `ExtractPages` / `Concatenate` with a filled widget whose `/AP` embeds the font | Yes, per annotation | **The widget is omitted** (`UnsupportedAppearance`, counted in the result) when its stored appearance graph exceeds 8 MiB; the call still succeeds. Documented in [behavior.md:168](../../../behavior.md): "local-limit failures omit the entire annotation". | Executed (table below) |
| Page flatten | Not a font path in `src` (no `FontFile` reference in [PdfFlattener.cs](../../../../src/jaytwo.Pdf/Internal/PdfFlattener.cs)) | Not applicable today | Read |

### Executed confirmation

Scratch program in `tmp/w218-closure/probe` (source copy: [w218-closure-appendix-probe.cs.txt](w218-closure-appendix-probe.cs.txt), project [w218-closure-appendix-probe.csproj.txt](w218-closure-appendix-probe.csproj.txt)), built against the current `src/jaytwo.Pdf` project reference with `dotnet build -c Release -o tmp/w218-closure/probe-bin` (SDK 10.0.402, runtime 10.0.13, net10.0), run as `dotnet tmp/w218-closure/probe-bin/probe.dll tmp/w218-closure/probe-run2.json <fonts>`. Each font is stored as a `FontFile2` stream inside a widget `/AP`, raw and Flate-compressed; a dirty stream of the same bytes is attached (an attachment stands in, because no public font API exists) to test `SaveIncremental` and `Sign` with a stub provider. Two synthetic random streams bracket 8 MiB.

| Stored stream bytes | Case | ExtractPages | SaveIncremental | Sign |
| --- | --- | --- | --- | --- |
| 825,628 | Noto Sans raw | kept | ok | ok |
| 4,529,044 | Droid Sans Fallback raw | kept | ok | ok |
| 7,472,912 | Malgun Gothic Flate (12.84 MiB font) | kept | ok | ok |
| 8,384,512 | random, 4,096 below 8 MiB | kept | ok | ok |
| 8,388,609 | random, 1 above 8 MiB | **widget omitted** | ok | **refused** |
| 11,546,482 | SimSun-ExtB Flate | **widget omitted** | ok | **refused** |
| 12,798,102 | LXGW WenKai Flate | **widget omitted** | ok | **refused** |
| 13,459,196 | Malgun Gothic raw | **widget omitted** | ok | **refused** |
| 21,894,056 | SimSun-ExtB raw | **widget omitted** | ok | **refused** |
| 25,486,932 | LXGW WenKai raw | **widget omitted** | ok | **refused** |

(Full records in the JSON: `concern1_probe`. Noto Sans and Droid Flate rows, omitted here, behaved like their raw rows.)

So the 8 MiB boundary is on **stored (possibly compressed) bytes**. A Flate-compressed unsubsetted font passes until about 8 MiB stored. Subsetted output (the normal case: 6 to 160 KB in every embedder tested) is far below it. The limits matter only for unsubsetted embedding, which FORM-FONT-07 forces for fonts whose `fsType` forbids subsetting.

One run was discarded: the first probe run omitted the AcroForm `/DR` font entry, so small cases hit an unrelated assembly refusal (`a default appearance or its font resources`). It was fixed and the whole run repeated; only the second run is recorded. That unrelated refusal is existing behavior for default appearances whose font is not in `/DR`.

### Classification under AGENTS.md

* **Implementation gap, not a governing conflict.** No charter, `supported.md` or adopted specification states that a supplied font up to the limit must survive `Sign` or assembly; FORM-FONT-05 is a draft input-size gate. `behavior.md` records current behavior and already describes both limits and the remedy. Charter: "Material limitations must be documented" (met) and "Routine compatibility decisions remain within engineering discretion".
* **No owner decision needed under your convergence rule.** The charter, the existing documented limits and the path of least resistance agree: leave the 8 MiB limits, document that they apply to saved output of unsubsetted fonts. Raising them is the alternative and would be an architectural change to unrelated operations, which the ledger already says the font principle does not silently approve ([forms.md FORM-FONT-05](../../forms.md)).
* Suggested drafting step (engineering, not applied; `forms.md` is being edited by another agent): add one sentence to FORM-FONT-05 stating that passing the font-size gate does not exempt saved output from the library's other documented limits (8 MiB signature appendix, 8 MiB per-annotation appearance graph), with the behaviors above. Note that if the owner raises L to 64 MiB under the A4 item above, nothing in this concern changes.

Not run: the repository test suite, `make` targets, Docker, any change to `src`/`test`. The probe is not a regression test and was not added to any test project.

## Concern 2: eligible fonts between 16 and 48 MiB

### Method

Listings first (no download): GitHub tree API for `google/fonts` (23,848 entries, not truncated, 3,923 font files), `notofonts/notofonts.github.io` (14,985, not truncated) and `notofonts/noto-cjk` (303); release asset listings for Sarasa Gothic, source-han-sans-ttf, Adobe Source Han Sans, four LXGW repositories, FanWunMing, Iansui and others. Unauthenticated `curl` against `api.github.com`, `raw.githubusercontent.com`, `github.com` releases and two direct sites; no client was spoofed. Downloads went into new directories under `tmp/w218-closure/` (`dl`, `fonts`, `fonts-lxgw`, `fonts-sarasa`, `fonts-shs`, `fonts-bsh`, `fonts-run`), were never executed, and all Python ran as `python -I` with paths as arguments. Archives were listed with 7-Zip 25.01 before extracting only the named members. Classification reuses the earlier script's header/`fontTools` logic through [w218-closure-font-survey.py](w218-closure-font-survey.py) (fontTools 4.68.0, Python 3.13.1). No downloaded file is committed or redistributed.

### Eligible fonts found (static `glyf`/`loca`, single face, not variable, not CFF, not a collection)

Bytes exact; MiB = bytes / 1,048,576. Complete SHA-256 is in the JSON. "Gate" is the owner's L = 33,554,432.

| Font (version) | Bytes (MiB) | SHA-256 (prefix) | Licence (from font name table unless noted) | Scripts (fixed samples) | Gate |
| --- | --- | --- | --- | --- | --- |
| BabelStone Han 16.0.3 | 51,971,284 (49.56) | d8bb747b3fdc | Arphic Public License (name table ID 13) | Han | **refused** |
| Source Han Sans SC Bold 2.002 (TrueType conversion) | 33,867,188 (32.30) | 25c2d2600f4b | OFL 1.1 | Latin, Greek, Cyrillic, Hangul, kana, Han | **refused** |
| Source Han Sans SC Regular 2.002 | 33,723,904 (32.16) | 53edb68d5f77 | OFL 1.1 | same | **refused** |
| Source Han Sans (generic) Regular 2.002 | 33,673,672 (32.11) | 16d712e241dc | OFL 1.1 | same | **refused** |
| Source Han Sans SC ExtraLight 2.002 | 33,333,528 (31.79) | 876de1e6bd1e | OFL 1.1 | same | accepted (by 220,904 bytes) |
| LXGW WenKai Mono Light 1.530 | 28,645,564 (27.32) | b3144f534329 | OFL 1.1 | Han et al. | accepted |
| LXGW WenKai Light 1.530 | 28,625,516 (27.30) | 9a50e3f0f51f | OFL 1.1 | Han et al. | accepted |
| Cactus Classical Serif Regular 1.005 | 28,502,932 (27.18) | ecbd58961db9 | OFL 1.1 | Han | accepted |
| Sarasa Gothic SC Light Italic 1.0.43 (hinted) | 24,950,308 (23.79) | 80b6046d87bc | OFL (licence file in archive; name table empty) | Latin, Han, kana | accepted |
| Sarasa Gothic SC Regular 1.0.43 (hinted) | 24,047,784 (22.93) | de65a1791c1d | same | same | accepted |
| Bpmf Zihi Kai Std Regular 1.610 | 17,497,872 (16.69) | 043aaa657f2b | OFL 1.1 | Han, bopomofo | accepted |
| Shippori Mincho B1 Regular 3.110 | 15,230,032 (14.52) | 89403e4b2a9c | OFL 1.1 | Japanese | accepted |
| LXGW WenKai TC Regular 1.330 | 13,110,528 (12.50) | 4fcc5aec11cb | OFL 1.1 | Han | accepted |

All 13 have `fsType` 0 (installable embedding) and composite depth at most 1 (Source Han, Cactus and BabelStone have no composites at all; 65,535 and 65,534 glyphs). The three Source Han Sans files were extracted with `7z e` from `source-han-sans-ttf-2.002.1.7z` (sha256 `b3057c0d4df53bbd96d292e69b9393efbe9b3f5b7a7354f2ec00b91535ed7636`). Sources: URLs and archive hashes are in `tmp/w218-closure/provenance.json` and are reproduced in the JSON per font (`provenance`). Cactus, Bpmf Zihi Kai, Shippori and LXGW TC came from `google/fonts` `main` (unversioned branch tip; the hash is the identity). BabelStone Han came from `https://www.babelstone.co.uk/Fonts/Download/BabelStoneHan.zip` (zip sha256 `6a928e436ee52d1857c32b5e4d5e71f4f0a9d13ccf660fc4f1ff08df930e12dc`, unversioned URL). I did not read the Arphic or Source Han licence texts beyond the name-table entry; "legally usable" here means the font's own licence field names a free licence, not a legal review. BabelStone Han's licence is Arphic's, which is not OFL.

Ineligible but notable (not tested): Noto Serif CJK variable TTFs are 57 MiB; `google/fonts` has Chiron Sung HK variable at 48.3 and 49.5 MiB, Noto Serif SC `[wght]` 23.96 MiB, Noto Color Emoji 24.16 MiB. The official Noto Sans/Serif CJK and Adobe Source Han Sans are CFF/OTC. All are refused by FORM-FONT-03 for format, not size. HanaMin (CJKVI HanaMinAFDKO release 8.030, `.otf` 31.6 to 35.0 MiB) is OTF, expected CFF, not downloaded or classified. Unifont 18.0.01 is distributed as `.otf` only, not downloaded.

### Embedder results on the new fonts (executed)

Commands (from the repository root; Python from `tmp/w218-font-limit/venv`):

```text
python -I docs/spec/drafting/research/w218-closure-font-survey.py --evidence docs/spec/drafting/research/w218-font-limit-evidence.py --out tmp/w218-closure/survey-run1.json --provenance tmp/w218-closure/provenance.json <13 font paths>
python -I docs/spec/drafting/research/w218-font-limit-evidence.py --repo . --fonts tmp/w218-closure/fonts-run --sysfonts tmp/w218-closure/empty --work tmp/w218-closure/work-run --out tmp/w218-closure/embed-run1.json --runs 2 --skip facts,sysdist,corpus
python -I docs/spec/drafting/research/w218-font-limit-itext.py --evidence <evidence.py> --harness <itext-harness.exe, Windows path> --fonts tmp/w218-closure/fonts-run --sysfonts tmp/w218-closure/empty --work tmp/w218-closure/work-itext --out tmp/w218-closure/itext-run1.json --runs 2
```

Seven fonts were run (Cactus, LXGW Light, Sarasa Light Italic, the three Source Han files above and ExtraLight, BabelStone Han), 2 fresh-process runs each, tools and versions as in the earlier evidence (PyMuPDF 2.0/MuPDF 1.28.5, fontTools 4.68.0, reportlab 5.1.0, fpdf2 2.9.0, pypdfium2 5.14.0, iText 9.8.0, qpdf 12.3.2, pdffonts 26.08.0). The first iText attempt failed on a Git-bash-style path; I re-ran with a Windows path and only the second attempt is recorded.

* **Every tool accepted every font in every run: 0 failures in 70 runs of the five tools (including the fontTools subsetter, which makes no PDF) and 28 iText runs.** `qpdf --check` and `pdffonts` exit 0 on all run-0 PDFs (structural checks only; no rendering ran).
* The three subsetting PDF writers produced 35 to 159 KB outputs for every font, including BabelStone Han (reportlab 38,201; fpdf2 35,639; PyMuPDF 138,739 bytes). iText subset: 125 to 265 KB.
* Whole-font embedding scales with the file: PDFium 20.8 MB output and 273 MiB working-set delta for BabelStone Han (1.4 s); 17.5 MB and 181 MiB for Source Han Sans Regular. iText full: 21.1 MB and 255 MiB managed allocation (BabelStone Han); 17.5 MB and 209 MiB (Source Han Sans Regular).
* fontTools subsetting: 9.0 s and 300 MiB for Source Han Sans, 22 s and 520 MiB for BabelStone Han (a diagnostic subsetter, as before).

Result for ledger A4: **a 32 MiB gate refuses fonts that established embedders accept, so the trigger above stands.** Not shown: shaping, glyph correctness, visual rendering, hostile structure, the .NET target library, or business-PDF demand for these specific fonts.

### Thoroughness and gaps

Listed completely (no truncation): `google/fonts`, `notofonts.github.io`, `noto-cjk`. Largest static font in `google/fonts` is Cactus Classical Serif (27.18 MiB); its only larger entries are variable or colour fonts. Not checked: commercial fonts (Arial Unicode MS, Windows CJK families beyond the two already measured) which cannot be acquired legally here; Fontsource and npm packages (WOFF2 only); Debian/Fedora font packages; Hanazono and HanaMin (OSDN host does not resolve, GitHub release is OTF); Plangothic (repo not found at the guessed URL); GNU Unifont TTF builds (only OTF found); whether the hinted Sarasa variants for Hong Kong, Japanese or Korean exceed the SC size (all ~24 MiB by archive size, not extracted). No download was refused with 403. A site that returned the zip only after the first request (babelstone.co.uk) was fetched once.

## Concern 3: is forms disproportionate; smallest honest path

### Measured (mechanical; from [w218-closure-measure.py](w218-closure-measure.py), JSON `concern3_measure`)

Command: `python -I docs/spec/drafting/research/w218-closure-measure.py --repo . --out tmp/w218-closure/measure-run1.json`.

* **Spec bytes:** forms.md 285,504 of 554,366 total spec bytes: 51.5% (52.3% excluding the README). Forms has 1,216 lines and 117 occurrences of "unexecuted". Next largest: workflows 59,848, opening-and-saving 43,278, pages 40,002, metadata 34,649, shared 24,933, inspection 24,282, security 15,503; attachments 6,199, signing 5,959, images 5,815.
* **Drafting aids** (`docs/spec/drafting/*.md`): 10 files; `history.md` 128,193, `reconciliation.md` 73,937, `form-adoption-review.md` 55,874 bytes. Research directory: 28 research files plus an appearance subdirectory.
* **Git history:** only 11 of 268 commits mention W-218; nearly all W-218 spec work is uncommitted (all `docs/spec/*.md` subject files except `readme.md` and two deleted phase files appear as untracked or modified). Commit counts per subject therefore cannot be measured; I report sizes instead.

### Per-subject table

Open gaps are the phrase count of the "Remaining limits, refusal and failure/cancellation gaps" column of the completeness review (comma/semicolon-separated phrases; a coarse proxy, not a count of decisions). The split of owner versus engineering is my reading of that cell text and each README status cell. **Every owner item below needs owner approval under AGENTS.md "Approval" before it is applied; nothing here selects scope.**

| Subject | Spec bytes | Gap phrases | Needs owner decision | Engineering drafting | Estimated next step |
| --- | --- | --- | --- | --- | --- |
| Signing | 5,959 | 4 | "Signer consistency and token-correlation proposals await owner review" (README) | Provider/algorithm/field/seed subset, revision staging, reservations, cancellation | Short: write the revision and failure profile; owner reviews proposals |
| Images | 5,815 | 5 | Format, transparency and profile subset and coordinate candidates "await owner review" (scope-shaping) | Fit/clipping, decode limits, lifetime, failure | Short: owner picks the subset; then draft |
| Attachments | 6,199 | 5 | Direct-operation identity and ambiguous-mutation proposals "await review" | Names, safe paths, decoded-limit defaults, atomicity | Short: owner review; then draft |
| Security | 15,503 | 5 | Whether read/export is bit-5 extraction (policy question per the forms review) | Schemes, per-operation checks, malformed grants, failure | Medium: authorization matrix |
| Inspection | 24,282 | 5 | None identified | Indexing, coordinates, malformed-version edges, identity type, limits | Medium: draft remaining edges |
| Shared | 24,933 | 4 | A universal cap or rollback policy would be an A4 item if it narrows acceptance | Cancellation latency, retry, restoration generality | Medium: after other subjects settle vocabulary |
| Metadata | 34,649 | 3 | Measured limit values that narrow acceptance (A4) | XML/RDF shapes, reserved keys, public representation, cancellation | Medium |
| Pages | 40,002 | 7 | "Remaining assembly-loss profile proposed for adoption review"; explicit preservation mode | Rotation, empty selections, encrypted/signed output, streaming failures | Medium to long |
| Open/save | 43,278 | 6 | Adoption of Q-58 to Q-64 (adoption of a specification needs owner approval) | Diagnostics, post-disposal lifecycle, repeated saves, revisions | Medium |
| Workflows | 59,848 | 9 | Adoption of engineering-supported proposals | Exact API repetition, special terminals, identity, cleanup | Long; depends on the engine subjects and forms |
| Forms | 285,504 | 6 | The A4 font-limit trigger above; P-CHOICE-INH refusal; any list or fixed-TI narrowing; 8 MiB/XMP boundary disposition | Rich blank, button/glyph proof, list assessment, remaining storage and authorization wording, target oracles | See below: freeze |

The gap-phrase counts (3 to 9) are nearly uniform, so the measured disproportion is in text volume and evidence rather than in listed gaps: forms is 46 to 49 times the size of the three smallest subjects.

### What the forms A1 exit strictly requires

Exact recorded exit (form-adoption-review.md, ledger A1): one identified forms revision containing (1) the R1 profile consolidated with R1 decisions 1 to 5 and superseded sentences removed; (2) R2's requested-validation oracle; (3) R3's public API appendix; (4) R4's finite shape, failure and resource appendix; (5) any resource default measured or brought under A4. Pre-adoption evidence is "reviewable wording plus independent research and arithmetic"; target-library checks are post-adoption.

* The review states items 2 to 4 exist in the identified revision and item 5 was met by selecting 32 MiB under A4. Item 1's "superseded sentences removed" is unverified here: `forms.md` still contains superseded figures (for example "0.125" 4 times, "24-point"/"24 pt" 8 times, "14-point" once, "Q-109" 3 times), which may be labelled as superseded history; I did not audit each occurrence.
* **Not in the recorded exit wording** (the review's own "live gaps" table lists them, but the ledger's exit text does not): rich blank grammar and XFA 2.x acquisition; button/glyph path proof; real list boxes; further reader experiments; Acrobat checks; paired-Opt interoperability beyond the 30 fixtures; flatten bit conjunction.
* **Now exposed by this research:** the A4 trigger above. Per the exit text ("brought under A4") it is the one item that touches the exit; it must be dispositioned (raise L or accept the refusal) or the exit's item 5 is no longer settled.
* Because the exit text and the review's "exit not reached" statement differ, whether A1's exit is met on the recorded wording is **an owner determination** (AGENTS.md: weakened acceptance requirements and changes to qualification criteria need owner approval). I do not claim A1 progress.

### Proposed freeze rule for forms (requires owner approval)

Proposal only: after the A4 font-limit trigger is dispositioned, freeze new forms research and drafting. Admit further forms work only when (a) it is needed to answer an owner-raised A4 question, (b) it corrects a contradiction inside the identified revision, or (c) it is a post-adoption acceptance task. Items outside the exit wording (rich blank, button proofs, lists, additional reader runs) become "recorded open risks" in the forms document, each stating the conservative outcome (refuse or defer), rather than blockers. Consistent with the charter ("do not introduce substantial complexity solely to accommodate speculative or uncommon edge cases"; "routine compatibility decisions remain within engineering discretion"). The owner decides whether to adopt this rule.

### Proposed order for the remaining subjects (reasons)

1. Forms: dispositions only (A4 limit, then the exit determination). Reason: blocks the A1 exit item and cascades to workflows.
2. Signing, attachments, images: each is under 6.2 KB and waits on a single owner review. Reason: cheapest to close; removes three subjects from the list.
3. Security, inspection, metadata: engineering drafting with one or two owner A4 items each; security's authorization matrix feeds forms' flatten/read-export policy.
4. Open/save, shared, pages: larger and interdependent (shared vocabulary, lifetime, assembly loss).
5. Workflows last: it composes the others and carries the most gap phrases (9).

This ordering is a proposal for the owner; it does not select scope.

## Material limitations

* Not target evidence for fonts: nothing here ran the jaytwo library on a font, only the existing 8 MiB code paths with synthetic and real font bytes.
* The probe stands an attachment in for a dirty font stream in the `Sign`/`SaveIncremental` rows; the assembly row used a real widget appearance.
* Gap-phrase counts and the owner/engineering split are mechanical or my reading of the cell text, not an audit of every decision.
* Licence statements come from font name tables and repository metadata, not legal review. Downloads from unversioned URLs are identified by SHA-256.
* The JSON is large (about 160 KB) because it carries all inputs verbatim.
* The scripts were converted to CRLF after the runs. The run records embedded in the JSON (`concern2_survey`, `concern3_measure`) therefore carry the pre-conversion (LF) SHA-256 of their scripts; the top-level `script_sha256` is the current CRLF hash. Content is otherwise identical.

## Owner-decision list

New, awaiting:

1. **A4 font limit** (reopened by Concern 2): keep 32 MiB and accept refusing Source Han Sans (TrueType distribution) and BabelStone Han, or raise L (64 MiB accepts everything found; 128 MiB for wider margin). Exact wording above.
2. **A1 exit determination:** whether the recorded exit wording is met once item 1 above is dispositioned, or whether the review's extra "live gaps" are binding.
3. **Forms freeze rule** (proposal above) and the **order for remaining subjects**.
4. Subject items already marked "await owner review" (signing, images, attachments, pages assembly-loss profile, open/save Q-58 to Q-64) are unchanged; this research adds no new ones.

Not needing the owner (engineering, converged): the 8 MiB limits stay and are documented as applying to saved output (Concern 1).

## Files created

All under `docs/spec/drafting/research/` unless noted; none is linked from any index.

* `w218-closure-concerns.md`, `w218-closure-concerns.json`
* `w218-closure-font-survey.py`, `w218-closure-measure.py`, `w218-closure-merge.py`
* `w218-closure-appendix-probe.cs.txt`, `w218-closure-appendix-probe.csproj.txt`
* Untracked scratch under `tmp/w218-closure/` (downloads, extracted fonts, probe build, run records), safe to delete.

Script SHA-256 values are in the JSON (`script_sha256`).
