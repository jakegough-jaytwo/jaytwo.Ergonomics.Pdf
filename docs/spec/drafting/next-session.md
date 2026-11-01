# Next-session handoff: all operation contracts adopted; forms adopted (W-219)

**Single current continuation entry point, updated 2026-10-31 after W-219.** Work in `C:/jake/git/jaytwo.Ergonomics.Pdf`, branch `wip`. [W-218](../../../agents/work-items/W-218.md) and [W-219](../../../agents/work-items/W-219.md) are **done**. Do not reopen either or treat any unexecuted acceptance map as passing.

## Current state

* **Adopted (eleven):** [shared](../shared.md), [metadata](../metadata.md), [signing](../signing.md), [attachments](../attachments.md), [images](../images.md), [security](../security.md), [inspection](../inspection.md), [opening and saving](../opening-and-saving.md), [pages](../pages.md), [workflows](../workflows.md) and [forms](../forms.md), each at the revision named in the [spec index](../README.md). Forms is `W-219-FORMS-2026-10-31-R2`, adopted by the owner on 2026-10-31 with D1 to D4 and the engineering decisions in its reading guide. Edit an adopted contract only with the owner's explicit direction.
* **Still deferred:** WORKFLOW-RICH-01 to 05 (conditional on forms; the owner has not directed un-deferring them) and ASSEMBLY-ATTACH-02 to 11.
* **Fonts:** there is no font-specific size limit. Every 32 MiB or 33,554,432-byte figure in the drafting records is history.
* **Adoption is of target contracts only.** All acceptance scenarios remain unexecuted. A difference between a contract and the code is an implementation gap. Adoption is not implementation, test success or release qualification.
* **Known stale statements in governing or adopted documents, not edited without owner approval:** `docs/supported.md` still says forms remains an unadopted draft; `security.md` says its adoption does not adopt "frozen form mutations"; `workflows.md` calls forms-dependent rules referenced, not adopted. Propose corrections to the owner rather than editing them.

## What the next session does

1. Wait for the owner's direction. Apply review answers the owner pastes back; they are decisions, not questions to repeat.
2. Do not create implementation, evidence or follow-up items unless the owner assigns them. The outstanding post-adoption evidence is listed in the forms adoption record.
3. Un-defer WORKFLOW-RICH-01 to 05 only if the owner directs it.

## Decision principle and authority

Charter > supported capabilities > adopted specifications. Drafts, research, code, tests and work items are evidence, not product authority. An adopted contract differing from code is an implementation gap; conflicting governing requirements need an owner decision. Where mainstream libraries do not converge the distinction is unlikely to be critical unless correctness, security or data preservation is at stake; favor the simplest predictable behavior and explicit refusal over guessing or silent destructive change. Escalate only governing or adopted edits, scope expansion, architectural exceptions or new rewrite strategies, weakened acceptance or qualification, and material compatibility changes. Q-143's failed-append unusability is settled.

## Verification

Run `make agents-check`, local Markdown path and anchor checks over changed files and the documents that link into them, scoped whitespace and CRLF checks and `git diff --check`. Missing tools or unrun checks are reported as not run. No runtime tests, builds or Docker are needed for draft-only changes. Never represent unrun targets or old evidence as passing. Drafting files use CRLF.

## Historical records: do not execute their instructions

* [handoff.md](handoff.md), [form-owner-decisions-handoff.md](form-owner-decisions-handoff.md), [w218-limit-and-subject-review.md](w218-limit-and-subject-review.md) and [closeout-proposal.md](closeout-proposal.md) are superseded snapshots.
* [form-adoption-review.md](form-adoption-review.md) keeps the ledger and the A1 exit; its status paragraphs are provenance.
* [history.md](history.md) and the dated reconciliation checkpoints preserve provenance. The [spec index](../README.md) and subject adoption records supply current status. Do not extend [research](research/).
