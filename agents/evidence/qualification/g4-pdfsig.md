# G4 second verifier: Poppler pdfsig

Work item: W-14. Gate: `QUALIFICATION.md` section 7. Poppler `pdfsig` is the second PDF-aware verifier beside pyHanko 0.38.0, run on the same signed bytes in the same test. This record covers the library's signed output as shipped on 2026-10-21: `/adbe.pkcs7.detached` through `IPdfExternalSigner`, RSA PKCS#1 v1.5 with SHA-256 and, since W-70, RSA PKCS#1 v1.5, ECDSA P-256, and ECDSA P-384, each with SHA-256, SHA-384, and SHA-512. PAdES B-B has not shipped; W-71 adds its `pdfsig` cases. G4 is not passed.

## Tool

| | Windows | Linux (CI `base` image) |
| --- | --- | --- |
| `pdfsig` | 26.08.0, MSYS2 UCRT64 `mingw-w64-ucrt-x86_64-poppler` 26.08.0-1 | 26.01.0, Ubuntu 26.04 `poppler-utils` 26.01.0-2ubuntu0.2 |
| Crypto backend | NSS 3.131.1 (`mingw-w64-ucrt-x86_64-nss` 3.131.1-1, NSPR 4.40-1) | NSS 3.120 (`libnss3`) |
| `certutil` | beside `pdfsig.exe` (same NSS package) | `libnss3-tools` 3.120-1ubuntu2.1 |
| Install | MSYS2 base `msys2-base-x86_64-20260927.sfx.exe` extracted to `%LOCALAPPDATA%\jaytwo-tools\msys64`, then `pacman -S mingw-w64-ucrt-x86_64-poppler`. The `JAYTWO_POPPLER` build (Poppler 26.09.0, used for `pdftotext`) has no `pdfsig`. | `Dockerfile` `base` stage: `poppler-utils` was already installed; W-14 adds `libnss3-tools`. |

Tests find `pdfsig` with `ExternalTool.Locate("JAYTWO_PDFSIG", ...)` (else `JAYTWO_TOOLS` or `PATH`), and `certutil` through `JAYTWO_CERTUTIL`, else beside `pdfsig`, else `PATH` outside Windows (on Windows `PATH` finds the unrelated system `certutil.exe`). A missing tool fails the test.

## Configuration

`Pdfsig.Verify` (`test/jaytwo.Ergonomics.Pdf.Tests/Pdfsig.cs`) creates a fresh NSS database (`certutil -N -d sql:<dir> --empty-password`) per run, adds only the roots the test passes with trust flags `,C,` (trusted CA for S/MIME; `pdfsig` validates the signer for S/MIME signing, so `C,,` leaves the root untrusted), and runs `pdfsig -nssdir sql:<dir> -no-ocsp [-upw password] signed.pdf`. Nothing from the user's or system's NSS database is used.

## Properties established

`SigningInteropTests`, each case also validated by pyHanko 0.38.0 through `Fixtures/validate_signature.py` on the same bytes. `pdfsig` rows name only what `pdfsig` reported; rows of the signing matrix (`.agents/skills/pdf-signing-change/references/signing-matrix.md`) not listed are not established by `pdfsig`.

