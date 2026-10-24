# Phase 2 specification: write a new file

Status: **draft outline**, 2026-10-19. Scope is adopted (D-38); behavior is not. The owner interview on section 5 finalizes it. Reset stage R3 implements N1 and N2 after the owner settles the section 5 questions they depend on; N3 to N5 are evaluated in R5 (D-54).

Phase 2 adds new-file operations to the `jaytwo.Pdf` engine, with an assembly workflow in `jaytwo.Ergonomics.Pdf`. Release timing is set by the later packaging decision (D-54). It adds operations without breaking the Phase 1 API in force when it starts. The tenets in `phase-1.md` section 2 apply unchanged.

## 1. Scope

Operations that produce a **new file** instead of appending to the original:

| ID | Operation | Use case |
| --- | --- | --- |
| N1 | **Extract pages** | Make a new PDF from selected pages of one PDF ("pages 3–7 and 12"). |
| N2 | **Concatenate** | Make a new PDF from several PDFs, or page ranges of them, in order. |
| N3 | **Compact** | Rewrite one PDF with only its live objects. Removed attachments and superseded revisions no longer take space. |
| N4 | **Remove revisions** | Keep only the current state, so earlier metadata, field values, and attachments cannot be recovered from the file. Same writer as N3. |
| N5 | **Remove password** | Write a decrypted copy, given the owner password (or a user password whose permissions allow it). |

Rotation stays in Phase 1 because it can be appended. Extraction and concatenation may also rotate pages as part of the copy.

## 2. The new-file writer

One internal writer serves N1–N5. It reuses the Phase 1 parser, cross-reference reader, decryption, and stream access.

1. Choose the root objects: selected pages, plus document-level objects to keep (section 5).
2. Walk the object graph from those roots, assigning new object numbers in a map from old to new. Shared objects (fonts, images, resources) are copied once per source document.
3. Write objects in order. Streams are copied **raw**: still compressed, never decoded, so gigabyte images pass through in constant memory. Encrypted inputs are decrypted while copying.
4. Write a new page tree. Attributes a page inherited from its old parents (`/Resources`, `/MediaBox`, `/CropBox`, `/Rotate`) are set on the copied page itself.
5. Write a cross-reference section and trailer at the end.

Resource model, to be confirmed in the interview:

- Memory is the old-to-new map for reachable objects plus one object at a time. It does not grow with stream sizes.
- Output needs only a writable stream, not a seekable one, because offsets are counted as bytes are written.
- Inputs stay seekable streams. Concatenating many files keeps one input open at a time when possible.

## 3. What happens to document-level structures

Draft defaults, each to be confirmed:

| Structure | Draft default |
| --- | --- |
| Form fields | Kept for copied pages. Name collisions across inputs are renamed (see question 3). |
| Existing signatures | Dropped: the signed bytes no longer exist in the new file. Signature fields become empty or are removed. |
| Bookmarks (outlines) | Dropped (question 1). |
| Links and named destinations | Links to copied pages kept; links to dropped pages removed (question 2). |
| Tagged structure (accessibility tree) | Dropped, and the file is no longer marked tagged (question 4). |
| Attachments | Kept from the first input (question 5). |
| Info and XMP | Taken from the first input, with `ModDate` refreshed (question 5). |
| Encryption | Output is unencrypted for N5; for N1–N4, see question 6. |
| Usage rights (`/UR3`), DocMDP, XFA | Dropped. |
| Page labels | Dropped (question 7). |

## 4. Out of Phase 2

Everything in `phase-1.md` section 11 under **Never**, plus: splitting by bookmarks or by size, optimizing or recompressing images, linearizing output, repairing broken files, and merging forms field-by-field (combining values of same-named fields).

## 5. Interview questions

To settle before this spec is final. Recommendations are marked.

1. **Bookmarks.** Drop (recommended for the first release of Phase 2) or keep and rebase them onto the copied pages?
2. **Links.** Keep links to copied pages and drop the rest (recommended), or drop all links?
3. **Field name collisions in concatenation.** Rename with a prefix per input (recommended), flatten each input's fields first, or refuse?
4. **Accessibility tags.** Drop (recommended), or keep the structure tree for extracted pages? Keeping it is a large job.
5. **Document-level data in concatenation.** Metadata and attachments from the first input only (recommended), or merge attachments from all inputs?
6. **Encryption of output.** For extraction, concatenation, and compaction of an encrypted input: write unencrypted (owner password required), or keep the same encryption? Adding new encryption to an unencrypted file is a separate yes-or-no scope decision.
7. **Page labels.** Drop, or renumber?
8. **Output format.** Classic cross-reference only (consistent with Phase 1), or cross-reference and object streams for smaller output, especially for compaction?
9. **Permissions.** Which permission bits gate extraction and concatenation (`CanAssemble`, `CanCopy`) when opened with a user password?
10. **Memory.** Is an old-to-new map proportional to the reachable object count acceptable for a 100,000-page input, or does that need a budget and a spill strategy?
