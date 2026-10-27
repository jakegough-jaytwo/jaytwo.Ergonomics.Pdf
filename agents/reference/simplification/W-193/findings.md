# W-193 annotation appearance and relationship research

Completed 2026-10-26 against product revision `6b2e581010f5372a6a19b3ec4b3b39d7872c21df`, initially clean working tree. D-66 authorizes research, not implementation. No runtime, repository test-suite, protected contract or shipped behavior changed. Queue bookkeeping removes only the completed item and retains the remaining order. No independent reviewer was assigned; external tools provide independent implementation evidence, not fresh-context review. The four review attachments remain unavailable as recorded by W-188; this packet verifies its own observations.

## Reproduce and evidence

Run `powershell -NoProfile -ExecutionPolicy Bypass -File agents/reference/simplification/W-193/run.ps1` from the repo root. Existing `.tools/test.runsettings` supplies tool paths. pypdf generates 26 original MIT-licensed synthetic PDFs, including explicitly malformed cases. The disposable net10.0 probe uses public PdfDocument/PdfDocumentEditor.Concatenate APIs. Generated files/build products stay in ignored `out/W-193`, outside the fixture corpus. Retained files:

- `inputs.json`: provenance, sizes and SHA-256 for each input; `generate.py` reproduces them.
- `results.json`: 44 operations, page selections, destination sizes, exceptions and omission reasons.
- `candidate.json`: reproducible structural counterfactual classifications; no candidate implementation is claimed.
- `inspection.json`: pypdf input/output relationships, ownership, object identities, AP stream hashes, output hashes, tool versions and 12 renderer comparisons. Output IDs/hashes vary across reruns.
- `probe/`, `run.ps1`, `inspect.py`: complete disposable sources and assertions. No action is activated or network endpoint contacted.

Observed: 43/44 operations accepted; dangling AP refused with PdfParseException and zero destination bytes. All 43 outputs pass qpdf --check. The build has 0 warnings/errors. Python 3.13.17, pypdf 6.21.1, qpdf 12.3.2, Poppler 26.08.0, MuPDF 1.28.5. Six source/output appearance pairs were rasterized at 72 dpi in both renderers, 140 x 120 pixels each. Square, nested Form resource closure and shared AP have zero changed pixels within each renderer; Stamp and Caret each lose 2,400 colored pixels; plain FreeText loses its text. The fixture supplies both its existing AP and a DA font in source AcroForm DR. The source/output Stamp PNG pair was also visually inspected at 144 dpi: green rectangle versus blank page. Rendering proves these visual outcomes only, not editing, thread UI or arbitrary font fidelity.

The first inspector run exposed an error in its expected Popup count for two input occurrences (it counted the blank second page); the expectation was corrected to selected page-1 copies. The final full probe rerun passes. pypdf's expected warning for the intentionally dangling IRT reference is not suppressed. No product failure was hidden to complete research.

## Current contract and implementation inventory

Authority: phase-2 A2/A4/A5/A9/A10, ACCEPTANCE.md assembly paragraphs at lines 223-234, docs/behavior.md assembly paragraph. Successful omission of unsupported annotations/appearances and narrow key copying is shipped behavior. These observations are proposal motivations, not newly confirmed defects against the current contract.

Graph navigation: `graphify query 'PdfPageCopier annotation Popup IRT Stamp FreeText Caret appearance relationships' --budget 2600`, then `graphify explain PdfAssemblyAppearances`, `PdfAssemblyInspection`, `PdfTypedObjectCopier`. The first query was truncated; located raw sources supplied the inventory rather than treating graph results as exhaustive.

| Code anchor at baseline | Verified policy |
| --- | --- |
| PdfPageExtractor.cs:16-21, 172-248, 351 onward | Basic subtype whitelist; copy allowed keys; rebuild P per output page; omit Stamp/FreeText/Caret/Popup; no typed relationship pass |
| PdfAssemblyAppearances.cs:8-10, 40-106, 108 onward | Missing AP accepted; otherwise N required, N/R/D only; stream/state structure checked; resource closure with visited identities; 4,096 objects, depth 32, 8 MiB bytes plus structure; local failures return false, dangling references throw |
| PdfAssemblyInspection.cs:31-43, 58 onward | Selected Annots checked for associations, page resource graph inspected; no ownership/reply validation |
| PdfTypedObjectCopier.cs:51-66, 69-78 | Source-qualified resource map registers shells before children; direct annotation copies are distinct while indirect immutable resources share mappings within an input occurrence |

### Per-subtype disposition

