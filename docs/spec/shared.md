# Shared preservation, input handling, limits and failures

**Adopted subject contract, revision W-218-SHARED-2026-10-31, approved by the owner on 2026-10-31.** It defines vocabulary and applicability rules that every operation contract references. It adds no operation behavior, no universal limit and no rollback guarantee. Authority: [charter](../charter.md) > [supported capabilities](../supported.md) > adopted contracts; the current implementation and [behavior](../behavior.md) are evidence, not requirements.

## What this contract does

Operation contracts ([forms](forms.md), [metadata](metadata.md), [pages](pages.md), [attachments](attachments.md), [signing](signing.md), [images](images.md), [security](security.md), [opening and saving](opening-and-saving.md), [workflows](workflows.md)) state their own results. This document supplies the common terms they use and the rules they may not contradict:

* what "preserved", "omitted" and "refused" mean;
* that eligibility is decided per operation;
* how resource limits are documented and enforced;
* the failure categories and the separate state a failure can leave behind;
* how cancellation is observed.

The charter's hard constraints (managed .NET, no third-party runtime dependencies) and its principles (bounded buffering, preservation of unrelated content, incremental updates, explicit failure) are inherited and not restated per rule.

## Clauses

### SHARED-PRESERVE-01: preservation vocabulary

* **Byte preservation** means identical bytes in the promised region. **Semantic preservation** means equivalent promised values, relationships and behavior even if serialization changes.
* **Omission** is a documented, caller-visible loss of an optional structure. **Refusal** is an explicit failure before the operation produces its result.
* Every operation names what it changes, retains and may omit, and what consent it needs. A successful result never silently promises preservation of a structure its contract did not classify.
* Incremental save preserves its original input bytes as a prefix. Output to another destination, new-file assembly and limited image creation are different output kinds and state their own guarantees. In-place rewriting stays excluded; a new full-document rewrite strategy needs owner approval under the charter and this contract introduces none.
* Where the library chooses between serializations, unrelated content is preserved semantically at minimum; byte identity is promised only where an operation says so.

### SHARED-INPUT-01: eligibility is per operation

* Opening a file, or one successful operation, establishes no other operation's eligibility. Each operation applies its own XFA, PDF/A, authentication, authorization and preservation rules ([security](security.md) owns authentication, permission and write-restriction rules).
* **XFA:** classification (none, hybrid, XFA-only) and the operation's acceptance, refusal or removal rule are stated per operation. Hybrid AcroForm use is supported where an operation says so; XFA packet editing is excluded. Removal of XFA needs the owner-selected [explicit abandonment consent](forms.md#explicit-xfa-abandonment-form-xfa-01); reading never supplies it, and removal is always a documented consequence.
* **PDF/A:** inputs are accepted under normal operation contracts and relevant structures are preserved where an operation says so. Conversion, validation and any guarantee of creating or preserving conformance (including PDF/A-3) are excluded; attachment support and associated-file keys confer no compliance claim.

### SHARED-LIMIT-01: documented limits

