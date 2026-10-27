# Supported capabilities

The intended product capabilities and boundaries of `jaytwo.Pdf` and `jaytwo.Ergonomics.Pdf`, whether or not each capability has been implemented.

This document is the durable scope commitment against which implementation, backlog items, and proposed changes are measured. An unfinished capability is a gap to close, not a reason to remove it from scope. Inclusion does not claim that a capability is available or production-qualified today.

The [Project Charter](CHARTER.md) defines why the project exists and its governing principles. This document defines what belongs in the product. The [behavior reference](behavior.md) defines what the implementation currently does, including exact conditions, limitations, preservation rules, and failure guarantees. [Acceptance criteria](../ACCEPTANCE.md) define the outcomes the current tests enforce. Delivery order, implementation status, and release milestones belong in planning records.

## Intended capabilities

| Area | Intended coverage |
| --- | --- |
| Document opening | Open synchronously or asynchronously from streams and stable random-access sources, with explicit ownership and authentication outcomes. |
| Document inspection | Read page count, page geometries, and rotation. |
| AcroForm inspection | Read field names, types, values, and widget geometries. |
| AcroForm population and export | Populate supported text, checkbox, radio, and choice fields, individually or in bulk; export form values. |
| AcroForm flattening | Flatten eligible editable fields into page content, subject to appearance, permission, and signature restrictions. |
| XFA-bearing forms | Classify hybrid and XFA-only forms and apply operation-specific acceptance, refusal, and removal rules, including supported use of hybrid forms' AcroForm representation. XFA packet editing is excluded; removal must be an explicitly documented consequence. |
| PDF/A inputs | Accept PDF/A inputs under normal operation contracts and preserve relevant structures where practical. Conversion, validation, and guarantees of creating or preserving conformance are excluded, including PDF/A-3. |
| Page rotation | Change page rotation in multiples of 90 degrees. |
| Page extraction | Create a new PDF from selected pages of documents the operation contract supports, preserving the requested order and allowing repeated and reordered pages. Which document structures survive, are dropped, or cause refusal is set by that contract. |
| Concatenation | Combine selected pages from supported existing PDFs into a new PDF, preserving the requested input and page order and allowing repeated inputs and pages. Preservation and refusal rules are set by the operation contract. |
| Incremental saves | Save supported edits as incremental updates, preserving the original document bytes as a prefix. |
| Binary attachments | Add, replace, and remove document-level embedded files; provide streaming reads and extraction, with caller-controlled limits and filename safety helpers. |
| User metadata | Read and update document information, including authorship information and custom key/value properties, and XMP metadata. |
| Image placement | Place supported images on existing pages with position, size, rotation, opacity, and foreground/background placement, for image stamping and image watermarking. |
| Image to PDF | Create a new PDF with specified page dimensions and place a supported image on the page. |
| Password-protected PDFs | Open and modify supported password-protected PDFs under documented authentication and permission rules. |
| Security permissions | Read and change supported document security permissions with appropriate authorization. |
| Digital signing | Sign PDFs using host-controlled keys in detached CMS and PAdES B-B/B-T formats, with optional RFC 3161 signature timestamps; append document timestamps. |
| Signature and revision inspection | Inspect signatures and the claims they embed, and list document revisions, without cryptographic validation or trust assessment. The depth of CMS content and revision or coverage detail reported is set by the operation contract. |
| Workflow ergonomics | Compose supported operations with sensible defaults and operation ordering; provide form-export shortcuts and file publication. |

### Image inputs and shortcuts

Image inputs are JPEG and PNG, within explicitly documented format restrictions. PNG decoding belongs within the library without a third-party image dependency. Unsupported image formats and variants are rejected; general image-format conversion is outside scope, and callers may convert images before supplying them.

Detailed contracts must define JPEG restrictions, PNG transparency and color-profile handling, resource limits, and placement coordinates relative to page rotation and cropping. PNG transparency includes both alpha channels and separate transparent-color declarations; its treatment must be explicit.

