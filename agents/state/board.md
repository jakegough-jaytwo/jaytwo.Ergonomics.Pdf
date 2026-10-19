# Task board

Owner: Technical Lead. One row per active task. A task may change only the files listed for it. As of 2026-10-18.

## Active

| Task | Implementer | May change | Branch or worktree | Waits on | Verifier |
|---|---|---|---|---|---|

No task is active. B-30, B-31, and B-32 were accepted by the Product Manager on 2026-10-18 and are committed on `wip` in the checkpoint that follows `f0da4e9`.

## Resume point

Checkpointed 2026-10-18 after the bounded resume of B-30, B-31, and B-32. The tasks ran one at a time on `wip`, with no worktrees. All three were accepted by the Product Manager after QA sign-off and Technical Lead review. Two tasks were returned once each, and none was returned a second time:

- B-30: the decoder computes the predictor row width in `long` and allocates nothing sized from `/Columns` before a full row exists. `EnsureXrefColumns` validates `/W` through `ReadWidths` first. Each case allocates under the 8 MiB bound the Technical Lead set, against about 268.5 MB before the fix. It was returned once because no test failed with only the parser fix reverted. `wrapping_xref_widths_are_refused_before_they_become_predictor_columns` closes that gap. The non-xref `/Columns` path is covered only by reading the code.
- B-31: the timestamp response is capped at 2 MiB (2,097,152 bytes) and read with `ResponseHeadersRead` through an 81,920-byte buffer, so a refused body reads at most 2,179,072 bytes. It was returned once because a timeout had lost its `TimeoutException` inner exception, and because a stalled body could hang on .NET Framework. The rework restores that exception shape and disposes the response on timeout. The .NET Framework abort is Implemented, not tested. `README.md` and `ACCEPTANCE.md` each gained one sentence on the cap.
- B-32: `InteropTests.qpdf_checks_built_incremental_filled_and_signed_pdfs` finds qpdf through `ExternalTool.Locate("JAYTWO_QPDF", "qpdf.exe")`. QA implemented it, and the Technical Lead verified the `JAYTWO_QPDF`-only setup independently.

At this checkpoint the full suite passed 200/200 on `net8.0` and 200/200 on `net6.0` with qpdf 12.4.2, Poppler 26.09.0, and SoftHSM 2 configured. The library built in Release for all four targets with warnings as errors: 0 warnings, 0 errors. No qualification gate is passed. G8 and G9 stay unpassed. B-18 stays open.

Next actions, in order:

1. No logged backlog item can proceed without the owner. B-13, B-14, B-15, B-17, and B-18 stay off the board until their tools or budgets exist (see Kickoff plan item 4).
2. Optional, if the owner wants it: run the test suite on a .NET Framework runtime. It would exercise the B-31 abort-by-dispose path and the G10 gap. This needs an owner decision on adding a test target.

## Kickoff plan

Under decision D-11:

1. B-02 and the three B-05 audits are finished. The planning report is `agents/reports/2026-10-18-tech-lead-planning.md`.
2. The Product Manager confirms or reorders `state/backlog.md` from that report.
3. B-01 acceptance is met on `wip`. B-03, B-06, B-08 through B-12, B-16, and B-19 through B-32 are done. B-18 remains open. G8 and G9 stay unpassed. No qualification gate is passed.
4. B-13, B-14, B-15, B-17, and B-18 stay off the board. B-13 needs a fourth PDF producer. B-14 needs a second PDF-aware signature verifier. B-15 needs a timestamp authority that is not pyHanko. B-17 needs an approved memory budget. B-18 needs a long fuzz run. `pdfsig.exe` is not installed. The Poppler 26.09.0 build has `pdftotext` and `pdftoppm` and does not include `pdfsig`. Python 3.13 has pypdf and pyHanko only.

## Shared-file claims

| Claim | Task |
|---|---|

No file is claimed. The B-25, B-28, B-29, B-30, B-31, and B-32 claims are released.
