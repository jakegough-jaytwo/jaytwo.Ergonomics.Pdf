# B-30 Core Engineer report

## 1. Task and owner

Core Engineer. B-30, "Bound the predictor `/Columns` allocation", within the boundary in `agents/reports/2026-10-18-tech-lead-B-30-board.md`. Branch `wip`, not committed. G8 is not passed.

## 2. Verified facts

- On the unfixed code, all three required cases throw `PdfParseException` but allocate about 268.5 MB first. The allocation was measured with `GC.GetAllocatedBytesForCurrentThread()` around `PdfDocument.Open`.
- `EnsureXrefColumns` is followed on both callers (`PdfParser.ParseXrefStream` and `PdfEngine.ReadXrefStream`) by `ParseXrefEntries`, which calls `ReadWidths`. Validating `/W` first moves the same refusal ahead of the decode. It does not add a new one.

## 4. Defects confirmed

All three cases were red on both frameworks. The table gives the allocation delta per case.

| Case | net8.0 before | net6.0 before | net8.0 after | net6.0 after |
|---|---|---|---|---|
| `/W [268435455 0 0] /Predictor 12` | 268,579,984 | 268,583,304 | 118,160 | 119,792 |
| `/W [1 1 1] /Predictor 12 /Columns 268435455` | 268,527,144 | 268,535,680 | 91,680 | 101,592 |
| `/W [4563402751 0 0] /Predictor 12` | 268,526,584 | 268,526,584 | 73,728 | 73,728 |

The bound is under 8,388,608 bytes. The "after" figures came from one run with the assertion temporarily set to `< 0` so the delta would print. The test file was restored afterward.

## 6. Changes made

- `src/jaytwo.Ergonomics.Pdf/Internal/PdfStreamDecoder.cs`:
  - `Unpredict` computes the row width in `long`. If it does not fit, it throws `PdfParseException` ("Predictor row width is too large."). The stride `rowBytes + 1` therefore cannot overflow.
  - `UnpredictPng` returns an empty array when no full row exists. This happens before any allocation sized from `/Columns`.
- `src/jaytwo.Ergonomics.Pdf/Internal/PdfParser.cs`: `EnsureXrefColumns` validates `/W` with `ReadWidths` before it sums the widths. That means three integers, each 0 to 8, so the sum is at most 24.
- `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs`: added the theory `predictor_columns_from_xref_stream_do_not_allocate_before_a_row_exists` with the three cases. Each case asserts `PdfParseException`, an allocation delta under 8 MiB, and unchanged stream bytes.

`PdfEngine.cs`, `PdfBuilder.cs`, the caps, `README.md`, and `ACCEPTANCE.md` are unchanged.

## 7. Verification run

.NET SDK 10.0.100-rc.1.

- `dotnet build src/jaytwo.Ergonomics.Pdf`: all four targets, 0 warnings, 0 errors.
- `dotnet test test/jaytwo.Ergonomics.Pdf.Tests --filter "FullyQualifiedName~OpenTests" -f net8.0`: 50/50 passed.
- The same command with `-f net6.0`: 50/50 passed.
- Those runs include `xref_stream_with_predictor_and_object_stream_reads_info`.

The full suite was not run.

## Limitations

- `IntOf` still narrows `/Columns`, `/Colors`, and `/BitsPerComponent` without a range check. For example, `/Columns 4294967297` reads as 1. This is not an allocation path any more, because the row buffers now follow the decoded data, which is capped.
- A predictor stream with a row width of `int.MaxValue` bytes or more is now refused. Before, its width wrapped.
- An xref stream with a missing or malformed `/W` is refused before its decode instead of after. The message is the same.

## 9. Handoff

QA Engineer verifies red-before-green against the bound. Then the Technical Lead reviews.

## Rework

This rework answers `agents/reports/2026-10-18-tech-lead-B-30-review.md` section 6. Only `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs` changed. `src/` was left as approved.

- **New test:** `wrapping_xref_widths_are_refused_before_they_become_predictor_columns`. It builds a Flate xref stream with `/W [4563402751 0 0] /DecodeParms << /Predictor 12 >>` whose data decodes to 2 zero bytes. The zlib input is `78 9C 63 60 00 00 00 02 00 01`. The test asserts three things:
  - `PdfParseException` with a message containing "/W".
  - An allocation delta under 8,388,608 bytes.
  - Unchanged stream bytes.
- **Shared helper:** the allocation measurement moved into `AssertRefusedWithinAllocationBound`. The B-30 theory uses the same helper, and its behavior is unchanged.
- **Red with `PdfParser.cs` alone reverted to HEAD:** on both net8.0 and net6.0 the new test failed with "Predictor rows are not aligned." and the other 50 passed. Afterward, `PdfParser.cs` was restored byte-identical; the SHA-256 is `e97fbcfb...daa9` before and after.
- **Green with the fix:** the `OpenTests` filter passed 51/51 on net8.0 and 51/51 on net6.0.
