# Project status

This file is state. It is true as of the date on each section and goes stale. When it disagrees with the repository, the repository wins. Report the difference to the section's owner.

## Repository

Owner: Technical Lead. As of 2026-10-18.

- Branch `wip`. `HEAD` is `b832f22`, "Initial commit", and contains only `LICENSE`. `main` points at the same commit.
- `src/`, `test/`, `agents/`, and the root docs are untracked. A worktree or branch cut from `HEAD` does not contain the library.
- Until the tree is committed, tasks that edit files run one at a time. See backlog item B-01.

## Code map

Owner: Technical Lead. As of 2026-10-18. Paths are under `src/jaytwo.Ergonomics.Pdf/` unless noted.

| Area | Owner seat | Where it lives |
|---|---|---|
| Public entry points | Technical Lead | `PdfDocument.cs`, `PdfPage.cs`, `PdfRect.cs`, `PdfPermissions.cs`, root exception types |
| Engine and integration | Technical Lead reviews; region owners edit | `Internal/PdfEngine.cs`, about 3,650 lines. Fills, metadata, attachments, encryption gates, DocMDP, and `Sign` meet here. |
| Parser, xref, object model | Core Engineer | `Internal/PdfParser.cs`, `Internal/PdfObject.cs`, `Internal/PdfCursor.cs`, `Internal/XrefEntry.cs` |
| Filters | Core Engineer | `Internal/PdfStreamDecoder.cs` |
| Standard security | Core Engineer | `Internal/PdfSecurity.cs` |
| Writer and stream plumbing | Core Engineer | `Internal/PdfSerializer.cs`, `Internal/PdfSliceStream.cs`, `Internal/SplicedStream.cs` |
| Text encodings | Core Engineer | `Internal/PdfTextCodec.cs` |
| Metadata | Core Engineer | `Metadata/` |
| Attachments | Core Engineer | `Attachments/` |
| AcroForm | AcroForm Engineer | `AcroForm/`, including `AppearanceBuilder.cs` |
| Signatures and timestamps | Signing Engineer | `Signatures/` |
| Tests | QA Engineer | `test/jaytwo.Ergonomics.Pdf.Tests/` |
| Foreign fixtures and helpers | QA Engineer | `test/jaytwo.Ergonomics.Pdf.Tests/Fixtures/`: generator scripts, `tsa_server.py`, `validate_signature.py`, committed PDFs |

Target frameworks: the library builds `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0`, all intentionally under D-13. The test project runs on `net8.0` and `net6.0` only. Test-only packages are `Pkcs11Interop` and, on `net8.0`, `System.Security.Cryptography.Pkcs`.

## Technical baseline

Owner: Technical Lead. As of 2026-10-18. Inventory only. Nobody has run the suite in this review round.

- The test project declares 107 `[Fact]` methods, plus `[Theory]` methods with 38 `[InlineData]` rows.
- These test files cover what the original role files called open gaps:
  - `AppearanceRenderTests`: Poppler text extraction and raster check of generated appearances.
  - `SigningInteropTests`: pyHanko trust of an embedded chain, an RFC 3161 token from the local TSA in `Fixtures/tsa_server.py`, and a SoftHSM signer through `IPdfExternalSigner`.
  - `DenseMemoryTests`: managed-memory peak on dense files, including a qpdf object-stream rewrite.
- Known code gap: `HttpTimestampAuthority` checks the PKI status and extracts the token. Neither it nor `CmsDetachedSignatureProvider` compares the token's message imprint or digest algorithm with the request. Requests carry no nonce, which D-14 now requires. See backlog item B-04.

## Evidence inventory

Owner: QA Engineer. Not yet produced. See backlog items B-03 and B-06.

Until it exists, the "Foreign files" section of `ACCEPTANCE.md` is the best list of independently validated behavior. Nothing there has been re-run in this round.

## Approved memory budgets

Owners: Technical Lead and QA Engineer, under `QUALIFICATION.md` gate G7. None approved yet. The standard's proposed starting budgets apply until fixture-specific ones are measured.

## Release readiness

Owner: Product Manager. The bar is `QUALIFICATION.md`, adopted under D-15. Not yet assessed against it. No capability is production-qualified.
