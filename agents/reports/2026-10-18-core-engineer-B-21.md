# B-21 Remaining acceptance clauses

## 1. Task and owner

Assertions for the three Core Engineer clauses in B-21. Core Engineer. No `src/` edits. No commit. The acceptance rows were not changed.

## 2. Verified facts

1. **Header window.** A `%PDF-1.7` header that starts at offset 1016 opens and the title is read. A header that starts at offset 1024 throws `PdfParseException`. Test: `OpenTests.header_after_the_first_1024_bytes_is_not_the_header`.

2. **Revision 2 fill-in bit.** `StandardEncryptor.FillOnly` sets the fill-in bit and leaves annotate clear. The file has a text field. The user password reports `CanAnnotate` and `CanFillForms` false. `SetText` throws `PdfEncryptionRestrictedException` and the save does not grow the stream. Test: `EncryptionTests.revision2_fill_in_without_annotate_cannot_fill_a_field`.

3. **`/ID` on an encrypted incremental save.** The appendix contains `0102030405060708090A0B0C0D0E0F10`. `rc4_round_trips_strings_streams_and_the_prefix` covers revision 2, revision 3 at 40 and 128 bits, and revision 4 RC4. `revision5_round_trips_and_keeps_the_password_bytes` covers revision 5.

## 6. Changes made or proposed

- `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs`: the header-window test.
- `test/jaytwo.Ergonomics.Pdf.Tests/EncryptionTests.cs`: the revision 2 fill test, and the `/ID` assertion on the RC4 and revision 5 saves. `Rc4Document` takes the permission bits so the fill test can build a revision 2 file.

## 7. Verification run

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --nologo --filter "FullyQualifiedName~header_after_the_first_1024_bytes_is_not_the_header|FullyQualifiedName~revision2_fill_in_without_annotate_cannot_fill_a_field|FullyQualifiedName~rc4_round_trips_strings_streams_and_the_prefix|FullyQualifiedName~revision5_round_trips_and_keeps_the_password_bytes"
```

SDK `10.0.100-rc.1.25451.107`. Result: Passed 7, Failed 0, Skipped 0. Duration 140 ms. `jaytwo.Ergonomics.Pdf.Tests.dll` (`net8.0`). The 7 rows are the header test, the revision 2 fill test, four RC4 rows, and the revision 5 save.

Evidence level: internally tested. The rest of the suite was not run.

## 9. Handoff

QA Engineer verifies these tests. All three clauses passed, so there is no defect report.
