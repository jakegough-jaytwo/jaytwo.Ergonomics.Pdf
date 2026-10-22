# Campaign record

Write one record per campaign or smoke qualification run in `agents/reports/<yyyy-mm-dd>-<task-id>-fuzz.md`. Every field is filled. Write "not recorded" or "not run" rather than leaving a field out.

## Run

- Kind: smoke or campaign.
- Commit and dirty-tree state. If dirty, list the changed runtime files.
- Harness commit, engine and version (for example SharpFuzz and libFuzzer versions), runtime, OS, architecture, and CPU count.
- Build configuration: Release, coverage disabled.
- Start and end times, wall time, and aggregate CPU-hours, per target and in total.
- Per-input limits: time, memory, and maximum input length.
- Seeds: corpus directory, file count, and a SHA-256 over the sorted file hashes, per target.
- Engine seed or seeds, and the exact command lines.

## Results

| Target | CPU-hours | Executions | Coverage (edges or features, if reported) | Final corpus size | Findings |
|---|---|---|---|---|---|

## Findings

One row per deduplicated finding.

| ID | Target | Signature (exception type and top library frames) | Minimized input (path, SHA-256, length) | Reproduces on commit | Severity | Disposition | Regression test |
|---|---|---|---|---|---|---|---|

Disposition is fix, documented limitation, or release blocker. A fix names the commit and the test that fails without it.

## Limitations

What the campaign did not reach or establish: targets skipped, time cut short, coverage plateaus, paths not reachable from the harness, and any portion run on a different commit.

## Result

State whether the campaign meets the G8 fuzzing target. It does not when a target fails to reach its predeclared allocation or stop condition, when a Critical or High finding is open, or when the campaign ran on code that a fix later changed without a rerun. D-23's 24 aggregate CPU-hours is a cap, not a minimum. The record does not pass G8 alone; G8 also needs the independent review.
