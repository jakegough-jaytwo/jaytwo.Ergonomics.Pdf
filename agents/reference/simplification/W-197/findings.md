# W-197: assembly refusal and explicit unsigned derivatives

Research completed 2026-10-26 at product revision `ae3d586a1ecd178d766f21cea5049f9e94a709bb`. One planning editor did all of it. There was no independent human or fresh-context review, and W-188's four missing review attachments are still unavailable, so R2A's and R2B's exact certification and lock boundaries could not be compared. This is a proposal backed by bounded evidence. It is not product adoption, a defect fix, or release qualification. Exact wording is in [proposal.md](proposal.md); none of it is approved.

## Recommendation

Replace silent loss with one fixed, bounded preflight rule set (R197) that runs before any output. Its scope depends on the category:

- **Whole input** for anything whose meaning covers the whole document: signatures, certification, usage rights, document scripts, the open action, catalog additional actions, document attachments, `/AF`, optional content, XFA, and the field tree. A signature on a discarded page still certifies or approves the whole source, so leaving that page out does not make the loss intentional.
- **Selected pages** for page-local behavior: page `/AA`, actions on retained annotations, file-attachment and media annotations, and optional-content references from retained pages. Leaving a page out by selection is an intentional exclusion (charter, "Preserve meaningful behavior or refuse").

There should be a single explicit consent, illustratively `PdfAssemblyOptions.AllowUnsignedDerivative`. It admits inputs whose only refusal reasons are approval signatures, document timestamps, or usage rights. The derivative drops the signed fields, their widgets, and `/UR3`, and it reports those losses. The consent never admits a certified input (any `P`, including an unresolved one), a signed field with a `/Lock` or FieldMDP reference, a malformed signature value, or any other refused category. It does not replace `AllowUnencryptedOutput` or the per-input permission checks. Content-painted marks stay outside inspection and removal.

Active content, attachments, and layers get no consent option. There is no `StripUnsupported`, no `BestEffort`, and no per-subtype flag. For document attachments, the supported recovery is two explicit steps that exist today: remove the attachments in an incremental revision, then assemble that revision (measured, E197-6).

## Reproduction

```powershell
.\agents\reference\simplification\W-197\run.ps1          # add -NoRestore to reuse a restore
```

The script uses the pinned Python and qpdf from `.tools/test.runsettings`, plus a throwaway net10.0 console in the ignored `out/W-197/probe`. The console calls only the public APIs of `jaytwo.Pdf` and `jaytwo.Ergonomics.Pdf`. It changes no product code and adds no test project, friend assembly, or runtime dependency.

The run has four steps:

1. `generate.py` writes 32 original MIT-licensed two-page fixtures. `fixture-manifest.json` records their hashes.
2. The probe creates one real engine-signed CMS input and one attachment-removed incremental revision, then records 109 outcomes.
3. `classify.py` applies R197 read-only to the fixtures and to the 51 tracked W-188 corpus files.
4. `validate_probes.py` asserts the outcomes and inspects every source and output with pypdf and qpdf.

Durable results are in `probe-results.json`, `candidate.json`, and `inspection.json`. Tools exercised: .NET SDK 10.0.402 (net10.0), Python 3.13.17, pypdf 6.21.1, qpdf 12.3.2.

The fixtures are focused synthetic cases. They are not corpus members or a demand-weighted sample. Their signature dictionaries are structural placeholders, because assembly discovers signatures and does not validate them. The single real CMS case is generated per run with an ephemeral key and is not committed.

The PowerShell 5.1 policy setting had to be left alone, so the script was run as `& run.ps1`. pypdf's warnings about the deliberately dangling references are silenced below ERROR so that PowerShell does not treat them as failures.

## Measured current behavior

