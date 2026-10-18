# B-11. Revision 4 stream cipher evidence

## Task and owner

QA Engineer. Read-only except this report. `EncryptionTests.cs` was not edited. No `agents/state/` edits.

**Signed off.** Opening the revision 4 file with password `user` returns the title from the AES string cipher and the embedded-file bytes from the RC4 stream cipher. Evidence required: internally tested.

## Verified facts

- `revision4_can_mix_aes_strings_with_rc4_streams` still asserts the title only and still has no stream.
- `revision4_rc4_stream_decrypts_beside_an_aes_string` builds revision 4 with `/StrF /Aes` (`AESV2`) and `/StmF /Rc4` (`V2`) and no `/EFF`. Object 4's title is AES-encrypted. Object 5 is an embedded-file stream whose bytes are RC4 of a 40-byte pattern, using `EncryptRc4` and the object key without `sAlT`. The test asserts those two ciphertexts of the same bytes and object number differ.
- `PdfDocument.Open` with password `user` returns title `Secret`. `Attachments["notes.bin"].Open()` returns those 40 plaintext bytes.
- The opening row in `ACCEPTANCE.md` is unchanged. `git diff` of that file does not contain the crypt-filter sentence.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~revision4_rc4_stream_decrypts_beside_an_aes_string" --nologo`

Passed 1, failed 0, skipped 0. Duration 51 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~revision4_rc4_stream_decrypts_beside_an_aes_string" --nologo`

Passed 1, failed 0, skipped 0. Duration 58 ms. Target `net6.0`.

Not run, and not counted as passes: the rest of `EncryptionTests`, the rest of the suite, and `netstandard2.0` / `netstandard2.1`.

## Changes made or proposed

This report only. `dotnet test` wrote gitignored coverage under `out/coverage/` because `test/Directory.Build.props` sets `CollectCoverage`.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone.
