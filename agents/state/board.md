# Task board

Owner: Technical Lead. One row per active task. A task may change only the files listed for it. As of 2026-10-18.

## Active

| Task | Implementer | May change | Branch or worktree | Waits on | Verifier |
|---|---|---|---|---|---|

B-34 and B-35 were accepted on 2026-10-18. The Security Auditor review is complete (`agents/reports/2026-10-18-security-auditor-B-18.md`), and the Technical Lead disposition is in `agents/reports/2026-10-18-tech-lead-B-18-audit-review.md`. Queued, in order, one at a time on `wip`: B-18 fuzz harness and run (QA; new test-only fuzz project under `test/`), B-14 `pdfsig` (QA), B-13 corpus (QA). The Security Auditor runs in parallel because it is read-only. The 24 CPU-hour fuzz run (D-23) runs in the background at below-normal priority while B-14 and B-13 proceed. Defects that the fuzzing or the audit confirms go to the owning engineer if small. Otherwise they are recorded as blockers.

## Resume point

Checkpointed 2026-10-18 after the bounded resume under owner decisions D-18 (OpenSSL as a test tool), D-19 (`net48` test target), and D-20 (G7 working budget). Tasks ran one at a time on `wip`, one agent at a time. All three were accepted after QA sign-off and Technical Lead review. One was returned once, and none was returned a second time:

- B-15: an OpenSSL 3.2.4 `openssl ts` token in a library document timestamp is accepted by pyHanko 0.38.0 (intact, valid, trusted, `ENTIRE_FILE`) under the test root only, and by `openssl ts -verify`. pyHanko needs the TSA certificate supplied out of band, because the request omits certReq (D-14). That is open decision D-O6. QA fixed a stderr pipe deadlock in `ExternalTool`. `AGENTS.md` names `OpenSslTimestampTests` in the tool list.
- B-33: the test project targets `net48` and runs the `netstandard2.0` build with no exclusions. It was returned once for a missing test of the B-31 abort-by-dispose path, which the rework added. On `net48`, `stalled_timestamp_response_headers_still_time_out` fails. This is library defect B-34, recorded and left failing.
- B-17: a file-backed 1 GiB dense PDF, a 250 MiB attachment, and a 500-page form all stay within the D-20 ceilings on three frameworks, at a peak of at most 10.31 MiB. The fixture-specific budgets are approved in `state/status.md`.

At this checkpoint the full suite passed 206/206 on `net8.0` and 206/206 on `net6.0`. On `net48` it ran 205 passed and 1 failed (B-34). Tools: qpdf 12.4.2, Poppler 26.09.0, SoftHSM 2, OpenSSL 3.2.4, pyHanko 0.38.0. The library built in Release for all four targets with warnings as errors: 0 warnings, 0 errors. No qualification gate is passed.

Next actions, in order:

1. B-34 (Signing Engineer, `HttpTimestampAuthority` only) can be boarded without the owner. It is a defect inside the charter, and its regression test already fails on `net48`.
2. D-O6 (set certReq in timestamp requests) needs the owner. If approved, the Signing Engineer changes `Rfc3161TimeStampRequest`, and QA drops the B-15 out-of-band certificate workaround.
3. G5 still lacks a CMS signature timestamp from a non-pyHanko authority. OpenSSL can supply it under D-18. That is a new backlog item for the Product Manager.
4. G7 remaining criteria: repeated object-stream access, repeated use and dispose, encrypted xref-stream discovery, limits, and a timing baseline.
5. B-13 (a fourth producer), B-14 (a second PDF-aware verifier, since `pdfsig` is not installed), and B-18 (a long fuzz run) still need tools or time from the owner.

## Kickoff plan

Under decision D-11:

1. B-02 and the three B-05 audits are finished. The planning report is `agents/reports/2026-10-18-tech-lead-planning.md`.
2. The Product Manager confirms or reorders `state/backlog.md` from that report.
3. B-01 acceptance is met on `wip`. B-03, B-06, B-08 through B-12, B-16, and B-19 through B-32 are done. B-18 remains open. G8 and G9 stay unpassed. No qualification gate is passed.
4. B-13, B-14, and B-18 stay off the board. (B-15 and B-17 closed on 2026-10-18 under D-18 and D-20.) B-13 needs a fourth PDF producer. B-14 needs a second PDF-aware signature verifier. B-15 needs a timestamp authority that is not pyHanko. B-17 needs an approved memory budget. B-18 needs a long fuzz run. `pdfsig.exe` is not installed. The Poppler 26.09.0 build has `pdftotext` and `pdftoppm` and does not include `pdfsig`. Python 3.13 has pypdf and pyHanko only.

## Shared-file claims

| Claim | Task |
|---|---|
| `agents/reports/2026-10-18-security-auditor-B-18.md` | B-18 review |
| `src/jaytwo.Ergonomics.Pdf/Signatures/HttpTimestampAuthority.cs` | B-34, B-35 |
| `src/jaytwo.Ergonomics.Pdf/Signatures/Rfc3161TimeStampRequest.cs` | B-34, B-35 |
| `src/jaytwo.Ergonomics.Pdf/Signatures/Rfc3161TimeStampToken.cs` | B-34, B-35 |
| `src/jaytwo.Ergonomics.Pdf/Signatures/CmsDetachedSignatureProvider.cs` | B-34, B-35 |
| `src/jaytwo.Ergonomics.Pdf/Signatures/DocTimeStampSignatureProvider.cs` | B-34, B-35 |
| `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs` | B-34, B-35 |
| `test/jaytwo.Ergonomics.Pdf.Tests/SigningInteropTests.cs` | B-34, B-35 |
| `test/jaytwo.Ergonomics.Pdf.Tests/OpenSslTimestampTests.cs` | B-34, B-35 |
| `test/jaytwo.Ergonomics.Pdf.Tests/Fixtures/validate_doc_timestamp.py` | B-34, B-35 |
| `test/jaytwo.Ergonomics.Pdf.Tests/Fixtures/tsa_server.py` | B-34, B-35 |
| `README.md` | B-34, B-35 |
| `ACCEPTANCE.md` timestamp rows only | B-34, B-35 |
| Signing Engineer task reports for B-34 and B-35 | B-34, B-35 |

The B-15, B-33, and B-17 claims remain released. The Security Auditor has no source-file claim and reviews the committed `d4b84dd` tree, not the B-34/B-35 WIP.
