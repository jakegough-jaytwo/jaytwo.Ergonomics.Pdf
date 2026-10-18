# B-28 QA verification after rework

## 1. Task and owner

QA Engineer. Independent check of the B-28 regressions: the seven confirmed parser paths from the committed work (`ee10c84`), plus the reworked `/Index`, `/N`, and `/First` cases in the uncommitted diff to `PdfParser.cs`, `PdfEngine.cs`, and `OpenTests.cs`. I changed no code, tests, or state files. G8 is not passed. B-18 remains open.

**QA sign-off: given** for the B-28 acceptance criteria, at the internally tested level. The Technical Lead has not yet re-reviewed the rework.

## 2. Verified facts

Every regression in `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs` expects `PdfParseException` and checks, through `AssertMalformedDoesNotExtend`, that the source bytes are unchanged afterwards. I checked three things independently for each path: which guard throws, whether the test fails without the fix, and whether the file stays unextended.

| Path | Regression | Guard that throws (stack frame from probe) | Without the fix |
|---|---|---|---|
| Comment skipping | `consecutive_comments_are_bounded_...` | `PdfParser.ReadToken`: "Too many consecutive PDF comments." | No exception (pre-B-28) |
| `/Prev` chain | `deep_prev_chain_is_bounded_...` | `PdfEngine.LoadXref`: "The cross-reference chain is too deep." | No exception (pre-B-28) |
| Cyclic `/Length` and object stream | `cyclic_stream_dependencies_are_refused_...` | `PdfEngine.Load`: "Cyclic indirect object dependency." (both forms) | Stack overflow, test host crashed |
| `Resolve` cycle | `cyclic_indirect_references_are_refused_...` | `PdfEngine.Resolve`: "Cyclic indirect object reference." | Infinite loop; 30 s blame hang timeout |
| `WalkField` | `cyclic_field_tree_is_refused_...` | `PdfEngine.WalkField`: "The field tree contains a cycle." | Stack overflow |
| `WalkNames` | `cyclic_embedded_file_name_tree_is_refused_...` | `PdfEngine.WalkNames`: "The embedded-file name tree contains a cycle." | Stack overflow |
| Malformed xref and object-stream integers | `malformed_xref_and_object_stream_integers_...` | Odd `/Index`, `/W` range, `/N` range, and header guards, each with its own message | `ArgumentOutOfRangeException` |
| Rework: `/Index` values (7 cases) | `malformed_xref_index_values_...` | `XrefIndexValue`, or the subsection range check for `[2147483647 2]` | 7 of 7 fail (pre-rework `HEAD`) |
| Rework: huge count (3 cases) | `huge_xref_index_count_is_bounded_...` | "Xref stream /Index count exceeds the decoded entries." | Hang; 30 s blame hang timeout (pre-rework `HEAD`) |
| Rework: `/N`, `/First` (6 cases) | `malformed_object_stream_count_or_first_...` | `ObjectStreamInteger` | 6 of 6 fail (pre-rework `HEAD`) |

The pre-rework run gave 13 failures and then the hang, which matches the rework report.

**Huge-count zero-width `/Index`.** `/W [0 0 0] /Index [0 2147483647]` is refused in under 1 ms, with 0 or 3 data bytes. So are these variants: `/Size 2147483647` with no `/Index`, two huge subsections, a huge second subsection after a valid one, and the same stream reached through hybrid `/XRefStm`.

**Probes beyond the committed tests**, all on the reworked code. Each one threw `PdfParseException` from the intended guard and left the bytes unchanged:
- Deep, acyclic chains at 300 levels hit the depth caps in `Load`, `Resolve`, `WalkField`, and `WalkNames`.
- Boundaries still open: 256 consecutive comments, and 255 extra `/Prev` sections.
- `/Index [0 99999999999999999999]` and `/N 99999999999999999999` are refused by the tokenizer ("Integer is out of range.").
- `/Index [0 1 -5 1]` is refused. So are `/Size 1.5` and `/Size -1` when they supply the default index.
- A missing `/N`, and an indirect `/N 7 0 R`, are refused.
- `/Index [2147483647 1]` passes the range check and fails later, for the unrelated reason that the test file has no `/Root`.

The probe results were the same on `net8.0` and `net6.0`.

## 3. Assumptions

A "pre-B-28" copy pairs `PdfParser.cs` and `PdfEngine.cs` from `11da851` with the rest of the current tree. A "pre-rework" copy pairs those two files from `HEAD` with the current tests. Both copies are in the scratchpad, not the repo.

## 5. Evidence missing

These do not block sign-off.
- The seven original facts assert only the exception type, not the refusal reason. Today the intended guard is what fires: the probe shows the stack frame and message above. A later change could still satisfy those facts with a different `PdfParseException`. The rework facts do assert the reason.
- The comment and `/Prev` facts test the 256 cap at 257. The code before B-28 did not crash at that size. These facts prove the cap is enforced. They do not reproduce the original stack exhaustion.
- An indirect `/N` or `/First` is now refused. Before, it was read as 0 and failed later. I know of no producer that emits it. I am recording the behavior, not reporting a defect.
- This is not a fuzz run. G8 is not passed.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`. Runtimes: .NET 8.0.31 and .NET 6. Tool paths for qpdf 12.4.2, Poppler 26.09.0, and SoftHSM 2 were set as the task specifies.

- `dotnet test test/jaytwo.Ergonomics.Pdf.Tests -f net8.0 --filter "FullyQualifiedName~OpenTests" --blame-hang-timeout 60s`: 47/47 passed. On `net6.0`: 47/47 passed.
- `ObjectStreamCacheTests`, `ForeignPdfTests`, `EncryptionTests`, and `DenseMemoryTests` filter: 44/44 on `net8.0`, 44/44 on `net6.0`.
- Probe fact `QaB28Probe.probe_all`, run in a scratch copy of the working tree: 50 inputs on each framework, results above.
- Pre-rework copy, B-28 filter, `net8.0`: 13 failed, then the run was aborted on a hang.
- Pre-B-28 copy, each original fact run alone on `net8.0`: 7 of 7 fail, as listed in the table.
- `git diff --check`: clean. `git status`: unchanged apart from this report.

Scratch copies, the probe source, its output, and the hang dumps are in `%LOCALAPPDATA%\Temp\claude\c--jake-git-jaytwo-Ergonomics-Pdf\dbb993d3-9525-41c5-9984-d9ff6b291669\scratchpad\qa-b28\`.

Evidence level: internally tested. Not a gate pass.

## 9. Handoff

The Technical Lead re-reviews the rework. The Product Manager then accepts B-28 or returns it. Optional follow-up for the Core Engineer, which does not block: add reason assertions to the seven original facts.
