# jaytwo.Ergonomics.Pdf

Stream-native PDF operations for .NET. MIT licensed, no runtime dependencies beyond `System.Memory` on `netstandard2.0`, and no `byte[]` of the whole file.

The library reads and updates PDF 1.0 through PDF 1.7 / ISO 32000-1 (with the PDF 2.0 xref and metadata behavior that shows up in real files). It is for the operations below, not a general PDF toolkit. Files produced by pypdf, qpdf, and pyHanko are part of the test suite, and those tools check the revisions this library writes.

## Charter

1. Fill AcroForm fields.
2. Digitally seal a document with a host PKCS#11 signer and an RFC 3161 timestamp authority.
3. Read AcroForm field geometry: name, value, rectangle, page number, rotation, and media box.
4. Read and write document information and XMP metadata.
5. Read and write embedded-file attachments.

## Tenets

- **Seekable streams, long offsets.** `PdfDocument.Open` requires `CanRead` and `CanSeek`. Offsets are `long` and are file offsets, including any preamble before `%PDF-`.
- **Do not buffer the file.** Opening reads the header and the tail xref. Later reads load the objects an operation touches. A 3 GiB file and a 1,000-page file stay sparse.
- **Incremental append.** `SaveIncremental` and `Sign` add one classic xref revision. The original bytes stay a prefix. Same-stream saves append. Cross-stream saves copy with a 1 MiB buffer into an empty destination, then append. The opened stream's bytes stay unchanged.
- **Read the xref forms writers actually emit.** Classic tables (CRLF, LF, and the 20-byte space-LF and space-CR rows), xref streams (Flate and PNG predictors), hybrid `/XRefStm`, and object streams. Writes are classic xref only. Edited objects that lived in an object stream are written as normal objects.
- **Working set is the xref map plus touched objects.** `/Size` is not an array. Clean objects loaded from an object stream are released when the public operation returns. A map that contains a dirty object stays. The 32 MiB and 64 MiB caps are decompression limits, and the bound for one structural stream. A signature appendix larger than 8 MiB is refused; call `SaveIncremental` first. Classic xref offsets stop at 9,999,999,999.
- **Password-based encryption is decrypted on read and encrypted on write.** Pass the user or owner password to `Open`. Revision 2 is 40-bit RC4. Revision 3 is RC4 at 40 to 128 bits. Revision 4 is AES-128 or RC4, chosen by the crypt filter. Revision 5 and revision 6 are AES-256. The new trailer keeps `/Encrypt` and `/ID`. Signature dictionaries stay plaintext. Cross-reference streams stay plaintext. `/Encrypt` is an uncompressed object found from the xref map. An encrypted xref stream fails `Open`. Public-key encryption is refused. A null password tries the empty user password, which opens a restrictions-only file. A user password is limited to the permission bits. `Permissions` reports print, high-quality print, modify, copy, annotate, form fill, assembly, and accessibility extraction. An unencrypted document allows every operation. The owner password is not limited by the reported bits. Revision 2 folds the later operations into print, modify, copy, and annotate.
- **Text is PDF text.** UTF-16BE (`FE FF`) and UTF-8 (`EF BB BF`) are recognized. Anything else is PDFDocEncoding, not Windows-1252. Writes use a literal string for ASCII and UTF-16BE hex otherwise.
- **Appearances are deliberately small.** A WinAnsi text or choice value gets a Helvetica appearance and `/NeedAppearances false`. Helvetica advances use the Adobe glyph widths, 1000 units per em, indexed by the WinAnsi byte. Check boxes and radios reuse the existing on-state. A check box with no appearance gets a generated `Yes`/`Off` mark. One radio widget with no on-state gets a mark named for its export value, drawn with Bezier curves. Comb, rich text, and non-WinAnsi values set `/NeedAppearances true`. Signing throws while that flag is true. Password and signature fields throw `PdfFieldValueException` and do not dirty the document.
- **Certification follows DocMDP `P`.** `P` 1 blocks fills, attachment edits, metadata writes, and `Sign`. `P` 2 and `P` 3 allow fills and `Sign`. Metadata and attachments stay blocked. A certification that does not resolve is treated as `P` 1. FieldMDP `All` blocks every fill. `Include` allows only the named fields. `Exclude` blocks the named fields. Reads still work. An incremental save leaves an existing signature byte range in place.
- **PKCS#11 stays in the host.** The library does not P/Invoke a PKCS#11 module. The host implements `IPdfExternalSigner.SignHash` so that it matches `RSA.SignHash(hash, SHA256, Pkcs1)`. The argument is the raw 32-byte SHA-256 digest. `CKM_SHA256_RSA_PKCS` would hash it again. `AdditionalCertificates` is the chain beyond the leaf, and those certificates are embedded in the CMS signature.
- **Timestamps.** `CmsDetachedSignatureProvider` can take an `ITimestampAuthority`. It hashes the RSA signature value, requests an RFC 3161 token with a random nonce and no `certReq`, and stores that token as the unsigned attribute `1.2.840.113549.1.9.16.2.14`. The provider rejects a token whose message imprint, digest algorithm, or nonce does not match the request. A document timestamp is still a second revision from `DocTimeStampSignatureProvider`, and that provider applies the same check. `HttpTimestampAuthority` POSTs `application/timestamp-query` and accepts PKI status 0 or 1. A response body larger than 2 MiB (2,097,152 bytes) throws `PdfTimestampException`, and the body read stays under `HttpClient.Timeout`.
- **Failed signs do not write.** The provider runs after `/ByteRange` is patched and before the appendix is appended. A throw, or a token larger than the reserved gap, leaves the destination length unchanged. Exceptions from the host's `IPdfExternalSigner.SignHash` are replaced with a generic `CryptographicException` without the provider's message, data, or inner exception.
- **Not thread-safe.** Dispose does not save. `leaveOpen: false` disposes the active stream.

