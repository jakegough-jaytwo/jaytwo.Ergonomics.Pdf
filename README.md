# jaytwo.Ergonomics.Pdf

Managed, MIT-licensed .NET libraries for the common business operations on existing PDFs: fill and flatten forms, edit metadata and attachments, rotate pages, sign with a host-held key, and assemble pages into a new file. They stay sparse on gigabyte files and on documents with thousands of pages.

The [Project Charter](docs/CHARTER.md) defines purpose, principles, and governance. [Supported capabilities](docs/SUPPORTED.md) defines intended scope, whether implemented or not. Use them to assess whether work serves the project. Current behavior is in [`docs/behavior.md`](docs/behavior.md).

## Two libraries

**`jaytwo.Pdf` understands PDFs.** It is the engine: parsing, the object model, writing, encryption, forms, appearances, signatures, CMS, and every document invariant.

**`jaytwo.Ergonomics.Pdf` understands what developers want to do with PDFs.** It describes operations, chooses defaults, orders and validates them, runs them through one engine editor, and reports task-level errors. It contains no PDF parsing, serialization, encryption, signing, or object manipulation.

The dependency runs one way: `jaytwo.Ergonomics.Pdf -> jaytwo.Pdf`. The engine is fully usable on its own.

## What it does

- **Read:** form fields and values, page geometry, document information and XMP, attachments, revisions, and signatures. Signature inspection reports what the file claims; it does not validate.
- **Change** an existing file as one incremental revision: fill fields (one at a time or all-or-nothing in bulk), flatten fields, write document information and XMP, add and remove attachments, and rotate pages.
- **Sign** with a key that stays in the host (PKCS#11, a cloud key service, or anything else), with RFC 3161 timestamps and document timestamps.
- **Open password-encrypted files** (standard security revisions 2 to 6) and honor their permissions.
- **Assemble** page selections from one or more files into a new, unencrypted file.
- **Workflows:** describe the operations once and run them against a stream or a file, with safe ordering and file publication handled for you.

## Product boundaries

Rendering, text extraction, general document authoring and layout, rewriting a file in place, redaction, signature validation (trust, revocation, or cryptographic verification), PDF/A conversion or conformance claims, XFA editing, JavaScript, annotation editing beyond what flatten requires, page-level file attachment annotations, public-key encryption, and adding encryption to a file.

Limited image placement and image-to-PDF are explicit scope exceptions under [SUPPORTED.md](docs/SUPPORTED.md). These and security-permission setters are intended additions, not current APIs; implementation requires an adopted operation contract under the [charter](docs/CHARTER.md).

Neither library is a general PDF toolkit or competes with qpdf, iText, PDFsharp, or Aspose. There is no layout engine, renderer, or document model. A workflow that records operations on an existing document is not a document model.

## Commitments

New work keeps these. A change that needs to break one is a change to the project's purpose and is the owner's decision.

1. **Memory follows the operation, not the file.** Opening reads the header and the cross-reference tail. Later reads load only the objects an operation touches. No API takes or returns the whole file as a `byte[]`, and offsets are `long`.
2. **Current editing and assembly writes.** Changes to an existing file are incremental updates. The original bytes stay a prefix, so existing signatures keep their signed bytes. The only other write path creates a new file and copies streams without decoding them.
3. **Read strictly.** Reading follows ISO 32000. Tolerated writer bugs are a short, closed list, each backed by a real file. Nothing scans a whole file to repair it. Page content streams are never decoded.
4. **Refuse rather than guess.** Unsupported input, or a write that certification, field locks, or permissions forbid, is refused before anything is written. Nothing silently emits a malformed file, invalidates a signature, or overstates what was checked.
5. **Keys stay with the host.** The host signs a digest and returns the signature value; the library builds the CMS. Neither library loads a PKCS#11 module.
6. **Inspect with a document, change with its editor.** `PdfDocument` only reads. `PdfDocument.Edit()` returns the `PdfDocumentEditor` that owns every change, save, and signature.
7. **Small and self-contained.** Managed code only. The runtime dependencies are `System.Memory` and `Microsoft.Bcl.AsyncInterfaces`, on `netstandard2.0` only. Engine internals stay internal.
8. **Simple things stay simple.** The workflow layer describes intent with sensible defaults. A caller does not need to know about cross-reference sections, appearance streams, byte ranges, or CMS.

## Tenets

When goals conflict, earlier tenets win:

1. Not a general toolkit.
2. Usable.
3. Production-ready.
4. According to the spec: ISO 32000-1, ISO 32000-2 where real files use it, and ETSI EN 319 142-1 for PAdES.
5. Compatible with real-world PDFs.
6. Maintainable.

## Does a change belong here?

A feature belongs when all of these hold:

- It serves a capability in the intended product scope.
- Its output strategy follows an adopted operation contract; new full-document rewrite strategies require owner approval under the charter.
- Its memory follows the objects it touches, not the size of the file or its page count.
- It respects the product boundaries, including the explicit image exceptions.
- It follows the charter and the applicable operation contract.

Fewer, well-tested operations beat broad, unreliable ones.

## Example

```csharp
using jaytwo.Ergonomics.Pdf;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Signatures;

await PdfWorkflow.FromFile("order.pdf")
    .Fill("Customer", "Ada Lovelace")
    .FillForm(new Dictionary<string, PdfFieldValue> { ["Agree"] = true, ["Total"] = "120.00" })
    .FlattenForm()
    .WithTitle("Executed Order")
    .AttachFile("order.json", "order.json")
    .SignAsync("order-signed.pdf", signer, new PdfSignatureOptions { FieldName = "Approval" }, authority, cancellationToken: cancellationToken);
```

## Status

Pre-release. No package is published, and the 1.0 release track is suspended while the [architectural reset](reset-plan.md) moves through stages R0 to R5. Target scope is in [`docs/spec/`](docs/spec/): Phase 1 reads and appends to existing files; Phase 2 writes new files. Caller-supplied fonts and PAdES long-term validation are planned for 1.x.

## More

- [`docs/behavior.md`](docs/behavior.md): shipped behavior, the support matrix, limits, and failure semantics.
- [`ACCEPTANCE.md`](ACCEPTANCE.md): the checks the tests enforce.
- [`docs/spec/`](docs/spec/): adopted operation contracts and draft requirements.
- [`CONTRIBUTING.md`](CONTRIBUTING.md): building and testing.
- [`AGENTS.md`](AGENTS.md): binding contributor rules and adopted implementation constraints.
