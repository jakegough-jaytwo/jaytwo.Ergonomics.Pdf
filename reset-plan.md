# Architectural Reset Plan

Status: owner-approved migration direction, 2026-10-23.

This plan applies the product direction in [`reset.md`](reset.md) to the repository as it exists today. It preserves the implementation, tests, interoperability evidence, and operational lessons that are already valuable while replacing the package boundary and public API model that no longer match the intended product.

This plan does not silently override `AGENTS.md`, the adopted specifications, `QUALIFICATION.md`, or recorded decisions. The first reset work item proposes the exact changes those protected sources need. Code migration starts only after the affected authority documents agree.

## 1. Decisions

The owner made these decisions during the reset interview on 2026-10-23:

1. Use a clean pre-1.0 break. Do not preserve the current API through a compatibility facade and do not run old and new public APIs side by side.
2. Add `jaytwo.Pdf` as a new project in this solution now. It is the PDF engine boundary, but it is not a NuGet package yet.
3. `jaytwo.Ergonomics.Pdf` depends on `jaytwo.Pdf`. `jaytwo.Pdf` never depends on Ergonomics.
4. Move the existing signing implementation and its tests into `jaytwo.Pdf` during the boundary reset without redesigning or extending it. Signing remains usable.
5. Resume signing work only after document assembly. Until then, signing changes are limited to regressions or migration breakage.
6. Establish a public random-access source abstraction, async reads, cancellation, and explicit resource ownership in the engine foundation phase. Cloud storage adapters are deferred.
7. Separate inspection from editing: `PdfDocument` is read-only, `PdfDocument.Edit()` creates a `PdfDocumentEditor`, and save and signing are editor operations.
8. Build Ergonomics as a deferred, intent-oriented workflow over one core editor session. Do not mirror the core API with fluent names.
9. Keep raw PDF dictionaries, tokens, indirect objects, parsers, and serializers internal. Reconsider an expert/raw API only for concrete use cases.
10. Split tests by ownership. Engine behavior and interoperability belong to `jaytwo.Pdf.Tests`; Ergonomics tests exercise only its public workflow behavior and the public engine API.
11. Suspend the current 1.0 NuGet release track. `jaytwo.Pdf` is explicitly non-packable during the reset, and packaging and versioning for both libraries are a later milestone.
12. Put document assembly before further signing work.

## 2. Reconciliation with the current repository

The current repository is not discarded. Its principal problem is architectural placement, not an absence of capable PDF code.

### 2.1 Retain

Retain the behavior and evidence for:

- sparse parsing of classic cross-reference tables, cross-reference streams, hybrid references, object streams, incremental revisions, and supported stream filters;
- incremental writing, exact-prefix preservation, rollback on failure, bounded copying, long offsets, and existing resource limits;
- password security revisions 2 through 6, permissions, encryption on write, and signature-aware modification policy;
- page inspection and rotation;
- AcroForm discovery, geometry, value export, single and bulk fill, generated appearances, XFA classification, and flattening;
- document information and XMP metadata;
- document-level attachments, relationships, checksums, dates, and compression;
- signature inspection, CMS construction, RSA and ECDSA signing, PAdES B-B/B-T, RFC 3161 timestamps, document timestamps, and external signer boundaries;
- revision inspection;
- the corpus, regression suite, fuzz harness, independent-tool checks, large-file workloads, output-identity checks, and failure-path tests.

These capabilities move to `jaytwo.Pdf`; they are not reimplemented in Ergonomics.

### 2.2 Refactor

Refactor these boundaries while preserving behavior:

- the sole production project becomes two projects;
- the current direct `PdfDocument` API becomes the starting material for the engine API rather than the Ergonomics API;
- direct mutation through an opened document becomes an explicit editor session;
- the stream-only input boundary becomes a random-access source contract with a seekable-stream adapter;
- asynchronous behavior expands from save and signing to opening and source reads;
- the monolithic internal engine is separated only where the new project, source, document/editor, and operation boundaries require it;
- namespaces move from `jaytwo.Ergonomics.Pdf` to `jaytwo.Pdf` for engine-owned concepts;
- tests and test utilities move to the project that owns the behavior they exercise.

### 2.3 Add

Add:

- the `jaytwo.Pdf` project and namespace;
- the `IPdfSource` contract and stream adapter;
- read-only document and explicit editor APIs;
- a deferred `PdfWorkflow` API in Ergonomics;
- boundary tests that prevent reverse dependencies and PDF implementation logic in Ergonomics;
- a new-file writer and page-import model for document assembly.

### 2.4 Defer

Defer:

- publishing `jaytwo.Pdf` as a NuGet package;
- new signing features, signing API expansion, dedicated signing qualification expansion, and new signing hardening campaigns until after assembly;
- raw object-model access;
- S3, Azure Blob, and other storage-specific adapters;
- compaction, removing revisions, and removing passwords until after the focused assembly phase;
- cryptographic signature verification, trust, revocation, and long-term-validation support;
- general content placement and any work resembling a renderer or layout engine.

## 3. Target solution structure

```text
jaytwo.Ergonomics.Pdf.sln
|
+-- src/
|   +-- jaytwo.Pdf/
|   |   +-- jaytwo.Pdf.csproj          # engine; IsPackable=false for now
|   |   +-- public semantic API
|   |   +-- internal parser, writer, object model, security, CMS
|   |
|   +-- jaytwo.Ergonomics.Pdf/
|       +-- jaytwo.Ergonomics.Pdf.csproj
|       +-- project reference to jaytwo.Pdf
|       +-- workflows, defaults, ordering, and convenience overloads
|
+-- test/
|   +-- jaytwo.Pdf.Tests/
|   +-- jaytwo.Ergonomics.Pdf.Tests/
|   +-- framework compatibility lanes as needed
|
+-- tools/
    +-- fuzz/                           # targets jaytwo.Pdf
```

The dependency rule is:

```text
jaytwo.Ergonomics.Pdf -> jaytwo.Pdf -> .NET runtime
```

No source file in Ergonomics parses, serializes, encrypts, signs, or directly manipulates PDF objects. Ergonomics may describe operations, validate workflow-level conflicts, choose defaults, order operations, and translate core failures into task-oriented errors.

## 4. Target API direction

Exact names remain subject to focused API design, but the responsibility split is settled.

### 4.1 Engine

```csharp
await using PdfDocument document =
    await PdfDocument.OpenAsync(source, options, cancellationToken);

IReadOnlyList<PdfPage> pages = document.Pages;
IReadOnlyList<PdfFormField> fields = document.AcroForm.Fields;
IReadOnlyList<PdfSignature> signatures = document.Signatures;

await using PdfDocumentEditor editor = document.Edit();
editor.Forms.SetValue("CustomerName", "Jane Doe");
editor.Metadata.Title = "Purchase Agreement";
await editor.SaveAsync(output, cancellationToken);
```

The engine owns document invariants, mutation tracking, incremental and new-file writing, permissions, certification rules, signature-preservation rules, encryption, serialization, and rollback.

### 4.2 Ergonomics

```csharp
await PdfWorkflow.From(input)
    .FillForm(fields => fields.Set("CustomerName", "Jane Doe"))
    .WithMetadata(metadata => metadata.WithTitle("Purchase Agreement"))
    .Attach("order.json", attachment)
    .SaveAsync(output, cancellationToken);
```

Workflow methods describe intent. `SaveAsync` opens the source, validates and orders the operations, creates one editor session, applies compatible operations, and writes the result. Signing is added to this workflow only in Phase 4.

## 5. Migration rules

1. Preserve behavior before improving it. A move or namespace change keeps the existing tests and output-identity fixtures green.
2. Do not duplicate the parser, object model, writer, security, CMS, form, or appearance implementation across projects.
3. Keep each checkpoint buildable and testable. Do not leave both projects compiling separate copies of the engine.
4. Make breaking changes directly; do not add obsolete shims for the pre-reset API.
5. Use the existing tests as executable behavior records. Change a test only when the approved new API requires it or its old expectation conflicts with the new charter.
6. Do not combine architectural movement with opportunistic algorithm rewrites.
7. A regression found during migration is fixed with a permanent test before the migration item closes.
8. Signing output and independent-verifier results must remain unchanged during the move unless a confirmed defect requires a separately approved behavior change.
9. The current qualification evidence is historical input, not automatically fresh evidence for the new project and API revisions.
10. No package is released while the dependency graph contains an unpublished engine package.

## 6. Phase 0: align authority and stop the old release plan

### Outcome

The repository's binding instructions describe the reset consistently before structural code work begins.

### Work

1. Propose exact updates to `AGENTS.md`, `docs/spec/`, `QUALIFICATION.md`, and affected skills and decisions for owner approval.
2. Replace the one-library product charter with the two-library responsibility split.
3. Define the new phase numbering and distinguish migrated existing capability from new phase work.
4. Suspend the current 1.0 release and package qualification track.
5. Replace `agents/state/current.md` with the reset work-item sequence after the owner approves the records.
6. Mark old work items as retained, deferred, superseded, or cancelled without rewriting frozen closeouts.

### Exit criteria

- No binding source contradicts the two-project dependency direction, clean break, editor model, source abstraction, revised phase order, or release pause.
- The reset has a small ordered set of work items with exact `allowed_changes`.
- Feature work does not continue against architecture scheduled to move.

## 7. Phase 1: engine foundation

### Outcome

`jaytwo.Pdf` is the only PDF engine in the solution. All current capabilities, including signing, run through it. The core has its intended document/editor and source boundaries.

