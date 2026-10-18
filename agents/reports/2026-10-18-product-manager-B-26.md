# B-26 acceptance

## Task and owner

Product Manager. Accepted B-26 as the parser review. Edited `agents/state/backlog.md` only. G8 is not passed. B-18 stays open.

## Verified facts

The Signing Engineer reported seven defects in `agents/reports/2026-10-18-signing-engineer-B-26.md`. None were executed. This is not the 24 CPU-hour fuzz run.

The Technical Lead confirmed all seven from the code in `agents/reports/2026-10-18-tech-lead-B-26-review.md`. That review does not pass G8.

1. Comment skipping recurses with no depth cap.
2. A `/Prev` chain of distinct offsets recurses with no depth cap.
3. Cyclic `/Length` and object streams parse the same object again.
4. `Resolve` follows reference cycles with no seen set.
5. `WalkField` recurses on `/Kids` with no seen set.
6. `WalkNames` recurses on name-tree `/Kids` without recording the node.
7. A malformed xref `/Index`, xref field width, or object-stream header throws `IndexOutOfRangeException`, `FormatException`, or `OverflowException`.

B-25 and B-27 stay open.

## Recommendation

B-28 is the bounded fix for those seven items. The Core Engineer implements it. It is not on the board. A major redesign is an owner decision. B-28 does not pass G8 and does not replace B-18.

## Handoff

B-26 is closed. B-28 is not on the board. G8 stays unpassed.
