# Project Charter

Build dependable .NET libraries that make common business operations on existing PDFs straightforward and production-ready. Support a deliberate scope well rather than become a general-purpose PDF toolkit.

This charter guides requirements, supported capabilities, and architectural decisions for both libraries. Detailed support, limitations, and acceptance criteria are defined separately; capability families do not promise comprehensive support for every PDF feature or variation.

## jaytwo.Pdf

Provide an independently useful PDF engine for developers who need direct, precise control over supported document operations, with explicit behavior and predictable resource use, without requiring developers to manipulate raw PDF objects or understand PDF serialization mechanics.

The engine owns PDF parsing, writing, encryption, forms, appearances, PDF signature construction, and document invariants. Cryptographic signing operations may be delegated to host-provided implementations. Its public API exposes supported operations rather than raw PDF objects.

## jaytwo.Ergonomics.Pdf

Make supported operations convenient to compose and execute through a small, discoverable API with sensible defaults, operation ordering, and file handling.

The workflow library uses the engine's public API and contains no PDF parsing, serialization, encryption, PDF signature construction, or raw object manipulation. The dependency runs one way: `jaytwo.Ergonomics.Pdf -> jaytwo.Pdf`.

## Product scope

Core capabilities are document inspection; interactive forms; metadata and embedded attachments; page operations and document assembly; password-protected PDFs; and digital signing with host-controlled keys and optional trusted timestamps.

Rendering, text extraction, document authoring and layout, redaction, cryptographic signature validation, and general-purpose PDF repair are outside the product scope unless explicitly adopted by the project owner. Limited blank-page creation and image placement for image-to-PDF conversion, stamping, and image watermarking are explicit exceptions. Internal functionality necessary to support approved workflows is permitted.

Existing implementations do not automatically establish product commitments. Capabilities outside the approved scope may be retained internally without becoming supported public features.

Simplification should preserve proven functionality and engineering knowledge unless removal provides a demonstrated benefit. Existing public compatibility commitments must be considered before removing or changing behavior.

## Architectural principles

1. **Managed .NET.** Both libraries are implemented entirely in managed .NET and introduce no native runtime dependencies. Host-provided cryptographic providers and platform services may be used through managed interfaces without becoming library dependencies.
2. **Self-contained implementation.** Neither library depends on third-party runtime packages. Microsoft framework compatibility packages are permitted where necessary for supported target frameworks. `jaytwo.Ergonomics.Pdf` depends on `jaytwo.Pdf`. Dependencies used only for development and testing are outside this restriction.
3. **Memory-efficient by design.** Favor streaming, random access, and bounded buffering. Avoid loading entire documents into memory when the operation does not inherently require it.
4. **Preserve existing documents.** Prefer incremental updates and preservation of original PDF bytes when modifying existing documents. Full rewrites are acceptable when required by the operation or correctness.
5. **Host-controlled cryptography.** Keep private signing keys under the host application's control. The libraries should integrate with supplied signing mechanisms rather than require custody of private key material.

Managed .NET and the runtime dependency restriction are hard constraints. The remaining principles guide engineering decisions; deviations are acceptable when justified by correctness, interoperability, security, or a demonstrated practical need, subject to architectural governance below.

## Production readiness

Supported workflows must produce correct, interoperable results, use predictable resources, and fail explicitly.

Operations should preserve unrelated document content and semantics to the extent compatible with the requested change. Material limitations must be documented.

Correctness must be demonstrated through meaningful tests and, where appropriate, independent tools, including independent verification of generated digital signatures.

## Compatibility and complexity

Prioritize compatibility with representative real-world business PDFs within the approved product scope.

Prefer proven interoperability over superficial implementation simplicity, but do not introduce substantial complexity solely to accommodate speculative or uncommon edge cases.

Significant trade-offs between compatibility, complexity, and maintainability require focused evidence and a project-owner decision. Routine compatibility decisions remain within engineering discretion.

## Governance

Engineers and agents may make implementation decisions consistent with the charter. Adding new full-document rewrite strategies requires explicit approval from the project owner, supported by a concise explanation of the need, alternatives, and trade-offs. Maintaining existing rewrite functionality does not require fresh approval.

The project owner approves changes to product scope or architectural direction. The charter governs the backlog; the backlog and implementation history do not independently authorize capabilities or commitments.

Change this charter deliberately when the project owner approves a change to the project’s purpose, governing principles, or architectural direction. Implementation difficulties, delivery priorities, and existing code do not by themselves change those commitments. Keep capability details in SUPPORTED.md, current behavior in behavior.md, and delivery plans in planning records.
