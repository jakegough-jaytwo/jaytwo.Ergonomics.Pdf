# B-05 Audit: Core

## 1. Task and owner

Read-only audit of the Core Engineer area against `ACCEPTANCE.md`. Core Engineer. No edits under `src/`, `test/`, `README.md`, `ACCEPTANCE.md`, `QUALIFICATION.md`, or `agents/state/`.

The suite was not run. A row matches when the code does what the sentence says and a test in this repository asserts it. That is an inventory, not a result.

AcroForm field semantics, appearances, CMS, timestamps, and the DocMDP/FieldMDP gate were left to those seats. Where a sentence also contains writer or encryption behavior, only that part is judged here.

## 2. Verified facts

### status.md

`git rev-parse` on `wip`: `HEAD` is `11da851` (`genesis`). That matches the repository section.

Two sentences do not match the tree. Report both to the Technical Lead.

- The repository section says the working tree is clean except new files under `agents/reports/`. `git status --porcelain=v1` also shows `agents/state/backlog.md`, `agents/state/board.md`, and `agents/state/status.md` modified.
- The technical baseline says nobody has run the suite in this review round. `agents/reports/2026-10-18-qa-engineer-B-02.md` and `agents/reports/2026-10-18-tech-lead-B-02-verify.md` record a run. This audit did not use that run, and does not claim any test passed.

`Internal/PdfEngine.cs` is 3,650 lines. The code map says about 3,650. The declared inventory is 107 `[Fact]` methods and 38 `[InlineData]` rows; a count of the test files matches. The code map does not list `Internal/IsExternalInit.cs`.

### Opening

1. **Non-seekable stream.** Matches. `PdfDocument.Open` rejects a stream that cannot seek with `ArgumentException` before a read. Test: `OpenTests.non_seekable_stream_is_rejected_before_any_read` (`ParamName` is `stream`, `ReadCalled` is false).

2. **Header, `startxref`, `/Prev`, object number.** Matches. `EnsureHeader` reads at most 1024 bytes. `FindStartXref` takes the last parseable `startxref` in the tail. A missing marker, a value past EOF, and a repeated `/Prev` offset throw `PdfParseException`. An object whose number differs from the xref entry throws from `PdfParser.ParseIndirect`. Tests: `OpenTests.preamble_is_preserved_and_offsets_include_it` (512-byte preamble, header not at byte 0), `last_startxref_wins_when_an_earlier_marker_exists`, `missing_startxref_is_a_parse_error`, `startxref_past_eof_is_a_parse_error`, `cyclic_prev_is_a_parse_error`, `object_number_mismatch_is_a_parse_error` (the mismatch is raised on `PageCount`, not inside `Open`).

3. **Standard security on open.** Matches except the stream half of mixed revision 4 ciphers. See section 5. Revisions 2, 3, 4, 5, and 6 open with the user or owner password. Strings and streams round-trip decrypted, including a literal string inside an encrypted object stream. A plaintext xref stream opens. An encrypted xref stream throws `PdfParseException`. `/Encrypt` is taken from the xref as an uncompressed object; a tail-only xref stream does not read an 8 MiB hole. Revision 5 truncates UTF-8 to 127 bytes and does not treat a decomposed password as the composed one. Revision 6 prepares with SASLprep-style mapping and NFKC, then truncates (`PrepareRevision6`). An empty user password opens when no password is passed. The wrong password, a missing password, and a non-standard handler throw `PdfEncryptedException`. Tests: `EncryptionTests.password_round_trips_strings_streams_and_the_prefix` (`Document(false)` is revision 4 AES; `Document(true)` is revision 6), `rc4_round_trips_strings_streams_and_the_prefix` (revision 2 at 40 bits, revision 3 at 40 and 128, revision 4 RC4), `revision5_round_trips_and_keeps_the_password_bytes`, `revision5_truncates_the_password_to_127_bytes`, `revision6_accepts_the_nfkc_form_of_the_password`, `empty_user_password_opens_without_a_password` (revisions 2 through 6), `empty_revision4_user_password_opens`, `detected_empty_user_password_keeps_the_permission_bits`, `missing_and_wrong_passwords_are_rejected`, `unsupported_handlers_are_refused`, `object_stream_strings_are_plaintext_inside_the_encrypted_stream`, `encrypted_xref_stream_is_rejected`, `plaintext_xref_stream_opens_with_the_password`, `plaintext_xref_stream_in_the_tail_does_not_read_the_middle`. `CreateRevision3` accepts a `/Length` from 40 to 128 bits in steps of 8. The tests use the endpoints.

