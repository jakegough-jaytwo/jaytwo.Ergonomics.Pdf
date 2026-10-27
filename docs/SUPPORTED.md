# Supported capabilities

The intended product capabilities and boundaries of `jaytwo.Pdf` and `jaytwo.Ergonomics.Pdf`, whether or not each capability has been implemented.

This document is the durable scope commitment against which implementation, backlog items, and proposed changes are measured. An unfinished capability is a gap to close, not a reason to remove it from scope. Inclusion does not claim that a capability is available or production-qualified today.

The [Project Charter](CHARTER.md) defines why the project exists and its governing principles. This document defines what belongs in the product. The [behavior reference](behavior.md) defines what the implementation currently does, including exact conditions, limitations, preservation rules, and failure guarantees. [Acceptance criteria](../ACCEPTANCE.md) define the outcomes the current tests enforce. Delivery order, implementation status, and release milestones belong in planning records.

## Intended capabilities

| Area | Intended coverage |
| --- | --- |
| Document inspection | Read page count, page geometries, and rotation. |
| AcroForm inspection | Read field names, types, values, and widget geometries. |
| AcroForm population and export | Populate supported text, checkbox, radio, and choice fields, individually or in bulk; export form values. |
| AcroForm flattening | Flatten eligible editable fields into page content, subject to appearance, permission, and signature restrictions. |
| Page rotation | Change page rotation in multiples of 90 degrees. |
| Page extraction | Create a new PDF from selected existing pages, preserving the requested order and allowing repeated and reordered pages. |
| Concatenation | Combine selected pages from existing PDFs into a new PDF, preserving the requested input and page order and allowing repeated inputs and pages. |
| Incremental saves | Save supported edits as incremental updates, preserving the original document bytes as a prefix. |
| Binary attachments | Read, add, replace, and remove document-level embedded files. |
| User metadata | Read and update document information, including authorship information and custom key/value properties, and XMP metadata. |
| Image placement | Place supported images on existing pages with position, size, rotation, opacity, and foreground/background placement, for image stamping and image watermarking. |
| Image to PDF | Create a new PDF with specified page dimensions and place a supported image on the page. |
| Password-protected PDFs | Open and modify supported password-protected PDFs under documented authentication and permission rules. |
| Security permissions | Read and change supported document security permissions with appropriate authorization. |
| Digital signing | Sign PDFs using host-controlled keys, with optional trusted timestamps; append document timestamps. |
| Signature and revision inspection | Inspect signature structures and embedded claims, and list document revisions and signature coverage, without cryptographic validation or trust assessment. |

### Image inputs and shortcuts

Image inputs are JPEG and PNG, within explicitly documented format restrictions. PNG decoding belongs within the library without a third-party image dependency. Unsupported image formats and variants are rejected; general image-format conversion is outside scope, and callers may convert images before supplying them.

Detailed contracts must define JPEG restrictions, PNG transparency and color-profile handling, resource limits, and placement coordinates relative to page rotation and cropping. PNG transparency includes both alpha channels and separate transparent-color declarations; its treatment must be explicit.

`jaytwo.Pdf` owns limited blank-page creation and image placement. `jaytwo.Ergonomics.Pdf` composes those operations into image-to-PDF and selected-page watermarking shortcuts through the public engine API. Neither library becomes a general document-authoring or layout system.

## Support boundaries

Capability families do not promise comprehensive support for every PDF feature or variation. Support is operation-specific. Opening or inspecting a document does not establish that it can be populated, flattened, assembled, stamped, or signed. Permissions, certification, field locks, appearances, document structures, and resource limits can restrict individual operations.

Extraction and concatenation do not imply lossless preservation of every document feature. Their detailed contracts must define supported structures and preservation, omission, and refusal rules.

Password and permission operations require explicit contracts for supported security schemes, authentication, authorization, encryption handling, and output strategy. Permission inspection alone does not satisfy the intended permission-changing capability.

Foreground/background image placement controls painting order; it does not guarantee visibility over every existing page element.

Private signing keys remain under host control. Signature inspection reports structures and claims rather than validating signatures. Timestamp inclusion alone does not establish trust.

Rendering, page-content text extraction, redaction, cryptographic signature validation, general PDF repair, general annotation editing, and general document authoring and layout are outside scope unless explicitly adopted by the project owner. Limited blank-page creation and image placement serve image-to-PDF conversion, image stamping, and image watermarking. Necessary internal support does not establish a general public feature.

## Resource and failure contracts

Supported operations require documented resource limits and explicit failure behavior. File size alone does not determine support; there is no guarantee that every document below a particular size can be processed.

Output guarantees must distinguish the operation, destination type, and point of failure. Detailed contracts must state whether a failure leaves the destination unchanged, permits partial output, or provides restoration or publication guarantees. Rollback and retry safety must be stated explicitly rather than inferred from a failed operation. Exact limits and failure mechanics belong in the behavior reference and its acceptance criteria.

## Maintaining the scope commitment

Change this document deliberately when the project owner approves a change to intended product capabilities or material boundaries. Completing a task, deferring delivery, or discovering an implementation limitation does not by itself change that commitment. Existing code and backlog history do not independently authorize additional scope.

Use this document to assess whether proposed work serves the product, which intended capabilities remain unmet, and whether simplification would remove an intended capability. Changes to existing public compatibility commitments require deliberate consideration under the charter; this document does not silently withdraw the guarantees recorded in the behavior reference.

Keep priorities and progress in planning records, current guarantees in the behavior reference, and exact acceptance scenarios in the acceptance criteria. Do not turn this document into a release checklist or implementation inventory.
