# B-25 Failure diagnostics omit secrets

## 1. Task and owner

Failure diagnostics for a rejected open and a failed sign must not contain the password or the file key. Core Engineer. `EncryptionTests.cs` and `SignatureTests.cs` only. No `src/` edits. No commit. G9 is not passed.

QA did not sign off the earlier PIN and private-key claim. Those assertions are removed. This report does not claim that omission.

## 2. Verified facts

1. **Failing open.** A revision 6 file rejects the password passed to `PdfDocument.Open` with `PdfEncryptedException`. The exception message and the public diagnostic (message, `ToString()`, source, help link, data, and inner exceptions) do not contain that password, the user password, the owner password, or the revision 6 file key. That key is the 32 bytes `01` through `20` from `StandardEncryptor`, checked as hex, Base64, and raw bytes. Test: `EncryptionTests.failed_open_diagnostic_omits_the_password_and_file_key`. `Open` takes no PIN.

2. **Failing sign.** The same kind of file is opened with its user password. `Sign` then fails because the host signer throws `CryptographicException` with a fixed message. `SignHash` receives only a hash. The library does not receive a PIN or a private key, and this test does not assert that those values are absent. The exception message and the same public diagnostic do not contain the user password, the owner password, or the same file key. Test: `SignatureTests.failed_sign_diagnostic_omits_the_password_and_file_key`.

The assertion text does not print those secrets. No defect was confirmed.

## 6. Changes made or proposed

- `test/jaytwo.Ergonomics.Pdf.Tests/EncryptionTests.cs`: the open check, renamed, with the PIN assertion removed.
- `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs`: the sign check, renamed. The PIN assertion, the PKCS#8 key assertion, and `FailingSigner` are removed. The signer is `ThrowingSigner`.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --nologo --filter "FullyQualifiedName~failed_open_diagnostic_omits_the_password_and_file_key|FullyQualifiedName~failed_sign_diagnostic_omits_the_password_and_file_key"
```

`net8.0`: Passed 2, Failed 0, Skipped 0. Duration 154 ms.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --nologo --filter "FullyQualifiedName~failed_open_diagnostic_omits_the_password_and_file_key|FullyQualifiedName~failed_sign_diagnostic_omits_the_password_and_file_key"
```

`net6.0`: Passed 2, Failed 0, Skipped 0. Duration 143 ms.

Evidence level: internally tested. G9 is not passed. The rest of the suite was not run.

## 9. Handoff

QA Engineer verifies these tests again. G9 is not passed.
