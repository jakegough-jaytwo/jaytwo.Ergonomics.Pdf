# B-19 review

## 1. Task and owner

Technical Lead. Review of the throwing-signer and throwing-timestamp tests. No edit to `SignatureTests.cs`, `README.md`, or `backlog.md`. The task is not accepted. QA still signs off.

## 2. Verified facts

Approved. No defects.

`external_signer_failure_does_not_extend_the_file` uses `ThrowingSigner.SignHash`, which throws `CryptographicException`. `CmsDetachedSignatureProvider.CreateContents` calls `SignHash` before it returns. `PdfEngine.Sign` calls `CreateContents` and writes the destination only after that call returns. `Assert.Throws` means `Sign` does not return. The stream length and bytes stay equal to the input.

`timestamp_authority_failure_does_not_leave_a_timestamped_signature` uses a real RSA `SignHash`, then `ThrowingAuthority.GetTimeStampToken`, which throws `PdfTimestampException` inside the same `CreateContents` call. `Sign` throws. The stream bytes stay equal to the input, and a later open has no signature field. This is the authority throwing. It is not an imprint, digest-algorithm, or nonce rejection.

The Signing Engineer recorded 2 passed and 0 failed on `net8.0`. This review did not rerun them. `net6.0` was not run.

## 6. Changes made or proposed

This report. `agents/state/board.md` marks B-19 in review and B-20 approved, awaiting acceptance. Neither row is removed.

## 9. Handoff

QA Engineer signs off. The Product Manager accepts after that. This review does not accept the task.
