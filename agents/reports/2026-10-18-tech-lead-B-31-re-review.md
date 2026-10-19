# B-31 re-review

## 1. Task and owner

Technical Lead. Re-review of the B-31 rework, uncommitted on `wip`, against section 6 of `2026-10-18-tech-lead-B-31-review.md`. G8 is not passed.

## 2. Verified facts

These facts come from reading the current `HttpTimestampAuthority.cs`, the `SignatureTests.cs` diff, and the Rework and Re-verification reports. I ran no tests.

- **Header phase.** `SendAsync(request, ResponseHeadersRead)` no longer takes the library's token. `HttpClient` again raises its own timeout exception. QA's run shows that `stalled_timestamp_response_headers_still_time_out` was red against the previous version, with an inner `TaskCanceledException` on `net8.0` and a null inner on `net6.0`. It is green now.
- **Body phase.**
  - The `CancellationTokenSource(_httpClient.Timeout)` starts before the send. The whole exchange therefore stays under `Timeout`, and `InfiniteTimeSpan` stays infinite.
  - `Register(response.Dispose)` aborts a stream that ignores the token. If the token has already fired when the headers arrive, `Register` runs the dispose at once. The read then fails, and that failure is converted the same way.
  - The registration is disposed before the response. Disposing the response twice is safe.
  - The filter `timeout.IsCancellationRequested && !(exception is PdfTimestampException)` converts the failure to `TaskCanceledException` with a `TimeoutException` inner. The cap refusal passes through unchanged.
  - `TaskCanceledException(string, Exception)` and `CancellationTokenRegistration` exist on `netstandard2.0`, and the four-target Release build is clean.
- **Size cap.** It is unchanged. QA re-checked every byte bound: at most 2,179,072 bytes, 0 for a declared length over the cap, and exactly 2,097,152 accepted. The file is not extended. `SignatureTests` passed 37/37 on `net8.0` and on `net6.0`.
- **Earlier findings.** Sync-over-async safety, member order, and the docs were reviewed in the first round and are unchanged.

## Assumptions

A connection error that happens after the timeout has fired is reported as a timeout. This is acceptable, because the call had timed out by then.

## 5. Evidence missing

The `.NET Framework` abort, which disposes the response, is Implemented, not tested. No test runtime covers it. This is recorded in both the implementer and QA reports.

## 6. Changes made

- Approved.
- `agents/state/board.md`: I removed the B-31 row from the Active table and released its four claims. The board notes that B-31 awaits Product Manager acceptance.

## 9. Handoff

The Product Manager accepts B-31 or returns it. Commit after acceptance. G8 is not passed.
