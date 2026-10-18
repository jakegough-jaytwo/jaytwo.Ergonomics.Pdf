# B-21 clauses 8 and 9

## Task and owner

QA Engineer. Read-only except this report. No product-code edits, no project-file edit, and no new package. No `agents/state/` edits.

**Clause 8: signed off.** **Clause 9: signed off.**

## Verified facts

Clause 8. `sign_to_another_stream_leaves_the_opened_stream_unchanged` opens a writable `MemoryStream`, calls `Sign` on a different destination, and asserts `source.ToArray()` still equals the input. The destination is longer than the input, and its prefix equals the input.

Clause 9. The citations match the source.

- `cms_signature_verifies_and_covers_the_byte_range` calls `cms.CheckSignature(verifySignatureOnly: true)` at `SignatureTests.cs` line 342, inside `#if NET8_0_OR_GREATER` (lines 336–345).
- `cms_embeds_the_certificate_chain_and_a_timestamp_over_the_signature_value` calls it at line 391, inside `#if NET8_0_OR_GREATER` (lines 380–394).
- `using System.Security.Cryptography.Pkcs` is inside `#if NET8_0_OR_GREATER` (lines 7–9). `MessageDigest(SignedCms)` is inside the same symbol (lines 625–638).
- `jaytwo.Ergonomics.Pdf.Tests.csproj` line 13 references `System.Security.Cryptography.Pkcs` 10.0.13 only when `TargetFramework` is `net8.0`. The test project targets `net8.0` and `net6.0`. The `net6.0` test build succeeded without that package.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~sign_to_another_stream_leaves_the_opened_stream_unchanged" --nologo`

Passed 1, failed 0, skipped 0. Duration 50 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~sign_to_another_stream_leaves_the_opened_stream_unchanged" --nologo`

Passed 1, failed 0, skipped 0. Duration 53 ms. Target `net6.0`.

Clause 9 was confirmed in source. No new test was added for it.

Not run, and not counted as passes: the rest of `SignatureTests`, and the rest of the suite.

## Changes made or proposed

This report only.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone.
