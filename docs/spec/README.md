# Operation specifications

These documents are being rebuilt from the [charter](../charter.md) and [supported capabilities](../supported.md). The charter sets purpose, principles and architecture; supported.md owns durable capability commitments and non-goals; adopted operation contracts make those commitments precise.

**Shared rules, metadata, signing, attachments, images, security, inspection, opening and saving, pages, workflows and forms are adopted.** Their acceptance maps remain unexecuted targets; adoption does not claim implementation, interoperability or release qualification. Forms is adopted as W-219-FORMS-2026-10-31-R2, and the workflows rich-clear clauses (WORKFLOW-RICH-01 to 05) stay a deferred conditional specification (see the [closeout proposal](drafting/closeout-proposal.md)). Current code and tests are evidence, not automatic target requirements.

## Read by subject

| Document | Contract responsibility | Status |
| --- | --- | --- |
| [Shared rules](shared.md) | Preservation, XFA/PDF/A input handling, operational limits, cancellation, refusal and failure vocabulary. | Adopted 2026-10-31 (W-218-SHARED-2026-10-31) |
| [Opening and saving](opening-and-saving.md) | Sync/async stream and random-access opening, source/editor lifetime, source stability, repeated saves, incremental output, destination failure and disposal. | Adopted 2026-10-31 (W-218-OPENSAVE-2026-10-31-R2); owner-selected lifetime and Q-143 clauses joined with engineering-supported Q-58 to Q-64 and new source-stability, repeated-save, destination and disposal rules; implementation gaps and target evidence remain |
| [Inspection](inspection.md) | Page count/geometries/rotation, form fields/widgets, signature claims and revisions. | Adopted 2026-10-31 (W-218-INSPECTION-2026-10-31); additive complete results, legacy coordinates/APIs retained; target evidence unexecuted |
| [Forms](forms.md) | Population, export, caller-font appearances and flattening; operation-specific XFA handling. | Adopted 2026-10-31 (W-219-FORMS-2026-10-31-R2); owner-approved D1 to D4 and recorded engineering decisions; existing `Fill`/`FillForm` behavior including successful `NeedsViewer` preserved; explicit data-only and appearance-ready opt-ins; internal-identity flatten-all; no font-specific size limit; acceptance scenarios and post-adoption evidence unexecuted; implementation gaps remain |
| [Metadata](metadata.md) | Document information, authorship/custom properties and XMP. | Adopted 2026-10-31 (W-218-METADATA-2026-10-31) |
| [Attachments](attachments.md) | Document-level embedded files, streaming/extraction, caller limits and filename helpers. | Adopted 2026-10-31 (W-218-ATTACHMENTS-2026-10-31-R2); catalog-AF-only compatibility corrected and acceptance clarified after review; target evidence unexecuted |
| [Pages](pages.md) | 90-degree rotation, ordered/repeated extraction and concatenation, preservation/omission/refusal. | Adopted 2026-10-31 (W-218-PAGES-2026-10-31-R2); selected form/bookmark/label/tag/attachment-refusal clauses joined with rules for selection, new-file output, security consent and every remaining preservation row; attachment-preservation mode deferred; amended 2026-11-01 (W-225) with the owner-approved omitted-annotation tag exception and the partial-selection attribute refusal; amended 2026-11-01 (W-230) so tag preservation covers complete documents and contiguous ranges only, with repeated, reordered, non-contiguous and mixed tagged/untagged assembly refused; implementation gaps and target evidence remain |
| [Security](security.md) | Password authentication, permission reads/changes, DocMDP, FieldMDP and Reader rights. | Adopted 2026-10-31 (W-218-SECURITY-2026-10-31) after accessibility-bit correction; initial owner-only unsigned R=5/6 permission output, R=2-4 deferred without rewrite authority; target evidence unexecuted |
| [Signing](signing.md) | Detached CMS/PAdES B-B/B-T, host-controlled keys, signature and document timestamps. | Adopted 2026-10-31 (W-218-SIGNING-2026-10-31); acceptance clarifies final-copy restoration failure; implementation gaps and target evidence remain |
| [Images](images.md) | JPEG/PNG inputs, image placement, limited blank-page creation and image-to-PDF. | Adopted 2026-10-31 (W-218-IMAGES-2026-10-31); acceptance clarifies late watermark failure; implementation planned and target evidence unexecuted |
| [Workflows](workflows.md) | Composition/defaults/order, export and image shortcuts, file publication and workflow failures. | Adopted 2026-10-31 (W-218-WORKFLOWS-2026-10-31-R2); selected clauses and engineering-supported proposals joined with terminal, execution-start, source-stability, finalization, cleanup-only and cancellation rules; forms-dependent rules referenced, not adopted; WORKFLOW-RICH-01 to 05 deferred as a conditional specification until forms is adopted; terminal-specific seekability and WORKFLOW-RESTORE-01 approved; implementation gaps and target evidence remain |

