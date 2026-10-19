# Backlog

Owner: Product Manager. Ordered by priority. Each item follows the template in `roles/product-manager.md`. Seeded on 2026-10-18 from the review round in `agents/reviews/`. The 2026-10-18 planning report confirmed this order. B-04, B-21, B-24, B-25, B-26, B-27, B-28, B-29, B-30, B-31, B-32, B-15, B-33, and B-17 are closed. B-13, B-14, B-18, and B-34 stay open and are not on the board.

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

## B-15. Closed

Accepted at the independently validated level for the document timestamp. G5 is not passed. OpenSSL 3.2.4 `openssl ts -reply` (D-18) answered the library's own nonce-bearing request, and `DocTimeStampSignatureProvider` embedded the token through `PdfDocument.Sign`. pyHanko 0.38.0 `validate_pdf_timestamp` reported the document timestamp intact, valid, trusted, and `ENTIRE_FILE`, with the test root as the only trust anchor. `openssl ts -verify` returned `Verification: OK` over the `/ByteRange` bytes, which is corroboration from the authority's own toolchain. A different root gives `trusted=false`, and a flipped byte gives `intact=false`. The Signing Engineer reported in `agents/reports/2026-10-18-signing-engineer-B-15.md`. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-15.md`: `OpenSslTimestampTests` 2/2 and `SigningInteropTests` 4/4 on `net8.0` and `net6.0`. Both tests fail when OpenSSL is missing. A mutation that reissues the TSA certificate fails the ESS signing-certificate check. QA fixed a stderr pipe deadlock in `ExternalTool`. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-15-review.md`. Limitations: the token has no certificates because the request omits certReq (D-14). pyHanko accepts it only when the test supplies the TSA certificate out of band. That certificate is added to the parsed token in memory, not to the PDF. This is open decision D-O6. No CMS signature timestamp from an authority other than pyHanko is tested yet.

## B-33. Closed

