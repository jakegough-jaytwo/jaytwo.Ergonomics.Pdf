# B-30 boarding

## 1. Task and owner

Technical Lead. Board B-30, "Bound the predictor `/Columns` allocation". Implementer: Core Engineer. Verifier: QA Engineer. Branch `wip`, no worktree. Tasks run one at a time on `wip`. No code or test edits. No test runs. No commit. G8 is not passed.

## 2. Verified facts

B-30 is in the charter. It is a bounded defect fix in the decoder and parser. It adds no capability and no package. These facts come from reading the code. I did not execute it.

- `PdfStreamDecoder.Unpredict` (`PdfStreamDecoder.cs:294`) computes `rowBytes = ((columns * colors * bpc) + 7) / 8` in unchecked `int`. `UnpredictPng` (`:355`) computes `stride = rowBytes + 1` and `rows = data.Length / stride`. It then allocates `previous = new byte[rowBytes]` (`:370`) even when `rows == 0`. Non-empty data shorter than one stride already throws "not aligned" before that allocation, so empty decoded data is the path that reaches it.
- `Unpredict` is reached from `DecodeOne` (`:226`, the xref and structural decode) and from `OpenDecoded` (`:63`). One fix in `UnpredictPng` covers both.
- `PdfParser.EnsureXrefColumns` (`PdfParser.cs:335`) adds `(int)number.LongValue` without checking. `/W [4563402751 0 0]` wraps to 268435455. It writes the sum to `/Columns` when the predictor is 10 or more and `/Columns` is absent. `ReadWidths` (`:399`) rejects any entry outside 0 to 8, but runs only after decoding.
- `EnsureXrefColumns` is called from `PdfParser.ParseXrefStream` (`:291`) and `PdfEngine.ReadXrefStream` (`PdfEngine.cs:1291`). The fix keeps its signature, so `PdfEngine.cs` does not change.
- `OpenTests.cs` already has `MalformedXrefStream` and `AssertMalformedDoesNotExtend`, which checks that the stream bytes are unchanged. The test project targets `net8.0;net6.0`. `PdfBuilder.Zlib` is private. The test can use the literal empty zlib stream `78 9C 03 00 00 00 00 01` or a local `ZLibStream`, so `PdfBuilder.cs` does not change.

## 6. Changes made or proposed

I changed `agents/state/board.md` in the "Active" table and "Shared-file claims" only. The B-30 row and claims cover:

- `src/jaytwo.Ergonomics.Pdf/Internal/PdfStreamDecoder.cs`: allocate nothing sized from `/Columns` until at least one full row (`rowBytes + 1` bytes) exists. Compute `rowBytes` and the stride in `long` or with `checked` arithmetic. If a value does not fit an `int`, throw `PdfParseException`. The decompression caps are unchanged.
- `src/jaytwo.Ergonomics.Pdf/Internal/PdfParser.cs`: validate `/W` before summing or writing `/Columns`. It must be three integers, each from 0 to 8. Reusing `ReadWidths` is acceptable. The sum cannot then exceed 24.
- `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs`: regression tests.

**Allocation bound.** Per case, the `GC.GetAllocatedBytesForCurrentThread()` delta must stay strictly under 8 MiB (8,388,608 bytes).

- Take the first reading immediately before the `PdfDocument.Open` that should be refused. Take the second immediately after the `Assert.Throws<PdfParseException>` returns.
- Build the PDF and copy it into the `MemoryStream` before the first reading.
- Measure synchronously on one thread. Use the per-thread counter, not `GC.GetTotalAllocatedBytes`, so parallel xUnit classes do not add noise.
- Each case also asserts `PdfParseException` and asserts that the stream bytes are unchanged.

On today's code each case allocates at least 268,435,455 bytes, so each fails by a factor of about 32. A clamp to the 32 MiB or 64 MiB cap also fails. The 8 MiB figure matches the `ACCEPTANCE.md` budget for opening and saving a dense file larger than 32 MiB.

**Required cases.** Each is a Flate xref stream with empty decoded data:

1. `/Size 1 /W [268435455 0 0] /Filter /FlateDecode /DecodeParms << /Predictor 12 >>`. This is the backlog case.
2. `/Size 1 /W [1 1 1] /Filter /FlateDecode /DecodeParms << /Predictor 12 /Columns 268435455 >>`. This is the explicit `/Columns` case.
3. `/Size 1 /W [4563402751 0 0] /Filter /FlateDecode /DecodeParms << /Predictor 12 >>`. This is the wrapping-sum case for the checked `/W` validation.

The implementer must show every case failing on the unmodified code with the measured delta, then passing with the fix, on both `net8.0` and `net6.0`. `xref_stream_with_predictor_and_object_stream_reads_info` must still pass. That test is the `ACCEPTANCE.md` row for xref streams with Flate and PNG predictor 12.

The task does not change `PdfEngine.cs`, `PdfBuilder.cs`, `README.md`, or `ACCEPTANCE.md`. Valid files behave the same. If the fix needs any other file, stop at the boundary and report.

## 9. Handoff

- **Core Engineer:** implement within the three claimed files and write the report.
- **QA Engineer:** verify independently, including the red-before-green check against the bound.
- **Technical Lead:** review.

B-31 is not boarded here. G8 is not passed.
