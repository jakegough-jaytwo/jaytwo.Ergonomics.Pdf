# Product Manager kickoff charter check

## Task and owner

Read-only charter check of `agents/state/backlog.md`, 2026-10-18. Owner: Product Manager.

No task is accepted. `backlog.md`, `decisions.md`, and `status.md` were not edited. Under D-11 the backlog is confirmed only after the Technical Lead's planning report.

## Verified facts

Compared B-01 through B-07 with `AGENTS.md`, `ACCEPTANCE.md`, `QUALIFICATION.md`, and `decisions.md`.

- No backlog item adds a capability `AGENTS.md` excludes. B-04 is an input check on timestamp creation. D-10 allows that. D-14 requires the nonce. TSA trust and signature validation stay out of the item.
- No item reopens a closed decision. B-07 stays closed under D-13. D-16 is not raised. B-01 matches D-12. B-02 runs the test project frameworks only (`net8.0` and `net6.0` in `test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj`). It does not drop a library target.
- No item weakens an `ACCEPTANCE.md` criterion. Nothing in `ACCEPTANCE.md` was edited.
- `Rfc3161TimeStampRequest.Create` is documented as a version-1 request with no nonce and `certReq` omitted (`src/jaytwo.Ergonomics.Pdf/Signatures/Rfc3161TimeStampRequest.cs`). The sealing criterion that `Create` of SHA-256("abc") is the 56-byte vector still stands.
- The only open decision in `decisions.md` is D-O4. The five D-15 rewordings are waiting on owner confirmation inside that decided item.
- `agents/reports/` had no seat reports. No test-result files are in the tree. The committed foreign corpus is seven PDFs: `pypdf-base.pdf`, `pypdf-acroform.pdf`, `qpdf-object-streams.pdf`, `qpdf-encrypted.pdf`, `qpdf-attachment.pdf`, `pyhanko-signed.pdf`, `pyhanko-sigfield.pdf`.
- `status.md` already says release readiness is not assessed and that no capability is production-qualified. This check did not run the suite.

Gates and section 14 capabilities the current backlog does not cover. B-06 is the later gap analysis. It waits on B-02 and B-03. These are not new backlog items.

| Gate or capability | What the backlog actually does |
|---|---|
| G1 support matrix | Named inside B-06. No item writes the matrix. |
| G2 structural interoperability | Named inside B-06. No corpus or recorded `qpdf` pass. |
| G3 functional correctness | No qualification item. B-03 and B-05 map or audit `ACCEPTANCE.md` only. |
| G4 signature verification | Named inside B-06 as a tool question. No scenario set. |
| G5 timestamp verification | B-04 is only the library-side imprint, digest algorithm, and nonce rejection. Independent token verification, verifier trust, and no-false-success are not tasked. |
| G6 appearance rendering | Not named in B-06's starting points. No item covers the G6 scenario list. |
| G7 large-file qualification | Named inside B-06. No benchmark item. No fixture-specific budget is approved. |
| G8 adversarial testing | Named inside B-06. No fuzzing or independent review item. |
| G9 failure and output integrity | No item. |
| G10 API, build, package | The `netstandard2.0` test gap is named inside B-06. No recorded build, package, or `netstandard2.0` run. |
| All eight section 14 capabilities | No qualification item. Timestamping is the only one with a related defect, B-04. |

B-02, B-03, and B-05 produce inventory and audit reports. They do not close a gate.

## Assumptions

- The imprint-check half of B-04's problem statement is taken from the technical baseline in `status.md`. This turn confirmed the public request has no nonce. It did not re-read the token parser.
- "Not assessed" means no recorded qualification evidence was found. It does not mean the code is absent.

## Evidence missing

Every mandatory gate, and every section 14 capability. Test source and `ACCEPTANCE.md` rows are not results. Nothing is production-qualified.

## Changes made or proposed

Wrote this report only.

Draft for the release-readiness heading in `status.md`. Not applied. A release was not proposed, so this is not the section 15 release package. If one were asked today, the recommendation would be **Not qualified**.

| Gate | Status |
|---|---|
| G1 Documented boundaries | not assessed |
| G2 Independent structural interoperability | not assessed |
| G3 Correctness of supported operations | not assessed |
| G4 Independent signature verification | not assessed |
| G5 Independent RFC 3161 verification | not assessed |
| G6 Independent form-appearance rendering | not assessed |
| G7 Large-file resource qualification | not assessed |
| G8 Security and adversarial-input testing | not assessed |
| G9 Failure handling and output integrity | not assessed |
| G10 Stable API, build, and documentation | not assessed |

| Capability | Status |
|---|---|
| Field extraction | not assessed |
| Field population | not assessed |
| Metadata | not assessed |
| Attachments | not assessed |
| Incremental writing | not assessed |
| PDF signing | not assessed |
| Timestamping | not assessed |
| Large-file support | not assessed |

No capability is production-qualified.

## Decisions needed

From the owner:

- **D-O4.** Confirm caller-facing coordinates. Recommendation: keep raw user space, page boxes, and rotation.
- **D-15.** Confirm the five rewordings already listed there. They are not new decisions. Recommendation: confirm them.
- **B-01.** Commit the working tree. This is the owner's, and it blocks parallel editing.

Not an owner decision unless the Technical Lead's plan changes the bytes of the existing `Rfc3161TimeStampRequest.Create`. That would be a breaking public API change. An added overload is not. The 56-byte vector stays until the owner accepts a breaking change.

A mandatory gate is not waived by recording it "out of scope for 1.0" in B-06. Dropping a blocking gate is an owner decision. Proposed numeric thresholds stay with the Technical Lead and QA under D-15.

## Handoff

Technical Lead writes the planning report and reconciles the audits. The Product Manager then confirms or reorders the backlog. QA owns the evidence for B-02, B-03, and B-06. No acceptance this turn.
