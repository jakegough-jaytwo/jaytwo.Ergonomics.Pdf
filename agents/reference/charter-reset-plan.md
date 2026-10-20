# Charter reset plan

Status: **agreed by author and reviewer; pending the owner's approvals in section 8.** Written 2026-10-19 under W-61 and revised the same day with every agreed finding from `agents/reports/2026-10-19-W-61-review.md`. Nothing in this plan has started. Steps that change `AGENTS.md`, `QUALIFICATION.md`, or `.agents/skills/` also need the owner's approval of the exact text.

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

> `docs/spec/` defines scope and target behavior. `ACCEPTANCE.md` and `README.md` define shipped behavior. A spec requirement marked New or Change is target behavior: it is not a disagreement with them, and it must not be advertised as shipped. The work item that implements it, in the same commit, changes its status to Exists, updates `ACCEPTANCE.md` and the shipped-behavior sections of `README.md`, and adds the required evidence. From then on, a contradiction among the implementation, `ACCEPTANCE.md`, `README.md`, and that requirement is a disagreement. A spec contradicting `AGENTS.md` is always a disagreement.

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
- **Versions** (agreed in review; corrects adopted spec §7.5, so the owner confirms):
  - Phase 1 opens PDF 1.0 to 1.7 plus the PDF 2.0 structures in R1.
  - An incremental write raises catalog `/Version` only up to `/1.7`, and only for a feature ISO 32000-1 defines at a later version than the file's effective version. Phase 1 never raises a file to `/2.0`. The header is never rewritten.
  - Writing `/AF` and `/AFRelationship` without a raise is a deliberate, documented compatibility extension to PDF 1.x, not strict ISO 32000-1 conformance. PDF/A-3 added associated files to its PDF 1.7 base, and PDF 2.0 later adopted them; 1.x readers ignore the keys, and `/EmbeddedFiles` stays the backward-compatible discovery path.
  - The PDF/A-3 relationship subset is `Source`, `Data`, `Alternative`, `Supplement`, and `Unspecified`. The other W9 values come from PDF 2.0. A PDF/A-3 input is not promised to stay conformant when the caller picks a value outside that subset.
  - W9 supplies associated-file building blocks useful to Factur-X and ZUGFeRD. It does not by itself claim PDF/A-3, Factur-X, or ZUGFeRD conformance; those formats also need profile-specific XMP metadata and an extension schema.
  - The W9 item tests that a representative PDF/A-3 input keeps effective version 1.7, that `/EmbeddedFiles` still lists the file, and that no PDF/A claim is made. A veraPDF observation is credited only for the profile property it actually checks.
- **Dependencies:** `System.Memory` (D-16) and, as an owner authorization, `Microsoft.Bcl.AsyncInterfaces` (D-39), both on `netstandard2.0` only, and none on other targets. Keep the "do not flag it" sentence and extend it to cover both packages.
- **Bullets to rewrite:**
  - DocMDP: flatten and rotate are blocked at every `P` level; the opt-in rule for approval signatures (spec 7.2).
  - Appearances: unchanged except that flatten requires them.
  - Signing: ECDSA, SHA-384/512, PAdES B-B, async signer.
  - Target frameworks.
- **Working model:** apply B3 and B4.
- **CI:** test targets become `net8.0` and `net10.0` on Linux, plus `net48` on Windows.

### A3. `README.md` and `ACCEPTANCE.md`

