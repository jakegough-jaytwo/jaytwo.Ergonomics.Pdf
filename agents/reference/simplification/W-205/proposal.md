# O 09 rollback disposition: exact draft, pending owner approval

Retain current PDF behavior and the W-185/W-186 boundaries. No pending-edit-lifetime change, transaction framework or generalized diagnostic API is proposed. This packet requests no runtime edit under W-205 and makes no queue insertion.

## Proposed clarification wording

For a future approved workflow failure-documentation update, append this paragraph to the signed-stream failure guidance in `docs/behavior.md`, with matching acceptance and public XML wording:

> During the final copy of a completed signed or timestamped staging file to a caller's stream, the workflow preserves the original copy, flush or cancellation exception if restoring the destination fails. Restoration attempts to set the destination back to its starting length, then restore its position; either step can fail. A restoration failure does not replace the original exception. This final workflow copy does not attach the engine's signing-specific restoration diagnostic. Pending engine edits, restored output length, and safe physical retry are separate facts. Verify or repair a failed destination before reusing it, and start a new workflow for another run.

The existing empty-destination requirement remains. The paragraph describes final copying, not every later result getter or disposal path. Do not claim new structured workflow diagnostics, guaranteed physical recovery, exact starting-position recovery for direct-engine signing, valid arbitrary-provider output, or automatic retry.

For retained lifetime/staging, no protected-file wording change is needed: current source, tests and contract already state the required behavior. If the owner wants explicit universal final-copy exception identity/type/token language in spec F20, propose the following **additional sentence**, subject to approval of the exact text:

> A failure while restoring the caller's destination after final copying, flushing or cancellation shall not replace the original operation exception, whose identity, type and cancellation token are preserved.

Do not insert either draft as shipped behavior until the correction and permanent regression land. AGENTS.md, QUALIFICATION.md and existing signing diagnostics need no proposed change.

## Focused follow-up implementation draft (unallocated and unqueued)

Title: Preserve workflow final-copy failure when restoration throws unexpectedly.

Problem: At baseline `d17c35f`, `WorkflowFiles.CopyAsync` catches only IOException, NotSupportedException and ObjectDisposedException from recovery. W-205's `workflow-copy-unexpected-restoration` writes 17 bytes then throws the original IOException; SetLength throws InvalidOperationException and replaces it. This loses the primary failure; output is still partial.

Scope: replace the restoration exception-type filter in the existing final-copy catch with preservation of the original operation exception for every restoration exception. Retain two ordered physical recovery steps, one-run builders, borrowed ownership, private staging, file publication, host redaction and the existing signing-specific diagnostics boundary. No new helper hierarchy, public reporting type or engine change.

Proposed allowed paths: `src/jaytwo.Ergonomics.Pdf/Internal/WorkflowFiles.cs`, `test/jaytwo.Ergonomics.Pdf.Tests/WorkflowSigningTests.cs` (or one narrowly named fault-stream test file), `docs/behavior.md`, `ACCEPTANCE.md`, the applicable `PdfWorkflowBuilder.Signing.cs` XML comments, its future work-item record and completion bookkeeping. Spec F20 only if the owner adopts the exact sentence above. No implementation is authorized by this draft.

Acceptance: a permanent regression recreates the 17-byte copy failure and unexpected SetLength failure, asserting original exception identity/type, partial output, borrowed resources open and one provider call. One distinct seek-failure case verifies length restored but uncertain position. Cover sync/async through the shared helper without a Cartesian fault matrix. Include original OperationCanceledException/token preservation if it uses a distinct mechanism. Existing expected-restoration cases and pre-copy signer/authority failures retain behavior. File publication rules and engine diagnostics stay unchanged.

Verification: focused workflow final-copy/ownership/failure tests on Windows net10.0, plus net48 for shared exception behavior if the approved item requires target evidence. Existing engine rollback tests ensure no accidental cross-boundary change only if engine code changes (excluded from this scope). No signature interoperability sweep is needed when signature bytes are unchanged; no full matrix or Docker checkpoint unless separately named. Permanent regression must fail against the pre-correction implementation, then pass with the correction; invoke test-value-evidence only if revision-bound evidence is explicitly requested by the final item. Run repository planning/task checks and graph update. Estimated effort: 0.5-1 engineering day, based on one filtered catch and existing stream fault scaffolding.

This is a diagnostic correction/strengthening, not a PDF capability removal or broad refactor. Count removal of one recovery exception filter, addition of regression/docs, and no deleted responsibility. Owner approval of exact wording, final scope and queue insertion is Gate 2 under D-66. W-211 should carry this packet into reconciliation; no separate ticket for already-supported pending edits or already-completed W-185/W-186 consolidation.
