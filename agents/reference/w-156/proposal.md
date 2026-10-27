# W-156 proposal: bounded PDF admission inspection

Status: recommendation awaiting owner approval. Nothing here is adopted, shipped or queued. Type and member names are illustrative. Spec text below is proposed wording; `docs/spec/` is not edited.

## 1. Four facts, kept apart

| Question | Answer carried by | Values |
| --- | --- | --- |
| Did the file parse? | `PdfDocument.Open` succeeding, or throwing as today | A file that cannot be opened has **no report**. Open failure is never a verdict. A password-protected file is opened by the host with the password first. |
| What did inspection see? | `PdfInspectionReport.Findings` | Structured, located, categorised. |
| How much did inspection cover? | `PdfInspectionReport.Coverage` and `.Gaps` | Per-scope `Complete`, `Partial`, `NotInspected`; each gap names kind, scope, location and the categories it could hide. |
| Does it satisfy the host's policy? | `PdfInspectionPolicy.Evaluate(report)` returning `PdfAdmissionDecision` | `Rejected`, `Indeterminate`, `WithinPolicy`. |
| Was anything removed? | Nothing | The API is read-only. No inspection type is accepted by `PdfDocumentEditor`. `WithinPolicy` says nothing about physical removal, earlier revisions, or content outside the inspected scope. |

An incremental save never turns a `Rejected` or `Indeterminate` document into an admitted one by hiding content in earlier revisions; the report says the earlier revisions were not inspected (section 5).

## 2. Public surface (engine, namespace `jaytwo.Pdf.Inspection`)

```csharp
public sealed partial class PdfDocument
{
    public ValueTask<PdfInspectionReport> InspectAsync(
        PdfInspectionOptions? options = null, CancellationToken cancellationToken = default);
}

public sealed class PdfInspectionOptions            // immutable; every limit has a documented default
{
    public int MaxStructureVisits { get; init; }    // default 100_000
    public int MaxDepth { get; init; }              // default 256, the existing traversal depth
    public int MaxFindings { get; init; }           // default 1_000
}

public sealed class PdfInspectionReport
{
    public int RevisionCount { get; }
    public IReadOnlyList<PdfInspectionFinding> Findings { get; }
    public int SuppressedFindingCount { get; }      // matches beyond MaxFindings, counted not listed
    public int InertActionCount { get; }            // in-document GoTo without /Next, counted not listed
    public IReadOnlyList<PdfInspectionGap> Gaps { get; }
    public IReadOnlyDictionary<PdfInspectionScope, PdfInspectionCoverage> Coverage { get; }
    public bool IsComplete { get; }                 // no gaps and every non-excluded scope Complete
    public long StructureVisits { get; }
}

public readonly struct PdfInspectionCategory : IEquatable<PdfInspectionCategory>
{   // open vocabulary: new categories arrive in minor versions without breaking switch statements
    public static PdfInspectionCategory JavaScript, Launch, ExternalReference, FormAction,
        EmbeddedFile, RichMedia, Xfa, OtherAction, UnknownAction;
    public string Name { get; }
}

public sealed class PdfInspectionFinding
{
    public PdfInspectionCategory Category { get; }
    public PdfInspectionTrigger Trigger { get; }    // OpenAction, DocumentAction, PageAction, AnnotationAction,
                                                    // FieldAction, OutlineItem, NamedScript, EmbeddedFile, Annotation, ...
    public PdfInspectionLocation Location { get; }
    public PdfInspectionReason Reason { get; }      // Present, ReachedThroughAction, ReachedThroughIndirection
    public int ChainDepth { get; }                  // 0 for the trigger's own action, n for the nth /Next
}

public sealed class PdfInspectionLocation
{
    public string Path { get; }                     // "Catalog/OpenAction/Next[1]", "Page 3/Annots[2]/A"
    public int? PageNumber { get; }                 // 1-based
    public string? FieldName { get; }
}   // no object numbers, dictionaries or tokens: the internal model stays internal

public sealed class PdfInspectionGap
{
    public PdfInspectionGapKind Kind { get; }       // Unresolvable, Unsupported, Encrypted, DepthExceeded, BudgetExhausted
    public PdfInspectionScope Scope { get; }
    public PdfInspectionLocation Location { get; }
    public IReadOnlyCollection<PdfInspectionCategory> Affects { get; }   // empty = could hide any category
}

public enum PdfInspectionScope { Catalog, NameTrees, Outlines, Pages, AcroForm,
    Resources, ContentStreams, StructureTree, PriorRevisions, UnreachableObjects }
public enum PdfInspectionCoverage { Complete, Partial, NotInspected }
```