4. **Incremental save of an encrypted file.** Matches. The original bytes stay a prefix. The new trailer repeats `/Encrypt` and `/ID` (`PdfEngine` trailer writer). New strings and streams are encrypted; a type `/Sig` or `/DocTimeStamp` dictionary is exempt (`PdfSecurity.IsExempt`); `/Contents` is a `ContentsSlot` written as hex. Reopening with the same password reads the update. Tests: `password_round_trips_strings_streams_and_the_prefix`, `revision5_round_trips_and_keeps_the_password_bytes`, `rc4_round_trips_strings_streams_and_the_prefix` (prefix, `/Encrypt`, plaintext literals absent, decrypted title and attachment), `signature_dictionary_stays_plaintext` (`(Because)` stays literal). `ForeignPdfTests.qpdf_encrypted_pdf_is_revised_signed_and_accepted` keeps the prefix across a title change plus `Sign`. The pyHanko checks in that test belong to the Signing Engineer.

5. **User password limited to form fill.** Matches. `RequireFill` allows a fill when `CanFill` is set. `RequireModify` blocks metadata and attachments. `RequireAnnotate` blocks `Sign`. The owner password sets `CanModify` without changing the reported bits. `/EncryptMetadata false` leaves a metadata stream in the clear. `/EFF /Identity` leaves an embedded-file stream in the clear (`CipherFor`). Tests: `EncryptionTests.user_password_is_limited_to_the_permission_bits` (revision 4 AES and revision 6: fill succeeds, metadata, attachment, and `Sign` throw `PdfEncryptionRestrictedException`, owner save is readable), `clear_metadata_and_identity_attachments_are_not_encrypted` (`xpacket` and `CLEARTEXT` in the appendix). The fill itself is the AcroForm setter. The gate is the engine.

6. **`Permissions`.** Matches. `PdfPermissions.FromStandard` uses print, modify, copy, and annotate below revision 3, and the later bits from revision 3 on. An unencrypted file is `AllowAll`. The owner password reports the same bits. A revision 2 file with the fill-in bit set and annotate clear cannot fill. Tests: `EncryptionTests.permissions_follow_the_security_revision` (the `r2 none` case sets bits 9–12 and expects fill to throw `PdfEncryptionRestrictedException`), `unencrypted_file_ignores_a_password`.

7. **Headers `%PDF-1.0` through `%PDF-1.7`, and an unread content stream.** Matches. An incremental save keeps the original header because the original bytes stay a prefix. Page walks do not call `ReadDecoded` or `OpenBody`. Test: `OpenTests.versions_before_1_7_open_and_save_keeps_the_header` (`1.0` through `1.6`, each with an `LZWDecode` content stream of one byte, header kept, prefix kept). `%PDF-1.7` is `PdfBuilder`'s default and is inside the prefixes asserted by the metadata and encryption saves.

8. **Classic xref endings.** Matches. `PdfParser.ReadClassicEntry` accepts 20-byte CRLF, 20-byte SP LF, 20-byte SP CR, and 19-byte LF. Tests: `OpenTests.space_lf_xref_entries_open`, `space_cr_xref_entries_open`, `lf_only_xref_entries_open`. CRLF is `PdfBuilder.Finish`'s default (`crlfEntries: true`) and is what `huge_size_does_not_require_a_dense_xref_array` opens.

9. **Xref streams, object streams, hybrid `/XRefStm`.** Matches. `AppendXrefStream` writes Flate with PNG predictor 12. Classic entries are loaded with overwrite, then `/XRefStm` is loaded without overwrite. Tests: `OpenTests.xref_stream_with_predictor_and_object_stream_reads_info` (title from an object stream), `hybrid_classic_xref_overrides_the_xref_stream` (title `Classic`, not `Stream`).

10. **`/Size` and preamble.** Matches. The xref is a `Dictionary<int, XrefEntry>`, not an array of `/Size`. A preamble stays a prefix and offsets include it. Tests: `OpenTests.huge_size_does_not_require_a_dense_xref_array` (`/Size 2000000000`), `preamble_is_preserved_and_offsets_include_it`.

