# B-34 header-phase timestamp timeout

## Task and owner

Signing Engineer implementation of B-34.

## Verified facts

- `HttpTimestampAuthority` now creates one deadline token before the send and passes it through the response-header wait, body reads, and response-disposal abort path. It arms that token up to 50 ms (at most 10%) ahead of `HttpClient`'s framework timer so .NET Framework cannot win the timer race with an unidentifiable bare cancellation.
- Only cancellation caused by that deadline is normalized to `TaskCanceledException` with a `TimeoutException` inner exception. `CancelPendingRequests` leaves the deadline token unset and is not reclassified.
- `SignatureTests.stalled_timestamp_response_headers_still_time_out`, the two body-stall tests, and `cancel_pending_timestamp_requests_is_not_reported_as_a_timeout` run on all three test frameworks.

## Changes made

- Changed `src/jaytwo.Ergonomics.Pdf/Signatures/HttpTimestampAuthority.cs` to pass the owned deadline token to `HttpClient.SendAsync`, win the .NET Framework timer race deterministically, and normalize only owned-deadline failures.
- Added the unrelated-cancellation regression to `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs`.
- Documented the cross-framework timeout shape in `README.md` and the applicable timestamp row of `ACCEPTANCE.md`.

## Verification run

- `dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -c Release -f net8.0 --no-restore --filter "FullyQualifiedName~SignatureTests" --logger "console;verbosity=minimal"`: 44 passed, 0 failed.
- Same command with `-f net6.0`: 44 passed, 0 failed.
- Same command with `-f net48`: 44 passed, 0 failed in the credited run; a second consecutive repeat also passed 44/44.
- `dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj -c Release --no-restore`: all four library targets built, 0 warnings and 0 errors.
- SDK: `10.0.100-rc.1.25451.107`.

## Evidence missing

- Independent QA sign-off and the final full-suite run remain with QA and the Technical Lead.

## Handoff

QA Engineer independently reruns B-34 on `net8.0`, `net6.0`, and `net48`; Technical Lead reviews the cancellation ownership and exception shape.
