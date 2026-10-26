# W-189 findings: refusal and recovery impact (Gate 1 measurement)

Research only (D-66). No runtime, test, protected-file or queue change. The planning editor is the single agent executing W-189; no independent reviewer is assigned and no independence is claimed. Candidate rules below are counterfactual classifications of existing files, not implemented behavior.

## Pinned inputs

| Item | Value |
| --- | --- |
| Revision | `98799d48e4e23f7dc0545bc217cc8b0bfc9ab729` (`wip`); tracked PDFs unchanged since W-188 (`8a6e496`): all 51 hashes in `fixture-manifest.csv` re-matched (`hashMatchesManifest` in `features.json`) |
| Corpus | 51 tracked PDFs (W-188 manifest). Not the 510 untracked build copies. |
| Tools | pypdf 6.21.1 on Python 3.13.17 (`.tools/python`); qpdf 12.3.2; .NET SDK 10.0.402, net10.0 probe |
| Dirty paths | unrelated W-213/W-155 files, untouched |

## Reproduction

```
# from the repository root
.tools/python/Scripts/python.exe -I agents/reference/simplification/W-189/classify.py . agents/reference/simplification/W-188/fixture-manifest.csv > agents/reference/simplification/W-189/features.json
dotnet build probe && dotnet probe.dll <repo-root> agents/reference/simplification/W-188/fixture-manifest.csv <outdir> > combine-probe.json   # probe/Program.cs; project file probe/probe.csproj.txt references src/jaytwo.Ergonomics.Pdf
classify.py over <outdir>/*.pdf -> output-features.json
cd agents/reference/simplification/W-189 && python -I build_matrix.py        # writes matrix.csv, summary.json
.tools/python/Scripts/python.exe -I agents/reference/simplification/W-189/crosscheck_qpdf.py   # independent qpdf comparison
```

The probe calls only the public `PdfWorkflow.Combine` API with `PdfCombineInput.FromFile`, once per fixture for three operations: `combine-all` (all pages, default options), `combine-page1` (page 1 only), and `combine-all-consent` (all pages, `AllowUnencryptedOutput = true`). Credential: the fixture's recorded user password where one exists (7 encrypted fixtures with a known password: libreoffice-memo-rc4, qpdf-encrypted, five `pypdf-unsigned-p-r*`); otherwise none. Owner-password runs were not made. Probe project lives in the scratchpad during the run; its source is archived in `probe/`.

## Recount of the reviews (measured)

| Review claim | Measured here | Reading |
| --- | --- | --- |
| 41 fixtures: 18 tagged, 16 outlines, 6 Reader-extended, 3 XFA, 4 signed | `corpus/manifest.json` has exactly 41 entries (33 in `corpus/` plus 8 in `Fixtures/`). Over those 41: tagged 18, outlines 16, usage-rights 6, XFA 3, signed 4 (certified 2 overlaps signed). The remaining 10 tracked PDFs (5 `defects/`, 4 focused-assembly-links, 1 focused-assembly-invoice) add none of these, so the same counts hold over all 51. | R2A's "41" is the corpus manifest, not the tracked set. Reproduced. The counts are overlapping feature counts, not refusals. |
| R2B raw scan of 36 files for rare filters | 36 of the 51 tracked PDFs contain at least one `*Decode` filter name; 34 of them use FlateDecode, 8 DCT, 2 CCITTFax, and 1 each RunLength, JBIG2 and JPX; no LZW, ASCIIHex or ASCII85. 15 contain none. | The numeric match is suggestive, not proof of R2B's method (R2B unavailable, secondhand). 11 files contain a non-Flate filter. The public-key encrypted file is among the 15 and its filters are unmeasured, so 36 is a lower bound. |

Do not add the two scans: one is a feature census over 41 manifest entries, the other a filter census over 51 tracked files.

## Current baseline (measured, public API)

Denominator: 51 tracked PDFs; `combine-page1` and `combine-all` gave the same result for all 51.

| Operation | Accepted | Refused |
| --- | --- | --- |
| combine-all, default options | 30 | 21 |
| combine-page1 | 30 | 21 |
| combine-all, `AllowUnencryptedOutput` | 31 | 20 |

Refusal reasons for combine-all default (21): encrypted input without consent 7; field actions or scripts 6; `/OCProperties` 2; nonempty `/AF` 2; XFA-only or dynamic form 1; unsupported encryption (public key) 1; default appearance or fonts unpreservable 1; unresolved inspected dictionary 1. With consent, 6 of the 7 remain refused because the user password's permissions do not allow assembly (`PdfEncryptionRestrictedException`); consent converts 1 of the 7.