11. **Dispose.** Matches. `Dispose` does not save. `leaveOpen: false` disposes the stream. Test: `OpenTests.dispose_does_not_save_and_closes_the_stream`.

### Scale

1. **3 GiB sparse file.** Matches except the 8 KiB metadata-appendix clause. See section 5. `ScaleTests.three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle` opens, reads the title, saves on the same stream, and signs. `TouchedHole` is false. `BytesRead` is asserted under 2 MiB. `SilentProvider` records `PdfByteRange` and does not read the file. Both range offsets are past 3 GiB. The signer contract is the Signing Engineer's; the engine supplies the range through `OpenSigned`.

2. **Cross-stream copy.** Matches. `CopyPrefix` reads at most 1 MiB per call. A non-empty destination throws `PdfException` before a write. Test: `ScaleTests.cross_stream_save_copies_in_one_megabyte_reads` (`MaxRequested` at most 1 MiB, bytes read less than twice the source plus 1 MiB, source bytes unchanged, destination length stays 1).

3. **1,000 pages.** Matches. `WalkPages` inherits the media box, the crop box, and rotation. An omitted crop box becomes the inherited crop box, or the media box when none is inherited. Content objects placed in a hole are not read. Test: `AcroFormTests.thousand_page_form_does_not_read_content_holes` (count 1000, page 1000, inherited media box 500 by 600, `/Rotate 90` on that page, crop box equal to the media box, `TouchedHole` false). The field's page number is this page index. Inherited crop, and a page crop that replaces it, are `AcroFormTests.crop_box_inherits_and_defaults_to_the_media_box`. The same page-box sentence in the AcroForm section matches on those two tests.

4. **Past 9,999,999,999.** Matches. `SaveIncremental` throws `PdfTooLargeForClassicXrefException` when `_length` is above `MaxClassicOffset`, before `CopyPrefix`. Test: `ScaleTests.classic_xref_refuses_offsets_past_ten_digits_without_writing` (length 10,000,000,000, destination length 0, source length unchanged, hole unread).

### Metadata

1. **Info dictionary.** Matches. Title, author, subject, keywords, creator, and producer round-trip. Non-ASCII text is UTF-16BE (`FEFF`). PDFDoc bytes `0x80` and `0xA0` map to U+2022 and U+20AC. A custom key survives. The dictionary is readable and writable by key. Creator and producer stay put when they are not set. `Trapped` is the name `True`, `False`, or `Unknown`; anything else throws `ArgumentException`. Tests: `MetadataTests.info_round_trips_unicode_and_keeps_custom_keys`, `pdfdoc_bytes_decode_bullet_and_euro`, `trapped_is_a_name_and_info_dates_round_trip_as_text` (reads `False`, rejects `Maybe`, writes `True`). The test does not write `Unknown`. `SetTrapped` allows it.

2. **Dates.** Matches except the refresh clause. See section 5. `D:YYYYMMDDHHmmSSZ` and `D:YYYYMMDDHHmmSS±HH'mm'` parse, including a value written back through the info key. Setting `ModificationDate` sets `_modDateExplicit`, and a later title edit does not replace it. Tests: `MetadataTests.dates_accept_zulu_and_offset_forms`, `trapped_is_a_name_and_info_dates_round_trip_as_text`, `explicit_modification_date_is_not_replaced`.

3. **XMP.** Matches. No XMP: an info edit creates a packet and `XmpEditor.ApplyInfo` updates the matching field (`dc:title`, `dc:description`, `dc:creator`, `pdf:Keywords`, `pdf:Producer`, `xmp:CreatorTool`, `xmp:CreateDate`, `xmp:ModifyDate`). Well-formed XMP is written back onto the existing metadata object, including `xmp:ModifyDate`. Malformed XMP does not fail `Open`; `OpenXmp` throws `PdfXmpException`; `SyncXmp` returns without writing. `SetXmp` stores the given bytes. Tests: `MetadataTests.absent_xmp_is_created_and_well_formed_xmp_is_updated` (created packet contains the title and `ModifyDate`; an existing packet contains the new title and `ModifyDate`), `malformed_xmp_stays_byte_for_byte_while_info_updates`, `set_xmp_stores_the_exact_bytes`. The absent-XMP test sets the title, so it exercises `dc` and `xmp`. It does not set keywords or producer, so it does not assert `pdf:Keywords` or `pdf:Producer`. Those mappings are in `XmpEditor.ApplyInfo`.

