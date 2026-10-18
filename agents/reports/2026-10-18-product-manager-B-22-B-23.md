# B-22 and B-23

## Task and owner

Product Manager. Added two backlog items. No product code. No gate is passed. No closed task is reopened.

## Verified facts

B-16 is closed in `agents/reports/2026-10-18-product-manager-B-16.md`. G6 is not passed. The Technical Lead is removing that row from the board. This report does not edit `board.md`.

The evidence inventory in `agents/state/status.md` still says there is no support matrix, no throwing-signer test, no imprint or nonce check, and no G6 alignment, encoding, clipping, rotation, CropBox, or font-size render. The release-readiness note still says the bar has not been assessed.

G3 still has no independent Info or XMP reader and no independent attachment-byte comparison.

## Changes made or proposed

`agents/state/backlog.md`:

- **B-22. Refresh the evidence inventory.** QA may edit only the evidence inventory and the release-readiness note. No gate becomes passed. A missing tool stays "not run."
- **B-23. Independent reader for Info, XMP, and attachment bytes.** Tests only, using pypdf 6.21.1 or qpdf 12.4.2. `qpdf --check` is not that reader. G3 is not passed.

B-13, B-14, B-15, B-17, and B-18 stay open and are not on the board. This machine has no fourth PDF producer, no pdfsig, no timestamp authority besides pyHanko, no approved memory budget, and no fuzz run. B-22 and B-23 are not on the board.

## Handoff

The Technical Lead boards B-22 and B-23 when they are ready. QA owns the inventory refresh. No gate is accepted.
