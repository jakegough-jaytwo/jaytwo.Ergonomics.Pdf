# B-31 boarding

## 1. Task and owner

Technical Lead. Board B-31, "Cap the timestamp response size". Implementer: Signing Engineer. Verifier: QA Engineer. Branch `wip`, no worktree. Tasks run one at a time on `wip`. No code or test edits. No test runs. No commit. G8 is not passed.

## 2. Verified facts

B-31 is in the charter. It bounds an existing read and adds no capability, no package, and no public API. These facts come from reading the code. I did not execute it.

- `HttpTimestampAuthority.GetTimeStampToken` (`HttpTimestampAuthority.cs:43-46`) copies the response stream into a `MemoryStream` with no cap. It then calls `ToArray`.
- **Upstream buffering.** `Post` (`:87`) calls `_httpClient.SendAsync(request)` with no `HttpCompletionOption`. The default is `ResponseContentRead`. Under that option, `HttpClient` loads the whole body into its own buffer before `SendAsync` returns. The only limit is `MaxResponseContentBufferSize`, which defaults to 2 GB. A cap on the later copy alone would not bound memory. The boundary therefore includes the send.
- **Timeout.** Today `HttpClient.Timeout` covers the whole body, because the body is read inside `SendAsync`. With `ResponseHeadersRead`, the timeout ends when the headers arrive. A server that stalls the body would then hang the read. The fix must keep the body read under that timeout.
- **Embedding ceiling.** `PdfEngine.Sign` (`PdfEngine.cs:905`) limits `ReservedContentsBytes` to 1,048,576 binary bytes. A token larger than 1 MiB can never be embedded, by either `CmsDetachedSignatureProvider` or `DocTimeStampSignatureProvider`.
- **Existing tests.** `SignatureTests.RecordingHandler` (`:1124`) and `AssertTimestampResponseDoesNotExtend` (`:759`) already show how to test a refusal that leaves the file unextended. That refusal uses `DocTimeStampSignatureProvider`.

## 6. Changes proposed

**Cap.** The response body is capped at 2 MiB (2,097,152 bytes). This sits above the 1 MiB embedding ceiling plus any plausible `PKIStatusInfo`, so no token that could be embedded is refused. It is a quarter of the 8 MiB appendix.

**Buffer.** Read the body with a fixed 81,920-byte buffer. Reading at most "the cap plus one buffer" means at most 2,179,072 bytes. Keep both values as private constants.

**`src/jaytwo.Ergonomics.Pdf/Signatures/HttpTimestampAuthority.cs`:**

- Call `SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)`.
- Read the body with `ReadAsync` under the same token, looping into a `MemoryStream`. When the total exceeds the cap, throw `PdfTimestampException` with a message that contains "larger than".
- Make the token a `CancellationTokenSource` set to `_httpClient.Timeout`. Use no timeout when that is `Timeout.InfiniteTimeSpan`. This keeps the whole exchange under the client's timeout, as today. A timeout still surfaces as `OperationCanceledException`, as today.
- Optional: refuse before reading when `Content-Length` exceeds the cap.
- Do not mutate the caller's `HttpClient`. That rules out `MaxResponseContentBufferSize`. Keep the existing sync-over-async pattern, which `netstandard2.0` needs.
- No public API change. Status, nonce, imprint, and digest-algorithm handling are unchanged.

**`test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs`:** add a handler whose response is a `StreamContent` over a test stream. The stream is non-seekable, so there is no `Content-Length`. It generates 16 MiB on demand without allocating it, and it counts the bytes returned from `Read` and `ReadAsync`. Use literal numbers in the tests, not the library constants. Required cases:

1. **Oversized body.** Call `GetTimeStampToken` directly. Assert `PdfTimestampException`, a message that contains "larger than", and a count of at most 2,179,072. On today's code the count is the full 16 MiB, because `HttpClient` buffers it all. Show it red on today's code.
2. **Oversized body through `Sign`.** Use `DocTimeStampSignatureProvider`. Assert `PdfTimestampException` and stream bytes equal to the input, so the file is not extended.
3. **Boundary.** A well-formed response of exactly 2,097,152 bytes returns its token. Pad the token with an OCTET STRING. A body of 2,097,153 bytes is refused. Call `GetTimeStampToken` directly, because this token will not fit `/Contents`.
4. **Timeout guard.** Use a client with `Timeout` set to 500 ms. Its body stream's `ReadAsync` waits on the cancellation token and never yields data. Assert `OperationCanceledException` (use `ThrowsAny`) within 10 seconds. This test passes on today's code too. It guards against a regression from `ResponseHeadersRead`. It is not a red-first case.

The existing `http_timestamp_authority_*` tests, the timestamp-status tests, and the DER tests must still pass on `net8.0` and `net6.0`.

**`README.md` and `ACCEPTANCE.md`:** add one sentence each where `HttpTimestampAuthority` is described (README line 27, ACCEPTANCE line 56). The sentence states that a response body over 2 MiB throws `PdfTimestampException`. Weaken no criterion.

Stop at the boundary and report if the fix needs any other file, including `CmsDetachedSignatureProvider.cs`, `DocTimeStampSignatureProvider.cs`, or `DerCodec.cs`.

## Assumptions

- `HttpClient` in `net6.0` and `net8.0` stops applying `Timeout` once the headers arrive under `ResponseHeadersRead`. The timeout guard test settles this either way.
- On .NET Framework through `netstandard2.0`, the network stream may ignore the cancellation token on `ReadAsync`. The test projects do not run that runtime. If the implementer confirms this, record it as a limitation.

## 9. Handoff

- **Signing Engineer:** implement within the four claimed files and write the report, with red-before-green for case 1.
- **QA Engineer:** verify independently, including the byte count against 2,179,072.
- **Technical Lead:** review.

G8 is not passed.
