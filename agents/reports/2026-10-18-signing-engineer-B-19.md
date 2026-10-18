# B-19 External signer and timestamp failures

## 1. Task and owner

Signing Engineer. Two failure tests in `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs`. No `src/` edit. No commit.

## 2. Verified facts

Both cases pass. The destination is not extended, and `Sign` does not return.

- `external_signer_failure_does_not_extend_the_file`. `ThrowingSigner.SignHash` throws `CryptographicException` from `CmsDetachedSignatureProvider`. `Sign` throws that exception. The opened stream length and bytes stay equal to the input.
- `timestamp_authority_failure_does_not_leave_a_timestamped_signature`. `ThrowingAuthority.GetTimeStampToken` throws `PdfTimestampException` after a real RSA `SignHash`. `Sign` throws that exception. The stream bytes stay equal to the input. Reopening shows no signature field. This is not an imprint, digest-algorithm, or nonce rejection.

`PdfEngine.Sign` calls `CreateContents` before it writes the appendix. Neither failure reaches that write.

## 3. Assumptions

A timestamp-authority failure here is the authority throwing. B-04 already covers a token whose imprint, digest algorithm, or nonce does not match.

## 6. Changes made or proposed

`test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs` only. Added the two facts, `ThrowingSigner`, and `ThrowingAuthority`.

## 7. Verification run

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~external_signer_failure_does_not_extend_the_file|FullyQualifiedName~timestamp_authority_failure_does_not_leave_a_timestamped_signature" --nologo`

Passed 2, failed 0, skipped 0. Duration 204 ms. SDK `10.0.100-rc.1.25451.107`, target `net8.0`. The rest of the suite was not run.

## 9. Handoff

QA Engineer verifies these tests. Technical Lead reviews. No product defect to fix.
