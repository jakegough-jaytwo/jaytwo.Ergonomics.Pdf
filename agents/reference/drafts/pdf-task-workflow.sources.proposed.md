<!-- Proposed replacement for the "## Sources of truth" section of .agents/skills/pdf-task-workflow/SKILL.md, from its heading up to "## Writing a work item". The rest of the skill is unchanged until W-62's successor item 3 shortens it (plan B4). -->

## Sources of truth

`AGENTS.md` sets the order of authority: `AGENTS.md`, then `docs/spec/`, `ACCEPTANCE.md`, `README.md`, `QUALIFICATION.md`, and the records under `agents/`. Apply its rule for target versus shipped behavior: a spec requirement marked New or Change is target behavior, not a disagreement, until the item that implements it changes it to Exists in the same commit that updates `ACCEPTANCE.md` and `README.md`.

When sources disagree under that rule, stop the affected work and report the disagreement to the owner. Do not choose between them yourself. The order says which source governs. It does not license resolving a conflict silently.

A decision in the charter, in an adopted spec, or accepted in `agents/decisions/` is closed. Do not reopen it in code or in a work item. If evidence says it is wrong, record the evidence and raise it with the owner. Repository records never grant permission the charter withholds.

Read only the sources relevant to the requested work item. When a workflow record contradicts the repository, trust the repository and correct the record.
