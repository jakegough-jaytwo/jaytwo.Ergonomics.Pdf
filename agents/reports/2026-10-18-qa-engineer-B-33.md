# B-33. `net48` test target

## 1. Task and owner

B-33, QA Engineer, under D-19. The test project gains `net48`. The library targets are unchanged, and `src/` is unchanged.

## 2. Verified facts

- The `net48` test build references `src/jaytwo.Ergonomics.Pdf/bin/Debug/netstandard2.0/jaytwo.Ergonomics.Pdf.dll`, with System.Memory 4.0.1.2 (package 4.5.5) in the output.
- `dotnet build test/jaytwo.Ergonomics.Pdf.Tests --no-incremental` builds `net48`, `net6.0`, and `net8.0` with 0 warnings and 0 errors. Warnings are errors, and StyleCop is included.
- The `net48` tests run in `testhost.net48.exe` from the SDK's `TestHostNetFramework` folder. Its PE machine type is 0x8664, so it is x64. SoftHSM's `softhsm2-x64.dll` loads, and the PKCS#11 tests pass.
- **`net48` full suite: 202 total, 201 passed, 1 failed, 0 skipped.** The failure is `stalled_timestamp_response_headers_still_time_out`, described in section 4.
- **B-31 stall tests on `net48`:**
  - `stalled_timestamp_response_body_still_times_out` **passed** in 0.52 s.
  - `stalled_timestamp_response_headers_still_time_out` **failed** in 0.51 s. The timeout fired, but the inner exception was null, not `TimeoutException`.
- **Exclusions on `net48`: none.** No test is skipped or compiled out. Every external-tool test ran on `net48` and passed: qpdf, Poppler, pyHanko/pypdf, SoftHSM/Pkcs11Interop, and OpenSSL.
- **Abort-by-dispose path.** The in-repo body test uses `StallingStream`, which honors the token. On .NET Framework, `StreamContent` passes the token through to the stream, so that test ends through the token, not through the dispose. A scratch `net48` x64 program, outside the repo, tested the dispose path directly. It used a body stream whose `ReadAsync` ignores the token and fails only when disposed. Result: `TaskCanceledException` → `TimeoutException` → `ObjectDisposedException`, after 510 ms, with the body disposed. The B-31 abort-by-dispose path works on .NET Framework 4.8.9363.0. This is scratch evidence, not a permanent test. Source: `%LOCALAPPDATA%\Temp\jaytwo-b33\abortprobe-Program.cs`.
- **Allocation counter.** On `net48`, `DenseMemoryTests` reads `AppDomain.MonitoringTotalAllocatedMemorySize` in place of `GC.GetTotalAllocatedBytes(true)`. A scratch check showed it is accurate. 1000 × 10 KB measured 10,048,000 bytes, and 100 × 1 MiB measured 104,860,000 bytes. The upper-bound asserts are therefore not vacuous.
- **Targeted runs** of the classes whose helpers changed: `SignatureTests`, `SigningInteropTests`, `DenseMemoryTests`, `AttachmentTests`, `LzwTests`, and `InteropTests`. 61/61 passed on `net8.0`, and 61/61 passed on `net6.0`.

## 3. Assumptions

- On `net48`, the test project ships `System.Runtime.CompilerServices.Unsafe` 6.0.0 as a test-only package. The SDK's `testhost.net48.exe.config` redirects Unsafe 1.0.0.0–6.0.0.0 to 6.0.0.0. The xUnit AppDomain inherits that redirect. Without 6.0.0.0 in the output, 57 tests failed with `FileLoadException`. The library's `netstandard2.0` build therefore runs with System.Memory 4.5.5 over Unsafe 6.0 under this host. This is a common unified set on .NET Framework. It is not a library change.

## 4. Defects confirmed

**D-B33-1. On .NET Framework, a header-phase timeout has no `TimeoutException` inner.**

