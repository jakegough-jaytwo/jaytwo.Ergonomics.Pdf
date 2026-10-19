# Fuzz targets

Each target is one function over a byte span in `tools/fuzz`. A target catches only the exceptions listed for it. Everything else escapes and is a finding. Keep this table and the harness in step.

| Target | Entry point | Exercises | Expected exceptions | Seeds |
|---|---|---|---|---|
| `open` | `PdfDocument.Open` with a null password, then a read walk: permissions, pages and boxes, every field's name, type, value, options, and widgets, Info and XMP, attachment names and bounded content reads, signature fields | header, classic xref, xref streams, hybrid `/XRefStm`, `/Prev` chains, object streams, every filter, standard security with the empty user password, page tree, field tree, metadata, name trees | `PdfException` and its subclasses | `test/.../Fixtures/*.pdf`, builder variants, minimized findings |
| `edit` | `Open`, then one fill of each field kind, a metadata and XMP write, an attachment add and remove, `SaveIncremental` to a new stream, reopen and read, then `Sign` with a self-signed test key; both outputs must keep their input as a prefix | incremental writer, appearance generation, encryption on write, DocMDP and FieldMDP gates, signature placement, classic-xref output | as `open` | as `open` |
| `object` | `PdfParser.ParseValue` over the input with no resolver | tokens, strings, hex strings, names, numbers, arrays, dictionaries, nesting, comments | `PdfException` | object fragments cut from the fixtures |
| `objstm` | `PdfParser.ParseObjectStream`; a leading `N First` text line gives `/N` and `/First`, filtered by the same guards as `PdfEngine.ParseObjectStream` | object-stream index, offsets, counts | `PdfException` | builder object-stream bodies |
| `decode` | a stream dictionary, `stream
`, then the data; `FiltersOf` and `ParmsOf` read the dictionary, then both `Decode` with the structural cap and the streaming `OpenDecoded` run | LZW, Flate, RunLength, ASCIIHex, ASCII85, PNG and TIFF predictors, decompression amplification | `PdfException` | encoded samples per filter |
| `der` | `DerCodec.ReadTlv` walked recursively, then `ReadCertificateIdentity` | DER lengths, nesting, truncation | `PdfException` | CMS from the signed fixtures, OpenSSL tokens, a TSA certificate |
| `tsr` | `HttpTimestampAuthority.ExtractToken` (mode 0) or the raw token, then `Rfc3161TimeStampToken.RequireMatch`; the input carries the expected imprint and nonce | RFC 3161 response status, token structure, TSTInfo, ESS certificate identifiers | `PdfException` | OpenSSL and pyHanko responses |
| `xmp` | `XmpEditor.TryParse`, `ApplyInfo`, `ToBytes` | XML parsing, DTD and entity expansion, XMP editing | `PdfXmpException` from `ApplyInfo`, which `PdfEngine.SyncXmp` treats as malformed XMP; `TryParse` reports parse failures | XMP packets from the metadata fixtures |

Per-input limits for a campaign: 10 seconds, 2048 MiB RSS, and input length at most 1 MiB for the `open` and `edit` targets and 64 KiB for the others. A stack overflow, an `OutOfMemoryException`, an `IndexOutOfRangeException`, a `NullReferenceException`, an `InvalidCastException`, an `ArgumentException`, an `OverflowException`, or a `KeyNotFoundException` escaping a target is always a finding.

The fixed-seed smoke run (`make fuzz-smoke`, part of `make unit-test`) runs every seed, the committed `tools/fuzz/regressions/<target>/` inputs, and 2000 deterministic mutations per target. Campaigns run with `make docker-fuzz-campaign` or `tools/fuzz/run-campaign.sh` in the Dockerfile `fuzz` stage, and add qpdf encryption and object-stream variants and OpenSSL timestamp responses from `tools/fuzz/external-seeds.sh`.

Paths not fuzzed directly, and why:

- Signature construction consumes library-built bytes and host callbacks, not untrusted bytes. It is reached through `edit` on signed inputs for the certification gate only.
- Password-derived keys for encrypted seeds other than the empty user password are reached only through the `open` seeds that use one.
