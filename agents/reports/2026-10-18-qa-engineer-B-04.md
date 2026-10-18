# B-04. Nonce and matching timestamp token

## Task and owner

QA Engineer. Read-only except this report. No product code, `README.md`, `ACCEPTANCE.md`, or `agents/state/` edits. B-09 was not started.

**Signed off.** The B-04 acceptance criteria are met at the evidence level the item asks for: internally tested, plus the existing independent TSA round trip.

## Verified facts

- `Rfc3161TimeStampRequest.Create(ReadOnlySpan<byte>)` still builds a version-1 request with no nonce and `certReq` omitted (`Rfc3161TimeStampRequest.cs`). `rfc3161_request_matches_the_sha256_abc_vector` equates `Create(SHA-256("abc"))` to the committed hex constant. That constant is 112 hex digits, 56 bytes, and its imprint is SHA-256("abc") (`ba7816bf…15ad`). The test passed on `net8.0` and `net6.0`.
- The nonce overload appends one INTEGER and still omits `certReq`. `NewNonce` draws 8 bytes from `RandomNumberGenerator`. A high bit produces a DER leading `0x00`, so the encoded INTEGER content is 8 or 9 bytes. `AssertNonceWidth` allows that.
- `CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider` each call `NewNonce`, `Create(imprint, nonce)`, and `Rfc3161TimeStampToken.RequireMatch` before returning contents. `HttpTimestampAuthority` does not perform the match. That matches the B-04 scope.
- `RequireMatch` throws `PdfTimestampException` when the TSTInfo imprint octets differ, when the imprint hash OID is not SHA-256 (`2.16.840.1.101.3.4.2.1`), or when the nonce is missing or differs as an unsigned integer. The algorithm fault in the tests uses OID `1.3.14.3.2.26`.
- `PdfEngine.Sign` calls `CreateContents` and writes the destination only after that call returns (`PdfEngine.cs`). `mismatched_timestamp_imprint_does_not_extend_the_file`, `mismatched_timestamp_digest_algorithm_does_not_extend_the_file`, and `missing_or_mismatched_timestamp_nonce_does_not_extend_the_file` each throw `PdfTimestampException` for `DocTimeStampSignatureProvider` and `CmsDetachedSignatureProvider` and assert `stream.Length` is unchanged. Those three facts passed on both frameworks.
- `ACCEPTANCE.md` has the three throw-and-do-not-extend rows, and the request-encoding row describes the nonce and the unchanged 56-byte vector. The `README.md` timestamp paragraph describes the random nonce, omitted `certReq`, and the same three rejections.
- `git diff` and `git status` for `test/jaytwo.Ergonomics.Pdf.Tests/Fixtures/tsa_server.py` are empty.
- `pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature` passed on `net8.0`. `validate_signature.py` prints `timestamp_intact`, `timestamp_valid`, and `timestamp_trusted`, and exits unless all three are true. `ExternalTool.Output` fails the test on a non-zero exit. The test also asserts `timestamp_intact=true` and `timestamp_trusted=true`. pyHanko 0.38.0 established that the embedded RFC 3161 token is intact, valid, and trusted against the local timestamp authority.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~SignatureTests" --nologo`

Passed 24, failed 0, skipped 0. Duration 529 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~SignatureTests" --nologo`

Passed 24, failed 0, skipped 0. Duration 577 ms. Target `net6.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature" --nologo`

Passed 1, failed 0, skipped 0. Duration 2 s. Target `net8.0`. pyHanko 0.38.0, imported by `python` on `PATH`. qpdf 12.4.2, Poppler, and SoftHSM 2.5.0 were still at the B-02 paths under `%LOCALAPPDATA%`. This filter did not invoke them.

Not run, and not counted as passes: the rest of the suite, `SigningInteropTests` other than the local-TSA fact, that fact on `net6.0`, and `netstandard2.0` / `netstandard2.1`.

## Changes made or proposed

This report only. `dotnet test` wrote gitignored coverage under `out/coverage/` because `test/Directory.Build.props` sets `CollectCoverage`.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone.
