# B-03. Evidence inventory

## Task and owner

QA Engineer. Mapped each `ACCEPTANCE.md` bullet to the tests that cover it and to a `team.md` section 6 level. Results are the B-02 run. No product code. `agents/state/backlog.md` was not edited. B-06 was not started.

## Verified facts

- `ACCEPTANCE.md` has 46 bullets. Each has at least one test. Those tests passed on `net8.0` and `net6.0` in B-02. See `agents/reports/2026-10-18-qa-engineer-B-02.md`.
- The inventory is the Evidence inventory section of `agents/state/status.md`. 35 rows are Internally tested. 11 are Independently validated. 0 have no test. None are Production-qualified.
- Independently validated rows are 35–45. The tool, version, and property are in that table. pypdf 6.21.1 was present for B-02 and no test invoked it. qpdf 12.4.2 on row 46 only rewrote the input; the memory bounds are the test's own measurements.
- `cms_signature_verifies_and_covers_the_byte_range` calls `SignedCms.CheckSignature(verifySignatureOnly: true)` only inside `#if NET8_0_OR_GREATER`. The `net6.0` pass does not execute that call.
- The Technical baseline in `agents/state/status.md` still says nobody has run the suite. B-02 did. That section was left as written. Its owner is the Technical Lead.

## Assumptions

- `LzwTests.hand_checked_vectors` input `ABABABA` is treated as covering the LZW code that is not in the table yet. The test does not name that clause.
- A row with a passing test stays Internally tested even when one clause of the bullet has no assertion. Those clauses are gaps below, not uncovered rows.

## Evidence missing

No acceptance bullet lacks a test. These clauses have no assertion. They are candidate backlog items for the Product Manager to file under B-06, not items added here.

1. `Open` reads the header from the first 1024 bytes. No test puts a header only after that window.
2. A revision 2 user password with the fill-in bit set and annotate clear cannot fill a field. `permissions_follow_the_security_revision` has no such bit pair.
3. The new trailer repeats `/ID` on an encrypted incremental save for RC4 revisions 2, 3, and 4, and for revision 5. Revision 4 AES and revision 6 assert the id bytes in `password_round_trips_strings_streams_and_the_prefix`. The others assert `/Encrypt` and the prefix.
4. The metadata appendix of the 3 GiB save is under 8 KiB. `three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle` does not measure it.
5. Kids inherit `/DA`. Geometry tests assert `/FT`, `/V`, and `/Ff`, not `/DA`.
6. A check box with no appearance gets a generated `Yes`/`Off` appearance. `simple_text_generates_helvetica_and_other_values_request_appearances` checks `Generated` and `IsChecked`, not those appearance names.
7. A radio mark is cubic Bezier curves, and `arc` is not an operator. No test reads that content stream. Poppler's pixel change on row 45 does not establish the operators.
8. `Sign(Stream, ...)` leaves the opened stream's bytes unchanged. `ScaleTests.cross_stream_save_copies_in_one_megabyte_reads` covers `SaveIncremental(Stream)` only.
9. `SignedCms.CheckSignature(verifySignatureOnly: true)` is not part of the `net6.0` test binary.
10. Row 45 does not assert that `Blue` is absent from the unfilled Poppler extraction. It asserts `Ada` is absent, and that both words are present after the fill.
11. Revision 4 strings and streams can use different ciphers. `EncryptionTests.revision4_can_mix_aes_strings_with_rc4_streams` sets `/StrF /Aes` and `/StmF /Rc4`, then asserts the title only. The file has no stream.
12. Other info edits refresh `ModDate` in UTC. `MetadataTests.trapped_is_a_name_and_info_dates_round_trip_as_text` sets `ModDate` explicitly, so it does not assert the refresh.

## Changes made or proposed

Filled the Evidence inventory section of `agents/state/status.md`. No other section of that file was edited.

## Verification run

No new test run. Levels use the B-02 results and the test bodies cited in the inventory. Tool versions from that run: qpdf 12.4.2, pyHanko 0.38.0, pypdf 6.21.1 (not invoked), Poppler 26.09.0, SoftHSM 2.5.0.

## Handoff

Technical Lead checks the inventory, and the stale sentence in the Technical baseline. Product Manager turns the gaps above into backlog items only as part of B-06, after this report.
