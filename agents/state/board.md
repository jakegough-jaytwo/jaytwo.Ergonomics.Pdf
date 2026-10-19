# Task board

Owner: Technical Lead. One row per active task. A task may change only the files listed for it. As of 2026-10-18.

## Active

| Task | Implementer | May change | Branch or worktree | Waits on | Verifier |
|---|---|---|---|---|---|

No task is active. B-36 was accepted on 2026-10-19 (returned once, for QA-B36-01).

## Resume point

Checkpointed 2026-10-19. Since `d4b84dd`: the owner decided D-21 (certReq), D-22 (test infrastructure and small fixes need no owner), and D-23 (24 CPU-hours of fuzzing and a Security Auditor seat). Accepted: B-34 (consistent timestamp timeout on every framework), B-35 (certReq true and strict token parsing; pyHanko validates the OpenSSL token with only the root), and B-36 (parser hardening, which closes SA-01 to SA-04, SA-09, and SA-11). Jenkins now runs the Linux Docker image (`da08d21`). `make docker-builder docker-test` must pass before every checkpoint (`AGENTS.md`, CI).

At this checkpoint the full suite passed 269/269 on Windows `net8.0`, `net6.0`, and `net48`, and on Linux `net8.0` and `net6.0` (Docker). The Release library builds for all four targets with 0 warnings. No qualification gate is passed.

Next actions, in order, one at a time on `wip`. None needs the owner except item 4:

1. B-38 (Signing Engineer): SA-05 and SA-06, both High.
2. B-37 (Core Engineer, Signing Engineer consulted): SA-07, High, an exact appendix-size guard.
3. B-18 fuzz harness and campaign (QA): seed the eleven audit triggers. Run up to 24 aggregate CPU-hours (D-23) at below-normal priority only after B-37 and B-38, so the campaign counts as evidence.
4. B-39, the FieldMDP `Include`/`Exclude` reversal (SA-08), needs owner confirmation. It changes a documented certification guarantee (`team.md` section 9).
5. B-14 (QA): `pdfsig`, which WSL Ubuntu 24.04 or Docker can provide. B-13 (QA): LibreOffice and/or PDFBox producers; Java is not installed yet.
6. Backlog, not yet boarded: B-40 (indirect `/Filter`), B-41 (classic xref bounds plus QA-B36-02), B-42 (object-stream allocation churn).

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
