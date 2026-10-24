# Project Reorientation: jaytwo.Pdf and jaytwo.Ergonomics.Pdf

## Objective

Reorient the existing PDF library project around a clean separation between a production-grade PDF engine and an ergonomic, developer-focused API built on top of it.

The existing implementation should not be treated as an architectural constraint. Preserve useful code, tests, and lessons learned, but be willing to substantially refactor, replace, or remove existing abstractions.

**The goal is not to salvage the current design. The goal is to build the right library.**

This is an architectural reset, not necessarily a complete code rewrite.

## 1. Product Vision

Build two independently consumable .NET libraries:

### jaytwo.Pdf

A robust, self-contained, stream-native PDF processing engine.

It understands PDF structures, document revisions, forms, embedded files, signatures, and document composition.

It exposes a capable, well-designed API for developers who need direct control over PDF operations.

### jaytwo.Ergonomics.Pdf

A high-level, fluent API that makes common PDF operations simple, composable, predictable, and difficult to misuse.

It understands what developers want to accomplish without requiring them to understand PDF implementation details.

### Guiding distinction

**jaytwo.Pdf understands PDFs.**

**jaytwo.Ergonomics.Pdf understands what developers want to do with PDFs.**

The two libraries should have a strict, one-way dependency:

```text
jaytwo.Ergonomics.Pdf
          |
          v
      jaytwo.Pdf
          |
          v
     .NET Runtime
```

`jaytwo.Pdf` must be fully functional without Ergonomics.

Ergonomics must not implement its own PDF parser, writer, signing engine, or document object model.

Both packages should be independently consumable but developed in the same repository initially.

---

## 2. Design Philosophy

These libraries should follow the principles already established by the broader `jaytwo.Ergonomics` family, especially the Images library.

### 2.1 Describe intent rather than mechanics

A developer should express the outcome they want.

For example:

```csharp
await PdfWorkflow.From(input)
    .FillForm(fields => fields
        .Set("CustomerName", "Jane Doe")
        .Set("Accepted", true))
    .WithMetadata(metadata => metadata
        .WithTitle("Purchase Agreement"))
    .Sign(signature => signature
        .WithSigner(signer)
        .WithTimestamp(timestampAuthority))
    .SaveAsync(output, cancellationToken);
```

This is a proposed API illustrating the desired developer experience, not an existing contract.

The developer should not need to know about:

- Cross-reference tables or streams
- Indirect object references
- Appearance streams
- Incremental revision mechanics
- ByteRange calculation
- CMS serialization
- PDF object graph manipulation

These are responsibilities of the underlying engine.

### 2.2 Make simple things simple

Common operations should require very little ceremony.

Advanced options should remain available without complicating common workflows.

Avoid configuration objects containing dozens of options that must be understood before performing a basic operation.

Provide sensible defaults and explicit overrides.

### 2.3 Preserve existing document content

PDFs are structured documents, not merely rendered page images.

The preferred behavior is to preserve existing bytes and objects wherever possible.

Avoid unnecessary:

- Rasterization
- Recompression
- Rewriting
- Normalization
- Loss of metadata
- Destruction of existing document structure

Use incremental updates where appropriate.

Full document rewriting should be reserved for operations that require it.

### 2.4 Stream-native by design

Large PDFs must not require whole-file buffering in memory or temporary files.

Support:

- Seekable streams
- Bounded random-access reads
- Asynchronous I/O
- Cancellation
- Incremental writing
- Object-store-backed sources through adapters
- Large document processing with predictable memory consumption

Memory consumption should depend primarily on the complexity of the operation rather than the total document size.

Do not claim that every operation can be performed in a single forward-only pass. PDF parsing, merging, and signing have distinct random-access and output requirements.

### 2.5 Prefer composability

Developers should be able to combine operations naturally.

For example:

