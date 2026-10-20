---
name: pdf-task-workflow
description: Plan and execute one repository work item from scope through handoff. Use when selecting or updating agents/work-items, coordinating a cross-area change, or assessing release readiness.
---

# PDF task workflow

Use one agent for the ordered task lifecycle. Do not simulate Product Manager, Technical Lead, implementer, and QA handoffs.

## Sources of truth

`AGENTS.md` sets the order of authority: `AGENTS.md`, then `docs/spec/`, `ACCEPTANCE.md`, `README.md`, `QUALIFICATION.md`, and the records under `agents/`. Apply its rule for target versus shipped behavior: a spec requirement marked New or Change is target behavior, not a disagreement, until the item that implements it changes it to Exists in the same commit that updates `ACCEPTANCE.md` and `README.md`.

When sources disagree under that rule, stop the affected work and report the disagreement to the owner. Do not choose between them yourself. The order says which source governs. It does not license resolving a conflict silently.

A decision in the charter, in an adopted spec, or accepted in `agents/decisions/` is closed. Do not reopen it in code or in a work item. If evidence says it is wrong, record the evidence and raise it with the owner. Repository records never grant permission the charter withholds.

Read only the sources relevant to the requested work item. When a workflow record contradicts the repository, trust the repository and correct the record.

## Writing a work item

Read `ACCEPTANCE.md` first. If the behavior is already a criterion, the item is about evidence or a regression, not new work. Create one stable `agents/work-items/W-NN.md` file from `agents/templates/work-item.md`; never move it when status changes. Every executable item has:

- **Kind:** defect, missing evidence, charter gap, performance risk, or enhancement.
- **Problem:** what is wrong or missing, with the evidence.
- **Why now:** the user need or risk it addresses.
- **Scope and exclusions.** Exclusions matter as much as scope.
- **Acceptance criteria:** observable and testable, citing the `ACCEPTANCE.md` rows added or changed.
- **Evidence required:** the evidence level, and which independent tool if any.
- **Dependencies:** other tasks or open decisions.

Keep the work set small. Do not add items for capabilities the charter excludes. Never weaken a criterion to close an item. If a criterion is wrong, change it as its own work item with its own justification.

Use `proposed`, `ready`, `active`, `waiting`, `done`, and `cancelled`. At most one item is active. `waiting` requires a concrete blocker, a resumption test, committed tracked work, and a `Safe checkpoint:` Notes entry naming the verification that makes unrelated work safe. Done and cancelled records are frozen except for dated Notes-only appendages; reopening preserves prior evidence.

## Workflow

1. Run `make agents-check`, then establish or read the work item. If it is already defined, verify rather than restate it. Use `agents/state/current.md` only for ordered near-term IDs and the approved tail-plan pointer.
2. Select the one domain skill that owns most of the change: `pdf-core-change`, `pdf-acroform-change`, or `pdf-signing-change`. Load another only when the change actually crosses that boundary.
3. Set the item to `active` and record repository-relative exact paths or directory prefixes ending in `/` in `allowed_changes`. Do not use globs. If work needs another path, stop at the boundary and rescope or split the item before continuing.
4. Inspect the smallest relevant code and tests, then implement the smallest in-charter change. No speculative rewrites, abstractions, or caches. Do not refactor a file only because it is large. Do not "fix" code that follows a documented exception in `AGENTS.md`, such as the scoped member-order suppressions.
5. Use `pdf-change-verification` after behavior, tests, build configuration, or public documentation changes.
6. Update affected public documentation and the work item's Notes or Closeout once observed results are known. Put only reusable cross-task evidence in `agents/evidence/`.
7. Stage the complete transaction and run `make agents-check` and `make task-check`. Every agent-created commit starts with `<item-id>:`. Write at most one separate report, `agents/reports/<yyyy-mm-dd>-<item-id>.md`, only when qualification evidence or the user requires it.

Do not maintain a second board or backlog, create separate role reports, or require the same agent to approve its own work under several personas. Run `make agents-status` for the derived overview. Distinguish implementation evidence, test evidence, independent-tool evidence, and genuinely independent review.

## Done

A work item is done when:

1. Its acceptance criteria are met at the evidence level the task required.
2. Every target framework builds with StyleCop warnings as errors.
3. Every confirmed defect has a permanent regression test.
4. Failure behavior matches the documented guarantee.
5. `README.md` and `ACCEPTANCE.md` describe the new behavior, and no criterion was weakened.
6. Remaining limitations are written down.

Give every confirmed defect a disposition: fix, documented limitation, or release blocker.

## Reports

Use these sections and leave out any that are empty: task; verified facts, with file, test, and command references; assumptions; defects confirmed; evidence missing; changes made, with the files affected; verification run, with commands, tools, versions, and results; decisions needed. Keep it short. A report that restates the charter is too long.

## Decisions and escalation

Use a separate `agents/decisions/D-NN.md` record when the durable choice, rationale, consequences, or authority will govern more than one work item. Research needed to make a decision is a work item; the resulting choice is a decision. Decision records explain or propose changes but never silently override `AGENTS.md`, `README.md`, `ACCEPTANCE.md`, `QUALIFICATION.md`, or an applicable skill.

Make routine in-charter decisions directly: implementation approaches, small fixes, limited refactoring that serves the task, test infrastructure, tool installs, fixtures, and performance changes backed by a measurement. Do not ask the owner to approve routine work the charter already covers.

Ask the owner before:

- changing the charter, including scope, non-goals, stated limits, or a closed decision;
- adding any runtime package;
- a breaking public API change, or a change to what the public field, page, or geometry types return;
- changing a signature, encryption, or certification guarantee, the signer contract, or the supported algorithms;
- widening the appearance subset or changing the reported coordinate system;
- a major redesign of the parser or the incremental-writing model;
- editing `AGENTS.md`, `QUALIFICATION.md`, or `.agents/skills/`;
- a release, or effort that looks out of proportion to the requirement.

Keep work single-agent by default. When an evidence standard requires genuine independence, record the need for a human or fresh session instead of treating a persona change as independence. Spawn a subagent only when the user explicitly requests delegation.
