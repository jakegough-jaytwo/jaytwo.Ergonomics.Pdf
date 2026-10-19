# B-15 QA: OpenSSL timestamp authority, independent verification

## 1. Task and owner

B-15. QA Engineer: independent verification and the harness. The implementation is the Signing Engineer's (`agents/reports/2026-10-18-signing-engineer-B-15.md`).

**QA sign-off: given.** The acceptance criterion is met at the Independently validated level. One harness defect was found and fixed (section 4). It was in the shared harness, not in the library.

## 2. Verified facts

- **The authority is not pyHanko.** `OpenSslTimestampAuthority` runs `openssl ts -reply -token_out` (OpenSSL 3.2.4, 11 Feb 2025, Git for Windows mingw64) on the request the library built. Through the public API, the library embeds the token with `PdfSignatureKind.DocTimeStamp` and `DocTimeStampSignatureProvider`.
  - `openssl ts -query -text` shows `Nonce: 0x…` and `Certificate required: no` in that request.
  - The test asserts that the embedded token equals the issued token byte for byte.
- **The independent verifier is pyHanko 0.38.0** (`validate_pdf_timestamp`), with pyhanko-certvalidator 0.33.0 and asn1crypto 1.5.1 on Python 3.13.1. It reports:
  - `intact=true`, `valid=true`, `trusted=true`
  - `coverage=ENTIRE_FILE`, `type=/DocTimeStamp`, `embedded_certs=0`

  The CMS/TSP parsing, the ESS check, the imprint comparison, and path building are not OpenSSL code. The RSA primitive comes from pyca/cryptography 50.0.2, which bundles OpenSSL 4.0.3, a separate build from the authority's 3.2.4. `openssl ts -verify` 3.2.4 is the authority's own toolchain, so it counts as corroboration only.
- **The test root is the only trust anchor.** `ValidationContext(trust_roots=[root])` replaces the OS store: `pyhanko_certvalidator/registry.py` falls back to system roots only when `trust_roots is None`. With an untrusted root, pyHanko reports `trusted=false`, `intact=true`.
- **Missing tool fails, not skips.** I ran with `JAYTWO_OPENSSL=C:\nonexistent\openssl.exe`, `JAYTWO_TOOLS` unset, and PATH reduced to dotnet, System32, and Windows. Both tests failed on both frameworks with `openssl.exe was not found. Set JAYTWO_OPENSSL or put it on PATH.` Failed 2, Skipped 0.
- **Temp cleanup.** There were 0 `jaytwo-ergonomics-pdf-*` directories in `%TEMP%` before and after the final runs. Each test deletes its directory in `finally`.

## 3. Assumptions

- **Judgment on the out-of-band TSA certificate: acceptable as an explicit test trust configuration. It cannot make a bad token pass.**
  - The fixture puts `tsa.pem` into the parsed, unsigned `SignedData.certificates`. A compliant TSA would have put the same certificate there if the request had carried `certReq`.
  - pyHanko then does four things:
    1. It selects the signer by SID.
    2. It checks the signed ESS `signing-certificate-v2` hash against that certificate (`generic_cms._check_signing_certificate` via `check_ess_certid`).
    3. It verifies the signature with that certificate's key.
    4. It builds the path only to the test root.
  - Mutation M1 (section 7) shows the ESS check is live. A certificate with the same key, issuer, and serial, chained to the trusted root, is still rejected.
  - Limitation: pyHanko skips the ESS check when the attribute is absent ("if not present -> no constraints"). The protection therefore depends on the TSA emitting the attribute. OpenSSL does, with `ess_cert_id_alg = sha256` in `ts.cnf`.

## 4. Defects confirmed

**Harness hang (Medium, test-only). Owner: QA Engineer. Fixed.**