| Evidence | Cases | Measured result |
| --- | --- | --- |
| E197-1 Silent signature-family loss | extract [1], extract [2], and concatenate for approval (p1, p2, invisible), real engine-signed p2, certified P1/P2/P3, unresolved `/Perms /DocMDP`, document timestamp, FieldMDP lock, `/UR3`, dangling `/V`, `/V` without ByteRange | All 39 operations are accepted. `SignatureWidget` is counted only when the selected page holds the widget, so extracting page 1 of a certified P1 file, or of the real signed file, reports no loss at all. `/Perms`, `/Reference`, `/Lock`, and `/UR3` never reach any output (pypdf walk). The field that the FieldMDP signature locked (`Name`) is copied as an ordinary editable `Input1.Name`. |
| E197-2 Silent active-content loss | document JavaScript, JavaScript `/OpenAction`, catalog `/AA`, page `/AA` on p1 and p2, `/AA` on a Text annotation, JavaScript `/A` on a Square annotation | All accepted with no omission reported. No output contains `/JS`, `/AA`, `/OpenAction`, or a JavaScript action. The annotations themselves are copied without their actions, because of the copier key allowlist. |
| E197-3 Counted or refused active content | JavaScript and Launch links; widget `/AA` | Links are omitted and counted as `UnsupportedLink` (W-192 owns this). Field actions are refused for the whole input even when the field's page is discarded (W-190 rule, unchanged). |
| E197-4 Attachments and layers | `/Names /EmbeddedFiles`, FileAttachment annotation on p1 and p2, catalog `/AF`, `/OCProperties`, `/OC` on an annotation with no `/OCProperties` | The name tree is dropped silently. File-attachment annotations are counted as `UnsupportedAnnotation` only when their page is selected. `/AF` and `/OCProperties` are refused before output (A5). The `/OC` annotation is copied without `/OC`, so content its author made conditional becomes unconditional. |
| E197-5 Hybrid XFA, painted mark | `/XFA` plus AcroForm fields; page text "Digitally signed by J. Doe" | XFA is dropped silently while the AcroForm survives (W-198). The painted text survives in the encoded content, as content is never interpreted. |
| E197-6 Attachment recovery | Remove `data.txt` and save incrementally, then classify | The original is a byte prefix of the prepared revision. R197 refuses the original and admits the prepared revision. The prepared file was also extracted successfully. |
| E197-7 Exhausted budget | Unsigned field chain 300 levels deep on discarded page 2 | `PdfResourceLimitException` for all three operations, with zero destination bytes. The existing field walk is already whole-input and fails closed. |
| E197-8 Diagnostics | Every current refusal | The messages name a structure, for example "Assembly refuses a nonempty or unresolved /OCProperties." None names the operation or the input occurrence: concatenating `[control, ocprops]` gives the same message as extracting from ocprops alone. No message names a next step. No password, URI, or host detail appeared. |
| E197-9 Destinations and publication | See the next table | Preflight refusals write nothing. Failures after output begins differ by destination. |

| Case | Engine destination | Workflow |
| --- | --- | --- |
| Preflight refusal (`ocprops`) | 0 bytes | Stream: 0 bytes. File: not published, no temporary file left behind. |
| Destination fails after 64 bytes | `IOException`; 64 partial bytes remain | Stream: truncated to 0 bytes (best effort, as documented). |
| Source read fails after output begins | Raw `IOException` passes through; 120 partial bytes remain | File: `IOException`, not published, no temporary file left behind. |
| Same-length change to a custom `IPdfSource` after `Open` | Accepted; the changed content is copied into the output | Not run. Same engine path. |

In short, a preflight guarantee covers only the structures that preflight reads. Later source reads, caller mutation of a source that does not report changes, and destination failures keep the separately documented consequences (docs/behavior.md, page-extraction and workflow sections). The probe measured them; it did not change them.

## Counterfactual impact (read-only classification, R197)

`candidate.json` holds every row. Selections are limited to what R197 inspects, which removes W-189's whole-graph over-count (its limit L1).

- **Fixtures, extract [1]** (34 inputs, 30 accepted today):
  - Refused: 15.
  - Refused unless an unsigned derivative is requested: 7 (approval ×3, real engine-signed, missing ByteRange, timestamp, `/UR3`).
  - Admitted: 5.
  - Pending another item: W-192 links 2, W-198 hybrid XFA 1.
  - Selected-page scope works both ways: page `/AA` and file-attachment annotations refuse only when their page is selected.
- **Corpus** (51 tracked files; 50 classified; the public-key file cannot be decrypted by pypdf). Results for page 1 and for all pages were identical, because every category the corpus hits is whole-input. The corpus exercises no selected-page rule.
  - Of the 30 accepted today, R197 newly refuses 5: gpo-bills (approval, certification, FieldMDP lock), irs-fw9 and va-21-4138 (document JavaScript, usage rights, hybrid XFA, tags), pyhanko-signed (approval only), and qpdf-attachment (embedded file).
  - 1 is recoverable by the derivative (pyhanko-signed). 1 is recoverable by the measured attachment two-step (qpdf-attachment; inferred from E197-6, not run on that file). 3 stay blocked.
  - 6 further accepted files are tagged and wait on W-191. They are not counted here.
