# Code map

Migrated from `agents/archive/state-v1/status.md` under D-33; D-36 and W-56 record its removal, and its pre-removal state is [squashed].

Maintained through `pdf-task-workflow`. As of 2026-10-18. Paths are under `src/jaytwo.Ergonomics.Pdf/` unless noted.

| Area | Primary skill | Where it lives |
|---|---|---|
| Public entry points | `pdf-task-workflow` plus the affected domain skill | `PdfDocument.cs`, `PdfPage.cs`, `PdfRect.cs`, `PdfPermissions.cs`, root exception types |
| Engine and integration | Affected domain skill; `pdf-change-verification` checks integration | `Internal/PdfEngine.cs`, about 3,650 lines. Fills, metadata, attachments, encryption gates, DocMDP, and `Sign` meet here. |
| Parser, xref, object model | `pdf-core-change` | `Internal/PdfParser.cs`, `Internal/PdfObject.cs`, `Internal/PdfCursor.cs`, `Internal/XrefEntry.cs` |
| Filters | `pdf-core-change` | `Internal/PdfStreamDecoder.cs` |
| Standard security | `pdf-core-change` | `Internal/PdfSecurity.cs` |
| Writer and stream plumbing | `pdf-core-change` | `Internal/PdfSerializer.cs`, `Internal/PdfSliceStream.cs`, `Internal/SplicedStream.cs` |
| Text encodings | `pdf-core-change` | `Internal/PdfTextCodec.cs` |
| Metadata | `pdf-core-change` | `Metadata/` |
| Attachments | `pdf-core-change` | `Attachments/` |
| AcroForm | `pdf-acroform-change` | `AcroForm/`, including `AppearanceBuilder.cs` |
| Signatures and timestamps | `pdf-signing-change` | `Signatures/` |
| Tests | `pdf-change-verification` | `test/jaytwo.Ergonomics.Pdf.Tests/` |
| Foreign fixtures and helpers | `pdf-change-verification` | `test/jaytwo.Ergonomics.Pdf.Tests/Fixtures/`: generator scripts, `tsa_server.py`, `validate_signature.py`, committed PDFs |

Target frameworks: the library builds `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0`, all intentionally under D-13. The test project runs on `net8.0`, `net6.0`, and Windows-only `net48`; `net48` consumes the `netstandard2.0` library. Test-only packages are `Pkcs11Interop` and, on `net8.0`, `System.Security.Cryptography.Pkcs`.
