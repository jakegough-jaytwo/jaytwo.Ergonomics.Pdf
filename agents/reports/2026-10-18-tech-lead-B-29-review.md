# B-29 review

## 1. Task and owner

Technical Lead. This reviews B-29 against the acceptance in `agents/state/backlog.md` and my checklist. B-29 has two parts. The `src` fixes were committed at `ee10c84` and had not been reviewed. The test tightening in `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs` is uncommitted. The inputs are `2026-10-18-signing-engineer-B-29.md` and `2026-10-18-qa-engineer-B-29.md`, where QA signed off. I made no code, test, or state edit. G8 is not passed.

## 2. Verified facts

- **The `src` change is two lines.** `git diff 11da851 ee10c84` on the two files shows exactly these:
  - `DerCodec.ReadTlv`: `index + length > data.Length` became `length > data.Length - index`. `index` is always `<= data.Length` at that point, so the subtraction cannot wrap. `length < 0` already catches a 4-byte length with the top bit set. With `30 84 7F FF FF FF`, `index` is 6 and the check now throws `PdfTimestampException` ("DER value exceeds its buffer.") before `Slice` runs.
  - `HttpTimestampAuthority.ExtractToken`: `code > 1` became `code < 0 || code > 1`. `ReadInteger` still folds `02 04 80 00 00 00` to `int.MinValue`, and that value is now rejected with `PdfTimestampException` before any token is read.
  - The other addition in `DerCodec.cs`, `Integer(ReadOnlySpan<byte>)`, is nonce work from B-04 and not part of B-29, as the Signing Engineer states.
- **The remaining arithmetic in `ReadTlv` is safe.** `index + count > data.Length` has `count <= 4`, and a .NET byte array is at most `0x7FFFFFC7` long, so `index + 4` cannot overflow. The fix sits in `ReadTlv`, so every caller gets it: the response, the status, and the token parse in `Rfc3161TimeStampToken`. The fix in the authority covers both the CMS-with-TSA path and the document-timestamp path.
- **The test tightening is correct and needed.** The Signing Engineer and QA each showed that the committed negative-status test still passed with the status check reverted, because the stand-in token fails later with another `PdfTimestampException`. `AssertTimestampResponseDoesNotExtend` now asserts that the message contains a phrase. Each phrase occurs once in `src/` (`HttpTimestampAuthority.cs:67` and `DerCodec.cs:158`). Both seats report that each test now fails when only its own fix is reverted, on `net8.0` and `net6.0`. The helper still asserts byte equality of the stream, which is stronger than "does not extend". The change follows the existing `RejectTimestamp` pattern and adds no abstraction.
- **Checklist.**
  - Charter: no new capability, no package, and no public API change.
  - Resources: no new allocation.
  - Failure: both inputs throw `PdfTimestampException` before the appendix is written, and the destination bytes stay unchanged.
  - Security: malformed input is now bounded on these two paths.
  - Simplicity: the smallest change that meets the criterion.
  - Build: clean (see section 7).
  - Docs: `README.md` already says `HttpTimestampAuthority` "accepts PKI status 0 or 1". The fix makes the code match that sentence. `ACCEPTANCE.md` needs no change.
  - Tests: each confirmed B-27 defect now has a regression test that discriminates.
  - `git diff --check` on the test file passed.

## 4. Defects confirmed

None in B-29.

Observation outside B-29 scope, not executed: `HttpTimestampAuthority.GetTimeStampToken` copies the whole HTTP response into a `MemoryStream` with no size cap. A hostile or broken TSA can make the library buffer an arbitrarily large body. This predates B-29 and does not block it. I raise it with the Product Manager as a candidate item under the working-set rule and G8.

## 5. Evidence missing

None for B-29 at the internally tested level. G8 still needs the long fuzz run (B-18).

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`, with the tool environment set as instructed.

- `dotnet build src/jaytwo.Ergonomics.Pdf -c Release -warnaserror`: `netstandard2.0`, `netstandard2.1`, `net6.0`, and `net8.0` built with 0 warnings and 0 errors. The build includes other seats' uncommitted `PdfEngine.cs` and `PdfParser.cs` changes.
- `dotnet test ... -f <tfm> --filter "FullyQualifiedName~jaytwo.Ergonomics.Pdf.Tests.SignatureTests"`:
  - `net8.0`: 31 passed, 0 failed.
  - `net6.0`: 31 passed, 0 failed.
  - Both runs include `negative_timestamp_status_does_not_extend_the_file` and `overflowing_der_length_does_not_extend_the_file`.
- I did not repeat the revert runs. I rely on the two independent revert runs by the Signing Engineer and QA, which agree.
- The full suite was not run.

## 8. Decisions needed

None. **B-29 is approved**, both the committed `src` fixes and the uncommitted `SignatureTests.cs` tightening, at the internally tested level.

## 9. Handoff

- Product Manager: decide acceptance of B-29, and decide whether the unbounded TSA response buffer becomes a backlog item.
- Orchestrator: commit the `SignatureTests.cs` change with B-29.

G8 is not passed. B-18 stays open.
