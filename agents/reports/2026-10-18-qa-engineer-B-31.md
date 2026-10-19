# B-31 QA verification

## 1. Task and owner

QA Engineer. Independently verify B-31, "Cap the timestamp response size". The inputs are `agents/state/backlog.md`, the bound in `agents/reports/2026-10-18-tech-lead-B-31-board.md` (cap 2,097,152 bytes, read limit 2,179,072 bytes), and `agents/reports/2026-10-18-signing-engineer-B-31.md`. The change is uncommitted on `wip`. G8 is not passed.

## 2. Verified facts

- **With the fix**, `SignatureTests` passed 36/36 on `net8.0` and 36/36 on `net6.0`.
- **Red with only `HttpTimestampAuthority.cs` at HEAD**, on both frameworks: 32 passed and 4 failed. The four tests fail as follows:
  - `oversized_timestamp_response_is_refused_after_a_bounded_read`: `InRange` failed. It read 16,777,216 bytes against the 2,179,072 limit.
  - `declared_oversized_timestamp_response_is_refused_before_reading`: it expected 0 bytes read and actually read 16,777,216.
  - `oversized_timestamp_response_does_not_extend_the_file`: the refusal said "not a DER sequence" instead of "larger than".
  - `timestamp_response_at_the_cap_returns_its_token_and_one_byte_more_is_refused`: the 2,097,153-byte body did not throw.
- **Stall guard.** `stalled_timestamp_response_body_still_times_out` passed in both trees, in about 0.5 s. This is expected, because it guards the move to `ResponseHeadersRead` and is not a red-first case.
- **Within-cap token, per the `HttpTimestampAuthority` row in `ACCEPTANCE.md`:**
  - With the fix, a response of exactly 2,097,152 bytes returns its token.
  - `http_timestamp_authority_posts_the_query_and_returns_the_token`, `..._accepts_granted_with_mods`, and `..._rejects_a_failure_status` pass on both frameworks.
  - The other timestamp tests also pass: imprint, digest algorithm, nonce, negative status, document timestamp, and CMS timestamp.
- **No criterion weakened.** The `ACCEPTANCE.md` and `README.md` diffs each add one sentence to the existing timestamp bullet. No text was removed or loosened.
- **Code reading.** The library never holds more than the cap in its `MemoryStream`. The check is `read > MaxResponseBytes - total` before `Write`. A declared `Content-Length` over the cap is refused before the body is opened. The caller's `HttpClient` is not mutated, and there is no public API change.

## 5. Evidence missing

- The `netstandard2.0` / .NET Framework runtime is not exercised, as the implementer's limitation notes. A network stream there might ignore the cancellation token. This is not run.
- The full suite was not run, per the brief. This is not a fuzz run.

## 7. Verification run

I used .NET SDK 10.0.100-rc.1.25451.107 and ran `dotnet test test/jaytwo.Ergonomics.Pdf.Tests --filter "FullyQualifiedName~SignatureTests" -f <tfm> --logger "console;verbosity=normal"` on `net8.0` and `net6.0` in two trees:

- the fix in place;
- `HttpTimestampAuthority.cs` replaced by `git show HEAD:` and then restored from a scratch copy.

After restore, the file's SHA-256 is `5cd82a6b…a9ca9` and its git blob is `26cd620…`, the same as before. The B-30 files were not touched.

## 9. Handoff

QA sign-off is given for B-31 at the Internally tested level. Next, the Technical Lead reviews. G8 is not passed.

## Re-verification (rework)

Inputs: section 6 of `agents/reports/2026-10-18-tech-lead-B-31-review.md` and the Rework section of the Signing Engineer report.

The pre-rework `HttpTimestampAuthority.cs` is my scratch copy from the first verification (SHA-256 `5cd82a6b…a9ca9`). Diffed against the rework, it differs only in three places:

- the send no longer takes the token;
- the response is disposed on timeout;
- the timeout is rethrown as `TaskCanceledException` with a `TimeoutException` inner exception, unless the exception is a `PdfTimestampException`.

| `HttpTimestampAuthority.cs` | net8.0 | net6.0 |
|---|---|---|
| Rework | 37/37 passed | 37/37 passed |
| Pre-rework | 35 passed, 2 failed | 35 passed, 2 failed |

- With the pre-rework file, `stalled_timestamp_response_body_still_times_out` fails on both frameworks. Its inner exception is null where `TimeoutException` was expected.
- With the pre-rework file, `stalled_timestamp_response_headers_still_time_out` also fails. Its inner exception is `TaskCanceledException` on `net8.0` and null on `net6.0`.
- With the rework, both timeout tests pass in about 0.5 s.
- The size-cap tests pass on both frameworks with the rework, and their byte-bound asserts hold:
  - `oversized_..._bounded_read`: at most 2,179,072 bytes read.
  - `oversized_..._does_not_extend_the_file`: at most 2,179,072 bytes read, and the stream bytes are unchanged.
  - `declared_oversized_...`: 0 bytes read.
  - `..._at_the_cap_...`: 2,097,152 bytes read and the token returned; one byte more is refused within the bound.
- I did not verify the .NET Framework abort by disposing the response. It is Implemented, not tested.
- I restored the file from a scratch copy of the rework. Its SHA-256 is `d3ef690f…a8d2` and `SignatureTests.cs` is `dd8f2ee2…89ab`, both the same as before re-verification.

QA sign-off for B-31 stands at the Internally tested level. Next: the Technical Lead re-reviews. G8 is not passed.
