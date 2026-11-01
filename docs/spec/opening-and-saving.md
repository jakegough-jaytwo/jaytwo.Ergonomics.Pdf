# Opening, lifetime and incremental saving

**Adopted subject contract, revision W-218-OPENSAVE-2026-10-31-R2, approved by the owner on 2026-10-31 after review corrections.** It joins the owner-selected clauses (ENGINE-OWN-01, ENGINE-PENDING-01, ENGINE-EDITOR-01, SAVE-DEFER-01, Q-143) and the engineering-supported proposals (Q-58 to Q-64, SAVE-APPEND-01) with the previously open rules for source stability, repeated saves, destination failure and disposal. Authority: [charter](../charter.md) > [supported capabilities](../supported.md) > adopted contracts. Apply [shared rules](shared.md) and [security](security.md). Current code and [behavior](../behavior.md) are comparison evidence; every difference below is an implementation gap, not a governance conflict. All acceptance targets are unexecuted.

## Purpose and scope authority

Relevant scope: Document opening; Incremental saves; Resource and failure contracts. The product opens documents synchronously and asynchronously from streams and stable random-access sources with explicit ownership and authentication outcomes, and saves supported edits incrementally with the original bytes preserved as a prefix. Appending to a source is supported; rewriting an input in place stays excluded. New-file assembly ([pages](pages.md)) and limited image creation ([images](images.md)) have their own output rules. [Workflows](workflows.md) own file publication.

## Clause map

| Area | Clauses | Origin |
| --- | --- | --- |
| Opening | OPEN-SOURCE-01, OPEN-STRUCT-01, OPEN-AUTH-01 | New consolidating rules; behavior retained |
| Ownership and lifetime | ENGINE-OWN-01/02/03, ENGINE-ERROR-01, ENGINE-CLEANUP-01/02, OPEN-DISPOSE-01, OPEN-READ-01, ENGINE-EDITOR-01, ENGINE-PENDING-01 | Owner-selected and engineering-supported; OPEN-* new |
| Source stability and repeated saves | SAVE-SOURCE-01, SAVE-REPEAT-01/02 | New consolidating rules |
| Output | SAVE-DEFER-01, SAVE-PREFIX-01, SAVE-FORMAT-01, SAVE-DEST-01, SAVE-NOOP-01, SAVE-ALIAS-01 | Owner-selected, supported and new |
| Failure | SAVE-FAIL-01, SAVE-PENDING-01, SAVE-APPEND-01/02/03 (Q-143), SAVE-CANCEL-01 | Owner-selected, supported and new |

## Opening

### Accepted sources and stability: OPEN-SOURCE-01

Accept a readable **seekable** stream or a random-access source that presents one stable snapshot read at absolute offsets, synchronously or asynchronously. Non-seekable and non-readable streams refuse before any read. Offsets are file offsets, including a preamble before `%PDF-`. Open reads the header and cross-reference chain only; later reads load the objects an operation touches, so the file is never buffered whole. A random-access source may return fewer bytes than requested; zero bytes is its end. The document reads from the source's start regardless of the stream's current position; position is not part of the document and is not restored.

**Stability is the caller's obligation.** The source must stay open, readable and unmodified for the document's lifetime, except for the document's own explicit appends. The library does not detect arbitrary mutation. It reports what it observes: a stream whose length changes (other than by the document's own appends), a source that reports its own change, or a short read inside required data is a source-changed or malformed-data failure of the operation in progress. The library never reads one source from two places at once; sharing a source among documents is permitted only when its implementation says that is safe.

### Supported structure: OPEN-STRUCT-01

