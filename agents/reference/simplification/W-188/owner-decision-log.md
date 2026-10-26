# Owner decision log

Statuses: settled principle, recommendation pending approval, evidence pending, approved, rejected, deferred. Only the owner moves an entry to `approved`, `rejected` or `deferred`; an approved entry records the exact wording or diff, date and linked tickets. Starting statuses come from the charter table (`agents/reference/reset-siimplify.md` lines 241 to 252). Product contract adoption and queue insertion need Gate 2 (D-66).

| ID | Question | Authority | Status | Next research | Evidence | Alternatives | Approved wording | Owner / date | Tickets |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| O 01 | Forms and occurrence namespaces | Owner direction, charter | settled principle | Specify edge cases | | | n/a | Jake, pre-2026-10-26 | W-190 |
| O 02 | Tags follow the same preservation principle | Owner direction | settled principle | Implementation and admission | | R2A refuses tags; R2B excludes (unresolved) | n/a | Jake | W-191 |
| O 03 | Duplicate page labels are acceptable | Owner constraint | settled principle | Preserve or fixed exclusion | | | n/a | Jake | W-191, W-195 |
| O 04 | Empty signature fields survive | Owner endorsement of Agent 2 | settled principle | Lock and seed semantics | | | n/a | Jake | W-194 |
| O 05 | Refusal and unsigned derivatives | | recommendation pending approval | Certification, timestamps, locks | | R2A narrower than R2B | | | W-197 |
| O 06 | Info/XMP and new identity | | evidence pending | Present/absent/malformed behavior | | create absent XMP vs Info only | | | W-205, W-206 |
| O 07 | XFA and Reader-rights preparation | | recommendation pending approval | Quantify workflow/credential friction | | | | | W-200, W-201 |
| O 08 | Signing provider and seed policy | W-151 binding | evidence pending | Retain capabilities absent approval | | | | | W-202, W-203 |
| O 09 | Workflow ordering and rollback | Current contract binding | evidence pending | Reuse W-185/W-186 | | | | | W-195, W-196 |
| O 10 | Async decoders and forensics | | evidence pending | Three independent decisions | | | | | W-208, W-209, W-210 |
| O 11 | Navigation, annotations, aliases, copier modes | | recommendation pending approval | Separate policy from cleanup | | | | | W-192 to W-199 |
| O 12 | Contract adoption and queue insertion | | owner approval required | Exact edits and ticket scopes | | | | | W-211 |

Ticket links are the charter's research areas matched to the queue in `agents/state/current.md`; confirm each when its item starts. If the owner defers a question, current behavior stands and the affected proposal carries the missing decision.
