# Technical Lead review: B-36

## Task and owner

B-36, parser and decoder hardening for audit findings SA-01, SA-02, SA-03, SA-04, SA-09, and SA-11. Implemented by the Core Engineer (`2026-10-18-core-engineer-B-36.md`, with its Rework section). QA signed off in `2026-10-19-qa-engineer-B-36.md` after re-verification. Reviewer: Technical Lead.

## Verified facts

- Charter: no public API change, no runtime package, and nothing sized from `/Size`. Every failure is an existing document exception, raised before a destination grows. Memory follows the working set: the xref map holds one entry per unique object, at 28 bytes a slot, with no per-section copies.
- Claim: `ParseObjectStream` in `PdfEngine.cs` was added to the claim on 2026-10-19, because SA-03 can only be fixed there. `SplitTokens` was deleted. The xref-merge region (`LoadXref`, `ReadXrefStream`) and `XrefEntry.cs` were added for the rework.
- Decisions taken here, not escalated (D-22: resource limits inside the charter): the overlap work budget; SA-09 extended to revisions 5 and 6, which ISO 32000-2 gives the same `Identity` default; the 2,097,152-object ceiling, below the 8,388,607 limit in PDF 1.7 Annex C and needed to keep a hard 256 MiB bound; the `/Index` ascending and non-overlapping rule (ISO 32000 7.5.8.2); and refusing an indirect `/Filter`, which used to return undecoded bytes silently (B-40).
- `XrefEntry` packing: the type takes 2 bits. Field 1 takes 40 bits, and a type-1 offset out of range saturates past any real file, so `Load` still throws. Field 2 takes 22 bits and saturates, and a negative value stays negative, so generations and the existing index check are unaffected.
- Evidence: every finding has a regression test that failed before its fix (`HardeningTests`, 57 cases). QA's revert checks covered SA-02, SA-04, and the entry budget. The full suite passed 269/269 on Windows `net8.0`, `net6.0`, and `net48`, and on Linux `net8.0` and `net6.0`. The Release library builds for all four targets with 0 warnings.

## Defects confirmed

QA-B36-02 (Medium, predates B-36): a classic xref section is built before the budget applies. It is folded into B-41 and does not block B-36.

## Changes made

The Technical Lead added the `ACCEPTANCE.md` SA-09 criterion sentence and the README sentence on `/Index` ordering. Documentation only.

## Handoff

Approved. Product Manager to accept.