Inspection is grouped separately from mutation so a read contract need not imply permission or eligibility to modify. Shared subjects define common terms; each operation still owns its exact inputs, outcomes, preservation, authorization, limits and failures. Grouping related operations in a file does not force identical support or output rules.

## Build the contracts from scope

Start with the charter, then supported.md. Establish the intended caller result and durable boundaries before choosing detailed mechanics. Draft shared vocabulary and source-lifetime rules alongside the operation contracts that need them; do not wait for a universal contract to settle every operation.

Use the subject outlines to work through inputs, results, preservation, authorization, limits, refusal and failure behavior. Record unresolved choices explicitly. Develop acceptance scenarios from those proposed outcomes, then compare existing behavior and tests to assess feasibility, compatibility, reusable evidence and implementation gaps. Existing implementation and retired specifications provide evidence rather than a default answer.

Keep existing public compatibility commitments visible during review. Rebuilding the target does not itself authorize changing them, removing proven functionality or narrowing scope. The charter and supported.md remain unchanged by this organization.

## Drafting and adoption

- [Drafting guide and contract outline](drafting/guide.md): document ownership, derivation, readable contract structure, source labels, requirement/acceptance traceability and owner adoption.
- [Coverage and transfer inventory](drafting/inventory.md): all 20 drafting entries with subject destinations, current-behavior comparison pointers, and details to retain in supported.md until replacement contracts are adopted.

- [Historical evidence and decision queue](drafting/history.md): earlier owner choices, implementation records, unadopted research and the questions raised by this rebuild.
- [Decision reconciliation and contract completion review](drafting/reconciliation.md): answered versus residual choices, cross-contract decision order and completeness gaps for every subject. This is the live follow-up map; historical questions already answered are not reopened.
- [Competitor-informed expectations](drafting/competitor-expectations.md): sourced context for the assembly and bulk-fill choices, separate from adopted requirements.

- [Workflow ownership and session examples](drafting/workflow-ownership.md): selected caller-resource/session policies, related research and separately unresolved resource/output details.
- [Engine lifetime comparison and clarification](drafting/engine-lifetime.md): primary .NET/Linux/PDF/EF/FFmpeg ownership precedents, the subsequently selected borrowing policy, and unsaved memory versus saved PDF bytes.

Inventory IDs identify drafting coverage; they are not ticket numbers or normative requirement IDs. Implementation tickets, delivery status and dependencies stay in planning records.

Normative precedence is charter > supported capabilities > adopted specifications. Draft outlines have no independent normative authority. Owner adoption must identify the reviewed contract and resolved decisions; organizing files does not adopt their unanswered details. A difference from an adopted contract is an implementation gap, while conflicting governing requirements require an owner decision.

[Behavior](../behavior.md) records current implementation; [acceptance](../acceptance.md) maps requirements to verification and enforced outcomes; [qualification](../qualification.md) records release evidence. Retired specifications under [docs/_retired_/spec](../_retired_/spec/) are historical input, not current requirements. Scope, architecture, acceptance weakening and qualification changes require their existing owner approvals.

For the continuation instructions and expected response format, read the [current session handoff](drafting/next-session.md).