| Subtype | Measured baseline / source inventory | Recommendation, pending approval |
| --- | --- | --- |
| Text | Retained; reply, Group and State/StateModel semantics lost in probes | Preserve plain notes, supported standard states, typed same-page replies/groups; invalid relationships refuse |
| Square/Circle | Square AP preserved; Circle source inventory only | Preserve bounded appearance and typed geometry/style; refuse unsupported meaningful entries rather than silently simplifying |
| Line/Polygon/PolyLine | Source inventory only | Preserve current typed subset; captions, leader lines, measurement, unknown intents must be preserved in a later slice or refused now |
| Ink | Source inventory only | Preserve paths and style; no new ink editing |
| Highlight/Underline/Squiggly/StrikeOut | Source inventory only | Preserve QuadPoints and bounded appearance; same markup relationships apply |
| Stamp | AP and no-AP examples omitted, counted | Add existing static AP plus Name; no new stamp API. Initially refuse absent AP, retaining an explicit evidence gap for standard reader icons |
| Caret | AP and no-AP examples omitted, counted | Add existing AP with RD/Sy; initially refuse absent AP |
| FreeText | AP and no-AP examples omitted, counted | Separate plain-text slice: AP and DA/resource fidelity, not just visible pixels. Initially refuse absent AP, callouts, typewriter/rich text/styles |
| Popup | Companion and orphan examples omitted, counted; parent retained without Popup | Preserve only typed markup companion, including Open and reciprocal ownership; no own AP/actions |
| Link | Governed by A6-A8 and W-192 research | Reuse W-192 contract; annotation relationship work must not change navigation occurrence rules |
| Widget/signature Widget | Separate field path; not probed here | Reuse W-194/W-197 proposals and approved field rules; no adoption or signature-field change here |
| FileAttachment/Sound/Movie/Screen/3D/RichMedia/Watermark/Redact/PrinterMark/TrapNet and unknown | Outside present basic whitelist, source inventory only | Refuse retained unsupported semantics; no general annotation/media/attachment feature added |

### Per-key inventory

The first seven groups below exhaust the current 27-key AnnotationKeys allowlist (including keys used only on links/widgets). Remaining groups identify rebuilt or omitted keys. Presence in the list does not prove value validity, semantic completeness or every subtype's use.

| Keys | Current / proposed |
| --- | --- |
| Type, Subtype, Rect, Contents, NM, M, F | Copied on retained basic annotations; retain with typed validation |
| Border, BS, C, CA | Copied; retain applicable style/opacity; AP transparency remains in AP, not synthesized from CA |
| T, Subj, CreationDate | Copied markup metadata; retain |
| Name, Open | Copied; retain applicable subtype semantics |
| AP, AS | Copied if appearance classifier accepts; preserve state and resources or refuse under proposed R193-4/5 |
| H, MK | Present in shared allowlist; keep only applicable Link/Widget path, not arbitrary basic annotation fields |
| QuadPoints, L, LE, IC, Vertices, InkList, RD | Copied; retain only applicable subtype geometry, validate types/counts |
| P | Rebuilt correctly in measured outputs; proposed validate source ownership before rebuilding |
| Popup, Parent, IRT, RT | Not in allowlist; dropped without a relationship-loss count. Proposed typed instance-local references; Parent means Popup ownership here, not field hierarchy |
| State, StateModel | Dropped from retained Text; proposed preserve standard state semantics with valid chain |
| DA, Q, Sy | Not in allowlist; required for proposed FreeText/Caret semantic completeness; typed addition separate from subtype admission |
| RC, DS, IT, ExData, BE, CL, Measure, LL/LLE/LLO, Cap/CP/CO and other extended geometry/intent | Not copied by basic path; proposed narrow typed support or refusal as specified in proposal.md, not AP-only acceptance |
| A/AA, OC, AF, StructParent/SE | Specialized action, optional-content, association and tag semantics; do not copy generically. Reconcile W-192/W-194/W-197 and current A5 gates; no silent new stripping rule |

## Relationship and resource observations

The retained basic annotations always have distinct indirect identities and correct output-page P in the checked operations, including repeated source pages and input occurrences. Shared AP references stay shared within an occurrence and use a different mapping for the second occurrence. The nested Form's raw program and the unused resource self-cycle both survive. These are bounded positive examples, not a general closure correctness proof.

Popup pair: valid input markup Popup and Popup Parent references are independently recorded; only markup survives. Reply/Group: both annotations survive, IRT/RT do not. Standard Review state Text survives without State/StateModel. These losses have no omission count, except the separately omitted Popup. Appearance equality cannot establish thread/group/state fidelity.

Malformed inputs: cross-page IRT, cyclic IRT, dangling IRT, mismatched Popup Parent and multiply owned source annotation are accepted after losing relationships or rebuilding ownership. Repeating the page does not restore semantics. A cross-page reply is not a valid excluded-destination case: the standard requires same-page targets. A wholly unselected second-page Stamp is uninspected; selecting it produces counted UnsupportedAnnotation. This distinguishes legitimate page exclusion from successful loss on retained pages.