```csharp
await PdfWorkflow.From(template)
    .FillForm(values)
    .Attach("order.json", attachment)
    .WithMetadata(metadata)
    .Sign(signingOptions)
    .SaveAsync(output);
```

The workflow layer should handle operation planning and enforce safe execution ordering.

In particular, document modifications should ordinarily precede signing.

### 2.6 Maintain a small, discoverable API

Avoid exposing PDF internals merely because they exist.

The core engine may expose detailed PDF concepts where necessary, but Ergonomics should present a task-oriented interface.

Avoid excessive interface proliferation, unnecessary abstraction layers, and dependency injection requirements for ordinary usage.

### 2.7 Production correctness over feature count

A smaller set of interoperable, thoroughly tested capabilities is preferable to broad but unreliable PDF support.

Never silently emit malformed PDFs, invalidate signatures, ignore unsupported operations, or misrepresent validation guarantees.

---

## 3. Runtime Dependency Policy

The core implementation should remain self-contained.

The project's zero-external-dependencies principle means:

- No iText
- No Aspose
- No commercial PDF SDKs
- No AGPL PDF engines
- No substantive third-party PDF parsing or writing libraries
- No required third-party cryptographic implementation libraries

The library should implement its own PDF-specific parsing, editing, serialization, and signing infrastructure.

Standard .NET runtime capabilities are permitted.

Lightweight platform compatibility packages, such as `System.Memory` for older target frameworks, are acceptable.

Test-only dependencies are permitted, including independent PDF producers, validators, signing tools, and interoperability utilities.

Optional integration packages may introduce additional dependencies when explicitly justified, but they must not compromise the self-contained core.

Do not confuse zero substantive runtime dependencies with a literal prohibition on every NuGet reference.

---

## 4. Package Responsibilities

### 4.1 jaytwo.Pdf — Core Engine

Responsible for:

**PDF parsing and document structure**
- PDF headers and trailers
- Cross-reference tables and streams
- Indirect objects and references
- Object streams
- Stream filters and decoding
- Document catalogs and page trees
- Incremental revisions
- Structural validation
- Robust malformed-input handling

**Document inspection**
- Page count
- Page dimensions and rotation
- Metadata
- Form fields
- Field values and types
- Widget annotations
- Page-relative field geometry
- Embedded files
- Existing signatures
- Signature dictionaries and ByteRanges

**Document modification**
- AcroForm value updates
- Appearance stream handling
- Supported form flattening
- Document information dictionary updates
- XMP metadata where supported
- Embedded-file insertion and removal
- Incremental revision writing

**Digital signing**
- Signature field handling
- Signature dictionaries
- ByteRange calculation
- CMS signature construction
- X.509 certificate handling
- External signing abstractions
- PKCS#11 integration through an appropriate signer/provider boundary
- RFC 3161 timestamp integration
- Multiple-signature support where permitted
- Signature verification as a separately defined capability
- Eventually long-term-validation support

**Document assembly**
- Page importing
- Object reference remapping
- Resource handling
- Page selection
- Document concatenation
- Form and annotation conflict handling
- Supported document normalization

**I/O infrastructure**
- Random-access PDF source abstraction
- Seekable-stream adapters
- Bounded read caches
- Incremental writers
- Asynchronous operations
- Cancellation and deterministic resource ownership

The core should expose useful semantic abstractions such as `PdfDocument`, `PdfPage`, `PdfFormField`, `PdfWidget`, and `PdfSignature`.

It must not require consumers to manipulate raw dictionaries for ordinary operations.

However, lower-level structures should be accessible through deliberate APIs when needed.

### 4.2 jaytwo.Ergonomics.Pdf — Workflow Layer

Responsible for:

- Fluent operations
- High-level transformations
- Operation composition
- Presets and sensible defaults
- Option normalization
- Workflow validation
- Execution ordering
- Common task shortcuts
- Friendly error reporting
- Convenient stream and file overloads

Representative capabilities:

