# B-15: Timestamp authority outside pyHanko

## 1. Task and owner

B-15, Signing Engineer. Under D-18, OpenSSL runs as the timestamp authority. A verifier that is not that authority checks a document timestamp.

## 2. Verified facts

- **Feasibility gate passed.** I ran a throwaway harness in the session scratchpad, outside the repo. It used the public API and `DocTimeStampSignatureProvider`.
  - (a) `openssl ts -reply` issued a token for the library's request. `openssl ts -query -text` showed `Nonce: 0x…` and `Certificate required: no`. The library's imprint, digest, and nonce check (B-04) accepted the token.
  - (b) `openssl ts -verify` accepted the embedded token.
- **pyHanko 0.38.0 cannot validate the token as issued.** `validate_pdf_timestamp` raises `signer certificate not included in signature [NO_SIGNING_CERTIFICATE_FOUND]`.
  - Cause: the library omits `certReq` (D-14). RFC 3161 section 2.4.1 then requires the TSA to leave `certificates` empty, and OpenSSL complies.
  - pyHanko looks for the signer certificate only in the token. `generic_cms.extract_certs_for_validation` has the comment `# TODO allow signer certificate to be obtained from elsewhere?`.
  - `openssl ts -verify` needs `-untrusted tsa.pem` for the same reason. Without it, the result is `PKCS7_get0_signers: signer certificate not found`.
- **The existing timestamp test does not show this gap.** `pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature` uses `DummyTimeStamper(certs_to_embed=…)`. That stamper embeds the certificate even when the request has no `certReq`, so the test passes anyway.
- **What the new test establishes.** This is independently validated, with an OpenSSL 3.2.4 authority and a test-only root as the only trust anchor:

  | Verifier | Intact: imprint equals SHA-256 over `/ByteRange` | Cryptographically valid | Trusted to the test root | Coverage |
  |---|---|---|---|---|
  | pyHanko 0.38.0 (`validate_pdf_timestamp`) | Yes | Yes | Yes | `ENTIRE_FILE` |
  | `openssl ts -verify` 3.2.4 | Yes | Yes | Yes | Not checked |

  - The TSA certificate carries critical `extendedKeyUsage = timeStamping`.
  - pyHanko and certvalidator are not OpenSSL. `openssl ts -verify` is the same toolchain as the authority, so it is corroboration only.
- **Negative checks.**

  | Change | pyHanko | `openssl ts -verify` |
  |---|---|---|
  | Wrong root (an untrusted self-signed root) | `trusted=false`, `intact=true` | `unable to get local issuer certificate` |
  | Header byte 7 flipped, inside `/ByteRange` | `intact=false` | `message imprint mismatch` |

## 3. Assumptions

- Each verifier receives the TSA certificate out of band.
  - pyHanko has no parameter for it. The fixture adds the certificate to the parsed `SignedData.certificates` in memory, not to the PDF. That field is unsigned.
  - pyHanko still checks the signed ESS `signing-certificate-v2` attribute against the certificate supplied this way.
  - `openssl ts -verify` gets the certificate through `-untrusted`.
- This is out-of-band certificate supply. It is not trust: the only trust anchor is the root. The test also asserts `embedded_certs=0`, which records the D-14 behavior.

## 4. Defects confirmed

None in the library. One finding goes to D-14, in section 8.

## 6. Changes made

- New `test/jaytwo.Ergonomics.Pdf.Tests/OpenSslTimestampTests.cs` with two tests:
  - `openssl_tsa_document_timestamp_is_accepted_by_pyhanko_and_openssl_verify`
  - `verifiers_reject_an_untrusted_root_and_a_tampered_byte`
- What the tests do:
  - Generate a root, a TSA certificate, and an untrusted root with `openssl req` and `openssl x509` in a temp directory.
  - Use a nested `OpenSslTimestampAuthority : ITimestampAuthority` that runs `openssl ts -reply -token_out`.
  - Stamp `pypdf-base.pdf` through `PdfDocument.Sign` with `PdfSignatureKind.DocTimeStamp`.
  - Check the embedded token against the issued token, then run both verifiers.
- New `test/jaytwo.Ergonomics.Pdf.Tests/Fixtures/validate_doc_timestamp.py`.
- I did not change `validate_signature.py`, `ExternalTool.cs`, or `src/`. No helper is needed from QA: the tests use `ExternalTool.Locate`, `Start`, `Output`, `Fixture`, and `TempDirectory` as they are.

## 7. Verification run

- **Environment:** the brief's env block, plus `JAYTWO_OPENSSL=C:\Program Files\Git\mingw64\bin\openssl.exe`.
- **Tool versions:** OpenSSL 3.2.4 (11 Feb 2025, mingw64). Python 3.13.1, pyHanko 0.38.0, pyhanko-certvalidator 0.33.0, asn1crypto 1.5.1.
- **Results:**

  | Command | Framework | Result |
  |---|---|---|
  | `dotnet test test/jaytwo.Ergonomics.Pdf.Tests -f net8.0 --filter "FullyQualifiedName~OpenSslTimestampTests"` | net8.0 | 2 passed, 0 failed |
  | Same, with `-f net6.0` | net6.0 | 2 passed, 0 failed |
  | `dotnet test … -f net8.0 --filter "FullyQualifiedName~SigningInteropTests"` | net8.0 | 4 passed, 0 failed (no regression) |

- The build ran clean with StyleCop warnings as errors on both targets. I did not run the full suite, per the brief.

## 8. Decisions needed

- **D-14 trigger.** The Product Manager should raise this with the owner. D-14 keeps `certReq` omitted "until a verifier needs the TSA certificate in the token."
  - pyHanko 0.38.0 needs it, for both document timestamps and signature timestamps.
  - Against any RFC 3161-compliant TSA, a library-produced timestamp therefore cannot be validated by pyHanko unless the caller supplies the TSA certificate out of band. That likely applies to other PDF validators too, though I have not verified any other.
  - Setting `certReq` true is a `src/` change to `Rfc3161TimeStampRequest`, and a change to a decided item. I did not make it.
  - If the owner sets `certReq`, the out-of-band step in `validate_doc_timestamp.py` and `-untrusted` can be removed, and the `embedded_certs=0` assertion flips.
- **G5 still has a gap.** No test checks a CMS signature timestamp from a non-pyHanko TSA. That is outside B-15, which asked for the document timestamp only.

## 9. Handoff

- QA Engineer: independent verification of B-15.
- Technical Lead: review of the two new test files.
- Product Manager: acceptance, and the D-14 item above to the owner.
