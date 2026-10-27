# W-211 proposed queue insertion and gates

Package v1, based on `fbe850e`. `agents/state/current.md` is **not edited** by W-211. This is the exact insertion Jake can approve per packet. Re-verify against `current.md` when approved: at this revision the sections run "Simplification reconciliation and implementation approval gate" (W-211), then "Security hardening: admission design and final guidance" (W-156, W-158, W-159), with the retained work-budget section (W-168, W-149) above them.

## Proposed new section

Insert this section between "Simplification reconciliation and implementation approval gate" and "Security hardening: admission design and final guidance". Only approved packets' tickets appear; allocate W numbers when each packet is approved.

```
## Simplification implementation: approved slices

1. T-01   (P1)  Remove unused copier policy modes
2. T-02   (P1)  Preserve the original exception when final-copy restoration throws
3. T-03   (P2)  Lock down assembly identity and viewer defaults
4. T-05   (P3)  Accept only full filter names in stream dictionaries
5. T-06   (P4)  Assembly refusal frame and diagnostics
6. T-07   (P5)  Freeze ordinary-form namespace and ownership contract
7. T-08   (P5)  Destination classification
8. T-11   (P5)  Annotation relationships and atomic appearance refusal
9. T-09, T-10, T-12, T-13 (P5)  URI semantics, action admission, Stamp/Caret, FreeText
10. T-14  (P6)  Preserve empty signature fields
11. T-15  (P6)  Explicit unsigned derivative
12. T-17, T-18 (P7)  Outline projection, page labels
13. T-20  (P8)  Page-local tag preservation
14. T-22, T-23 (P9)  Rights removal and AbandonXfa, together
15. T-25  (P10) Atomic metadata setters
(Checkpoint CP-1 after item 13; CP-2 after item 14; Gate 4 after both.)
```

Within a numbered line, items are independent unless the Depends column in [tickets.md](tickets.md) says otherwise. Packets are independently approvable except where noted below; an unapproved packet leaves a gap in the list, and its dependents move to "proposed" with a stated prerequisite rather than being dropped.

## Dependency graph (approved packets only)

```
T-01, T-02, T-03, T-05, T-25     independent
T-06 --> T-07 --> T-13
T-06 --> T-08 --> T-09, T-17
T-06 --> T-10
T-06 --> T-11 --> T-12, T-13, T-14, T-20
T-06 --> T-15 (needs D-A, D-B)          T-14 --> T-16 (contingent, needs D-C)
T-07 --> T-20
T-22 --> T-23 (one window); T-22 --> T-15 rights half (D-A)
T-18 independent
T-26, T-27, T-28 independent records/research
```

## Position relative to existing items

| Existing item | Position |
| --- | --- |
| W-168, then W-149 | unchanged, above the new section; the new section does not depend on them (T-27 coordinates) |
| W-156 | after T-06; starts from R197's category and budget vocabulary; retain |
| W-158 | after the owner's W-210 choice; uses W-210's replacement text; retain |
| W-159 | unchanged |
| W-138, W-109 | stay `proposed`; notes appended by T-28; W-109 after W-138 |
| W-214 to W-217 | unrelated encryption items, unchanged |

## Gate 3: cross-area integration checkpoints (separate worktree)

Neither checkpoint authorizes a release. Each uses a separate worktree and the Docker gate, per `AGENTS.md`.

| Checkpoint | When | Contents |
| --- | --- | --- |
| CP-1 assembly | after the last approved P4 to P8 ticket | `make docker-builder docker-test`; full framework matrix for assembly tests; qpdf, pypdf, Poppler (`pdftoppm`, `pdfinfo`), MuPDF `mutool`, pyHanko, `pdfsig` for the empty-field signing smoke test; independent reader plus a structure-aware check for tags (manual reading-order procedure where no tool exists); fuzz smoke if the importer parsers changed (`pdf-fuzz-qualification` rules) |
| CP-2 rights and XFA | after T-22 and T-23 | same Docker gate; fill-only and modify-capable credential cases; qpdf/pypdf structural checks; legacy Reader usability is a separate version-specific claim and is not made by tool success |
| Signing checkpoint | only if Candidate N, R203 or T-16 is approved | per `W-202/proposal.md` follow-up 3 and `W-203/proposal.md` scope 3 |

## Gate 4: final reconciliation

After CP-1 and CP-2 (or after the approved packets complete): reconcile implemented behavior with `docs/spec/`, `ACCEPTANCE.md`, `docs/behavior.md`, the example code, `QUALIFICATION.md` restatements and the open backlog; close or re-propose every ticket that did not ship; record superseded decisions. Neither checkpoint changes release status; the 1.0 track stays suspended.

## Items that need no queue slot

Retain decisions (provider API, seed policy, ordering, aliases, async, decoders, forensics) need only a recorded decision, drafted in [approvals.md](approvals.md) section 3.