Accepted as the `net48` test target under D-19, at the internally tested level. G10 is not passed. The test project now targets `net8.0`, `net6.0`, and `net48`. On `net48` it runs against the library's `netstandard2.0` build. The library targets did not change. No test is excluded on `net48`: every external-tool test ran there. `TestCompat.cs` supplies the APIs that .NET Framework lacks, and forwards to the original APIs on `net6.0` and `net8.0`. Two test-only additions apply to `net48`: a `System.Net.Http` reference, and `System.Runtime.CompilerServices.Unsafe` 6.0.0 for the test host's binding redirect. The new `stalled_timestamp_body_that_ignores_the_token_is_aborted_by_disposing_the_response` covers the B-31 abort-by-dispose path. With the dispose registration removed, it fails on all three frameworks. QA reported in `agents/reports/2026-10-18-qa-engineer-B-33.md`. The task was returned once for that missing test. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-33-review.md` after re-running `SignatureTests` on `net48`: 37 of 38 passed. The one failure is B-34, a library defect. It is recorded and left failing. `netstandard2.1` still has no test run.

## B-34. Header-phase timestamp timeout lacks `TimeoutException` on .NET Framework

- **Kind:** defect.
- **Problem:** On `net48`, `SignatureTests.stalled_timestamp_response_headers_still_time_out` fails. The `TaskCanceledException` has no `TimeoutException` inner exception (`SignatureTests.cs:186`). `HttpTimestampAuthority` leaves the header phase to `HttpClient`'s own timeout exception, and only .NET 5+ adds the inner exception. B-31 claimed the same exception shape for both phases. The timeout itself works.
- **Why now:** The regression test already exists and fails on `net48`, so the full suite is red on .NET Framework.
- **Scope and exclusions:** Make the header-phase timeout produce `TaskCanceledException` with a `TimeoutException` inner exception on every framework. Change only `HttpTimestampAuthority`. No public API change.
- **Acceptance criteria:** `stalled_timestamp_response_headers_still_time_out` passes on `net48`, `net6.0`, and `net8.0`, and the other stall tests still pass.
- **Evidence required:** Internally tested. Not on the board.
- **Dependencies:** None. Owner: Signing Engineer.

## B-16. Closed

Accepted as the Poppler checks that were added. G6 is not passed. There is no second renderer, and the gate's acceptance is wider than this set. The first version was not signed off: `agents/reports/2026-10-18-qa-engineer-B-16.md` records that the center check was only an 80-point gap. The corrected test requires the center `Align` word in x 174–180. QA signed that off in `agents/reports/2026-10-18-qa-engineer-B-16-alignment.md`: `AppearanceRenderTests` passed 2/2 on `net8.0` and on `net6.0`. Poppler 26.09.0. The Technical Lead approved the corrected test in `agents/reports/2026-10-18-tech-lead-B-16-review.md`.

## B-17. Closed

Accepted at the internally tested level, against the D-20 working budget. G7 is not passed. `LargeWorkloadTests` runs three file-backed workloads on `net8.0`, `net6.0`, and `net48`. The first is a 1,073,915,943-byte dense PDF: inspect, metadata edit, and sign, with peak heap growth of at most 3.06 MiB. Inspecting it reads 8.6 MB of the file. The second is a 268,439,553-byte attachment, added and extracted through file streams, at most 0.30 MiB, and its hashes match. The third is a 500-page form with 1,000 fields and object streams: extraction, edit, and sign, at most 10.31 MiB. All 1,000 fields match, and `qpdf --check` passes. QA reported in `agents/reports/2026-10-18-qa-engineer-B-17.md`. The Technical Lead re-ran `net8.0` with matching numbers and approved in `agents/reports/2026-10-18-tech-lead-B-17-review.md`. The approved fixture-specific budgets are in `state/status.md`. Limitations: the suite asserts the D-20 ceilings, not the tighter budgets, because the heap counter is process-wide. Timing is from one machine in Debug with coverage on. The object-stream file allocates 90–175 MiB per operation through repeated decoding (O1). These G7 criteria are not covered: repeated use and dispose, encrypted xref-stream discovery, output and decompression limits, and a timing baseline.

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

## B-25. Closed

Accepted under the re-scoped wording, at the internally tested level. G9 is not passed, and G9's PIN line stays open. The first review refused approval in `agents/reports/2026-10-18-tech-lead-B-25-review.md`, because no PIN reaches the `ThrowingSigner` failure path. The Product Manager re-scoped the PIN criterion in `agents/reports/2026-10-18-product-manager-B-28-B-25.md`: no library API receives a PIN, so B-25 asserts no PIN omission. The password and key-material criteria are unchanged. QA concurred and signed off in `agents/reports/2026-10-18-qa-engineer-B-25-rescope.md`. `failed_open_diagnostic_omits_the_password_and_file_key` and `failed_sign_diagnostic_omits_the_password_and_file_key` passed 2/2 on `net8.0` and on `net6.0`. A rejected `Open` and a failed `Sign` of a revision 6 file do not expose the passwords or the file key in the message, `ToString()`, source, help link, data, or inner exceptions. The key is checked as hex, Base64, and raw bytes. The run logs had 0 matches for those values. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-25-re-review.md`. No `src/` change was made, and `README.md` and `ACCEPTANCE.md` were not changed. `external_signer_diagnostic_is_sanitized_and_does_not_extend_the_file` is not B-25 evidence and was not accepted under any task. G9 still needs its own assessment. That assessment covers the PIN clause, a host signer exception that carries its own PIN, and host-configured `HttpClient` credentials. B-25 releases `EncryptionTests.cs` and `SignatureTests.cs`. Does not reopen B-19.

## B-26. Closed

Accepted as the parser review. G8 is not passed. The Signing Engineer reported seven defects in `agents/reports/2026-10-18-signing-engineer-B-26.md`. The Technical Lead confirmed all seven from the code in `agents/reports/2026-10-18-tech-lead-B-26-review.md`. None were executed. This is not the 24 CPU-hour fuzz run. B-18 stays open. That review does not pass G8.

## B-27. Closed

Accepted as the signer review. G8 is not passed. The AcroForm Engineer reported two defects in `agents/reports/2026-10-18-acroform-engineer-B-27.md`. The Technical Lead confirmed both from the code in `agents/reports/2026-10-18-tech-lead-B-27-review.md`. A negative PKI status is treated as success. A long-form DER length of `0x7FFFFFFF` misses the bounds check and throws `ArgumentOutOfRangeException`. Neither input was executed. This review does not pass G8. B-18 stays open.

## B-28. Closed