- `README.md`:
  - Replace "Charter" with a short Phase 1 list, labeled as the 1.0 target, and a link to the spec. Current support stays separately visible.
  - Replace "Out of scope" with the spec's "Never" list, and add a one-line roadmap for Phase 2 and the 1.x items.
  - Keep the support matrix and tenets describing shipped behavior only (A1).
  - Fix the target-framework line. List `Microsoft.Bcl.AsyncInterfaces` as a dependency only when the async item adds it.
  - Say that no PDF/A conformance is claimed, and that appending to a PDF/A file is not guaranteed to keep it conformant.
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
| G2 | 50+ fixtures, 4+ producers | At least 30 distinct, meaningful files, at least 5 in each of the four categories (business and government forms, Office and browser exports, scans and linearized files, generated reports), and at least two thirds authored by tools other than the repository's test generators (pypdf, qpdf, pyHanko). Each has a manifest entry. |
| G4 | Two independent verifiers | pyHanko plus Poppler `pdfsig`, across RSA and ECDSA, each digest, and both formats |
| G6 | Second renderer desirable | Poppler plus MuPDF required, for fill and flatten |
| G7 | Existing workloads | Adds flatten on 1,000 pages, bulk fill of 500 fields, inspection and revision listing on 1 GiB without reading signed ranges, and a compressed 1 GiB attachment; timings recorded as a baseline |
| G8 | 24 CPU-hour campaign plus independent review | One campaign within D-23's aggregate cap, run after the input-surface items (C3). Its plan predeclares, for every target, a bounded allocation or stop condition. It is complete only when every target reaches that allocation or condition (otherwise the gate records it as failed or incomplete), every artifact is triaged, and affected targets are rerun from fixed code. Its record names the targets, seeds and corpus hashes, per-input time, memory, and size limits, coverage or health observations, and triage and rerun results. No fixed minimum duration. Plus one independent review of the parser and signer. |

Also add the freshness rule: evidence names its commit, and it expires when the relevant production code, test oracle, fixture, tool configuration, or gate definition changes. Then regenerate `agents/evidence/qualification/status.md` and `acceptance-matrix.md` against the new gates.

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
  - Re-home the minimum durable artifacts the new gates still need:
    - the corpus manifest and its validator, under the corpus item and `pdf-change-verification`;
    - the performance workload and baseline record, under G7 and `pdf-change-verification`;
    - the qualification evidence inventory and its freshness check, under A5;
    - a release-gate summary generated from that inventory, not maintained as a second board.
  - Defer the clause-to-contract map until a concrete conformance problem justifies it.
  - Delete `skill-plan.md` and point `agents/state/current.md` at the Phase 1 queue.
  - Mark D-27 and D-32 superseded.
