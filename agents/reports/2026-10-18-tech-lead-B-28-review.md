# B-28 review

## 1. Task and owner

Technical Lead. Review of the B-28 parser and engine bounds and their regressions. No code, test, or state edit. G8 is not passed, and B-18 remains open.

## 2. Verified facts

The changes stay within the assigned `PdfParser.cs`, `PdfEngine.cs`, and `OpenTests.cs` boundary. Comment, xref-chain, object-load, reference, field-tree, and name-tree traversal now have finite guards. The object-stream count multiplication and header parsing are checked. The seven new facts expect `PdfParseException` and compare the source bytes after refusal.

The scoped run passed 33/33 on `net8.0` and 33/33 on `net6.0`. The four library targets built with zero warnings and zero errors, and `git diff --check` passed.

## 4. Defects confirmed

Approval is refused because malformed xref `/Index` handling remains incomplete and permits hostile unbounded work.

`PdfParser.ReadIndex` still unchecked-casts every integer or real to `int` without requiring non-negative integral values. `ParseXrefEntries` then uses `start + i` unchecked and loops `count` times. Inputs such as a negative count can be silently skipped, and `/W [0 0 0] /Index [0 2147483647]` can drive billions of iterations and dictionary insertions without consuming decoded bytes. The new test covers only an odd item count. This does not meet the assigned malformed-`/Index` failure behavior or hostile-input bound.

## 5. Evidence missing

Add regressions for negative/out-of-range `/Index` values and for a huge count with zero-width fields. They must throw `PdfParseException` promptly and leave the stream unchanged. QA has not yet supplied the independent B-28 verification required by the workflow.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`.

- All `OpenTests` plus both B-25 facts: 33 passed on `net8.0`; 33 passed on `net6.0`.
- Library build: `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0` succeeded, zero warnings and errors.
- `git diff --check`: passed.

## 8. Decisions needed

None. This is a bounded correction inside the existing B-28 assignment, not a parser redesign.

## 9. Handoff

Core Engineer bounds and validates `/Index`; QA Engineer then verifies all B-28 regressions. G8 remains unpassed, and B-18 remains open.
