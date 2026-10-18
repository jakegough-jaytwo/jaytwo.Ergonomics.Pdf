# B-10. Metadata appendix of the 3 GiB save

## Task and owner

QA Engineer. Read-only except this report. `ScaleTests.cs` was not edited. No `agents/state/` edits.

**Signed off.** On the 3 GiB save, the metadata appendix is 886 bytes, under 8 KiB. The hole and byte-read checks still pass. Evidence required: internally tested.

## Verified facts

- `three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle` records `sparse.Length` immediately before `SaveIncremental` and subtracts it from the length immediately after. `Sign` runs after that subtraction. The asserted difference is the metadata revision.
- `SparsePdfStream.Write` only appends, and `SetLength` throws, so that difference is the appended metadata appendix.
- The test writes `metadata appendix` and asserts the difference is less than `8 * 1024`. Both runs printed `metadata appendix 886`. 886 is under 8192.
- After the signature, the test still asserts `TouchedHole` is false, `BytesRead` is under 2 MiB, and both `/ByteRange` offsets are past 3 GiB.
- The scale row in `ACCEPTANCE.md` still says the metadata appendix is under 8 KiB.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle" --nologo --logger "console;verbosity=detailed"`

Passed 1, failed 0, skipped 0. Test duration 66 ms. Output: `metadata appendix 886`. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle" --nologo --logger "console;verbosity=detailed"`

Passed 1, failed 0, skipped 0. Test duration 85 ms. Output: `metadata appendix 886`. Target `net6.0`.

Not run, and not counted as passes: the rest of `ScaleTests`, and the rest of the suite.

## Changes made or proposed

This report only. `dotnet test` wrote gitignored coverage under `out/coverage/` because `test/Directory.Build.props` sets `CollectCoverage`.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone.
