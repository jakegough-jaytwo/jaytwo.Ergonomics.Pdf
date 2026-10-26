# Simplification evidence ledger

Shared ledger for the W-188 to W-211 research. Append rows; do not rewrite earlier ones. Columns: ID, claim, kind (measured / inferred / secondhand), revision, symbol or clause, command, observation, limitation, reproduction owner.

Disagreement classes: revision, operation, factual error, scope, product tradeoff.

| ID | Claim | Kind | Revision | Source / command | Observation | Limitation | Owner |
| --- | --- | --- | --- | --- | --- | --- | --- |
| E-001 | Repository has 51 tracked PDF fixtures | measured | 8a6e496 | `git ls-files '*.pdf' \| wc -l`; `fixture-manifest.csv` | 51 (33 `corpus`, 8 `Fixtures`, 5 `defects`, 4 `focused-assembly-links`, 1 `focused-assembly-invoice`) | Says nothing about feature content | W-189 |
| E-002 | R2A "41 fixtures" | secondhand | n/a | charter line 148 | Not reproducible: no 41-file set matches | R2A unavailable | W-189 |
| E-003 | R2B "raw scan of 36 files" | secondhand | n/a | charter line 148 | Not reproducible against 51 or 33 | R2B unavailable; scan method unknown | W-189 |
| E-004 | R2A feature counts (18 tagged, 16 outlines, 6 Reader-extended, 3 XFA, 4 signed) | secondhand | n/a | charter line 148 | Unverified; overlapping features are not refusal totals | Needs a detection method and denominator | W-189 |
| E-005 | Review attachments R1A/R1B/R2A/R2B are not in the repository or obvious local paths | measured | 8a6e496 | `baseline.md` search list | None found | Search was of tracked files, Downloads, Desktop | Owner to supply |
| E-006 | Related ticket states (W-138, W-109, W-156, W-158 proposed; W-151, W-184 to W-187 done) | measured | 8a6e496 | `grep '^status:' agents/work-items/W-NN.md` | As in `baseline.md` | Records only | W-188 |
| E-007 | Working tree has unrelated uncommitted W-213 / W-155 changes | measured | 8a6e496 | `git status --short` | 2 modified, 3 untracked | W-213 would change README authority | Owner |
| E-008 | `agents-check` passes with 213 items, 67 decisions | measured | 8a6e496 | `make agents-check` | OK | Structural check only | W-188 |

## Review disagreements (classification pending the attachments)

Charter-stated disputes; none can be classified from primary text yet.

| ID | Dispute | Provisional class | Basis | Needs |
| --- | --- | --- | --- | --- |
| D-A | Tag handling: R2A refuses; R2B excludes | product tradeoff | charter lines 41, 242 | W-191 |
| D-B | Page labels: preserve vs exclude | product tradeoff (O 03 settles acceptability of duplicates) | charter line 41, 243 | W-191 |
| D-C | Absent XMP: R2A creates; R2B does not | product tradeoff | charter lines 41, 246 | W-205 / W-206 |
| D-D | Certification and locks in unsigned derivatives | product tradeoff | charter line 85 | W-197 |
| D-E | Duplicate or contradictory fills: reject vs current | scope | charter line 41 | W-190 |
| D-F | Fixture denominators (41 vs 36 vs 51) | factual error or revision | charter line 148 | W-189 |
