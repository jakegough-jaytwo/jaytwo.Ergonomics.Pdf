# Technical Lead review: B-15

## Task and owner

B-15, timestamp authority outside pyHanko. Implemented by the Signing Engineer (`2026-10-18-signing-engineer-B-15.md`), verified by QA (`2026-10-18-qa-engineer-B-15.md`). Reviewer: Technical Lead.

## Verified facts

- The change is test-only: new `OpenSslTimestampTests.cs`, new `Fixtures/validate_doc_timestamp.py`, and a harness fix in `ExternalTool.cs`. `git status` shows no `src/` change. No runtime package. No public API change. The change stays inside the board row.
- `OpenSslTimestampAuthority` is a test `ITimestampAuthority` that runs `openssl ts -reply -token_out`. The document timestamp goes through the public `PdfDocument.Sign` with `PdfSignatureKind.DocTimeStamp` and `DocTimeStampSignatureProvider`. The test asserts that the embedded token equals the token OpenSSL issued, so the verifiers check the authority's bytes.
- The positive test asserts pyHanko `intact`, `valid`, `trusted`, and `ENTIRE_FILE`, plus `openssl ts -verify` `Verification: OK` over the `/ByteRange` bytes. The negative test asserts `trusted=false` under a different root, `intact=false` with the header version byte flipped, and the matching OpenSSL failures. QA mutation M1 shows the ESS signing-certificate check is live.
- `validate_doc_timestamp.py` passes `trust_roots=[root]`, which replaces the system store. The certificate it adds sits in the unsigned `certificates` field of the parsed token, in memory only. I accept it as an explicit test trust configuration for the reasons QA records.
- `ExternalTool.Run` drains stdout and stderr concurrently and kills the process tree after 600 s. This fixes a real pipe deadlock. `Output` keeps its exit-code assert, and `Locate` keeps its message. The existing `Output` callers in `DenseMemoryTests`, `AppearanceRenderTests`, and `InteropTests` run in the final full suite.

## Defects confirmed

None in the change. The harness deadlock that QA fixed predated B-15.

## Evidence missing

- pyHanko cannot validate a compliant TSA's token for a library request unless the TSA certificate is supplied separately, because the request omits certReq (D-14). This is an owner question. It is not a B-15 defect.
- G5 still has no CMS signature timestamp from an authority other than pyHanko.

## Changes made or proposed

None by the reviewer. B-33 note: `OpenSslTimestampTests` and `ExternalTool` use `Convert.FromHexString`, `Encoding.Latin1`, `ProcessStartInfo.ArgumentList`, and `Process.Kill(bool)`, which `net48` lacks. The `net48` target has to handle these.

## Verification run

I relied on the QA and implementer runs: `OpenSslTimestampTests` 2/2 and `SigningInteropTests` 4/4 on `net8.0` and `net6.0`. The full suite runs once, at the checkpoint.

## Handoff

Approved. Product Manager to accept.
