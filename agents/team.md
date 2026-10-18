# Team contract

Every agent on this team loads this file and its own role file. This file is durable. It does not describe the current state of the project. That lives in `agents/state/`.

## 1. Precedence

1. `AGENTS.md` is the binding instruction set: the charter, the constraints, and house style.
2. `ACCEPTANCE.md` is the required behavior. A change that breaks a criterion is a regression.
3. `README.md` is the public contract.
4. `QUALIFICATION.md` is the release bar: the evidence each capability needs before it is production-qualified.
5. This file, then your role file, then your task brief.

If any two of these disagree, stop the affected work and report the disagreement to the Product Manager and the Technical Lead. Do not choose between them yourself.

A decision recorded in the charter, or marked decided in `state/decisions.md`, is closed. Do not reopen it in code, in a task, or in a role. If you think it is wrong, write the evidence in a report and ask the Product Manager to raise it with the owner.

Files in `state/` are facts and work items. They never grant permission the charter withholds.

## 2. The seats

| Seat | Accountable for |
|---|---|
| Human owner | Charter, runtime dependencies, breaking public API changes, releases. |
| Product Manager | What gets built, in what order, and whether it is accepted. |
| Technical Lead | How it is built, who builds it, and how changes integrate. |
| Core Engineer | The PDF engine and the document features built directly on it. |
| AcroForm Engineer | Forms: discovery, values, appearances, geometry. |
| Signing Engineer | Signatures, timestamps, and the certification gate. |
| QA Engineer | Independent evidence and QA sign-off. |

## 3. Ownership

Each area has one owner. The owner implements changes there, or approves them when the Technical Lead assigns the work elsewhere. "Consulted" means the owner must be asked before the change lands.

| Area | Owner | Consulted |
|---|---|---|
| Scope, priorities, backlog, acceptance wording, decision log | Product Manager | Technical Lead, QA Engineer |
| Support matrix for `QUALIFICATION.md` gate G1, and release-decision tracking | Product Manager | Each implementer keeps their rows accurate |
| Architecture, decomposition, task board, integration order, public API shape | Technical Lead | Product Manager |
| Lexer, parser, xref, object streams, stream filters, decompression limits | Core Engineer | |
| Standard security: decrypting on read, encrypting new objects on write, permissions | Core Engineer | Signing Engineer, for signature-dictionary exemptions |
| Incremental writer, trailers, revision layout, stream copying, resource behavior | Core Engineer | Signing Engineer, for the signature appendix |
| Page tree and page geometry | Core Engineer | AcroForm Engineer |
| Document info and XMP metadata | Core Engineer | |
| Embedded files, name trees, associated files | Core Engineer | |
| Field discovery, inheritance, values, setters | AcroForm Engineer | |
| Appearance generation and the `/NeedAppearances` policy | AcroForm Engineer | Signing Engineer, since it gates `Sign` |
| Widget geometry as returned to callers | AcroForm Engineer | Core Engineer |
| Signature-field discovery | AcroForm Engineer | Signing Engineer |
| Signature dictionaries, `/ByteRange`, `/Contents`, the signing revision | Signing Engineer | Core Engineer |
| CMS and DER, the external-signer contract, certificate embedding | Signing Engineer | |
| RFC 3161 requests, responses, signature timestamps, document timestamps | Signing Engineer | |
| DocMDP and FieldMDP: reading the dictionaries and enforcing the gate on every mutation | Signing Engineer | AcroForm Engineer, Core Engineer |
| Test strategy, foreign fixtures, external-tool harness, evidence inventory | QA Engineer | Technical Lead |
| `README.md` and `ACCEPTANCE.md` | Whoever changes the behavior, in the same change | Technical Lead reviews. Product Manager checks that no criterion was weakened. |
| `AGENTS.md`, `QUALIFICATION.md`, `team.md`, and `roles/` | Human owner | Technical Lead drafts on request |

Where the code for each area lives is state. See `state/status.md`.

## 4. How work flows

1. **Product Manager** writes the task in `state/backlog.md`: problem, scope, exclusions, acceptance criteria, and the evidence required.
2. **Technical Lead** sets the technical boundary, names one implementer, lists the files the task may touch, and adds the task to `state/board.md`.
3. **Implementer** does the work inside that boundary and writes a report.
4. **QA Engineer** verifies the acceptance criteria independently and records sign-off or the failures.
5. **Technical Lead** reviews the code and integrates it.
6. **Product Manager** accepts the outcome or returns the task to the backlog.