Accepted as the bounded fix for the seven parser paths. G8 is not passed. The first version was returned in `agents/reports/2026-10-18-tech-lead-B-28-review.md` for unchecked narrowing in xref `/Index` and object-stream `/N` and `/First`. The rework is `agents/reports/2026-10-18-core-engineer-B-28-rework.md`, uncommitted on `wip`. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-28.md`: `OpenTests` passed 47/47 on `net8.0` and on `net6.0`. Each of the seven paths, and each of the 16 rework cases, throws `PdfParseException` and leaves the source bytes unchanged. Without the fix, all seven original facts failed, and 13 rework cases failed and the zero-width huge-count cases hung. The Technical Lead approved the rework in `agents/reports/2026-10-18-tech-lead-B-28-re-review.md`. The four library targets built with 0 warnings and 0 errors. `README.md` and `ACCEPTANCE.md` were not changed, because no documented behavior moved. Limitations: the seven original facts assert only the exception type, and an indirect `/N` or `/First` is refused as before, now with an accurate message. The full suite was not run. This is not the fuzz run. B-18 stays open. The predictor `/Columns` allocation found in that review is B-30.

## B-29. Closed

Accepted as the bounded fix for the two signer paths. G8 is not passed. The fixes are in `DerCodec.ReadTlv`, which now checks `length > data.Length - index`, and `HttpTimestampAuthority.ExtractToken`, which now rejects `code < 0 || code > 1`. Both were committed at `ee10c84`. The Technical Lead boarded the item in `agents/reports/2026-10-18-tech-lead-B-29-board.md`. The Signing Engineer found in `agents/reports/2026-10-18-signing-engineer-B-29.md` that the committed negative-status test passed even with its fix reverted. The test now asserts the refusal phrase. That tightening in `SignatureTests.cs` is uncommitted on `wip`. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-29.md`: `SignatureTests` passed 31/31 on `net8.0` and on `net6.0`. Status `02 04 80 00 00 00` and DER length `30 84 7F FF FF FF` each throw `PdfTimestampException` and leave the stream bytes equal to the input. With only its own fix reverted, each test fails on both frameworks. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-29-review.md`. The four library targets built with 0 warnings and 0 errors. `README.md` already says the authority accepts PKI status 0 or 1, and `ACCEPTANCE.md` was not changed. The full suite was not run. This is not the fuzz run. B-18 stays open. The unbounded TSA response buffer found in that review is B-31.

## B-30. Closed

Accepted as the bounded fix for the predictor `/Columns` allocation, at the internally tested level. G8 is not passed. `PdfStreamDecoder.Unpredict` now computes the row width in `long` and refuses one of `int.MaxValue` bytes or more. `UnpredictPng` returns before any buffer sized from `/Columns` when no full row exists. `PdfParser.EnsureXrefColumns` now calls `ReadWidths` before it sums `/W`, so the sum is at most 24. The fix and its tests are uncommitted on `wip`. The Technical Lead boarded the item in `agents/reports/2026-10-18-tech-lead-B-30-board.md` and set the bound: a per-thread allocation delta under 8,388,608 bytes. The Core Engineer reported in `agents/reports/2026-10-18-core-engineer-B-30.md`. The first review, `agents/reports/2026-10-18-tech-lead-B-30-review.md`, approved the source and returned the tests, because reverting `PdfParser.cs` alone failed no test. The rework added `wrapping_xref_widths_are_refused_before_they_become_predictor_columns`. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-30.md`: `OpenTests` passed 51/51 on `net8.0` and on `net6.0`. `/W [268435455 0 0]`, an explicit `/Columns 268435455`, and `/W [4563402751 0 0]`, each with `/Predictor 12` and empty decoded data, throw `PdfParseException`, allocate under the bound, and leave the stream bytes unchanged. With both fixes reverted, those cases allocated 268,526,576 to 268,543,352 bytes and failed. With only `PdfParser.cs` reverted, the new `/W` test failed on both frameworks. `xref_stream_with_predictor_and_object_stream_reads_info` passed in every tree. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-30-re-review.md`. The four library targets built with 0 warnings and 0 errors. `README.md` and `ACCEPTANCE.md` were not changed, because no documented behavior moved. Limitations: an explicit `/Columns` on a non-xref predictor stream is covered by reading the code only. `IntOf` still narrows `/Columns`, `/Colors`, and `/BitsPerComponent` without a range check, which is no longer an allocation path. A malformed `/W` is now refused before the decode, with the `/W` message. The full suite was not run. This is not the fuzz run. B-18 stays open.

## B-31. Closed

Accepted as the bounded fix for the timestamp response size, at the internally tested level. G8 is not passed. `HttpTimestampAuthority` now sends with `ResponseHeadersRead`, so `HttpClient` no longer buffers the body. It reads the body in 81,920-byte chunks and throws `PdfTimestampException` ("larger than") before the total passes 2,097,152 bytes. It refuses a declared `Content-Length` over the cap before opening the body. The body read stays under `HttpClient.Timeout`. The caller's `HttpClient` is not mutated, and there is no public API change. The fix and its tests are uncommitted on `wip`. The Technical Lead boarded the item in `agents/reports/2026-10-18-tech-lead-B-31-board.md` and set the cap at 2 MiB, under the 8 MiB appendix and above the 1 MiB embedding ceiling, with a read limit of 2,179,072 bytes. The Signing Engineer reported in `agents/reports/2026-10-18-signing-engineer-B-31.md`. The first review, `agents/reports/2026-10-18-tech-lead-B-31-review.md`, returned the change. The library's token had replaced `HttpClient`'s own timeout exception, and the body read on .NET Framework could ignore the token. The rework restores `TaskCanceledException` with a `TimeoutException` inner for both phases, and disposes the response on timeout. QA signed off in `agents/reports/2026-10-18-qa-engineer-B-31.md`: `SignatureTests` passed 37/37 on `net8.0` and on `net6.0`. A 16 MiB body is refused after at most 2,179,072 bytes. A declared oversize is refused after 0 bytes. Through `Sign`, the stream bytes stay unchanged. A body of exactly 2,097,152 bytes returns its token, and one byte more is refused. With the source at HEAD, 4 size tests failed on both frameworks, reading 16,777,216 bytes. With the pre-rework source, both timeout tests failed. The existing `http_timestamp_authority_*` and timestamp tests passed. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-31-re-review.md`. The four library targets built in Release with 0 warnings and 0 errors. `README.md` and `ACCEPTANCE.md` each gained one sentence on the cap. No text was removed, and no criterion was weakened. Limitations: the .NET Framework abort by disposing the response is implemented, not tested. Peak memory is the 2 MiB buffer plus its `ToArray` copy. A connection error after the timeout fires is reported as a timeout. The full suite was not run. This is not the fuzz run. B-18 stays open.