1. **Summary.** When the TSA stalls the response headers, `HttpTimestampAuthority.GetTimeStampToken` throws `TaskCanceledException` with a null `InnerException` on .NET Framework. A body-phase timeout carries `TimeoutException`. The two phases surface the same timeout differently, and the header shape differs from `net6.0`/`net8.0`.
2. **Impact.** The timeout itself holds, at 509 ms against a 500 ms `Timeout`. A caller on .NET Framework that tells a timeout from a cancellation by `InnerException is TimeoutException` misclassifies a header stall. Signing and timestamping are otherwise unaffected.
3. **Reproduction.** `dotnet test test/jaytwo.Ergonomics.Pdf.Tests -f net48 --filter FullyQualifiedName~stalled_timestamp_response_headers_still_time_out`. The test uses `StallingHandler` and `Timeout = 500 ms`.
4. **Expected.** `TaskCanceledException` with a `TimeoutException` inner, within 10 s. This is the assertion B-31 added, and it passes on `net6.0` and `net8.0`.
5. **Actual.**
   ```
   Failed SignatureTests.stalled_timestamp_response_headers_still_time_out [509 ms]
   Assert.IsType() Failure
   Expected: System.TimeoutException
   Actual:   (null)
     at SignatureTests.stalled_timestamp_response_headers_still_time_out() in SignatureTests.cs:line 186
   ```
   Cause, from reading the code: `ExchangeAsync` leaves the header phase to `HttpClient`'s own timeout exception. On .NET 5 and later, that exception carries `TimeoutException`. On .NET Framework, `HttpClient` throws a bare `TaskCanceledException`. The library wraps only the body phase.
6. **Severity.** Medium. It is a predictable edge-case failure in exception shape, not in the timeout.
7. **Suggested owner.** Signing Engineer.

The test was not weakened or excluded. It stays red on `net48` until the owner decides.

## 5. Evidence missing

- No permanent test covers the token-ignoring body stream, the abort-by-dispose path. Only the scratch probe above covers it. Adding one is a new test, outside B-33's compatibility-only boundary.

## 6. Changes made

- `test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj`:
  - adds `net48` to `TargetFrameworks`;
  - for `net48` only, adds a `System.Net.Http` reference, `System.Runtime.CompilerServices.Unsafe` 6.0.0, and the `AutoGenerateBindingRedirects` and `GenerateBindingRedirectsOutputType` settings.
- `test/jaytwo.Ergonomics.Pdf.Tests/TestCompat.cs`, new. One internal static class. On `net6.0` and `net8.0`, each member forwards to the API the tests used before. Under `NETFRAMEWORK` it supplies:
  - hex to and from;
  - SHA-256;
  - `UnixEpoch` and Latin-1;
  - random fill;
  - the allocated-bytes counter;
  - zlib, as an RFC 1950 header, deflate, and Adler-32;
  - PKCS#8 PEM export, through `DerCodec`;
  - argument quoting by the `CommandLineToArgvW` rules;
  - extensions for `Process.Kill(entireProcessTree)` (via `taskkill /T /F`), `string.Replace(..., StringComparison)`, and `HttpContent.ReadAsByteArrayAsync(CancellationToken)`.
- Call sites now use `TestCompat`. No assertion changed.
  - `AppearanceRenderTests.cs`, `ExternalTool.cs`, `ForeignPdfTests.cs`, and `InteropTests.cs`: `ArgumentList.Add` becomes `TestCompat.AddArgument`.
  - `AttachmentTests.cs`, `LzwTests.cs`, and `PdfBuilder.cs`: the private `Zlib` helpers delegate to `TestCompat.Zlib`. The now-unused `System.IO.Compression` usings are removed.
  - `DenseMemoryTests.cs`, `EncryptionTests.cs`, `ForeignPdfTests.cs`, `LzwTests.cs`, `OpenSslTimestampTests.cs`, `SignatureTests.cs`, and `SigningInteropTests.cs` use the hex, SHA-256, epoch, Latin-1, random, allocation, and PEM helpers.
  - `SignatureTests.cs`: `StallingStream.ReadAsync(Memory<byte>, ...)` is compiled only off .NET Framework, where the overload exists. Under `NETFRAMEWORK`, `RecordingHandler.Send` is a private method rather than an override, because .NET Framework has no `HttpMessageHandler.Send`.
