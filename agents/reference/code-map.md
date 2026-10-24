# Code map

Migrated from `agents/archive/state-v1/status.md` under D-33; D-36 and W-56 record its removal, and its pre-removal state is [squashed].

Maintained through `pdf-task-workflow`. As of 2026-10-23. Paths are under `src/jaytwo.Pdf/` unless noted.

| Area | Primary skill | Where it lives |
|---|---|---|
| Public entry points | `pdf-task-workflow` plus the affected domain skill | `PdfDocument.cs` (inspection), `PdfDocumentEditor.cs` (changes, saves, signatures), `PdfPage.cs`, `PdfPageEditor.cs`, `PdfRect.cs`, `PdfPermissions.cs`, root exception types |
| Engine and integration | Affected domain skill; `pdf-change-verification` checks integration | `Internal/PdfEngine.cs` opens a document and routes each operation to its owner (W-137). `Internal/PdfObjectStore.cs` holds the source, cross-reference map, object cache, edits and their backup, trailer values, and the D-49 lease. `Internal/PdfWriteGate.cs` holds the password-permission, DocMDP, FieldMDP, and usage-rights checks every write passes. `Internal/PdfPageTree.cs` reads pages and sets rotation. |
| Parser, xref, object model | `pdf-core-change` | `Internal/PdfDocumentLoader.cs` (header, startxref, xref chain, trailer, security setup), `Internal/PdfLexer.cs`, `Internal/PdfParser.cs`, `Internal/PdfXrefReader.cs`, `Internal/PdfObjectStreamReader.cs`, `Internal/PdfObject.cs`, `Internal/PdfCursor.cs`, `Internal/XrefEntry.cs` |
| Filters | `pdf-core-change` | `Internal/PdfStreamDecoder.cs` |
| Standard security | `pdf-core-change` | `Internal/PdfSecurity.cs` |
| Writer and stream plumbing | `pdf-core-change` | `Internal/PdfIncrementalWriter.cs` (appends a revision), `Internal/PdfSerializer.cs`, `Internal/PdfSliceStream.cs`, `Internal/SplicedStream.cs` |
| Text encodings | `pdf-core-change` | `Internal/PdfTextCodec.cs` |
| Metadata | `pdf-core-change` | `Metadata/`; `PdfMetadataEditor.cs` writes; `Internal/PdfInfoMetadata.cs` keeps Info and XMP consistent |
| Attachments | `pdf-core-change` | `Attachments/`; `PdfAttachmentCollectionEditor.cs` writes; `Internal/PdfEmbeddedFiles.cs` reads and writes the name tree and `/AF` |
| AcroForm | `pdf-acroform-change` | `AcroForm/`, including `AppearanceBuilder.cs`; `PdfAcroFormEditor.cs` fills and flattens; `Internal/PdfFieldTree.cs` reads the field tree, `Internal/PdfFormFiller.cs` fills, `Internal/PdfFormAppearances.cs` writes appearances, `Internal/PdfFlattener.cs` flattens |
| Signatures and timestamps | `pdf-signing-change` | `Signatures/`; `Internal/PdfSigner.cs` signs, `Internal/PdfSignatureInspector.cs` lists signatures and revisions |
| Engine tests | `pdf-change-verification` | `test/jaytwo.Pdf.Tests/` and its `netstandard2.0` lane `test/jaytwo.Pdf.NetStandard.Tests/` |
| Workflow tests (public API only) | `pdf-change-verification` | `test/jaytwo.Ergonomics.Pdf.Tests/` |
| Foreign fixtures and helpers | `pdf-change-verification` | `test/jaytwo.Pdf.Tests/Fixtures/`: generator scripts, `tsa_server.py`, `validate_signature.py`, committed PDFs |

Target frameworks: both libraries build `net10.0`, `net8.0`, `net6.0`, and `netstandard2.0`. `test/jaytwo.Pdf.Tests/` runs on `net10.0`, `net8.0`, `net6.0`, and Windows-only `net48`; `net48` consumes the `netstandard2.0` library, and `test/jaytwo.Pdf.NetStandard.Tests/` runs that build on `net8.0`. Test-only packages include `itext`, `Pkcs11Interop`, and, on `net10.0` and `net8.0`, `System.Security.Cryptography.Pkcs`.
