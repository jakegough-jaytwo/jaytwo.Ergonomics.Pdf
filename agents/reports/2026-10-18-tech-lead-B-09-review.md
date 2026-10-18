# B-09 review

## 1. Task and owner

Technical Lead. Review of `info_edit_refreshes_moddate_in_utc`. No edit to `MetadataTests.cs`, the QA report, or `backlog.md`. The task is not accepted.

## 2. Verified facts

Approved. No defects.

The test opens a file whose `ModDate` is `D:20200101120000-05'00'`, sets `Title` to `New`, and does not set `ModificationDate`. `SetInfoText` calls `TouchModDate`, which writes `DateTimeOffset.UtcNow` through `FormatDate`. That format ends in `Z` when the offset is zero. After `SaveIncremental`, a later open reads title `New`. `ModificationDate` has offset zero, falls within one second of `UtcNow` around the edit, and the info text starts with `D:` and ends with `Z`. The old offset cannot satisfy that.

`trapped_is_a_name_and_info_dates_round_trip_as_text` still writes an explicit `ModDate` and is not this assertion. The metadata row in `ACCEPTANCE.md` is unchanged: other info edits refresh `ModDate` in UTC.

QA recorded one pass on `net8.0`. This review did not rerun it. `net6.0` was not in that report. QA is adding that run. It is not a defect in the test.

## 6. Changes made or proposed

This report. `agents/state/board.md` marks B-09 in review and B-11 approved, awaiting acceptance. Neither row is removed. B-10 stays.

## 9. Handoff

QA finishes the `net6.0` note. The Product Manager accepts B-09 after that. This review does not accept it.
