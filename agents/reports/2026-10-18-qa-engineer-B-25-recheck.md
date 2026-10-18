# B-25 Failure diagnostics omit secrets, recheck

## 1. Task and owner

QA Engineer. Recheck after the PIN and private-key assertions were removed. The first refusal stays in `agents/reports/2026-10-18-qa-engineer-B-25.md`. No edits to product code or `agents/state/`. No commit. G9 is not passed.

## 2. Verified facts

1. **Hollow claims are gone.** `failed_open_diagnostic_omits_the_password_and_file_key` and `failed_sign_diagnostic_omits_the_password_and_file_key` do not mention a PIN or a PKCS#8 private key. `FailingSigner` is gone. The sign test uses `ThrowingSigner`, whose `SignHash` receives only a hash and throws `CryptographicException` with a fixed message. The updated core report does not claim those omissions.

2. **Password and file-key checks remain.** The open test still requires the rejected password, the user password, and the owner password to be absent from the exception message and the public diagnostic. The sign test still requires the user password and the owner password to be absent from the same texts. Both tests build a 32-byte file key, bytes `01` through `20`, and `AssertDiagnosticOmitsKey` checks that key as hex, Base64, and raw ISO-8859-1 bytes. A leak of those values into the message, `ToString()`, source, help link, data, or an inner exception would fail the test.

3. **Both frameworks passed.** `net8.0`: passed 2, failed 0, 244 ms. `net6.0`: passed 2, failed 0, 134 ms. The run printed no password or key material.

Signed off. G9 is not passed.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --nologo --filter "FullyQualifiedName~failed_open_diagnostic_omits_the_password_and_file_key|FullyQualifiedName~failed_sign_diagnostic_omits_the_password_and_file_key"
```

`net8.0`: Passed 2, Failed 0, Skipped 0. Duration 244 ms.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --nologo --filter "FullyQualifiedName~failed_open_diagnostic_omits_the_password_and_file_key|FullyQualifiedName~failed_sign_diagnostic_omits_the_password_and_file_key"
```

`net6.0`: Passed 2, Failed 0, Skipped 0. Duration 134 ms.

## 9. Handoff

Technical Lead reviews. Product Manager is not asked to pass G9 from this report.
