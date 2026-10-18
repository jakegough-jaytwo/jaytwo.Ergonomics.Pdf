# B-27. Signer review

## 1. Task and owner

AcroForm Engineer. Read-only review of the signature providers, CMS and DER encoding, timestamp request and token handling, and the incremental sign path. Nothing in `src/`, the tests, or `agents/state/` was changed. G8 is not passed. B-18 stays open.

## 2. Verified facts

Files read: `Signatures/CmsDetachedSignatureProvider.cs`, `DocTimeStampSignatureProvider.cs`, `Rfc3161TimeStampRequest.cs`, `Rfc3161TimeStampToken.cs`, `HttpTimestampAuthority.cs`, `DerCodec.cs`, `PdfSignatureContext.cs`, `PdfSignatureOptions.cs`, `IPdfExternalSigner.cs`, `IPdfSignatureProvider.cs`, `ITimestampAuthority.cs`; `Internal/PdfEngine.cs` (`Sign`, `BuildSignature`, `SealExistingField`, `CreateSignatureDictionary`, `WriteAppendix`, `OpenSigned`, `FinishWrite`, `Restore`, `ValidateDestination`, `CopyPrefix`, `EnsureSigningAllowed`); `Internal/PdfSerializer.cs` (object writing, `/ByteRange`, `/Contents`, `IsSignature`); `Internal/PdfSecurity.cs` (`IsExempt`, `EncryptsStringsOf`, `CipherFor`); `Internal/SplicedStream.cs`; `Internal/PdfTextCodec.cs` (`FormatDate`, `ToUpperHex`). Also `agents/team.md` section 7, `agents/state/board.md`, the B-27 and B-18 backlog entries, D-14, the sealing rows in `ACCEPTANCE.md`, and `QUALIFICATION.md` section 10. No `CheckForOverflowUnderflow` setting in the project files.

`Sign` builds the appendix in a buffer, patches `/ByteRange` as four 10-digit integers, and hashes through `OpenSigned`. The gap is `<` plus the reserved hex plus `>`. `SplicedStream` omits that gap. `/Contents` is patched only after `CreateContents` returns, and `destination.Write` is after that. A throw from the provider, including an oversized token, hits `Restore` before the destination write. That matches the accepted throwing-signer behavior.

`CmsDetachedSignatureProvider` puts the signed-range SHA-256 in the `messageDigest` attribute, signs the SHA-256 of the DER SET of signed attributes (tag `0x31`), and stores that SET with the tag replaced by `0xA0`. An optional timestamp is unsigned attribute `1.2.840.113549.1.9.16.2.14` over the SHA-256 of the RSA signature value. `DocTimeStampSignatureProvider` timestamps the signed-range hash. Both call `Rfc3161TimeStampRequest.Create(imprint, nonce)` with `NewNonce` (8 random bytes) and then `Rfc3161TimeStampToken.RequireMatch`. Neither provider uses the no-nonce `Create` overload. This does not contradict the accepted nonce or imprint work.

A signature dictionary is `/Type /Sig` or `/DocTimeStamp`. `IsSignature` and `IsExempt` leave its strings unencrypted. `/Contents` is a hex slot, not an encrypted string. `/M` and the CMS signing-time attribute are the same `DateTimeOffset`; the CMS value is UTC.

`ExtractToken` rejects a positive decoded status greater than 1. That covers the accepted status 0 and status 2 cases. Status 1 (`grantedWithMods`) also passes that comparison. `RequireMatch` still checks the imprint and the nonce.

## 3. Assumptions

Integer math in `DerCodec` is the C# unchecked default. A token that survives `ExtractToken` is still passed to `RequireMatch` by both providers. The caller chooses the timestamp URL.

## 4. Defects confirmed

1. **A negative PKIStatus is treated as success.** `HttpTimestampAuthority.ExtractToken` throws only when `DerCodec.ReadInteger` returns a value greater than 1. `ReadInteger` folds the content into a signed 32-bit int and allows at most 4 content bytes. A 4-byte INTEGER whose first content byte is `0x80` or higher decodes as negative (`02 04 80 00 00 00` becomes `int.MinValue`). A negative value is not greater than 1, so extraction continues and returns the following ContentInfo. Status values 0 and 1, and any positive value that still fits in a signed int, are classified as the comparison says. The imprint and nonce checks are unchanged. A negative status is not granted or grantedWithMods.

2. **A long-form DER length can miss the bounds check.** `DerCodec.ReadTlv` rejects a length when `length < 0` or `index + length > data.Length`. The addition is unchecked. A 4-byte length of `0x7FFFFFFF` makes `index + length` wrap negative, the comparison does not throw, and `ReadOnlySpan<byte>.Slice` then throws `ArgumentOutOfRangeException` instead of `PdfTimestampException`. The slice fails before a buffer of that length is allocated. On the sign path this is inside `CreateContents`, so the destination write has not started.

## Observations

These are not confirmed defects.

- The timestamp response is copied with `CopyTo` and no maximum. No sealing row states a body cap. The endpoint is caller-chosen.
- `Post` and `OpenResponse` block on `GetResult`.
- SignedData is version 1 while the digest algorithm is SHA-256, and `AlgorithmSha256` includes NULL. That is the CMS encoding already accepted by the pyHanko and `SignedCms` checks. Not reopened.
- TSTInfo version is not checked. After `genTime`, the walk skips a SEQUENCE or a BOOLEAN and treats the first INTEGER as the nonce, then returns. A `tsa` field after that nonce is not read. A token with `tsa` and no nonce fails closed.
- `Restore` does not truncate the destination. The appendix is written only after `CreateContents` returns.
- The library does not check the TSA signature on the token. D-14 and G5 leave TSA trust to the independent verifier.

## 5. Evidence missing

No test command was run. The two defects are from reading the code. The negative-status and overflowing-length inputs were not executed.

## 6. Changes made or proposed

None.

## 7. Verification run

No commands.

## 9. Handoff

Technical Lead reconciles this report. G8 is not passed. B-18 stays open. This review does not replace B-18.
