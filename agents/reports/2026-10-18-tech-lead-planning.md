# Planning report

## 1. Task and owner

Technical Lead. Reconcile the three B-05 audits, the accepted B-02 run, the kickoff prep, and the Product Manager kickoff. No product code. `agents/state/status.md` was not edited. `agents/state/backlog.md` and `agents/state/decisions.md` were not edited.

## 2. Verified facts

B-02 is accepted. `%LOCALAPPDATA%\Temp\jaytwo-b02\b02-net8.trx` and `b02-net6.trx` each record 145 executed, 145 passed, and 0 failed. See `agents/reports/2026-10-18-qa-engineer-B-02.md` and `agents/reports/2026-10-18-tech-lead-B-02-verify.md`.

`agents/state/status.md`, Technical baseline, still says: "Nobody has run the suite in this review round." Replacement, not applied:

> B-02 is accepted. On 2026-10-18 the suite ran on `net8.0` and on `net6.0`: 145 executed, 145 passed, and 0 failed on each. TRX: `%LOCALAPPDATA%\Temp\jaytwo-b02\b02-net8.trx` and `b02-net6.trx`.

`HEAD` is `11da851` (`genesis`) on `wip`. `git status --porcelain=v1` shows `agents/state/backlog.md`, `agents/state/board.md`, and `agents/state/status.md` modified, plus untracked files under `agents/reports/`, including `2026-10-18-qa-engineer-B-03.md`. The repository section's "clean except new files under `agents/reports/`" sentence is stale. It was not edited, because the QA Engineer is writing the Evidence inventory section.

The code map and the declaration inventory (107 `[Fact]`, 38 `[InlineData]`, `PdfEngine.cs` at 3,650 lines) still match the kickoff prep. `Internal/IsExternalInit.cs` remains an unlisted polyfill.

The three audits are inventories. They did not run the suite. Where they say a row matches, that means the code and a declared assertion agree. B-02 is the pass record.

**B-04.** The signing audit confirms the gap. `Rfc3161TimeStampRequest.Create(ReadOnlySpan<byte>)` writes version 1 and a SHA-256 imprint, no nonce. `HttpTimestampAuthority` accepts PKI status 0 or 1 and does not read the request. `CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider` do not compare imprint, digest algorithm, or nonce. The 56-byte SHA-256("abc") vector is locked by `SignatureTests.rfc3161_request_matches_the_sha256_abc_vector` and by the sealing row in `ACCEPTANCE.md`. `PdfEngine.Sign` writes the destination only after `CreateContents` returns. B-01 is closed on `wip`.

**Charter conflict.** `AGENTS.md` and the README appearances tenet say comb, rich text, non-WinAnsi, password, and signature values set `/NeedAppearances`. `ACCEPTANCE.md` says password and signature fields throw `PdfFieldValueException`. The AcroForm audit: `SetText` throws for the password flag and for `/Sig` before a write, and `simple_text_generates_helvetica_and_other_values_request_appearances` asserts both throws. Comb, rich text, and non-WinAnsi do set the flag. `team.md` section 1 forbids choosing between those documents.

**Choice field.** `ACCEPTANCE.md` says a value that is not listed throws `PdfFieldValueException` unless the Edit flag is set. The AcroForm audit: `RequireOption` returns when `Options` is empty and Edit is clear. `simple_text_generates_helvetica_and_other_values_request_appearances` fills choice `Choice`, which has no `/Opt`, with `Yes`, and expects `Generated`.

**Core clauses with no assertion.** The Core audit found no code that contradicts them.

- Other info edits refresh `ModDate` in UTC. `TouchModDate` writes `DateTimeOffset.UtcNow`. No test edits a field without setting `ModificationDate` and then reads `ModDate`.
- The metadata appendix of the 3 GiB save is under 8 KiB. `three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle` does not measure that appendix. The writer has no 8 KiB check.
- Revision 4 strings and streams can use different ciphers. `CreateRevision4` reads `StrF` and `StmF` separately. `revision4_can_mix_aes_strings_with_rc4_streams` decrypts the title and the file has no stream.

**Left as observations.** PKI status 1 is accepted; the README says 0 or 1, and the sealing row names status 0 and status 2. `DocTimeStampSignatureProvider` does not reject an empty token; the CMS provider does. `FieldMDP` `Include` and `Exclude`, and DocMDP `P` 3, are charter behavior in `AGENTS.md`. The signing audit found no acceptance row and no test that names `Include`, `Exclude`, or `P` 3.