### Attachments

1. **Bytes, size, names, remove.** Matches. Added bytes round-trip, including `0x00` and `0xFF`. `/Params /Size` is the byte length. `WriteAttachments` sorts by the raw name and writes both the `EmbeddedFiles` name tree and `/AF`. Lookup by name works. A missing name throws `PdfAttachmentNotFoundException` on open and on remove. `Remove` rewrites both arrays from the remaining slots. Test: `AttachmentTests.binary_attachment_round_trips_size_and_sorted_names`.

2. **Flate, DCT, copy size, seekable content.** Matches. An existing FlateDecode embedded file is inflated when the attachment is opened. DCTDecode throws `PdfUnsupportedFilterException` and does not return the compressed bytes. A new attachment is copied in reads of at most 64 KiB (`CopyFileBody`). The content stream must be seekable. Tests: `AttachmentTests.flate_attachment_is_inflated_and_dct_is_refused`, `binary_attachment_round_trips_size_and_sorted_names` (`MaxRequested` at most 65536), `non_seekable_attachment_is_rejected`.

3. **LZW.** Matches. EarlyChange 0 and 1, a clear code, and the code equal to the next table entry (`aaa` / `30C0A020`, decoder branch `code == _nextCode`) decode. `/Filter [/ASCII85Decode /LZWDecode]` runs ASCII85 first. Abbreviations `LZW`, `AHx`, `A85`, and `Fl` are accepted. `PdfStreamDecoder.Decode` throws `PdfObjectTooLargeException` above the caller cap. Tests: `LzwTests.early_change_zero_and_one_use_different_widths_and_both_decode`, `clear_code_resets_the_table`, `hand_checked_vectors_decode_and_match_the_encoder`, `ascii85_then_lzw_follows_filter_array_order`, `abbreviations_decode_hex_base85_and_flate`, `decoded_output_above_the_cap_is_refused`, `lzw_attachment_and_xmp_round_trip_through_the_document`.

4. **Run length.** Matches. A length byte of 0–127 copies that many plus one. A length byte of 129–255 repeats. A length byte of 128 ends the stream and the bytes after it are ignored (`004180FF` decodes to `A`). `RL` is accepted. A truncated run throws `PdfParseException`. Tests: `RunLengthTests.hand_checked_runs_decode`, `repeat_of_128_and_literal_of_128_decode`, `truncated_literal_and_repeat_are_parse_errors`, `expanded_run_above_the_cap_is_refused`, `run_length_attachment_opens`.

### Writer clauses in the sealing section

Matches. The original bytes stay a prefix. A second save's `/Prev` points at the previous xref. A fill and a metadata edit written by one `Sign` are one revision. New xref lines are 20 bytes and end in CRLF. `SaveIncremental()` and `Sign(options, provider)` append. A read-only opened stream is rejected with `ArgumentException`. `SaveIncremental(Stream)` and `Sign(Stream, ...)` copy into an empty destination and leave the opened stream unchanged. A missing `/ID` is created in `Sign` and not in a metadata-only save. Tests: `SignatureTests.incremental_revision_appends_a_classic_xref`, `fill_and_metadata_share_one_revision_and_can_be_signed_together`, `signature_kind_must_match_the_provider_and_the_source_stream_can_be_signed`, `missing_id_is_created_only_when_signing`, `ScaleTests.cross_stream_save_copies_in_one_megabyte_reads`. `ForeignPdfTests.Sign` copies into a new destination from a read-only source and `AssertPrefix` checks the result. The Signing Engineer audited this same sealing row. Byte-range, CMS, and timestamp clauses were not judged here.

### Foreign files, core rows

The tests call qpdf. This audit did not run them.