```csharp
PdfWorkflow.From(input)
    .FillForm(...)
    .Attach(...)
    .WithMetadata(...)
    .Sign(...)
    .SaveAsync(...);
```

```csharp
await PdfWorkflow.Combine(
        cover,
        agreement,
        exhibits)
    .SaveAsync(output);
```

```csharp
var fields = await PdfForm.ReadFieldsAsync(input);
```

Exact API names are open for design. Prioritize consistency, discoverability, and idiomatic .NET conventions over preserving these examples verbatim.

Ergonomics should never duplicate implementation logic that belongs in the core.

---

## 5. Core Document and Editing Model

The most important architectural decision is how documents are inspected, modified, and serialized.

Prefer explicit separation between document inspection and editing.

Conceptually:

```csharp
await using var document =
    await PdfDocument.OpenAsync(input, cancellationToken);

// Inspection
var pages = document.Pages;
var fields = document.AcroForm.Fields;
var signatures = document.Signatures;
```

For direct editing:

```csharp
await using var document =
    await PdfDocument.OpenAsync(input, cancellationToken);

await using var editor = document.Edit();

editor.Forms.SetValue("CustomerName", "Jane Doe");
editor.Metadata.Title = "Purchase Agreement";

await editor.SaveAsync(output, cancellationToken);
```

For Ergonomics:

```csharp
await PdfWorkflow.From(input)
    .FillForm(fields => fields
        .Set("CustomerName", "Jane Doe"))
    .WithMetadata(metadata => metadata
        .WithTitle("Purchase Agreement"))
    .SaveAsync(output, cancellationToken);
```

These examples illustrate the desired conceptual separation.

The core editor should own:

- Mutation tracking
- Document consistency
- PDF-specific invariants
- Incremental revision generation
- Signature-preservation rules
- Serialization

The Ergonomics layer should own:

- Intent-oriented operation descriptions
- Workflow-level validation
- Default configuration
- Operation ordering
- Convenience and composition

Avoid two independent mutation engines.

The Ergonomics workflow should execute through the core's editing model.

### Deferred execution

Prefer deferred execution in the Ergonomics layer.

Operations such as `FillForm()` and `Attach()` should describe modifications without immediately rewriting the PDF.

`SaveAsync()` should compile and execute the workflow.

This allows the implementation to:

- Group compatible modifications
- Minimize unnecessary revisions
- Choose incremental versus full rewriting
- Validate incompatible operations
- Order signing correctly
- Manage resources consistently

Do not introduce deferred execution into the core unless it materially improves core semantics.

---

## 6. Digital Signing Architecture

Digital signing is a foundational capability, not an afterthought.

The design must support certificates, hardware security modules, trusted timestamps, and external signing services.

### Separate PDF signing from cryptographic signing

The PDF engine owns:

- Signature fields
- Signature dictionaries
- ByteRange preparation
- Signed-content hashing
- CMS/PAdES-specific PDF integration
- Signature embedding
- Incremental revisions
- PDF signing rules

Cryptographic providers own:

- Private-key operations
- Key selection
- HSM session management
- Remote signing calls

A signer abstraction should allow implementations backed by:

- .NET cryptographic APIs
- PKCS#11 hardware security modules
- Remote signing services
- Custom signing implementations

Avoid coupling the PDF engine directly to a particular HSM or PKCS#11 vendor.

Avoid making timestamp authorities mandatory.

### Signing correctness

The system must:

- Preserve signed bytes correctly
- Generate accurate ByteRanges
- Reject unsupported or unsafe post-signature changes
- Support multiple signatures where PDF semantics permit them
- Distinguish approval signatures from certification signatures
- Handle timestamp failures explicitly
- Generate externally verifiable CMS signatures
- Never treat successful serialization as proof of cryptographic validity

Signing should normally occur after document modifications.

Signature verification and trust evaluation should be distinct operations with clearly documented guarantees.

---

## 7. Stream and Storage Architecture

