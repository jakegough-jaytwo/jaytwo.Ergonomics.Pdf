# W-151 proposed owner disposition

Status: approved by the owner, 2026-10-26. The owner approved the exact AGENTS.md replacement and original-exception reporting contract, directed preservation of the restore-to-original-length-and-seek convention and ordinary-save behavior, and required net10.0/net48 regressions. Implementation is recorded in W-151. Seed-value standards questions remain subject to the primary-source verification requirement below.

The owner supplied `w-151-expert-review-response.md`. It confirms the signing-guarantee conflict and recommends preserving the original exception with structured restoration diagnostics. The expert did not inspect the repository. Its standards interpretation remains review input, not verified implementation evidence or owner approval.

## Approval requested

Approve the exact AGENTS.md replacement below and the original-exception reporting contract below. This resolves the existing conflict for W-151; it does not authorize new algorithms, profiles, certificate validation, or signer capabilities.

### Exact protected wording

In AGENTS.md's PKCS#11/signing bullet, replace only:

> A failed sign or timestamp never extends the output, and exceptions from host code are not passed through with their details.

with:

> A sign or document timestamp that fails before destination writing leaves the destination bytes unchanged. If destination copying, appending, or finalization fails after writing begins, the engine attempts to restore the destination's original length and then seek to that length. Restoration is best effort: destination failure may prevent truncation or seeking, and partial output may remain if truncation fails. The original operation failure is preserved, and a restoration failure is observable with length restoration distinguished from seeking. Restoring pending engine edits does not establish that the physical destination is safe for retry. Exceptions from host code are not passed through with their details.

This intentionally describes the existing restore-to-end convention. The expert's proposed promise to restore the starting position would add behavior beyond the present helper, which calls `Seek(originalLength, Begin)`.

Phase-1 section 2 and QUALIFICATION.md G9 already allow destination failure to prevent restoration. W19 specifically describes signer, timestamp, size, and validation failures, which occur before output writing. No changes to those protected texts are proposed here. F20 already documents best-effort restoration during workflow final copying. No qualification evidence is refreshed by this planning transaction.

### Failure-reporting contract

- Preserve the original exception object, type, stack, and cancellation classification. Use `throw;` on the original failure path.
- When signing rollback fails, attach one documented structured outcome under a stable public `Exception.Data` key. Provide a public signing-specific diagnostic type/access mechanism; its exact names are routine API design within W-151.
- Report the original destination length, whether truncation succeeded, whether seeking was attempted and succeeded, and the restoration exception. If truncation fails, seeking need not be attempted. Do not claim position or byte restoration merely because length restoration succeeded.
- Do not replace the original exception with an aggregate or wrapper. Structured `Data` is programmatically observable; default exception logging may omit it. Document that limitation and show consumers how to inspect it.
- Cover write and finalization failures. Preserve sanitized host exceptions; do not recover their hidden details. Do not claim an unsuccessfully restored same-stream destination is safe to retry just because pending edits were restored.
- Keep ordinary incremental-save rollback behavior unchanged. The workflow final-copy guarantee remains its existing best-effort contract; this item does not silently add a new workflow rollback API.

### Implementation following approval

The existing W-151 scope authorizes enforcing supported mandatory seed requirements and refusing unsupported applicable mandatory requirements before provider invocation. Implementation will verify the expert's standards claims against primary sources, resolve actual field ownership, and test on net10.0 and net48. Optional or irrelevant seed entries remain non-blocking. The expert's uncertain PDF 2.0 signature-policy semantics, timestamp applicability, and custom-provider limitations must not be treated as settled standards merely because the owner approves the output-failure contract. If primary-source review cannot resolve one, prepare a focused self-contained expert question before adopting that behavior.

Update README.md, ACCEPTANCE.md, and public API documentation to describe the implemented seed checks and restoration reporting in the implementation commit. Preserve all existing successful-rollback criteria; add failed-truncation and failed-seek outcomes. Scope runtime and test paths in W-151 before editing them.

## Suggested owner reply

> Approve the exact AGENTS.md replacement and original-exception reporting contract in w-151-owner-disposition.md. Continue W-151 within its existing scope, and bring back any unresolved standards question in a self-contained expert prompt.