- **qpdf xref stream and object stream.** Matches. `ForeignPdfTests.qpdf_object_stream_is_read_revised_and_accepted` requires `/ObjStm` and `/XRef`, asserts title `Foreign base` before the edit, author `pypdf` after it, keeps the original bytes, and calls `qpdf --check`.
- **qpdf revision 6.** The core clauses match. `qpdf_encrypted_pdf_is_revised_signed_and_accepted` rejects `wrong-password` with `PdfEncryptedException`, keeps the prefix, and reopens `Sealed` with `user-secret`. The CMS signature and pyHanko result are the Signing Engineer's.
- **qpdf attachment.** Matches. `qpdf_attachment_survives_an_incremental_revision` reads `foreign-attachment` for `note.txt` before and after a title change, keeps the prefix, and calls `qpdf --check`.
- **Dense memory.** Matches. `DenseMemoryTests.peak_managed_memory_does_not_follow_a_dense_file` opens a file whose content stream is 32 MiB (file length asserted above 32 MiB), opens a qpdf object-stream rewrite of that file, saves a title change, and asserts allocated bytes under 8 MiB, within 3 MiB of the 8 MiB file of the same shape, and a sampled heap peak under 8 MiB.

### Engine primitives the other seats call

`RequireFill`, `RequireModify`, and `RequireAnnotate` are the encryption gates. `EnsureDocumentChange` and `EnsureFieldChange` are the certification gates; those policies were not audited. `SaveIncremental` and `Sign` are the one writer. Object streams are decoded in `ReadDecoded` and released by `ReleaseAfter`. `ObjectStreamCacheTests.field_walk_releases_object_streams_and_rereads_them` asserts the release. The second-read field values are the AcroForm row.

## 3. Assumptions

A declared assertion is not a pass. Foreign and dense-memory tests fail closed when qpdf is missing. This audit did not see that happen.

`Document(false)` in `EncryptionTests` builds revision 4 AES, not revision 5. Revision 5 is `AesDocument(Kind.Revision5)`.

## 5. Evidence missing

Three clauses are implemented and not asserted. No code was found that contradicts them.

- **Other info edits refresh `ModDate` in UTC.** `TouchModDate` writes `DateTimeOffset.UtcNow` through `FormatDate`, which emits `Z` when the offset is zero. No test changes a field without setting `ModificationDate` and then reads `ModDate`.
- **The metadata appendix of the 3 GiB save is under 8 KiB.** `three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle` saves and then signs, and never measures the metadata appendix. The writer has no 8 KiB check. The 8 MiB figure in `Sign` is the signature-appendix estimate.
- **Revision 4 strings and streams can use different ciphers.** `CreateRevision4` reads `StrF` and `StmF` separately. `revision4_can_mix_aes_strings_with_rc4_streams` decrypts the title and the file has no stream, so the RC4 stream filter is not used.

This audit did not run the suite.

Smaller gaps, not separate rows: `Trapped` name `Unknown` is allowed and not written by a test. Revision 6 preparation-before-truncation is the order in `PrepareRevision6`; the NFKC test uses a short password and does not cross 127 bytes. No test places the `%PDF-` header at the 1024-byte boundary.

## 7. Verification run

`git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`, `git status --porcelain=v1`. `HEAD` is `11da851`. No `dotnet test`.

## 9. Handoff

QA Engineer verifies this audit. Technical Lead reconciles it into the planning report.

## Behavior no criterion describes

- `EnsureHeader` accepts a `%PDF-2.` header. The opening criteria name `%PDF-1.0` through `%PDF-1.7`.
- `FindStartXref` searches 64 KiB from the end, then 1 MiB. A sole `startxref` farther back is `PdfParseException`.
- The four classic endings are exact. A 21-byte entry ending in SP CR LF is not one of them. `ReadClassicEntry` consumes SP and one of CR or LF, and does not skip whitespace before the next entry.
- `PdfStreamDecoder.Decode` takes the caller cap. `OpenDecoded`, which `PdfAttachment.Open` uses, does not. A predictor forces a buffered read at `StructuralCap` (32 MiB). Xref streams use `XrefCap` (64 MiB).
- Added files get `/AFRelationship /Unspecified`. The reader walks name-tree `Kids`. The writer emits one flat `Names` array.
- LZW with TIFF predictor 2 is implemented and asserted by `LzwTests.tiff_predictor_runs_after_lzw`. The criteria name PNG predictor 12 on xref streams.
- Object-stream payloads are cached and released at the end of a public call (`RetainedObjectStreamCount`).
