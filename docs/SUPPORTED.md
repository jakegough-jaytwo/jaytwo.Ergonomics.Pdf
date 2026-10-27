# Supported capabilities

Draft for owner review. This document summarizes current documented support in `jaytwo.Pdf` and `jaytwo.Ergonomics.Pdf`; it does not adopt new scope or change existing guarantees.

The [charter](CHARTER.md) describes the product's purpose and enduring principles. This document describes the operations and inputs supported today. The [behavior reference](behavior.md) defines their detailed conditions and failure guarantees, and [acceptance criteria](../ACCEPTANCE.md) record the checks the test suite enforces. Planned requirements and unadopted research proposals are not current support. This summary makes no release or production-qualification claim.

## Meaning of support

Support applies to a particular operation under its documented conditions. Opening or inspecting a document does not establish that it can be filled, flattened, assembled, or signed. Password permissions, certification, field locks, required appearances, document structures, and resource limits can restrict individual operations.

PDF compatibility is deliberately bounded. Unsupported required structures and malformed inputs may be rejected. The parser does not scan the whole file to repair cross-references. Only the writer quirks listed in the behavior reference are tolerated.

## Libraries and document access

| Library | Supported role |
| --- | --- |
| `jaytwo.Pdf` | Inspect documents and perform supported edits, incremental saves, page extraction and concatenation, and signing through its public document and editor APIs. |
| `jaytwo.Ergonomics.Pdf` | Compose form fills and flattening, document information edits, attachments, and rotation; export form values; combine selected pages; finish workflows with a save, signature, or document timestamp. |

Documents open synchronously or asynchronously from a readable, seekable stream or a stable random-access `IPdfSource`. Ownership is explicit. A document provides reads; its editor owns changes and saves. Disposing either does not save. Documents and editors are not thread-safe.

PDF 1.0 through 1.7 are supported, together with the specific PDF 2.0 structures described in the behavior reference. This is not comprehensive PDF 2.0 support. Classic cross-reference tables, cross-reference streams, hybrid references, and object streams are read. Required stream decoding supports Flate, LZW, RunLength, ASCIIHex, and ASCII85, including their documented abbreviations. Page content streams are not decoded.

## Operations

| Area | Supported operations | Material conditions and limits |
| --- | --- | --- |
| Inspection | Read page geometry and rotation, form fields and values, document information and XMP, embedded attachments, signatures, and logical revisions. | Signature inspection reports structure and embedded claims; it does not validate signatures or trust. Reading a lazily accessed structure can fail even after opening succeeds. |
| Form filling and export | Fill text fields, checkboxes, radio buttons, and choice fields individually or in bulk; export terminal field values. | Bulk fill validates every entry before changing the document. Password, signature, push-button, and read-only fields cannot be filled. Export excludes password fields, signature fields, and push buttons. |
| Form appearances | Generate Helvetica appearances for supported WinAnsi text and choice values; reuse button appearances and generate supported missing button marks. | Comb, rich-text, and non-WinAnsi values require viewer-generated appearances. Signing and flattening refuse while `/NeedAppearances` is true. Caller-supplied appearance fonts are not supported. |
| Form flattening | Flatten all eligible fields or named fields using their normal widget appearances. | Requires usable appearances and applicable permissions. Signed signature fields are never flattened. Signed documents require explicit consent where flattening is allowed; certification and field locks still apply. |
| XFA-bearing forms | Classify hybrid and XFA-only forms; fill the AcroForm portion of an unsigned hybrid form. | Filling an unsigned hybrid removes its XFA and Reader usage rights. Signed hybrid filling and XFA-only filling are refused. XFA packets are not edited. Flattening and assembly have their own documented XFA restrictions. |
| Metadata | Read and write document information and XMP. | XMP must meet the documented XML and size restrictions. Certification blocks metadata writes at every level. |
| Attachments | List, open, add, replace, and remove document-level embedded files. | Subject to permissions and certification. Page-level attachment annotations are not an attachment-editing feature. Associated-file keys are a documented PDF 1.x compatibility extension; no PDF/A conformance is claimed. |
| Rotation | Set page rotation in multiples of 90 degrees. | Requires assembly permission under user authentication. Certification blocks rotation; approval signatures and document timestamps require explicit consent to breaking signatures. |
| Incremental updates | Save pending edits, preserving the original document bytes as a prefix. | Same-stream saves append. Cross-stream saves require an empty destination and copy before appending. Updates follow the source's final cross-reference format; no object streams are written. |
| Page assembly | Extract or concatenate ordered page selections into a new, unencrypted PDF; repeat and reorder pages. | Selected content, resources, eligible fields, annotations, and links are preserved within the documented assembly subset. Assembly is not a lossless copy of all document semantics; see below. |
| Digital signing | Append detached CMS signatures; create PAdES baseline B-B and, with an RFC 3161 signature timestamp, B-T; append separate document timestamps. | Private keys stay with the host. Certification, field locks, signature-field policy, permissions, appearances, and signature size limits apply. Timestamp inclusion does not establish trust. |

