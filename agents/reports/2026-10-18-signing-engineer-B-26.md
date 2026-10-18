# B-26 Parser review

## 1. Task and owner

Signing Engineer. Read-only review of the PDF parser. No `src/` edit, no test edit, no `agents/state/` edit, no commit. G8 is not passed. This is not the 24 CPU-hour fuzz run. B-18 stays open.

## 2. Verified facts

`Directory.Build.props` does not set `CheckForOverflowUnderflow`, so integer arithmetic in this code is unchecked.

Buffered decode is capped. `PdfStreamDecoder.Decode` and `ReadAll` stop at the caller's max. Xref streams use `XrefCap` (64 MiB). Object streams and other buffered reads use `StructuralCap` (32 MiB). Literal strings stop at `StructuralCap`. Array and dictionary nesting stops at depth 256. LZW walks stop at the 4096-entry table. A repeated xref offset throws from `LoadXref`. The page tree throws from `WalkPages` when an object id repeats. An object stream that is not an uncompressed object throws. An unsupported encryption revision throws. A truncated classic xref entry throws.

## 3. Assumptions

None of these paths were executed. The defects below are the control flow in the current source. A `StackOverflowException` from that recursion is not catchable. This review does not measure how many frames fit on a given stack.

## 4. Defects confirmed

1. Comment skipping recurses with no depth limit. `PdfParser.ReadToken` consumes one `%` comment and calls `ReadToken` again (`PdfParser.cs` lines 592–604). A run of comments grows the stack for every comment.

2. A `/Prev` chain recurses once per distinct offset, with no depth limit. `LoadXref` rejects a repeated offset (`PdfEngine.cs` lines 1084–1087) and then calls itself for `/XRefStm` and `/Prev` (lines 1119–1122 and 1143–1146). The recursive call is still on the stack when the next offset is loaded.

3. `Load` does not record an object as in progress. The cache write is after `ParseIndirect` returns (`PdfEngine.cs` lines 1346–1371). `ResolveLength` calls that resolver for an indirect `/Length` (`PdfParser.cs` lines 478–488). A stream whose `/Length` refers to itself, or to a stream that refers back, parses the same object again. The same hole exists for an object stream: `ParseObjectStream` calls `Load` before `_objectStreams` records the stream (`PdfEngine.cs` lines 1386 and 1409).

4. `Resolve` follows references with no seen set (`PdfEngine.cs` lines 1538–1546). If object A is a reference to B and B is a reference to A, the loop does not end. `WalkPages` detects a repeated page id only after `Resolve` returns.

5. `WalkField` recurses on `/Kids` with no seen set (`PdfEngine.cs` lines 1898–1906). A cycle in the field tree grows the stack. This runs when fields are collected, not while the xref is loaded.

6. `WalkNames` recurses on name-tree `/Kids` without recording the node (`PdfEngine.cs` lines 2542–2549). Its seen set records embedded-file ids only. A cycle in that tree grows the stack. This runs when attachments are listed.

7. Malformed xref and object-stream integers escape as runtime exceptions. `ParseXrefEntries` indexes `/Index` in pairs with no even-count check (`PdfParser.cs` lines 300–303), so an odd `/Index` throws `IndexOutOfRangeException`. `ReadField` tests `cursor + width` in an unchecked `int` (`PdfParser.cs` lines 438–441); a large positive width skips that test and the byte read throws `IndexOutOfRangeException`. `ParseObjectStream` tests `count * 2` in an unchecked `int` and then calls `int.Parse` (`PdfEngine.cs` lines 1422–1432). A `/N` whose doubled value overflows, or a non-numeric header token, throws `IndexOutOfRangeException`, `FormatException`, or `OverflowException`.

## 5. Evidence missing

No path above was executed. There is no fuzz corpus, seed, or duration. B-18 is still the fuzz item.

## 6. Changes made or proposed

No code change. Report only.

## 7. Verification run

Read only. No test command. G8 is not passed.

Files read: `QUALIFICATION.md` section 10; `agents/state/board.md` (B-26); `agents/state/backlog.md` (B-26, B-18); `Directory.Build.props`; `src/jaytwo.Ergonomics.Pdf/Internal/PdfParser.cs`; `XrefEntry.cs`; `PdfCursor.cs`; `PdfStreamDecoder.cs`; and in `PdfEngine.cs` the open, xref, object-stream, `Load`, `Resolve`, page, field, name-tree, and stream-read methods (`Initialize` through `ReadXrefStream`, `Load` through `Resolve`, `WalkPages`, `WalkField`, `WalkNames`, `OpenBody`, `ReadDecoded`, `IntOf`, `SplitTokens`). `PdfObject.cs` was not read past its type list.

## 9. Handoff

Technical Lead reviews. No product change is proposed. Do not mark G8 passed. B-18 stays open.