The Product Manager kickoff found no backlog item that adds an excluded capability, reopens a closed decision, or weakens `ACCEPTANCE.md`. Its statement that B-01 blocks parallel editing is superseded: backlog B-01 is closed. D-O4 and the D-15 rewordings stay as already recorded. They are not reopened. This report did not read `agents/reports/2026-10-18-qa-engineer-B-03.md`.

## 3. Assumptions

- "Not listed" for a choice value includes a field with no `/Opt`. That is the reading that makes the `Choice` fill a candidate defect. The AcroForm audit also recorded the other reading.
- An untracked B-03 report means the evidence inventory is in progress, not that B-03 is accepted.

## 4. Defects confirmed

B-04, as already written on the backlog. The signing audit confirms it. It is not a miss against a current `ACCEPTANCE.md` row.

The choice-field behavior is a candidate defect under the reading in section 3. It is not boarded.

The password and signature disagreement is a charter conflict, not a defect assigned to the AcroForm Engineer.

## 5. Evidence missing

The three Core clauses in section 2. Missing assertions, not confirmed code defects.

B-03 and B-06 are still open. Nothing is production-qualified. The Product Manager kickoff's gate table stands until B-06.

## 6. Changes made or proposed

`agents/state/board.md` now has B-03, B-04, and B-06. B-02 and the B-05 rows are removed.

**B-04, decided.** Non-breaking public API: add a nonce-bearing overload. Do not change the bytes of `Rfc3161TimeStampRequest.Create(ReadOnlySpan<byte>)`. Changing that method is an owner decision. This report does not make it. `certReq` stays omitted. The check runs in `CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider`. `ITimestampAuthority` does not change. No engine file.

Implementer: Signing Engineer. Branch: cut from `11da851`. Verifier: QA Engineer. Consulted: none. QA reviews `test/jaytwo.Ergonomics.Pdf.Tests/Fixtures/tsa_server.py` if, and only if, the local TSA does not echo the nonce and that file has to change.

May change:

- `src/jaytwo.Ergonomics.Pdf/Signatures/Rfc3161TimeStampRequest.cs`
- `src/jaytwo.Ergonomics.Pdf/Signatures/CmsDetachedSignatureProvider.cs`
- `src/jaytwo.Ergonomics.Pdf/Signatures/DocTimeStampSignatureProvider.cs`
- `src/jaytwo.Ergonomics.Pdf/Signatures/DerCodec.cs`
- at most one new internal type under `src/jaytwo.Ergonomics.Pdf/Signatures/`
- `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs`
- `README.md`
- `ACCEPTANCE.md`
- `tsa_server.py` only in the case above

**Not boarded.**

- Password and signature fills. The AcroForm Engineer does not start a fix.
- The choice-field candidate. The Product Manager writes the backlog item first.
- The three Core evidence gaps. The Product Manager writes those evidence tasks first.
- PKI status 1, the empty document-timestamp token, FieldMDP `Include`/`Exclude`, and DocMDP `P` 3. No tasks.

**Recommendations for the Product Manager.**

- Raise the password and signature conflict with the owner. `AGENTS.md` and `README.md` say `/NeedAppearances`. `ACCEPTANCE.md` and the tests say `PdfFieldValueException`. The code throws.
- Write a backlog item for a choice field with no `/Opt`, Edit clear, and a value accepted.
- Write evidence tasks for the `ModDate` UTC refresh, the 3 GiB metadata appendix under 8 KiB, and a revision 4 file whose stream cipher differs from its string cipher.
- Confirm or reorder the backlog from this report. G6 is not in B-06's starting points.

The baseline replacement in section 2 is applied when the Evidence inventory edit is no longer in progress.

## 7. Verification run

Read the seven reports named in the task, `AGENTS.md`, `ACCEPTANCE.md`, `agents/team.md`, `agents/state/backlog.md`, and `agents/state/board.md`. `git status --porcelain=v1` and `git rev-parse --short=7 HEAD`. No `dotnet test`. No product edits.

## 8. Decisions needed

The owner, through the Product Manager: which document wins for password and signature fills. Not chosen here.

The owner only if a later plan changes the bytes of `Rfc3161TimeStampRequest.Create(ReadOnlySpan<byte>)`. The overload does not need that decision.

The Product Manager: the choice-field item and the three evidence tasks, before either is boarded.

## 9. Handoff

Signing Engineer starts B-04 on a branch from `11da851`, inside the file list above. QA Engineer finishes B-03, then verifies B-04. Product Manager raises the charter conflict, writes the unboarded items, and confirms the backlog. Technical Lead applies the baseline sentence after the Evidence inventory edit is finished. B-06 stays waiting on B-03.
