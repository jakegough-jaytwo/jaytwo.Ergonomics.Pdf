# B-05 Audit: Signing

## 1. Task and owner

Read-only audit of sealing against `ACCEPTANCE.md`. Signing Engineer. No edits under `src/`, `test/`, `README.md`, `ACCEPTANCE.md`, `QUALIFICATION.md`, or `agents/state/`. B-04 was not implemented.

The suite was not run. A row matches when the code does what the sentence says and a test in this repository asserts it. That is an inventory, not a result.

## 2. Verified facts

### status.md

The signing sentence in the technical baseline is true and narrower than the code.

- `HttpTimestampAuthority.ExtractToken` reads the PKI status integer, rejects `code > 1`, and returns the next sequence. It does not read the request. `src/jaytwo.Ergonomics.Pdf/Signatures/HttpTimestampAuthority.cs`.
- `CmsDetachedSignatureProvider` hashes the RSA signature value, calls `Rfc3161TimeStampRequest.Create`, and embeds the returned bytes when they are non-empty. It does not parse the token. `src/jaytwo.Ergonomics.Pdf/Signatures/CmsDetachedSignatureProvider.cs`.
- `Rfc3161TimeStampRequest.Create` writes version 1 and a SHA-256 `MessageImprint`. The XML doc says no nonce and `certReq` omitted. `src/jaytwo.Ergonomics.Pdf/Signatures/Rfc3161TimeStampRequest.cs`. The 56-byte vector in `rfc3161_request_matches_the_sha256_abc_vector` locks that encoding.
- `DocTimeStampSignatureProvider.CreateContents` does the same request and returns the token with no imprint, algorithm, nonce, or empty-token check. `src/jaytwo.Ergonomics.Pdf/Signatures/DocTimeStampSignatureProvider.cs`. Status names only the HTTP client and the CMS provider. B-04 already includes this provider.

Repository section, checked with `git rev-parse` and `git status --porcelain=v1`: `HEAD` is `11da851` (`genesis`) on `wip`, which matches the current status text. The sentence that the working tree is clean except `agents/reports/` does not. `agents/state/backlog.md`, `agents/state/board.md`, and `agents/state/status.md` are modified. Report that to the Technical Lead.

D-09 holds. `src/` has no PKCS#11 call. `Pkcs11Interop` is used from `SigningInteropTests` only.

### Sealing

All six rows match.

1. **Byte range, gap, prefix, one revision, xref, append, and cross-stream copy.** `/ByteRange` is four 10-digit integers (`PdfSerializer` placeholder, `PdfEngine.Ten`). The gap is `<`, reserved hex, `>` (`ContentsSlot`; gap length `2 + ReservedContentsBytes * 2`). `OpenSigned` splices that gap out. Tests: `SignatureTests.byte_range_excludes_the_contents_gap_and_a_small_gap_rolls_back` (four `\d{10}` fields, gap size, digest unchanged after a byte inside the gap), `incremental_revision_appends_a_classic_xref` (prefix, `/Prev`, 20-byte `CRLF` xref lines), `fill_and_metadata_share_one_revision_and_can_be_signed_together` (one new `startxref` for a fill, a title edit, and `Sign`), `signature_kind_must_match_the_provider_and_the_source_stream_can_be_signed` (same-stream `SaveIncremental()` and `Sign(options, provider)` grow the stream; a read-only stream throws `ArgumentException`). Cross-stream `Sign` copies into an empty destination in `ForeignPdfTests.Sign` and `SigningInteropTests.Sign` (`AssertPrefix` / the original bytes as a prefix). `ValidateDestination` rejects a non-empty destination. The non-empty assertion itself is `ScaleTests.cross_stream_save_copies_in_one_megabyte_reads` on `SaveIncremental`. `Sign` uses that same method. No test passes a non-empty destination to `Sign`.

2. **Oversized token, `/NeedAppearances`, kind mismatch, and signing a generated appearance.** `PdfEngine.Sign` throws `PdfSignatureTooLargeException` before `destination.Write`. `NeedAppearances` and a kind mismatch throw `PdfSignException` before the write. Tests: `byte_range_excludes_the_contents_gap_and_a_small_gap_rolls_back` (32-byte payload, 8-byte reservation, one `/ByteRange`, file ends at that range), `need_appearances_blocks_signing_without_writing`, `signature_kind_must_match_the_provider_and_the_source_stream_can_be_signed`, `fill_and_metadata_share_one_revision_and_can_be_signed_together` (`SetText` returns `Generated`, then one revision).