### Ordered work

1. **Create and enforce the project boundary.** Add `src/jaytwo.Pdf/jaytwo.Pdf.csproj`, mark it non-packable, add it to the solution, and make Ergonomics reference it. Move engine implementation and engine-owned public types into the project and `jaytwo.Pdf` namespaces. Leave no reverse reference.
2. **Move engine tests.** Establish `test/jaytwo.Pdf.Tests`. Move parser, writer, forms, metadata, attachments, encryption, revisions, signing, corpus, interop, scale, output-identity, and hardening tests. Point the fuzz project at `jaytwo.Pdf`.
3. **Separate inspection and editing.** Make `PdfDocument` read-only. Introduce `PdfDocumentEditor`, move all mutations and save/sign operations to it, and preserve all-or-nothing validation and rollback behavior.
4. **Establish random-access I/O.** Introduce `IPdfSource`, its seekable-stream adapter, async open and read paths, cancellation, partial-read rules, consistency requirements, and explicit ownership. Keep synchronous stream conveniences where they are honest and useful.
5. **Stabilize semantic core APIs.** Retain strong page, geometry, field, widget, attachment, metadata, revision, permission, and signature models. Keep the raw object model internal.
6. **Resolve foundational compatibility defects.** Do W-117 and W-99 in the engine context rather than in the old architecture.
7. **Re-establish the fast verification baseline.** Build every target and run focused plus proportionate regression tests. Re-record output identity where a namespace or API-only change cannot affect bytes; investigate any byte change rather than accepting it mechanically.

### Signing rule during Phase 1

Move signing code and tests as-is. Changes required for namespaces, project references, the document/editor split, source access, or an observed regression are allowed. New formats, algorithms, validation features, workflow design, verifier expansion, and dedicated hardening work wait for Phase 4.

### Exit criteria

- `jaytwo.Pdf` builds on every supported target and has no reference to Ergonomics.
- Ergonomics contains no parser, writer, object model, encryption, CMS, appearance, or PDF mutation implementation.
- Engine tests and fuzz targets compile against `jaytwo.Pdf`.
- Existing shipped behaviors pass their focused tests, and representative output-identity and independent-tool checks still pass.
- `PdfDocument` is inspection-only and `PdfDocumentEditor` owns changes and output.
- Stream input works through `IPdfSource`; async opening and reads observe cancellation and bounded-memory rules.
- W-117 and W-99 have their approved dispositions.

## 8. Phase 2: ergonomic workflows

### Outcome

`jaytwo.Ergonomics.Pdf` provides meaningfully simpler, deferred workflows for common non-signing tasks without duplicating the engine API.

### Scope

- create `PdfWorkflow.From(...)` and clear input/output ownership rules;
- describe operations without opening or rewriting the file immediately;
- fill and export forms, flatten, update metadata, add or remove attachments, and rotate pages;
- offer stream and file conveniences, with cancellation on I/O operations;
- normalize options and defaults;
- reject incompatible workflows before output begins;
- order operations and execute them through one `PdfDocumentEditor` session;
- provide friendly errors without hiding the underlying failure category;
- add read shortcuts only where they reduce real ceremony.

Signing is intentionally absent from the workflow in this phase. Callers that need the already-supported signing behavior use `jaytwo.Pdf` directly until Phase 4.

### Testing

Ergonomics tests cover intent capture, defaults, option precedence, composition, ordering, invalid-workflow rejection, file and stream ownership, cancellation, and parity with direct core operations. They do not use engine internals.

W-80 belongs at the end of this phase so fill and flatten rendering is checked through the stable engine and workflow boundaries.

### Exit criteria

- Common non-signing workflows are shorter than their direct-core equivalents and add real defaults or safeguards.
- Every workflow executes exclusively through public `jaytwo.Pdf` APIs.
- Multiple compatible operations produce the intended minimal set of revisions.
- Invalid workflows fail before changing the destination.
- Poppler and MuPDF cover the agreed fill and flatten rendering cases.

## 9. Phase 3: document assembly

### Outcome

The engine can create a new PDF from selected pages of existing PDFs, and Ergonomics exposes a simple stapling workflow.

### Scope

- new-file writer foundation;
- page import and reachable-object remapping;
- page extraction;
- concatenation;
- page selection and reordering;
- resource, annotation, form-field, metadata, outline, attachment, and name-conflict rules;
- explicit treatment of input signatures: the assembled output does not preserve their cryptographic validity;
- bounded-memory behavior and cancellation for large inputs;
- `PdfWorkflow.Combine(...)` and, if justified by use cases, a compose builder.

### Exclusions

- compaction;
- removing historical revisions;
- removing passwords;
- preserving input signatures as valid signatures on the new output;
- arbitrary page-content editing, layout, rendering, stamping, or watermarking.

