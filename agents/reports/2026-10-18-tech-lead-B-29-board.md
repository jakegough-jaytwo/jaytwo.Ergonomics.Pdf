# B-29 boarding

## 1. Task and owner

Technical Lead. Board B-29 and release the B-25 and B-28 rows and claims, now that the Product Manager has closed both items (`2026-10-18-product-manager-B-25-accept.md` and `2026-10-18-product-manager-B-28-B-25.md`). No code or test edits. No test runs. No commit. G8 is not passed.

## 2. Verified facts

B-29 is in the charter. It is a bounded defect fix in the signer, with no new capability and no new package. The proposed code and tests are already committed at HEAD (`ee10c84`). I confirmed from the code that they touch only these three files:

- `src/jaytwo.Ergonomics.Pdf/Signatures/DerCodec.cs`: `ReadTlv(ReadOnlySpan<byte>, int)` checks `length < 0 || length > data.Length - index`. Because it subtracts rather than adds, the bounds check cannot wrap when the long-form length is `0x7FFFFFFF`. `ReadInteger` is unchanged and still folds 4 bytes into a signed `int`.
- `src/jaytwo.Ergonomics.Pdf/Signatures/HttpTimestampAuthority.cs`: `ExtractToken` rejects `code < 0 || code > 1` with `PdfTimestampException`.
- `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs`: `negative_timestamp_status_does_not_extend_the_file` (status `02 04 80 00 00 00`), `overflowing_der_length_does_not_extend_the_file` (body `30 84 7F FF FF FF`), the private helper `AssertTimestampResponseDoesNotExtend`, and the existing `RecordingHandler`.

None of these has been executed or reviewed under B-29. They match the shape the B-27 review described.

## 6. Changes made

`agents/state/board.md`, in the "Active" table and the "Shared-file claims" section only:
- I removed the B-25 and B-28 rows and released their claims.
- I added a B-29 row. Implementer: Signing Engineer. May change: the three files above. Branch: `wip`. Waits on: none. Verifier: QA Engineer. Brief: verify the committed code and tests against the B-29 acceptance criteria, do not rewrite them, make a minimal fix only if verification finds a defect, and do not pass G8.
- I recorded the B-29 claim on those three files. No claims overlap.

I did not change "Resume point" or "Kickoff plan".

## 9. Handoff

- **Signing Engineer:** verify B-29 on `net8.0` and `net6.0`. Show that each test fails without its fix and passes with it. Each case must throw `PdfTimestampException`, not `ArgumentOutOfRangeException`, and must leave the file unextended.
- **QA Engineer:** verify.
- **Technical Lead:** review.

B-30 stays off the board. G8 is not passed.
