# Embedded attachments

**Adopted subject contract, revision W-218-ATTACHMENTS-2026-10-31-R2, approved by the owner on 2026-10-31 after review corrections.** Authority: [charter](../charter.md) > [supported capabilities](../supported.md) > adopted contracts. Apply [shared rules](shared.md), [security](security.md) and [saving](opening-and-saving.md). This covers document-level EmbeddedFiles/catalog associated files, not page attachment annotations, PDF/A-3 validation or secure erasure.

## Listing, identity and collisions: ATTACH-ID-01

List each EmbeddedFiles name-tree entry separately, plus each catalog-AF-only embedded-file specification once. Do not duplicate a specification solely because catalog AF also references its name-tree entry. Coincident exposed names must not hide distinct entries. Each listed entry retains an opaque read identity within that open document so its Open reads that entry's payload, not a fresh first-match name lookup. The identity is not a mutation handle and does not survive reopening; an old handle to a replaced/removed entry refuses rather than opening a new payload.

The exposed name is the decoded tree key when it is PDF text; retain current compatibility for a non-text string key by falling back to the file specification's UF, then F. Missing/unestablishable identity or malformed tree traversal fails complete listing, rather than returning a partial list as complete. Preserve raw key bytes separately; match exposed names exactly without case, Unicode or path normalization. A tree key takes precedence over mismatching UF. Name-based lookup/open and replacement require exactly one match; ambiguity refuses instead of selecting a payload. Missing lookup/removal reports not found. For catalog-AF-only embedded files, expose UF then F as the name and apply the same entry-bound reads, unique-name lookup/replacement and explicit remove-all rules. Name collisions are checked across both sources. Listing an AF-only attachment does not invent name-tree membership.

**Compatibility exception:** existing `Remove(name)` and workflow `RemoveAttachment(name)` explicitly remove **every** matching entry; retain that documented bulk operation. Adding an unused name creates one entry; Add's replace-by-name behavior replaces one matching entry, while multiple matches refuse atomically. No write-by-opaque-identity API is introduced. Stricter ambiguous name lookup/replacement and entry-bound reads are target changes, not current behavior claims.

## Add, replace and remove: ATTACH-WRITE-01

The caller supplies a nonempty name, readable seekable content stream at its starting position, and optional description, MIME type, relationship, dates, compression and attachment-panel request. Content runs from captured position through end. Default relationship is Unspecified, compression is off, and absent dates use Add time. Description/MIME/relationship are claims, not payload validation. Write MD5 CheckSum for interoperability, not security. Relationship names include Source, Data, Alternative, Supplement, Unspecified, EncryptedPayload, FormData and Schema, without a PDF/A promise.

Add computes size/checksum and compressed extent with bounded streaming, then save reads again; input must remain open and unchanged until persistence. A detected content change fails save with the existing unchanged-destination guarantee; this does not promise detection of every external mutation in advance. Default `leaveOpen=false` transfers successful Add's content lifetime to the document; `leaveOpen=true` borrows it. Failed Add transfers no ownership: the caller retains disposal responsibility. Reads may move input positions; no restoration is promised.

Replacement retains any existing raw tree key and retargets catalog AF references for that specification to the replacement. Replacing a catalog-AF-only attachment retains that catalog association without adding a name-tree entry. Removal drops the selected name-tree entries, where present, and the selected catalog AF references, including AF-only attachments. Preserve unrelated catalog associations. Preserve unrelated entries, associations, metadata and page content. Refuse a targeted specification with page/object associations outside the supported catalog AF subset if those relationships cannot be preserved; never leave a dangling association silently. New keys retain current PDFDocEncoding/UTF-16BE compatibility rules and serialized-byte ordering. Attachment-panel opening changes PageMode only when requested. Removal leaves recoverable bytes in earlier incremental revisions; it is not redaction or secure deletion.

Each mutation is atomic in pending state: failure/cancellation restores pre-call entries, associations, metadata and rights state while earlier successful edits remain. No destination is written until explicit save/sign. Certified documents refuse attachment writes at every DocMDP level; enforce password permission and Reader rights for Create/Modify/Delete independently. Q-33 permits otherwise eligible signed granting edits and refuses signed removal-dependent edits. No write claims earlier signatures remain accepted by every verifier. Attachment operations neither read nor remove XFA; retain unrelated PDF/A structures without a conformance guarantee.

## Decoded streams and limits: ATTACH-READ-01

A listed entry opens a caller-disposed decoded stream tied to document/source lifetime. Support Flate, LZW, RunLength, ASCIIHex and ASCII85 and their standard aliases; refuse other filters when bytes are requested. Declared Size is nullable untrusted metadata, never an extraction bound. Invalid dates fail when read without making unrelated payload reads fail.