Accepted runs report omissions for 8 files (4 signature widgets, 4 with unsupported links); that is the only reported loss.

### Successful omissions that are not reported (measured on the 31 accepted consent runs)

Comparing feature presence in each saved output against its input: tags dropped in 8 outputs, outlines 7, page labels 4, named destinations 1, XMP 13, AcroForm 3, signature fields 4, signed 2 and certified 1 (signatures cannot survive a new file), usage rights 2, XFA 2, JavaScript 2, embedded files 1. These are silent in `PdfAssemblyResult`, which counts only annotation omissions. This is the successful-omission class, distinct from preservation. Feature presence is a whole-graph indicator, not a semantic check that surviving structures are correct.

## Counterfactual refusal impact (derived by read-only classification)

Candidate rule: refuse a whole input if it has the category, applied to the 30 inputs that combine-all accepts today. A file counted here is newly refused. Whole-graph presence is an over-estimate of selected-page reach (limit L1); the page-1 baseline equalling all-pages shows nothing about selected-page candidate behavior.

| Category (rule) | Newly refused of 30 | Files |
| --- | --- | --- |
| tagged | 8 | chrome-invoice, chrome-ledger-2000p, edge-handbook, excel-invoice, irs-fw9, powerpoint-photos, va-21-4138, word-report |
| signed, certified or timestamp | 2 | gpo-bills, pyhanko-signed |
| usage rights | 2 | irs-fw9, va-21-4138 |
| XFA | 2 | irs-fw9, va-21-4138 |
| document-level or field active content | 3 | frb-beige-book, irs-fw9, va-21-4138 |
| attachments or AF | 1 | qpdf-attachment |
| optional content | 0 | none accepted today |
| field lock | 1 | gpo-bills |
| outlines, page labels or named destinations (only if loss became a refusal) | 11 | edge-handbook, frb-beige-book, gpo-bills, irs-f5500-2005, libreoffice-calc-ledger, libreoffice-form-pypdf-inherited, libreoffice-form, naca-rm-19930086000, nbs-circular-1, powerpoint-photos, word-report |

Intersections among the 30 (non-zero only): tagged and usage rights 2; tagged and XFA 2; tagged and active content 2; usage rights and XFA 2; usage rights and active content 2; XFA and active content 2 (irs-fw9 and va-21-4138 carry all five); tagged and outlines/labels/destinations 3; signed and field lock 1 (gpo-bills); signed and outlines/labels 1; active content and outlines/labels 1; field lock and outlines/labels 1.

Union of all nine categories: 18 of the 30 currently accepted. Union excluding outlines/labels/destinations (the first eight, the "core" refusal set): 12 of 30. Never sum the column: the eight core rows add to 19 category hits across 12 files.

### Accepted after named preparation, and what stays blocked

Preparations modeled: remove usage rights; abandon XFA; unsigned derivative (drops signatures and locks). Of the 12 core newly refused files, only 2 (gpo-bills, pyhanko-signed) become acceptable with those preparations. The other 10 stay blocked, because 8 are tagged and no tag preparation is modeled, frb-beige-book has document-level JavaScript and qpdf-attachment has an embedded file, which no preparation addresses. Scenario B adds an explicitly authorized untagged derivative: 8 of 12 are then accepted; 4 stay blocked (frb-beige-book, irs-fw9 and va-21-4138 on active content; qpdf-attachment on its embedded file). The scenario B number is a bound on what a derivative could recover, not an implementation estimate.

Under the default (non-consent) baseline there are also 21 existing refusals; the 7 encrypted ones are recoverable by consent only when the restriction allows, as above.

## Operation coverage

Measured: `combine-all`, `combine-page1`, `combine-all-consent` (above). Not measured here: fill, flatten, metadata, signing, rotation, attachments, owner-password runs, repeated occurrences of one file, multi-input combines. The existing corpus manifest records `open`, `attachments`, `revise` and `fill` outcomes for the 41 entries; those were not re-run, and a refusal count for them would need a classifier of what each candidate would newly refuse, which is W-190/W-194/W-207 territory. Page extraction is represented here by `combine-page1`, a one-input Combine with a selection; the engine's extraction API was not probed separately.

## Coverage gaps (each blocks the dependent adoption claim)

