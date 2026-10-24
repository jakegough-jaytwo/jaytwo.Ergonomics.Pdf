# Phase 2 specification: write a new file

Status: **adopted for N1 and N2**, 2026-10-24 (D-38, D-61). Behavior for N1 and N2 is target behavior; its requirement rows (section 6) are New until the work item that ships each one sets it to Exists. N3 to N5 stay a draft outline, evaluated in reset stage R5 (D-54).

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

One internal writer serves N1 and N2 and is designed to serve N3 to N5. It reuses the Phase 1 parser, cross-reference reader, decryption, and stream access.

1. Choose the root objects: the selected pages. Do not start from the source catalog, and do not decide what to copy by unrestricted reachability.
2. Copy by typed per-structure policy. Each reference has one outcome: copy, remap, omit, or reject. The same key means different things in different dictionaries, so `/Parent` on a page, `/P` on an annotation, `/Parent` on a field, and article `/B` each have their own rule. A source object's identity map registers the destination object before its children are traversed, so legitimate cycles end.
3. Write one page dictionary and its page-owned annotations per output page. Share fonts, images, and other immutable resources once per source document. Do not deduplicate across documents.
4. Copy streams **raw**: still compressed, never decoded, so gigabyte images pass through in constant memory. Remove only the encryption layer of an encrypted input. Remove a `/Crypt` filter and its `/DecodeParms` entry when the layer is removed. Write `/Length` as an indirect object after the stream where the length is not known first. This does not stop the engine from decoding a structural object stream to import an object from it.
5. Resolve inherited `/Resources`, `/MediaBox`, `/CropBox`, and `/Rotate` onto each copied page. Keep `/UserUnit`, the transparency `/Group`, and explicit `/TrimBox`, `/BleedBox`, and `/ArtBox`. Do not import the source page tree. Resources a page inherited and does not use are copied, because finding them would require reading the content stream.
6. Write a new page tree with at most 64 children per node.
7. Write a cross-reference section and trailer at the end: a classic table when every offset and entry fits it, otherwise a cross-reference stream. Never write an object stream.
8. Output needs a writable stream, not a seekable one, because offsets are counted as bytes are written. A failure after the first output byte leaves an invalid file that the caller must discard. Path-based workflow saves publish through a temporary sibling file (F17).

Resource model:

- Memory follows the imported structural object mappings, the output page instances, the cross-reference state, bounded parser and stream buffers, and the objects being processed. It does not grow with total input file bytes or with unselected pages.
- Inputs stay seekable sources. Concatenating many files keeps one input open at a time when possible.

## 3. What happens to document-level structures

| Structure | Rule |
| --- | --- |
| Form fields | Kept for copied pages under one synthetic top-level parent field per input occurrence, so equal names in different occurrences stay separate. Never flattened implicitly. A form whose scripts, calculation order, or default resources the writer cannot preserve is refused. |
| Signatures | Every signature field and its widget is omitted, signed or not. No signed appearance survives as a claim that the signature is valid. `/UR3`, `/DocMDP`, and `/DSS` are dropped. |
| Bookmarks (outlines) | Dropped. |
| Links and named destinations | Whitelisted links only (A6 to A9). Named destinations become direct destinations; no name tree is written. |
| Tagged structure (accessibility tree) | Dropped. `/StructTreeRoot` is omitted, `/MarkInfo /Marked` is cleared, and `/StructParents` and `/StructParent` are removed. |
| Info and XMP | A new minimal `/Info` is written. XMP is omitted. No PDF/A, PDF/UA, or PDF/X claim is carried over. |
| Document `/ID` | A new identifier. |
| Attachments | Document attachments are omitted. A nonempty `/AF` is refused within the inspection scope of A5. |
| Optional content (layers) | A nonempty `/OCProperties` is refused. Dropping it could make hidden content visible. |
| XFA | Dynamic XFA is refused. |
| Catalog `/OpenAction`, `/AA`, `/ViewerPreferences`, `/Perms`, and the JavaScript name tree | Omitted. |
| Encryption | The output is unencrypted. An encrypted input needs the explicit option in A11. Adding or keeping encryption on the output is not offered. |
| Page labels | Dropped. |

## 4. Out of Phase 2

Everything in `phase-1.md` section 11 under **Never**, plus: splitting by bookmarks or by size, optimizing or recompressing images, linearizing output, repairing broken files, and merging forms field-by-field (combining values of same-named fields).

## 5. Interview answers

The ten questions are settled (D-61).

1. **Bookmarks.** Dropped. Rebasing them is a later feature.
2. **Links.** A whitelist of internal and external links is kept (A6 to A9). Everything else is dropped.
3. **Field name collisions in concatenation.** A parent field per input occurrence. No implicit flatten. Unpreservable forms are refused.
4. **Accessibility tags.** Dropped, and the file is no longer marked tagged.
5. **Document-level data in concatenation.** Not taken from the first input. A new minimal `/Info`, no XMP, no document attachments. A nonempty `/AF` is refused.
6. **Encryption of output.** Always unencrypted, and only with an explicit option. Re-encrypting the output is deferred.
7. **Page labels.** Dropped. Preserving them ranks above outlines in the backlog.
8. **Output format.** Header 1.5 or later, classic table or cross-reference stream by offset size, no object streams.
9. **Permissions.** The owner password bypasses `/P`. For a user password, R2 needs bits 4 and 5 and R3 or later needs bits 5 and 11, for every input. Bit 3 (print) and bit 10 (accessibility) are not part of the rule. This is a conservative product policy; ISO 32000 defines no bit for building a file from selected pages.
10. **Memory.** The old-to-new map is acceptable with explicit budgets and a dedicated resource-limit error. No spill to disk.

