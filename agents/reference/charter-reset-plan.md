# Charter reset plan

Status: **proposed, for independent review.** Written 2026-10-19 under W-61 at the commit that adds this file. Nothing in it has started. Steps that change `AGENTS.md`, `QUALIFICATION.md`, or `.agents/skills/` also need the owner's approval of the exact text.

## 0. For the reviewer

You are reviewing a plan, not code. Read these first, in order:

1. `docs/spec/phase-1.md`: the adopted Phase 1 scope (release 1.0). Requirement IDs (R1–R12, W1–W20) are cited below.
2. `agents/decisions/D-38.md` and `D-39.md`: the owner's decisions behind it.
3. `AGENTS.md`, `README.md`, `QUALIFICATION.md`: the documents this plan changes.

Section 7 lists the questions where review is most useful. Write findings to `agents/reports/<date>-W-61-review.md`, or return them to the owner. Do not change the files this plan proposes to change.

## 1. Situation

The owner wants a library for six business operations: read, fill, and flatten forms; metadata; reading encrypted files; PKCS#11 signing; page extraction; and concatenation. Since its first commit, the repository charter instead excluded extraction, concatenation, and rotation, did not mention flattening, and excluded ECDSA. About 60 work items qualified that narrower scope, about a dozen of them reorganizing the agent process itself. The next queued items (W-18, W-13, W-14) are release-qualification work for the old scope.

On 2026-10-19 the owner set a two-phase scope (D-38):

- **Phase 1, release 1.0:** everything that reads a PDF or appends an incremental update. This adds flatten, rotate, bulk fill, form-data export, signature inspection, revision listing, richer attachments, ECDSA, SHA-384/512, PAdES B-B, async signing and saving, hybrid XFA handling, usage-rights handling, and tolerance of a short list of writer quirks.
- **Phase 2, release 1.x:** a new-file writer for page extraction, concatenation, compaction, removing revisions, and removing passwords. Its spec is a draft; a separate owner interview settles it.

The owner also decided:
- Targets become `netstandard2.0`, `net8.0`, and `net10.0`.
- `Microsoft.Bcl.AsyncInterfaces` is approved on `netstandard2.0` (D-39).
- The public API may change freely until 1.0.
- `QUALIFICATION.md` is right-sized (spec section 10).
- The agent workflow is slimmed.

This plan covers three workstreams that must happen before Phase 1 feature work:

- **A.** Make the repository's governing documents agree with the spec.
- **B.** Slim the agent workflow.
- **C.** Re-plan the open work items and lay out the Phase 1 queue.

## 2. Goals and non-goals

Goals:

- `AGENTS.md`, `README.md`, `QUALIFICATION.md`, and the decisions no longer contradict `docs/spec/phase-1.md`, so work items stop tripping the "sources disagree, stop" rule.
- Per-task overhead fits a small library with one maintainer and one agent.
- The queue is ordered to deliver Phase 1 with the least rework.

Non-goals:

- No Phase 1 feature code. The only build change is the target-framework switch (A4).
- No Phase 2 work. The Phase 2 interview is separate.
- No rewrite of closed work items or historical reports.

## 3. Workstream A: charter update

### A1. A precedence rule for target versus shipped behavior

This is the most important change. The spec describes the target state and marks requirements Exists, Change, or New. `README.md` and `ACCEPTANCE.md` describe shipped, tested behavior. Under today's rule ("if any two disagree, stop"), every New requirement would be a disagreement.

Proposed rule for `AGENTS.md` and `pdf-task-workflow`:

> `docs/spec/` defines scope and target behavior. `ACCEPTANCE.md` and `README.md` define shipped behavior. A spec requirement marked New or Change is not a disagreement with them until its work item lands; that item updates `ACCEPTANCE.md` and `README.md` to match the spec. A real disagreement is the spec contradicting `AGENTS.md`, or shipped behavior contradicting the spec for a requirement marked Exists.

Proposed authority order:

1. `AGENTS.md`: binding constraints.
2. `docs/spec/`: scope and target behavior.
3. `ACCEPTANCE.md`: shipped behavior.
4. `README.md`: public contract.
5. `QUALIFICATION.md`: release evidence.
6. `agents/` records.

### A2. `AGENTS.md` (owner approval of exact text)

- **Product charter:**
  - Replace the operations sentence with the Phase 1 list and point to `docs/spec/phase-1.md` and `phase-2.md`.
  - Keep stream-native and large-file design, scoped to Phase 1.
  - Add one sentence saying Phase 2 adds a new-file writer that copies streams without decoding them.
