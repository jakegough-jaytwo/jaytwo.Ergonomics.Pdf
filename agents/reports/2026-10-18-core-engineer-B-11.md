# B-11 Revision 4 stream cipher evidence

## 1. Task and owner

Evidence that a revision 4 stream uses a different cipher from strings. Core Engineer. No `src/` edits. No commit. Branch stayed `wip`.

## 2. Verified facts

`EncryptionTests.revision4_can_mix_aes_strings_with_rc4_streams` still asserts the title only and still has no stream.

The new file is revision 4, `/StrF /Aes` (`AESV2`) and `/StmF /Rc4` (`V2`), with no `/EFF`. Object 4's title is AES-encrypted with the `sAlT` object key. Object 5 is an embedded-file stream whose bytes are RC4 of a 40-byte pattern, using the same file key and the object key without `sAlT`. Those two ciphertexts of the same bytes and object number are not equal. Name-tree and filespec strings are AES-encrypted for their own object numbers.

`PdfDocument.Open` with password `user` returns title `Secret` and, from `Attachments["notes.bin"].Open()`, the 40 RC4 plaintext bytes.

## 3. Assumptions

None. The stream cipher is the one `StmF` names because `/EFF` is absent, so an embedded file uses the stream crypt filter.

## 6. Changes made or proposed

- `test/jaytwo.Ergonomics.Pdf.Tests/StandardEncryptor.cs`: `EncryptRc4` encrypts with the revision 4 RC4 object key of an existing file key.
- `test/jaytwo.Ergonomics.Pdf.Tests/EncryptionTests.cs`: `revision4_rc4_stream_decrypts_beside_an_aes_string`.
- `PdfBuilder.cs` was not changed. `StreamObject` and `AppendXrefStream` already write the file.

The opening row in `ACCEPTANCE.md` was not changed.

## 7. Verification run

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~revision4_rc4_stream_decrypts_beside_an_aes_string" --nologo
```

SDK `10.0.100-rc.1.25451.107`. Result: Passed, 1, Failed 0, Skipped 0. Duration 54 ms. `jaytwo.Ergonomics.Pdf.Tests.dll` (`net8.0`).

Evidence level: internally tested. The rest of the suite was not run.

## 9. Handoff

QA Engineer verifies this test. The stream decrypted, so there is no defect report.