- `ExternalTool.Output` read stdout to EOF, then stderr, and waited with no timeout.
  - A tool that writes more stderr than the pipe buffer before it exits blocks forever.
  - `openssl req -newkey rsa:2048` writes progress dots to stderr. Across 300 runs: median 1,022 bytes, p99 3,009, max 3,718. That is close to the roughly 4 KiB pipe buffer, and each test generates three keys.
- Reproduced with a temporary probe that runs `python -c "sys.stderr.write('x'*1000000)"` through `ExternalTool.Output`:
  - The original harness hung until `timeout 90` killed it (exit 124).
  - The fixed harness passed in 84 ms.
- The probe was removed afterwards.

## 6. Changes made

- `test/jaytwo.Ergonomics.Pdf.Tests/ExternalTool.cs`:
  - New `Run(Process)`. It drains stdout and stderr concurrently with `ReadToEndAsync`. It waits at most 600 s, then kills the process tree and throws `TimeoutException`. It returns the exit code, stdout, and stderr.
  - `Output` and `Locate` (the `where.exe` call) now use `Run`.
  - Behavior is otherwise unchanged. `Output` still asserts exit 0 with the same message.
- `test/jaytwo.Ergonomics.Pdf.Tests/OpenSslTimestampTests.cs`: `OpenSslVerify` uses `ExternalTool.Run` in place of its own sequential reads.
- `validate_doc_timestamp.py`: unchanged.
- No `src/` change.
- Every temporary mutation was reverted. The SHA-256 of all three files was checked against the backups before the harness edit.

## 7. Verification run

Env from the brief, plus `JAYTWO_OPENSSL=C:\Program Files\Git\mingw64\bin\openssl.exe`. Command: `dotnet test test/jaytwo.Ergonomics.Pdf.Tests -f <tfm> --filter "FullyQualifiedName~<class>"`. The build was clean with StyleCop warnings as errors.

| Run | net8.0 | net6.0 |
|---|---|---|
| `OpenSslTimestampTests`, before the harness change | 2/2 passed | 2/2 passed |
| `OpenSslTimestampTests`, missing OpenSSL | 2 failed, 0 skipped | 2 failed, 0 skipped |
| `OpenSslTimestampTests`, final | 2/2 passed | 2/2 passed |
| `SigningInteropTests`, final (touches `ExternalTool.cs`) | 4/4 passed | 4/4 passed |

Mutations (net8.0, each reverted):

| Mutation | Result |
|---|---|
| M1: pyHanko gets a reissued TSA certificate (same CSR, key, and serial 2, `-days 3`) | Both tests fail: `Signing certificate attribute does not match selected signer's certificate … [NO_SIGNING_CERTIFICATE_FOUND]` |
| M2: fixture skips the certificate injection | Both tests fail: `signer certificate not included in signature [NO_SIGNING_CERTIFICATE_FOUND]` |
| M3: authority answers a request with one imprint bit flipped (nonce intact) | Both tests fail in `Sign`: `PdfTimestampException: The timestamp token's message imprint does not match the request.` No token reaches a verifier. |

The Signing Engineer's negative cases (wrong root, flipped header byte) are asserted in `verifiers_reject_an_untrusted_root_and_a_tampered_byte` and pass.

## 8. Decisions needed

- I agree with the Signing Engineer's D-14 item, for the Product Manager to take to the owner. pyHanko 0.38.0 cannot validate a library-requested timestamp from any RFC 3161-compliant TSA without the certificate supplied out of band.
- Observation, no action proposed: `Locate` falls back silently to PATH when the variable names a missing file. A mistyped `JAYTWO_OPENSSL` can therefore select a different `openssl.exe`; Git ships two, both 3.2.4. This behavior is shared by every tool.

## 9. Handoff

- Technical Lead: review the `ExternalTool.cs` change. `DenseMemoryTests`, `AppearanceRenderTests`, and `InteropTests` also call `Output`. I did not run them, per the brief.
- Product Manager: acceptance of B-15. The G5 CMS signature timestamp from a non-pyHanko authority remains untested.