| Gap | Why it blocks |
| --- | --- |
| Tagged forms | 10 tracked files are tagged AcroForms (7 without XFA), but 8 of them are already refused today by script, rights or other rules, so a tag rule's marginal effect on them is zero and bounded form-tag preservation cannot be observed through Combine. Only irs-fw9 and va-21-4138 (both hybrid XFA with scripts) are accepted today. Needs an ordinary unscripted tagged form fixture (W-191). |
| Scripted public forms (field-level JavaScript on ordinary forms) | 6 current script refusals are measured, but no fixture separates benign calculation scripts from active behavior. |
| Hybrid XFA plus rights in a signed file | irs-fw9 and va-21-4138 are unsigned; signed hybrid is not in the tracked set. |
| Certified and locked inputs | 2 certified, 1 field-locked tracked; no certification P1/P2/P3 spread, no document timestamp, no `/Lock` on an approval signature other than gpo-bills. |
| Malformed XMP and malformed destinations | None in the 51; malformed cases are adversarial and kept apart. |
| Repeated widgets and replies on repeated pages | Reply annotations (`/IRT`): 0 detected; repeated occurrence not exercised. |
| Relative URIs | 9 files carry URI actions; 3 of them carry at least one URI classified relative-or-other (which three was not recorded in the output). Base-URI behavior is not classified. |
| Label restarts | 7 files have `/PageLabels`; restarts, prefixes and nested ranges not classified. |
| Required-stream filters | No LZW, ASCIIHex or ASCII85 stream in any tracked fixture; RunLength 1, JBIG2 1, JPX 1, CCITTFax 2. Whether any is in a required (non-image) stream is not classified. |
| Sparse or very large files | `chrome-ledger-2000p.pdf` (2 000 pages) is the largest tracked case; no 10 000+ page or gigabyte sparse file. |
| Public-key encrypted file | `chrome-invoice-pyhanko-pubkey.pdf` unreadable by pypdf, so features and filters are unmeasured for it (1 of 51). |
| Owner-password roles | Not run; only user passwords were applied. |
| Customer demand | Fixture coverage is not demand: the corpus was chosen for variety, not weighted by customer use. |
| Rights of 10 fixtures | Per-file terms exist only for the 41 manifest entries; the five defect fixtures, one focused-invoice and four focused-link files lack a recorded licence in this repository. |

## Method limits

- L1: classification is whole-object-graph, not reachability from selected pages, so it can over-report features a selected page would not reach. The one unreadable file contributes no features.
- L2: feature definitions are mine and differ from the repo probe where noted: `tagged` is `/StructTreeRoot` or `/MarkInfo /Marked true`; `outlines` requires `/First`; signed means a `/FT /Sig` field with `/V`.
- L3: `field-actions` and `javascript` were found by object scan; whether an action is benign was not judged.
- L4: qpdf cross-check compared eight key-presence tests over all 51 files: seven keys agree for every file. One definitional difference: `uscis-i9-2025.pdf` has an `/Outlines` root with no `/First`, which qpdf counts and the classifier (and the manifest's 16) does not. The other keys agreed everywhere.
- L5: the probe measures accept or refuse and feature presence in the output. No renderer or semantic equivalence check was run, and no output was validated by an independent PDF viewer. Statements about what a recipient sees are not made.
- L6: one agent produced, ran and read the probe and classifier; the qpdf check is independent of pypdf only for key presence.

## Measured versus inferred

Measured: the recount, all current results, reported omissions, the silent feature drops on accepted outputs, category counts, intersections and unions, preparation counts under the stated assumptions. Inferred: that the candidate refusals would cost these counts under selected-page reach; that preparations remove exactly the categories listed; the R2B method; customer impact. Unavailable: the four review attachments (W-188 gap, still open).

## Candidate result and recovery burden (per category, no recommendation)

| Category | Candidate | Recovery path | Credential | Loss if recovered | Remaining refusal |
| --- | --- | --- | --- | --- | --- |
| tagged | refuse, or explicit untagged derivative | derivative option (not modeled in runtime) | none | accessibility tags | none if derivative exists |
| signed/certified | refuse | unsigned derivative | none; certified P1 blocks writes anyway | signatures | locked certified inputs |
| usage rights | refuse, or remove | `remove-rights` preparation | none | Reader extensions | none |
| XFA | refuse, or abandon | `abandon-xfa` preparation | none | dynamic form | XFA-only |
| active content | refuse | none modeled | none | n/a | all |
| attachments/AF | refuse | none modeled | none | n/a | all |
| field lock | follow signed | unsigned derivative | none | locks | none |

This table is a Gate 1 input. Wording, API shape and effort belong to the downstream items (W-190, W-191, W-194, W-197, W-207) and Gate 2.
