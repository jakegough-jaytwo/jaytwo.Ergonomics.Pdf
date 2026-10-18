# B-10 Metadata appendix size

## 1. Task and owner

Evidence that the metadata appendix of the 3 GiB save is under 8 KiB. Core Engineer. No `src/` edits. No commit. Branch stayed `wip`.

The board text still said B-10 was not boarded. This task was started from the assignment, and only `ScaleTests.cs` was edited.

## 2. Verified facts

`three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle` saves a title change, then signs. `SparsePdfStream` only appends, so the length change across `SaveIncremental` is the metadata revision. The signature revision is written after that measurement.

That appendix was 886 bytes. 8 KiB is 8192 bytes. 886 is under that bound.

The hole check and the byte-read check are still asserted after the signature, along with the long `/ByteRange` offsets.

## 6. Changes made or proposed

`test/jaytwo.Ergonomics.Pdf.Tests/ScaleTests.cs` records `sparse.Length` around `SaveIncremental` and asserts the difference is under `8 * 1024`. The test writes the byte count. The scale row was not changed.

## 7. Verification run

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle" --nologo --logger "console;verbosity=detailed"
```

SDK `10.0.100-rc.1.25451.107`. Output: `metadata appendix 886`. Result: Passed, 1, Failed 0. Duration 72 ms. `jaytwo.Ergonomics.Pdf.Tests.dll` (`net8.0`).

Evidence level: internally tested. The rest of the suite was not run.

## 9. Handoff

QA Engineer verifies this test. The appendix is under 8 KiB, so there is no defect report.