## Password-protected and signed documents

Password-based standard security revisions 2 through 6 are supported: RC4, AES-128, and AES-256 as specified for each revision and crypt filter. Suitable user or owner credentials are required; omitting a password tries the empty user password. User authentication enforces the applicable permission bits. Owner authentication bypasses password permissions, but does not bypass certification or field locks.

Incremental edits retain the existing encryption and encrypt new data with the existing cipher, subject to the documented exceptions such as signature `/Contents`. Adding encryption and public-key encryption are unsupported. Page assembly removes encryption only with the caller's explicit consent and the required permissions; it does not offer encrypted output.

Certification at DocMDP level 1 blocks every write and signing. Levels 2 and 3 permit form filling and signing, subject to other restrictions; metadata, attachments, flattening, and rotation remain blocked. An unresolved certification is treated as level 1. FieldMDP restrictions apply to filling and flattening. Reader usage rights are kept or removed according to the operation-specific rules in the behavior reference.

Preserving existing signature bytes does not promise that a validator will accept a later modification. Signature inspection does not verify document digests, cryptographic signatures, certificate trust, revocation, or timestamp trust.

Signing supports RSA PKCS#1 v1.5 and ECDSA P-256/P-384 with SHA-256, SHA-384, or SHA-512 through host-provided signing interfaces. The host can integrate PKCS#11; neither library loads PKCS#11 directly. PAdES long-term validation is not supported.

## Page assembly preservation

Assembly preserves selected page content and images without decoding their streams, inherited page geometry and resources, supported interactive fields, supported basic annotations, and supported links. Field names are scoped per input occurrence. Links to unselected pages are omitted; supported external links are limited to HTTP, HTTPS, and mailto, under the behavior reference's URI and action restrictions.

The new document omits existing signatures and their widgets, outlines, tags, page labels, XMP, document attachments, catalog actions, viewer preferences, and permissions. It receives a new identifier and minimal document information. Some unsupported annotations or appearances are omitted; other structures cause refusal before output. Associated files, optional-content catalog properties, dynamic XFA, and XFA-only forms have explicit refusal rules. Consult [page extraction and concatenation](behavior.md#page-extraction-and-concatenation) before relying on preservation.

## Output and resource limits

The engine uses random access, long offsets, and bounded buffering. Structural, decompression, and signing limits remain part of support; this is not a promise to process every document of a particular file size. Exact caps are listed in the [support matrix](behavior.md#support-matrix).

Failure guarantees depend on the API and the point of failure. Signing failures before destination writing leave destination bytes unchanged. Once engine destination writing begins, restoration is best effort and partial output can remain. Direct page assembly can leave incomplete output after writing begins. Workflow file outputs use temporary-file publication; workflow stream outputs have their own copy and restoration guarantees. The behavior reference specifies each case. None of these guarantees makes arbitrary output safe to retry after a destination failure.

## Unsupported operations

The libraries do not provide rendering, page-content text extraction, authoring or layout, stamping or watermarks, redaction, PDF/A conversion or conformance guarantees, cryptographic signature validation, XFA editing, JavaScript execution, general annotation editing, page-level attachment editing, public-key encryption, or adding encryption. General PDF repair, rewriting an input file in place, compaction, and general removal of revision history are not supported operations.

Creating a new PDF from selected existing pages is supported assembly; creating a document from scratch is not supported.

## Maintaining this document

Update this summary when an approved change ships a capability or changes a material support boundary. Update the detailed behavior and acceptance contracts in the same work item when their public guarantees change. Proposed capabilities remain in specifications or planning records until implemented and verified. Existing internal code alone does not establish supported public behavior.