## B-32. Closed

Accepted as the test-harness fix, at the internally tested level. No gate is passed. `InteropTests.qpdf_checks_built_incremental_filled_and_signed_pdfs` now locates qpdf with `ExternalTool.Locate("JAYTWO_QPDF", "qpdf.exe")` and logs the path. All four `qpdf --check` calls run from that path. `TryLocateQpdf`, `ProbeQpdf`, and the local `Start` were removed. Only `InteropTests.cs` changed, and the change is uncommitted on `wip`. The Technical Lead boarded the item in `agents/reports/2026-10-18-tech-lead-B-32-board.md`. Because QA implemented the change, the Technical Lead reviewed and verified it. The QA Engineer reported in `agents/reports/2026-10-18-qa-engineer-B-32.md`. `JAYTWO_TOOLS` was unset in every run. With `JAYTWO_QPDF` only, the test failed on today's code and passes with the fix, on `net8.0` and on `net6.0`. With `PATH` only, it passes before and after. With neither, it fails, not skips, with "qpdf.exe was not found. Set JAYTWO_QPDF or put it on PATH." The test project built with 0 warnings. The Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-32-review.md`, and independently ran the `JAYTWO_QPDF`-only case on `net8.0` with qpdf 12.4.2: 1/1 passed, logging the `JAYTWO_QPDF` path. No `src/`, `README.md`, or `ACCEPTANCE.md` change. Limitations: the `PATH`-only and neither cases, and every `net6.0` run, rest on the implementer's runs. `Locate` also checks `JAYTWO_TOOLS`. The full suite was not run. The `JAYTWO_QPDF` note in `agents/state/status.md`, Repository section, is now stale. That file's owner removes it.