| Test | Output | `pdfsig` establishes | pyHanko establishes |
| --- | --- | --- | --- |
| `pdfsig_and_pyhanko_verify_new_and_existing_field_signatures_and_trust_the_chain_from_the_root` | New field `Signature1` on `pypdf-base.pdf`; existing empty field `ExistingSig` of `pyhanko-sigfield.pdf`; leaf, intermediate, and root embedded | One signature; field name; signer common name; `adbe.pkcs7.detached`; SHA-256; `Signed Ranges` equal to the file's `/ByteRange`; `Total document signed`; `Signature is Valid.`; `Certificate is Trusted.` with only the root configured, and `Certificate issuer isn't Trusted.` with no root while the signature stays valid; exit 0 | intact, valid, trusted from the root alone, coverage `ENTIRE_FILE`, intermediate and root embedded |
| `pdfsig_and_pyhanko_verify_a_timestamped_signature_an_encrypted_file_and_a_later_revision` | RFC 3161 token from the OpenSSL test authority as an unsigned attribute; AES-256 revision 6 file (`qpdf-encrypted.pdf`, user password), signed with a CMS signing time at the Unix epoch since W-97; a metadata revision after signing | For each: valid and trusted, ranges equal to `/ByteRange`. Timestamped and encrypted: `Total document signed`. Later revision: `Not total document signed`, ranges unchanged by the later save | Timestamped: token intact and trusted against the TSA root. Encrypted: intact, valid, trusted, `ENTIRE_FILE`. Later revision: intact, valid, trusted, `ENTIRE_REVISION` |
| `encrypted_signature_dictionary_strings_read_in_pypdf_and_verify_in_pyhanko_and_pdfsig` (W-97) | `pypdf-base.pdf` encrypted by qpdf with 40-bit RC4 (revision 2), 128-bit RC4 (revision 3), AES-128 (revision 4), and AES-256 (revision 6), signed under the user or the owner password with `/Reason`, `/Location`, `/ContactInfo`, `/Name`, and a CMS signing time at the Unix epoch | Valid and trusted from the root, ranges equal to `/ByteRange`, `Total document signed`, exit 0, so `pdfsig` read the encrypted `/M` without crashing | intact, valid, trusted, `ENTIRE_FILE`. pypdf 6.21.1 read the five strings as passed |
| `pyhanko_fieldmdp_lock_on_an_encrypted_file_holds_and_the_allowed_fill_keeps_the_signature` (W-97) | pyHanko approval signature with a FieldMDP `Include [Locked]` lock on a qpdf-encrypted form (128-bit RC4 and AES-256), then a library fill of `Open` | After the fill: valid, ranges equal to `/ByteRange`, `Not total document signed`, exit 0 | The pyHanko signature before the fill: intact and valid. pyHanko 0.38.0 cannot grade the fill: its difference analysis raises `TypeError` on the encrypted lock's field names |
| `pdfsig_and_pyhanko_reject_a_tampered_byte_and_a_signature_from_another_key` | One byte of the signed range changed; a signature value made with a key other than the certificate's | Tampered: `Digest Mismatch.`, exit 1, no `Signature is Valid.`. Other key: `Signature is Invalid.`, exit 1, no `Signature is Valid.` | Tampered: `intact=false`. Other key: intact byte range, signature not valid (new `invalid` check) |
| `pkcs11_keys_sign_every_digest_and_pyhanko_and_pdfsig_trust_the_chain` (W-70; was `pkcs11_signature_matches_pkcs1_and_pyhanko_and_pdfsig_trust_the_chain`) | SoftHSM 2 RSA 2048 (`CKM_RSA_PKCS` over a host-built DigestInfo), P-256, and P-384 (`CKM_ECDSA` over the raw digest) keys through `IPdfExternalSigner`, each with SHA-256, SHA-384, and SHA-512; intermediate and root embedded | As the first row, signer `PKCS11 Leaf`, trusted from the root alone, `Signing Hash Algorithm` equal to the chosen digest | intact, valid, trusted, `ENTIRE_FILE`; `md_algorithm` and `pkcs7_signature_mechanism` equal to the chosen digest and key (`sha384_ecdsa`, for example) |
| `pyhanko_and_pdfsig_trust_every_key_and_digest` (W-70) | In-memory RSA 2048, P-256, and P-384 leaves issued by an RSA root, each with SHA-256, SHA-384, and SHA-512; leaf and root embedded | One signature; signer common name; `adbe.pkcs7.detached`; `Signing Hash Algorithm` `SHA-256`, `SHA-384`, or `SHA-512` as chosen; ranges equal to `/ByteRange`; `Total document signed`; valid; trusted from the root | intact, valid, trusted, `ENTIRE_FILE`, with the chosen `md_algorithm` and mechanism |
| `pyhanko_trusts_the_timestamp_on_an_ecdsa_signature` (W-70) | P-256 with SHA-256 and P-384 with SHA-512, with an RFC 3161 token from the OpenSSL test authority over the SHA-256 of the DER-encoded ECDSA value | As the row above | As the row above, and the token intact, valid, and trusted against the TSA root |

So `pdfsig` independently establishes, for this output: byte-range integrity over exactly the recorded `/ByteRange`; the CMS signature value under the embedded signer certificate (and its failure for another key); whether the signed range reaches the end of the file; and trust under an explicitly configured test root, kept separate from integrity. It rejects each tampered case.

## Limits of pdfsig

- **No modification analysis.** A later revision is reported only as `Not total document signed`, whatever it changes (W-92 found the same for attachments and metadata). DocMDP, FieldMDP, and "prohibited writes" are graded by pyHanko only.
- **Embedded RFC 3161 tokens are not checked.** The token on a signature is neither reported nor verified; pyHanko and `openssl ts -verify` cover G5.
- **Document timestamps are not verified.** On the library's `/ETSI.RFC3161` revision, `pdfsig` 26.08.0 reports `Signature Type: unknown` and `Signature has not yet been verified.` and exits 1 (observed 2026-10-21; W-92 saw the same with 26.01.0). That is not a false success, but it is no verification.
- **Certificate validity is checked at the current time, not the signing time.** A signature dated 1970 by a certificate issued yesterday is reported `Certificate is Trusted.`.
- **The exit code covers the signature only.** It is 0 for a valid signature from an untrusted signer, so tests read the report lines.
- **Crash on an unreadable `/M`.** When the CMS signing time is missing or at the Unix epoch, `pdfsig` falls back to the dictionary's `/M`; if `/M` is not a date it exits with an access violation before printing a report. Before W-97 the library wrote `/M` in plaintext on an encrypted file, `pdfsig` decrypted it to bytes that are not a date, and the encrypted case had to sign with a current time. Since W-97 `/M` is encrypted and the encrypted cases sign at the epoch.
- No revocation (`-no-ocsp`), no AIA fetching, and no signature inspection comparison: the library's inspection API has not shipped (W-72).

## Fixtures

| File | SHA-256 |
| --- | --- |
| `Fixtures/pypdf-base.pdf` | `f2a70d070564dd6b2a8f63bee1e1f1119a46fbb51fb644fff178c7a16c107120` |
| `Fixtures/pyhanko-sigfield.pdf` | `1b95f440159cd947a069736510e2ca95a99d43e94abe58e613eed9cb801d79b1` |
| `Fixtures/qpdf-encrypted.pdf` | `a4096a9567f6e07370c0832465d36000f354b2c2096b109ef21643bcb20270be` |

Keys, certificates, and the TSA PKI are generated per run.

## Results

Recorded at the W-14 commit (base `W-96`), Release and Debug builds, GA SDK 10.0.402:

- Windows 11, `SigningInteropTests` 8/8 on each of `net10.0`, `net8.0`, `net6.0`, and `net48`, with `pdfsig` 26.08.0, pyHanko 0.38.0, pypdf 6.21.1, Python 3.13, qpdf 12.4.2, SoftHSM 2, OpenSSL 3.2.4. Result: pass.
- Linux, `make docker-builder docker-test` (Ubuntu 26.04 image): 3526/3526 on each of `net10.0`, `net8.0`, and `net6.0`, the four tests above among them, with `pdfsig` 26.01.0. Result: pass.

The full-suite counts are in the W-14 closeout.

Recorded at the W-97 commit (base `W-97`), Release, GA SDK 10.0.402: `SigningInteropTests` 13/13, the five W-97 cases among them, on each of `net10.0`, `net8.0`, `net6.0`, and `net48` with `pdfsig` 26.08.0, and on Linux `net10.0`, `net8.0`, and `net6.0` with `pdfsig` 26.01.0 in `make docker-builder docker-test`. Result: pass. The full-suite counts are in the W-97 closeout.