The core must support bounded random-access I/O.

Consider an abstraction similar to:

```csharp
public interface IPdfSource
{
    ValueTask<long> GetLengthAsync(
        CancellationToken cancellationToken = default);

    ValueTask<int> ReadAsync(
        long offset,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default);
}
```

The exact contract should account for partial reads, source consistency, concurrency, and lifetime management.

Potential source implementations:

- Seekable file streams
- Memory streams
- Custom random-access sources
- S3 range-backed sources
- Azure Blob-backed sources

Object storage integrations may live in optional packages.

The core must not depend on AWS or Azure SDKs.

### Performance objectives

- Avoid whole-document buffering.
- Avoid temporary disk storage where not necessary.
- Support large documents, including PDFs with hundreds or thousands of pages.
- Use bounded caches.
- Minimize repeated random-access requests.
- Prefer incremental updates when appropriate.
- Permit copying unchanged source bytes to the output.
- Support efficient sealing of PDFs stored in object storage.

For signing workflows, explicitly account for the need to compute the digest over the final ByteRange and produce valid output.

Do not pretend that a signature can always be calculated and published through an arbitrary single-pass stream.

---

## 8. Document Assembly and the PDF Stapler

Document assembly is a desired capability, but it is architecturally distinct from incremental editing.

Target an API similar to:

```csharp
await PdfWorkflow.Combine(
        coverPage,
        agreement,
        exhibits)
    .SaveAsync(output);
```

And eventually:

```csharp
await PdfWorkflow.Compose()
    .Add(coverPage)
    .Add(agreement, pages => pages.Range(1, 5))
    .Add(exhibits)
    .SaveAsync(output);
```

The core must correctly handle imported page object graphs and indirect references.

Do not assume that flattening AcroForms alone makes arbitrary PDF merging trivial.

Establish explicit behavior for:

- Existing signatures
- AcroForm fields
- Annotations
- Bookmarks and outlines
- Page resources
- Metadata
- Embedded files
- Duplicate field names
- Unsupported document structures

A combined PDF is generally a new document. Do not imply that input digital signatures remain cryptographically valid on the combined output.

The stapler should be implemented after the foundational parser, document model, and writer are sufficiently mature.

---

## 9. Content Placement Is Not a Core Priority

Do not allow the PDF project to drift into becoming a full graphics or layout engine.

General-purpose stamping and watermarking are outside the initial project scope.

Potential future capabilities include:

- Inserting supported raster images
- Embedding compatible single-page PDF content
- Preserving vector content
- Positioning visual signature appearances
- Integrating with `jaytwo.Ergonomics.Images`

These should be considered optional extensions rather than prerequisites for the core engine.

Do not introduce a general rendering engine merely to support PDF operations that can be performed structurally.

Raw PostScript interpretation is not a goal.

Illustrator support, if introduced, should be limited to compatible embedded PDF representations rather than a full Illustrator parser.

---

## 10. API Design Standards

The following standards apply to both packages.

### Async-first I/O

Provide asynchronous I/O where operations involve streams, files, remote services, or cryptographic providers.

Support `CancellationToken` consistently.

Synchronous operations may be available when genuinely appropriate, but must not conceal blocking remote I/O.

### Explicit ownership

Document whether APIs own or borrow input/output streams.

Do not unexpectedly dispose caller-owned resources.

Prefer deterministic disposal for documents, editing sessions, and underlying resources.

### Predictable failure behavior

Unsupported operations must fail explicitly.

Do not silently:

- Skip fields
- Drop attachments
- Rewrite or flatten content unexpectedly
- Invalidate signatures
- Ignore timestamp failures
- Omit unsupported PDF structures
- Produce partially successful results without reporting them

### Strong types where valuable

Use meaningful models for:

- Page indices and page numbers
- Rectangle geometry
- PDF field types
- Signature configurations
- Validation results
- Revision identifiers
- Metadata
- Document permissions