- **Exclusions:**
  - Remove merge, split, rotate, and ECDSA from the exclusion list.
  - Narrow "annotation editing" to "annotation editing beyond what flatten requires".
  - Keep the rest: rendering, text extraction, rewrite in place, creating encryption, XFA editing, JavaScript, redaction, signature validation. PAdES-LTV moves from "never" to "planned for 1.x".
- **Dependencies:** `System.Memory` and `Microsoft.Bcl.AsyncInterfaces` on `netstandard2.0` (D-16, D-39), and none on other targets. Keep the "do not flag it" sentence and extend it to cover both packages.
- **Bullets to rewrite:**
  - DocMDP: flatten and rotate are blocked at every `P` level; the opt-in rule for approval signatures (spec 7.2).
  - Appearances: unchanged except that flatten requires them.
  - Signing: ECDSA, SHA-384/512, PAdES B-B, async signer.
  - Target frameworks.
- **Working model:** apply B3 and B4.
- **CI:** test targets become `net8.0` and `net10.0` on Linux, plus `net48` on Windows.

### A3. `README.md` and `ACCEPTANCE.md`

- `README.md`:
  - Replace "Charter" with a short Phase 1 list and a link to the spec.
  - Replace "Out of scope" with the spec's "Never" list, and add a one-line roadmap for Phase 2 and the 1.x items.
  - Keep the support matrix and tenets describing shipped behavior only (A1).
  - Fix the target-framework line.
- `ACCEPTANCE.md`: no change now. Each Phase 1 work item adds or changes its rows.

### A4. Target frameworks (separate work item, build change)

- Library: `netstandard2.0;net8.0;net10.0`. Test project: `net8.0;net10.0`, plus `net48` on Windows. `net48` runs the `netstandard2.0` library build, so that build stays exercised.
- Update the test project's `System.Security.Cryptography.Pkcs` condition, the `Dockerfile` SDK and runtimes, `Makefile` `unit-test`, and README's build section. The fuzz project targets `net8.0` only and needs no change.
- Do not add the D-39 package yet; the first async item adds it.
- Mark D-13 superseded by D-38. D-13 forbade dropping a target for lifecycle reasons; the owner overrode it.
- Verification: `make docker-builder docker-test`, a Windows run including `net48`, and `make pack` showing the three targets and their dependency groups.
- Compatibility note: `netstandard2.1` consumers (Unity, Xamarin, .NET Core 3.x) fall back to the `netstandard2.0` build and its package references. `net6.0` consumers use the `netstandard2.0` build. No consumer loses access.

### A5. `QUALIFICATION.md` (owner approval of exact text)

Rewrite from about 400 lines to about 150. Keep the structure that works: definition, gates G1–G10, defect policy, capability-specific qualification, release decision. Replace the thresholds with spec section 10:

| Gate | Today | Proposed |
| --- | --- | --- |
| G2 | 50+ fixtures, 4+ producers | About 30 files, mostly real-world, all four categories (business and government forms, Office and browser exports, scans and linearized files, generated reports), each with a manifest entry |
| G4 | Two independent verifiers | pyHanko plus Poppler `pdfsig`, across RSA and ECDSA, each digest, and both formats |
| G6 | Second renderer desirable | Poppler plus MuPDF required, for fill and flatten |
| G7 | Existing workloads | Adds flatten on 1,000 pages, bulk fill of 500 fields, inspection and revision listing on 1 GiB without reading signed ranges, and a compressed 1 GiB attachment; timings recorded as a baseline |
| G8 | 24 CPU-hour campaign plus independent review | One campaign within D-23's cap, run after the parser-surface items (C3), plus one independent review of the parser and signer |

Also add the freshness rule: evidence names its commit and goes stale when the code it covers changes. Then regenerate `agents/evidence/qualification/status.md` and `acceptance-matrix.md` against the new gates.

## 4. Workstream B: slim the workflow

### Inventory

- `AGENTS.md`: 72 lines.
- Seven skills, 536 lines.
- `agents/`: 120 files, about 4,600 lines:
  - 61 work items, 3,234 lines
  - 39 decisions
  - 7 evidence files
  - 8 reports
  - 2 reference files
- `tools/agents/workflow.py`: 317 lines.

Closed work items cost nothing until read. The costs are ceremony and stale guidance.

### Proposals

- **B1. Stop the tail plan.** `agents/reference/skill-plan.md` (D-27, D-32) still plans four new skills: interop, performance, spec-conformance, and release qualification.
  - Cancel them.
  - Fold the one valuable artifact, the corpus manifest format, into the corpus item (C2), plus a short reference under `pdf-change-verification`.
  - Delete `skill-plan.md` and point `agents/state/current.md` at the Phase 1 queue.
  - Mark D-27 and D-32 superseded.
