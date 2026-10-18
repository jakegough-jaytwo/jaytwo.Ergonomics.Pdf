# B-25 PIN re-scope, QA concurrence and verification

## 1. Task and owner

QA Engineer. Concur with or object to the Product Manager's B-25 PIN re-scope (`2026-10-18-product-manager-B-28-B-25.md`, B-25 entry in `state/backlog.md`). Verify the two committed B-25 tests against the new wording. No edits to code, tests, or state. No commit. G9 is not passed.

## 2. Verified facts

1. **No library API receives a PIN.** The public surface was checked file by file under `src/jaytwo.Ergonomics.Pdf`, excluding `Internal/`.
   - `PdfDocument`: `Open(Stream, bool)`, `Open(Stream, string?, bool)`, `SaveIncremental`, `Sign`, `Dispose`. The only secret-bearing input is the `Open` password.
   - `IPdfExternalSigner` (`Signatures/IPdfExternalSigner.cs`): `CertificateDer`, `AdditionalCertificates`, `SignHash(ReadOnlySpan<byte> hash)`. No member carries a PIN, a session, or a slot.
   - `CmsDetachedSignatureProvider` takes `(IPdfExternalSigner)` or `(IPdfExternalSigner, ITimestampAuthority?)`. `DocTimeStampSignatureProvider` takes `(ITimestampAuthority)`. `HttpTimestampAuthority` takes `(HttpClient, Uri)`. `PdfSignatureOptions` holds field, page, rect, kind, reserve, reason, location, contact info, signer name, and signing time.
   - `grep -iE "\bpin\b|pkcs11|SecureString|passphrase|credential"` over `src/` finds nothing. Every other public `string` parameter is a field name, value, attachment name, or exception message.
   So a PIN exists only inside a host's `IPdfExternalSigner` implementation. The library never receives one, so no library diagnostic can carry one unless the host's own exception carries it. The re-scope correctly keeps that case open under G9.

2. **The password and key-material criteria are unchanged.** The new acceptance covers a rejected `Open` and a failed `Sign` of an encrypted file. It inspects the message, `ToString()`, source, help link, data, and inner exceptions. It checks the password passed to `Open`, the user password, the owner password, and the file key as hex, Base64, and raw bytes. These are the same diagnostic surfaces and secrets as before. Only the PIN assertion was removed, and that assertion could not be tested. G9's PIN line in `QUALIFICATION.md` section 11 stays open. The unaccepted `external_signer_diagnostic_is_sanitized_and_does_not_extend_the_file` is correctly excluded from B-25 evidence.

3. **The two tests meet the new wording.**
   - `EncryptionTests.failed_open_diagnostic_omits_the_password_and_file_key` opens an R6 AES-256 file with a wrong password. It asserts `PdfEncryptedException`. It checks that the rejected, user, and owner passwords and the file key do not appear in the message or in `PublicDiagnostic`, which walks the message, `ToString()`, `Source`, `HelpLink`, and `Data` down the inner-exception chain.
   - `SignatureTests.failed_sign_diagnostic_omits_the_password_and_file_key` opens an R6 file with the user password and runs `Sign` with `ThrowingSigner`. It asserts `CryptographicException`. It checks the user and owner passwords and the file key against the same surfaces.
   - The key being checked is the real document key. `StandardEncryptor.cs:44-47` sets the R6/R5 file key to bytes `01` through `20`, which is the 32-byte key both tests build. `AssertDiagnosticOmitsKey` checks it as hex (case-insensitive), Base64, and ISO-8859-1 raw bytes.
   - Assertion failure text names only the kind of secret ("password", "key"). Neither test file uses `ITestOutputHelper` or `Console.Write`.

Concur with the re-scope. Signed off on B-25 under the re-scoped wording. G9 is not passed.

## 3. Assumptions

- A host signer exception that carries its own PIN belongs to the G9 assessment, not to B-25, as the Product Manager stated.
- `HttpClient` credentials that a host configures (for example, TSA authentication headers) are not a PIN or key material that the library receives. They are worth one line in the G9 assessment.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`. The tool environment was set as the task specifies. These tests need no external tools.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f <tfm> --nologo -v q --logger "console;verbosity=normal" --filter "FullyQualifiedName~failed_open_diagnostic_omits_the_password_and_file_key|FullyQualifiedName~failed_sign_diagnostic_omits_the_password_and_file_key"
```

- `net8.0`: Passed 2, Failed 0, Skipped 0. Open 53 ms, sign 151 ms.
- `net6.0`: Passed 2, Failed 0, Skipped 0. Open 70 ms, sign 140 ms.
- The console output of both runs was scanned for `secret-q7m`, `secret-k2p`, the file-key hex, and the file-key Base64. There were 0 matches in each log.

## 9. Handoff

Technical Lead re-reviews B-25. G9 stays open for its own assessment, including the PIN clause.
