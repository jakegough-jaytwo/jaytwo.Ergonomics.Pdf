# B-04 review

## 1. Task and owner

Technical Lead. Review of the Signing Engineer's B-04 implementation. No product code and no `README.md` edit. The task is not accepted. The Product Manager accepts it after QA sign-off.

## 2. Verified facts

Approved. No defects.

The staged B-04 diff is the signature files, `SignatureTests.cs`, `README.md`, and `ACCEPTANCE.md`. `ITimestampAuthority.cs` and `Fixtures/tsa_server.py` are not in it. `PdfEngine.Sign` is not in it. An unstaged one-line change in `RequireOption` is B-08, not this review.

`Rfc3161TimeStampRequest.Create(ReadOnlySpan<byte>)` still writes version 1 and a SHA-256 imprint only. `certReq` is omitted. `SignatureTests.rfc3161_request_matches_the_sha256_abc_vector` still locks the 56-byte vector `30360201013031300d060960864801650304020105000420ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad`. The new overload appends a nonce INTEGER. A high bit is padded with `0x00` in `DerCodec.Integer(ReadOnlySpan<byte>)`.

`CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider` each call `NewNonce`, the overload, and `Rfc3161TimeStampToken.RequireMatch`. A bad imprint, a digest algorithm other than SHA-256, a missing nonce, a mismatched nonce, or an empty token throws `PdfTimestampException` before `CreateContents` returns. `Sign` still writes the destination after that return, so the throw does not extend the file. `AssertTimestampRejected` covers both providers and asserts the original length for all four faults.

`ACCEPTANCE.md` keeps the 56-byte vector and adds the nonce sentence plus the three throw rows. No existing row was removed or loosened. The README timestamp paragraph matches that behavior. The appearances bullet, password and signature fields throw `PdfFieldValueException`, is the closed D-17 wording. It was left as written.

Checklist: the overload is additive and stays in `jaytwo.Ergonomics.Pdf.Signatures`. No runtime package. TSA trust is not added. The nonce comes from `RandomNumberGenerator`, which is disposed. The token parse walks the returned bytes. One new internal type holds the check. The public overload has XML docs. The reported build was clean on all four target frameworks.

## 5. Evidence missing

`SignatureTests` was not run on `net6.0`. QA sign-off still has to cover that. This review did not rerun the suite. The Signing Engineer's `net8.0` results stand: `SignatureTests` 24 passed, and `pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature` passed.

## 6. Changes made or proposed

This report. `agents/state/board.md` marks B-04 in review. It still claims `ACCEPTANCE.md`.

## 8. Decisions needed

None. The existing `Create(ReadOnlySpan<byte>)` bytes were not changed.

## 9. Handoff

QA Engineer signs off, including `SignatureTests` on `net6.0`. The Product Manager accepts the task after that. This review does not accept it.
