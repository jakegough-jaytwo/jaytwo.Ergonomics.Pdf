# Agent team

This folder defines the agent team that owns `jaytwo.Ergonomics.Pdf`. It separates three kinds of material that change at different rates.

| Folder | What it holds | Changes when |
|---|---|---|
| `team.md`, `roles/` | Durable definitions: who owns what, how work flows, what each seat is allowed to decide. No project status. | The team itself changes. Rare, and only with the owner. |
| `state/` | Current state: baseline, backlog, owner decisions, the task board. | Every task. Owned by the seats named in each file. |
| `reports/` | Task reports written by agents. Append-only. | Every task. |

`reviews/` holds the review round that produced this structure. `archive/roles-v1/` holds the original role files those reviews cite by line number. Neither is loaded by agents.

## Launching an agent

An agent's instructions are, in order:

1. `AGENTS.md`, which reaches every agent through `CLAUDE.md`.
2. `agents/team.md`.
3. `agents/roles/<role>.md`.
4. The task the agent is assigned on `agents/state/board.md`.

The agent reads `state/` at the start of each task. State is facts and work items. It never overrides the charter, the team contract, or the role.

## Roles

| Seat | File |
|---|---|
| Product Manager | `roles/product-manager.md` |
| Technical Lead | `roles/tech-lead.md` |
| Core Engineer | `roles/core-engineer.md` |
| AcroForm Engineer | `roles/acroform-engineer.md` |
| Signing Engineer | `roles/signing-engineer.md` |
| QA Engineer | `roles/qa-engineer.md` |

Use exactly these names in every file, task, and report.

## Rule for editing this folder

If a sentence would stop being true when a task finishes, it belongs in `state/`, not in `team.md` or a role file. Test counts, tool versions, known defects, open gaps, current priorities, and file sizes are state.
