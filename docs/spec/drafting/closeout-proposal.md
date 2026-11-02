# W-218 closeout proposal

**Prepared 2026-10-31; adoption recorded 2026-10-31. Not a closeout:** the owner adopted the three drafts below as their -R2 revisions, but [W-218](../../../agents/work-items/W-218.md) stays active until the owner separately decides to close it. Adoption covers target contracts only; nothing here creates a follow-up item, marks work done, claims implementation or declares release qualification. All acceptance scenarios remain unexecuted, and no runtime, interoperability, rendering or failure-injection check has run.

## Recommendation

1. **Adopt** the three consolidated drafts below, each as one reviewed revision. *Done 2026-10-31 at the -R2 revisions in the table.*
2. **Close W-218 as the specification-rebuild item** once they are adopted, with forms and every implementation gap carried as owner-scheduled follow-up work rather than blocking closure.
3. **Leave forms frozen and unadopted.** Its A1 exit is unmet and no new forms work is needed to close W-218. Forms is the only subject without an adopted contract.

If the owner prefers to hold W-218 open until forms reaches its exit, only the closure decision changes; the adoptions stand.

## What is adopted, what is ready, what is frozen

| Subject | Status | Revision |
| --- | --- | --- |
| [Shared](../shared.md), [metadata](../metadata.md), [signing](../signing.md), [attachments](../attachments.md), [images](../images.md), [security](../security.md), [inspection](../inspection.md) | Adopted 2026-10-31 | W-218-SHARED, -METADATA, -SIGNING, -ATTACHMENTS-R2, -IMAGES, -SECURITY, -INSPECTION (all 2026-10-31) |
| [Opening and saving](../opening-and-saving.md) | Adopted 2026-10-31 | W-218-OPENSAVE-2026-10-31-R2 |
| [Pages](../pages.md) | Adopted 2026-10-31 | W-218-PAGES-2026-10-31-R2 |
| [Workflows](../workflows.md) | Adopted 2026-10-31; WORKFLOW-RICH-01 to 05 deferred | W-218-WORKFLOWS-2026-10-31-R2 |
| [Forms](../forms.md) | Frozen, unadopted; A1 exit unmet ([review](form-adoption-review.md)) | W-218-FORMS-A1-2026-10-31 (identified, not adopted) |

## Decisions requested

The three drafts join clauses you already selected or supported with rules that settle their remaining gaps. Only the choices with user-visible consequence beyond those clauses are listed; every other detail is a routine engineering decision made under the decision principle.

**Opening and saving.**

* A document that observes its source changed becomes unusable (read, edit and save fail; disposal is allowed). The alternative is to fail only the operation in progress and keep the document usable, which guesses about stale offsets.
* Saves never consume pending edits, so a document can be saved to several destinations; a successful source append persists the edits and advances the baseline.
* A separate-destination stream must be empty at position zero.
* Standalone engine saves attempt to restore the destination's starting length and report a restoration failure separately; workflows save without relying on that (they leave failed output, as you selected).

**Pages.**

* Unsupported ordinary annotations and links are omitted and counted, as today. The earlier review recommended refusing unsupported retained-page links; this draft departs from that to preserve existing public behavior. Form widgets, tags and attachments refuse instead.
* Signatures on selected pages are omitted and counted; a signed input is not refused for its signatures alone.
* **Tagged inputs refuse until tag preservation is implemented.** Today tags are silently discarded, so this is visible to current callers. The preserved subset is defined (page-pruned structure with per-occurrence copies) but unimplemented. An untagged derivative remains your decision and is not offered.
* A hybrid-XFA input needs an explicit abandonment consent; XFA-only and layered inputs refuse.
* The output's document information, XMP and identifiers are fresh; the source's are omitted by rule.
* The attachment-preservation mode (ASSEMBLY-ATTACH-02 to 11) is **not offered**; only default refusal is adopted.

**Workflows.**

* An argument error (null, ineligible stream, bad path) does not consume the one execution; everything after it does, including a pre-cancelled token.
* A cleanup-only failure after a successful write fails the call rather than reporting success.
* Combine, extract and image-to-PDF output streams need not seek; ordinary save, append, sign and timestamp streams must (approved 2026-10-31).
* Ordinary workflow output (direct file or caller stream) leaves failed bytes in place even when it calls the engine; the engine's destination restoration is not performed for it. Append and sign/timestamp keep their restoration (WORKFLOW-RESTORE-01, approved 2026-10-31).
* **WORKFLOW-RICH-01 to 05 are deferred** as a conditional specification until the relevant forms contract is adopted (approved 2026-10-31). Adopting workflows adopts no forms rule: preparation stands on adopted security and shared rules, flatten selection on the flatten operation's own behavior, and form export on today's `Export`.
* Non-form operations replay in declaration order without normalization.
* Signing and timestamp terminals keep their adopted staged publication; ordinary terminals write directly.

## Forms and implementation gaps: can they be scheduled separately?

**Yes, recommended.** The adopted contracts and the three drafts state their dependencies on forms by reference and add no forms rule; each draft now carries an explicit forms boundary, and workflows defers its rich-clear and mixed-fill clauses. Existing `Fill`, `FillForm` and flatten behavior is preserved by the charter's compatibility rule and by the contracts, so deferring forms adoption does not weaken it. The cost of deferring: no new form API, explicit data-only or appearance-ready opt-in, additive form inspection or export, font work or flatten change can be implemented until forms is adopted (implementation requires an adopted contract), and the workflow clauses that depend on those rules stay referenced to the frozen draft.

Implementation gaps to carry as follow-up work, **not created as items here**, grouped so they can be scheduled by the owner:

* **Engine opening and saving:** borrow-by-default stream ownership; separate restoration-failure reporting; unusable-after-failed-append and source-changed transitions; destination position check; asynchronous disposal; ownership acceptance ordering.
* **Pages:** tag preservation; `InputN.` naming, with `InputN.CopyM.` for entries that repeat a page and independent repeated copies; bookmark and page-label preservation; hybrid-XFA consent; attachment-refusal coverage; omission reasons.
* **Workflows:** direct-file output with fail-if-exists, no-consumption argument checks, cleanup-only failure, preparation, flatten selection and mixed fill phase, and the W-205 final-copy diagnostic-masking finding.
* **Adopted contracts:** metadata duplicate-touched-definition refusal; attachment read identity, ambiguity and restoration; planned image support; owner-only R=5/6 permission output with independent reopen checks; signing and inspection additive APIs.
* **Evidence:** every subject's acceptance map is unexecuted, and thresholds, latency and numeric defaults are unmeasured. Executing them is verification work; it is not part of drafting and does not need to precede adoption.

## Consistency resolved while consolidating

Engine restoration versus workflow no-restoration is stated in both contracts as separate, deliberate policies. Signing keeps its private-disk staging and stronger publication; ordinary workflow output stays direct. Pages' no-restoration for assembly output matches the workflow rule. No governing-document conflict was found, so none needs an owner ruling; no adopted contract was edited.

## Not done, by design

The three contracts above are adopted as target contracts only; no follow-up item is created, W-218 is not marked done, forms is untouched, behavior, acceptance and qualification records are unchanged, and nothing is staged or committed.

## Remaining decision

Adoption is recorded in each contract, the spec index and the completeness rows. Whether to mark W-218 done is a separate owner decision, not yet made.
