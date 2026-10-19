# B-31 review

## 1. Task and owner

Technical Lead. Review of B-31, "Cap the timestamp response size", uncommitted on `wip`, against `2026-10-18-tech-lead-B-31-board.md`. Inputs: `2026-10-18-signing-engineer-B-31.md` and `2026-10-18-qa-engineer-B-31.md`. G8 is not passed.

## 2. Verified facts

- **Cap and read.** These match the brief:
  - `ResponseHeadersRead` is used, so `HttpClient` no longer buffers the body.
  - The `read > MaxResponseBytes - total` check runs before `Write`, so the `MemoryStream` never holds more than 2,097,152 bytes.
  - A `Content-Length` over the cap is refused before the body is opened.
  - The caller's client is not mutated, and there is no public API change.
  - The tests count bytes from a non-seekable generated stream against 2,179,072 and cover the exact boundary. QA's red run gives 4 failures with the source at HEAD.
  - `README.md` and `ACCEPTANCE.md` each gain one accurate sentence. No criterion is weakened.
- **Sync-over-async.** There is one block now instead of two. Every `await` inside uses `ConfigureAwait(false)`, so no `SynchronizationContext` is captured. The deadlock risk is no greater than before. Acceptable.
- **Member order.** Order is const, fields, constructor, public method, private static, then private instance. That satisfies SA1202, SA1203, and SA1204. The implementer reports a clean Release build on all four targets.
- **Timeout surfacing changed. This is a regression.** Before, a timeout on `net5+` surfaced from `HttpClient` as `TaskCanceledException`. Its `InnerException` was `TimeoutException`, and its message named the configured timeout. The fix now passes its own `CancellationTokenSource(_httpClient.Timeout)` to `SendAsync`. That token is created first and fires first, so `HttpClient` treats the timeout as caller cancellation and drops the `TimeoutException`. I confirmed this with a scratch `net8.0` program, run outside the repo, using a handler that stalls the headers and `Timeout = 500 ms`:
  - Before: `TaskCanceledException`, `inner=TimeoutException`, "The request was canceled due to the configured HttpClient.Timeout of 0.5 seconds elapsing."
  - After: `TaskCanceledException`, `inner=TaskCanceledException`, "A task was canceled."

  On `net6.0` and `net8.0` builds, a caller that uses the documented .NET pattern `catch (TaskCanceledException e) when (e.InnerException is TimeoutException)` would no longer catch a TSA timeout. The body phase now loses the same shape. The stall test only asserts `OperationCanceledException`, so it misses this.
- **`netstandard2.0` cancellation limitation.** On .NET Framework, the response stream can ignore the token in `ReadAsync`. A hostile TSA that stalls the body could then hang `Sign` without bound. That is B-31's threat model. Accepting it as a limitation is not justified, because the standard mitigation is cheap.

## 4. Defects confirmed

1. On `net6.0` and `net8.0`, the timeout's caller-visible shape regressed, as shown above.
2. On `netstandard2.0` running on .NET Framework, a stalled body may not honor the timeout.

## 6. Changes required (returned to the Signing Engineer)

Make these changes in `HttpTimestampAuthority.cs` and `SignatureTests.cs` only.

1. **Header phase.** Call `SendAsync(request, HttpCompletionOption.ResponseHeadersRead)` without the library's token, so `HttpClient` applies its own `Timeout` and its own exception, exactly as before. Keep the `CancellationTokenSource(_httpClient.Timeout)`, created before the send, for the body read only, so the total stays under `Timeout`.
2. **Body phase.** While reading, register the timeout token to dispose the response, so a stream that ignores the token is still aborted. If any exception escapes the read while `timeout.IsCancellationRequested`, throw `new TaskCanceledException(<message naming the configured HttpClient.Timeout in seconds>, new TimeoutException(...))`. This matches the `net5+` shape on every target. A `PdfTimestampException` raised by the cap must still pass through unchanged.
3. **Tests.**
   - Tighten `stalled_timestamp_response_body_still_times_out` to assert `TaskCanceledException` with `InnerException` of type `TimeoutException`.
   - Add a header-phase case: a handler whose `SendAsync` awaits `Task.Delay` on its token, with `Timeout` set to 500 ms. Assert the same type and inner exception within 10 s. This case is red on the current change; my scratch run shows the inner exception is `TaskCanceledException` there.
   - Keep everything else.

The cap, buffer, `Content-Length` path, docs, and the other tests are approved as they stand. Record the .NET Framework behavior as Implemented, not tested, because no test runtime covers it.

## 7. Verification run

I ran a scratch `net8.0` console program in the session scratchpad with .NET SDK 10.0.100-rc.1. It showed the exception shapes above. I ran no repository tests.

## 9. Handoff

The Signing Engineer reworks the two items. QA re-verifies `SignatureTests` on `net8.0` and `net6.0`, including red for the header-phase case on the current change. Then the Technical Lead re-reviews. The B-31 claims stay held. G8 is not passed.
