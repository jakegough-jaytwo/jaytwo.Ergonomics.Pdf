# B-35 QA verification

## Task and owner

Independent QA verification of Signing Engineer task B-35 under D-21.

## Verified facts

- Both request overloads explicitly encode `certReq` TRUE. The SHA-256("abc") no-nonce vector is 59 bytes, and OpenSSL 3.2.4 reports `Certificate required: yes` for the library request.
- `SignatureTests` accepts ESSCertID with SHA-1 and ESSCertIDv2 with default SHA-256 or explicit SHA-512. It rejects missing or wrong certificates, issuer/serial mismatches, a missing ESS attribute, multiple SignerInfos, multiple ESS attributes, a wrong first certificate identifier even when a later identifier is correct, malformed issuer/serial, unsupported ESS hashes, trailing token bytes, and a non-minimal DER length.
- Every rejection above is exercised through both `DocTimeStampSignatureProvider` and `CmsDetachedSignatureProvider`; the helper requires the destination length to remain unchanged. Imprint, digest-algorithm, and nonce rejection tests also remain green.
- A generated realistic chain (4096-bit root and intermediate, 2048-bit TSA) is required to total 3-6 KiB. Its certificate-bearing timestamp token is required to stay under 8 KiB and its timestamped CMS under 12 KiB, well below the fixed 8 MiB signature-appendix limit and within the default 16 KiB reservation.
- The OpenSSL-produced document timestamp contains one embedded certificate. `validate_doc_timestamp.py` accepts only the PDF and `root.pem`; it no longer accepts or inserts a TSA certificate. pyHanko reports the timestamp intact, valid, trusted, and covering `ENTIRE_FILE`. OpenSSL `ts -verify` also succeeds using only `root.pem` and the certificate carried by the token.
- The Python timestamp-server CMS path is also root-only: `SigningInteropTests.pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature` supplies the TSA root, not the TSA leaf, and passed on all three frameworks.
- `README.md` and the applicable timestamp rows of `ACCEPTANCE.md` now state `certReq` TRUE, required identified-certificate inclusion, and that inclusion is not signature or trust validation. Existing nonce, imprint, and digest criteria remain.

## Defects confirmed

None.

## Changes made

- Added this QA report only.

## Verification run

Tools: OpenSSL `3.2.4`; Python `3.13.1`; pyHanko `0.38.0`; pypdf `6.21.1`; qpdf `12.4.2`; Poppler `26.09.0`; SoftHSM `2.5.0`; .NET SDK `10.0.100-rc.1.25451.107`.

- Exact command: `$env:JAYTWO_QPDF='C:\Users\Jake\AppData\Local\Temp\qpdf-bin\qpdf-12.4.2-msvc64\bin\qpdf.exe'; $env:JAYTWO_POPPLER='C:\Users\Jake\AppData\Local\jaytwo-tools\poppler\poppler-26.09.0\Library\bin'; $env:JAYTWO_PKCS11='C:\Users\Jake\AppData\Local\jaytwo-tools\softhsm\SoftHSM2\lib\softhsm2-x64.dll'; $env:JAYTWO_OPENSSL='C:\Program Files\Git\mingw64\bin\openssl.exe'; foreach ($tfm in @('net8.0','net6.0','net48')) { dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -c Release -f $tfm --no-restore --filter 'FullyQualifiedName~SignatureTests' --logger 'console;verbosity=minimal'; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }`
  - Each framework: 44 passed, 0 failed, 0 skipped.
- Exact command: `$env:JAYTWO_OPENSSL='C:\Program Files\Git\mingw64\bin\openssl.exe'; foreach ($tfm in @('net8.0','net6.0','net48')) { dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -c Release -f $tfm --no-restore --filter 'FullyQualifiedName~OpenSslTimestampTests' --logger 'console;verbosity=minimal'; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }`
  - Each framework: 2 passed, 0 failed, 0 skipped.
- Exact command: `$env:JAYTWO_QPDF='C:\Users\Jake\AppData\Local\Temp\qpdf-bin\qpdf-12.4.2-msvc64\bin\qpdf.exe'; $env:JAYTWO_POPPLER='C:\Users\Jake\AppData\Local\jaytwo-tools\poppler\poppler-26.09.0\Library\bin'; $env:JAYTWO_PKCS11='C:\Users\Jake\AppData\Local\jaytwo-tools\softhsm\SoftHSM2\lib\softhsm2-x64.dll'; $env:JAYTWO_OPENSSL='C:\Program Files\Git\mingw64\bin\openssl.exe'; foreach ($tfm in @('net8.0','net6.0','net48')) { dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -c Release -f $tfm --no-restore --filter 'FullyQualifiedName~SigningInteropTests' --logger 'console;verbosity=minimal'; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }`
  - Each framework: 4 passed, 0 failed, 0 skipped. This is the full class, including the pyHanko timestamp case and SoftHSM path.
- Exact command: `dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj -c Release --no-restore -warnaserror`
  - All four library targets succeeded with 0 warnings and 0 errors.

## Handoff

QA signs off B-35 at the independently validated level for the OpenSSL-produced document timestamp and at the internally tested level for the negative ESS/DER matrix. Technical Lead reviews the implementation; Product Manager may accept the task. This sign-off does not claim TSA trust validation by the library and does not pass a qualification gate.
