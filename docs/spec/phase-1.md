# Phase 1 specification: read and append

Status: adopted by the owner, 2026-10-19 (D-38). Open questions are listed in section 12 and do not block work on settled requirements.

Phase 1 is release 1.0. It ships production-qualified on its own. Phase 2 (`phase-2.md`) adds to it in 1.x without breaking it.

## 1. Purpose

A permissively licensed, managed .NET library for the common business PDF operations that push developers toward commercial toolkits: reading and filling forms, metadata, attachments, encrypted files, and PKCS#11 signing. It works on gigabyte files and on documents with thousands of pages while reading only what an operation needs.

## 2. Tenets

In priority order. When two conflict, the earlier one wins.

1. **Not a general toolkit.** We do not compete with qpdf, iText, PDFsharp, or Aspose. Every operation is a named business use case. There is no layout engine, no "create a hello-world PDF", no fluent document model.
2. **Usable.** A developer can do each use case in a few lines, with errors that say what to do next.
3. **Production-ready.** Every advertised operation passes the release gates in section 10. Failure never leaves a half-written output. The one exception is a failure of the destination stream itself, such as an I/O error or a full disk: the library then attempts to restore the destination's original length, still reports the failure, and documents that restoration cannot be guaranteed (`QUALIFICATION.md` G9).
4. **According to the spec.** ISO 32000-1 (PDF 1.7) is the reference; ISO 32000-2 is followed where real files use it. Deviations are deliberate, listed, and tested.
5. **Compatible with real-world PDFs.** The test corpus is dominated by files real tools produced, not files this library or its test generators made.
6. **Maintainable.** Small internal surface, one way to do each thing, no speculative abstraction.

## 3. The phase rule

**Phase 1 is everything that reads a PDF or appends to it.** Every Phase 1 write is an incremental update (ISO 32000-1 7.5.6): the original bytes stay an exact prefix of the output, one new revision is appended, and memory follows the objects the operation touches.

Anything that writes a new file, renumbers objects, or drops bytes from the original is Phase 2.

## 4. Resource model

These hold for every Phase 1 operation.

- Input is a readable, seekable `Stream`. Offsets are `long`. Callers with a non-seekable source copy it to a file first.
- `Open` reads the header and the last cross-reference section chain. Other bytes are read only when an operation needs the objects in them. A page content stream is never decoded.
- Working memory is the cross-reference map plus the objects an operation touches. It does not grow with page count, image size, or attachment size. Existing budgets (D-20) carry forward; each new operation in section 6 gets a budget before it is qualified.
- Output is either appended to the opened stream or copied, then appended, to an empty destination stream with a 1 MiB buffer.
- Writes use classic cross-reference tables. Saving fails with `PdfTooLargeForClassicXrefException` before writing anything when an offset would pass 9,999,999,999 bytes (about 9.3 GiB). Reading has no such limit.
- Decompression caps (32 MiB and 64 MiB) and structural limits stay as documented in `README.md`.

## 5. Reads

Status key: **Exists** (implemented and tested today), **Change** (exists, behavior or API changes), **New**.

| ID | Requirement | Status |
| --- | --- | --- |
| R1 | Open PDF 1.0 through 1.7, plus the PDF 2.0 structures real files use. Classic xref, xref streams, hybrid files, object streams, linearized files. | Exists (no linearized fixture yet) |
| R2 | Open standard-security revisions 2 through 6 with a user or owner password, decrypting strings and streams on read. Public-key security is refused with `PdfEncryptedException`. | Exists |
| R3 | `Permissions` reports the user-access bits. A user password is held to them; an owner password is not. | Exists |
| R4 | `Pages`: number, media box, crop box, rotation, in PDF user space. | Exists |
| R5 | Fields: full name, type, value, flags, rectangle, page, options, check state. | Exists |
| R6 | **Form-data export.** `AcroForm.Export()` returns one entry per terminal field (not per widget): text as string, check box as bool, radio as the selected export value or null, choice as string or string list. | Exists |
| R7 | Document information dictionary and XMP, by property and by key. | Exists |
| R8 | Document-level embedded files: list with name, description, MIME type, size, relationship, dates; open the content as a stream. | Change (relationship and dates) |
| R9 | **Signature inspection.** List every signature field: field name, whether it is signed, SubFilter, signer certificate (DER), chain certificates present in the CMS, digest and signature algorithm, `/M` and CMS signing time, reason, location, contact, `/ByteRange`, which revision it signs, whether later revisions exist, whether a timestamp token is embedded and its time, certification level (DocMDP `P`) if it certifies. No trust, revocation, or cryptographic validation (section 11). Reading signature metadata does not read the signed byte range. | New |
| R10 | **Revision listing.** List revisions in order with the end offset of each and whether a signature covers it. A linearized file's first-page cross-reference section and its main section are one revision, not two. Only cross-reference sections and trailers are read. | New |
| R11 | Strict reading with a documented list of tolerated writer quirks (section 8). | Exists |
| R12 | Form classification: `AcroForm.IsHybridXfa` and `AcroForm.IsXfaOnly`. | New |

