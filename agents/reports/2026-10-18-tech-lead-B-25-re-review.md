# B-25 re-review

## 1. Task and owner

Technical Lead. Second review of B-25 against the re-scoped entry in `agents/state/backlog.md`. The first review refused approval on the PIN criterion (`2026-10-18-tech-lead-B-25-review.md`). The Product Manager re-scoped that criterion (`2026-10-18-product-manager-B-28-B-25.md`). QA concurred and signed off (`2026-10-18-qa-engineer-B-25-rescope.md`). No code, test, or state edit in this review. G9 is not passed.

## 2. Verified facts

- **The refusal reason is resolved.** I refused because the board asked for PIN-omission evidence and no PIN reached the `ThrowingSigner` path. The binding wording now says no PIN assertion is required, because no library API receives a PIN. That holds in the code. `PdfDocument.Open` takes a stream and a password. `IPdfExternalSigner.SignHash` receives only the hash. The PIN clause stays open under G9, including a host signer exception that carries its own PIN. The unaccepted `external_signer_diagnostic_is_sanitized_and_does_not_extend_the_file` is correctly excluded from B-25 evidence. This corrects a clause that could not be tested. The password and key-material criteria are unchanged.
- **The tests meet the new wording.**
  - `EncryptionTests.failed_open_diagnostic_omits_the_password_and_file_key` opens an R6 file with a wrong password and asserts `PdfEncryptedException`. It checks the rejected, user, and owner passwords and the file key.
  - `SignatureTests.failed_sign_diagnostic_omits_the_password_and_file_key` opens an R6 file with the user password, so the file key is held. `Sign` then fails through `ThrowingSigner`, and the test asserts `CryptographicException`. It checks the user and owner passwords and the file key.
  - Both use `PublicDiagnostic`, which walks the message, `ToString()`, `Source`, `HelpLink`, and `Data` through the whole inner-exception chain. The key is checked as hex (case-insensitive), Base64, and ISO-8859-1 raw bytes.
- **The checked key is the real document key.** `test/jaytwo.Ergonomics.Pdf.Tests/StandardEncryptor.cs` lines 44-48 set the R5/R6 file key to bytes `01` through `20`. Both fixtures (`EncryptionTests.Document` and `SignatureTests.EncryptedPage`) build R6 through that class.
- **Test output.** Assertion text names only the kind of secret. Neither test writes output. The run logs below contain no password and no form of the key.
- **Checklist.** Charter: tests only, no `src/` change, no new package. The B-25 test files have no change since HEAD (`git status` shows only `OpenTests.cs` from B-28 under `test/`). Simplicity: the helpers are duplicated across the two test classes, which is acceptable for private test helpers in separate files. Build: both test targets compiled with no warnings. Docs: no documented behavior moved. Correctness, resources, failure, and security: not affected by a test-only change.

## 4. Defects confirmed

None.

## 5. Evidence missing

None for B-25 under the re-scoped wording. G9 still needs its own assessment, covering the PIN clause, host signer exceptions, and host-configured `HttpClient` credentials, as QA noted.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`, with the tool environment set as the task specified.

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f <tfm> --nologo -v q --logger "console;verbosity=normal" --filter "FullyQualifiedName~failed_open_diagnostic_omits_the_password_and_file_key|FullyQualifiedName~failed_sign_diagnostic_omits_the_password_and_file_key"
```

- `net8.0`: 2 passed, 0 failed. Open 112 ms, sign 211 ms.
- `net6.0`: 2 passed, 0 failed. Open 72 ms, sign 154 ms.
- I searched both logs for `secret-q7m`, `secret-k2p`, the file-key hex, and the file-key Base64. There were 0 matches. Neither log contains a warning or an error.
- The full suite was not run, as instructed.

## 8. Decisions needed

None. **B-25 is approved** under the re-scoped acceptance at the internally tested level.

## 9. Handoff

Product Manager: close B-25. Technical Lead: update the board. B-25 releases `SignatureTests.cs` and `EncryptionTests.cs`, which unblocks B-29's dependency. G9 is not passed.