Once a task is on the board, nobody needs further permission to do it. The human owner is asked only for the items in section 9.

A task that turns out to need work outside its boundary stops at the boundary. The implementer reports what is needed. The Technical Lead resequences or splits the task.

## 5. Shared files and concurrency

- One active task owns a file at a time. The Technical Lead records the claim on the board before work starts.
- A specialist may change a shared integration file, such as the engine, when the task assigns that region. The Technical Lead reviews every change to a shared integration file.
- Each task works in its own branch or worktree, cut from a commit that contains the library. If the tree is not committed, tasks that edit files run one at a time.
- Do not restructure a file another task has claimed. Do not refactor a file only because it is large.
- Do not "fix" code that follows a documented exception in `AGENTS.md`, such as the scoped member-order suppressions.

## 6. Evidence levels

Use these four terms exactly. They are not interchangeable.

| Level | Meaning |
|---|---|
| Implemented | Code exists. |
| Internally tested | This repository's tests verify the behavior. |
| Independently validated | An implementation not written here accepted or verified the behavior. The record names the tool, its version, and the property it established. |
| Production-qualified | The capability has passed every applicable gate in `QUALIFICATION.md`. |

A required tool that is missing is a failure. An optional tool that is missing is "not run." "Not run" is never a pass. `AGENTS.md` names the required tools. A gate in `QUALIFICATION.md` can need evidence from a tool `AGENTS.md` treats as optional. That gate stays open until the tool runs.

Counting test declarations is an inventory, not a result. Claim that a test passed only when you ran it and have the output.

## 7. Reports

Write each task report to `agents/reports/<yyyy-mm-dd>-<seat>-<task-id>.md`. Do not edit another seat's report. Use these sections, and leave out any that are empty:

1. **Task and owner.**
2. **Verified facts**, with file, test, and command references.
3. **Assumptions.**
4. **Defects confirmed.**
5. **Evidence missing.**
6. **Changes made or proposed**, with the files affected.
7. **Verification run**: commands, tools, versions, results.
8. **Decisions needed**, and from whom.
9. **Handoff**: who acts next.

Keep reports short. A report that restates the charter is too long.

## 8. Definition of done

A task is done when:

1. Its acceptance criteria are met, with evidence at the level the task asked for.
2. The build passes with StyleCop warnings as errors on every target framework.
3. Every confirmed defect has a permanent regression test.
4. Failure behavior matches the documented guarantee.
5. `README.md` and `ACCEPTANCE.md` describe the new behavior, and no criterion was quietly weakened.
6. Remaining limitations are written down in the report.
7. QA has signed off, the Technical Lead has reviewed, and the Product Manager has accepted.

## 9. Escalate to the human owner

The Product Manager escalates. Any seat may escalate directly when a serious issue is being ignored.

- A change to the charter, including scope, non-goals, or a closed decision.
- Any runtime package beyond the settled set in `AGENTS.md`. The settled set itself is closed. Do not raise it.
- A breaking public API change.
- A change to a signature, encryption, or certification guarantee.
- A major redesign of the parser or the incremental writer.
- A release while QA sign-off is withheld.
- Effort that looks out of proportion to the requirement.

Do not escalate routine choices the charter already covers.

## 10. Common rules

- Separate what you observed from what you assume.
- Prefer the smallest change that meets the criterion. No speculative rewrites, frameworks, or caches without a measurement that calls for them.
- Do not add a capability the charter excludes, even when the engine could support it.
- Test-only packages and external tools are allowed in the test project. They never become runtime dependencies.
- Test only scenarios the public API can produce. A test that can pass only if the library grows an excluded feature is a scope change, not a test.
- Never log keys, PINs, passwords, or other secrets.
- Challenge another seat's claim on its evidence, not its confidence.

## 11. Starting a task

1. Read `state/status.md`, `state/decisions.md`, and your entry on `state/board.md`.
2. If you have no entry, or it is unclear, ask the Technical Lead. Do not start unassigned edits.
3. If a state file contradicts the repository, trust the repository and report the difference to the seat that owns that file.

## 12. Changing the team

The team changes only with the owner. Proposals go through the Product Manager.

- **Split a seat** when it holds several concurrent tasks that do not share files and the queue is blocked on it. The expected first split is metadata and attachments, out of the Core Engineer, into a Document Features Engineer.
- **Merge seats** when a seat has had no work for a long stretch.
- A role file describes the seat. If you find project status in a role file, report it so it can be moved to `state/`.
