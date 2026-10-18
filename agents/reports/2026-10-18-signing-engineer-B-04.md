# B-04 Nonce and matching timestamp token

## 1. Task and owner

Signing Engineer. Nonce on provider requests, and a token check in both providers. No commit. `Fixtures/tsa_server.py` was not changed. pyHanko's `DummyTimeStamper` copies `req['nonce']` into TSTInfo.

## 2. Verified facts

`Rfc3161TimeStampRequest.Create(ReadOnlySpan<byte>)` is unchanged. `rfc3161_request_matches_the_sha256_abc_vector` passed, so SHA-256("abc") is still the 56-byte vector. `certReq` stays omitted. The new overload places a nonce INTEGER after the imprint. A nonce whose high bit is set is encoded with a leading `0x00` (`DerCodec.Integer(ReadOnlySpan<byte>)`).

`CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider` each draw 8 random bytes and call that overload. After `GetTimeStampToken`, `Rfc3161TimeStampToken.RequireMatch` parses the CMS SignedData, reads TSTInfo, and throws `PdfTimestampException` unless all three match:

- message imprint octets
- imprint hash algorithm, which must be SHA-256 (`2.16.840.1.101.3.4.2.1`)
- nonce, compared as an unsigned integer

The CMS `SignedData` digest-algorithm set is not compared. It is not a field of the request. `ITimestampAuthority` and `PdfEngine` were not changed. `Sign` still writes the destination only after `CreateContents` returns, so these throws do not extend the file.

An empty token now throws from both providers. It previously threw only from the CMS provider.

## 3. Assumptions

"Digest algorithm" means the hash algorithm inside the token's `MessageImprint`. That is the algorithm the request sends.

## 5. Evidence missing

`SignatureTests` was not run on `net6.0`. No other foreign or interop test was run.

## 6. Changes made or proposed

- `src/jaytwo.Ergonomics.Pdf/Signatures/Rfc3161TimeStampRequest.cs`
- `src/jaytwo.Ergonomics.Pdf/Signatures/Rfc3161TimeStampToken.cs` (new internal type)
- `src/jaytwo.Ergonomics.Pdf/Signatures/CmsDetachedSignatureProvider.cs`
- `src/jaytwo.Ergonomics.Pdf/Signatures/DocTimeStampSignatureProvider.cs`
- `src/jaytwo.Ergonomics.Pdf/Signatures/DerCodec.cs`
- `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs`
- `README.md` timestamp paragraph
- `ACCEPTANCE.md` request-encoding row, plus three rows for a bad imprint, a bad digest algorithm, and a missing or mismatched nonce

## 7. Verification run

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~SignatureTests" --nologo`

Passed 24, failed 0, skipped 0. Duration 712 ms. SDK `10.0.100-rc.1.25451.107`, target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature" --nologo`

Passed 1, failed 0. Duration 2 s. pyHanko 0.38.0 reported the token intact, valid, and trusted. The fixture was not edited.

`dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj --nologo -v q`

Succeeded, 0 warnings, 0 errors, all four target frameworks.

## 9. Handoff

QA Engineer verifies. Technical Lead reviews. The Product Manager is not asked to accept this.