## Out of scope

Rendering, page-content text extraction, reflow, merge, split, rotate, creating a PDF from scratch, rewriting the file in place, creating encryption, public-key encryption, signature validation and revocation, PAdES-LTV, ECDSA, XFA, JavaScript, annotation editing, and redaction. Page content streams are never decoded. Filters other than Flate, LZW, RunLength, ASCIIHex, and ASCII85 (and the abbreviations `Fl`, `LZW`, `RL`, `AHx`, and `A85`) throw when their bytes are actually required.

## Support matrix

Read-only compatibility and modification compatibility are separate. Opening a file, including a signed file, is a read. A later fill, metadata write, attachment edit, or signature is allowed only when the rows below allow that operation. There is no experimental tier. The operations in **Out of scope** stay excluded.

| Topic | Read | Modify |
|---|---|---|
| PDF versions | `%PDF-1.0` through `%PDF-1.7` open. Offsets include a preamble before `%PDF-`. PDF 2.0 xref and metadata behavior that shows up in those files is read with them. | An incremental save keeps the original header. The library does not create a PDF. |
| Cross-reference | Classic tables (CRLF, LF, and the 20-byte space-LF and space-CR rows), xref streams (Flate and PNG predictors), hybrid `/XRefStm`, and object streams. Cross-reference streams stay plaintext. An encrypted xref stream fails `Open`. `/Encrypt` is an uncompressed object found from the xref map. | Writes are classic xref only. Edited objects that lived in an object stream are written as normal objects. `/Size` is not an array. |
| Encryption | Revision 2 is 40-bit RC4. Revision 3 is RC4 at 40 to 128 bits. Revision 4 is AES-128 or RC4, chosen by the crypt filter, so strings and streams can differ. Revision 5 and revision 6 are AES-256. Strings and streams come back decrypted. Public-key encryption is refused. A null password tries the empty user password. `/EncryptMetadata false` leaves XMP in the clear. `/EFF /Identity` leaves embedded-file streams in the clear. | New strings and streams are encrypted with that same cipher. The new trailer keeps `/Encrypt` and `/ID`. Signature dictionaries stay plaintext. The library does not create encryption. A user password is limited to the permission bits. The owner password is not. An unencrypted document allows every operation. |
| AcroForm | Fields report the dotted name, value, rectangle in user space, page number, rotation, and media box. `Pages` also reports the crop box. No AcroForm means no fields. | WinAnsi text and choice values get a Helvetica appearance and `/NeedAppearances false`. Check boxes and radios reuse an existing on-state. A check box with no appearance gets a generated `Yes`/`Off` mark. One radio widget with no on-state gets a mark named for its export value. Comb, rich text, and non-WinAnsi values set `/NeedAppearances true`. Signing throws while that flag is true. A choice value that is not listed throws `PdfFieldValueException` unless the Edit flag is set. Password and signature fields throw `PdfFieldValueException` and do not dirty the document. |
| Signing | An existing signature is carried as a prefix of a later incremental save. DocMDP `P` and FieldMDP are read with the file. A certification that does not resolve is treated as `P` 1. Opening the file does not allow a fill, a metadata write, an attachment edit, or another signature. | `P` 1 blocks fills, attachment edits, metadata writes, and `Sign`. `P` 2 and `P` 3 allow fills and `Sign`. Metadata and attachments stay blocked. FieldMDP `All` blocks every fill. `Include` allows only the named fields. `Exclude` blocks the named fields. Reads still work. `Sign` appends one `/adbe.pkcs7.detached` CMS signature, RSA with SHA-256 and PKCS#1, through the host signer. An RFC 3161 token is an unsigned attribute on that signature: random nonce, no `certReq`, rejected when the imprint, digest algorithm, or nonce does not match. A document timestamp is a separate `/ETSI.RFC3161` revision. A signature appendix larger than 8 MiB is refused. |
| Metadata | The information dictionary can be read by key, including `Trapped` (`True`, `False`, or `Unknown`). XMP can be read. Malformed XMP does not fail `Open`. | Info keys and XMP can be written when certification and the user password allow it. Other info edits refresh `ModDate` in UTC. A certified file blocks metadata writes at every `P` level. |
| Attachments | Embedded files can be listed and opened by name. Flate, LZW, RunLength, ASCIIHex, and ASCII85 (and `Fl`, `LZW`, `RL`, `AHx`, and `A85`) are decoded when the bytes are required. Any other filter throws. | `Add` and `Remove`. Copying a new attachment reads at most 64 KiB at a time from a seekable content stream. A certified file blocks attachment edits at every `P` level. |
| Limits | `Open` requires a readable seekable stream. Offsets are `long`. The 32 MiB and 64 MiB caps are decompression limits, and the bound for one structural stream. The type is not thread-safe. Dispose does not save. | Same-stream saves append. Cross-stream saves copy with a 1 MiB buffer into an empty destination, then append. Classic xref offsets stop at 9,999,999,999. A file past a cap throws before the destination grows. |

