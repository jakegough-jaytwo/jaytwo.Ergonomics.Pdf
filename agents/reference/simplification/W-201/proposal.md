# W-201: exact proposed identity/viewer contract

Status: unadopted Gate 2 proposal for Jake, 2026-10-26. No requirement wording, status or shipped behavior is changed by this file. Evidence and limits are in [findings.md](findings.md). Recommend retaining fresh identity and viewer defaults, with active-action handling delegated to W-192/W-197's proposed admission rules. Names for new omission diagnostics remain W-192's proposal.

## Per-property disposition

Apply identically to extraction and every concatenation occurrence, including single-input, reordered and repeated-input cases:

| Property | Proposed rule | Rationale/recovery |
| --- | --- | --- |
| Info Title, Author, Subject, Keywords | Exclude every input value. Do not choose first input or concatenate lists. | Host chooses new-document values after assembly through ordinary setters. |
| Info Creator, custom keys | Exclude. Do not describe the new file as created by an input's application. | Host may intentionally supply new values/custom keys afterward. |
| Info Producer | Generate only `jaytwo.Pdf`. Do not copy source Producer or include an unrequested source history. | Describes this writer; existing later explicit editing remains available. |
| Info CreationDate, ModDate | Omit at assembly; do not copy or synthesize current-clock dates. | No asserted preservation of source dates, no extra clock dependency. Later explicit edits use the ordinary date contract. |
| Info Trapped | Omit; do not inherit True or False. | No new trapping/conformance claim. |
| Trailer ID | Generate a new ID for every output, with two equal initial elements; never reuse any input's ID. | New identity even for a one-page extraction or identical repeated operation. Subsequent edits use their own existing ID rules. |
| Document XMP | Omit the entire input packet, including standard dc/pdf/xmp fields, translations, custom/unknown namespaces, xmpMM identifiers and history. Do not parse/repair/merge it for identity purposes. Do not create XMP during assembly. | A complete host-authored replacement is an explicit later edit; no generic namespace filtering is offered. Absent or XML-malformed packets do not themselves add a new assembly refusal, provided the source can otherwise be opened/assembled. |
| PDF/A, PDF/UA, PDF/X identification in document metadata | Exclude; never infer or emit a profile claim from any input. | This policy neither establishes nor validates conformance. Do not strip arbitrary page/resource data under a claim of conformance cleanup. |
| ViewerPreferences UI entries, including DisplayDocTitle/Direction | Exclude the entire dictionary, including unknown/custom keys. | Reader preferences/defaults apply. R2L and title display can change; no claim of presentation equivalence. |
| ViewerPreferences print entries | Exclude PrintScaling, Duplex, PickTrayByPDFSize, PrintPageRange, NumCopies, Enforce and all other print preferences with the dictionary. Do not combine ranges or choose a primary input. | The output has no document print instructions from an input. Callers requiring them need a suitable external editor/print pipeline; no in-library recovery API is promised. No printer/UI result was measured. |
| PageMode, PageLayout | Omit, rather than write explicit overrides. | PDF defaults UseNone/SinglePage apply; no inherited FullScreen or source-specific pane/spread layout. |
| OpenAction destination array, or single GoTo without Next | Deliberately omit valid initial navigation, even when the target is retained. Under W-192's approved future rule, count InitialViewDropped once per input occurrence. Malformed initial navigation refuses. | New document starts at default first-page view; this does not alter retained page links. Bounds/classification/counts belong to W-192/W-197. |
| Other OpenAction, including non-script URI, Launch, Named, remote GoTo and chains | Refuse before output under W-192/W-197's proposed admission rule; do not silently omit, execute or copy. | Active behavior is not a harmless initial-view preference. Existing current behavior omits it; adoption/implementation is still required. Removal/preparation by an authorized external tool is a separate explicit host choice, not a W-201 API. |
| Catalog/selected-page AA, scripts | No new W-201 rule: W-192/W-197 own refusal scope and diagnostics. | Do not add a second inspector or broaden its search boundary. |
| Lang, tags, labels, outlines, attachments, output intents, page/resource metadata | No new W-201 disposition. Their binding contracts and separate proposals keep their scopes. | Lang loss is recorded as an observation, not permission to settle accessibility policy. Identity is not a universal metadata sanitization operation. |

## Exact proposed wording

After owner adoption, add the following subsection after the phase-2 preservation matrix, without changing any unrelated matrix row or requirement status:

> **New-document identity and viewer defaults.** Extraction and concatenation create a new document identity, including extraction from a single input. No input is the primary source of document metadata. The new Info dictionary contains only Producer with the value `jaytwo.Pdf`; Title, Author, Subject, Keywords, Creator, CreationDate, ModDate, Trapped and custom input entries are not copied, and no timestamps are generated by assembly. The output has a newly generated ID whose two elements initially agree. Document XMP is omitted in full, including custom namespaces, translations, profile identifiers and source document identifiers/history; assembly does not create, parse, synchronize or repair a document packet for this purpose. An XML-malformed packet alone does not add an assembly refusal. This does not override source integrity, permission or resource-limit failures. No PDF/A, PDF/UA or PDF/X claim is inferred or carried over from document metadata. ViewerPreferences, including all display and print entries, PageMode and PageLayout are omitted. Their PDF/reader defaults apply; identical display or printing behavior is not promised. A caller may subsequently assign new-document metadata through the ordinary editing API; that separate operation follows its own synchronization, authority and failure contract.

