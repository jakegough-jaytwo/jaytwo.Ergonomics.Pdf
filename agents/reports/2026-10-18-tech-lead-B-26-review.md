# B-26 review

## 1. Task and owner

Technical Lead. Review of the seven parser findings. No `src/`, test, `backlog.md`, or `status.md` edit. No fix is boarded. G8 is not passed. None of the paths were executed.

## 2. Verified facts

All seven are confirmed from the code.

1. Confirmed. `ReadToken` consumes one `%` comment and calls itself. Array nesting stops at 256. Comment skipping has no depth cap.
2. Confirmed. `LoadXref` throws `PdfParseException` for a repeated offset. A chain of distinct offsets still calls `LoadXref` again before the current frame returns. There is no depth cap.
3. Confirmed. `Load` writes the cache only after `ParseIndirect` returns. An indirect `/Length` calls that resolver. A stream whose length refers to itself, or a pair that refers to each other, parses the same object again. `ParseObjectStream` calls `Load` before `_objectStreams` records the stream. `_objectStreamParseDepth` is not a depth cap.
4. Confirmed. `Resolve` follows references in a loop with no seen set. If A refers to B and B refers to A, and both are cached as references, the loop does not end. `WalkPages` checks a repeated page id only after `Resolve` returns.
5. Confirmed. `WalkField` recurses on `/Kids` with no seen set. A cycle in the field tree grows the stack.
6. Confirmed. `WalkNames` recurses on name-tree `/Kids` without recording the node. The seen set records embedded-file ids only. A cycle in that tree grows the stack.
7. Confirmed. An odd xref `/Index` reads `index[pair + 1]` and throws `IndexOutOfRangeException`. `ReadField` adds `cursor + width` in an unchecked `int`, so a large positive width skips the length test and the byte read throws `IndexOutOfRangeException`. `ParseObjectStream` multiplies `count * 2` in an unchecked `int` and then calls `int.Parse`. Overflow of that product, or a non-numeric header token, throws `IndexOutOfRangeException`, `FormatException`, or `OverflowException` instead of `PdfParseException`. These are on the open path.

## 6. Changes made or proposed

This report. B-26 stays on `agents/state/board.md`. No fix is boarded. B-25 still owns `EncryptionTests.cs` and `SignatureTests.cs`. B-27 stays a read-only signer review.

## 9. Handoff

A fix for a confirmed item is a later task. This review does not pass G8.