* Every operation documents the limits that apply to it: unit, scope (per input, per object, per call, per output), enforcement point and observable outcome. A documented fixed limit is sufficient; callers need not be given controls unless a contract says so (for example the caller-controlled attachment limits).
* Where a contract limits an input by size, actual consumed or decoded extent governs, not an advertised length, and a non-seekable or lying source is measured as it is read.
* Passing a size limit promises nothing about structural eligibility.
* Reaching a limit is a resource-limit failure under SHARED-FAIL-01, not a silent truncation, unless the operation documents a reported omission instead.
* Limits are specific to operations and justified by actual resource risks. There is no universal numeric cap, memory bound, work counter or caller-configurable resource control. Different operations may state different limits and different boundary conventions; this is deliberate and each is documented where it applies. Example: an XMP packet is refused only above 32 MiB ([METADATA-RAW-03](metadata.md#raw-xmp-metadata-raw-01-to-03)). The owner decided on 2026-10-31 that a supplied font program has no font-specific size limit (see [FORM-FONT-05](forms.md#supplied-and-retained-font-byte-budgets-form-font-05)).
* Benchmark workloads, performance budgets and measured results belong in verification and qualification records, not in this contract, and this contract changes no qualification criterion.
* **Known interactions between operations (current behavior, documented in [behavior](../behavior.md)):** a signature appendix larger than 8 MiB is refused and the caller saves incrementally first; an encoded appearance graph over 8 MiB makes page assembly omit that annotation with a reported reason. Operations that can produce such large pending streams (for example an unsubsetted supplied font) are subject to these limits at save, sign and assembly time. Passing an input-size gate does not exempt later output from them. If implementation shows that a supported input is materially lost this way, that is raised as a concrete compatibility case; silent loss is not adopted as an operation guarantee.

### SHARED-FAIL-01: failure categories

Distinguish, and preserve the determinable cause and affected operation of: invalid argument, unsupported input, malformed data, authentication failure, authorization refusal, resource limit, processing or provider failure, observed cancellation, and destination failure.

* No universal ordering of simultaneous errors and no single exception hierarchy is promised; an operation reports the first failed gate it determines.
* A requested complete result is never a partial result presented as complete. Partial output or omission occurs only where the operation's contract permits it and reports it.
* Timing is stated per operation (before any change, during processing, or while writing output). A preflight check is never assumed to find every failure.
* A cleanup failure after a primary failure is reported separately and does not replace the primary cause.
* Acceptance checks assert category and the promised state, not message text.

### SHARED-STATE-01: failure leaves separate states

For each operation the contract names its failure boundary for each of the following, which are independent promises:

1. **Borrowed inputs:** caller streams and sources stay caller-owned; positions are not restored unless stated.
2. **Pending document state:** whether a failed call restores its pre-call pending state while earlier successful edits remain.
3. **Destination bytes, length and position:** what a failed write leaves behind.
4. **Publication:** whether a final file appears, and when.
5. **Cleanup** of resources the library owns.

Unchanged input, unchanged destination and complete-file publication are different guarantees; none implies another. Failure alone never implies an automatic retry, a reusable editor or workflow, reversal of a provider's side effects, source-position rollback or durability. Retained from selected decisions: the failed open document is unusable after a failed append write (Q-143, [selected](drafting/reconciliation.md#owner-selected-initial-decision-q-143)); a failed workflow is single-use; a direct workflow output after writing begins is left for the caller without automatic rollback.

### SHARED-CANCEL-01: cancellation

Cancellation is observed only at the checkpoints an operation documents (before work and at its phase boundaries). A blocked synchronous borrowed read or host call need not be interruptible. When cancellation is observed the operation returns no success and applies its own restoration, output and cleanup rule. No universal latency, checkpoint count or retry promise exists, and elapsed time is not evidence of a contract.

## Selected failure boundaries (applicability map)

Owner-selected boundaries stay in their operation contracts. This table maps them; it is not a transaction contract. Pending document state (reads see it, [ENGINE-PENDING-01](opening-and-saving.md#owner-selected-pending-document-state-engine-pending-01)) and destination contents are separate results.

| Boundary | Selected result (owner clause) |
| --- | --- |
| One engine bulk fill | A failed call restores its pre-call pending state; earlier edits remain (FORM-BULK-01, [forms](forms.md)). |
| One metadata edit or raw replacement | A failed call restores both stores and date state; earlier edits remain (METADATA-SYNC-01, METADATA-RAW-02, [metadata](metadata.md)). |
| Rights removal and XFA abandonment preparations | Each call is atomic; successful rights removal stays pending if later abandonment fails (SECURITY-RIGHTS-04, FORM-XFA-03, FORM-PREPARE-01). |
| Ordinary workflow edits before the destination opens | Ordinary edits and available validation run first; a destination identified as the input is refused before destructive opening (WORKFLOW-FILE-02, WORKFLOW-ALIAS-01, [workflows](workflows.md)). |
| Workflow file or stream output after writing begins | The failed output is left for the caller; owned resources are cleaned up and borrowed ones stay open (WORKFLOW-FILE-03, WORKFLOW-STREAM-02). |
| Primary failure then cleanup failure | The primary failure is preserved and cleanup failure is reported separately (WORKFLOW-ERROR-01; engine ENGINE-ERROR-01 and ENGINE-CLEANUP-01/02 proposals in [opening and saving](opening-and-saving.md)). |
| Opening and transferred input | Transferred inputs follow the Q-58 to Q-64 proposals in [opening and saving](opening-and-saving.md); default borrowing keeps caller disposal. |
| Save and append | Pending-state retention after a failed save, and Q-143's unusable-after-failed-append rule, are in [opening and saving](opening-and-saving.md); they promise no output restoration, retry or reuse. |

Operation-specific form targeting, value, choice, button, text, rich, appearance, font and flatten clauses, and the workflow preparation and flatten normalization clauses, are owned by [forms](forms.md) and [workflows](workflows.md) and are not restated here. Each applies its own pending-state atomicity.

## Relationships

[Security](security.md) owns authentication, permission and write restrictions. [Opening and saving](opening-and-saving.md) owns source lifetime and incremental output. [Workflows](workflows.md) owns composition and file publication. Every other subject contract references this document and states its own outcomes and approved exceptions.

## Acceptance map

All targets are unexecuted; nothing here reports a result. Compare the stated guarantee only, not message text.

| ID | Scenario |
| --- | --- |
| SHARED-A1 | Open a hybrid-XFA and a PDF/A input; perform a permitted read and an ineligible edit. No removal without consent, no conformance claim (SHARED-INPUT-01). |
| SHARED-A2 | For each adopted operation, exercise its own limit with actual extent and a misleading declared length; list any operation without a documented limit as a gap (SHARED-LIMIT-01). |
| SHARED-A3 | Trigger one malformed, unsupported, unauthorized, resource and I/O failure; assert category and the promised state (SHARED-FAIL-01). |
| SHARED-A4 | Fail a pending-state mutation, an append write and a workflow publication separately; compare only their own stated guarantees (SHARED-STATE-01). |
| SHARED-A5 | Cancel before work, at a documented phase boundary and during a blocked borrowed read; assert no success and the operation's restoration (SHARED-CANCEL-01). |
| SHARED-A6 | Sign after producing a pending stream over 8 MiB and assemble a page whose appearance graph exceeds 8 MiB; assert the documented refusal and reported omission (known interactions). |

## Remaining gaps

* Operation contracts that do not yet state limits, failure timing or cancellation checkpoints (see the [completeness review](drafting/reconciliation.md#completeness-review-by-subject)) must do so; that is those subjects' work, not a shared promise.
* Diagnostic representation, cleanup dependency order and post-disposal usability are engine design details in [opening and saving](opening-and-saving.md).
* Measured limit values and checkpoint latency are unmeasured and make no claim here.

## Adoption record

Approved by the owner on 2026-10-31 as written: no universal resource cap, general rollback or retry guarantee, or cancellation latency promise; limits are specific to operations and justified by actual resource risks; existing operation-specific correctness and publication guarantees remain binding. Adoption changes no behavior, acceptance or qualification record and claims no executed result. Decision principle recorded by the owner: where mainstream PDF libraries do not converge, the distinction is unlikely to be critical unless correctness, security or data preservation is at stake; favor the simplest predictable behavior for common business PDFs, prefer explicit refusal over guessing or silent destructive change, and add no mechanism solely for theoretical completeness. History: [shared limits and failure policy](drafting/history.md#shared-limits-and-failure-policy).
