# Technical Lead review: B-33

## Task and owner

B-33, the `net48` test target under D-19. Implemented and reported by QA in `2026-10-18-qa-engineer-B-33.md`, including its Rework section. Reviewer: Technical Lead.

## Verified facts

- `src/` is unchanged (`git diff --quiet src/`). The library targets are unchanged (D-13). On `net48` the test project consumes the `netstandard2.0` build.
- The `net48`-only additions are test-only: a `System.Net.Http` reference, `System.Runtime.CompilerServices.Unsafe` 6.0.0 for the test host's binding redirect, and binding-redirect generation. None reaches the package.
- `TestCompat.cs` forwards to the original API on `net6.0` and `net8.0`. The call-site edits in the 12 test files are mechanical. I read the `SignatureTests` and `DenseMemoryTests` diffs line by line, and no assertion changed. `DenseMemoryTests` on `net48` measures with `AppDomain.MonitoringTotalAllocatedMemorySize`. QA checked that counter against a known 100 MiB allocation.
- First review: returned once. No permanent test reached the B-31 abort-by-dispose path, which is the purpose D-19 names. The rework adds `stalled_timestamp_body_that_ignores_the_token_is_aborted_by_disposing_the_response`. Its body stream ignores the token. QA removed the `timeout.Token.Register(response.Dispose)` line, and the test failed on all three frameworks. With the line restored, it passes.
- My independent re-run: `dotnet test -f net48 --filter FullyQualifiedName~SignatureTests` gave 37 passed and 1 failed out of 38. The only failure is `stalled_timestamp_response_headers_still_time_out`, at `SignatureTests.cs:186`.

## Defects confirmed

D-B33-1 (library, Medium). On .NET Framework, a header-phase timeout surfaces as a bare `TaskCanceledException`, with no `TimeoutException` inner exception. On .NET 5+, `HttpClient` supplies that inner exception. B-31's claim that both phases have the same exception shape holds only on .NET 5+. The timeout itself works. The fix belongs in `HttpTimestampAuthority` (Signing Engineer) and is outside this round. It is logged as B-34, and the regression test already exists and fails.

## Evidence missing

`netstandard2.1` still has no test run (G10).

## Handoff

Approved. Product Manager to accept B-33 and log B-34.