- **B2. Remove dead decisions from the tree.** Delete the nine superseded decisions (D-01 to D-08, D-11) and the rejected D-O4, as D-34 to D-36 did for other historical material. Git keeps them. One closed work item cites one of them, so either the validator accepts a closed item that cites a removed decision, or that one citation is annotated; pick one in the item.
- **B3. One commit per task transaction.** Today `task-check` requires a separate record-only commit to activate an item before its work can be committed. Proposal: accept one transaction that sets the item `active` and includes paths inside that item's own `allowed_changes`. Keep:
  - one active item
  - `allowed_changes`
  - the item-ID commit prefix
  - `make agents-check` before work
- **B4. Shorten `pdf-task-workflow`** from 88 to about 40 lines.
  - Drop text that repeats `AGENTS.md`.
  - Replace the source-of-truth section with A1.
  - Keep the work-item fields, the status lifecycle, and the done checklist.
- **B5. Keep** the three domain skills, `pdf-change-verification`, `pdf-fuzz-qualification`, and `pdf-security-review`. Each has a distinct trigger. Update their references when Phase 1 features land: the signing matrix for ECDSA and PAdES, the field matrix for flatten.
- **B6. Retire stale evidence.**
  - Mark `agents/evidence/technical-baseline.md` and the two checkpoint files as historical.
  - Keep `memory-budgets.md` and the fuzz triage.
  - Regenerate the status and acceptance matrix under A5.
- **Measure:** a typical feature task needs:
  - one activation-plus-work commit
  - `agents-check` and `task-check`
  - one domain skill plus the verification skill
  - no report unless qualification evidence is produced

## 5. Workstream C: re-plan against Phase 1

### C1. Open items

| Item | Today | Proposed disposition | Why |
| --- | --- | --- | --- |
| W-13 corpus | ready, 50 files | Rescope to G2 as revised; start early | Real files decide the quirk list (R11) and drive tests for every feature. Licensing must be recorded per file. |
| W-14 second verifier | ready | Keep: add `pdfsig` for today's RSA output, then extend in each signing item | Building the harness early makes every signing item verifiable. |
| W-18 fuzz and review | waiting | Keep waiting; new blocker: the C3 parser-surface items | R9 parses untrusted CMS, R10 walks xref chains, R11 changes the parser. Qualifying before they land wastes the campaign. The smoke run stays in CI. |
| W-40 indirect filters | proposed | Ready, early | Real-world compatibility (R1); ISO permits indirect `/Filter`. |
| W-42 object-stream churn | proposed | Ready, before G7 | G7 evidence. |
| W-47 auto-size and `/MK` colors | ready | Keep, before flatten | Flatten makes a clipped or colorless appearance permanent. |
| W-49 timestamp exception sanitizing | proposed | Fold into the async signer item | That item rewrites the same interface. |
| W-50 oversize literal allocation | proposed | Ready, before G7 and G8 | Small; bounded allocation. |
| W-51 exact overflow size | proposed | Fold into the signing-format item | Same code path. |

### C2. New items

Create each as `proposed` with Problem and Scope citing spec IDs. Refine its acceptance criteria when it becomes active, adding the `ACCEPTANCE.md` rows it introduces.

| Seq | Item | Spec | Domain skill | Size |
| --- | --- | --- | --- | --- |
| 1 | Charter documents (A1–A3, A5) | - | task-workflow | M |
| 2 | Target frameworks (A4) | §9 | core | S |
| 3 | Workflow slimming (B1–B6) | - | task-workflow | M |
| 4 | Corpus (rescoped W-13) | §8, G2 | change-verification | L |
| 5 | Indirect filters (W-40) | R1 | core | S |
| 6 | Writer quirks from the corpus | R11, §8 | core | M |
| 7 | Form value model, single and bulk fill, export (API reshape) | W1, W2, R6 | acroform | M |
| 8 | Async signer, timestamp, and save (+W-49, adds D-39 package) | W20 | signing | M |
| 9 | Second verifier (W-14) | G4 | signing | S |
| 10 | Digests and ECDSA | W15, W16 | signing | M |
| 11 | PAdES B-B (+W-51) | W17 | signing | M |
| 12 | Signature inspection | R9 | signing | M |
| 13 | Revision listing | R10 | core | S |
| 14 | Catalog version raising | §7.5 | core | S |
| 15 | Attachment relationship, dates, checksum, compression, page mode | R8, W9–W13 | core | M |
| 16 | XFA classification and removal | R12, W4 | acroform | S |
| 17 | Usage rights | §7.3 | signing | S |
| 18 | Auto-size and `/MK` colors (W-47) | §7.6 | acroform | M |
| 19 | Flatten | W3, §7.1, §7.2 | acroform | L |
| 20 | Rotate | W6, §7.2 | core | S |
| 21 | Allocation work (W-42, W-50) and G7 workloads | §4, G7 | core | M |
| 22 | Second renderer (MuPDF) for G6 | G6 | change-verification | S |
| 23 | Fuzz campaign and independent review (W-18) | G8 | fuzz, security-review | L |
| 24 | Release checks: README examples run, pack, versioning | G10 | change-verification | M |

