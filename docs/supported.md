# Supported capabilities

The intended product capabilities and boundaries of `jaytwo.Pdf` and `jaytwo.Ergonomics.Pdf`, whether or not each capability has been implemented.

This document is the durable scope commitment against which implementation, backlog items, and proposed changes are measured. An unfinished capability is a gap to close, not a reason to remove it from scope. Inclusion does not claim that a capability is available or production-qualified today.

> A useful durability test is: would changing this sentence change the product’s scope, or merely refine an operation’s contract? Scope belongs in supported.md; contract refinements belong in the spec.

The [Project Charter](charter.md) defines purpose, governing principles, and architecture. This document defines capability commitments and durable product boundaries. Adopted [operation specifications](spec/README.md) define precise normative contracts: required inputs, outcomes, preservation, authorization, limits, refusal, and failures. The [behavior reference](behavior.md) records what the implementation currently does, including its present limitations and guarantees. [Acceptance records](acceptance.md) map required outcomes to verification and record enforced test outcomes; they do not independently adopt product requirements. Delivery order, implementation status, and release milestones belong in planning records. The spec index records which operation contracts are adopted; forms remains an unadopted draft.

## Intended capabilities

| Area | Intended coverage |
| --- | --- |
| Document opening | Open synchronously or asynchronously from streams and stable random-access sources, with explicit ownership and authentication outcomes. |
| Document inspection | Read page count, page geometries, and rotation. |
| AcroForm inspection | Read field names, types, values, and widget geometries. |
| AcroForm population and export | Populate supported text, checkbox, radio, and choice fields, individually or in bulk; export form values. |
| AcroForm appearances | Generate text and choice-field appearances using caller-supplied TrueType fonts, embedded or subset as needed, for a documented character and script subset. Unsupported characters or shaping requirements must produce an explicit outcome. |
| AcroForm flattening | Flatten eligible editable fields into page content, subject to appearance, permission, and signature restrictions. |
| XFA-bearing forms | Classify hybrid and XFA-only forms and apply operation-specific acceptance, refusal, and removal rules, including supported use of hybrid forms' AcroForm representation. Removal must be an explicitly documented consequence. |
| PDF/A inputs | Accept PDF/A inputs under normal operation contracts and preserve relevant structures where practical. |
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
| Digital signing | Construct detached CMS and PAdES B-B/B-T signatures using host-controlled keys, with optional RFC 3161 signature timestamps, and append document timestamps. |
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

Implementation requires an adopted operation contract. Product-scope changes, architectural changes, and new full-document rewrite strategies require owner approval under the [charter](charter.md).

Foreground/background image placement controls painting order; it does not guarantee visibility over every existing page element.

Private signing keys remain under host control. Signature inspection reports structures and claims rather than validating signatures. Timestamp inclusion alone does not establish trust.

## Enduring non-goals

These are product boundaries, independent of implementation status. Changing them requires an explicit owner-approved scope change:

- Rendering and page-content text extraction.
- General document authoring and layout, apart from the limited image operations below.
- Redaction, cryptographic signature validation, and general PDF repair.
- PAdES B-LT/B-LTA construction. Separate document timestamps alone do not establish B-LTA conformance.
- XFA packet editing.
- JavaScript authoring, editing, or execution.
- PDF/A conversion, validation, and guarantees of creating or preserving conformance, including PDF/A-3. Attachment support and associated-file keys do not imply PDF/A-3 compliance.
- Public-key encryption and adding encryption to a file.
- General annotation editing and page-level file attachment annotations.
- Rewriting an input file in place. Incrementally appending to a source is a separate supported operation.

Limited blank-page creation and image placement for image-to-PDF conversion, image stamping, and image watermarking are the explicit image exception adopted in [D-68](../agents/decisions/D-68.md). General stamping or watermarking beyond that image capability is not included.

Necessary internal functionality for supported workflows does not create additional public capabilities.

## Resource and failure contracts

Supported operations require documented resource limits and explicit failure behavior. File size alone does not determine support; there is no guarantee that every document below a particular size can be processed.

Output guarantees must distinguish the operation, destination type, and point of failure. Detailed contracts must state whether a failure leaves the destination unchanged, permits partial output, or provides restoration or publication guarantees. Rollback and retry safety must be stated explicitly rather than inferred from a failed operation. Required limits and failure mechanics belong in adopted operation specifications. The behavior reference records current implementation guarantees and limitations; acceptance records link those requirements to verification and enforced outcomes. Drafting or adopting a contract must not silently weaken existing public compatibility commitments.

## Maintaining the scope commitment

Change this document deliberately when the project owner approves a change to intended product capabilities or material boundaries. Completing a task, deferring delivery, or discovering an implementation limitation does not by itself change that commitment. Existing code and backlog history do not independently authorize additional scope.

Use this document to assess whether proposed work serves the product, which intended capabilities remain unmet, and whether simplification would remove an intended capability. Changes to existing public compatibility commitments require deliberate consideration under the charter; this document does not silently withdraw the guarantees recorded in the behavior reference.

Keep priorities and progress in planning records, precise normative operation contracts in adopted specifications, current guarantees in the behavior reference, and verification scenarios and enforced outcomes in acceptance records. Do not turn this document into a release checklist or implementation inventory.