## 6. Incremental writes

All writes are pending until `SaveIncremental`, `Sign`, or their async forms. Several edits in one save produce one revision. Dispose never saves.

### 6.1 Forms

| ID | Requirement | Status |
| --- | --- | --- |
| W1 | Fill a text, check box, radio, or choice field by full name. Text and choice values in WinAnsi get a generated Helvetica appearance. Other values set `/NeedAppearances true` and return `NeedsViewer`. | Exists |
| W2 | **Bulk fill**, all-or-nothing. Every value is validated (field exists, type, read-only, `/MaxLen`, listed option, FieldMDP, certification, permissions) before anything is changed. One failure throws and leaves the document unchanged. | Exists |
| W3 | **Flatten** all fields or named fields (section 7.1). | New |
| W4 | **Hybrid XFA.** The first form write to a hybrid form removes `/XFA` from the AcroForm dictionary in the same revision, so every viewer shows the AcroForm values. A write to an XFA-only form throws `PdfUnsupportedFormException`. | New |
| W5 | Password and signature fields cannot be filled. Read-only fields throw `PdfFieldReadOnlyException`. Neither dirties the document. | Exists |

### 6.2 Pages

| ID | Requirement | Status |
| --- | --- | --- |
| W6 | **Rotate.** Set a page's rotation to 0, 90, 180, or 270 (other multiples of 90 are normalized). The value is written on the page object itself, overriding any inherited `/Rotate`. Field and box coordinates stay in user space and do not change. | New |

### 6.3 Metadata

| ID | Requirement | Status |
| --- | --- | --- |
| W7 | Write information-dictionary keys and XMP. An info edit refreshes `ModDate` in UTC and keeps matching XMP properties in step. | Exists |

### 6.4 Attachments

| ID | Requirement | Status |
| --- | --- | --- |
| W8 | Add, replace (add with an existing name), and remove document-level embedded files. Content is streamed from a seekable stream, never buffered whole. These are the files Acrobat lists in its attachments panel. | Exists |
| W9 | **Relationship.** The caller chooses `/AFRelationship`: `Source`, `Data`, `Alternative`, `Supplement`, `EncryptedPayload`, `FormData`, `Schema`, or `Unspecified` (default). E-invoice formats such as Factur-X and ZUGFeRD require one of these on their embedded XML. The PDF/A-3 subset is `Source`, `Data`, `Alternative`, `Supplement`, and `Unspecified`; the other values come from PDF 2.0, and a PDF/A-3 input is not promised to stay conformant when one of them is chosen. This supplies associated-file building blocks only; it does not claim PDF/A-3, Factur-X, or ZUGFeRD conformance, which also need profile-specific XMP metadata. | New |
| W10 | **Dates and checksum.** `/Params` carries `/CreationDate`, `/ModDate`, and `/CheckSum` (MD5 of the uncompressed bytes, computed in an extra streaming pass over the content). | New |
| W11 | **Optional compression.** `Compress = true` writes the content Flate-encoded while streaming, in constant memory. Default is off. | New |
| W12 | **Open to attachments.** An option sets catalog `/PageMode /UseAttachments` so viewers open with the attachments panel showing. | New |
| W13 | Removing an attachment removes it from the name tree and `/AF`. Its bytes stay in earlier revisions until Phase 2 compaction; this is documented on `Remove`. | Change (documentation) |

