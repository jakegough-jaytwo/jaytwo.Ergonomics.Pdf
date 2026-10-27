# W-218 scope reconciliation: adopted with owner revisions

Adopted on 2026-10-27 as [D-68](../decisions/D-68.md). This record supersedes the original proposal in this file; the original wording remains in Git history.

## Adopted reconciliation

- CHARTER.md expressly permits limited blank-page creation and image placement for image-to-PDF conversion, stamping, and image watermarking.
- AGENTS.md, README, and both phase specs remove the conflicting blanket image exclusions and link to SUPPORTED.md for intended capability boundaries.
- SUPPORTED.md remains independent of implementation status. Image-to-PDF is outside the existing N1/N2 assembly contracts; no current API or N1/N2 output guarantee changes.
- Implementation requires an adopted operation contract. Product-scope changes, architectural changes, and new full-document rewrite strategies require owner approval under the charter. No blanket approval gate for every detailed specification or delivery decision is added.
- W-218 preserves the initial PNG implementation boundary: 8-bit, non-interlaced RGB/RGBA. Exact transparency, color-profile, resource-limit, and refusal rules remain operation-contract work.

## Permission proposal not adopted

Get/set security permissions remains intended scope with appropriate authorization. Limiting setters to already-encrypted documents with owner authentication was an additional proposed decision, not mere reconciliation, and is not adopted. The operation contract must settle supported changes, credentials, encryption interaction, and output strategy. Get/set permissions alone does not authorize adding encryption, password changes/removal, or a new rewrite strategy. N5 remains its separate R5 evaluation.

## Verification boundary

This is documentation and scope reconciliation. Current implemented behavior, acceptance requirements, release qualification, and delivery stages are unchanged. Existing stricter implementation constraints remain in force where this decision does not expressly amend them.