Policy and decision (engine; a workflow-layer wrapper in `jaytwo.Ergonomics.Pdf` is a later ticket):

```csharp
public sealed class PdfInspectionPolicy        // immutable
{
    public static PdfInspectionPolicy Strict { get; }          // allows no category
    public PdfInspectionPolicy Allow(PdfInspectionCategory category);
    public PdfInspectionPolicy RequireScope(PdfInspectionScope scope);   // scope must be Complete or the decision is Indeterminate
    public PdfInspectionPolicy WithRule(Func<PdfInspectionFinding, PdfRuleOutcome> rule);   // host extension point
    public PdfAdmissionDecision Evaluate(PdfInspectionReport report);
}
public enum PdfRuleOutcome { Abstain, Allow, Reject }

public sealed class PdfAdmissionDecision
{
    public PdfAdmissionVerdict Verdict { get; }                // Rejected | Indeterminate | WithinPolicy
    public IReadOnlyList<PdfInspectionFinding> RejectingFindings { get; }
    public IReadOnlyList<PdfInspectionGap> BlockingGaps { get; }
    public IReadOnlyCollection<PdfInspectionScope> Exclusions { get; }  // scopes not Complete that the policy did not need
}
```

Cancellation: `InspectAsync` honours the token between structure visits and throws `OperationCanceledException`. It never returns a partial report, so a cancelled run cannot be mistaken for an incomplete one or for an admission.

## 3. The admission invariant

> `WithinPolicy` is returned only when (a) no finding is rejected by the policy, and (b) no gap, suppressed finding or non-`Complete` scope could hide a category the policy does not allow.

