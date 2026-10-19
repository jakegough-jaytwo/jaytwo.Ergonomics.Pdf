---
name: pdf-fuzz-qualification
description: Build, run, minimize, and record bounded fuzz campaigns for QUALIFICATION.md gate G8. Use for the fuzz harness, the fixed-seed CI smoke run, a qualification campaign, or triage of a fuzz finding. Not for an ordinary regression test or the read-only security review.
---

# PDF fuzz qualification

Fuzzing produces G8 evidence. It does not prove security, and elapsed CPU time is not a pass. Coverage quality, findings, and their dispositions matter more than hours. Read `QUALIFICATION.md` section 10, the B-18 backlog item, and decision D-23 before a campaign. D-23 caps a campaign at 24 aggregate CPU-hours on existing machines, with no paid compute.

## Layout

- The harness and runners live under `tools/` (D-28). Skill folders hold workflow only.
- Seeds and minimized regression inputs live with the harness, never in this skill.
- Confirmed findings become deterministic tests in `test/jaytwo.Ergonomics.Pdf.Tests`.
- Campaign records live in `agents/reports/` using `references/campaign-record.md`.

## Targets

`references/targets.md` lists each entry point, what it exercises, its seeds, and its expected exception types. Add a target only for a high-risk parser, decoder, or encoder path reachable from untrusted bytes. Do not fuzz a path the charter excludes.

## Outcome classes

Classify every input outcome:

1. **Accepted:** the operation completed.
2. **Controlled rejection:** a documented document exception from `references/targets.md` for that target.
3. **Uncontrolled failure:** any other exception, a crash, a stack overflow, a hang past the per-input time limit, or memory past the per-input limit.

Only class 3 is a finding. A controlled rejection with a misleading message or a leaked secret is also a finding. Never widen a target's expected exceptions so a finding disappears; that is a contract change and goes through `pdf-task-workflow`.

## Workflow

1. Build the harness in Release with coverage disabled. Record the commit and whether the tree is dirty.
2. Run the fixed-seed smoke run. It is deterministic and short enough for CI. A smoke failure is a regression, not a campaign finding.
3. Run the bounded campaign: per-target time shares, per-input time and memory limits, and process isolation so one crash does not end the campaign.
4. Deduplicate findings by exception type and the top frames inside the library. Minimize each to the smallest input that still reproduces.
5. Reproduce each finding with the harness `run` mode on the exact commit. A finding that does not reproduce is recorded as such, not dropped.
6. Assign severity from observed behavior on a supported operation. Fix a confirmed Critical or High before the campaign is called complete. Other findings get a disposition: fix, documented limitation, or release blocker.
7. Promote each confirmed finding to a deterministic regression test whose assertion comes from the documented contract, and add its minimized input to the seed corpus.
8. Write the campaign record. Missing, interrupted, or unexecuted portions are recorded as such. They are never passes.

Fixes follow the owning domain skill and `pdf-change-verification`. A campaign that found and fixed defects is rerun from the fixed code before it counts toward G8.

## Boundaries

- Fuzzing does not substitute for the independent review D-23 requires. Use `pdf-security-review` in a fresh context for that.
- Do not add runtime dependencies. Fuzzing engines and instrumentation are test-only.
- Do not log passwords, keys, or PINs from seeds or findings.
- Do not add repair heuristics to make inputs parse. The target is controlled rejection, not acceptance.
