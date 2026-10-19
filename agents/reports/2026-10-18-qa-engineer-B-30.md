# B-30 QA verification

## 1. Task and owner

QA Engineer. Independently verify B-30, "Bound the predictor `/Columns` allocation", against `agents/state/backlog.md` and the bound in `agents/reports/2026-10-18-tech-lead-B-30-board.md`. Implementation uncommitted on `wip`. G8 is not passed.

## 2. Verified facts

- With the fix, all three required cases in `OpenTests.predictor_columns_from_xref_stream_do_not_allocate_before_a_row_exists` pass on `net8.0` and `net6.0`. Each asserts `PdfParseException`, a per-thread allocation delta under 8,388,608 bytes, and stream bytes equal to the input.
- With both src fixes reverted (`git stash push -- PdfParser.cs PdfStreamDecoder.cs`), all three cases fail on both frameworks on the allocation assertion. `PdfParseException` was already thrown; only the allocation was wrong.

| Case | net8.0 unfixed | net6.0 unfixed |
|---|---|---|
| `/W [268435455 0 0] /Predictor 12` | 268,526,576 | 268,526,704 |
| `/W [1 1 1] /Predictor 12 /Columns 268435455` | 268,527,144 | 268,543,352 |
| `/W [4563402751 0 0] /Predictor 12` | 268,526,584 | 268,526,584 |

- `xref_stream_with_predictor_and_object_stream_reads_info`, the `ACCEPTANCE.md` row for xref streams with Flate and PNG predictor 12, passed in every run, fixed and unfixed, on both frameworks.
- Isolation, `net8.0`. With only `PdfStreamDecoder.cs` reverted, case 2 fails (268,527,184 bytes) and cases 1 and 3 pass, because the parser fix rejects `/W` first. With only `PdfParser.cs` reverted, all three pass, because the decoder fix returns before any `/Columns`-sized allocation.
- By reading the code: the `UnpredictPng` early return also covers the non-xref `OpenDecoded` path. `samples * bpc` cannot overflow `long`, and `rowBytes < int.MaxValue` keeps `rowBytes + 1` in range.

## 4. Defects confirmed

None in behavior.

Test gap, low: no test fails when only the `EnsureXrefColumns` `/W` validation is reverted. With the decoder fix in place, the wrapped `/W` sum does not change behavior that can be observed, so this validation is defense in depth. Case 3 does not independently guard it. The Technical Lead decides whether this matters.

## 5. Evidence missing

- No test covers an explicit `/Columns 268435455` on a non-xref predictor stream (`OpenDecoded`). That path is covered by reading the code only.
- The full suite was not run, per the brief. This is not a fuzz run.

## 7. Verification run

.NET SDK 10.0.100-rc.1.25451.107. `dotnet test test/jaytwo.Ergonomics.Pdf.Tests --filter "FullyQualifiedName~OpenTests" -f <tfm> --logger "console;verbosity=normal"`.

- Fixed: `net8.0` 50/50 passed, `net6.0` 50/50 passed, run twice, including the final run on the restored tree.
- Both fixes reverted: `net8.0` 47 passed, 3 failed. `net6.0` 47 passed, 3 failed.
- After the reverts, `git hash-object` of both src files matches the pre-QA values (`e43f1de…`, `ec7b807…`). The stash list is empty.

## 9. Handoff

QA sign-off given for B-30 at the Internally tested level. Next: the Technical Lead reviews, including the test gap above. G8 is not passed.

## Re-verification (rework)

Input: `agents/reports/2026-10-18-tech-lead-B-30-review.md` section 6, and the Rework section of the Core Engineer report. Only `OpenTests.cs` changed. The src hashes are unchanged from the first verification (`e43f1de…`, `ec7b807…`).

| Tree | net8.0 | net6.0 |
|---|---|---|
| Fixed | 51/51 passed | 51/51 passed |
| `PdfParser.cs` only at HEAD | 50 passed, 1 failed | 50 passed, 1 failed |
| Both src files at HEAD | 47 passed, 4 failed | 47 passed, 4 failed |

- With only `PdfParser.cs` reverted, `wrapping_xref_widths_are_refused_before_they_become_predictor_columns` fails on both frameworks. Its `Assert.Contains("/W")` fails because the actual message is "Predictor rows are not aligned." The three theory cases still pass in that tree. The parser gap is now guarded.
- With both src files reverted, the theory still fails on the allocation bound after the helper refactor, at 268,526,576 to 268,543,352 bytes. The new test fails too.
- `xref_stream_with_predictor_and_object_stream_reads_info` passed in every tree.
- I restored the src files from scratch copies. `git hash-object` for both src files and `OpenTests.cs` (`842540a…`) matches the values taken before re-verification.

The test gap is closed. QA sign-off stands at the Internally tested level. G8 is not passed. Next: the Technical Lead re-reviews.
