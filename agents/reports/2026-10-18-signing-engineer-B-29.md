# B-29 verification: bound the two confirmed signer paths

## 1. Task and owner

Signing Engineer. Verify the B-29 code and tests committed at `ee10c84` against the B-29 acceptance criteria. Make a minimal fix only if verification finds a defect. G8 is not passed.

## 2. Verified facts

- `DerCodec.ReadTlv` (`src/jaytwo.Ergonomics.Pdf/Signatures/DerCodec.cs`, line 156) checks `length < 0 || length > data.Length - index`. It subtracts instead of adding, so a long-form length of `0x7FFFFFFF` cannot wrap the check. Before B-29, the check was `index + length > data.Length`.
- `HttpTimestampAuthority.ExtractToken` (`src/jaytwo.Ergonomics.Pdf/Signatures/HttpTimestampAuthority.cs`, line 65) rejects `code < 0 || code > 1` with `PdfTimestampException`. Before B-29, the check was `code > 1`.
- `overflowing_der_length_does_not_extend_the_file` discriminates as committed. With the DER check reverted, it fails on net8.0 and net6.0 because the code throws `ArgumentOutOfRangeException` where the test expects `PdfTimestampException`. With the fix, it passes.
- These are the only two lines in the two src files that bear on B-29. The `DerCodec.Integer(ReadOnlySpan<byte>)` overload also in the diff is nonce work and is not part of B-29.

## 4. Defects confirmed

- `negative_timestamp_status_does_not_extend_the_file` did not discriminate as committed. With the status check reverted to `code > 1`, it still passed on net8.0 and net6.0. The response's stand-in token `SEQUENCE { INTEGER 7 }` fails later in the provider with `PdfTimestampException` ("The timestamp token is not a time-stamp token."), so asserting only the exception type and that the file is unchanged did not exercise the status check.

## 6. Changes made

`test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs` only:
- `AssertTimestampResponseDoesNotExtend` now takes a `phrase` and asserts that the `PdfTimestampException` message contains it. This follows the existing `RejectTimestamp` pattern.
- The negative-status test passes `"rejected the request with status"`. The overflow test passes `"exceeds its buffer"`.

No src changes. `git diff` for `src/jaytwo.Ergonomics.Pdf/Signatures` is clean after each temporary revert.

## 7. Verification run

Filter `FullyQualifiedName~SignatureTests`, .NET SDK test runner. Each revert was applied with `sed` and restored with `git checkout`.

| State | net8.0 | net6.0 |
|---|---|---|
| Fixes present | 31/31 pass | 31/31 pass |
| Status check reverted | negative-status test fails, message lacks the phrase | same |
| DER check reverted | overflow test fails, `ArgumentOutOfRangeException` | same |

`dotnet build src/jaytwo.Ergonomics.Pdf -c Release -warnaserror` built netstandard2.0, netstandard2.1, net6.0, and net8.0 with 0 warnings and 0 errors. The test project also built with `-warnaserror` and 0 warnings. The library build includes other seats' uncommitted `PdfEngine.cs` and `PdfParser.cs` changes, which I did not touch.

## 9. Handoff

QA Engineer verifies. Technical Lead reviews the test change. G8 is not passed.