Also replace only the existing phase-2 matrix row for “Catalog OpenAction, AA, ViewerPreferences, Perms, and the JavaScript name tree” **when W-192/W-197's admission wording is adopted**, with these separate rows (coordinate their Perms/signature changes separately):

> ViewerPreferences, PageMode and PageLayout: omitted under the new-document identity rule, including document print instructions.
>
> OpenAction: valid destination navigation is deliberately omitted and counted once per input occurrence as InitialViewDropped. A valid destination array or a single GoTo action without Next is navigation. Malformed navigation and other actions, including non-script URI and Launch actions, refuse before output under the adopted bounded action-inspection rule.
>
> Catalog AA and the JavaScript name tree: governed by the adopted bounded action-inspection/refusal rule. Perms: governed by the adopted signature/rights rule.

This integrated replacement is dependent wording, not a request to adopt all other packets through W-201. Until those rules ship, documentation must continue to state that the current engine omits action open actions. No discrepancy among binding sources was found here; proposals are not shipped guarantees.

For ACCEPTANCE.md, retain the existing A4 identity bullet and add after it:

> Extraction and concatenation use the same identity policy with one input, reversed inputs and repeated occurrences. Info contains only Producer=`jaytwo.Pdf`; no source timestamps or custom keys and no document XMP are copied. Absent XMP remains absent; XML-malformed XMP alone does not refuse an otherwise assemblable source. Each new output has a fresh ID with equal initial elements. ViewerPreferences (including print preferences), PageMode and PageLayout are absent. Post-assembly metadata editing is a separate explicit operation and does not recover input identity automatically. Removing profile markers is not a conformance or sanitization claim.

For docs/behavior.md, add immediately after its assembly description:

> Assembly creates a new document, even for extraction from one source. It writes only `Producer=jaytwo.Pdf` in Info and a fresh document ID. It copies no document XMP, source title/author/dates/custom metadata, viewer preferences (including print instructions), PageMode or PageLayout. PDF and reader defaults apply; source display and printing choices are not preserved. Choose values for the new document through a later metadata edit. Custom XMP requires a deliberately authored packet; copying a source profile identifier does not establish PDF/A, PDF/UA or PDF/X conformance. Initial-view navigation and active open actions follow the separately documented action-admission contract. This rule concerns document identity, not removal of all metadata in page content/resources.

Public assembly method remarks should add this exact sentence without changing their existing failure guarantees:

> Output identity is fresh: Info has only Producer=`jaytwo.Pdf`, document XMP and source viewer/print preferences are omitted, and the document ID is new; assign any desired new-document metadata in a separate editing session.

No AGENTS.md, QUALIFICATION.md or skill wording change is proposed. The existing charter already separates assembly from ordinary edits. W-200 alone owns any proposed changes to setters, malformed-packet editing and atomic synchronization; this item does not remove existing metadata editing APIs or make assembly synthesize XMP.

## Follow-up draft and review boundary

Suggested future item: **Clarify and lock down assembly identity and viewer defaults**. Scope is exact owner-approved wording, public remarks and focused permanent tests of existing behavior. Candidate allowed paths: `ACCEPTANCE.md`, `docs/behavior.md`, `src/jaytwo.Pdf/PdfDocumentEditor.cs` remarks, and a focused assembly identity test file under `test/jaytwo.Pdf.Tests/`; any protected phase-2 text requires approval of the wording above. Confirm paths when the item is created. No ID, activation or queue insertion is authorized by this research.

Verification should exercise one metadata-rich extract, two ordered inputs/reverse order and fresh IDs on a repeated invocation; custom and malformed document XMP, absence, print/display preferences and PageMode/PageLayout must be independently inspected. Verify input bytes unchanged. A targeted public-API recovery case should demonstrate that later Title/Author assignment creates the chosen values rather than reviving input identity. Use focused net10.0 tests/build with StyleCop plus pypdf/qpdf checks. Synthetic markers must be labeled unverified and must not become conformance assertions. Runtime behavior changes are not expected; a discovered defect needs a scoped fix and permanent regression.

Initial-action validation, malformed-action refusal, InitialViewDropped and the URI/Launch refusals belong to W-192/W-197's future implementation. They must retain before-output preflight and existing stream/publication guarantees. W-201 neither implements nor independently budgets that shared work. No new viewer API, profile converter, metadata sanitizer, runtime dependency or copying framework is proposed.

Estimate: 1–2 engineer days for this documentation/regression slice; zero expected production logic changes. No deletion saving should be counted. Host recovery remains extra work: select values, reopen, two assignments plus save for title/author, or additionally author and assign a complete custom packet. Printer/viewer recovery and conformance workflows remain external capabilities. Missing commercial execution, original review attachments, real-corpus loss rates, actual viewer/printer behavior and independent review are explicitly unmeasured. Those gaps prevent claims about those properties; they do not invalidate the named structural observations.
