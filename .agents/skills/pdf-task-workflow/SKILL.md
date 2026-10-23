---
name: pdf-task-workflow
description: Plan and execute one repository work item from scope through handoff. Use when selecting or updating agents/work-items, coordinating a cross-area change, or assessing release readiness.
---

# PDF task workflow

`AGENTS.md` sets the order of authority, the rule for target versus shipped behavior, and what changes only with the owner's approval. A decision in the charter, an adopted spec, or `agents/decisions/` is closed: if evidence says it is wrong, record the evidence and raise it with the owner. When a workflow record contradicts the repository, trust the repository and correct the record.

## Work items

Read `ACCEPTANCE.md` first. Behavior that is already a criterion makes the item evidence or a regression, not new work. Create `agents/work-items/W-NN.md` from `agents/templates/work-item.md` with a kind, the problem and its evidence, why now, scope and exclusions, acceptance criteria citing the `ACCEPTANCE.md` rows they add or change, the evidence level and any independent tool, and dependencies. Do not add items for excluded capabilities. Never weaken a criterion to close an item; a wrong criterion is its own item.

Statuses are `proposed`, `ready`, `active`, `waiting`, `done`, and `cancelled`. `waiting` needs a concrete blocker, a resumption test, committed tracked work, and a `Safe checkpoint:` note naming the verification that makes unrelated work safe. Done and cancelled records are frozen except for dated Notes appendages.

## Steps

1. Run `make agents-check`. Read the item, or write it; verify it rather than restate it.
2. Load the one domain skill that owns most of the change. Load another only when the change crosses that boundary.
3. Set the item `active`, with exact paths or directory prefixes ending in `/` in `allowed_changes` and no globs. If the work needs another path, stop and rescope or split the item.
4. Make the smallest in-charter change. No speculative rewrites, abstractions, or caches, and no refactoring a file only because it is large.
5. Verify with `pdf-change-verification` after behavior, test, build, or public-documentation changes.
6. Record observed results in the item's Notes or Closeout. Put only reusable cross-task evidence in `agents/evidence/`. Write `agents/reports/<yyyy-mm-dd>-<item-id>.md` only when qualification evidence or the user requires a report.
7. Stage the whole transaction, activation included, run `make task-check`, and commit with the item ID as the subject prefix.

Commit planning records (work items, decisions, `agents/state/current.md`) as a record-only transaction as soon as they are consistent. The queue in `agents/state/current.md` authorizes starting the next item without asking. If `task-check` rejects a commit that only touches planning records, treat it as a defect in the check and fix the check under a work item, rather than adding an item to route around it.

## Done

An item is done when:

- its criteria are met at the required evidence level;
- every target builds with StyleCop warnings as errors;
- each confirmed defect has a permanent regression test and a disposition: fix, documented limitation, or release blocker;
- failure behavior matches the documented guarantee;
- `README.md` and `ACCEPTANCE.md` describe the new behavior, and no criterion was weakened;
- remaining limitations are written down.

## Escalation

Decide routine in-charter work directly: implementation approaches, small fixes, refactoring that serves the task, test infrastructure, tool installs, fixtures, and measured performance changes. Ask the owner before changing the charter or a closed decision; adding a runtime package; a breaking public API change after 1.0; changing a signature, encryption, or certification guarantee, the signer contract, or the supported algorithms; widening the appearance subset or changing the reported coordinate system; redesigning the parser or the incremental-writing model; a release; or effort out of proportion to the requirement.

Record a choice that will govern more than one item in `agents/decisions/D-NN.md`. Research toward a decision is a work item. Keep implementation, test, independent-tool evidence, and genuinely independent review distinct; a persona change is not independence.
