# B-25 Failure diagnostics omit secrets

## 1. Task and owner

QA Engineer. Independent check of `failed_open_diagnostic_omits_the_password_pin_and_key` and `failed_sign_diagnostic_omits_the_password_pin_and_key`. No edits to the tests, product code, or `agents/state/`. No commit. G9 is not passed.

## 2. Verified facts

1. **Both tests passed.** `net8.0`: passed 2, failed 0, 138 ms. `net6.0`: passed 2, failed 0, 139 ms. The run printed no password, PIN, or key material. Failure text in both helpers names the kind only.

2. **What the assertions inspect.** `AssertDiagnosticOmits` requires the secret to be absent from `Exception.Message` and from `PublicDiagnostic`. `PublicDiagnostic` walks the exception and its inner exceptions and appends each message, `ToString()`, source, help link, and `Data` entries. `AssertDiagnosticOmitsKey` does the same for hex, Base64, and ISO-8859-1 of the supplied bytes. A value that appeared in those texts would fail the test.

3. **Open, rejected password.** `EncryptionTests` opens a revision 6 file with a password that is not the user or owner password. `PdfDocument.Open` throws `PdfEncryptedException`. `PdfSecurity.CreateAes256` throws that exception from the rejection path, whose message is fixed and does not include the supplied password. A leak of that password into the message or the public diagnostic would fail the test. That omission is signed off.

4. **Sign, user password and file key.** `SignatureTests` opens the same kind of file with the user password, then `Sign` throws `CryptographicException`. `PdfEngine.Sign` restores in-memory state and rethrows that exception. The file-key bytes checked are the revision 6 test encryptor's file key, which the library holds after a successful open. A leak of the user password or that file key into the message or the public diagnostic would fail the test. Those two omissions are signed off.

## 5. Evidence missing

The sign test's PIN and private-key claim is not signed off. `FailingSigner` stores a PIN and a PKCS#8 private key and throws `CryptographicException` with a fixed message that contains neither. `IPdfExternalSigner.SignHash` receives only a hash. `CmsDetachedSignatureProvider` does not receive the PIN or the private key. The test can pass because the signer omits them. The library path is not what that claim checks.

These open-test checks are also not signed off. `Open` takes no PIN, so the PIN string never reaches the library. The user and owner passwords are used to build the file and are not the password passed to `Open`. `CreateAes256` rejects the password before it decrypts the file key, so that failure path does not hold the key.

G9 is not passed. The rest of the suite was not run.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --nologo --filter "FullyQualifiedName~failed_open_diagnostic_omits_the_password_pin_and_key|FullyQualifiedName~failed_sign_diagnostic_omits_the_password_pin_and_key"
```

`net8.0`: Passed 2, Failed 0, Skipped 0. Duration 138 ms.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --nologo --filter "FullyQualifiedName~failed_open_diagnostic_omits_the_password_pin_and_key|FullyQualifiedName~failed_sign_diagnostic_omits_the_password_pin_and_key"
```

`net6.0`: Passed 2, Failed 0, Skipped 0. Duration 139 ms.

## 9. Handoff

Technical Lead reviews. Product Manager is not asked to pass G9 from this report.
