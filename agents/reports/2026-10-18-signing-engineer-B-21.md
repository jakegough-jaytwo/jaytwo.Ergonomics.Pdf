# B-21 Clauses 8 and 9

## 1. Task and owner

Signing Engineer. Clause 8 asserts `Sign(Stream, ...)` leaves the opened stream unchanged. Clause 9 records that `SignedCms` stays on `net8.0`. No `src/` edit. No project-file edit. No new package. No commit.

## 2. Verified facts

Clause 8 passes. The opened stream's bytes do not change.

- `ScaleTests.cross_stream_save_copies_in_one_megabyte_reads` covers `SaveIncremental(Stream)` only.
- `sign_to_another_stream_leaves_the_opened_stream_unchanged` opens a writable `MemoryStream`, calls `PdfDocument.Sign(Stream, PdfSignatureOptions, IPdfSignatureProvider, leaveOpen)`, and asserts the source bytes still equal the input. The destination is longer than the input and its prefix equals the input.

Clause 9 needs no code change. `SignedCms.CheckSignature(verifySignatureOnly: true)` is already compiled only for `net8.0`.

- `cms_signature_verifies_and_covers_the_byte_range` calls it at `SignatureTests.cs` line 342, inside `#if NET8_0_OR_GREATER` (lines 336–345).
- `cms_embeds_the_certificate_chain_and_a_timestamp_over_the_signature_value` calls it at line 391, inside `#if NET8_0_OR_GREATER` (lines 380–394).
- `using System.Security.Cryptography.Pkcs` is inside `#if NET8_0_OR_GREATER` (lines 7–9). `MessageDigest(SignedCms)` is inside the same symbol (lines 625–638).
- `jaytwo.Ergonomics.Pdf.Tests.csproj` line 13 references `System.Security.Cryptography.Pkcs` 10.0.13 only when `TargetFramework` is `net8.0`. The test project targets `net8.0` and `net6.0`. The `net6.0` build does not get that package, and the calls above are not compiled for it.

## 6. Changes made or proposed

`test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs` only. Added `sign_to_another_stream_leaves_the_opened_stream_unchanged`.

## 7. Verification run

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~sign_to_another_stream_leaves_the_opened_stream_unchanged" --nologo`

Passed 1, failed 0, skipped 0. Duration 54 ms. SDK `10.0.100-rc.1.25451.107`, target `net8.0`. The rest of the suite was not run. Clause 9 was inspected in source. It was not given a new test or package.

## 9. Handoff

QA Engineer verifies the new test and the clause 9 citations. Technical Lead reviews. No product defect to fix.