### C3. Ordering rationale

- **Documents before code** (1–3), so no feature item trips the disagreement rule.
- **Corpus early** (4–6). Real files decide which quirks exist and give every later item realistic fixtures.
- **API reshape early** (7, 8). Breaking changes are free until 1.0. Doing them first means later tests and evidence are written once against the final shape.
- **Signing grouped** (8–12), with the verifier harness first, so every signing change is checked by two PDF verifiers as it lands.
- **Version raising before attachments** (14 → 15), because `/AF` needs it.
- **Appearances before flatten** (18 → 19).
- **Parser-surface items before fuzzing.** Items 6, 12, 13, and 16 add or change parsing of untrusted input, so the G8 campaign (23) runs after them.
- **Qualification last** (21–24) against final code, so evidence is not stale at release.

## 6. Risks

| Risk | Mitigation |
| --- | --- |
| The disagreement rule stalls every feature item | A1 lands first. |
| Corpus licensing: real-world files may not be redistributable | US federal government forms are public domain. Office and browser exports can be authored for the corpus with the real tools. Record the license per file. Files that cannot be committed are fetched by a script with a pinned hash, and their tests skip with a recorded reason, never a silent pass. |
| The corpus shows that common files need a full-scan repair, which the spec excludes | Record the evidence and escalate to the owner. Do not widen R11 inside an item. |
| API reshape invalidates existing tests and evidence | Do it at item 7, before new evidence is produced. Re-run the full suite and regenerate the acceptance matrix in that item. |
| Flatten geometry (rotated pages, `/Matrix`, nonzero origins) is subtle | Two renderers (G6), corpus forms with rotation, ISO 32000-1 12.5.5 cited in tests. |
| Usage-rights rules: Reader's real behavior may differ from the transform parameters | Verify against Adobe Reader behavior where available. Otherwise remove `/UR3` on any non-fill write; the safe default only loses features and never shows an error. |
| Phase 1 is roughly 24 items | Each S/M item is a single focused commit. The L items (corpus, flatten, fuzz) may split when activated. |
| Slimming becomes its own long project, as the last migration did | B is one item with a fixed list. Anything not on the list waits. |

## 7. Questions for the reviewer

1. Does A1's precedence rule remove the stall without letting the spec silently override `AGENTS.md`?
2. Is anything in `docs/spec/phase-1.md` inconsistent with ISO 32000-1, ISO 32000-2, or ETSI EN 319 142-1? Check especially PAdES B-B attributes (W17), the flatten placement algorithm (§7.1), the version-raising rule (§7.5), linearized revision counting (R10), and the `/UR3` transform parameters (§7.3).
3. Is the C2 order right? In particular: corpus before the API reshape, and signature inspection (12) before fuzzing.
4. Is deleting superseded decisions (B2) worth the validator change, or should they stay with `status: superseded`?
5. Does B3 weaken any protection the two-commit activation was providing?
6. Is anything missing from Phase 1 that the spec's tenets imply? For example: encrypted-input coverage for every new write, and what flatten does with an existing signature on a field it does not touch.
7. Are the right-sized gates (A5) enough to call 1.0 production-ready for enterprise use?

## 8. Approvals

| Step | Needs owner approval | Why |
| --- | --- | --- |
| A1, A2 | Yes | `AGENTS.md` |
| A3 | No | Follows the adopted spec |
| A4 | No | Decided in D-38; D-13 is superseded |
| A5 | Yes | `QUALIFICATION.md` |
| B1 | Yes | Changes D-27's approved skill plan |
| B2, B6 | No | Records cleanup, as D-34 to D-36 |
| B3 | Yes | Changes the workflow guarantees in `AGENTS.md` |
| B4, B5 | Yes | `.agents/skills/` |
| C1, C2 | No | Work-item records; the order goes to the owner as `agents/state/current.md` |