### 6.5 Signing

| ID | Requirement | Status |
| --- | --- | --- |
| W14 | Sign through a host-implemented `IPdfExternalSigner`. The library never loads a PKCS#11 module. A new signature field is created, or an existing empty one is reused by name. | Exists |
| W15 | **Key types.** RSA (PKCS#1 v1.5) and **ECDSA P-256 and P-384**. For ECDSA the signer returns the IEEE P1363 `r‖s` form that PKCS#11 `CKM_ECDSA` and .NET `ECDsa.SignHash` produce; the library DER-encodes it. | Change (ECDSA new) |
| W16 | **Digests.** SHA-256 (default), **SHA-384, and SHA-512**. The signer receives the raw digest and the algorithm. | Change |
| W17 | **Formats.** `adbe.pkcs7.detached` (current) and **PAdES baseline B-B** (`ETSI.CAdES.detached`, ETSI EN 319 142-1): `signing-certificate-v2` signed attribute, no CMS `signing-time` attribute (the time is in `/M`). With a timestamp authority, the result is PAdES B-T. | Change (PAdES new) |
| W18 | RFC 3161 signature timestamp as an unsigned attribute, and document timestamps as a separate revision, with the existing nonce, imprint, algorithm, and certificate checks. | Exists |
| W19 | Failure never extends the output: signer, timestamp, size, and validation failures leave the destination unchanged. Signer exception details are not exposed. | Exists |
| W20 | **Async.** The signer and timestamp interfaces are asynchronous and take a `CancellationToken`. `SaveIncrementalAsync` and `SignAsync` exist beside the synchronous methods. Reads stay synchronous. The API may use `ValueTask` and `IAsyncDisposable` on every target (D-39). | Exists |

### 6.6 API

The API may change freely until 1.0. After 1.0, semantic versioning applies. The shapes below are illustrative; the requirements above are binding.

```csharp
using var doc = PdfDocument.Open(stream, password);

var data = doc.AcroForm.Export();                       // R6
doc.AcroForm.Fill(new Dictionary<string, PdfFieldValue> // W2
{
    ["Name"] = "Ada",
    ["Agree"] = true,
    ["Colors"] = new[] { "Red", "Blue" },
});
doc.AcroForm.Flatten();                                 // W3
doc.Pages[0].Rotation = 90;                             // W6

doc.Attachments.Add("factur-x.xml", xml, new PdfAttachmentOptions
{
    MimeType = "text/xml",
    Relationship = PdfAttachmentRelationship.Alternative, // W9
    Compress = true,                                       // W11
});

foreach (PdfSignatureInfo sig in doc.Signatures) { }    // R9
foreach (PdfRevision rev in doc.Revisions) { }          // R10

await doc.SignAsync(output, new PdfSignatureOptions
{
    Format = PdfSignatureFormat.PadesBaselineB,         // W17
    DigestAlgorithm = PdfDigestAlgorithm.Sha384,        // W16
}, new CmsSignatureProvider(cloudKmsSigner, timestampAuthority), cancellationToken);
```

## 7. Behavior rules

### 7.1 Flatten

- For each widget being flattened, the library appends drawing operators to its page that paint the widget's current normal appearance (`/AP /N`, selected by `/AS` for buttons) as a form XObject, placed by the algorithm in ISO 32000-1 12.5.5. The existing page content is wrapped in `q`…`Q` by adding streams to the `/Contents` array, so no content stream is decoded.
- Widgets flagged `Hidden` or `NoView` are removed without being drawn.
- Flattened widgets are removed from the page `/Annots`; flattened fields are removed from `/Fields` and their parents. When no fields remain, `/XFA` and `/NeedAppearances` are removed too.
- A widget without a usable appearance (for example after a non-WinAnsi fill) makes flatten throw before changing anything.
- Signed signature fields are never flattened. Unsigned signature fields are kept unless named (section 12, question 6).

### 7.2 Writes on signed or certified documents

- Certification (DocMDP) rules stay as today: `P` 1 blocks every write and signing; `P` 2 and `P` 3 allow fills and signing; metadata, attachment, flatten, and rotate writes are blocked at every level. FieldMDP locks apply to fills and flatten.
- On a document with any approval signature, **flatten and rotate throw** `PdfSignatureConflictException` unless the caller sets `AllowBreakingSignatures = true`, because both change page content and viewers will report the earlier signatures as modified. Fills, metadata, attachments, and new signatures stay allowed, as today.

### 7.3 Usage rights (Reader extensions)

When the catalog `/Perms` has a `/UR3` signature, a write keeps it when its transform parameters grant that operation:

- `/Form` `FillIn` for fills;
- `/EF` `Create`, `Delete`, or `Modify` for attachment edits; replacing an attachment that has the same name is `Modify`;
- `/Signature` `Modify` for `Sign` into an existing empty signature field (ISO 32000-1 Table 255).

Any other write removes `/UR3` from `/Perms` in the same revision, so Adobe Reader drops the extended features silently instead of reporting a broken document. A `Sign` that creates a new signature field and a document timestamp are such writes.

On a document that already has a signature (approval, certification, or document timestamp), a write never changes `/Perms`: `/UR3` stays even when its transform does not grant the write, because removing it is a change no signature permits, and validators report the earlier signatures as modified. Adobe Reader may then report that its extended features are unavailable.

### 7.4 Encryption on write

New strings and streams use the document's existing cipher. The trailer keeps `/Encrypt` and `/ID`. The signature `/Contents` stays plaintext. A user password is held to the permission bits for every write. No operation adds, removes, or changes encryption in Phase 1.

### 7.5 Version

When a write uses a feature that ISO 32000-1 defines at a later version than the file's effective version (for example `/PageMode /UseAttachments`, PDF 1.6), the catalog `/Version` is raised in the same revision, never above `/1.7` (ISO 32000-1 7.7.2). Phase 1 never raises a file to `/2.0`. The header is never rewritten.

`/AF` and `/AFRelationship` are written without a version raise. This is a deliberate, documented compatibility extension to PDF 1.x, not strict ISO 32000-1 conformance: PDF/A-3 (ISO 19005-3) added associated files to its PDF 1.7 base, PDF 2.0 later adopted them, 1.x readers ignore the keys, and `/EmbeddedFiles` stays the backward-compatible way to find the file. Raising to 2.0 instead would break PDF/A-3 inputs such as e-invoices, and would subject the file to PDF 2.0 deprecations (for example RC4 security) that Phase 1 deliberately preserves.

No PDF/A conformance is claimed. Appending to a PDF/A file is not guaranteed to keep it conformant.

### 7.6 Appearances

Generated appearances cover WinAnsi text only (Western European languages), in Helvetica with Adobe advance widths. Other scripts return `NeedsViewer`, which blocks signing and flattening with a clear exception. Caller-supplied fonts are planned for 1.x (section 12, question 1).

## 8. Real-world tolerance

Reading is strict to ISO 32000. A short, closed list of writer bugs that Acrobat accepts is tolerated. Each entry has a real-world fixture, a test, and a line in `README.md`. A quirk handler may look in a small window around the expected position; it never scans the whole file, so sparse reading holds.

Candidates, confirmed or dropped by what the corpus actually contains:

- A cross-reference offset that is off by a few bytes: look nearby for `N G obj`.
- A stream `/Length` that is wrong: find `endstream` near the stated end.
- Bytes after the final `%%EOF`.
- Cross-reference rows with nonstandard line endings beyond those already accepted.

Files with no usable cross-reference, or with structurally invalid objects, throw `PdfParseException`. Rebuilding a cross-reference by full scan is out of scope.

## 9. Platforms and dependencies

- Targets `netstandard2.0`, `net6.0`, `net8.0`, and `net10.0` (D-40). `netstandard2.1` is dropped. Tests run on `net6.0`, `net8.0`, and `net10.0`, and on `net48` on Windows.
- Runtime dependencies: on `netstandard2.0` only, `System.Memory` and `Microsoft.Bcl.AsyncInterfaces` (which brings `System.Threading.Tasks.Extensions`, D-39). `net8.0` and `net10.0` have none. Any other runtime package is an owner decision.
- Not thread-safe. One `PdfDocument` per operation.

## 10. Release bar

1.0 is released when every requirement marked in sections 5 and 6 passes the gates below. This right-sizes `QUALIFICATION.md`; that file is updated under its own work item with owner approval.

| Gate | Phase 1 bar |
| --- | --- |
| G1 Boundaries | `README.md` support matrix matches this spec, including every tolerated quirk. |
| G2 Interop corpus | About 30 committed or reproducibly fetched files, mostly real-world, covering all four categories: business and government forms (including hybrid XFA, Reader-extended, and signed forms), Office and browser exports (Word, Excel, LibreOffice, Chrome or Edge), scans and image-heavy files (including linearized), and generated reports (thousands of pages). Each has a manifest entry: producer, source, license, and what it exercises. Every write is checked with `qpdf --check`. |
| G3 Correctness | Every requirement has a positive test, a negative test, and an independent-reader check (pypdf, qpdf, Poppler, or pyHanko). |
| G4 Signatures | Two PDF-aware verifiers: pyHanko plus Poppler `pdfsig`. RSA and ECDSA, each digest, both formats. |
| G5 Timestamps | A token from an authority other than the one under test verifies with pyHanko and `openssl ts -verify`. |
| G6 Rendering | Fill and flatten results render as expected in Poppler and one other renderer (MuPDF or pdf.js). |
| G7 Large files | Existing workloads plus flatten on a 1,000-page form, bulk fill of 500 fields, signature inspection and revision listing on a 1 GiB file without reading signed ranges, and a 1 GiB attachment added with compression. Memory budgets met; timings recorded as a baseline, not as targets. |
| G8 Security | One fuzzing campaign over the parser and decoders, with every finding fixed or dispositioned, and one independent review of the parser and the signer. |
| G9 Failure | Every failure path leaves the output unchanged and throws a documented exception; messages carry no secrets. |
| G10 Release | All targets build warning-free; package contents and dependencies checked; README examples compile and run; versioning policy documented. |

Evidence names the commit, tool versions, and fixtures it ran against. Evidence older than the code it covers is stale.

## 11. Not in Phase 1

**Phase 2** (`phase-2.md`): page extraction, concatenation, compaction, removing revisions, removing passwords.

**Planned for 1.x:** caller-supplied fonts for non-WinAnsi appearances; long-term validation data (PAdES B-LT and B-LTA).

**Never:** rendering, text extraction, stamping and watermarks, layout or creating PDFs from scratch, redaction, signature validation (trust, revocation, cryptographic verification), PDF/A conversion, XFA editing, JavaScript, annotation editing beyond what flatten requires, page-level file attachment annotations, public-key encryption.

## 12. Open questions

These were flagged for discussion at adoption. Settled requirements above do not wait on them.

1. **Fonts timing.** Caller-supplied TrueType fonts are deferred to 1.x. Revisit if users need non-Latin fills in 1.0.
2. **Long-term validation.** PAdES B-LT and B-LTA are deferred to 1.x. Revisit if a user archives signed documents for years.
3. **Large attachment plus signature.** Signing in the same revision as a large attachment hits the 8 MiB signature-appendix cap and requires `SaveIncremental` first. Should `Sign` do that split automatically (two revisions)?
4. **Integrity check in inspection.** Should signature inspection offer an opt-in check that the signed bytes still hash to the CMS message digest? It reads the whole signed range, and it is the first step toward the validation that section 11 excludes.
5. **Page-level attachment listing.** Read-only listing of paperclip annotations was declined. Confirm.
6. **Unsigned signature fields on flatten.** Keep them (current rule), or flatten them as empty boxes?
7. **Value typing.** `PdfFieldValue` with implicit conversions, or plain `object`?
8. **MD5 under FIPS.** On .NET Framework with the FIPS policy enabled, MD5 throws. Should the checksum be skipped silently, skipped with a flag, or fail the add?