Avoid adding strong types merely for ceremony.

### Avoid over-abstraction

Do not introduce interfaces for every class.

Prefer a focused public API and implementation details hidden behind internal types.

Interfaces should represent meaningful extension points, particularly:

- PDF sources
- Cryptographic signers
- Timestamp authorities
- Optional storage adapters

### Discoverable defaults

Developers should be able to use the common case without configuring PDF implementation details.

Advanced behavior must remain available through explicit options.

---

## 11. Scope and Delivery Priorities

### Phase 1 — Foundational Core

Establish:

- Package separation
- Public API direction
- Parser and document model
- Random-access source abstraction
- Cross-reference and indirect object support
- Incremental writer
- Document inspection
- Metadata handling
- AcroForm field discovery
- Field geometry extraction
- Form filling
- Embedded-file operations
- Bounded resource consumption

This phase establishes the foundation for all other capabilities.

### Phase 2 — Signing and Sealing

Implement and validate:

- CMS signing
- External signer abstraction
- PKCS#11 integration
- RFC 3161 timestamping
- Signature fields
- ByteRange construction
- Incremental signature insertion
- Signature inspection
- Multiple-signature handling where permitted
- Interoperability with independent signature validators

### Phase 3 — Document Assembly

Implement:

- Page import
- Object graph remapping
- PDF concatenation
- Page selection and reordering
- Form and annotation handling
- Resource conflict resolution
- Explicit signature behavior

### Phase 4 — Advanced Capabilities

Evaluate:

- Full signature verification
- Certificate trust and revocation validation
- Long-term-validation data
- PAdES-related profiles
- Advanced form flattening
- Archival-oriented capabilities
- Image and vector content placement
- Optional cloud-storage adapters

These are priorities, not an instruction to postpone testing until the end of a phase.

Each capability must be tested for correctness as it is developed.

---

## 12. Quality and Interoperability Requirements

The PDF engine must be independently testable and should be held to a significantly higher correctness standard than the Ergonomics wrapper.

### Core testing

Test:

- PDFs from multiple independent producers
- Classic cross-reference tables
- Cross-reference streams
- Object streams
- Hybrid-reference files
- Incrementally updated documents
- Large PDFs
- Malformed and adversarial inputs
- Missing or corrupted references
- Field appearance generation
- Embedded-file integrity
- Metadata preservation
- Signature correctness
- Memory and resource limits
- Cancellation behavior

### Signing interoperability

Use independent tools to:

- Generate test documents
- Produce reference signatures
- Verify signatures emitted by the library
- Inspect revisions and ByteRanges
- Validate timestamp tokens
- Confirm attachment integrity

Do not rely exclusively on the library validating its own output.

### Ergonomics testing

Test:

- Fluent API behavior
- Default values
- Operation composition
- Option precedence
- Execution ordering
- Invalid workflow rejection
- Error propagation
- Core API interoperability

### Security and resource limits

Treat PDFs as untrusted input.

Implement explicit limits for:

- Object counts
- Nesting depth
- Decoded stream sizes
- Cross-reference structures
- Memory allocations
- Recursive references
- Decompression expansion
- Resource consumption

Do not allow malformed documents to create unbounded memory usage or infinite parsing loops.

---

## 13. Existing Code Migration Strategy

The current implementation is not the source of architectural truth.

Before further feature development, conduct an assessment of the existing codebase.

### Step 1 — Inventory

Identify:

- Existing public APIs
- Existing parser capabilities
- Existing writer capabilities
- AcroForm implementation
- Signing and CMS implementation
- PKCS#11 abstractions
- Timestamping implementation
- Attachment support
- Metadata support
- Test coverage
- Runtime dependencies
- Architectural coupling

### Step 2 — Classify

Classify major components into:

**Retain** — Correct, well-tested, and consistent with the new architecture.

**Refactor** — Valuable implementation with problematic API or dependency boundaries.

