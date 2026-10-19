# B-35 self-contained RFC 3161 tokens

## Task and owner

Signing Engineer implementation of B-35 under D-21.

## Verified facts

- Both request overloads explicitly encode DER `certReq` TRUE; the no-nonce SHA-256("abc") vector is 59 bytes.
- Both timestamp providers require one SignerInfo and one ESS signing-certificate attribute, then require the token to embed the certificate identified by the first ESSCertID or ESSCertIDv2. This is an inclusion check only: no signature, path, validity-period, revocation, or trust validation was added.
- Parsing consumes every outer CMS, SignedData, signer-info, attribute, signing-certificate, certificate-identifier, issuer/serial, message-imprint, and TSTInfo field. It rejects trailing data, non-minimal DER lengths, multiple SignerInfos, multiple ESS attributes, an unsupported ESS digest, malformed issuer/serial, and a wrong first CertID even when a later CertID is correct. Token parsing is capped at 8 MiB.
- SHA-1 ESSCertID and SHA-256/SHA-512 ESSCertIDv2 variants pass. Missing, wrong, or issuer/serial-mismatched certificates fail both providers without extending the destination.
- A synthetic 4096-bit root/intermediate plus 2048-bit TSA chain totals 3-6 KiB; its token is under 8 KiB and the timestamped CMS is under 12 KiB, both fitting the default 16 KiB reservation. The fixed 8 MiB appendix bound therefore has orders of magnitude of headroom for this realistic chain.
- OpenSSL-generated tokens now contain the TSA certificate. pyHanko validates the document timestamp with only `root.pem` configured; neither the test nor its validation script supplies the TSA certificate out of band.

## Changes made

- Updated `Rfc3161TimeStampRequest.cs`, `Rfc3161TimeStampToken.cs`, both timestamp providers' XML documentation, `SignatureTests.cs`, `SigningInteropTests.cs`, `OpenSslTimestampTests.cs`, and `Fixtures/validate_doc_timestamp.py`.
- Updated `README.md` and only the applicable timestamp rows of `ACCEPTANCE.md` to describe `certReq` TRUE, certificate inclusion, and the separation between inclusion and trust.

## Verification run

- `SignatureTests` in Release: 44 passed, 0 failed on each of `net8.0`, `net6.0`, and `net48`.
- `OpenSslTimestampTests` in Release, with `JAYTWO_OPENSSL=C:\Program Files\Git\mingw64\bin\openssl.exe`: 2 passed, 0 failed on each of `net8.0`, `net6.0`, and `net48`.
- `SigningInteropTests.pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature` in Release: 1 passed, 0 failed on each of `net8.0`, `net6.0`, and `net48`.
- `dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj -c Release --no-restore`: all four library targets built, 0 warnings and 0 errors.
- Tools: OpenSSL 3.2.4, Python 3.13.1, pyHanko 0.38.0, .NET SDK 10.0.100-rc.1.25451.107.

## Evidence missing

- Independent QA sign-off and the final full-suite run remain with QA and the Technical Lead.

## Handoff

QA Engineer independently verifies the negative ESS cases, root-only OpenSSL/pyHanko interoperability, non-extension guarantees, and all three frameworks. Technical Lead reviews the strict-DER boundary and the distinction between certificate inclusion and trust.
