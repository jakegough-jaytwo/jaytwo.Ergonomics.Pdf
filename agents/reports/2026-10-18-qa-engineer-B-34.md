# B-34 QA verification

## Task and owner

Independent QA verification of Signing Engineer task B-34.

## Verified facts

- `SignatureTests.stalled_timestamp_response_headers_still_time_out` passed on `net8.0`, `net6.0`, and `net48`; it requires `TaskCanceledException` with a `TimeoutException` inner exception.
- The body-stall and abort-by-dispose timeout regressions passed in the same three runs.
- `SignatureTests.cancel_pending_timestamp_requests_is_not_reported_as_a_timeout` passed on all three frameworks. It waits until the request is active, calls `HttpClient.CancelPendingRequests`, and requires an `OperationCanceledException` whose inner exception is not `TimeoutException`.
- The implementation diff confines B-34 behavior to `HttpTimestampAuthority`: one owned deadline token covers headers and body, while timeout normalization is conditioned on that token being canceled.

## Defects confirmed

None.

## Changes made

- Added this QA report only.

## Verification run

Environment: .NET SDK `10.0.100-rc.1.25451.107` on Windows; VSTest `18.0.0-preview-25451-107`.

- Exact command: `$env:JAYTWO_QPDF='C:\Users\Jake\AppData\Local\Temp\qpdf-bin\qpdf-12.4.2-msvc64\bin\qpdf.exe'; $env:JAYTWO_POPPLER='C:\Users\Jake\AppData\Local\jaytwo-tools\poppler\poppler-26.09.0\Library\bin'; $env:JAYTWO_PKCS11='C:\Users\Jake\AppData\Local\jaytwo-tools\softhsm\SoftHSM2\lib\softhsm2-x64.dll'; $env:JAYTWO_OPENSSL='C:\Program Files\Git\mingw64\bin\openssl.exe'; foreach ($tfm in @('net8.0','net6.0','net48')) { dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -c Release -f $tfm --no-restore --filter 'FullyQualifiedName~SignatureTests' --logger 'console;verbosity=minimal'; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }`
  - `net8.0`: 44 passed, 0 failed, 0 skipped.
  - `net6.0`: 44 passed, 0 failed, 0 skipped.
  - `net48` against the library's `netstandard2.0` build: 44 passed, 0 failed, 0 skipped.
- Exact command: `dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj -c Release --no-restore -warnaserror`
  - `netstandard2.0`, `netstandard2.1`, `net6.0`, and `net8.0`: succeeded, 0 warnings, 0 errors.

## Handoff

QA signs off B-34 at the internally tested level. Technical Lead reviews the implementation; Product Manager may accept the task.