**Replace** — Functionally useful but architecturally unsuitable or insufficiently reliable.

**Remove** — Out of scope, redundant, or unnecessary.

Do not automatically preserve abstractions just because implementation effort has already been invested.

### Step 3 — Establish the core boundary

Move PDF-specific implementation responsibilities into `jaytwo.Pdf`.

Ensure it has no dependency on `jaytwo.Ergonomics.Pdf`.

Establish the public core document and editing API.

### Step 4 — Build the ergonomic layer

Implement `jaytwo.Ergonomics.Pdf` as a thin, high-value workflow layer over the core.

Do not simply duplicate the entire core API with fluent method names.

Each Ergonomics feature should provide meaningful convenience, composition, defaults, or safeguards.

### Step 5 — Preserve and expand tests

Keep valid existing tests wherever possible.

Replace tests that encode obsolete implementation assumptions while preserving their behavioral intent.

Build integration tests against independent PDF tooling.

### Step 6 — Resume feature development

Only after the architectural direction is established should substantial new feature development continue.

Avoid adding new features to architecture already scheduled for replacement.

---

## 14. What Not to Build

This project is not intended to be:

- An Adobe Acrobat replacement
- A PDF rendering engine
- A desktop PDF editor
- A general graphics or page-layout system
- An HTML-to-PDF renderer
- A PostScript interpreter
- A wrapper around another PDF SDK
- A collection of dozens of narrowly divided NuGet packages

The core objective is reliable programmatic manipulation of existing PDFs.

Do not pursue broad specification coverage merely for completeness when it does not support a concrete use case.

At the same time, foundational parsing and document integrity cannot be sacrificed simply because the public API exposes a narrower feature set.

---

## 15. Success Criteria

The reoriented project is successful when:

1. `jaytwo.Pdf` can be installed and used independently.
2. `jaytwo.Ergonomics.Pdf` provides genuinely simpler workflows rather than duplicating the core API.
3. Ordinary form filling, attachment handling, metadata modification, and signing require minimal developer code.
4. The core supports large PDFs without unnecessary whole-file buffering.
5. Incremental updates preserve existing document bytes and structure where appropriate.
6. PDF signatures can be independently verified.
7. Unsupported inputs and operations fail safely and predictably.
8. The implementation maintains the project's runtime dependency policy.
9. Additional features can be developed without substantially changing the fundamental architecture.
10. The libraries remain straightforward for developers to understand, test, and maintain.

---

## 16. Immediate Instructions for the Development Team

Treat this document as the new architectural charter.

**Do not immediately start implementing new features.**

First:

1. Review the current repository and identify architectural mismatches with this charter.
2. Propose the new solution, project, namespace, and dependency structure.
3. Define the intended public API for `jaytwo.Pdf` and `jaytwo.Ergonomics.Pdf`.
4. Identify existing components that can be retained, refactored, replaced, or removed.
5. Identify technical risks and design decisions that require resolution.
6. Produce a phased migration plan that preserves valuable tests and working implementations.
7. Highlight any proposed deviations from this charter and explain the tradeoffs.

The architectural review should be substantive, not merely a recommendation to rename namespaces or introduce another wrapper project.

Challenge existing abstractions wherever they conflict with the new design.

Avoid treating the existing implementation as a finished product that must be preserved.

Also avoid unnecessary rewrites when a component already satisfies the intended design.

The goal is a coherent, production-grade PDF processing foundation with a genuinely ergonomic developer experience.

**Optimize for the architecture we want to maintain over the next several years, not for minimizing disruption to the code that exists today.**

### Final guiding principle

> `jaytwo.Pdf` should make correct PDF processing possible, reliable, and extensible.
>
> `jaytwo.Ergonomics.Pdf` should make correct PDF processing easy, intuitive, and composable.
>
> The implementation complexity belongs in the engine. The developer experience belongs in Ergonomics. Neither should compromise the other.
