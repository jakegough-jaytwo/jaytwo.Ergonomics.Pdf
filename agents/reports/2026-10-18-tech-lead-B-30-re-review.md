# B-30 re-review

## 1. Task and owner

Technical Lead. Re-review of the B-30 test rework, uncommitted on `wip`, against `2026-10-18-tech-lead-B-30-review.md` section 6. G8 is not passed.

## 2. Verified facts

These come from reading `git diff test/` and the Rework and Re-verification sections of the Core Engineer and QA reports. I ran no tests.

- `OpenTests.wrapping_xref_widths_are_refused_before_they_become_predictor_columns` meets section 6. It builds a Flate xref stream with `/W [4563402751 0 0] /Predictor 12`. The input `78 9C 63 60 00 00 00 02 00 01` is a valid zlib stream for two zero bytes; I checked the Adler-32, `0x00020001`. The test asserts `PdfParseException` with a message containing "/W", an allocation under 8,388,608 bytes, and unchanged stream bytes.
- The helper `AssertRefusedWithinAllocationBound` keeps the measurement rules from the boarding report. The PDF is built and copied before the first reading, the counter is per-thread, and the second reading is taken right after `Assert.Throws`. The three theory cases behave as before.
- QA's evidence closes the gap. With only `PdfParser.cs` at HEAD, the new test fails on `net8.0` and `net6.0` with "Predictor rows are not aligned." With the fix, `OpenTests` passes 51/51 on both frameworks. `xref_stream_with_predictor_and_object_stream_reads_info` passed in every tree.
- Only `OpenTests.cs` changed. The src hashes match the approved versions.

## 6. Changes made

- Approved. The source was approved in the first review, and the test gap is now closed.
- `agents/state/board.md`: I removed B-30 from the Active table and released its three claims. The board notes that B-30 awaits Product Manager acceptance.

## 9. Handoff

The Product Manager accepts B-30 or returns it. Commit after acceptance. B-31 is not boarded here. G8 is not passed.