## 6. Requirements

| ID | Requirement | Status |
| --- | --- | --- |
| A1 | N1 and N2 accept an ordered sequence of input occurrences. Each occurrence identifies one source document and an ordered selection of positive, 1-based page numbers. A source page may appear multiple times within one occurrence. A page number above the source page count is rejected before output. | New |
| A2 | Input occurrences are independent for link resolution and form-field namespaces, even when they reference the same source document. Repeated source pages within one occurrence produce distinct output page instances. Immutable source resources may be shared across instances. | New |
| A3 | Every signature field and its widget is omitted, signed or unsigned. `/UR3`, `/DocMDP`, and `/DSS` are dropped. The output never carries a signed appearance that implies a signature remains valid. | New |
| A4 | Outlines, tagged structure, page labels, XMP, document attachments, `/OpenAction`, catalog `/AA`, viewer preferences, `/Perms`, and the JavaScript name tree are omitted. A new minimal `/Info` and a new `/ID` are written. | New |
| A5 | A nonempty catalog `/AF`, a nonempty catalog `/OCProperties`, or dynamic XFA is refused with a typed error. Nonempty `/AF` entries found on selected pages, their annotations, imported Form or Image XObjects, or inspected `/MCAF` resource properties are also refused. These checks use bounded structural inspection and complete before output begins. The structure tree is not traversed, and associations that exist only on discarded structure elements are not guaranteed to be detected. An association within the inspected scope that cannot be resolved, or that exceeds a budget, fails closed. | New |
| A6 | An internal link or destination that targets a copied page is remapped. A link to a different page targets that page's first occurrence within the same input occurrence. A link to its own page targets its own output copy. A named destination becomes a direct destination. A target that is not in the occurrence drops the link. | New |
| A7 | An external link is preserved only when its action is `/S /URI`, the effective URI is a syntactically valid absolute `http`, `https`, or `mailto` URI, `/IsMap` is absent or false, `/Next` is absent, and the annotation does not contain `/AA`. An `http` or `https` URI that contains userinfo is unsupported. An unsupported link is omitted in its entirety. The link is rebuilt from `/Rect`, `/Border`, `/BS`, `/C`, `/H`, `/F`, `/QuadPoints`, and the action; `/PA`, `/SE`, `/StructParent`, and the source `/P` are not copied. `/GoToR`, `/Launch`, and `/JavaScript` actions are not supported. | New |
| A8 | A relative URI reference is resolved by RFC 3986 against a syntactically valid absolute `http` or `https` catalog `/URI /Base`. The base and the resolved URI contain no userinfo, and the resolved URI satisfies A7. An invalid base, an unresolvable reference, or an unsupported result omits the link. A base without a trailing slash is valid. No base is inferred from a file path, and no network access occurs. | New |
| A9 | A supported `/AP` appearance and its required resources are copied structurally under object-count, depth, and byte limits. An unsupported, unclassifiable, or locally over-budget appearance omits its annotation, and the omission is counted before any object that belongs only to that annotation is emitted. A global resource-limit failure, a source-integrity failure, or a failure after serialization of the annotation has begun aborts the operation. | New |
| A10 | The result reports each omission by reason code and count. It holds no URLs, objects, or per-page history, and its size does not grow with the number of pages or annotations. A refused operation throws a typed error instead of returning a result. | New |
| A11 | An encrypted input is refused unless the caller sets an option that allows unencrypted output. With the option, an input authenticated as owner is accepted, and an input authenticated as user is accepted only when the permissions in Q9 allow it. Every input is checked separately, including the same file opened with different credentials. Public-key encryption stays refused. | New |
| A12 | Before output begins, the writer selects a header version no lower than 1.5 and no lower than the effective version of any selected input whose features are retained. It writes classic cross-reference tables when their offset and entry constraints are met, and cross-reference streams otherwise. It writes no object streams. Offsets are `long`. | New |
| A13 | Memory is bounded by the imported structural object mappings, the output page instances, the cross-reference state, bounded parser and stream buffers, and the objects being processed. It does not grow with total input file bytes or with unselected pages. Limits for mapped objects, traversal depth, output pages, and aggregate structural memory raise a dedicated resource-limit error. No disk-backed mapping is required. | New |
| A14 | The external-link option has two states, `PreserveSupported` (the default) and `Drop`. | New |
| A15 | A test fixture added for these requirements has documented provenance and redistribution rights, and follows the interoperability-corpus rules in `QUALIFICATION.md`. A fixture needed only for a focused regression is not a corpus member. The tests cover the PDF Association link files for URI chains, `/Base`, `/IsMap`, and link appearances, a Factur-X hybrid invoice, a page with inherited resources and rotation, a repeated page, a file concatenated twice, and an AES-256 input. | New |

**Never:** rendering, text extraction, stamping and watermarks, layout or creating PDFs from scratch, redaction, signature validation (trust, revocation, cryptographic verification), PDF/A conversion, XFA editing, JavaScript, annotation editing beyond what the copy requires, page-level file attachment annotations, public-key encryption, and adding encryption to a file.
