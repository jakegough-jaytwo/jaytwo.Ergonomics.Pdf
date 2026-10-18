# Backlog

Owner: Product Manager. Ordered by priority. Each item follows the template in `roles/product-manager.md`. Seeded on 2026-10-18 from the review round in `agents/reviews/`. The 2026-10-18 planning report confirmed this order. B-04, B-21, B-24, B-26, and B-27 are closed. B-13, B-14, B-15, B-17, and B-18 stay open and are not on the board. B-25 is active in review. B-28 is active and returned for corrections. B-29 is not boarded; proposed changes are already present in the mixed working tree and remain unaccepted.

## B-01. Closed

The working tree is committed on `wip`. HEAD is `11da851` "genesis". `git ls-tree HEAD` lists `src/`, `test/`, and `agents/`. The worktree is clean except new report files. `main` and `develop` remain `b832f22` and contain only `LICENSE`. Acceptance passes on `wip`. It does not block parallel editing.

## B-02. Closed

The suite ran on 2026-10-18. `net8.0` and `net6.0` each recorded 145 executed, 145 passed, and 0 failed. TRX: `%LOCALAPPDATA%\Temp\jaytwo-b02\b02-net8.trx` and `b02-net6.trx`. Reports: `agents/reports/2026-10-18-qa-engineer-B-02.md` and `agents/reports/2026-10-18-tech-lead-B-02-verify.md`. The Technical Lead accepted the run.

## B-03. Closed

The evidence inventory is in `agents/state/status.md`. The QA report records 46 `ACCEPTANCE.md` bullets, each with at least one test from the B-02 run: 35 internally tested, 11 independently validated, none production-qualified. Report: `agents/reports/2026-10-18-qa-engineer-B-03.md`.

## B-04. Closed

Accepted. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-04.md`: `SignatureTests` passed 24/24 on `net8.0` and on `net6.0`. `pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature` passed on `net8.0`. pyHanko 0.38.0 reported the embedded token intact, valid, and trusted. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-04-review.md`. That review had not seen the `net6.0` run; the QA report supplies it. `ACCEPTANCE.md` keeps the 56-byte no-nonce vector and adds the three throw-and-do-not-extend rows. No existing row was removed or loosened.

## B-05. Subsystem audits against `ACCEPTANCE.md`

- **Kind:** missing evidence. **Implementers:** Core Engineer, AcroForm Engineer, Signing Engineer, one report each.
- **Problem:** no seat has checked its own area against the criteria under the new ownership.
- **Scope:** read-only. For each row in your area, note whether the code and tests match it. Note behavior in your area that no criterion describes. Do not propose product features.
- **Acceptance:** one report per seat in the `team.md` format.

## B-06. Closed

The gap list is `agents/reports/2026-10-18-qa-engineer-B-06.md`. No gate is passed, and none is marked out of scope for 1.0. Library-side imprint and nonce checks stay in B-04. B-03 clauses 4, 11, and 12 stay in B-10, B-11, and B-09. The other candidates are B-12 through B-21. None of those are on the board.

## B-07. Closed

Framing the target-framework decision is no longer needed. The owner decided it under D-13.

B-08 through B-11 are not on the board. B-04 stays the first code change. B-04 claims `ACCEPTANCE.md`. None of these four items edit that file. An edit to it waits until that claim ends. B-12 through B-21 are the B-06 gaps. They are not on the board.

## B-08. Closed

Accepted. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-08.md`: `AcroFormTests` passed 14/14 on `net8.0` and on `net6.0`. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-08-review.md`. A choice field with no `/Opt` and Edit clear throws `PdfFieldValueException` and does not dirty the document. A field with the Edit flag still accepts an unlisted value. `ACCEPTANCE.md` was not changed by this task. The choice sentence is the same.

## B-09. Closed

