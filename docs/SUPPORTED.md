# Supported capabilities

Product scope and delivery priorities for `jaytwo.Pdf` and `jaytwo.Ergonomics.Pdf`.

This inventory describes intended scope, not a claim that every capability is implemented or production-qualified. Priorities do not remove existing documented support or change existing behavior. New capabilities become current support after implementation and verification.

The [Project Charter](CHARTER.md) defines scope and principles. This document narrows the capability families into intended operations and delivery priorities. The [behavior reference](behavior.md) defines current conditions, preservation rules, limits, and failure guarantees, and separates planned additions from current support. [Acceptance criteria](../ACCEPTANCE.md) define the outcomes the current tests enforce.

This is the owner's intended documentation reset. Reconciling the older scope exclusions in `AGENTS.md` and adopting detailed specifications for additions remain separate work; this inventory does not authorize their implementation.

## MVP scope

| Area | Intended coverage | Current support |
| --- | --- | --- |
| Document inspection | Read page count, page geometries, and rotation. | Documented in the behavior reference. |
| AcroForm inspection | Read field names, types, values, and widget geometries. | Documented in the behavior reference. |
| AcroForm population | Populate supported text, checkbox, radio, and choice fields. | Documented, with field, permission, and appearance restrictions. |
| Page extraction | Create a new PDF from selected existing pages. | Documented, within the assembly preservation subset. |
| Concatenation | Combine existing PDFs into a new PDF. | Documented, within the assembly preservation subset. |
| Binary attachments | Read and add document-level embedded files. | Documented; replacement and removal also retain their current contracts. |
| User metadata | Read and update authorship information and custom key/value properties. | Engine Info keys and XMP are documented; workflow metadata covers its named properties. |
| Image placement | Place supported images on existing pages with position, size, rotation, opacity, and foreground/background placement, for image stamping and image watermarking. | Planned; no current public contract. |
| Image to PDF | Create a new PDF with specified page dimensions and place a supported image on the page. | Planned; no current public contract. |

### Planned image inputs and shortcuts

- JPEG, within documented format restrictions.
- PNG, within documented format restrictions, decoded within the library without a third-party image dependency.

The planned contract will reject unsupported image formats and variants. General image-format conversion is outside scope; callers may convert images before supplying them. JPEG and PNG support are targets, not current support claims.

Detailed behavior must define JPEG restrictions, PNG transparency and color-profile handling, resource limits, and placement coordinates relative to page rotation and cropping. RGB PNGs can declare transparency separately from an alpha channel; its treatment must be explicit.

Under this plan, `jaytwo.Pdf` will own the limited blank-page creation and image placement operations. `jaytwo.Ergonomics.Pdf` will compose them into image-to-PDF and selected-page watermarking shortcuts through the public engine API. Neither library becomes a general document-authoring or layout system.

## Lower priority

- Read security permissions (current); change security permissions (planned).
- Flatten editable fields (current, within the documented appearance and signature restrictions).
- Read and incrementally update supported encrypted/password-protected PDFs (current). Creating encrypted output or changing passwords is not current support.

These priorities guide future work; they do not withdraw current support. The behavior reference defines current security schemes and authentication requirements. Permission changes still need a detailed contract, including authorization and their effect on encryption and output strategy; reading permission bits does not imply the ability to change them.

## Lowest priority

- PDF signatures, including host-controlled signing and optional trusted timestamps (current, within the documented algorithms, formats, and provider requirements).

This priority does not remove existing signing, timestamping, or signature-inspection guarantees. Cryptographic signature validation remains outside scope; including a timestamp does not establish trust.

## Support boundaries

Support is operation-specific. Opening or inspecting a document does not establish that it can be populated, flattened, assembled, stamped, or signed. Permissions, certification, field locks, appearances, document structures, and resource limits can restrict individual operations.

Extraction and concatenation do not imply lossless preservation of every document feature. `behavior.md` defines supported structures and their preservation, omission, and refusal rules. Existing documented guarantees remain in force until explicitly changed.

Planned foreground/background image placement will control painting order; it will not guarantee visibility over every existing page element.

Rendering, page-content text extraction, redaction, cryptographic signature validation, general PDF repair, general annotation editing, and general document authoring and layout remain outside scope. Limited blank-page creation and image placement are planned for image-to-PDF conversion, image stamping, and image watermarking. Necessary internal support does not establish a general public feature.

Existing documented capabilities beyond the MVP retain their current contracts. Internal implementation alone does not establish a supported public feature.

## Maintenance

Keep planned scope distinguishable from implemented, verified support. Charter principles do not themselves promise an output strategy, supported PDF variation, or resource bound for every operation. Update detailed behavior and acceptance criteria when an approved change alters a public guarantee. Keep exact limits and test scenarios in those references; implementation and passing tests alone do not establish production qualification.