- **B2. Keep superseded decisions; remove them from active navigation.** Verify that D-01 to D-08, D-11, and D-O4 have terminal statuses (superseded or rejected) and that no current guidance points to them. Do not delete them: D-34 to D-36 distilled material into decisions rather than deleting decisions, and keeping them preserves the authority chain and validator references.
- **B3. One commit per task transaction.** Today `task-check` requires a separate record-only commit to activate an item before its work can be committed. Its useful protection is that every non-record path is checked against an already committed item's `allowed_changes`. Keep that protection by making the staged transaction self-describing:
  - parse work items from the staged index, not the working tree or HEAD;
  - require at most one `active` item in the staged repository state;
  - keep record-only transitions (work-item records and `agents/state/current.md` only) valid without an owner;
  - for staged non-record paths, resolve exactly one owner: the item whose staged transition the transaction makes (activation, or completion from `active` or `ready` to `done`), or else the unchanged HEAD-active item; reject the transaction when the owner is ambiguous;
  - check every staged non-record path against that owner's staged `allowed_changes`;
  - validate the staged repository state;
  - add tests for unrelated pre-staged files, two activations, an activation with a disallowed path, activation plus allowed work, completion plus work leaving zero active items, and record-only changes.

  Also keep the item-ID commit prefix and `make agents-check` before work.
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
| W-13 corpus | ready, 50 files | Rescope to G2 as revised; start early, limited to fixtures, provenance, manifest validation, and format probes | Real files decide the quirk list (R11) and give later items realistic fixtures. Feature-specific API tests come with the feature items, so the API reshape does not rewrite them. Licensing is recorded per file. |
| W-14 second verifier | ready | Keep, ahead of every signing behavior change: add `pdfsig` for today's RSA output, then extend in each signing item | No new signature code is accepted on the old single-verifier path. |
| W-18 fuzz and review | waiting | Keep waiting; `depends_on` the input-surface items 5, 6, 7, 13, 14, 16, and 17 (C2) | Each adds or changes parsing of untrusted input: filters, quirks, CMS, xref chains, attachment parameters, XFA detection, usage-rights parameters. Qualifying before they land wastes the campaign. The smoke run stays in CI. |
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
| 7 | Usage rights, common policy for every write | §7.3 | signing | S |
| 8 | Form value model, single and bulk fill, export (API reshape) | W1, W2, R6 | acroform | M |
| 9 | Second verifier (W-14) | G4 | signing | S |
| 10 | Async signer, timestamp, and save (+W-49, adds D-39 package) | W20 | signing | M |
| 11 | Digests and ECDSA | W15, W16 | signing | M |
| 12 | PAdES B-B (+W-51) | W17 | signing | M |
| 13 | Signature inspection | R9 | signing | M |
| 14 | Revision listing | R10 | core | S |
| 15 | Catalog version raising (up to 1.7; see A2) | §7.5 | core | S |
| 16 | Attachment relationship, dates, checksum, compression, page mode | R8, W9–W13 | core | M |
| 17 | XFA classification and removal | R12, W4 | acroform | S |
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
- **Usage rights before the first new write** (7). The common policy lands once, for the writes that exist today; each later write item adds its operation-specific cases, so every item can meet C4 when it lands.
- **API reshape early** (8, 10). Breaking changes are free until 1.0. Doing them first means later tests and evidence are written once against the final shape.
- **Signing grouped** (9–13), with the verifier harness (9) before any signing behavior change, so every signing change is checked by two PDF verifiers as it lands.
- **Version raising before attachments** (15 → 16), because `/PageMode /UseAttachments` needs it. (`/AF` does not trigger a raise; see A2.)
- **Appearances before flatten** (18 → 19).
- **Input-surface items before fuzzing.** Items 5, 6, 7, 13, 14, 16, and 17 add or change parsing of untrusted input, so the G8 campaign (23) depends on all of them.
- **Qualification last** (21–24) against final code, so evidence is not stale at release.

### C4. Checks every new write must include

Each item that adds or changes a write covers, in its acceptance criteria:

- encrypted input under the owner and the user password, including refusal when the permission bits forbid the write;
- new strings and streams encrypted with the document's cipher, with `/Encrypt` and `/ID` kept;
- the original bytes kept as a prefix, and the destination unchanged on every documented failure;
- certification, approval-signature, FieldMDP, and usage-rights behavior where it applies;
- for the async item, sync and async equivalence, and cancellation leaving the destination unchanged.

For flatten specifically: DocMDP blocks it at every `P` level; an approval signature requires the explicit opt-in; signed signature fields are never flattened. Tests show that the earlier signature's bytes stay cryptographically intact while an independent verifier reports the expected later modification.

For usage rights: the item resolves the actual signature `/Reference`, `/TransformMethod`, and `/TransformParams`. For a write that password permissions, DocMDP, and FieldMDP otherwise allow, a UR3 transform that is absent, unresolvable, malformed, unknown, or insufficient for the change causes `/UR3` to be removed from `/Perms` in the same revision, and the write proceeds. The library never keeps a rights signature it cannot show permits the change. A structural failure that prevents safely reading or rewriting the document stays an ordinary parse failure; this rule does not mean repairing arbitrary corruption.

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

Answered in `agents/reports/2026-10-19-W-61-review.md`. Kept here for the record.

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
| Spec §7.5 and W9 correction (A2 versions) | Yes | Changes the adopted spec |
| A3 | No | Follows the adopted spec |
| A4 | No | Decided in D-38; D-13 is superseded |
| A5 | Yes | `QUALIFICATION.md` |
| B1 | Yes | Changes D-27's approved skill plan |
| B2, B6 | No | Records cleanup; B2 deletes nothing |
| B3 | Yes | Changes the workflow guarantees in `AGENTS.md` |
| B4, B5 | Yes | `.agents/skills/` |
| C1, C2 | No | Work-item records; the order goes to the owner as `agents/state/current.md` |
