# Backlog

Owner: Product Manager. Ordered by priority. Each item follows the template in `roles/product-manager.md`. Seeded on 2026-10-18 from the review round in `agents/reviews/`. The Product Manager should confirm or reorder it in the first planning report.

## B-01. Commit the working tree

- **Kind:** prerequisite. **Owner:** human owner.
- **Problem:** `HEAD` contains only `LICENSE`. Branches and worktrees cannot isolate concurrent tasks.
- **Scope:** commit the current tree on `wip`, or a branch the owner chooses. Do not run `git init`.
- **Acceptance:** `git ls-tree HEAD` lists `src/`, `test/`, and `agents/`.
- **Blocks:** parallel editing tasks.

## B-02. Run the full suite with the required tools

- **Kind:** missing evidence. **Implementer:** QA Engineer.
- **Problem:** the current test count is an inventory of declarations. No one has recorded a run with qpdf, Python with pyHanko and pypdf, Poppler, and SoftHSM present.
- **Scope:** run every test on `net8.0` and `net6.0`. Record tool versions, pass, fail, and not-run counts. No code changes.
- **Acceptance:** a report with the commands and the result per test class. Any failure becomes a defect report.

## B-03. Evidence inventory

- **Kind:** missing evidence. **Implementer:** QA Engineer.
- **Problem:** no map exists from each `ACCEPTANCE.md` row to its tests and evidence level.
- **Scope:** map each row to the tests that cover it and the evidence level reached. List rows with no test. Read-only.
- **Acceptance:** the evidence inventory section of `state/status.md` is filled in. Gaps are listed as candidate backlog items.
- **Depends on:** B-02, for the results column.

## B-04. Send a nonce, and check that a timestamp token matches its request

- **Kind:** defect. **Implementer:** Signing Engineer. **Gate:** G5.
- **Problem:** the library accepts any token from a status-0 or status-1 response. It does not compare the token's message imprint or digest algorithm with the request. The request carries no nonce, so a replayed token cannot be detected.
- **Scope:**
  - Add a random nonce to each request, under D-14. Keep `certReq` omitted.
  - Reject a token whose imprint, digest algorithm, or nonce does not match the request. Throw `PdfTimestampException`.
  - Run the check where the request is built, in `CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider`, not only in `HttpTimestampAuthority`. Any `ITimestampAuthority` implementation is then covered.
  - TSA trust stays out of scope under D-10.
- **Public API:** `Rfc3161TimeStampRequest.Create` is public and documented as "no nonce." Adding a nonce-bearing overload is additive. Changing the existing method's output is a breaking change and goes to the owner. The Technical Lead chooses before work starts.
- **Acceptance:**
  - New `ACCEPTANCE.md` rows: a mismatched imprint, a mismatched digest algorithm, and a missing or mismatched nonce each throw and do not extend the file.
  - The request-encoding row and the `README.md` timestamp paragraph describe the nonce.
  - The existing local-TSA test still passes, with pyHanko reporting the token intact, valid, and trusted.
- **Evidence:** internally tested, plus the existing independent TSA round trip.

## B-05. Subsystem audits against `ACCEPTANCE.md`

- **Kind:** missing evidence. **Implementers:** Core Engineer, AcroForm Engineer, Signing Engineer, one report each.
- **Problem:** no seat has checked its own area against the criteria under the new ownership.
- **Scope:** read-only. For each row in your area, note whether the code and tests match it. Note behavior in your area that no criterion describes. Do not propose product features.
- **Acceptance:** one report per seat in the `team.md` format.

## B-06. Gap analysis against `QUALIFICATION.md`

- **Kind:** missing evidence. **Implementers:** QA Engineer for the evidence, Product Manager for the backlog it produces.
- **Problem:** the release bar was adopted under D-15. No one has measured the project against it.
- **Scope:** read-only. For each gate G1 through G10, and each capability in section 14, record what evidence exists and what is missing. Known starting points:
  - **G2.** The committed foreign corpus is far smaller than 50 fixtures from four producers.
  - **G4.** Confirm whether pyHanko plus `SignedCms` meets "two independent implementations, one PDF-aware," or whether pdfsig is needed.
  - **G7.** The dense-memory test is tens of MiB, not 1 GiB. No 250 MiB attachment or 500-page benchmark exists.
  - **G8.** No fuzzing or independent security review exists.
  - **G1.** No support matrix exists.
  - **G10.** No test runs the `netstandard2.0` build.
- **Acceptance:** the evidence inventory in `state/status.md` has a row per gate. The Product Manager turns each gap into a backlog item, or records why it is out of scope for 1.0.
- **Depends on:** B-02 and B-03.

## B-07. Closed

Framing the target-framework decision is no longer needed. The owner decided it under D-13.