`Open(maxDecodedBytes)` accepts a nonnegative per-open limit in **actual decoded bytes**. Zero permits only empty payload; exactly the limit succeeds, and discovering a further decoded byte throws PdfAttachmentTooLargeException without delivering it. Enforce during streaming despite false Size or high compression. Each open has its own counter. Parameterless Open retains its existing no-caller-limit behavior; there is no invented default payload cap or aggregate extraction API. A caller extracting several entries maintains an aggregate budget and passes its remaining budget to each limited open. Decoder work uses bounded buffers, checked counters and existing structural restrictions; cyclic/malformed name trees refuse rather than revisiting indefinitely. Payload remains untrusted.

Direct extraction copies that stream to a caller-owned destination; it creates no file automatically. Late decode, limit, cancellation or write failure can leave partial destination bytes, length and position; no rollback is promised. Input PDF and pending state remain unchanged. Disposing an entry stream leaves document and borrowed destination open; document disposal invalidates dependent reads.

## Safe filename component: ATTACH-NAME-01

GetSafeFileName returns a nonempty portable component of at most 255 UTF-16 code units **and** 255 UTF-8 bytes. Neutralize both separators, controls, Windows-invalid characters and reserved device names; eliminate trailing periods/spaces and dot/parent components, with a fallback when nothing remains. It contains no directory, drive or absolute-path syntax and need not be unique. Keep the PDF name separate and unchanged.

Callers choose directories, resolve collisions and control creation/overwrite, symlinks and races. The helper is not an extraction sandbox. [Workflow publication](workflows.md) does not make direct stream extraction atomic or turn a component into a secure path.

## Failure and cancellation: ATTACH-FAIL-01

Use [shared categories](shared.md#shared-fail-01-failure-categories), separating malformed tree/payload, unsupported filter, ambiguous/missing target, authorization, decoded limit and source/destination I/O. Known identity/authorization errors precede mutation. Observe cancellation where offered before traversal/copy, between entries/payload chunks and before pending-state commit. Blocked synchronous I/O need not be interruptible; legacy methods without a token gain none implicitly. Restore pre-call pending state; extraction alone permits partial destination. Do not rewind borrowed inputs or destinations. Clean up owned resources and report cleanup failure separately. Save/append/sign/workflow output retain their own destination/unusability rules; no automatic retry follows failure.

## Acceptance map

All targets are **unexecuted**. Compare payload bytes and associations with a second PDF reader, not only this library's writer.

| ID | Scenario |
| --- | --- |
| ATTACH-A1 | Add/save/reopen/read/replace/remove for name-tree and catalog-AF-only attachments: exact payload, metadata, existing raw key and unrelated entries; AF-only replacement creates no tree entry, and old bytes remain recoverable after removal. |
| ATTACH-A2 | Duplicate names across name-tree/AF-only entries, distinct payloads and mismatching UF: read each by entry, ambiguous lookup/replacement refuses, legacy Remove removes every match. After replacement/removal, stale entry handles refuse rather than opening a new payload. |
| ATTACH-A3 | Catalog AF-only and jointly referenced specifications: list without duplication from AF, open/replace/remove by the same rules, preserve membership and unrelated catalog associations; unsupported affected associations preserve or refuse atomically without dangling references. |
| ATTACH-A4 | Zero/exact/exceeded decoded limits, false Size and compressed expansion: excess byte never returned; late extraction failure can leave partial output. |
| ATTACH-A5 | Malformed/cyclic tree, unsupported filter and invalid date: complete listing and independent payload/date failure boundaries. |
| ATTACH-A6 | Unsafe/reserved/long names and colliding safe components: bounded component, no path, caller controls existing-file policy. |
| ATTACH-A7 | Failing Add/read/save and cancellation: earlier edits and changed-content unchanged-destination guarantee. Failed Add with either leaveOpen value transfers no ownership and leaves the stream for caller disposal; successful transferred/borrowed lifetimes follow ATTACH-WRITE-01. |
| ATTACH-A8 | Certification, signed granting/nongranting rights and password denial: no unauthorized pending/output change. |

## Remaining engineering gaps

Entry-bound reads/stale handles, ambiguous lookup/replacement refusal, association preflight and whole-call restoration need implementation comparison and focused evidence. Retain explicit remove-all compatibility. Existing parser/decoder bounds remain applicable; no universal cap is added. No runtime or interoperability check ran for these targets.

## Adoption record

Approved by the owner on 2026-10-31, conditional on addressing the supplied agent review; that condition is satisfied in revision W-218-ATTACHMENTS-2026-10-31-R2. Catalog-AF-only embedded files remain supported document attachments for listing, entry-bound reads, unique-name lookup/replacement and explicit remove-all, preserving catalog-only membership and unrelated associations. This corrects the original draft's compatibility ambiguity. Ambiguous lookup/replacement refusal and stale-handle refusal remain target changes; legacy remove-all and unlimited parameterless Open remain. Acceptance explicitly covers AF-only files, stale handles and failed-Add ownership, with all targets unexecuted. Adoption changes no runtime behavior or qualification status. History: [attachments](drafting/history.md#attachments). Assembly's separate [copy policy](pages.md#default-attachment-bearing-input-refusal-assembly-attach-01-q-122) does not change direct operations.