Rules, all enforced inside `Evaluate` (the decision's constructor is `internal`, so a host cannot build an affirmative decision by hand):

1. **Closed by default.** `Strict` allows nothing. A category the policy has not heard of (a newer engine's) is rejected, never ignored.
2. **Gaps with an empty `Affects`** could hide anything, so they block every policy that does not allow every category.
3. **Suppressed findings block.** If `SuppressedFindingCount > 0` and any category is still rejected by policy, the decision is `Indeterminate`. A host can raise `MaxFindings`, but a capped list cannot admit.
4. **`Rejected` dominates `Indeterminate`.** A found problem is reported even when coverage is also incomplete.
5. **Exclusions are echoed, not hidden.** Scopes that are `NotInspected` by design (`Resources`, `ContentStreams`, `StructureTree`, `UnreachableObjects`, and `PriorRevisions` when `RevisionCount > 1`) appear in `Exclusions` of every `WithinPolicy` decision, so the host sees exactly what its policy did not cover.
6. **Allowing a category removes its gaps from consideration.** A policy that allows `JavaScript` is not made indeterminate by a gap that can only hide JavaScript.

The word is `WithinPolicy`, not `Safe` or `Clean`: it asserts only that the inspected structures satisfy the stated rules.

## 4. Inspected scope and bounded traversal

Reads structures only. It never decodes a content stream, never interprets painted content, and does not decode a stream to find an action (`/JS` presence is the finding; its text is not read). It does not claim to detect behavior outside the structures below.

| Scope | Structures | Categories reported |
| --- | --- | --- |
| Catalog | `/OpenAction`, `/AA`, `/AF` | any action category; `EmbeddedFile` for a nonempty `/AF` |
| NameTrees | `/Names /JavaScript`, `/Names /EmbeddedFiles` (entries counted, trees walked within budget) | `JavaScript`, `EmbeddedFile` |
| Outlines | each item's `/A` | action categories |
| Pages | page `/AA`, `/AF`, each annotation's subtype, `/A`, `/AA` | action categories; `RichMedia` for Sound, Movie, Screen, RichMedia and 3D annotations; `EmbeddedFile` for FileAttachment |
| AcroForm | `/XFA`; every field's inherited `/A` and `/AA`, widget actions | `Xfa`, action categories |
| Never inspected | `Resources`, `ContentStreams`, `StructureTree`, `UnreachableObjects`, `PriorRevisions` | reported as `NotInspected` coverage |

Action categories are decided by `/S` against the action-type table in ISO 32000-2 clause 12.6.4.1 (`JavaScript`; `Launch`; `ExternalReference` for URI, GoToR, GoToE; `FormAction` for SubmitForm, ResetForm, ImportData; `RichMedia` for Sound, Movie, RichMediaExecute; `OtherAction` for Named, Hide, SetOCGState, Rendition, Trans, GoTo3DView, Thread, GoToDp; `UnknownAction` for any other name). In-document `GoTo` is navigation and is counted in `InertActionCount`. Each action's `/Next` chain is followed, whether the next action is direct, indirect or an array.

- **Work limit.** One unit is one structure visit: an indirect resolution or a container entered. It is deterministic, independent of the clock and machine. Default 100,000 covers the 4,000-page form shape (about 4,000 page visits plus annotations and fields, W-167 baseline) with large margin.
- **Memory.** Follows the visited set, which is bounded by `MaxStructureVisits`, plus at most `MaxFindings` findings. No whole-file buffering, no stream decode, so a gigabyte file inspects in the same memory as a small one.
- **Cycles.** A visited action or node is not re-entered. A `/Next` loop is reported once and is not a gap.
- **Depth.** Trees, outlines, field trees and `/Next` chains stop at `MaxDepth` with a `DepthExceeded` gap.
- **Dangling reference in a slot the policy cares about.** ISO 32000 reads it as null. This is still a gap (`Unresolvable`), because the engine never repairs the cross-reference by scanning and a repairing reader may reach an object the engine cannot. `Affects` is empty.
- **Unsupported / encrypted.** An object stream the engine cannot decode (unsupported filter, or a crypt filter it cannot apply) makes every object inside unresolvable. That is a gap with kind `Unsupported` or `Encrypted` on the scope that needed the object. A document that needs a password or uses public-key encryption never produces a report. These two kinds are **not exercised** by the reference model (section 7).

## 5. Earlier revisions and unreachable objects

The report inspects the current view only. `RevisionCount` is reported, and when it exceeds 1, `PriorRevisions` is `NotInspected`. Incremental updates preserve older bytes, including removed scripts and attachments, so `WithinPolicy` on a revisioned file describes the latest view and nothing deeper. A host that needs the earlier content gone must rewrite the file outside this library (rewriting in place is out of scope, spec section 11). `UnreachableObjects` (objects no trailer path reaches) is likewise `NotInspected`, because the engine does not scan the file. A policy may *require* either scope with `RequireScope(...)`; since neither can ever be `Complete` in v1, such a policy is always `Indeterminate`. That makes the limitation enforceable rather than documentary. (`RequireScope` is specified, not exercised by the model.)

## 6. Extensibility without exposing the object model

- Categories are an open struct; triggers, scopes and gap kinds are enums that may grow in minor versions, documented as non-exhaustive.
- `WithinPolicy`/`Rejected` decisions depend on `Category` and host rules over the public `PdfInspectionFinding`; no host code ever sees a dictionary, token or object number.
- Hosts add rules (`WithRule`) but cannot add traversal. Extra inspectors are an engine change, so coverage claims stay the engine's.
- Payloads (URIs, file names, script text) are not in findings. A later opt-in `Details` surface would be a separate owner decision because those values are sensitive and unbounded.

## 7. Relationship to W-197 (R197)

R197 is an assembly-time preflight whose scope table (whole input, selected pages) shares this proposal's category vocabulary and budget model. Recommended build order: one internal traversal, used by R197 with its input-occurrence scope and refusal exceptions, and by `InspectAsync` for the general report. R197 keeps refusing (it has no policy, report or `Indeterminate`); it does not replace this API, because it has no completeness reporting and no host-facing use outside assembly. No overlap savings are claimed beyond sharing the traversal and category list.

## 8. Evidence

`run.py` generates 19 original fixtures, drives the reference model (`model.py`, an independent pypdf parser implementing sections 2 to 5) and asserts expected categories, gaps and verdicts. Result: 19 fixtures, all expectations met; qpdf `--check` exit 0 on every fixture; recorded in `results.json`.

| Mechanism | Fixture | Verdict under `Strict` | Notes |
| --- | --- | --- | --- |
| Control | clean (GoTo link only) | `WithinPolicy` | `InertActionCount` 1; `Exclusions` lists four never-inspected scopes |
| JavaScript | openaction-js, names-js, page2-aa | `Rejected` | catalog, name tree, and a discarded-looking page 2 |
| Nested action | nested-next-launch | `Rejected` (Launch) | OpenAction GoTo then `/Next` array, indirect GoTo, `/Next` Launch |
| Indirect action | indirect-field-aa | `Rejected` (FormAction, JavaScript) | field, indirect `/AA`, indirect ResetForm, `/Next` JavaScript |
| External reference | link-uri, link-gotor | `Rejected` | also `Allow(ExternalReference)` on link-uri gives `WithinPolicy` with exclusions |
| Form action | submit-form | `Rejected` | |
| Embedded files | embedded-files, fileattachment-annot | `Rejected` | name tree and FileAttachment annotation |
| Rich media | richmedia-annot | `Rejected` | |
| Unknown | unknown-action (`/S /VendorExtension`) | `Rejected` (UnknownAction) | unknown is rejected, not ignored |
| XFA | xfa | `Rejected` | |
| Cycle | next-cycle | `Rejected` (OtherAction) | reported once, no gap |
| **Incomplete: dangling** | dangling-action | `Indeterminate` | `Unresolvable` gap, `Pages` coverage `Partial` |
| **Incomplete: budget** | wide-budget with `MaxStructureVisits=50` | `Indeterminate` | `BudgetExhausted`; also `Indeterminate` under partial-allow; `WithinPolicy` only for allow-all |
| **Incomplete: depth** | deep-outline with `MaxDepth=10` | `Indeterminate` | the JavaScript at depth 31 is not seen; the gap says so; at default depth it is `Rejected` |
| **Incomplete: cap** | openaction-js with `MaxFindings=0` | `Indeterminate` | suppressed finding cannot admit |
| Earlier revision | revisioned-removed-js | `WithinPolicy` | `RevisionCount` 2, `PriorRevisions` `NotInspected`; the removed OpenAction JavaScript is unseen, as designed |
| Parse failure | password-required | no report | `NotOpened`; not a verdict |

Not exercised: object-stream decode failures (`Unsupported`, `Encrypted` gaps) and public-key encryption refusal at open. pypdf cannot write them, and the reference model has no decoder. They are specified from the engine's existing behavior (spec section 4 and `docs/behavior.md`) and need engine fixtures in the implementation ticket. The model measures contract behavior and traversal logic, not engine performance or memory; those need the implementation and the W-167 baseline method.

## 9. Proposed spec text (Phase 1, new requirement, status New)

> R-ADM. `PdfDocument.InspectAsync` reports, without modifying the document, the active-content and interactive structures listed in this section, with their location, explicit coverage of each scope and every gap that could hide a finding. It never decodes content streams, never reads signed ranges, and traverses within a documented structure-visit budget and the caller's cancellation token. `PdfInspectionPolicy.Evaluate` returns `WithinPolicy` only when no finding is rejected and no gap, suppressed finding or uninspected scope could hide a category the policy does not allow. Earlier revisions and unreachable objects are reported as not inspected. Inspection is not sanitization, signature validation, malware scanning, or a statement about painted content.

## 10. Owner decisions requested

1. **Scope.** Adopt a general host admission inspection API in Phase 1, or fold W-156 into R197 (assembly refusal only). Recommendation: adopt; R197 shares the traversal.
2. **Verdict names** `Rejected` / `Indeterminate` / `WithinPolicy` and the open `PdfInspectionCategory` struct.
3. **Cancellation** throws and never returns partials (recommended), versus returning an incomplete report.
4. **Defaults:** 100,000 visits, depth 256, 1,000 findings; confirm after the implementation measures the 4,000-page shape.
5. **Prior revisions** as `NotInspected` forever in v1, with `RequireScope` making that enforceable. Alternative: a later item walks earlier revisions.
6. **Payload details** (URIs, file names) excluded from findings in v1.

## 11. Draft implementation items (not created)

| Draft | Scope |
| --- | --- |
| P156-A | Internal traversal and budgets shared with R197; report, finding, gap and coverage types; engine fixtures for undecodable object streams and encrypted-handler refusals |
| P156-B | `PdfInspectionPolicy` and `PdfAdmissionDecision`, invariant tests (including property tests that no gap, cap or exclusion yields `WithinPolicy`) |
| P156-C | Spec text, `ACCEPTANCE.md` rows, `docs/behavior.md` section (hosts own process, storage and network controls; trust boundary shared with W-158) |
| P156-D | Optional workflow-layer wrapper in `jaytwo.Ergonomics.Pdf` |
