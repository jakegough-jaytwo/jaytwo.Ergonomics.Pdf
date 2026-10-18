# B-19. External signer and timestamp failures

## Task and owner

QA Engineer. Read-only except this report. No product-code edits. No commit.

**Signed off.** A throwing external signer leaves the destination unchanged. A throwing timestamp authority throws and does not leave a timestamped signature. Evidence required: internally tested. Gates G4, G5, and G9 are not passed by this report.

## Verified facts

- `external_signer_failure_does_not_extend_the_file` uses `ThrowingSigner.SignHash`, which throws `CryptographicException`. `Sign` throws that exception. The opened stream length and bytes stay equal to the input.
- `timestamp_authority_failure_does_not_leave_a_timestamped_signature` uses a real RSA `SignHash` and `ThrowingAuthority.GetTimeStampToken`, which throws `PdfTimestampException`. `Sign` throws that exception. The stream bytes stay equal to the input. A reopen finds no signature field. This is not an imprint, digest-algorithm, or nonce rejection.
- `PdfEngine.Sign` calls `CreateContents` before it writes the appendix, so neither throw reaches that write.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~external_signer_failure_does_not_extend_the_file|FullyQualifiedName~timestamp_authority_failure_does_not_leave_a_timestamped_signature" --nologo`

Passed 2, failed 0, skipped 0. Duration 245 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~external_signer_failure_does_not_extend_the_file|FullyQualifiedName~timestamp_authority_failure_does_not_leave_a_timestamped_signature" --nologo`

Passed 2, failed 0, skipped 0. Duration 192 ms. Target `net6.0`.

Not run, and not counted as passes: the rest of `SignatureTests`, and the rest of the suite.

## Changes made or proposed

This report only.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone.