- `OpenSslTimestampTests.cs` is still untracked from B-15. Only its two `Latin1` and `FromHexString` call sites changed.

## 7. Verification run

- .NET SDK 10.0.100-rc.1.25451.107 and .NET Framework 4.8.9363.0, on Windows 11 x64.
- Tools: qpdf 12.4.2, Poppler 26.09.0, SoftHSM 2 (`JAYTWO_PKCS11`), OpenSSL from Git `mingw64`, and Python 3.13.1 with pyHanko and pypdf.
- `dotnet build test/jaytwo.Ergonomics.Pdf.Tests --no-incremental`: 0 warnings, 0 errors, on three targets.
- `dotnet test test/jaytwo.Ergonomics.Pdf.Tests -f net48 --no-build --logger trx`: 201 passed, 1 failed, 0 skipped. Output is in `%LOCALAPPDATA%\Temp\jaytwo-b33\b33-net48.trx` and `test48.log`.
- `dotnet test -f net8.0` and `-f net6.0` with the class filter above: 61/61 on each.
- The full `net8.0`/`net6.0` suite was not run. It runs at the checkpoint.

## 8. Decisions needed

- Product Manager, with the Signing Engineer: whether D-B33-1 becomes a backlog item. The other option is to accept the .NET Framework header-phase exception shape as a documented limitation. Under that option the test's assertion would need an owner-approved change. QA does not relax it.

## 9. Handoff

- Technical Lead: review the harness change, and re-run `net48` independently as the board requires.
- Product Manager: triage D-B33-1. B-33's criteria are met, except that one of the two stall tests fails on `net48`. That failure is a library defect, not a harness defect.

## Rework

The Technical Lead returned B-33 once because no permanent test reached the abort-by-dispose path. This rework closes the section 5 gap.

**Change.** `SignatureTests.cs` gains a test, `stalled_timestamp_body_that_ignores_the_token_is_aborted_by_disposing_the_response`, and a nested class, `TokenIgnoringStream`. The stream's `ReadAsync` ignores the token. Only `Dispose` ends a pending read early, and it fails the read with `ObjectDisposedException`. Otherwise the read fails with `IOException` after 30 s, so a regression fails the test instead of hanging it. The test sends the request through `HttpTimestampAuthority` with `StreamHandler` and `Timeout = 500 ms`, the same setup as the existing stall tests. It asserts:
- `TaskCanceledException`, with a `TimeoutException` inner whose own inner is `ObjectDisposedException`;
- that the body stream was disposed;
- that the call finished within 10 s.

**Frameworks.** The test runs on all three targets, not only `net48`. On `net6.0` and `net8.0`, `StreamContent` also passes the token to the inner stream, so a stream that ignores the token reaches the dispose path there too. The exception shape is the same on every framework, because the library builds it itself.

**Revert check.** I temporarily replaced the `timeout.Token.Register(response.Dispose)` line in `src/.../HttpTimestampAuthority.cs` with a comment, rebuilt, and ran the new test. It failed on `net48`, `net8.0`, and `net6.0`, each in 30 s, with `Expected: System.ObjectDisposedException, Actual: System.IO.IOException`. I then restored the file with `git checkout`. `git diff --quiet src/` is clean, and the suites below ran against the restored source.

**Runs**, after a `--no-incremental` build with 0 warnings and 0 errors:
- `--filter FullyQualifiedName~SignatureTests`:
  - `net48`: 37 passed, 1 failed. The failure is `stalled_timestamp_response_headers_still_time_out`, which is D-B33-1, unchanged.
  - `net8.0`: 38/38 passed.
  - `net6.0`: 38/38 passed.
- Full `net48` suite: 203 total, 202 passed, 1 failed (D-B33-1), 0 skipped, 0 excluded. TRX: `%LOCALAPPDATA%\Temp\jaytwo-b33\b33-net48-rework.trx`.

**Status.** The section 5 gap is closed: the B-31 abort-by-dispose path is internally tested on `net48`, `net6.0`, and `net8.0`. D-B33-1 stays open, and the header-phase test is unchanged.