- **Reconciliation with W-189.** W-189's non-tag "core" union was 6 files. R197 has 5. The difference is frb-beige-book: W-189 counted its `/OpenAction` as active content, but it is the destination array `[405 0 R /FitH 792]`. R197 treats a destination or a `/GoTo` open action as navigation, which the A4 new-document rule drops. Do not add these counts to W-189's tag figures.

## Authority notes

- Shipped A3 drops `/DocMDP`, `/UR3`, and `/DSS` and omits every signature field. A4 omits catalog actions, the JavaScript tree, and attachments. These are documented contracts, not defects. The charter's "preserve or refuse" direction is target research and does not supersede them. The measured losses are therefore recorded as successful-omission behavior, not as violations.
- **For the owner: a wording question, not resolved here.** `AGENTS.md` says "DocMDP `P` 1 blocks every write and signing". A3, an adopted requirement marked Exists, extracts pages from a P1-certified file into a new unsigned file without consent (E197-1). The charter's DocMDP rules sit beside the incremental-write rules, and phase-2 A3 expressly drops `/DocMDP`, so they probably govern incremental writes. I did not choose between these readings. The recommended refusal of every certified input is consistent with either reading. If the owner rejects that refusal, the owner should also state how the charter sentence applies to new-file writes.
- W-194's proposed admission paragraph is reconciled here. The default refuses approval signatures, timestamps, certification, and FieldMDP restrictions even when their pages are discarded. Unresolved certification is refused. The derivative is a separate, named consent. W-194 slice A still owns empty signature fields and slice B owns `/Include` locks. A merely empty field never needs derivative consent.
- W-151 and D-66 remain binding. No signing scope is added. Classification is discovery: a missing ByteRange still counts as signed, and nothing here validates trust.

## Cost, migration and limits

- **Business use protected.** Callers can no longer turn a certified, locked, signed, scripted, or attachment-bearing document into an apparently ordinary excerpt without noticing.
- **Valuable loss under the derivative.** Signature evidence, timestamps, and Reader-extended features. Cryptographic validity never transfers. The source file is unchanged.
- **Remaining exclusions.**
  - Certified documents at any `P`, and signatures with field locks. Recovery is an uncertified original from the issuer.
  - Document scripts, script open actions, and catalog or page actions. There is no in-library recovery, and a generic remove-scripts operation is excluded by the charter.
  - Media annotations.
  - Malformed signature values.
- **Credential burden.** No change. Encrypted inputs still need `AllowUnencryptedOutput` and permitted credentials, per input.
- **Responsibility.**
  - Deleted: nothing of substance. The silent paths become refusals, and the passive-key copy allowlist remains.
  - Added: catalog, `/Perms`, field-value, and page or annotation classification; one consent option; derivative loss counts; and structured diagnostics.
  - Shared, and counted once: W-190's field walk, W-194's signature-field classification, W-198's and W-199's preparations as recovery paths, and W-156's vocabulary.
- **Rough effort.** Default refusal set plus diagnostics: 3–5 engineering days. Derivative consent and loss reporting: 2–3 days. Focused tests, independent pypdf/qpdf checks, and workflow-publication checks: 2–3 days. The basis is the localized code (`PdfAssemblyInspection` at 130 lines, the per-page annotation loop and field walk in `PdfPageExtractor` and `PdfAssemblyForms`), not measured implementation time.
- **Residual risk.**
  - Scripts in structures R197 does not inspect stay undetected and are not copied by the typed allowlist. Examples: actions inside XObjects or discarded pages, `/Next` chains beyond the open action, rendition and 3D data in unselected graphs, and anything inside content streams.
  - R197 promises no detection of arbitrary JavaScript.
  - Painted marks can still look like signatures.

**Unmeasured:**

- Real prevalence of page and annotation actions.
- Certified P2/P3 business demand.
- Signed hybrid XFA.
- Multi-signature histories and incremental-revision history beyond the final revision.
- Malicious budget shapes other than field depth (wide trees, huge name trees).
- Encrypted signed inputs.
- Owner-password runs.
- Framework sensitivity, rendering, and recipient-viewer behavior.

No subagents or persona handoffs were used. Product tests, the framework matrix, fuzzing, and Docker were not required and were not run. An implementation item must add permanent regressions for whatever contract it ships.
