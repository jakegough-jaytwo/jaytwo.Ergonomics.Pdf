# B-06. Gap analysis against QUALIFICATION.md

## Task and owner

QA Engineer, evidence only. Compared gates G1–G10 and the section 14 capabilities with the B-02 run and the B-03 inventory. No gate is passed. No capability is Production-qualified. `agents/state/backlog.md` was not edited. Release readiness was not signed off.

## Verified facts

- The gate and capability rows are in the Evidence inventory section of `agents/state/status.md`, after the acceptance table.
- B-03 clause gaps 11 and 12 are in `agents/reports/2026-10-18-qa-engineer-B-03.md`. The inventory points at them.
- Starting points from backlog item B-06, checked against the repository:
  - G1. Confirmed: there is no support matrix. `README.md`, `ACCEPTANCE.md`, and `AGENTS.md` do describe the advertised behavior.
  - G2. Confirmed and counted: six committed foreign PDFs, three producers (pypdf, qpdf, pyHanko). Not 50, and not four.
  - G4. Corrected: pyHanko 0.38.0 is a PDF-aware verifier. `SignedCms.CheckSignature` ran on `net8.0` only and on CMS bytes the test assembled. That pair is not two PDF verifiers. pdfsig was not run and is still required for the second PDF-aware check.
  - G7. Corrected: the dense test is 8 MiB and 32 MiB, not 1 GiB. The attachment test copies 200 KiB, not 250 MiB. A 1,000-page sparse read exists (`thousand_page_form_does_not_read_content_holes`) and does not extract, edit metadata, and sign. A sparse 3 GiB open, save, and sign exists (`three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle`) and is not a real file.
  - G8. Confirmed: no fuzz harness and no independent security review. Corrected: some malformed-input tests exist, so the gate is not empty.
  - G10. Confirmed: no test runs the `netstandard2.0` build. `netstandard2.1` is also untested. B-02 passed `net8.0` and `net6.0` only.

## Assumptions

- `Fixtures/tsa_server.py` uses pyHanko `DummyTimeStamper`. The token and the pyHanko 0.38.0 check are one toolchain.
- The six names under `Fixtures/*.pdf` are the committed foreign corpus. Generator scripts are not extra fixtures.

## Evidence missing

Candidate backlog items. Not filed.

1. Write the G1 support matrix, with read-only compatibility separate from modification.
2. Grow the foreign corpus to at least 50 fixtures from four producers, including a real-world file not built for this library.
3. Run a second PDF-aware signature verifier (pdfsig or equivalent). Do not count `SignedCms` as that verifier.
4. Check timestamp imprint and nonce (already B-04). Use a TSA that is not pyHanko, and have an independent verifier check a document timestamp.
5. Render the rest of the G6 set, and add a second renderer for representative cases.
6. Add the G7 workloads: file-backed 1 GiB dense, 250 MiB attachment, and a 500-page extract, metadata edit, and sign. Record timing. Approve fixture budgets before calling G7 met.
7. Add a fixed-seed fuzz suite toward the 24 CPU-hour target, and an independent review of the parser and the signer.
8. Test a throwing `IPdfExternalSigner` and a timestamp failure that must not be reported as a successful timestamped signature.
9. Run the `netstandard2.0` and `netstandard2.1` builds, and record a pack plus package verification.
10. Keep the B-03 clause gaps, including 11 and 12, with the acceptance rows they belong to.

## Changes made or proposed

Added the two B-03 clause gaps and the qualification tables in the Evidence inventory section of `agents/state/status.md`. No other section of that file was edited.

## Verification run

No new test run. Tool versions remain the B-02 set: qpdf 12.4.2, pyHanko 0.38.0, pypdf 6.21.1 (not invoked), Poppler 26.09.0, SoftHSM 2.5.0. pdfsig was not present in that run.

## Handoff

Product Manager turns these gaps into backlog items after the Technical Lead's planning report. QA does not sign off on release readiness.