Accepted. QA recorded `info_edit_refreshes_moddate_in_utc` in `agents/reports/2026-10-18-qa-engineer-B-09.md`: passed 1, failed 0, on `net8.0` and on `net6.0`. An info edit that does not set `ModificationDate` writes `ModDate` in UTC, and a later read returns that UTC value. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-09-review.md`. That review had seen only `net8.0`. The QA report supplies the `net6.0` run. `ACCEPTANCE.md` was not changed. The metadata row is the same.

## B-10. Closed

Accepted. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-10.md`. `three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle` passed on `net8.0` and on `net6.0`. Both runs measured a metadata appendix of 886 bytes, before the signature. 886 is under 8 KiB. The hole and byte-read checks still passed. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-10-review.md`. `ACCEPTANCE.md` was not changed. The scale row is the same.

## B-11. Closed

Accepted. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-11.md`. `revision4_rc4_stream_decrypts_beside_an_aes_string` passed on `net8.0` and on `net6.0`. Opening with password `user` returns title `Secret` from the AES string cipher and the 40 embedded-file bytes from the RC4 stream cipher. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-11-review.md`. `ACCEPTANCE.md` was not changed. The opening row is the same.

## B-12. Closed

Accepted as the support-matrix document. G1 is not passed. The matrix is the **Support matrix** section in `README.md`. It separates read from modify. Opening a signed file is a read. Password and signature fields throw `PdfFieldValueException` and do not dirty the document. The Technical Lead approved it in `agents/reports/2026-10-18-tech-lead-B-12-review.md`, with no mismatches. That review does not pass G1.

## B-13. Grow the foreign corpus for G2

- **Kind:** missing evidence.
- **Problem:** The QA report counted six committed foreign PDFs from three producers (pypdf, qpdf, pyHanko). Gate G2 asks for at least 50 fixtures from four producers, including a real-world file not built for this library.
- **Why now:** G2 is release-blocking. The current corpus is the starting point, not the gate.
- **Scope and exclusions:** Add distinct, meaningful fixtures until those counts are met, including classic xref, xref streams, object streams, hybrid references, incremental revisions, nested page trees, encrypted files, forms, signature fields, metadata, and embedded files. Do not pad the corpus with trivial variations. Not on the board.
- **Acceptance criteria:** At least 50 fixtures from at least four producers, including one real-world file. Supported fixtures pass their applicable checks. Deliberately unsupported cases fail predictably. Modified output passes `qpdf --check`. `QUALIFICATION.md` section 4.
- **Evidence required:** Independently validated. Name the tool, version, and property. Not on the board.
- **Dependencies:** None. An `ACCEPTANCE.md` edit waits while B-04 claims that file.

## B-14. Second PDF-aware signature verifier

- **Kind:** missing evidence.
- **Problem:** pyHanko 0.38.0 is one PDF-aware verifier. `SignedCms.CheckSignature` ran on `net8.0` only, on CMS bytes the test assembled. That pair is not two PDF verifiers. pdfsig was not run.
- **Why now:** Gate G4 requires two independent verifiers, one of which understands PDF signature semantics.
- **Scope and exclusions:** Run pdfsig, or another PDF-aware verifier that is not pyHanko. Do not count `SignedCms` as that verifier. Do not add signature validation to the library. Not on the board.
- **Acceptance criteria:** The second verifier checks the supported signed output, and the record names the tool, its version, and the property it established. `QUALIFICATION.md` section 6.
- **Evidence required:** Independently validated. Not on the board.
- **Dependencies:** None. An `ACCEPTANCE.md` edit waits while B-04 claims that file.

## B-15. Timestamp authority outside pyHanko

- **Kind:** missing evidence.
- **Problem:** `Fixtures/tsa_server.py` uses pyHanko's `DummyTimeStamper`, and the pyHanko 0.38.0 check is the same toolchain. No independent verifier has checked a document timestamp from a different authority.
- **Why now:** Gate G5 requires an independently implemented RFC 3161 authority, and a document timestamp is a separate capability.
- **Scope and exclusions:** Use a timestamp authority that is not pyHanko. Have a verifier that is not that authority check a document timestamp. The library-side imprint, digest algorithm, and nonce checks stay in B-04. Do not add TSA trust validation to the library. Not on the board.
- **Acceptance criteria:** A token from the non-pyHanko authority is embedded, and an independent verifier accepts the document timestamp under an explicit test trust configuration. The record names the authority, the verifier, their versions, and the properties established. `QUALIFICATION.md` section 7.
- **Evidence required:** Independently validated. Not on the board.
- **Dependencies:** B-04 for the nonce-bearing request the authority must answer. This item does not repeat that work. An `ACCEPTANCE.md` edit waits while B-04 claims that file.

## B-16. Closed

Accepted as the Poppler checks that were added. G6 is not passed. There is no second renderer, and the gate's acceptance is wider than this set. The first version was not signed off: `agents/reports/2026-10-18-qa-engineer-B-16.md` records that the center check was only an 80-point gap. The corrected test requires the center `Align` word in x 174–180. QA signed that off in `agents/reports/2026-10-18-qa-engineer-B-16-alignment.md`: `AppearanceRenderTests` passed 2/2 on `net8.0` and on `net6.0`. Poppler 26.09.0. The Technical Lead approved the corrected test in `agents/reports/2026-10-18-tech-lead-B-16-review.md`.

## B-17. G7 large-file workloads

- **Kind:** missing evidence.
- **Problem:** The dense test is 8 MiB and 32 MiB. The attachment test copies 200 KiB. The 1,000-page test is a sparse read and does not extract, edit metadata, and sign. The 3 GiB case is sparse, and its metadata-appendix measurement is B-10.
- **Why now:** Gate G7 is release-blocking. Sparse files do not meet the file-backed workloads.
- **Scope and exclusions:** File-backed 1 GiB dense PDF, a 250 MiB attachment added and extracted, and a 500-page extract, metadata edit, and sign. Record timing. The Technical Lead and QA approve fixture-specific memory budgets before G7 is called met. Do not treat the proposed 256 MiB and 512 MiB figures as already approved. Not on the board.
- **Acceptance criteria:** Those three workloads complete correctly on real file-backed inputs. Peak memory and timing are recorded. G7 stays unmet until the fixture-specific budgets are approved. `QUALIFICATION.md` section 9.
- **Evidence required:** Internally tested measurements. Not on the board.
- **Dependencies:** B-10 remains the 3 GiB metadata-appendix assertion. An `ACCEPTANCE.md` edit waits while B-04 claims that file.

## B-18. Fuzz suite and independent security review

- **Kind:** missing evidence.
- **Problem:** No fuzz harness and no independent security review exist. Some malformed-input tests exist, so gate G8 is not empty.
- **Why now:** G8 is release-blocking.
- **Scope and exclusions:** A fixed-seed fuzz suite aimed at the standard's 24 CPU-hour target, then a smaller continuous suite. An independent review of the parser and the signer by someone who did not implement that code. Confirmed defects become regression tests. Do not log secrets. Not on the board.
- **Acceptance criteria:** The fuzz run records seed, entry points, and duration. The review records the reviewer and the findings. `QUALIFICATION.md` section 10.
- **Evidence required:** Internally tested fuzz regressions, and an independent review. Not on the board.
- **Dependencies:** None. An `ACCEPTANCE.md` edit waits while B-04 claims that file.

## B-19. Closed

Accepted. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-19.md`. `external_signer_failure_does_not_extend_the_file` and `timestamp_authority_failure_does_not_leave_a_timestamped_signature` passed 2/2 on `net8.0` and on `net6.0`. A throwing external signer leaves the destination bytes unchanged. A throwing timestamp authority throws `PdfTimestampException` and does not leave a signature field. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-19-review.md`. That review had not seen the `net6.0` run. The QA report supplies it. This is not an imprint, digest-algorithm, or nonce rejection. Gates G4, G5, and G9 are not passed.

## B-20. Closed

Accepted as the build and package record. G10 is not passed. QA recorded it in `agents/reports/2026-10-18-qa-engineer-B-20.md`. Debug builds of `netstandard2.0` and `netstandard2.1` each succeeded with 0 warnings and 0 errors. `dotnet pack` rebuilt `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0` and wrote `jaytwo.Ergonomics.Pdf.0.1.0.nupkg`. The package's only dependency is `System.Memory` 4.5.5 on `netstandard2.0`. The other target frameworks have an empty dependency group. qpdf, pyHanko, Poppler, SoftHSM, and Pkcs11Interop are not in the package. The Technical Lead approved the record in `agents/reports/2026-10-18-tech-lead-B-20-review.md`. No target was dropped. The test suite was not run on either netstandard target.

## B-21. Closed

Every clause is closed. Clauses 4, 11, and 12 are B-10, B-11, and B-09.

- **Kind:** missing evidence.
- **Problem:** B-03 listed acceptance clauses with no assertion. Clauses 4, 11, and 12 are already B-10, B-11, and B-09. Dispositions:
  1. Closed. `header_after_the_first_1024_bytes_is_not_the_header` passed on `net8.0` and on `net6.0`. A header at offset 1016 opens. A header at offset 1024 throws `PdfParseException`. QA: `agents/reports/2026-10-18-qa-engineer-B-21.md`. Technical Lead: `agents/reports/2026-10-18-tech-lead-B-21-review.md`.
  2. Closed. `revision2_fill_in_without_annotate_cannot_fill_a_field` passed on `net8.0` and on `net6.0`. The user password reports annotate and form fill clear, `SetText` throws `PdfEncryptionRestrictedException`, and the stream length stays unchanged. Same reports.
  3. Closed. `rc4_round_trips_strings_streams_and_the_prefix` and `revision5_round_trips_and_keeps_the_password_bytes` passed on `net8.0` and on `net6.0`. The Core filter was 7/7 on each. Revisions 2, 3, 4 RC4, and 5 keep `/ID` `0102030405060708090A0B0C0D0E0F10` in the incremental appendix. Same reports.
  5. Closed. `kids_inherit_default_appearance` passed on `net8.0` and on `net6.0`. The parent `/DA` is inherited. The AcroForm filter was 3/3 on each. Same reports.
  6. Closed. `bare_check_box_appearance_is_named_yes_and_off` passed on `net8.0` and on `net6.0`. The generated appearance is named `/Yes` and `/Off`. Same reports.
  7. Closed. `radio_mark_is_cubic_curves_without_arc` passed on `net8.0` and on `net6.0`. The appendix contains `c` and does not contain `arc`. Same reports.
  8. Closed. `sign_to_another_stream_leaves_the_opened_stream_unchanged` passed on `net8.0` and on `net6.0`. The opened stream's bytes stay equal to the input. The other destination is longer, and its prefix equals the input. QA: `agents/reports/2026-10-18-qa-engineer-B-21-clauses-8-9.md`. Technical Lead: `agents/reports/2026-10-18-tech-lead-B-21-sign-review.md`. That review had not seen the `net6.0` run. The QA report supplies it.
  9. Closed. `SignedCms.CheckSignature(verifySignatureOnly: true)` is compiled only for `net8.0`, inside `#if NET8_0_OR_GREATER`. The test project references `System.Security.Cryptography.Pkcs` only on `net8.0`. The `net6.0` build does not compile those calls. Same reports. No new test was added.
  10. Closed. The unfilled claim was a wrong acceptance sentence, not a library defect. `pdftotext` of unfilled `pypdf-acroform.pdf` prints `Red` and `Blue`, and `Ada` is absent. The library did not write `Blue`. Evidence: `agents/reports/2026-10-18-qa-engineer-B-21-clause10.md`. The corrected sentence is in `ACCEPTANCE.md`.