`jaytwo.Pdf` owns limited blank-page creation and image placement. `jaytwo.Ergonomics.Pdf` composes those operations into image-to-PDF and selected-page watermarking shortcuts through the public engine API. Neither library becomes a general document-authoring or layout system.

## Support boundaries

Capability families do not promise comprehensive support for every PDF feature or variation. Support is operation-specific. Opening or inspecting a document does not establish that it can be populated, flattened, assembled, stamped, or signed. Permissions, certification, field locks, appearances, document structures, and resource limits can restrict individual operations.

Supported writes enforce applicable password-security permissions, certification restrictions (DocMDP), and field locks (FieldMDP), and apply documented Reader usage-rights preservation and removal rules. Enforcement does not establish signature validity or trust.

Extraction and concatenation do not imply lossless preservation of every document feature. Their detailed contracts must define supported structures and preservation, omission, and refusal rules.

Password and permission operations require explicit contracts for supported security schemes, authentication, authorization, encryption handling, and output strategy. Permission inspection alone does not satisfy the intended permission-changing capability. Get/set permissions does not itself adopt adding encryption or password changes/removal.

Implementation requires an adopted operation contract. Product-scope changes, architectural changes, and new full-document rewrite strategies require owner approval under the [charter](CHARTER.md).

Foreground/background image placement controls painting order; it does not guarantee visibility over every existing page element.

Private signing keys remain under host control. Signature inspection reports structures and claims rather than validating signatures. Timestamp inclusion alone does not establish trust.

## Enduring non-goals

These are product boundaries, independent of implementation status. Changing them requires an explicit owner-approved scope change:

- Rendering and page-content text extraction.
- General document authoring and layout, apart from the limited image operations below.
- Redaction, cryptographic signature validation, and general PDF repair.
- XFA packet editing.
- JavaScript authoring, editing, or execution.
- PDF/A conversion, validation, and guarantees of creating or preserving conformance, including PDF/A-3.
- Public-key encryption and adding encryption to a file.
- General annotation editing and page-level file attachment annotations.
- Rewriting an input file in place. Incrementally appending to a source is a separate supported operation.

Limited blank-page creation and image placement for image-to-PDF conversion, image stamping, and image watermarking are the explicit image exception adopted in [D-68](../agents/decisions/D-68.md). General stamping or watermarking beyond that image capability is not included.

XFA classification and operation-specific acceptance, refusal, and removal include supported use of hybrid forms' AcroForm representation. XFA packet editing remains excluded. The operation contract must explicitly document removal as a consequence wherever it occurs; current rules remain in the behavior reference.

PDF/A inputs are accepted under normal operation contracts, with relevant structures preserved where practical. This does not guarantee that every PDF/A input supports every operation or that output creates or preserves conformance. Attachment support and associated-file keys do not imply PDF/A-3 compliance. Necessary internal functionality for supported workflows does not create additional public capabilities.

## Resource and failure contracts

Supported operations require documented resource limits and explicit failure behavior. File size alone does not determine support; there is no guarantee that every document below a particular size can be processed.

Output guarantees must distinguish the operation, destination type, and point of failure. Detailed contracts must state whether a failure leaves the destination unchanged, permits partial output, or provides restoration or publication guarantees. Rollback and retry safety must be stated explicitly rather than inferred from a failed operation. Exact limits and failure mechanics belong in the behavior reference and its acceptance criteria.

## Maintaining the scope commitment

Change this document deliberately when the project owner approves a change to intended product capabilities or material boundaries. Completing a task, deferring delivery, or discovering an implementation limitation does not by itself change that commitment. Existing code and backlog history do not independently authorize additional scope.

Use this document to assess whether proposed work serves the product, which intended capabilities remain unmet, and whether simplification would remove an intended capability. Changes to existing public compatibility commitments require deliberate consideration under the charter; this document does not silently withdraw the guarantees recorded in the behavior reference.

Keep priorities and progress in planning records, current guarantees in the behavior reference, and exact acceptance scenarios in the acceptance criteria. Do not turn this document into a release checklist or implementation inventory.
