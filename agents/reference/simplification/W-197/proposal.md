# W-197 approval packet: assembly refusal and the unsigned derivative

Status: this is a recommendation awaiting Gate 2 owner approval. The wording below is not adopted, and no implementation item is activated or queued. Option and enum names are illustrative. W-194's slices A and B, W-190's form rules, W-191's tag decision, W-192's link actions, W-198's XFA preparation and W-199's rights removal keep their own scopes. This packet covers only the admission frame they plug into and the signed-input derivative.

## R197 inspection scope and budgets

Preflight runs for every input occurrence before the first destination byte, as A5 already requires. It reads structures; it never decodes content streams or interprets painted content. It does not claim to detect JavaScript or any other behavior outside the structures listed here.

| Scope | Inspected | Bound |
| --- | --- | --- |
| Whole input | Catalog `/Perms` (`/DocMDP`, `/UR3`, `/UR`), `/OpenAction`, `/AA`, `/AF`, `/OCProperties`, `/NeedsRendering`, `/AcroForm /XFA`; presence of a nonempty `/Names /JavaScript` or `/Names /EmbeddedFiles` tree root. Only `/Names` and `/Kids` are checked, so the tree is not walked. | O(1) per input |
| Whole input | The complete AcroForm field tree, which the existing form walk already reads. For each field: the inherited `/FT`, `/A` and `/AA`, `/Lock`, and the `/V` of a signature field, resolved to `/Type`, `/SubFilter` and the `/TransformMethod` of each `/Reference`. | The existing traversal depth (256) and mapped-object budget, plus a proposed per-input field-node budget |
| Selected pages | Page `/AA`; each retained annotation's subtype, `/A`, `/AA`, `/OC` and `/AF`; the retained resource graph (A5, unchanged), including `/Properties` entries of type OCG or OCMD. | The existing structural budgets |
| Never | Discarded pages and their annotations, except through the whole-input field walk. Content streams. The structure tree (W-191). Actions inside XObjects or Form appearance streams. `/Next` chains, except that any `/Next` on the open action counts as active. Earlier revisions, and objects reachable only from discarded structures. | — |

If anything inside this scope cannot be resolved, is cyclic, or exceeds a budget, assembly refuses. It never admits an input it could not fully inspect.

## Exact proposed contract

Replace phase-2 A3 with:

> A3. Assembly inspects each input occurrence for approval signatures, document timestamps, certification, field-lock signatures and usage rights before output, whether or not the pages they appear on are selected. A signature field whose value resolves to a signature dictionary is signed, even when its byte range or contents are missing or invalid. A signature value that is present but does not resolve to a dictionary is malformed. By default, every one of these refuses the input. With `AllowUnsignedDerivative`, an input whose only such structures are approval signatures, document timestamps or usage rights is accepted. Its signed fields and their widgets, `/UR3`, `/UR`, `/DocMDP` and `/DSS` are omitted, and the result reports each omission. A certified input, at any `/P` or when the certification does not resolve, an input with a signed field that carries `/Lock` or a FieldMDP reference, and an input with a malformed signature value are refused with or without consent. Consent does not replace `AllowUnencryptedOutput` or the A11 permission checks. Empty signature fields follow [W-194 slice A wording]. No output carries a signed appearance, a signature dictionary or a claim that a signature remains valid. Marks painted in page content are not inspected or removed.

Add a new requirement after A5:

