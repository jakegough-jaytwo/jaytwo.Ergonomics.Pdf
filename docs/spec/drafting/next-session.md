# W-218 next-session handoff: owner closeout decision

**Single current continuation entry point, updated 2026-10-31 after consolidating opening/saving, pages and workflows.** Work in `C:/jake/git/jaytwo.Ergonomics.Pdf`, branch `wip`, under active [W-218](../../../agents/work-items/W-218.md). Do not stage or commit, including through a temporary index. Preserve the large existing worktree and index.

## Current state

* **Adopted (seven):** [shared](../shared.md), [metadata](../metadata.md), [signing](../signing.md), [attachments](../attachments.md) (R2), [images](../images.md), [security](../security.md), [inspection](../inspection.md), each at its 2026-10-31 revision. Do not reopen their decisions or edit them without owner approval.
* **Adopted 2026-10-31 after review corrections (three more):** [opening and saving](../opening-and-saving.md) (W-218-OPENSAVE-2026-10-31-R2), [pages](../pages.md) (W-218-PAGES-2026-10-31-R2) and [workflows](../workflows.md) (W-218-WORKFLOWS-2026-10-31-R2), each with an adoption record. WORKFLOW-RICH-01 to 05 and ASSEMBLY-ATTACH-02 to 11 remain deferred conditional specifications, and no forms rule is adopted. Adoption is of target contracts only.
* **Frozen:** [forms](../forms.md), with its unmet A1 exit in [form-adoption-review.md](form-adoption-review.md). No font-specific size limit exists; older 32 MiB figures are history. Do not elaborate forms or claim forms progress.
* **Closeout proposal:** [closeout-proposal.md](closeout-proposal.md) identifies what is adopted, what remains draft, the owner decisions requested, and recommends carrying forms and the implementation gaps as owner-scheduled follow-up. **W-218 is not done and must not be marked done before the owner decides.**
* All acceptance scenarios remain unexecuted. Adoption is not implementation, test success or release qualification.

## What the next session does

Wait for the owner's decision on closing W-218. Then:

1. Edit the adopted contracts only with the owner's explicit direction; a change to a selected clause also needs it.
2. The three adoptions are recorded (adoption record, status line, spec index and completeness rows); record nothing further unless directed.
3. Treat closing W-218 as a separate decision, not yet made. Do not create follow-up items, implement gaps or declare qualification unless the owner assigns them.

Review answers pasted back by the owner are decisions to apply, not questions to repeat.

## Decision principle and authority

Charter > supported capabilities > adopted specifications. Drafts, research, code, tests and work items are evidence, not product authority. An adopted contract differing from code is an implementation gap; conflicting governing requirements need an owner decision. Where mainstream libraries do not converge the distinction is unlikely to be critical unless correctness, security or data preservation is at stake; favor the simplest predictable behavior and explicit refusal over guessing or silent destructive change. Escalate only governing or adopted edits, scope expansion, architectural exceptions or new rewrite strategies, weakened acceptance or qualification, and material compatibility changes. Q-143's failed-append unusability is settled.

## Verification and handoff result

Run `make agents-check`, local Markdown path and anchor checks over changed files, scoped whitespace and CRLF checks and `git diff --check` for tracked changed paths; check untracked files directly. `make task-check` reports no staged paths, so staged verification is unavailable (do not stage for a green result). No runtime tests, builds or Docker are needed for draft-only changes. Never represent unrun targets or old evidence as passing. Draft files use CRLF.

## Historical records: do not execute their instructions

* [handoff.md](handoff.md), [form-owner-decisions-handoff.md](form-owner-decisions-handoff.md) and [w218-limit-and-subject-review.md](w218-limit-and-subject-review.md) are superseded snapshots.
* [form-adoption-review.md](form-adoption-review.md) retains genuine forms gaps, not work authorization.
* [history.md](history.md) and the dated reconciliation checkpoints preserve provenance. The [spec index](../README.md), completeness rows and subject adoption records supply current status. Do not extend [research](research/).
