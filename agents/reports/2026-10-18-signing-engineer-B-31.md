# B-31 Signing Engineer report

## 1. Task and owner

Signing Engineer. B-31, "Cap the timestamp response size", within the boundary in `agents/reports/2026-10-18-tech-lead-B-31-board.md`. Branch `wip`, uncommitted. G8 is not passed.

## 2. Verified facts

- Before the fix, `HttpClient` buffered the whole body inside `SendAsync`. A 16 MiB non-seekable body was read in full: 16,777,216 bytes on `net8.0` and `net6.0`.
- After the fix, the same body is refused after 2,129,920 bytes (26 reads of 81,920). A 2,097,153-byte body is refused after 2,097,153 bytes. A 2,097,152-byte body returns its token. All are within 2,179,072.

## 4. Defects confirmed

The unbounded response read in `HttpTimestampAuthority.GetTimeStampToken`. It was observed in the B-29 review and is now executed: see the red run below.

## 6. Changes made

- `src/jaytwo.Ergonomics.Pdf/Signatures/HttpTimestampAuthority.cs`: `SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)`. The body is read with `ReadAsync` into a fixed 81,920-byte buffer. A read that would take the total past 2,097,152 bytes throws `PdfTimestampException` ("...is larger than 2097152 bytes."), so the `MemoryStream` never holds more than the cap. A declared `Content-Length` over the cap is refused before the body is opened. The token is a `CancellationTokenSource(_httpClient.Timeout)` covering the send and the body read. `InfiniteTimeSpan` stays infinite. The caller's `HttpClient` is not mutated. A single sync-over-async block replaces the two that were there before. There is no public API change. Status, nonce, imprint, and digest-algorithm handling are unchanged.
- `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs`: five tests, plus `StreamHandler`, `GeneratedStream` (a non-seekable stream that generates bytes and counts them), and `StallingStream`. `AssertTimestampResponseDoesNotExtend` gains a handler overload. The tests are `oversized_timestamp_response_is_refused_after_a_bounded_read`, `oversized_timestamp_response_does_not_extend_the_file` (`DocTimeStampSignatureProvider` through `Sign`, stream bytes unchanged), `declared_oversized_timestamp_response_is_refused_before_reading` (0 bytes read), `timestamp_response_at_the_cap_returns_its_token_and_one_byte_more_is_refused`, and `stalled_timestamp_response_body_still_times_out` (500 ms timeout, under 10 s).
- `README.md` line 27 and `ACCEPTANCE.md` line 56: one sentence each on the 2 MiB cap.

## 7. Verification run

- Red, with the tests added and the source unfixed: `dotnet test test/jaytwo.Ergonomics.Pdf.Tests --filter "FullyQualifiedName~SignatureTests" -f net8.0`, then `-f net6.0`. Each recorded 4 failed and 32 passed. The bounded-read case read 16,777,216 bytes. The declared case read 16,777,216 bytes. The one-byte-over case returned a token. The `Sign` case failed with "not a DER sequence". The stall test passed, as expected for a guard.
- Green, after the fix, with the same commands: 36/36 on `net8.0` and 36/36 on `net6.0`.
- `dotnet build src/jaytwo.Ergonomics.Pdf -c Release --no-incremental`: `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0` built with 0 warnings and 0 errors.
- The full suite was not run.

## Limitations

- On .NET Framework through `netstandard2.0`, a network stream may ignore the cancellation token in `ReadAsync`. The rework below addresses this.
- Peak memory is the 2 MiB `MemoryStream` plus its `ToArray` copy and the 80 KiB buffer.

## Rework

This rework answers `2026-10-18-tech-lead-B-31-review.md`, section 6.

- `HttpTimestampAuthority.cs`: `SendAsync(request, ResponseHeadersRead)` no longer passes the library's token, so `HttpClient` times the header phase with its own exception. The `CancellationTokenSource(_httpClient.Timeout)` is still created before the send, but now covers only the body read. A registration on that token disposes the response, which aborts a body stream that ignores the token. If an exception escapes the read after the timeout fires, it is rethrown as `TaskCanceledException` with a `TimeoutException` inner and a message naming the timeout in seconds. A `PdfTimestampException` from the cap passes through unchanged.
- `SignatureTests.cs`: `stalled_timestamp_response_body_still_times_out` now asserts `TaskCanceledException` with a `TimeoutException` inner. The new `stalled_timestamp_response_headers_still_time_out` uses a `StallingHandler` whose `SendAsync` waits on its token, with `Timeout` at 500 ms, and asserts the same shape within 10 s.
- **Red on the previous version:** 2 failed and 35 passed on `net8.0` and on `net6.0`. On `net8.0` the header case's inner exception was `TaskCanceledException`. On `net6.0` it was null. On both frameworks the body case's inner exception was null.
- **Green:** `SignatureTests` passed 37/37 on `net8.0` and 37/37 on `net6.0`. The Release build of all four targets had 0 warnings and 0 errors.
- Aborting a token-ignoring body stream on .NET Framework by disposing the response is **Implemented, not tested**: no test runtime covers it.

## 9. Handoff

QA Engineer: re-verify `SignatureTests` on `net8.0` and `net6.0`, including the byte count against 2,179,072 and the header case red on the previous version. Then the Technical Lead re-reviews.
