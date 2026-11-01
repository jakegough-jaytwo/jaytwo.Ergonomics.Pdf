# Workflow composition, export shortcuts and file output

**Adopted subject contract, revision W-218-WORKFLOWS-2026-10-31-R2, approved by the owner on 2026-10-31 after review corrections.** It joins the owner-selected execution, ownership, overwrite and output-failure clauses and the engineering-supported preparation and flatten proposals (the rich-clear proposals are a deferred conditional specification, not in force) with rules for terminals, execution start, source stability, finalization, cleanup-only failure and cancellation. Authority: [charter](../charter.md) > [supported capabilities](../supported.md) > adopted contracts. The workflow library composes the public engine API and contains no PDF parsing, serialization, encryption, signature construction or raw object manipulation; a workflow can never widen an engine operation or bypass its restrictions. Apply [shared rules](shared.md), [security](security.md) and [opening and saving](opening-and-saving.md). Current code and [behavior](../behavior.md#workflows) are comparison evidence; every difference is an implementation gap. All acceptance targets are unexecuted.

## Purpose and scope authority

Relevant scope: Workflow ergonomics; Image inputs and shortcuts. Workflows give sensible defaults and operation ordering, form-export shortcuts, image-to-PDF and selected-page watermark compositions of the adopted [image](images.md) capability, and file publication. Form-specific rules (eligibility, appearances, fonts, values) belong to the adopted [forms](forms.md) contract and are referenced, not restated; the workflow adds only normalization, ordering and output.

## Clause map

| Area | Clauses |
| --- | --- |
| Terminals and lifecycle | WORKFLOW-TERM-01, WORKFLOW-BEGIN-01, WORKFLOW-LIFE-01, WORKFLOW-DESCRIBE-01 |
| Ordering and normalization | WORKFLOW-ORDER-01/02, WORKFLOW-FILL-01, WORKFLOW-FLATTEN-01, WORKFLOW-PREPARE-01 |
| Resources and sessions | WORKFLOW-OWN-01, WORKFLOW-SESSION-01, WORKFLOW-SOURCE-01 |
| Output | WORKFLOW-STREAM-01/02, WORKFLOW-RESTORE-01, WORKFLOW-FILE-01 to 04, WORKFLOW-ALIAS-01/02, WORKFLOW-CONCURRENCY-01, WORKFLOW-FINAL-01 |
| Failure | WORKFLOW-ERROR-01, WORKFLOW-CLEANUP-01, WORKFLOW-CANCEL-01 |
| Deferred conditional specification (not in force) | WORKFLOW-RICH-01 to 05 |

## Terminals and their output contracts: WORKFLOW-TERM-01

Each terminal delegates its result to an engine or subject contract; the workflow adds destination handling. Direct output (below) applies except where a terminal's own contract states a stronger boundary.

| Terminal | Result contract | Destination | Failure boundary |
| --- | --- | --- | --- |
| Ordinary edit and save | One editor session, one ordinary save ([SAVE-FORMAT-01](opening-and-saving.md#revision-structure-version-and-encryption-save-format-01)); no pending edits is an exact copy ([SAVE-NOOP-01](opening-and-saving.md#no-pending-edit-output-save-noop-01)) | Path (direct file) or empty writable seekable stream | WORKFLOW-FILE-02/03, WORKFLOW-STREAM-02 |
| Explicit source append | Engine append ([SAVE-APPEND-01/02](opening-and-saving.md#failed-ordinary-source-append-save-append-01-q-128)) to the opened writable seekable stream; the input itself is the destination | None separate | The workflow and its document are single-use; the source is repaired externally after a post-write failure; no restoration beyond the engine's attempt |
| Form export | In-memory result; no destination | None | The export operation's own result and refusal behavior, as today; the adopted [forms](forms.md) contract's complete-result rules are referenced, not restated, and this contract adds none; nothing is written |
| Combine, extract | New unencrypted file ([pages](pages.md)) | Path or empty writable stream (seek not required) | Pages' PAGES-FAIL-01 and WORKFLOW-FILE-03, WORKFLOW-STREAM-02 |
| Image-to-PDF | New unencrypted file ([IMAGE-CREATE-01](images.md#limited-creation-and-fit-image-create-01)) | Path or empty writable stream (seek not required) | As combine |
| Selected-page image watermark | Ordinary edit and save with placements as ordinary operations, one session | As ordinary edit | As ordinary edit; a late selected-page failure leaves none stamped (IMAGE-FAIL-01) |
| Sign, document timestamp | [Signing](signing.md): private-disk staged ordinary edits, then one signature revision, then publication of the completed result | Path (temporary-sibling publication) or empty writable seekable stream (final copy) | SIGN-OUTPUT-01's stronger boundary; before the final copy the stream stays empty and the file unchanged |

Stream terminals require an empty, writable destination at position zero, and also seekable where the terminal's engine save needs it to attempt length restoration (ordinary save, append, sign and timestamp); assembly and image-to-PDF streams need not seek (WORKFLOW-STREAM-01). The direct-file boundary below does **not** apply to sign and timestamp terminals, which keep their adopted staging and publication; ordinary direct workflow output has different guarantees, and neither is described as the other. The attachment, image and font inputs a terminal needs are caller-supplied borrowed resources read at execution (WORKFLOW-DESCRIBE-01, WORKFLOW-SOURCE-01).

## Execution start and repetition: WORKFLOW-BEGIN-01

A description is consumed when a terminal is invoked and its **source-independent arguments are accepted**: null and syntactically invalid arguments, a destination stream that lacks the required capability or is not empty at position zero, and a path that cannot be formed all fail as invalid-argument or destination-eligibility errors **without** consuming the description, so the caller may correct the argument and call again. From the point those checks pass, execution has begun and the description is consumed — including a pre-cancelled token, a refusal, a failure and a cancellation (WORKFLOW-LIFE-01). A second or concurrent terminal call on a consumed description fails explicitly without any execution. Consumption is a single atomic claim, so concurrent calls cannot both proceed. Mutating a consumed description fails explicitly. Retry means a new description. This adds no cross-process locking and does not stop host signer or timestamp-authority retries inside one execution.

## Owner-selected workflow execution model

On 2026-10-27, after the measured W-204 ordering and override differences were explained, the owner selected staged ordinary form edits, last-value-wins effective fills, and single-execution descriptions. These are target policies, not adoption of all D-60/W-204 interface, ownership, staging or publication details.

### Ordinary operation ordering: WORKFLOW-ORDER-01

Collect ordinary workflow form fills, apply them as one bulk population call, then perform requested form flattening. Replay other ordinary operations in their declaration order after those form stages. This is a declarative workflow plan rather than literal replay of every method call.

For example, a flatten request followed by a fill of that field must fill it before flattening, so the flattened result contains the supplied value. Reordering form operations is deliberate; do not reorder other ordinary operations to infer a desired final state or suppress intermediate errors. The bulk fill follows the engine's selected per-call atomicity under FORM-BULK-01.

This clause alone does not select overlapping-flatten normalization: WORKFLOW-FLATTEN-01 develops the separately supported Q-66 policy below. Error order is WORKFLOW-ORDER-02, no-op output is SAVE-NOOP-01, and exact revision counts and whole-workflow transaction guarantees are not promised. WORKFLOW-PREPARE-01 develops explicit preparation integration consistent with the shared preparation rule ([shared](shared.md); the adopted forms contract's FORM-PREPARE-01 is referenced for context and not restated or changed here); ordinary form staging is not permission to execute fills before required preparation or to infer consent from an ordinary edit. Both composition clauses are engineering-supported drafts whose runtime verification remains separate.

### Effective duplicate fills: WORKFLOW-FILL-01

For repeated assignments to the same targeted field in a deferred workflow, the last assignment wins. Validate the final effective value rather than applying and validating every discarded assignment. A default may therefore be overridden before execution; an earlier invalid draft does not by itself fail the workflow if it is replaced by a valid final value.

A final invalid value still fails under the chosen field-validation rules. Do not let a discarded draft mutate field values, appearances or readiness flags. After normalization, the adopted forms contract's FORM-NAME-01 to 03 (referenced, not restated here) state exact full-name lookup, refusal of distinct-field ambiguity and terminal named targets. Effective names differing by case/Unicode spelling are not merged through implicit name normalization. Decoding, malformed descriptions/hierarchies, ambiguity reporting, final result reporting and error precedence remain open. Proposed FORM-BULK-02 separately rejects literal direct-engine bulk duplicates; normalization supplies only each effective last workflow assignment. This does not authorize engine duplicate merging or normalization of non-form operations.

### Single-execution descriptions: WORKFLOW-LIFE-01

Once a terminal operation begins, the workflow description cannot be mutated or executed again, including after failure or cancellation. Concurrent or later attempts must not perform another execution. The caller creates a new description for a retry.

This is a lifecycle boundary, not permission to close borrowed resources, lose output guarantees, retry host signing/timestamp services automatically or add cross-process locking. Execution start is WORKFLOW-BEGIN-01; exception types and internal retry policies are not selected; resource ownership is selected in WORKFLOW-OWN-01 below. Argument validation is performed when a terminal is invoked (WORKFLOW-BEGIN-01); whether describing methods also reject invalid arguments immediately is not selected.

### Deferred data reads and captured descriptions: WORKFLOW-DESCRIBE-01

On 2026-10-27, the owner selected deferred data reads with described values/options captured at description time. Do not read PDF or attachment data merely to describe a workflow. Snapshot the supplied scalar values, page selections and option collections when described, so later mutation of those caller collections does not silently change the recorded operation.

This does not snapshot borrowed stream/source contents in advance. Source stability, positions and resource lifetime during the run are WORKFLOW-SOURCE-01, argument validation is WORKFLOW-BEGIN-01, and borrowing/ownership follows WORKFLOW-OWN-01; path resolution and capture depth for complex supplied objects remain open. Do not infer eager whole-document/attachment buffering, an ownership transfer or copying of host signer/provider objects from argument capture.

## Owner-selected resource ownership and ordinary session

On 2026-10-27, after concrete resource examples, repository evidence and the session-model clarification, the owner selected borrowing caller-supplied resources and one ordinary editing session/save.

### Borrowed and owned resources: WORKFLOW-OWN-01

Caller-supplied input, attachment and output streams/random-access sources remain open after success, failure and cancellation. The caller retains responsibility for disposing them. The workflow disposes only handles/resources it creates and owns, including files it opens from paths, its engine objects, adapters and temporary resources. Disposing an internal wrapper must not close a borrowed resource underneath it.

For example, an output stream remains open for the caller to rewind and upload, and an attachment stream remains open for later caller use. When given only a path, the workflow owns its opened file handle and closes it when the run finishes or fails under the eventual cleanup contract.

This does not guarantee restored stream positions, unchanged destination bytes, retry safety, source mutation tolerance, thread safety or an ownership-transfer overload. Flush and success finalization are WORKFLOW-FINAL-01, cleanup failures are WORKFLOW-CLEANUP-01, resource lifetimes during execution are WORKFLOW-SOURCE-01, and destination guarantees are the WORKFLOW-FILE and WORKFLOW-STREAM clauses. The workflow's selected borrowing policy does not dictate the independent engine's source-ownership defaults.

### One ordinary edit session: WORKFLOW-SESSION-01

For an ordinary edit workflow over one existing document, open the input once, apply the accepted staged edits through one document/editor session, then save the ordinary edits once. Do not reopen/save between ordinary stages as an unadvertised implementation shortcut that changes their pending-state or failure contract.

For example, fill a supported field, update Title and rotate a page using one open session and one ordinary save, applying WORKFLOW-ORDER-01. Engine parsing/writing and invariants remain owned by the engine public API. Any eventual explicit preparation integration must respect FORM-PREPARE-01 within its selected session contract.

This choice does not settle no-op revision output, signing/timestamp staging and extra revisions, assembly or image creation, engine-wide editor concurrency, output failure guarantees or performance budgets. It is a workflow execution boundary, not a promise that every terminal or input combination produces exactly one revision.

The no-edit output target is developed separately in engineering-supported SAVE-NOOP-01 in [opening/saving](opening-and-saving.md#no-pending-edit-output-save-noop-01), reviewed alongside this contract: copy exact input bytes to a distinct destination, or perform zero source writes for explicit append. Applicable ordinary file/stream defaults, failed-output retention and source-identity rules remain in force. It is no shortcut around eligibility checks. ENGINE-EDITOR-01 separately selects engine editor exclusivity.

SAVE-PENDING-01 is an engine ordinary separate-output failure guarantee; it does not permit rerunning a workflow, trigger an automatic retry or restore its failed destination (WORKFLOW-RESTORE-01). A no-edit copy can still fail during source/output I/O. Keep unexecuted no-edit/recovery scenarios distinct from the selected one-session/single-execution scenarios.

## Owner-selected output strategy

On 2026-10-27, the owner selected empty/writable/seekable stream output, direct file-destination writes rather than temporary-sibling publication, and caller coordination of competing saves. The direct-file choice deliberately differs from the earlier D-60/D-65 staged publication model; it is a new target policy rather than a claim that current code has changed.

### Output-stream capabilities: WORKFLOW-STREAM-01

Require every caller output stream to be writable, empty and at position zero, and also **seekable only for the terminals whose engine save needs it**: ordinary save, append, sign and timestamp. Combine, extract and image-to-PDF streams need not seek (WORKFLOW-TERM-01, PAGES-OUT-01). Do not promise direct non-seekable HTTP/network output for a seek-requiring terminal or silently add a new non-seekable staging mode; callers can supply an eligible intermediate resource and transfer its completed contents separately. Eligibility is checked before the description is consumed (WORKFLOW-BEGIN-01).

Seekability does not imply rollback: WORKFLOW-STREAM-02 and WORKFLOW-RESTORE-01 leave failed output for the caller. The final position after success is WORKFLOW-FINAL-01 and aliasing is WORKFLOW-ALIAS-01/02; the final position after failure is unspecified. Caller streams remain open under WORKFLOW-OWN-01 even when output fails.

### Direct file output: WORKFLOW-FILE-01

Write file output directly to the destination rather than promising temporary-sibling processing followed by publication of a completed file. Document that a failed save can leave partial or invalid output. If an existing destination is truncated/overwritten under WORKFLOW-FILE-04, its prior contents can be lost; no unchanged-prior-destination or complete-file-publication guarantee is inferred.

Destination opening and failed-file treatment are selected in WORKFLOW-FILE-02/03 below. Create-or-overwrite defaults and fail-if-exists support are selected in WORKFLOW-FILE-04 below; flush, success finalization and durability are WORKFLOW-FINAL-01, and exact error reporting remains open. The final-destination decision does not resolve internal signing/timestamp preparation or authorize rewriting the input file in place. Explicit source append remains distinct from excluded in-place rewriting; input/output identity safeguards follow WORKFLOW-ALIAS-01, with exact mechanisms still open.

Current staged file behavior and D-65 remain unchanged as implementation/history evidence until a separately approved implementation transaction addresses compatibility and the selected target. No existing runtime or acceptance result is relabeled as verifying direct-file output.

### Competing saves: WORKFLOW-CONCURRENCY-01

Callers coordinate independent saves targeting the same output path. Do not promise cross-process transaction isolation, exclusive publication or atomic create-if-absent merely from an overwrite-disabled option. A completed call does not establish that another process has not changed the path or contents.

The earlier D-65 examples concern staged publication; the rebuilt target is direct output. Direct concurrent writes may have different and more destructive effects, including truncation/partial content, so do not transfer D-65's completed-file race outcome as a guarantee here. Supported concurrent same-path use, exact overwrite/create primitives and failure diagnostics remain separate; borrowing and single-execution rules do not provide filesystem coordination.

## Owner-selected direct-file opening and failure treatment

On 2026-10-27, the owner selected applying ordinary edits/available validation before creating or truncating output and leaving failed direct output for the caller without restoration or automatic deletion. After Linux/.NET convention evidence, the owner selected create-or-overwrite with a fail-if-exists option in WORKFLOW-FILE-04.

### Delay destination writing: WORKFLOW-FILE-02

Apply the ordinary pending edits and perform immediately available validation before creating or truncating the destination. A known invalid field or applicable permission refusal must not trigger destination creation/truncation merely because workflow execution began. This preserves the pre-write failure boundary without introducing a temporary-sibling publication contract.

It is not exhaustive preflight: serialization, delayed source reads, cancellation or destination I/O can still fail after output begins and leave a partial file. Phase order is WORKFLOW-ORDER-02, alias safeguards are WORKFLOW-ALIAS-01/02, overwrite/create is WORKFLOW-FILE-04 and signing keeps its own staging; exception types remain unselected. Do not imply that every processing failure occurs before destination bytes or that all source data is buffered first.

### Leave failed direct output: WORKFLOW-FILE-03

After direct writing has begun and the save fails, leave the resulting destination in its documented partial/invalid state for the caller to discard or replace. Do not restore its earlier contents or automatically delete the failed file. Apply required cleanup to workflow-owned handles under WORKFLOW-OWN-01; cleanup does not turn failed output into a successful PDF or make retry safe.

If validation fails before a destination is created, this clause does not require creating a failed file. The rule applies to new and overwritten destinations once writing starts; any prior overwritten contents may already be lost. Stream failure contents and primary-error reporting follow WORKFLOW-STREAM-02 and WORKFLOW-ERROR-01; exact diagnostic shape, durability, external side effects and same-path concurrency remain separate contracts. No previous-destination or completed-publication guarantee is selected.

### Create-or-overwrite default: WORKFLOW-FILE-04

On 2026-10-27, after the Linux/.NET convention comparison, the owner selected create-or-overwrite by default for direct Save(path), with an explicit fail-if-exists option. This is a deliberate change from earlier opt-in-overwrite wording; do not silently keep the old default or claim the current staged implementation has changed.

Create an absent destination or overwrite an existing eligible destination under the default path-save behavior. When the caller selects fail-if-exists, refuse a destination detected as existing rather than intentionally overwriting it. Apply WORKFLOW-FILE-02 before creation/truncation, and WORKFLOW-FILE-03 after writing starts. Prior overwritten contents may be lost if a later write fails.

This choice does not promise atomic create-if-absent against concurrent filesystem changes, override input-alias refusal, authorize excluded in-place source rewriting, or change explicit source append. Caller coordination remains required. Exact option/API spelling, filesystem create modes, symlink/hard-link treatment, identity checks, error types and race/cleanup details remain to be specified.

## Owner-selected stream failure and error reporting

On 2026-10-27, the owner selected Q-53/Q-54. The owner subsequently selected the evidence-based file-identity boundary in WORKFLOW-ALIAS-01 for Q-52.

### Leave failed stream output: WORKFLOW-STREAM-02

If workflow stream output fails after writing starts, leave the resulting partial bytes for the caller. Do not automatically truncate, rewind or restore the output as rollback, including through the engine (WORKFLOW-RESTORE-01). Keep the borrowed stream open under WORKFLOW-OWN-01. The terminal-specific requirements in WORKFLOW-STREAM-01 still apply; seekability a terminal needs is not a restoration promise. The final position after failure is unspecified and validation timing is WORKFLOW-BEGIN-01. This does not require writing bytes when validation fails before output begins. It governs ordinary, combine, extract and image-to-PDF output; sign and timestamp terminals keep SIGN-OUTPUT-01 and append keeps SAVE-APPEND-01/02.

### Who restores what: WORKFLOW-RESTORE-01

Engine best-effort restoration and workflow failed-output retention are separate policies for separate destinations, and neither is described as the other.

| Destination | After a failure once writing began |
| --- | --- |
| Standalone engine save to a separate destination ([SAVE-FAIL-01](opening-and-saving.md#failure-and-resource-contract)) | Best-effort restoration of the starting length; restoration failure reported separately. |
| Source append, by the engine or the workflow's explicit-append terminal ([SAVE-APPEND-01/02](opening-and-saving.md#failed-ordinary-source-append-save-append-01-q-128)) | Best-effort restoration of the source's starting length, then the document is unusable. This is the input itself, not published output. |
| Sign and timestamp terminals | The adopted [SIGN-OUTPUT-01](signing.md#reservations-revisions-and-output-sign-output-01) restoration and staging. |
| Workflow direct file or caller stream from an ordinary save, combine, extract or image-to-PDF | **No restoration.** Bytes written before the failure stay as written; nothing is truncated, rewound or deleted, by the workflow or by the engine it calls. |

To meet the last row, the workflow performs its ordinary save with the engine's destination restoration not performed (an internal mode or equivalent; no public API is selected). A workflow whose engine call truncates its failed destination does not conform. Pending-state rules (SAVE-PENDING-01) are unaffected, and a failed workflow still cannot run again.

### Preserve the primary failure: WORKFLOW-ERROR-01

When an operation/output failure is followed by cleanup failure, preserve the original operation/output failure as primary and report cleanup failures separately. A cleanup exception must not mask that primary failure or imply successful output/restoration. This also applies where another applicable contract requires restoration (engine saves, append, signing: WORKFLOW-RESTORE-01); it does not introduce restoration for workflow direct file or borrowed stream output. Cleanup-only failure is WORKFLOW-CLEANUP-01; exact exception/diagnostic and cancellation representation remain open.

## Owner-selected input/output identity boundary

### Known same-file refusal: WORKFLOW-ALIAS-01

On 2026-10-27, the owner selected early refusal when library-controlled source/destination abstractions can establish that they identify the same file. Refuse before creating, truncating or writing the destination in that case. This is an identity-based check, not a possible-alias heuristic.

For caller-supplied streams and sources, do not refuse merely because they might share an underlying resource. Matching size/timestamp, similar metadata or uncertain wrapper relationships do not establish file identity and must not trigger alias refusal. Do not require a speculative stream-alias detector or promise universal identity detection. This does not relax independently selected stream-capability requirements.

Document that when identity cannot be established, an undetected alias can corrupt the input as well as leave partial output; a later exception does not ensure source preservation. Callers must keep otherwise unidentifiable inputs and outputs distinct. This limitation does not authorize in-place input rewriting as a supported operation. Explicit incremental source append remains separate.

Link identity, abstraction/API shape, platform coverage, races and error types remain open. These implementation details must respect the selected no-suspicion-based-refusal boundary; W-206's size/timestamp heuristic is not adopted.

### Normalized path identity: WORKFLOW-ALIAS-02 (Q-119)

**Engineering-supported draft refinement, not whole-contract adoption or runtime verification.** When library-controlled source and destination paths establish the same file by normalized absolute-path identity, refuse before opening the destination. For example, `report.pdf` and `sub/../report.pdf` establish this identity when they resolve to the same absolute path. Case handling follows filesystem evidence; do not assume a universal case-folding rule. This refines WORKFLOW-ALIAS-01 and does not replace its positive-identity boundary.

This check does not permit source append, override security restrictions or promise unchanged bytes when identity cannot be established. It does not test arbitrary caller-supplied streams for possible aliasing. Link identity, link races, error priority and platform coverage remain open; a path race or undetected alias can still corrupt the source. Literal spelling equality was rejected as too narrow for this draft; the owner has not selected a universal same-file detector.

Unexecuted WORKFLOW-ALIAS-D: use two path spellings resolving to one absolute path and distinct equal-size/equal-time files. Assert early refusal and unchanged input before destructive destination opening for the established alias, and no refusal based only on matching size/time. Exercise case-sensitive and case-insensitive filesystems according to observed identity rather than a hardcoded rule. Link cases await their separate policy. No new filesystem probe or runtime verification was performed.

## Deferred conditional specification: WORKFLOW-RICH-01 to 05

**Not in force.** WORKFLOW-RICH-01 to 05 below are engineering-supported draft clauses for explicit rich clears, the mixed fill and rich-clear phase, supplied-font resources and font-work cancellation. Those operations are defined by the [forms](forms.md) contract, adopted separately as W-219-FORMS-2026-10-31-R2. They are retained so the work need not be re-derived. They join the workflow fill phase only when this contract is amended, with owner approval, to reference the adopted forms contract; forms adoption alone does not un-defer them. Adopting this contract does not adopt them, any rich-clear or supplied-font operation, or any forms rule they cite. The owner approved this deferral on 2026-10-31. WORKFLOW-RICH-03 records the owner's decision that there is no font-specific size limit; the "now proposes" cross-references in this section are drafting history.

### Rich-clear assignment normalization: WORKFLOW-RICH-01 (Q-127)

**Engineering-supported draft clause, not whole-contract adoption or runtime verification.** Normalize ordinary fills and explicit rich clears for the same exact full field name together under last-assignment-wins. Validate and apply only the final effective action. An invalid plain fill overwritten by a later explicit rich clear is discarded; the final rich clear still must satisfy FORM-RICH-02/03's eligibility and blank-appearance requirements. In reverse order, a final plain fill remains subject to FORM-RICH-01's ordinary RichText refusal. This does not change named-target ambiguity, security restrictions, WORKFLOW-ORDER-01's fill-before-flatten phase or the selected one-session/one-save boundary. WORKFLOW-RICH-02 now proposes one atomic mixed engine fill-phase call; its exact public declaration API and resource/cancellation rules remain open. Unexecuted WORKFLOW-RICH-A: exercise both orders, effective validation, retained earlier edits and failure before destructive output opening; no runtime test ran.

### Mixed fill and rich-clear atomicity: WORKFLOW-RICH-02 (Q-132)

**Engineering-supported draft clause, not whole-contract adoption or runtime verification.** Submit final effective ordinary fills and explicit rich clears as one atomic engine operation in the workflow's fill phase. If any action fails after mutation, restore the pending document state to immediately before that phase while retaining earlier successful preparation edits. For example, if `Notes` fails after a valid `Name` fill, `Name` returns to its pre-phase value; earlier rights/XFA preparation remains pending under its separate call boundaries. This extends FORM-BULK-01's selected per-call state guarantee to the mixed action, not to caller-resource side effects, output bytes or a whole-workflow transaction. WORKFLOW-RICH-04 now proposes cancellation observations and state restoration at action/commit and bounded internal-work checkpoints; exact engine/public API, checkpoint frequency, resource limits and failure diagnostics remain open. Unexecuted WORKFLOW-RICH-B: inject a post-mutation rich-clear failure after a valid fill, inspect pending values/widgets/resources and earlier preparation, and assert no destructive output opening under the ordinary workflow boundary. No runtime probe ran.

### Public-call resource accounting: WORKFLOW-RICH-03 (Q-137)

**Engineering-supported mixed-call boundary, reconciled with the owner's 2026-10-31 decision that there is no font-specific size limit (draft direction, not adoption or measured behavior).** All final effective fills and rich clears remain one public engine call under WORKFLOW-RICH-02, including pending-state restoration and retention of earlier edits. Supplied-font resources follow FORM-FONT-05/09/10: **no font-specific size limit** (the caller supplies the stream and bears its cost), memory and work proportional to the font's size and the effective requests, and controlled structural refusal; the library's other documented fixed limits still apply to saved output. Internal action boundaries do not excuse disproportionate repeated processing. No separate live/retained budget, caller-configurable font budget, exact allocation charging or composite counter is required. The earlier 64 MiB proposal and any 32 MiB or 33,554,432-byte threshold are superseded history. Check knowable fixed limits before crossing them and consumed bytes and work during processing. Nonfont limits, cleanup and measured checkpoint latency remain open. Unexecuted WORKFLOW-RICH-C varies font bytes, requested/repeated text and mixed actions, checking proportional growth and late refusal/cancellation restoration with earlier edits retained. See the [integrated form revision](forms.md#integrated-form-review-revision-w-218-forms-a1-2026-10-31).

### Mixed-phase cancellation: WORKFLOW-RICH-04 (Q-142)

**Engineering-supported draft clause, not whole-contract adoption or runtime verification.** Observe cancellation before each final effective action and before committing the mixed fill phase. Add defined checkpoints within long font parsing, glyph work or other bounded processing; action-boundary checks alone do not make such work responsive. WORKFLOW-RICH-05 now identifies byte-scan/glyph-work units and the borrowed-read boundary, with measured thresholds still pending. When cancellation is observed after mutation, restore the entire phase's pending document state while retaining earlier successful preparation edits under WORKFLOW-RICH-02. For example, cancellation after `Name` changes but during `Notes` font work restores `Name` and `Notes` to their pre-phase pending state. This does not roll back caller-resource effects or promise cleanup success; borrowed-stream positions, owned temporary cleanup and diagnostic priority remain separate. Unexecuted WORKFLOW-RICH-D: inject cancellation before actions, during long font/parsing work and before commit; compare pending state and earlier preparation, observing resource effects separately. No cancellation or latency probe ran.

### Bounded internal cancellation checkpoints: WORKFLOW-RICH-05 (Q-146)

**Engineering-supported draft clause, not a measured cancellation guarantee.** Place cancellation checks after bounded chunks of font/table byte scanning and other long parser loops, at bounded composite-glyph component/expanded-point work units, and before potentially blocking borrowed-stream reads. Set exact work-unit thresholds and a representative observation-latency target only after measurement; an action-boundary check does not make a long operation responsive. A check before a borrowed synchronous read cannot interrupt that read once it blocks. If cancellation is actually observed, WORKFLOW-RICH-04/02 govern pending phase-state restoration; this clause does not restore caller-stream position or guarantee resource cleanup. Example: a large font performs a long glyph expansion before the next effective action; checkpoint visits/points allow observation during that work, while a blocked synchronous read remains outside an interruptibility promise. Exact asynchronous-read cancellation support, cleanup diagnostics and measured thresholds remain open. Unexecuted WORKFLOW-RICH-E: inject cancellation mid-scan, mid-composite expansion, before a borrowed read and after a synchronous read blocks; measure observation where promised and separately inspect pending state and caller-stream effects. No cancellation or timing probe ran.

## Engineering-supported composition proposals: Q-65 and Q-66

On 2026-10-28, the user supported both proposals, expressly including Q-66's validation consequence and Q-65's per-call preparation failure sequence. This is engineering support only: owner adoption, field/flatten contract completion and runtime verification remain separate. The endorsement does not establish implementation or turn historical feasibility evidence into an acceptance result.

### Explicit preparation declarations: WORKFLOW-PREPARE-01

Allow explicit ordinary-workflow declarations for eligible Reader-rights removal and hybrid XFA abandonment. Run declared rights removal first, XFA abandonment second, then ordinary staged edits: bulk effective fills, flattening, and other ordinary operations in declaration order. Use one document/editor session without an intermediate Save. Description still defers source reads and captures supplied values/options under WORKFLOW-DESCRIBE-01; no concrete method names or preparation-declaration normalization are selected here.

Preserve explicit consent, general-modification authority, eligibility and every applicable password, certification, field-lock, signature and rights restriction. A fill/flatten request, successful open or hybrid classification is not a preparation declaration. Do not automatically remove rights or XFA merely because an ordinary operation needs preparation. SECURITY-RIGHTS-05 permits signed granting-rights edits only when every other check permits them and refuses signed removal-dependent edits; this preparation proposal supplies no signed-removal mode, XFA-only eligibility expansion or universal hybrid-preparation requirement.

If rights removal fails, stop before XFA abandonment. If abandonment fails after rights removal succeeded, its per-call rollback preserves the earlier successful rights removal as pending while restoring abandonment's pre-call state. Do not roll back the entire preparation sequence or save between its calls. Apply SECURITY-RIGHTS-04 and the shared XFA and preparation rules rather than introducing a combined preparation transaction; the adopted forms contract's FORM-XFA-03 and FORM-PREPARE-01 are referenced for context and are not restated or changed by this clause.

Those pending-state guarantees concern the same engine session. Workflow single execution and owned-resource cleanup still apply on failure; this proposal does not promise public access to a failed workflow's internal document or permit resuming/reusing that workflow. It adds no output rollback, automatic retry, new rewrite strategy or weakening of consent/authority. Exact errors, declaration repetition, eligibility subsets and diagnostic representation remain open.

Unexecuted targets WORKFLOW-PREPARE-A/B/C: on an eventually eligible unsigned hybrid input requiring both explicitly declared preparations, instrument rights-first/XFA-second execution, then ordinary staged edits, one session and no intermediate Save. Fail rights removal and verify abandonment is not invoked; fail abandonment after successful rights removal and inspect the engine session's per-call pending state before workflow cleanup, without requiring a new public intermediate-state API. Exercise missing consent, insufficient authority and ineligible inputs under their eventual rules; do not silently prepare. Verify single execution and selected ownership separately. No production-stage test or signed-removal result is claimed.

### Effective flatten selection: WORKFLOW-FLATTEN-01

Merge named flatten selections and flatten each selected field once after bulk fill. An all-fields request supersedes named selections: validate the effective all-fields operation and skip separate field-name validation for discarded selections. The user expressly accepts that flatten-all plus a misspelled named request succeeds when the all-fields operation is otherwise eligible and completes successfully. Do not turn the discarded name into a missing-field refusal.

For example, requests for Name, Address and Name produce one effective named selection of Name and Address. Adding all-fields instead produces the all-fields operation; a separate MisspelledName request is then discarded without field-name resolution/validation. Name resolution applies only to remaining named targets after normalization (the flatten operation's own rules; the adopted forms contract's FORM-NAME-01 to 03 are referenced, not restated); discarded names are not checked for missing/ambiguous/group targeting. The effective all-fields operation retains its own eligibility rules. Name decoding, malformed structures, ambiguity reporting and source-independent null/syntax argument validation remain open.

Validate the effective operation under the flatten operation's own eligibility, appearance, permission/signature, resource and failure behavior, which this contract neither restates nor changes. All-fields is not a way to flatten an otherwise ineligible field or bypass appearance/authorization checks. Absent-target refusal, name resolution and per-call pending-state atomicity are the flatten operation's own behavior; the adopted forms contract's FORM-TARGET-01, FORM-NAME-01 to 03 and FORM-FLATTEN-01 are referenced, not restated or made clauses of this contract. Decoding, malformed structures, ambiguity reporting, supported types, appearances and error precedence stay open. Fill-before-flatten and non-form declaration order remain unchanged.

Unexecuted targets WORKFLOW-FLATTEN-A/B/C: combine overlapping named selections and verify each eventually identified field is flattened once; combine all-fields with an otherwise misspelled/missing named selection and verify that name does not refuse an otherwise successful eligible all-fields operation. Reverse description order to confirm supersession is not last-declaration-wins. Include an ineligible effective all-fields case and require the eventually selected refusal, despite named supersession. Assess engine atomicity/effective-target absence against proposed FORM-FLATTEN-01 / FORM-TARGET-01 without imposing a named check on discarded selections. Exact fixtures, eligibility/error oracles and runtime results remain unresolved/unexecuted.

## Non-form conflicts, repeated declarations and error order: WORKFLOW-ORDER-02

Only ordinary form fills (last assignment wins) and flatten selections are normalized (WORKFLOW-FILL-01, WORKFLOW-FLATTEN-01). Every other ordinary operation replays in declaration order against the one session with **no normalization or deduplication**, so a later call sees the pending effects of earlier ones: a second rotation or metadata assignment of the same target is applied after the first and wins, and a second add of an attachment with an existing name meets the attachment contract's collision refusal. A refusal anywhere fails the whole run before any destination is opened, and because a failed workflow is single-use its pending edits are never exposed. Declaring the same explicit preparation (rights removal or XFA abandonment) more than once is one declaration; preparations are repeat-safe (SECURITY-RIGHTS-04). The run reports the **first** failed gate in phase order (describe, begin, open, preparation, form stages, ordered operations, pre-destination checks, save); no cross-phase error precedence is promised (SHARED-FAIL-01).

## Source stability and positions: WORKFLOW-SOURCE-01

Input, attachment, image and font streams and sources are borrowed and read **only during execution**. Each must stay open, readable and unmodified until the terminal returns (OPEN-SOURCE-01); the workflow does not copy them in advance or detect arbitrary mutation, and it reports the failures it observes as the operation in progress (a source change makes the engine document unusable, which ends this single-use run). Stream positions are not restored, and a stream read once is not rewound for a second attempt; attachment content is read in a bounded way when saved, and content that changes between reads fails the save. A consumed one-shot stream cannot be reused to retry; the caller supplies a fresh description and fresh resources. Random-access sources also support synchronous reads where the engine document needs them (OPEN-SOURCE-01). The workflow opens the input once (WORKFLOW-SESSION-01) and uses the engine's default borrowing, never transferring ownership of a caller resource to the engine.

## Success finalization: WORKFLOW-FINAL-01

A successful stream terminal flushes the destination, leaves it open and positioned at the end of the written data. A successful file terminal flushes and closes its owned handle before returning. Durability (a forced flush to storage), atomic publication of an ordinary direct file and a restored position are not promised. A flush or close failure is a destination failure, never reported as success. The returned result describes only what the terminal produced; it makes no signature-validity, conformance or accessibility claim. Sign and timestamp terminals publish per SIGN-OUTPUT-01.

## Cleanup-only failure: WORKFLOW-CLEANUP-01

When the operation and output succeeded and only cleanup of a workflow-owned resource fails (closing the output file, disposing the engine document, deleting a temporary file), the call **fails**: it does not swallow the cleanup error or report warning-only success, matching ENGINE-CLEANUP-01. The failure says the destination content was written but could not be confirmed (for a file, the close or flush failure); it asserts neither a valid nor an invalid output. Remaining independent owned resources are still cleaned up and every observed failure is retained (ENGINE-CLEANUP-02); borrowed resources stay open. When a primary failure exists, cleanup failures are reported separately and never replace it (WORKFLOW-ERROR-01). Unperformed dependent cleanup is never reported as success. Temporary-file deletion failures after a signed-terminal publication follow the signing contract and are cleanup failures here.

## Cancellation: WORKFLOW-CANCEL-01

Asynchronous terminals take a cancellation token and are preferred; synchronous terminals are the fallback and use no token. Cancellation is observed before execution begins (a pre-cancelled token still consumes the description once the arguments are accepted, WORKFLOW-BEGIN-01), between phases, at the engine's checkpoints (open, save per [SAVE-CANCEL-01](opening-and-saving.md#cancellation-save-cancel-01)) and before the destination is opened or truncated. Observed cancellation returns no success. Before the destination is opened nothing is written and an existing file or stream is untouched; after writing begins the direct-output rules leave the failed output for the caller (WORKFLOW-FILE-03, WORKFLOW-STREAM-02). Borrowed resources stay open, owned resources are cleaned up (WORKFLOW-CLEANUP-01), and no automatic retry occurs. A blocked synchronous borrowed read or a host signer or timestamp call already in flight need not be interruptible, and a provider's external side effects (for example an issued timestamp request) are not reversed. No latency target or checkpoint count is promised (SHARED-CANCEL-01).

## Form dependencies

After its own normalization, the workflow calls the engine's form operations, whose rules are today's behavior. The adopted [forms](forms.md) contract defines rules for them, listed here as context only, each with its own per-call atomicity: value, clear and export membership (FORM-VALUE-01, FORM-CLEAR-01, FORM-EXPORT-01 to 03); exact choice matching, cardinality, duplicates, ordering and editable text (FORM-CHOICE-01 to 06); button matching and checkbox eligibility (FORM-BUTTON-01 to 06); individual-fill atomicity, ReadOnly and password refusal, MaxLen and script-bearing fills (Q-84 to Q-88); text and field variants (Q-89 to Q-93); local rich-clear overrides, checkbox ambiguity and the explicit appearance-ready path (Q-99 to Q-103); font input and overflow (Q-104 to Q-108); and the engine font and layout proposals (Q-109 to Q-118, with the owner's 2026-10-31 decision of no font-specific size limit). The existing `Fill`/`FillForm` behavior, including a successful `NeedsViewer` result, is preserved. Effective bulk validation and appearance work precede destructive destination opening, with per-call state restoration. No form rule is added or changed here, no font or layout work is done,.

**Forms boundary.** Forms is adopted separately as W-219-FORMS-2026-10-31-R2, and adopting this contract does not adopt any forms eligibility, resource, font, layout, readiness or result rule, any new form operation or any new form behavior. Every FORM-* and Q-84 to Q-118 reference above names a rule in the adopted forms contract; it is context for where a form rule joins a workflow stage, not a requirement of this contract. A workflow's form stages keep the existing `Fill`/`FillForm` compatibility boundary and behave as today's `Fill`, `FillForm`, `Export` and flatten do, and the workflow adds only the normalization, ordering, session and output rules in the clauses above. Whether a workflow may promise ready visual output, and how it treats data-only or known `NeedsViewer` results, is decided with the forms opt-ins, not here.

## Ordinary-run summary

| Phase | Governing clauses |
| --- | --- |
| Describe | WORKFLOW-DESCRIBE-01: capture values, selections and options; read no PDF or attachment data |
| Begin | WORKFLOW-BEGIN-01, WORKFLOW-LIFE-01: argument checks without consumption, then one consumed execution |
| Open and session | WORKFLOW-SESSION-01, WORKFLOW-OWN-01, WORKFLOW-SOURCE-01, ENGINE-OWN-01, ENGINE-EDITOR-01, ENGINE-PENDING-01 |
| Preparation | WORKFLOW-PREPARE-01 (rights, then XFA), explicit only |
| Form stages | One bulk fill of effective assignments, then flatten (WORKFLOW-ORDER-01, FILL-01, FLATTEN-01) |
| Other operations | Declaration order, no normalization (WORKFLOW-ORDER-02) |
| Before the destination | Apply edits and available validation, refuse established same-file identity (WORKFLOW-FILE-02, ALIAS-01/02) |
| Save | One save; create-or-overwrite or fail-if-exists file, or an eligible stream (WORKFLOW-FILE-04, STREAM-01) |
| Success | WORKFLOW-FINAL-01, WORKFLOW-CLEANUP-01 |
| Failure | Failed output left for the caller (WORKFLOW-FILE-03, STREAM-02); primary failure first (WORKFLOW-ERROR-01); cancellation (WORKFLOW-CANCEL-01) |

## Acceptance map

All targets are **unexecuted**. Instrument source and attachment reads, engine opens, editor sessions, saves, destination open/truncate/write calls, positions, flushes and every disposal. Assert category and the promised state, not message text. Historical W-204/W-205/W-206 probes are coverage candidates, not results for this contract.

| ID | Scenario |
| --- | --- |
| WORKFLOW-A1 | WORKFLOW-DESCRIBE-01, WORKFLOW-LIFE-01, WORKFLOW-BEGIN-01: zero PDF or attachment reads while describing; later mutation of caller collections ignored; invalid arguments, an ineligible stream and a null path do not consume the description; a successful, refused, failed, cancelled and pre-cancelled run each consume it; concurrent and later calls never execute twice. |
| WORKFLOW-A2 | WORKFLOW-ORDER-01/02, WORKFLOW-FILL-01: flatten-before-fill equals fill-before-flatten; interleaved non-form operations keep declaration order with no normalization; a discarded invalid draft never fails; a final invalid value fails; repeated preparation is one declaration. |
| WORKFLOW-A3 | WORKFLOW-OWN-01, WORKFLOW-SESSION-01, WORKFLOW-SOURCE-01: borrowed streams stay open on success, refusal, cancellation and failure; path handles are closed; one open, one editor session and one ordinary save; no rewind of a consumed stream; a mutated source fails the run. |
| WORKFLOW-A4 | WORKFLOW-STREAM-01/02, WORKFLOW-RESTORE-01, WORKFLOW-FILE-01 to 04: required stream capabilities per terminal (seekable for ordinary save, append, sign and timestamp; not for combine, extract and image-to-PDF) and position zero; known refusals before any destination change; existing files unchanged and no file created on pre-write refusal; create, overwrite and fail-if-exists; failed ordinary, combine, extract and image-to-PDF output left in place without truncation or deletion, with the engine's destination restoration not performed, while append and sign/timestamp keep their own restoration. |
| WORKFLOW-A5 | WORKFLOW-ALIAS-01/02, WORKFLOW-ALIAS-D: two spellings of one path refuse before destructive opening; equal size/time distinct files and unrelated caller streams are not refused. |
| WORKFLOW-A6 | WORKFLOW-ERROR-01, WORKFLOW-CLEANUP-01: operation failure then cleanup failure keeps the primary; cleanup-only failure after a successful write fails the call and retains every observed failure; borrowed resources untouched. |
| WORKFLOW-A7 | WORKFLOW-CANCEL-01: cancel before begin, between phases, before opening, during the ordinary form stages and mid-save; no success; state and output per phase. (WORKFLOW-RICH-D/E belong to the deferred specification.) |
| WORKFLOW-A8 | WORKFLOW-PREPARE-A/B/C, WORKFLOW-FLATTEN-A/B/C: rights then XFA then ordered edits in one session with no intermediate save; per-call failure boundaries; all-fields supersession. Flatten oracles are today's flatten behavior. (WORKFLOW-RICH-A/B/C belong to the deferred specification.) |
| WORKFLOW-A9 | WORKFLOW-TERM-01: ordinary, append, export, combine, extract, image-to-PDF, watermark, sign and timestamp terminals each reach their delegated contract; sign and timestamp keep staging and publication and are not described as direct output; a no-edit ordinary run copies exact bytes. |
| ORDINARY-RUN-A | Composed review scenario: describe a fill override and flatten, then metadata and rotation; verify effective fill and flatten, declaration order, one save, ownership, a known pre-write refusal and a post-write failure. |

## Implementation gaps

Differences from current code, none started here: the file-output default changes from temporary-sibling publication to direct create-or-overwrite for ordinary terminals (signing keeps its staging); the engine needs an internal save mode that skips its destination restoration so workflow output stays in place on failure (WORKFLOW-RESTORE-01); borrowed-by-default input ownership; fail-if-exists and no-consumption argument checks; the [W-205 final-copy diagnostic-masking finding](../../agents/reference/simplification/W-205/findings.md#observed-diagnostic-masking-problem); cleanup-only failure reporting; explicit preparation declarations and effective flatten selection are proposals with no implementation (the deferred rich-clear and mixed-fill specification has none and is not in force); and checkpoint thresholds, latency and numeric resource limits are unmeasured.

## Adoption record

Approved by the owner on 2026-10-31 as revision W-218-WORKFLOWS-2026-10-31-R2, after review corrections. Adoption covers the owner-selected WORKFLOW-ORDER-01, FILL-01, LIFE-01, DESCRIBE-01, OWN-01, SESSION-01, STREAM-01/02, FILE-01 to 04, CONCURRENCY-01, ERROR-01 and ALIAS-01 clauses; the engineering-supported WORKFLOW-PREPARE-01, WORKFLOW-FLATTEN-01 and WORKFLOW-ALIAS-02 clauses; and the new WORKFLOW-TERM-01, BEGIN-01, ORDER-02, SOURCE-01, RESTORE-01, FINAL-01, CLEANUP-01 and CANCEL-01 rules. The owner approved WORKFLOW-FLATTEN-01's selection normalization using the current flatten operation's eligibility and failure behavior; terminal-specific seekability (combine, extract and image-to-PDF output streams need not seek; ordinary save, append, sign and timestamp streams must); and WORKFLOW-RESTORE-01 (ordinary workflow output leaves failed bytes in place, including when it calls the engine). Approved consequences: argument errors do not consume a description; a cleanup-only failure fails the call; non-form operations replay without normalization. **WORKFLOW-RICH-01 to 05 and all forms rules were unadopted at this adoption (forms was adopted afterward, separately, as W-219-FORMS-2026-10-31-R2; WORKFLOW-RICH-01 to 05 remain deferred until this contract is amended to reference it):** the rich-clear clauses are a deferred conditional specification, the forms draft was context only, PREPARE-01 stands on adopted security and shared rules, and form export keeps today's `Export` behavior. This adopts a target contract only. All acceptance targets remain unexecuted, the implementation gaps above remain open, and no implementation, interoperability or release-qualification claim is made. History: [workflows and publication](drafting/history.md#workflows-and-publication).

## Historical evidence and owner decisions

See the [topic history](drafting/history.md#workflows-and-publication), [workflow ownership examples](drafting/workflow-ownership.md) and the [W-198](../../agents/reference/simplification/W-198/findings.md), [W-199](../../agents/reference/simplification/W-199/findings.md) and [W-204](../../agents/reference/simplification/W-204/findings.md) findings. They support feasibility only, were not rerun and do not adopt any proposal.