> A5a. Before output, assembly refuses an input occurrence that has a nonempty `/Names /JavaScript` tree, an `/OpenAction` that is an action other than `/GoTo` without `/Next`, a nonempty catalog `/AA`, or a nonempty `/Names /EmbeddedFiles` tree. It refuses an input when a selected page has a nonempty `/AA`, when a retained annotation other than a link or widget has `/A` or `/AA`, when a retained annotation is a `/FileAttachment`, `/Sound`, `/Movie`, `/Screen`, `/RichMedia` or `/3D` annotation, or when a selected page or retained annotation refers to an optional-content group while the input has no `/OCProperties`. A destination open action is navigation, and the A4 new-document rule omits it. Field actions keep the existing form rule. Link actions follow A7 [or W-192's approved replacement]. These checks cover the inspected scope described in `docs/behavior.md`, and they do not detect behavior outside it.

Amend A4 so that it no longer lists document attachments, `/OpenAction` actions, catalog `/AA` or the JavaScript name tree as silent omissions. A destination `/OpenAction` stays omitted.

Amend A10 by adding one sentence:

> Signature-derivative omissions are counted per input occurrence as `ApprovalSignature`, `DocumentTimestamp` and `UsageRights`.

The counts still do not grow with page or annotation count.

## Refusal diagnostics

Every preflight refusal during assembly is thrown before output and has these properties:

- **Operation.** The message names `ExtractPages` or `Concatenate`. The workflow propagates the engine exception unchanged, as it does today.
- **Input occurrence.** A 1-based position in the caller's sequence. Proposed: a `PdfAssemblyNotSupportedException.InputOccurrence` property. `PdfEncryptionRestrictedException` and `PdfResourceLimitException`, when raised by assembly preflight, include the occurrence in their message. The type stays unchanged for existing callers.
- **Category.** Proposed: a `PdfAssemblyRefusal` enum with `Certification`, `FieldLockSignature`, `MalformedSignature`, `SignedInput`, `DocumentScript`, `OpenAction`, `AdditionalActions`, `AnnotationAction`, `DocumentAttachments`, `AssociatedFiles`, `AttachmentAnnotation`, `MediaAnnotation`, `OptionalContent`, `Xfa`, `FieldAction`, `Form`, `Appearance`, `Incomplete`. `Form` and `Appearance` belong to W-190 and W-194. Only `SignedInput` is recoverable by consent.
- **Next step.** A fixed sentence chosen by category:

| Category | Next step in the message |
| --- | --- |
| `SignedInput` (approval, timestamp, rights only) | "Set AllowUnsignedDerivative to create an unsigned copy; signatures are not carried over." |
| `Certification`, `FieldLockSignature` | "A certified or field-locked document cannot be assembled; use an uncertified original from its author." |
| `MalformedSignature`, `Incomplete` | "The input is malformed or exceeds an inspection limit." |
| `DocumentAttachments` | "Remove the attachments in an incremental revision (PdfDocumentEditor.Attachments.Remove), assemble that revision, and add them to the output if needed." |
| `Xfa` | The W-198 preparation, once approved; otherwise "Hybrid XFA cannot be assembled." |
| Scripts, actions, media, optional content | "Assembly does not copy document behavior; use a source without it." |

Messages never include passwords, URIs, file paths, script text, attachment or field names, object numbers, or the text of host or I/O exceptions.

## Pre-output guarantees versus later failures

Proposed docs/behavior.md wording, measured by E197-9:

> Assembly inspects every input before writing. A refusal from that inspection writes nothing to the destination, and the workflow publishes no file. Inspection does not guarantee that later source reads succeed, that a caller's source stays unchanged, or that the destination accepts every write. Once output begins, a failure leaves partial bytes in an engine destination, which the caller must discard. A workflow stream destination is truncated to zero on a best-effort basis, and a workflow file is never published. A source that changes without reporting it can supply changed bytes to the output.

## Decisions for the owner

1. **Certified P2/P3.** Recommended: refuse with or without consent (C1). The alternative is to admit P2 and P3 into the derivative while P1 and unresolved certification stay refused (C2). C1 follows the most conservative reading of "consent does not override DocMDP". C2 treats a new file as outside DocMDP's modification semantics. On the measured corpus, C1 versus C2 changes no accepted file: gpo-bills also carries a field lock, and opm-of306-pyhanko-certified is already refused for field actions. Real demand is unmeasured.
2. **Field-lock signatures.** Recommended: refuse the whole input, even when the locked fields are not retained. The alternative is to admit when no locked field is selected. That needs W-194 slice B's name mapping and is deferred.
3. **Attachments.** Recommended: refuse, with the existing two-step recovery. The alternative is to count attachments into the derivative. That is not recommended, because attachments are content, not provenance.

## Draft follow-ups (no work items created)

| Draft | Scope and candidate paths | Acceptance and validation |
| --- | --- | --- |
| P197-A: Default refusal frame and diagnostics | `PdfAssemblyInspection.cs`, `PdfPageExtractor.cs` and `PdfAssemblyForms.cs` under `src/jaytwo.Pdf/Internal/`; `PdfAssemblyNotSupportedException.cs` and a new refusal enum under `src/jaytwo.Pdf/`; assembly tests under `test/jaytwo.Pdf.Tests/`; `test/jaytwo.Ergonomics.Pdf.Tests/` for publication; A3/A4/A5a/A10 wording after approval; `ACCEPTANCE.md`; `docs/behavior.md` | Each R197 category refuses, from both extraction and concatenation, with zero destination bytes and the occurrence in the message. Discarded-page signatures refuse, and discarded-page actions do not. Exhausted field budgets refuse. Workflow file and stream refusals publish nothing. Partial-output cases are documented, not changed. Corpus rows reproduce `candidate.json`. |
| P197-B: Explicit unsigned derivative | Depends on P197-A. `PdfAssemblyOptions.cs`, `PdfOmissionReason.cs` and the internal assembly paths; the same test and contract paths | With consent: approval, timestamp and rights inputs are accepted, report counts, and contain no signature dictionary, `/Perms` or signed widget (pypdf/qpdf). Certified, field-lock and malformed inputs still refuse. Encrypted inputs still need both consents. A real engine-signed source stays unchanged. |
| W-156 disposition | `agents/work-items/W-156.md` Notes only, with owner approval | Retain W-156 and all its criteria. R197 is an assembly-specific fail-closed preflight. It does not distinguish parse success from inspection completeness from a policy verdict, it reports nothing about uninspected structures, and it covers no non-assembly host use. Record that W-197 is complete and that W-156 should start from R197's category vocabulary and budget model. No overlap savings are claimed. |

Each implementation item must list its exact paths before activation and add permanent regressions. The field and signature classification shared with W-190 and W-194 is counted once. Neither draft requires the full framework matrix or the Docker gate.
