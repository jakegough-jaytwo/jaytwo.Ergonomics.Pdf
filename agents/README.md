# Repository automation

This repository uses one active agent with progressively disclosed skills. The active workflow is defined by `AGENTS.md` and the skill bundles under `.agents/skills/`.

## Active skills

| Skill | Use it for |
|---|---|
| `pdf-task-workflow` | Scope and execute one work item, update current ordering or evidence, or assess release readiness. |
| `pdf-core-change` | Parser, xref, filters, encryption, incremental writing, pages, metadata, or attachments. |
| `pdf-acroform-change` | Form discovery, values, appearances, signature-field discovery, or widget geometry. |
| `pdf-signing-change` | PDF signatures, CMS, external signing, RFC 3161, DocMDP, or FieldMDP. |
| `pdf-change-verification` | Tests, external-tool evidence, checkpoint validation, and qualification evidence. |
| `pdf-security-review` | Explicit read-only security audits and gate G8 review evidence. |
| `pdf-fuzz-qualification` | The fuzz harness, the fixed-seed smoke run, a G8 fuzz campaign, or triage of a fuzz finding. |

`AGENTS.md` contains short trigger rules because it is always loaded. Each skill contains only its workflow-specific instructions. The domain skills refer back to the charter instead of copying it.

## Tool compatibility

There is one copy of each skill, under `.agents/skills/<name>/SKILL.md`. Each trigger rule in `AGENTS.md` names the file path, so an agent can follow it without discovering skills natively.

| Tool | Reads instructions from | Finds skills |
|---|---|---|
| Codex | `AGENTS.md` | Native, from `.agents/skills/`. Each skill's `agents/openai.yaml` (`.agents/skills/<name>/agents/openai.yaml`) adds Codex display metadata. |
| Cursor | `AGENTS.md` | Native, from `.agents/skills/` |
| Claude Code | `CLAUDE.md`, which imports `AGENTS.md` | By path, from `AGENTS.md`. Claude Code discovers only `.claude/skills/`. |
| Grok Build | `AGENTS.md` and `CLAUDE.md` | By path, from `AGENTS.md`. Its documented project location is `.grok/skills/`. |

Do not add `.claude/skills/`, `.cursor/skills/`, or `.grok/skills/` mirrors. Cursor also scans `.claude/skills/`, so a mirror would load each skill twice. Refer to a skill by its bare name, such as `pdf-core-change`, not by one tool's invocation syntax (`$name` in Codex, `/name` in Claude Code).

## Work, decisions, and evidence

- `work-items/W-NN.md` is the canonical record for one unit of work. Status is front matter; files never move between status folders.
- `state/current.md` is the only maintained ordering file. `make agents-status` derives the active, waiting, decision, and checkpoint view.
- `decisions/D-NN.md` contains durable choices and proposals. Decisions are not work items.
- `evidence/` contains reusable cross-task and qualification evidence; `reference/` contains durable navigation material. Task-specific evidence stays in the work item's Closeout.
- `reports/` contains task reports. Existing B-prefixed filenames remain unchanged. New work writes at most one report named `<yyyy-mm-dd>-<work-id>.md` when a durable report is needed.
- `reference/skill-plan.md` is the approved tail plan (D-27) that `state/current.md` points to.
- D-34 and W-54 record the removal of the role prompts and review rounds (`roles/`, `reviews/`, `skill-reviews/`, `workflow-review/`); their pre-removal state is [squashed]. D-35 and W-55 record the removal of the team contract (`team.md`), the v1 role prompts (`archive/roles-v1/`), the role-named reports, and the legacy `state/` pointer files; their pre-removal state is [squashed]. D-36 and W-56 record the removal of the byte-exact pre-migration state (`archive/state-v1/`); its pre-removal state is [squashed]. Every closed B item is now a closed W record with the same number under D-37 and W-60.

Responsibility labels such as “Core Engineer” in historical state are provenance, not instructions to launch another agent. For current work, select the matching skill.

## Rule for editing skills and workflow records

If a sentence would stop being true when a work item finishes, it belongs in that item or reusable evidence, not in `AGENTS.md` or a skill. Test counts, tool versions, known defects, open gaps, current priorities, and file sizes are records rather than policy.

`AGENTS.md`, `QUALIFICATION.md`, and `.agents/skills/` change only with the owner's approval. The agent proposes edits to them. It does not make them unasked.

## Why this structure

The design follows current OpenAI guidance:

- [Using skills to accelerate OSS maintenance](https://developers.openai.com/blog/skills-agents-sdk) recommends repository policy in `AGENTS.md`, repo-local skills in `.agents/skills/`, and progressive loading of skill instructions and resources.
- [Rethinking skills and prompts](https://developers.openai.com/blog/rethinking-skills-and-prompts-for-gpt-6-astra) recommends concise skill descriptions, conditional document loading, and removing instructions that force irrelevant context into every task.
- [Skills](https://developers.openai.com/api/docs/guides/tools-skills) defines `SKILL.md` bundles and the metadata-first discovery model.
- [Multi-agent](https://developers.openai.com/api/docs/guides/responses-multi-agent) recommends one agent for ordered work and shared mutable resources, and subagents for bounded independent work that can benefit from parallelism or isolated context.

The old workflow ran Product Manager, Technical Lead, specialist, QA, review, and acceptance mostly in sequence. The current workflow keeps that reasoning in one context, loads domain guidance only when applicable, and reserves a fresh reviewer for claims that genuinely require independence.
