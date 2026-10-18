# Task board

Owner: Technical Lead. One row per active task. A task may change only the files listed for it. As of 2026-10-18.

## Active

| Task | Implementer | May change | Branch or worktree | Waits on | Verifier |
|---|---|---|---|---|---|

No task is active. B-25, B-28, and B-29 were accepted on 2026-10-18.

## Resume point

Checkpointed 2026-10-18 after the bounded resume of B-28, B-25, and B-29. All three were accepted by the Product Manager after QA sign-off and Technical Lead review. Neither task needed a second return:

- B-28: the `/Index`, `/Size`, `/N`, and `/First` rework added 16 regression cases in `OpenTests.cs`. An indirect object-stream `/N` or `/First` is still refused. This is an existing limitation, not a regression (`agents/reports/2026-10-18-tech-lead-B-28-re-review.md`).
- B-25: the PIN criterion was re-scoped, because no library API receives a PIN. The password and key-material criteria are unchanged. G9's PIN line stays open for the G9 assessment.
- B-29: the committed src fixes were verified unchanged. `SignatureTests.cs` now also checks the refusal message, because the negative-status test had passed with its fix reverted.

At this checkpoint the full suite passed 190/190 on `net8.0` and 190/190 on `net6.0` with qpdf 12.4.2 (also on `PATH`), Poppler 26.09.0, and SoftHSM 2 configured. The library built in Release for all four targets with warnings as errors: 0 warnings, 0 errors. No qualification gate is passed. G8 and G9 stay unpassed. B-18 stays open.

Next actions, in order:

1. Technical Lead: board B-30 (Core Engineer, predictor `/Columns` and `/W` allocation) and B-31 (Signing Engineer, timestamp response size cap). Their files do not overlap, so they may run in parallel as worktrees under `team.md` section 5.
2. Product Manager: log a backlog item for `InteropTests.qpdf_checks_built_incremental_filled_and_signed_pdfs` ignoring `JAYTWO_QPDF` (see `state/status.md`).
3. B-13, B-14, B-15, B-17, and B-18 stay off the board until their tools or budgets exist (see Kickoff plan item 4).

## Kickoff plan

Under decision D-11:

1. B-02 and the three B-05 audits are finished. The planning report is `agents/reports/2026-10-18-tech-lead-planning.md`.
2. The Product Manager confirms or reorders `state/backlog.md` from that report.
3. B-01 acceptance is met on `wip`. B-03, B-06, B-08 through B-12, B-16, and B-19 through B-29 are done. B-30 and B-31 are logged and not boarded. B-18 remains open. G8 and G9 stay unpassed. No qualification gate is passed.
4. B-13, B-14, B-15, B-17, and B-18 stay off the board. B-13 needs a fourth PDF producer. B-14 needs a second PDF-aware signature verifier. B-15 needs a timestamp authority that is not pyHanko. B-17 needs an approved memory budget. B-18 needs a long fuzz run. `pdfsig.exe` is not installed. The Poppler 26.09.0 build has `pdftotext` and `pdftoppm` and does not include `pdfsig`. Python 3.13 has pypdf and pyHanko only.

## Shared-file claims

| Claim | Task |
|---|---|

No file is claimed. The B-25, B-28, and B-29 claims are released.
