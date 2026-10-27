# W-211: simplification reconciliation and approval packet

Package v1, 2026-10-27, repository `fbe850e` (branch `wip`, clean tree at start). Research and draft proposals only (D-66). **Nothing in this folder is adopted.** No protected file, shipped document, runtime code, test, work-item record other than W-211, or queue order was changed. Submission for approval is this item's outcome; approval is Jake's, recorded in [approvals.md](approvals.md).

## Contents

| File | Purpose |
| --- | --- |
| [traceability.md](traceability.md) | PLAN 01 to 19 and O 01 to O 12 mapped to evidence, recommendation, gap, existing item or draft ticket |
| [contract-matrix.md](contract-matrix.md) | Capability-by-operation contract: Preserve, Refuse, Exclude by selection, Exclude by new-document rule, Derivative, Retain; failure semantics |
| [reconciliation.md](reconciliation.md) | 15 conflicts between proposals with resolutions; protected-clause merge map; W-138/W-109/W-156/W-158 overlaps; savings accounting; contrary findings |
| [tickets.md](tickets.md) | 28 draft tickets (T-01 to T-28) with candidate paths, prerequisites, acceptance, validation, effort basis; priced-but-not-recommended options |
| [queue-insertion.md](queue-insertion.md) | Exact proposed insertion into `agents/state/current.md`, dependency graph, Gate 3 checkpoints, Gate 4 |
| [approvals.md](approvals.md) | Versioned approval packets P1 to P11 and R, owner choices D-A to D-K, blank ledger, draft retain decision, review brief |

## How the exact wording is attached

The exact proposed AGENTS, spec, qualification, skill, acceptance and behavior wording for each slice already exists in the slice's own proposal, written when its evidence was fresh. Copying it here would create a second text that can drift, so this package attaches it **by reference** to one authoritative file per slice and adds only what reconciliation changes. The source files and their git blob hashes at `fbe850e` (re-check at approval):

| Slice | Authoritative file | Blob |
| --- | --- | --- |
| Forms | `W-190/proposal.md` | `d5c89040b85e` |
| Tags | `W-191/proposal.md` | `7e89d7bf2a2e` |
| Navigation and URI | `W-192/proposal.md` | `f475cfcb5f13` |
| Annotations | `W-193/proposal.md` | `6736c5f2ab82` |
| Signature fields | `W-194/proposal.md` | `09c129cc23c3` |
| Outlines | `W-195/proposal.md` | `381899e756f6` |
| Labels | `W-196/proposal.md` | `91d03b6c62c4` |
| Refusal and derivative | `W-197/proposal.md` | `c79e5079c99a` |
| XFA preparation | `W-198/proposal.md` | `2b0f81aad1e3` |
| Rights removal | `W-199/proposal.md` | `a95ba7231102` |
| Info and XMP | `W-200/proposal.md` | `d06c018bdc8a` |
| Assembly identity | `W-201/proposal.md` | `9e9373d6161f` |
| Signing providers | `W-202/proposal.md` | `b00144096153` |
| Seed policy | `W-203/proposal.md` | `d689b8a9aa5a` |
| Workflow ordering | `W-204/proposal.md` | `9ccffd4cdce5` |
| Rollback | `W-205/proposal.md` | `c88f802c3089` |
| Aliases | `W-206/proposal.md` | `105c23633f1f` |
| Copier modes | `W-207/proposed-removal.diff`, `findings.md` | `0f4a039fa945` (findings) |
| Async | `W-208/proposal.md` | `bec01664f2cf` |
| Decoders | `W-209/proposal.md` | `9d0ac4ef1a72` |
| Forensics | `W-210/proposal.md` | `b212d36122e5` |

Three places where a single text had to be merged are described in reconciliation.md (A3, A9, A4); the merged A3 and A9 text itself is **not** written here because it depends on owner choices D-A and D-B, and writing it first would be guessing. Where an owner choice is settled, producing the merged diff is the first step of the corresponding ticket.

## Dependency graph (research)

W-188 (baseline) fed W-189 (refusal impact), which fed W-190 to W-210. W-197 sets the admission frame that W-190, W-192, W-193, W-194, W-195 and W-191 plug into; W-198 and W-199 are a joint pair; W-202, W-203 and W-194 interact only through signing policy; W-208, W-209, W-210 and W-206 are independent retain-or-narrow studies. All 23 dependencies of W-211 are `done`.

## Evidence audit

- **Links:** every relative link and every `W-nnn/<file>` path in this folder resolves (21 references checked by script; two false positives were prose fractions, not paths).
- **Measured vs inferred:** corpus counts (51 tracked PDFs; 30 accepted and 21 refused for Combine; 8 of 30 tagged; category unions of 12 core and 18 total of 30), probe results and all per-slice measurements are copied from the sources and remain measured only in the sense those sources state. Every effort range is the source's own inference; the 47 to 90 day gross total in tickets.md is a sum of those inferences, not a plan. Ticket paths are candidates from the sources, not verified against the current tree except where a source verified them.
- **Authoritative semantics:** ISO 32000-1 and ISO 32000-2 clauses were read by the source items from the owner's `C:/jake/git/pdfdocs`; W-211 added no new spec reading.
- **Not re-run:** W-211 reran no probe and no external tool. Its own contribution is reading the 23 outputs, finding conflicts (reconciliation.md section 1), and structuring the packets. A fresh context should check the conflict list; see approvals.md section 4.
- **Independence:** one agent produced every research item and this package. No independence is claimed. No reviewer is assigned.

## Known gaps (each blocks only the claim that needs it)

1. The four original review attachments (R1A, R1B, R2A, R2B) were never found (W-188); R2A/R2B positions are known only through the charter and W-189's recount.
2. Refusal impact is measured for Combine on 51 tracked files only. Fill, flatten, metadata, signing, rotation, owner-password runs, repeated occurrences, selected-page reach and customer demand are unmeasured.
3. No effort estimate exists for W-194's slices; the first ticket there must supply one (T-14, T-16).
4. Viewer-level claims (legacy Reader rights, FreeText regeneration, label GUI behavior) lack named-version evidence.
5. No gigabyte or real-remote measurement; `ExtractPages` read amplification (T-26) unexplained.
6. Licensed Aspose runtime comparison unavailable; no external consumers of the provider API surveyed.
7. This package merges proposals on paper only; no merged protected-file diff has been tried against the current text.
