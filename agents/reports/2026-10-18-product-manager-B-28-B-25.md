# B-28 acceptance and B-25 PIN decision

## 1. Task and owner

Product Manager. Accept or return B-28. Decide the B-25 PIN criterion. Record the predictor `/Columns` allocation from the B-28 re-review. G8 and G9 are not passed. B-18 stays open.

## 2. Verified facts

- **B-28 evidence chain.** Rework: `2026-10-18-core-engineer-B-28-rework.md`. QA sign-off: `2026-10-18-qa-engineer-B-28.md`. `OpenTests` passed 47/47 on `net8.0` and on `net6.0`. Each of the seven paths and the 16 rework cases throws `PdfParseException` and leaves the bytes unchanged. Each fails or hangs without the fix. Technical Lead approval: `2026-10-18-tech-lead-B-28-re-review.md`. All four targets built with 0 warnings and 0 errors. No documented behavior moved, so `README.md` and `ACCEPTANCE.md` need no change. The reports record the limitations. Against `team.md` section 8, items 1 to 7 are met at the internally tested level the task asked for.
- **B-25 PIN.** `PdfDocument.Open(Stream, string?, bool)` takes no PIN. `IPdfExternalSigner.SignHash(ReadOnlySpan<byte>)` receives only the hash. No library API accepts a PIN.
- **No accepted prior PIN evidence.** `external_signer_diagnostic_is_sanitized_and_does_not_extend_the_file` (`SignatureTests.cs`) and the replacement in `CmsDetachedSignatureProvider.cs` ("The external signer failed.") appear in no QA report, no acceptance, and no `ACCEPTANCE.md` row. The only report that mentions them is the B-25 review. B-19 accepted two different tests.

## 6. Changes made

`agents/state/backlog.md`:
- Header line updated.
- B-28 closed in the B-26 and B-27 style.
- B-25 scope and acceptance re-scoped for the PIN. It is marked as awaiting QA concurrence and Technical Lead re-review. The password and key-material criteria are unchanged. B-19 is not reopened.
- B-30 added for the predictor `/Columns` allocation in `UnpredictPng` and `EnsureXrefColumns`. It is a defect for the Core Engineer, not boarded and not started. It is a separate item rather than a note under B-18, because it is a confirmed defect with a bounded fix, like B-28 and B-29.

## 8. Decisions needed

- **B-25 decision: re-scope, not cite.** The PIN clause cannot apply to library input, so B-25 does not assert it. This corrects a clause that cannot be tested. It does not weaken the criterion. G9's PIN line in `QUALIFICATION.md` section 11 stays open for the G9 assessment. That includes a host signer exception that carries its own PIN. The unaccepted sanitization test is candidate evidence for a later item, not for B-25.
- No owner decision is needed.

## 9. Handoff

- QA Engineer: concur or object to the B-25 re-scope.
- Technical Lead: re-review B-25, then update the board. B-28 is closed and releases its claim. B-30 stays off the board.