3. **Reuse an empty signature field.** `SealExistingField` reuses the widget, sets `/V` to a new signature dictionary, ORs `/SigFlags` with 3, rejects a non-signature type, a field that already has a signature dictionary, and a read-only field. Tests: `empty_signature_field_is_reused_and_a_second_sign_does_not_grow` (one field, original rect and page, `/SigFlags 3`, `/V` a new object, second `Sign` throws `PdfSignException` and the length stays), `text_field_name_is_rejected_without_writing`, `read_only_signature_field_is_rejected_without_writing`. The non-signature throw is one type check. The test uses a text field.

4. **`/ID` and signing time.** `Sign` creates `/ID` only when missing. `SaveIncremental` does not. CMS writes `/M` and the signing-time attribute `1.2.840.113549.1.9.5`. Tests: `two_signatures_and_existing_id_are_preserved`, `missing_id_is_created_only_when_signing`, `cms_signature_verifies_and_covers_the_byte_range` (`D:19700101000000Z` and UTCTime `700101000000Z`).

5. **CMS, chain, and signature timestamp.** `CmsDetachedSignatureProvider` puts the byte-range SHA-256 in `messageDigest` `1.2.840.113549.1.9.4`, signs the SHA-256 of the signed attributes through `IPdfExternalSigner.SignHash`, embeds `CertificateDer` plus `AdditionalCertificates`, and stores the token as unsigned attribute `1.2.840.113549.1.9.16.2.14` (tag `0xA1`). Tests: `cms_signature_verifies_and_covers_the_byte_range` and `cms_embeds_the_certificate_chain_and_a_timestamp_over_the_signature_value`. `SignedCms.CheckSignature(verifySignatureOnly: true)`, the attribute equality, and `Certificates.Count == 2` are inside `#if NET8_0_OR_GREATER`. Both tests still assert the range digest and the certificate bytes are present on `net6.0`. The timestamp test asserts the imprint of `LastSignature` is inside the request and that the OID bytes `2A864886F70D010910020E` are in the CMS. It does not decode the `0xA1` tag.

6. **RFC 3161 request, HTTP client, and document timestamp.** Tests: `rfc3161_request_matches_the_sha256_abc_vector`, `http_timestamp_authority_posts_the_query_and_returns_the_token` (media type `application/timestamp-query`, status 0, token returned; the handler does not assert the POST verb; the code sets `HttpMethod.Post`), `http_timestamp_authority_rejects_a_failure_status` (status 2), `document_timestamp_embeds_the_token_for_the_range_hash` (request equals `Create` of the range hash; token bytes `01020304` are in the file; `/DocTimeStamp`).

### Foreign files, signature rows

All seven match. pyHanko checks go through `Fixtures/validate_signature.py`. Exit 0 requires intact, cryptographically valid, and trusted, plus the requested coverage. These tests were not run.

- **qpdf revision 6, title change, and CMS signature.** `ForeignPdfTests.qpdf_encrypted_pdf_is_revised_signed_and_accepted`. Wrong password throws `PdfEncryptedException`. `AssertPrefix`. Reopen with `user-secret` reads `Sealed`. `qpdf --password=user-secret --check`. `Validate(..., "entire")`.
- **pyHanko signature, then a title change.** `pyhanko_signature_stays_intact_after_an_incremental_append`. `pyhanko-signed.pdf` starts with `pypdf-base.pdf`. The title revision starts with the signed file. `Validate(..., "revision")`.
- **Empty field `ExistingSig`.** `pyhanko_signature_field_is_signed_and_accepted`. The field is a signature with a null value, `FieldName` is `ExistingSig`, the original bytes stay a prefix, coverage is `entire`.
- **Chain trusted from the root alone.** `SigningInteropTests.pyhanko_trusts_an_embedded_chain_from_the_root_alone`. Trust anchor is the root. Checks include `chain` (`other_embedded_certs` at least 2). The test asserts `intact=true` and `trusted=true`.
- **HTTP timestamp token.** `pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature`. `HttpTimestampAuthority` posts to `Fixtures/tsa_server.py`. Checks include `timestamp`. The test asserts `timestamp_intact=true` and `timestamp_trusted=true`. The script also rejects a token that is not valid.
- **SoftHSM through `IPdfExternalSigner`.** `pkcs11_signature_matches_pkcs1_and_pyhanko_trusts_the_chain`. `RSA.VerifyHash` checks a probe digest (`pkcs11-probe`), not the PDF signature bytes. pyHanko then trusts the PDF chain from the root. The library does not load the module.
- **Integrity is not the modification policy.** `integrity_stays_distinct_from_the_modification_policy`, samples from `Fixtures/certify_samples.py`. One changed byte: `intact=false` (`broken`). Approval plus a metadata append: `intact=true`, `valid=true`, `modification=LTA_UPDATES`, `docmdp-ok`. pyHanko `P` 1 (`locked.pdf`): fill and metadata throw `PdfCertificationLockedException`. `P` 2 (`fillable.pdf`): fill is allowed, metadata throws, and the filled file is `intact=true` with `modification=FORM_FILLING` and `docmdp-ok`. That test does not call `Sign` on the `P` 1 file.