### Exit criteria

- Imported object graphs are complete, remapped, and independently readable.
- Page selection, order, and resources survive independent-tool inspection and rendering.
- Conflicting document-level structures follow documented deterministic rules.
- Large-input memory use follows the reachable working set rather than total source size wherever the operation permits it.

## 10. Phase 4: signing

### Outcome

Signing is integrated into the final core/editor/workflow architecture, and new signing investment resumes from the already validated baseline.

### Scope

- add signing to deferred Ergonomics workflows with signing ordered after document modifications;
- revisit the signer and timestamp APIs only where the final source/editor model demonstrates a need;
- run W-103 or its approved replacement for independent PAdES profile classification;
- run W-104 or its approved replacement for direct CMS-reader fuzz coverage;
- refresh signing qualification against the new project, namespaces, API, and packaging model;
- consider additional signing formats or algorithms only through separate approved work items;
- retain the distinction between structural inspection and cryptographic validation.

### Exit criteria

- Direct-core and ergonomic signing produce independently verified results.
- Workflow validation prevents unsafe operation ordering.
- Existing RSA, ECDSA, digest, CMS/PAdES, timestamp, encryption, certification, and failure guarantees remain covered.
- Signing-specific fuzz and independent-verifier evidence is fresh for the final implementation.

## 11. Phase 5: advanced capabilities

Evaluate and schedule separately:

- compaction, removing revisions, and removing passwords through the new-file writer;
- cryptographic signature verification;
- certificate trust and revocation evaluation;
- long-term-validation data and additional PAdES profiles;
- optional S3, Azure Blob, or other `IPdfSource` adapters in separate integration packages;
- a deliberate expert/raw API backed by concrete use cases;
- tightly bounded image, vector, or signature-appearance placement that does not become a layout engine.

An item enters this phase only with a concrete use case, explicit guarantees, and a resource model.

## 12. Existing queue disposition

| Item | Disposition |
| --- | --- |
| W-117, cross-reference-stream append interoperability | Phase 1 foundational engine compatibility |
| W-99, unsigned encryption `/P` decision | Phase 1 foundational engine compatibility |
| W-80, MuPDF as second renderer | End of Phase 2 |
| W-103, EU DSS validation | Phase 4 signing |
| W-104, direct CMS fuzz target | Phase 4 signing |
| W-81, current 1.0 release checks | Supersede; the old release track is suspended |
| Other qualification-only work | Pause and reassess when its owning phase reaches a stable boundary |

Completed work remains part of the repository history and a source of tests, implementation, and lessons. Completion under the old architecture does not by itself qualify the new project boundary or public APIs.

## 13. Risks and controls

| Risk | Control |
| --- | --- |
| Namespace and project movement obscures behavior changes | Separate mechanical moves from API redesign; keep focused tests green at each checkpoint |
| A second engine emerges in Ergonomics | One-way project reference, boundary tests, and prohibition on PDF implementation code in Ergonomics |
| `PdfEngine` is rewritten merely because it is large | Extract only around the source, document/editor, and operation seams required by the target design |
| Async source work increases allocation or I/O amplification | Define partial-read and caching rules, preserve sparse probes, and measure representative large files |
| Signing regresses during migration | Move signing tests with the implementation; preserve output identity and independent-verifier checks; freeze enhancements |
| The workflow becomes a fluent mirror of the core | Require each workflow API to add composition, defaults, validation, ordering, or convenience |
| Assembly expands into a general toolkit | Keep Phase 3 limited to page import, selection, and combination with explicit document-structure policies |
| Qualification work becomes stale during structural churn | Run focused falsification during migration and produce release evidence only after the owning phase stabilizes |
| An unpublished engine makes the Ergonomics package unusable | Suspend the NuGet release track until packaging for both libraries is deliberately adopted |

## 14. Definition of reset completion

The architectural reset is complete when:

1. `jaytwo.Pdf` is the sole PDF engine and can be used independently as a project reference.
2. `jaytwo.Ergonomics.Pdf` contains only workflows, defaults, ordering, convenience, and friendly task-level errors.
3. The dependency direction is enforced in projects and tests.
4. The engine has a read-only document, explicit editor, random-access source abstraction, async I/O, cancellation, and explicit ownership.
5. Every valuable existing capability and regression test has an explicit home; none was silently discarded.
6. Existing signing remains functional without new signing scope before Phase 4.
7. Ergonomics provides genuinely simpler non-signing workflows.
8. Document assembly is complete before signing work resumes.
9. Protected repository guidance and the work-item queue agree with the new architecture and phase order.
10. Packaging remains intentionally blocked until a separate owner decision establishes how both projects ship.

At that point the repository is back on the intended path. Subsequent phases add capability to the new architecture rather than extending the architecture being replaced.
