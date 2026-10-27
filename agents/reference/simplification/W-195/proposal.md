# W-195 Gate 2 proposal

Draft, 2026-10-26. Research only. R195 identifiers are local proposal labels, not adopted requirements. Approval must cover exact wording and the implementation ticket. Replace the phase-2 A4 and "Bookmarks: Dropped" wording only in the approved implementation transaction. Coordinate destinations with W-192, diagnostics with W-197 and page labels with W-196. Current omission behavior remains shipped.

## Exact proposed wording

**R195-1 - Projection.** Assembly projects each input occurrence's ordinary outline, in input order and then source tree order, into one output outline. It adds no wrapper heading, merges no titles and never links across occurrences. An item targeting a source page selected by its own occurrence is kept and points to the first selected copy of that page, as a direct destination array for all eight destination modes, with named destinations resolved. An item whose structurally valid target page is unselected is not kept as navigation. If it still has surviving descendants it is kept as a destination-less heading with its title, `/C`, `/F` and open state; a heading with no surviving descendant is removed. Items are never reparented or promoted. Page order never reorders the tree. `/Prev`, `/Last` and `/Count` are recomputed and open state is preserved. `/SE` is dropped with tags.

**R195-2 - Refusal.** Refuse before destination writing, regardless of page selection, for: a cycle, a shared item, a parent mismatch, a missing or invalid title, a dangling or non-page or integer-page destination, a missing name, `/Dest` together with `/A`, a bad mode or parameter count, an invalid `/C` or `/F`, and an unresolved structure; these raise `PdfParseException`. Refuse any `/A` other than a single `/GoTo` without `/Next` with `PdfAssemblyNotSupportedException`; an unsupported action is never emitted as a title-only item. Depth over 256 or more than 1,000,000 items fail as limit errors. Diagnostics follow the W-197 frame and carry no title or URI text.

**R195-3 - Count.** A new omission reason `ExcludedOutlineDestination` counts each item with an unselected target, whether it was removed or kept as a heading.

**R195-4 - Scope.** Page labels are separate (W-196). A per-input wrapper heading is not included.

## Proposed implementation slices, all unapproved and unqueued

| Slice | Scope | Acceptance | Effort (inference) |
| --- | --- | --- | --- |
| A: outline reader and projector | New `PdfAssemblyOutlines.cs`, the extractor, the omission enum, focused tests; reuses the W-192 destination resolver and the W-197 frame | Trees in findings.md reproduced; every R195-2 case refuses with zero destination bytes; qpdf, pypdf and MuPDF outline agreement | about 100-200 production lines, 120-200 test lines |
| B: contract documents | Phase-2 A4, "Bookmarks: Dropped" row, `ACCEPTANCE.md` line 224, `docs/behavior.md` line 137, `PdfDocumentEditor.cs` remark near line 153 | Needs Gate 2 and the authorized spec-status transaction | small |

Total about 3-6 engineer-days. Dependencies: W-192 and W-197 first. Bookmark additions are not deletion savings. No new runtime dependency, API or policy switch is proposed.

## Decision and evidence limits

Recommend Rule A. The measured and inferred split, tool coverage gaps (no Acrobat or Preview; title byte fidelity; encrypted input) and the absence of an independent reviewer are in findings.md.
