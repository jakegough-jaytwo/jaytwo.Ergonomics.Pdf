# B-29 Bound the two confirmed signer paths, QA verification

## 1. Task and owner

QA Engineer. Independently verify B-29 against its acceptance criteria in `state/backlog.md`. The fixes are in `Signatures/DerCodec.cs` and `Signatures/HttpTimestampAuthority.cs`, committed at `ee10c84`. The uncommitted test tightening is in `SignatureTests.cs`. No edits to code, tests, or state. No commit. G8 is not passed.

## 2. Verified facts

1. **The fixes are as reported.** `DerCodec.ReadTlv` (line 156) checks `length < 0 || length > data.Length - index`. `HttpTimestampAuthority.ExtractToken` (line 65) rejects `code < 0 || code > 1`. Both throw `PdfTimestampException`. `git diff` on `src/jaytwo.Ergonomics.Pdf/Signatures` is empty, so the fixes are exactly HEAD.

2. **The test tightening is sound.** `AssertTimestampResponseDoesNotExtend(response, phrase)` drives `PdfDocument.Sign` with `DocTimeStampSignatureProvider` over `HttpTimestampAuthority` and a canned HTTP response. It asserts `PdfTimestampException`, asserts that the message contains `phrase`, and asserts that the stream bytes equal the original PDF. Byte equality is stronger than an unchanged length. Each phrase occurs once in `src/`:
   - `"rejected the request with status"` is in `HttpTimestampAuthority.cs:67`.
   - `"exceeds its buffer"` is in `DerCodec.cs:158`.

3. **`negative_timestamp_status_does_not_extend_the_file` meets the criterion.** The status is `02 04 80 00 00 00`, which `ReadInteger` folds to `int.MinValue`. With the fix it throws for the status reason and the file is unchanged. With the check reverted to `code > 1`, it fails on both frameworks: `Assert.Contains() Failure, Not found: rejected the request with status`. The later failure was "The timestamp token is not a time-stamp token." This confirms the Signing Engineer's finding: the committed type-only assertion could not discriminate, and the tightened assertion can.

4. **`overflowing_der_length_does_not_extend_the_file` meets the criterion.** The input is `30 84 7F FF FF FF`, so `index` is 6 and `length` is `0x7FFFFFFF`, and `index + length` wraps. With the fix it throws `PdfTimestampException` ("DER value exceeds its buffer.") and the file is unchanged. With the check reverted to `index + length > data.Length`, it fails on both frameworks: expected `PdfTimestampException`, actual `ArgumentOutOfRangeException`.

5. **Each revert fails only its own test.** The other B-29 test still passed under each revert.

6. **`src` was restored.** Each revert was restored with `git checkout` on that one file. `git diff --quiet -- src/jaytwo.Ergonomics.Pdf/Signatures` succeeded after each restore. The SHA-256 of `git diff -- src` was `af510768…beac` before and after, so the other seats' uncommitted `PdfEngine.cs` and `PdfParser.cs` changes were not disturbed. `git status --short src` lists only those two files.

Both B-29 criteria are met at the internally tested level. QA signs off. G8 is not passed.

## 3. Assumptions

- The negative-status phrase is shared with the `code > 1` path, so it does not pin the value `-2147483648`. The revert run shows that this is enough to discriminate. Low; no action needed.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`, with the tool environment set as instructed. Filters were targeted; the full suite was not run.

| State | Filter | net8.0 | net6.0 |
|---|---|---|---|
| Fixes present (before) | `FullyQualifiedName~SignatureTests` | 31/31 pass | 31/31 pass |
| Status check reverted | both B-29 tests | negative-status fails (phrase not found), overflow passes | same |
| DER check reverted | both B-29 tests | overflow fails (`ArgumentOutOfRangeException`), negative-status passes | same |
| Restored (after) | `FullyQualifiedName~SignatureTests` | 31/31 pass | 31/31 pass |

Logs: `%TEMP%\b29-{base,status,der,final}-{net8.0,net6.0}.log`.

## 9. Handoff

Technical Lead reviews the `SignatureTests.cs` test change and integrates it. Product Manager decides acceptance. G8 is not passed. B-18 stays open.