## Usage

```csharp
using jaytwo.Ergonomics.Pdf;
using jaytwo.Ergonomics.Pdf.AcroForm;
using jaytwo.Ergonomics.Pdf.Attachments;
using jaytwo.Ergonomics.Pdf.Metadata;
using jaytwo.Ergonomics.Pdf.Signatures;

using var document = PdfDocument.Open(stream, leaveOpen: true);

foreach (PdfField field in document.AcroForm.Fields)
{
    Console.WriteLine($"{field.FullName} page {field.PageNumber} {field.Rect.Width}x{field.Rect.Height} checked={field.IsChecked}");
}

document.AcroForm.SetText("Name", "Ada");
document.Metadata.Title = "Signed copy";
document.Metadata["Department"] = "Forms";
document.Attachments.Add("notes.bin", notes, leaveOpen: true);

PdfPage page = document.Pages[0];
document.Sign(
    new PdfSignatureOptions
    {
        PageNumber = page.Number,
        Rect = new PdfRect(page.CropBox.Urx - 180, 36, page.CropBox.Urx - 36, 72),
    },
    new CmsDetachedSignatureProvider(pkcs11Signer));
```

Pass the password to `Open(stream, password)` when the file requires one. `Open(stream)` tries the empty user password, which is how a restrictions-only file opens. The permission bits still apply. `Permissions` reports them for either password: an owner open can modify the file and still shows the user-password limits. An unencrypted file reports every operation allowed. Supported files are revision 2 or 3 (RC4), revision 4 (AES-128 or RC4), and revision 5 or 6 (AES-256). Revision 5 takes the UTF-8 password, cut at 127 bytes. Revision 6 prepares that password first, so a decomposed password matches its composed form. `SetText`, `SetCheckBox`, `SetRadio`, and `SetChoice` return a `PdfAppearanceDisposition` so the caller can see whether a viewer still has to regenerate appearances. `SaveIncremental()` with no destination appends to the opened stream, and so does `Sign(options, provider)`. Pass an empty destination stream to `SaveIncremental` or `Sign` when the opened file must stay unchanged. A large attachment that does not fit in the 8 MiB signature appendix is `SaveIncremental` to that private stream, then `Sign` on the same private stream. Replace the visible file only after `Sign` returns.

`Pages` reports each page number, media box, crop box, and rotation, which is enough to place a signature rectangle. `Sign` reuses an existing empty signature field named by `FieldName` and keeps its `/Rect` and `/P`. A null `FieldName` creates `SignatureN`. Signing that field again throws `PdfSignException`. A name that is not a signature field throws `PdfSignException`. `AcroForm.GetFields` looks a field up by its dotted name. Check boxes and radios expose `IsChecked`, so the on-state name does not have to be guessed. Choice fields expose `/Opt` as `Options`; `SetChoice` writes those export values, and a multi-select value is a PDF array. `Metadata["Key"]` reads and writes the information dictionary, including private keys. `Trapped` stays a name (`True`, `False`, or `Unknown`). Attachments can be opened by name.

PDF 1.0 through 1.7, the operations above, standard security revisions 2 through 6, and the caps above. A file past a cap throws before the destination grows. This is not a promise that every conforming PDF can be modified.

## Build

```bash
dotnet test jaytwo.Ergonomics.Pdf.sln
```

The package targets `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0`. The `netstandard2.0` build depends on `System.Memory`. Acceptance criteria for the tests live in `ACCEPTANCE.md`.
