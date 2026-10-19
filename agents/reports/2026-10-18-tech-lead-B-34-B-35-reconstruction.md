# B-34/B-35 reconstruction review

## Task and owner

Technical Lead reconstruction of the interrupted Signing Engineer batch on `wip` at `d4b84dd`.

## Verified facts

- `git status --short --branch` reports `wip` at `d4b84dd`, ahead of `origin/wip` by four commits, with the expected three modified state files, five modified signing files, two modified test/fixture files, and the untracked Security Auditor role.
- The complete diff is WIP: 589 insertions and 55 deletions before this report/board reconciliation. No B-34/B-35 implementation, QA, Technical Lead review, or Product Manager acceptance report existed.
- D-21 closes D-O6 and requires explicit `certReq` TRUE, identified TSA-certificate inclusion checks, root-only pyHanko validation, and placeholder evidence without adding trust validation. D-22 authorizes B-34 and the later test infrastructure. D-23 caps fuzzing at 24 aggregate CPU-hours and authorizes an independent read-only Security Auditor. These decisions are mutually consistent and fit the charter.
- The active rows permit the Signing Engineer files already modified. The new `agents/roles/security-auditor.md` is owner-authorized team setup under D-23, not B-34/B-35 implementation. The modified Product Manager-owned `backlog.md` and `decisions.md` are preparatory state, not Signing Engineer claims.
- `README.md` and `ACCEPTANCE.md` still describe omitted `certReq`; `OpenSslTimestampTests.cs` and `SigningInteropTests.cs` still contain the out-of-band-certificate behavior. The implementation is not documentation- or interoperability-complete.

## Defects confirmed

- The previous shared-file table said no file was claimed while two active rows existed. `agents/state/board.md` now records the exact active claims.
- The checkpoint-history `Resume point` remains stale when read as current instructions: it says D-O6 is open and B-13/B-14/B-18 need owner action. Current D-21 through D-23 and the active/queued rows supersede those historical statements.

## Evidence missing

- No current build or test result can be credited; artifacts may predate the edits.
- The unfinished ESS parser has not yet demonstrated strict handling of trailing/multiple SignerInfos, multiple ESS attributes or CertIDs, complete DER consumption, malformed certificate identity structures, issuer/serial variants, supported/unsupported digest algorithms, or bounded allocation.
- The B-34 WIP has no regression proving `CancelPendingRequests` or another unrelated cancellation near the deadline is not reclassified as timeout.
- The realistic-chain test is synthetic and has not yet established compatibility with the actual OpenSSL token or the 8 MiB appendix bound.

## Changes made

- Reconciled `agents/state/board.md` shared-file claims with the active rows.
- Wrote this report. No source, test, product documentation, or Product Manager-owned state file was changed.

## Handoff

The Signing Engineer may proceed within the recorded claims. Treat every existing signing/test edit as unaccepted WIP. Finish the strict ESS parsing and cancellation distinction, update only the applicable timestamp documentation, run focused implementer checks, and write separate B-34 and B-35 reports before independent QA.