- **Acceptance criteria:** Clauses 1, 2, 3, 5, 6, 7, 8, 9, and 10 are closed.

## B-22. Closed

Accepted as the inventory refresh. No qualification gate is passed. No capability is production-qualified. QA updated the evidence inventory and the release-readiness note in `agents/reports/2026-10-18-qa-engineer-B-22.md`. The inventory names the evidence from B-04, B-09, B-10, B-11, B-12, B-16, B-19, B-20, and B-21. The `net6.0` timestamp interop run stays not run, and that is not a pass. The Technical Lead approved it in `agents/reports/2026-10-18-tech-lead-B-22-review.md`. That review does not pass a gate.

## B-23. Closed

Accepted as the independent-reader evidence. G3 is not passed. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-23.md`. pypdf 6.21.1 confirmed Info title `InfoEdited`, XMP `dc:title` `x-default` `XmpEdited`, and the exact `notes.bin` bytes. The test passed 1/1 on `net8.0` and on `net6.0`. It does not call `qpdf --check`. The Technical Lead approved it in `agents/reports/2026-10-18-tech-lead-B-23-review.md`. That review does not pass G3.

## B-24. Closed

Accepted as the inventory record of the B-23 reader. G3 is not passed. QA recorded the reader in `agents/reports/2026-10-18-qa-engineer-B-24.md`. The G3, metadata, and attachment rows name pypdf 6.21.1, Info title `InfoEdited`, XMP `dc:title` `x-default` `XmpEdited`, and `notes.bin` bytes `00 01 02 7F 80 FE FF` followed by ASCII `B-23`. The release-readiness note still says no qualification gate is passed. The Technical Lead approved it in `agents/reports/2026-10-18-tech-lead-B-24-review.md`. That review does not pass G3.

## B-25. Failure diagnostics omit secrets

- **Kind:** missing evidence.
- **Problem:** `QUALIFICATION.md` gate G9 requires that failure handling does not expose passwords, PINs, or key material through diagnostics. No test shows that.
- **Why now:** G9 is release-blocking, and this check is missing.
- **Scope and exclusions:** Tests only. No `src/` change unless a check fails, and then stop with a defect report. Do not pass G9. Do not log a password, a PIN, or key material in the test output. Active. Technical Lead review refused approval in `agents/reports/2026-10-18-tech-lead-B-25-review.md`, because the PIN criterion cannot be met test-only: no PIN reaches the `ThrowingSigner` failure path. The Product Manager re-scopes the PIN criterion or cites accepted prior evidence. Does not reopen B-19.
- **Acceptance criteria:** A failing operation's diagnostic text does not contain a password, a PIN, or key material. The test output does not print those values. G9 is not passed. `QUALIFICATION.md` section 11.
- **Evidence required:** Internally tested. Not on the board.
- **Dependencies:** None.

## B-26. Closed

Accepted as the parser review. G8 is not passed. The Signing Engineer reported seven defects in `agents/reports/2026-10-18-signing-engineer-B-26.md`. The Technical Lead confirmed all seven from the code in `agents/reports/2026-10-18-tech-lead-B-26-review.md`. None were executed. This is not the 24 CPU-hour fuzz run. B-18 stays open. That review does not pass G8.

## B-27. Closed

Accepted as the signer review. G8 is not passed. The AcroForm Engineer reported two defects in `agents/reports/2026-10-18-acroform-engineer-B-27.md`. The Technical Lead confirmed both from the code in `agents/reports/2026-10-18-tech-lead-B-27-review.md`. A negative PKI status is treated as success. A long-form DER length of `0x7FFFFFFF` misses the bounds check and throws `ArgumentOutOfRangeException`. Neither input was executed. This review does not pass G8. B-18 stays open.

## B-28. Bound the seven confirmed parser paths

- **Kind:** defect. **Implementer:** Core Engineer.
- **Problem:** The B-26 review confirmed seven parser defects from the code. None were executed. Comment skipping, `/Prev` chains, cyclic `/Length` and object streams, `Resolve` reference cycles, `WalkField`, and `WalkNames` can recurse or loop without a depth cap or a seen set. A malformed xref `/Index`, xref field width, or object-stream header throws `IndexOutOfRangeException`, `FormatException`, or `OverflowException` instead of `PdfParseException`.
- **Why now:** Those paths are confirmed. A bounded fix is the next product step. G8 stays unpassed.
- **Scope and exclusions:** One bounded fix in `PdfParser.cs`, `PdfEngine.cs`, and the tests that fail today. Depth caps and seen sets for comment skipping, `/Prev` chains, cyclic `/Length` and object streams, `Resolve` reference cycles, `WalkField`, and `WalkNames`. Malformed xref `/Index`, xref field width, and object-stream headers throw `PdfParseException`. No parser rewrite. A major redesign is out of scope and would be an owner decision. Do not pass G8. Does not replace B-18. Active and returned for unchecked narrowing in `/Index`, `/N`, and `/First`.
- **Acceptance criteria:** Each of the seven confirmed paths has a regression test that expects `PdfParseException` or the existing documented refusal, and the file is not extended. `QUALIFICATION.md` section 10. G8 is not passed.
- **Evidence required:** Internally tested. Not a gate pass. Not on the board.
- **Dependencies:** B-26, which is closed. Does not replace B-18.

## B-29. Bound the two confirmed signer paths

- **Kind:** defect. **Implementer:** Signing Engineer.
- **Problem:** The B-27 review confirmed two signer defects from the code. Neither input was executed. `HttpTimestampAuthority.ExtractToken` treats a negative PKI status as success because `ReadInteger` folds a 4-byte INTEGER such as `02 04 80 00 00 00` into a signed value that is not greater than 1. `DerCodec.ReadTlv` adds `index + length` in an unchecked `int`, so a long-form length of `0x7FFFFFFF` wraps the bounds check and `Slice` throws `ArgumentOutOfRangeException`.
- **Why now:** Those paths are confirmed. A bounded fix is the next product step. G8 stays unpassed.
- **Scope and exclusions:** Bound the two confirmed paths in `DerCodec.cs` and `HttpTimestampAuthority.cs`, with regression tests. A negative PKI status is rejected with `PdfTimestampException` and does not extend the file. A long-form DER length that would wrap the bounds check throws `PdfTimestampException`, not `ArgumentOutOfRangeException`, and does not extend the file. Do not reopen nonce, imprint, or the throwing-signer work. Do not pass G8. Does not replace B-18. The test file is `SignatureTests.cs`, which B-25 still owns, so this item stays off the board until that claim ends. Proposed B-29 code and tests are already present in the mixed working tree but are not accepted or authorized for further work.
- **Acceptance criteria:** A negative PKI status throws `PdfTimestampException` and does not extend the file. A long-form DER length that would wrap the bounds check throws `PdfTimestampException`, not `ArgumentOutOfRangeException`, and does not extend the file. `QUALIFICATION.md` section 10. G8 is not passed.
- **Evidence required:** Internally tested. Not a gate pass. Not on the board.
- **Dependencies:** B-27, which is closed. Waits until B-25 releases `SignatureTests.cs`. Does not replace B-18. Does not reopen B-04 or B-19.
