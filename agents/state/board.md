# Task board

Owner: Technical Lead. One row per active task. A task may change only the files listed for it. As of 2026-10-18.

## Active

| Task | Implementer | May change | Branch or worktree | Waits on | Verifier |
|---|---|---|---|---|---|
| B-02 Run the suite | QA Engineer | nothing, read-only | n/a | | Technical Lead |
| B-05 Audit: Core | Core Engineer | nothing, read-only | n/a | | QA Engineer |
| B-05 Audit: AcroForm | AcroForm Engineer | nothing, read-only | n/a | | QA Engineer |
| B-05 Audit: Signing | Signing Engineer | nothing, read-only | n/a | | QA Engineer |
| B-06 Qualification gap analysis | QA Engineer, then Product Manager | `agents/state/status.md` evidence section, `agents/state/backlog.md` | n/a | B-02, B-03 | Technical Lead |

## Kickoff plan

Under decision D-11:

1. All rows above run in parallel. None edit code.
2. The Technical Lead writes a planning report that reconciles the audits, then confirms or updates `state/status.md`.
3. The Product Manager confirms or reorders `state/backlog.md` from that report.
4. Editing tasks, starting with B-04, go on this board after B-01 is done. Before then they run one at a time.

## Shared-file claims

None.
