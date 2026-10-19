# B-30 review

## 1. Task and owner

Technical Lead. Review of B-30, "Bound the predictor `/Columns` allocation", uncommitted on `wip`. Inputs: `2026-10-18-tech-lead-B-30-board.md`, `2026-10-18-core-engineer-B-30.md`, `2026-10-18-qa-engineer-B-30.md`. G8 is not passed.

## 2. Verified facts

These come from reading the diff and the code. I ran no tests.

- `PdfStreamDecoder.Unpredict`: `samples` is at most `int.MaxValue`, and `bpc` is at most `int.MaxValue`, so `samples * bpc` fits a `long`. A row width of `int.MaxValue` or more is refused, so `rowBytes + 1` cannot overflow. `UnpredictPng` returns before `output` and `previous` when `rows == 0`. When `rows > 0`, both buffers are bounded by the decoded data, which is capped. `UnpredictTiff` allocates only `data.Length`. Both `DecodeOne` and `OpenDecoded` go through `Unpredict`. Correct.
- `PdfParser.EnsureXrefColumns` now calls `ReadWidths` first, so the sum is at most 24. Signature unchanged.
- `PdfEngine.ReadXrefStream` (`PdfEngine.cs:1291`), the other caller, always reaches `ParseXrefEntries` -> `ReadWidths` after a successful decode. Neither caller has a recovery `catch`; the only `catch` is `InvalidDataException` around the decode. So no file that opened before is refused now. The one change: when `/W` is malformed and the decode would also fail, the refusal is now `PdfParseException` ("Xref stream /W ...") rather than the decode error (`PdfUnsupportedFilterException`, `PdfObjectTooLargeException`, or "could not be decoded"). Such a file was refused either way. Acceptable, and no documented guarantee moves.
- Charter, resources, and style: no new package, no public API change, caps unchanged, `README.md` and `ACCEPTANCE.md` correctly untouched. The test follows the bound I set: per-thread counter, one reading before `Open` and one after `Assert.Throws`, the build done before the first reading, and the unchanged-bytes assertion.

## 4. Defects confirmed

Test gap, as QA reported. I rate it required, not optional. The backlog names the unchecked `/W` sum in `EnsureXrefColumns` as part of the defect. The scope asks for it to be validated, with regression tests. `team.md` section 8 item 3 requires a permanent regression test for each confirmed defect. Today, reverting `PdfParser.cs` alone fails no test. All three cases use empty decoded data, so the decoder fix hides the parser fix.

The gap can be tested from the public API. With decoded data that is not empty and not stride-aligned, the two paths give different messages. Take `/W [4563402751 0 0] /Predictor 12` with 2 decoded bytes. With the fix, `EnsureXrefColumns` throws "Xref stream /W entry is outside the supported range". With the parser change reverted, the wrapped `/Columns 268435455` reaches `UnpredictPng`, which throws "Predictor rows are not aligned."

## 6. Changes required (returned to the Core Engineer)

In `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs` only, add one case for `/W` validated before use. Use a Flate xref stream with `/W [4563402751 0 0] /DecodeParms << /Predictor 12 >>` and a few decoded bytes that are not empty. Assert all of the following:

- `PdfParseException`, with a message that contains "/W".
- The allocation delta is under 8,388,608 bytes.
- The stream bytes are unchanged.

Show it red on `net8.0` and `net6.0` with only `PdfParser.cs` reverted, and green with the fix. Then rerun the `OpenTests` filter on both frameworks. Leave `src/` alone. The source changes are approved as they stand.

## 9. Handoff

Core Engineer adds the test. QA re-verifies the isolation check (`PdfParser.cs` reverted alone must fail). Technical Lead re-reviews the test only. G8 is not passed.