Local AP depth and byte-limit probes each omit the complete annotation with UnsupportedAppearance; a resource action does likewise. A dangling AP reference aborts before output. The unused AP-resource cycle is accepted without recursion failure. Object-count/global exhaustion are source-inspected, not measured here. No timing, cancellation, aggregate-work or adversarial bound is inferred; W-149/W-168 remain separate.

## Authoritative PDF semantics

Read-only owner-supplied `C:/jake/git/pdfdocs`, using its AGENTS.md and keys.tsv/clauses.tsv before prose. No licensed specification text is copied into this packet. PDF page indices below follow that reference repository.

- ISO 32000-1 12.5.5, `187-12-5-5-appearance-streams.md`, pp.395-397: self-contained Form appearances, rectangle/BBox/Matrix mapping, normal/rollover/down states and state selection. Structural preservation and appearance-program validity are different claims.
- 12.5.6.2, `190-12-5-6-2-markup-annotations.md`, Table 170, pp.399-401: Popup ownership, same-page IRT, R/Group semantics and group attributes. Reciprocal Popup/Parent is legitimate; arbitrary reply cycles are not a substitute for a reply tree. Group primary has no IRT. This is why output-page-instance identity applies instead of link-to-first-copy navigation.
- 12.5.6.3/4, state discussion and `192-12-5-6-4-text-annotations.md`, pp.401-402, Table 172: state annotations update another annotation through IRT, with author and state model. Losing IRT/State changes the review record.
- 12.5.6.6, `194-12-5-6-6-free-text-annotations.md`, pp.403-405, Table 174: DA is required, AP controls present display, Q/intent/callout/style carry separate semantics. 12.7.3.3 in `236-12-7-3-field-dictionaries.md`, pp.442-444, identifies DA font/default-resource requirements. Copying AP alone does not prove regeneration.
- 12.5.6.11/12, `199-...caret...md` and `200-...rubber-stamp...md`, pp.412-413, Tables 180/181: Caret RD/Sy and Stamp Name are passive presentation semantics; copying an existing Stamp does not create a stamping API.
- 12.5.6.14, `202-12-5-6-14-pop-up-annotations.md`, pp.413-414, Table 183: Popup is a markup companion without its own AP/actions; Parent, if present, is indirect. The stricter same-page, listed membership and unambiguous-owner admission is a proposed supported subset, not a claim that every excluded producer form violates ISO.

## Compatibility, savings and limits

Business use: reviewed agreements, invoice approval marks, proofreading carets, text-box comments, threaded discussions and review states in assembled packets. The recommendation prevents a successful result from presenting a flattened conversation or missing approval mark as preservation. Unsupported variants require a simpler original or a separately prepared derivative that deliberately loses annotation behavior; no generic stripping/preparation call is offered. Selecting an unrelated page can avoid page-local unsupported annotations. Repeating a malformed page or obtaining an owner password cannot repair its relationships. Existing encrypted-input consent and permissions remain necessary, with no new credential requirement.

Counterfactual under the complete draft, inferred rather than executed: of **43 accepted baseline operations**, 17 would newly refuse and 26 remain accepted. New refusals: three absent-AP added-subtype cases; orphan Popup; mismatched Popup Parent; four cross-page-reply selections; cyclic reply; dangling reply; two duplicate-owner selections that include both source pages; three locally unsupported AP cases; annotation AA. The already-refused dangling AP is outside that denominator. FreeText's existing-AP case remains accepted only after the proposed DA/font work. Three existing-AP subtype examples gain preservation, Popup and reply/Group/state operations gain relationships; baseline accepted counts alone conceal those changes. These synthetic cases are distinct-risk probes, not customer refusal percentages, and cannot be added to W-189's overlapping whole-corpus totals.

Responsibility removed: successful annotation-loss fallback for the replaced scope. Responsibility retained/added: structural classification, typed resource closure, ownership, graph remapping, refusal diagnostics and regressions; this is mostly correctness/preservation work, not code deletion. No measured line-count or performance savings. Effort ranges and independent slices are in proposal.md; preflight work overlaps other simplification items and must not be counted twice.

Missing evidence: customer annotation corpus/prevalence; independent human/context review; the four original reviews; UI verification of comments, groups, popups and subsequent FreeText editing; no-AP viewer icon fidelity; malformed value/type coverage for every key; AP state selection/transforms/fonts/images beyond these minimal examples; empirical object-count/global-work budgets. All candidate behavior remains inference until implemented and verified. No full framework matrix, fuzz, qualification update or Docker gate was required or run. These gaps constrain adoption claims, not the completeness of this bounded research deliverable.