Open `%PDF-1.0` through `%PDF-1.7` files and read PDF 2.0 cross-reference and metadata structures that appear in them. Read classic tables, cross-reference streams, hybrid sections and object streams, and the closed list of documented writer quirks in [behavior](../behavior.md#support-matrix). There is no repair, rebuild or scanning of a damaged file. Malformed or unsupported structure refuses explicitly (SHARED-FAIL-01); a bounded structural limit that is reached is a resource-limit failure. Opening establishes no operation's eligibility (SHARED-INPUT-01); an operation applies its own XFA, PDF/A, authorization and preservation rules. Existing fixed structural limits remain as documented in [behavior](../behavior.md#support-matrix); this contract adds no universal cap.

### Authentication outcome: OPEN-AUTH-01

Authentication outcomes (unencrypted, user, owner, failure, unsupported scheme) are defined by [SECURITY-AUTH-01](security.md#authentication-and-permission-inspection-security-auth-01). Opening an encrypted file with wrong or missing credentials is an authentication failure, never a permission denial, and follows the ordinary failed-open ownership rules below. Passwords are not retained in results or diagnostics.

## Ownership, disposal and concurrency

### Engine lifetime decisions submitted for review

| Question | Selected result | Alternative not selected |
| --- | --- | --- |
| Q-55: source ownership at engine open | ENGINE-OWN-01: borrow supplied streams and sources by default; explicit optional transfer; internally opened path handles are owned. | Mandatory ownership arguments and close-by-default. Changes the current stream default recorded in W-132, so compatibility review precedes implementation. |
| Q-56: pending-state owner and visibility | ENGINE-PENDING-01: document reads show accepted pending edits, which survive editor disposal; no implicit save. | Editor-disposal rollback. |
| Q-57: editor concurrency | ENGINE-EDITOR-01: one active editor per document; callers coordinate. | Concurrent editors. |

See [primary-source comparisons](drafting/engine-lifetime.md) for rationale.

### Owner-selected engine source ownership: ENGINE-OWN-01

Borrow caller-supplied streams and stable random-access sources by default. The caller retains disposal. Disposing the document, or a failed or cancelled default-borrowing open, must not close the supplied resource, and an internal wrapper must never close what it wraps. An explicit optional ownership-transfer choice makes a successful document own and dispose its input with itself. Handles the library opens from paths are internally owned and cleaned up when their use ends or opening fails. This governs engine document opening only; font, attachment and raw-metadata inputs keep their own entry-point rules, and workflow borrowing is WORKFLOW-OWN-01. Borrowing does not restore stream position, permit concurrent mutation or early disposal of a still-needed source, or guarantee retry safety. The default deliberately differs from the historical close-by-default stream overload.

### Engineering-supported opening/save proposals: Q-58 to Q-60

Q-58 to Q-60 are developed below as ENGINE-OWN-02 (failed transferred open), SAVE-NOOP-01 and SAVE-PENDING-01. They are engineering-supported draft clauses; owner adoption of this contract adopts them.

**ENGINE-OWN-02.** Once the engine accepts an explicitly transferred input, it owns cleanup after a failed or cancelled open; a parse or authentication failure does not return disposal to the caller merely because no document is returned. Default-borrowed inputs stay open.

### Q-61: proposed ownership acceptance point

**ENGINE-OWN-03.** Complete source-independent argument validation first, with no query of source capabilities, length, position or data (for a stream, no `CanRead`, `CanSeek`, `Position` or `Length`; naming them selects no required capability). Accept explicit ownership immediately before the first phase that observes cancellation or accesses the source. After acceptance the engine retains cleanup responsibility through cancellation, capability failure and parse or authentication failure. Before acceptance the caller disposes. Default borrowing never transfers responsibility. Owning cleanup is a duty to attempt it, not a promise that disposal succeeds. No callback or public ownership-reporting API is selected.

| Situation | Result |
| --- | --- |
| Invalid source-independent arguments, even with cancellation requested | Refuse before acceptance, with zero source queries; the caller keeps disposal. |
| Valid arguments, explicit transfer, token already cancelled | Ownership accepted before cancellation is observed; the engine cleans up. |
| Capability or length access, parsing or authentication fails | Ownership already accepted; the engine cleans up. |
| Default borrowing | No transfer; every failure leaves the caller's resource open. |
| Disposal fails after accepted ownership | Responsibility is not reassigned; no successful-disposal claim; ENGINE-ERROR-01 and ENGINE-CLEANUP-01/02 apply. |

### Opening primary failure: ENGINE-ERROR-01

When opening fails or is cancelled and owned-resource cleanup also fails, the original failure or cancellation stays primary and cleanup failures are reported separately. Cleanup failure never masks the original or turns a failed open into success. Representation follows the established pattern for engine rollback diagnostics (a documented accessor on the primary exception, as for signing rollback failures): cleanup failures are programmatically retrievable from the thrown primary, never replace it, and no message text is asserted. Competing opening errors keep SHARED-FAIL-01's no-universal-ordering rule.

### Engineering-supported cleanup proposals: Q-63 and Q-64

**ENGINE-CLEANUP-01.** When cleanup is the only failure (for example, disposing a document whose owned input fails to dispose after otherwise successful use), report it as a failure; do not swallow it or report warning-only cleanup. One failure is thrown as itself; several are thrown together so none is lost. **ENGINE-CLEANUP-02.** After one cleanup action fails, continue other independently cleanable owned resources, keep borrowed inputs open, never automatically retry the failed action, and retain every observed failure. Cleanup that depends on the failed action succeeding need not run, and an unperformed dependent action is never reported as successful disposal. No dependency graph or cleanup framework is selected; an engine-opening failure that already exists stays primary.

### Disposal and use after disposal: OPEN-DISPOSE-01

`Dispose` ends the document's use, disposes only owned resources, and never saves or discards in a way that changes any source or destination. Repeated `Dispose` is a no-op and does not retry a failed cleanup. After disposal every read, edit and save fails with an explicit object-disposed failure. A document with an active editor may be disposed; the editor becomes unusable the same way and its pending edits are lost with the document. Disposing an editor never disposes its document. Add an asynchronous disposal path that uses an owned resource's asynchronous disposal when it offers one; the synchronous path is the documented fallback. A failed-append document (SAVE-APPEND-02) and a source-changed document (SAVE-SOURCE-01) remain disposable.

### Reads and thread safety: OPEN-READ-01

Neither the document nor its editor is thread-safe, and the library introduces no locking. The caller must not call into one document concurrently, including reading during a save. Concurrent reads of independent documents are permitted when their sources are independent. Reads through the document never persist anything or change another document.

### Owner-selected single editor: ENGINE-EDITOR-01

One active editor per document, caller-coordinated. Requesting a second editor while one is active fails explicitly, without creating a second editor or saving. After the first editor is disposed, a later editor is available and retains pending edits (ENGINE-PENDING-01). The exception type and disposal-race behavior are not selected; a race is a caller coordination failure.

### Owner-selected pending document state: ENGINE-PENDING-01

Accepted edits belong to the open document's pending state. Reads through the document reflect them before save, according to the relevant inspection, metadata and form contract. Disposing an editor releases editing access without saving or discarding them; a later editor on the same document retains them. Example: assign Title = New, dispose the editor, inspect Info Title through the document: New; the original bytes still contain the prior title until an explicit save. This is one document session's state, not synchronization with a separately opened document.

Failed calls restore the state immediately before that call while retaining earlier successful edits, per each operation's selected atomicity (FORM-BULK-01, METADATA-SYNC-01, METADATA-RAW-02, preparation calls, image calls). Editor disposal never rolls back earlier edits. METADATA-DATE-02/04's explicit date value or absence continues across editors. Live versus snapshot collections already returned to callers follow their own inspection contract. No explicit discard API is selected. State after saves is SAVE-REPEAT-01/02.

## Source stability and repeated saves

### Source change: SAVE-SOURCE-01

Once a document observes that its source changed or that a required read is inconsistent with the structure it parsed (a changed stream length, a source-reported change, a read that no longer matches), that document is **unusable** for further reads, edits and saves, which fail explicitly with the source-changed category; disposal remains allowed. Pending edits are not exposed or retried through it. The caller may open a new document from the source. Rationale: the cross-reference map and any pending edits refer to bytes that are no longer the bytes parsed, so continuing would guess. This rule is a read-failure rule and does not depend on whether a write had begun; a failed append after writing began has its own rule (SAVE-APPEND-02). The library is not a general change detector and does not digest or compare source content.

### Repeated saves to separate destinations: SAVE-REPEAT-01

A successful or failed save to a separate destination never consumes the document's pending state. The document's baseline stays the opened source; its pending edits remain pending and visible. Saving again, to a fresh eligible destination, produces a semantically equivalent result with the same promised edits. Byte identity between the two outputs is not promised (identifiers and dates are not frozen by this contract beyond METADATA-DATE rules). Each save re-applies source, authorization, limit and destination rules.

### Source append advances the baseline: SAVE-REPEAT-02

After a **successful** explicit source append, the appended revision is persisted: the document's baseline advances to the new end of the source, the saved edits are no longer pending, and the document remains usable. Later edits and appends add further revisions; the document's own appends do not count as source change (SAVE-SOURCE-01). A separate-destination save after an append includes the appended revision in its prefix. After a **failed** append, SAVE-APPEND-02 governs. A no-op append (SAVE-NOOP-01) writes nothing and advances nothing.

## Output

### Owner-selected ordinary write boundary: SAVE-DEFER-01

Nothing is written before an explicit save. For ordinary existing-document editing, mutations remain pending: an ordinary mutation does not write PDF bytes to the source or a destination, and disposing an editor does not save. Reading the source, or moving a borrowed stream's position, is not persistence. An explicit save can still fail under its output contract. This clause chooses no scratch-storage policy and does not cover signing and timestamp preparation, which have their own phases in [signing](signing.md). WORKFLOW-FILE-02 places workflow edits and available validation before destination creation.

### Scope-derived incremental result: SAVE-PREFIX-01

For a successful incremental save of supported edits, the complete original document bytes are an exact prefix of the output; for explicit source append the existing bytes stay the prefix and the update follows. Output to a distinct destination contains the original prefix plus the update. A prefix alone establishes no permission to make the edit, signature validity, trust or PDF/A conformance. Source append is a deliberate supported operation, separate from excluded in-place rewriting and from the workflow same-file refusal.

### Revision structure, version and encryption: SAVE-FORMAT-01

One save adds one revision. The cross-reference format follows the section at the file's `startxref`: a classic table after a classic or hybrid section, a cross-reference stream after a cross-reference stream; no new object streams are written, and edited objects previously in object streams are rewritten as normal objects. The original header is kept. When a write emits a key defined at a later PDF version than the file's effective version, the same revision raises catalog `/Version` only as far as needed (never beyond what [behavior](../behavior.md#support-matrix) records for the key) and never lowers it. For an encrypted file, new strings and streams are encrypted with the file's existing handler and ciphers, `/Encrypt` and `/ID` are kept, and encryption is neither created nor removed; permission setting follows [SECURITY-SET-01](security.md#permission-setting-output-security-set-01). Revision count for a save with no pending edits is zero (SAVE-NOOP-01). Other signing and timestamp revisions follow [signing](signing.md).

### Destination eligibility and finalization: SAVE-DEST-01

For an ordinary save (new-file assembly and image creation have their own destination rules in [pages](pages.md) and [images](images.md)), a separate-destination stream must be writable, seekable, **empty (length zero) and at position zero**; the engine refuses any other destination before writing. This refusal also rejects most non-identical aliases of a non-empty source. Source append requires the opened stream itself to be writable and seekable; a random-access snapshot source offers separate-destination output only. Copy into the destination uses bounded buffering and never buffers the whole file. On success the destination is flushed, left open and positioned at the end of the written data. Durability (forced flush to storage), atomic publication and restored position are not promised. The engine borrows the destination under ENGINE-OWN-01's spirit: it never closes a caller's destination.

### No-pending-edit output: SAVE-NOOP-01

An otherwise eligible ordinary save with no pending edits to a distinct destination produces an exact byte copy of the input and adds no revision. An explicit source append with no pending edits performs zero source writes. Eligibility, authentication, alias, fail-if-exists and resource rules still apply; a byte copy does not bypass them. An equal-value setter is not a no-op where the setter changes dates. Signing, timestamps, assembly and image creation are never no-ops. Workflow file creation or overwrite may occur to deliver the copy under its own defaults, and ordinary same-file output stays refused when identity is established.

### Aliasing: SAVE-ALIAS-01

The engine's alias rules are identity-based and minimal: the opened stream object as the destination means source append; any other destination must satisfy SAVE-DEST-01. The library does not search for possible aliases among caller streams and does not refuse on matching size or timestamps. Path-based identity belongs to workflows (WORKFLOW-ALIAS-01/02). An undetected alias can corrupt the input as well as leave partial output; callers keep unidentifiable inputs and outputs distinct.

## Failure, restoration and cancellation

### Failure and resource contract

**SAVE-FAIL-01 — failure phases for an ordinary save.**

| Phase | Examples | Destination | Pending state | Document |
| --- | --- | --- | --- | --- |
| Before the first destination write | Invalid argument, ineligible destination, permission, certification or lock refusal, known size-rule refusal, pre-write cancellation | Untouched | Preserved | Usable |
| After writing begins, separate destination | Malformed object, changed source length, serialization, copy or destination I/O failure, cancellation | Starting length restoration attempted; contents otherwise unspecified | Preserved if the source remains usable (SAVE-PENDING-01) | Usable unless the source changed (SAVE-SOURCE-01) |
| After writing begins, source append | Same causes | Starting length restoration attempted | Not observable | Unusable (SAVE-APPEND-02) |
| Finalization (flush) | Flush failure | Same as after writing begins | As the applicable row | As the applicable row |

Validate eligibility, authorization and edit validity before the first destination write wherever those checks are available; this is not exhaustive preflight, and delayed source reads can still fail after writing begins. Restoration is a best-effort attempt to set the destination to its starting length (empty, or the opened file's length for append); do not claim unchanged bytes, restored position, atomic publication or retry safety from it. The primary failure stays primary and any restoration failure is reported separately and never suppressed (the same accessor pattern as ENGINE-ERROR-01). No automatic retry occurs.

**Workflow interaction.** A standalone engine save attempts restoration. A workflow-published ordinary, combine, extract or image-to-PDF file or stream follows WORKFLOW-FILE-03 and WORKFLOW-STREAM-02, which leave failed output for the caller without restoration or deletion; the workflow therefore saves with the engine's destination restoration not performed (WORKFLOW-RESTORE-01). The workflow's explicit-append terminal keeps SAVE-APPEND-01/02 restoration of the source, and sign and timestamp terminals keep their own. The policies are deliberate and owner-selected; each contract states its own.

Signing and timestamp output keep their private-disk staging and stronger publication boundary in [signing](signing.md) and use SAVE-APPEND-01/02 only for the ordinary append steps they share.

### Pending state after failed separate-output save: SAVE-PENDING-01

After failure or cancellation of an ordinary save to a separate destination, preserve all pre-save pending edits, including explicit metadata date or absence state, when the source remains usable. Reads and a later editor continue to expose them. The caller may request a save to a fresh eligible destination; it must again satisfy source, authorization, limits and destination rules, so this is not a success guarantee. Source usability is a precondition. This promises no destination rollback, deletion, rewind, publication or retry safety, and a failed workflow still cannot run again.

### Failed ordinary source append: SAVE-APPEND-01 (Q-128)

After an ordinary explicit source append begins writing and fails, including observed cancellation after writing begins, attempt to restore the source to its starting length. The append failure stays primary; a restoration failure is reported separately and a failed truncation or seek is never suppressed. Restoration is best effort; a partial suffix or other damage can remain, and a successful truncation does not prove the original prefix intact. The current writer suppresses truncation errors, so separate reporting is a required change, not verified behavior.

### Failed append writing disables the open document: SAVE-APPEND-02 (Q-133, Q-143)

**Owner-selected on 2026-10-30.** After an ordinary source append begins writing and fails, mark that open document unusable for further reads, edits and saves whether restoration reports success, failure or indeterminate. Later attempts fail explicitly; disposal remains allowed. The caller may inspect or recover the source externally and open a new document. Do not infer prefix integrity or retry safety from successful truncation, a restored length or a fresh parse.

### Pending edits after established append restoration: SAVE-APPEND-03 (Q-138)

Deferred; not part of this contract. Q-143 supersedes the proposal to retain pending edits and allow a new append on the same document. A future recovery mode would need its own owner decision, an exact original-prefix proof and failure-injection evidence.

### Owner-selected initial failed-append boundary (Q-143)

SAVE-APPEND-02's unconditional unusability, together with SAVE-APPEND-01's best-effort restoration and separate reporting, is the initial choice. Stable-source and whole-prefix comparison or digest proof routes remain historical research for a possible future mode; neither is required.

### Cancellation: SAVE-CANCEL-01

Per SHARED-CANCEL-01, observe cancellation before any destination write, between copy blocks, between serialization phases, and before finalization. Asynchronous variants await source reads, destination writes and flushes with the token. A synchronous borrowed read or write that is already blocked need not be interruptible, and the synchronous appendix serialization of an otherwise asynchronous save is not interruptible. Observed cancellation returns no success and applies SAVE-FAIL-01 by phase: before writing, nothing changes and pending state is preserved; after writing begins, the failure rules above apply. A cancelled open returns no document (ENGINE-OWN-03). No latency target exists.

### Owner direction: asynchronous operations

Async is highly preferred; blocking operations are the documented fallback only where no async option exists. Every open and save has an asynchronous form with cancellation. Known synchronous fallbacks, stated rather than hidden: after opening, document properties and the synchronous methods read the source synchronously, so a random-access source must also support synchronous reads; and the appendix of an asynchronous save is serialized synchronously. This records a design direction and these two limits; it selects no exact API.

## Resource and limit summary

Every limit below already exists and is documented in [behavior](../behavior.md#support-matrix); this contract adds none.

| Limit | Scope and enforcement | Outcome |
| --- | --- | --- |
| Structural and decompression caps | Per object or stream while reading | Resource-limit or malformed-data failure; never silently truncated |
| Cross-reference offset ceiling | Update offsets, checked before the destination grows | Refusal before writing |
| Copy buffering | Bounded block copy for separate destinations | Not a failure condition; memory stays bounded |
| Signature appendix | Owned by [signing](signing.md) | Documented there |

Actual consumed extent governs, never an advertised stream length (SHARED-LIMIT-01).

## Related contracts

**Forms boundary.** [Forms](forms.md) is frozen and unadopted, and adopting this contract adopts no forms rule. A save neither validates appearance paint nor persists a readiness marker; what readiness means, including what a reopened document reports, belongs to forms. The FORM-BULK-01 reference in ENGINE-PENDING-01 names today's all-or-nothing bulk fill and adds no form behavior. Pending-state restoration for a failed form call does not change save, append or workflow destination failure. Apply [shared rules](shared.md) and [security](security.md); [workflows](workflows.md) own file publication.

## Acceptance map

All targets are **unexecuted**. Inspect outputs with an independent reader and instrument every source and destination write, length, position and disposal separately. Assert category and promised state, not message text.

| ID | Scenario |
| --- | --- |
| OPENSAVE-A1 | Open sync and async from a stream and a random-access source; non-seekable and unreadable stream refuse before any read; encrypted open with wrong, missing, user and owner credentials (OPEN-SOURCE-01, OPEN-AUTH-01). |
| OPENSAVE-A2 | ENGINE-OWN-A to I: default borrowing and transfer on success, rejection, cancellation and parse failure; zero source queries before acceptance; disposal failure keeps responsibility with the engine and reports the original failure as primary. |
| OPENSAVE-A3 | ENGINE-ERROR-A/B/C and ENGINE-CLEANUP-A to D: primary-then-cleanup failure, cleanup-only failure, independent continuation, dependent-action skip, borrowed input kept open. |
| OPENSAVE-A4 | OPEN-DISPOSE-01: dispose twice, use after dispose, dispose with an active editor, dispose a failed-append and a source-changed document. |
| OPENSAVE-A5 | ENGINE-EDITOR-A, ENGINE-PENDING-A/B/C, SAVE-DEFER-A/B: second editor refused; pending edits survive editor disposal; no persistence before save; failed bulk and metadata calls restore pre-call state. |
| OPENSAVE-A6 | SAVE-SOURCE-01: truncate, extend and replace a stream between operations and make a source report change; require an explicit source-changed failure, no persisted edit and later refusal with disposal allowed. |
| OPENSAVE-A7 | SAVE-REPEAT-01/02: save twice to two destinations and compare edits; append, edit again and append; separate save after an append has the appended revision in its prefix; a failed save leaves pending state intact. |
| OPENSAVE-A8 | SAVE-PREFIX-A, SAVE-FORMAT-01: byte prefix after separate save and after append; xref format, version and encryption continuity checked with a second reader. |
| OPENSAVE-A9 | SAVE-NOOP-A/B: exact copy with no added revision; zero write calls for no-edit append; eligibility checks still run. |
| OPENSAVE-A10 | SAVE-DEST-01, SAVE-ALIAS-01: non-empty, position-nonzero, read-only and non-seekable destinations refuse with the destination untouched; success flushes, leaves the destination open and positioned at the end. |
| OPENSAVE-A11 | SAVE-FAIL-01 (SAVE-OUTPUT-REVIEW-A/B): fail a distinct destination during copy, serialization and finalization; compare length, bytes and position separately; a destination that refuses truncation reports a separate restoration failure; pending edits retained and no automatic retry (SAVE-PENDING-A/B/C). |
| OPENSAVE-A12 | SAVE-APPEND-A/B and REVIEW-A: fail an append with successful and failing restoration, including a parseable prefix corruption; require unusability, separate reporting and allowed disposal; reopen as a new document. |
| OPENSAVE-A13 | SAVE-CANCEL-01: cancel before writing, mid-copy, before finalization and during a blocked synchronous read; require no success and the phase's promised state. |

Historical scenario IDs SAVE-OUTPUT-REVIEW-A/B and SAVE-APPEND-C/D (deferred with SAVE-APPEND-03) are retained in [history](drafting/history.md#opening-and-saving). No failure injection, runtime result or interoperability check has run.

## Implementation gaps

Differences from current code, to be carried as implementation work after adoption (none is started here): the stream ownership default changes from close-by-default to borrow; engine save truncation errors are suppressed rather than reported (SAVE-APPEND-01, SAVE-FAIL-01); the failed-append document is not yet marked unusable (SAVE-APPEND-02); the source-changed unusability and the nonzero-position destination refusal (SAVE-SOURCE-01, SAVE-DEST-01) need confirmation against current checks; the document needs an asynchronous disposal path (OPEN-DISPOSE-01); the engine needs an internal save mode that skips its destination restoration for workflow output (SAVE-FAIL-01, WORKFLOW-RESTORE-01); and ownership acceptance ordering (ENGINE-OWN-03) is not verified.

## Adoption record

Approved by the owner on 2026-10-31 as revision W-218-OPENSAVE-2026-10-31-R2, after review corrections. Adoption covers the owner-selected ENGINE-OWN-01, ENGINE-PENDING-01, ENGINE-EDITOR-01, SAVE-DEFER-01 and Q-143 clauses; the engineering-supported Q-58 to Q-64 and SAVE-APPEND-01 clauses; and the new OPEN-SOURCE/STRUCT/DISPOSE/READ, SAVE-SOURCE-01, SAVE-REPEAT-01/02, SAVE-FORMAT-01, SAVE-DEST-01, SAVE-ALIAS-01 and SAVE-CANCEL-01 rules, with SAVE-APPEND-03 left deferred. Approved consequences beyond the selected policies: a source change makes the document unusable (SAVE-SOURCE-01); saves do not consume pending edits and append advances the baseline (SAVE-REPEAT); an ordinary-save destination must be empty at position zero (SAVE-DEST-01); and the workflow-output restoration boundary in WORKFLOW-RESTORE-01. The forms boundary in Related contracts stands: adoption adopts no forms rule. This adopts a target contract only. All acceptance targets remain unexecuted, the implementation gaps above remain open, and no implementation, interoperability or release-qualification claim is made. History: [opening and saving](drafting/history.md#opening-and-saving).

## Historical evidence

See the [topic history](drafting/history.md#opening-and-saving) and [engine lifetime comparison](drafting/engine-lifetime.md). Earlier decisions retain their recorded context; implementation and completed research are not silently adopted.