### Also in this area

- **AcroForm DocMDP / FieldMDP sentence.** Matches. `docmdp_blocks_writes_and_still_allows_reads` (dangling `/Perms /DocMDP` blocks fill and `Sign`; the field name is still read). `docmdp_form_fill_permission_allows_fill_and_signing_but_not_metadata` (`P` 2). `fieldmdp_all_blocks_a_fill_and_still_allows_a_read`. Gates are `CertificationLevel`, `EnsureSigningAllowed`, `EnsureFieldChange`, and `EnsureDocumentChange` in `PdfEngine`.
- **Opening sentence: signature-dictionary strings and `/Contents` stay plaintext.** Matches. `IsExempt` skips `/Type /Sig` and `/Type /DocTimeStamp`. `/Contents` is a `ContentsSlot`, written as hex. Test: `EncryptionTests.signature_dictionary_stays_plaintext` (`(Because)` stays literal).
- **Scale sentence, signing clauses only.** `ScaleTests.three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle`. `SilentProvider` records `PdfByteRange` and does not read the file. Both offsets are past 3 GiB. The rest of that row is the Core Engineer's.

## 3. Assumptions

A declared assertion is not a pass. Foreign and interop rows depend on qpdf, Python, pyHanko, pypdf, and, for SoftHSM, `JAYTWO_PKCS11`. They fail closed when a tool is missing. This audit did not see that happen.

## 4. Defects confirmed

The B-04 gap is real. It is not a miss against a current `ACCEPTANCE.md` row. D-14 requires a nonce and a matching token. The code accepts any non-empty token from PKI status 0 or 1.

`DocTimeStampSignatureProvider` has the same hole and does not reject an empty token. `CmsDetachedSignatureProvider` does reject an empty token (`PdfTimestampException`). Status.md does not mention the document-timestamp provider. B-04 already does. No new backlog item.

## 5. Evidence missing

- This audit did not run the suite.
- On `net6.0`, `SignedCms.CheckSignature` and the `messageDigest` attribute comparison are not compiled. Those assertions exist for `net8.0`.
- No test calls `Sign` with a non-empty destination. `SaveIncremental` does, through the same `ValidateDestination`.

## 6. Changes made or proposed

None. This report is the only file written.

## 7. Verification run

`git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`, `git status --porcelain=v1`. `HEAD` is `11da851`. No `dotnet test`. No qpdf, pyHanko, or SoftHSM run.

## 9. Handoff

QA Engineer verifies this audit. Technical Lead reconciles it and owns the status.md repository sentence. B-04 stays on the backlog until the Technical Lead puts it on the board after B-01.

## Behavior no criterion describes

Observations only. Not a backlog.

- PKI status 1 is accepted. `http_timestamp_authority_accepts_granted_with_mods`. README says 0 or 1. The sealing row names status 0 and status 2. Any status above 1 is rejected; only 2 is tested.
- `certReq` stays omitted. The 56-byte vector includes that.
- A document timestamp is its own later revision after a CMS signature. `document_timestamp_follows_a_cms_signature` (`/SubFilter /ETSI.RFC3161`, three `startxref`s). A document timestamp does not write `/M`.
- `ReservedContentsBytes` outside 1..1048576, and an appendix estimate over 8 MiB, throw `PdfSignException` before the file grows.
- A null `/V` is unsigned. `null_signature_value_is_signable`. A signature parent keeps its widget. `signature_parent_keeps_its_widget_and_receives_the_value`.
- `FieldMdpBlocks` implements `Include` and `Exclude`. No acceptance row and no test name them.
- `P` 3 uses the same fill and `Sign` gate as `P` 2. `EnsureDocumentChange` blocks metadata and attachments at every non-zero level. No test sets `P` 3, and no certification test edits an attachment. A resolved `/P 1` blocks `Sign` through `CertificationLevel() == 1`. The test that calls `Sign` uses a DocMDP reference that does not resolve.
- `Sign` writes an approval signature or a document timestamp. It does not write a DocMDP or FieldMDP dictionary.
- An empty additional certificate throws `PdfSignException`. A page number outside the document throws `PdfSignException`. A missing `FieldName` allocates `SignatureN`.
