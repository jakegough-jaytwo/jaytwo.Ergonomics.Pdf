# B-10 review

## 1. Task and owner

Technical Lead. Review of the 3 GiB metadata-appendix measurement. No product code and no `backlog.md` edit. The task is not accepted.

## 2. Verified facts

Approved. No defects.

`three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle` stores `sparse.Length` immediately before `SaveIncremental` and subtracts it from the length immediately after. `Sign` runs after that subtraction. `SparsePdfStream.Write` appends only when the position is already at the end, and `SetLength` throws, so the difference is the appended metadata revision. The test prints `metadata appendix` and asserts that difference is less than `8 * 1024`.

QA recorded `metadata appendix 886` on `net8.0` and on `net6.0`. 886 is under 8192. Each run passed 1 and failed 0. This review did not rerun it. After the signature, the test still asserts `TouchedHole` is false, `BytesRead` is under 2 MiB, and both `/ByteRange` offsets are past 3 GiB.

The scale row in `ACCEPTANCE.md` still says the metadata appendix is under 8 KiB. QA did not edit `ScaleTests.cs`.

## 6. Changes made or proposed

This report. B-10 stays on `agents/state/board.md` until the Product Manager accepts it. B-09 is removed because `agents/reports/2026-10-18-product-manager-B-09.md` accepts it. B-19 and B-20 are boarded.

## 9. Handoff

The Product Manager accepts B-10. This review does not.
