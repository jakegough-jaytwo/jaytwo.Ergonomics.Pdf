# W-209 findings: decoder use at required decoding sites

Research only. Baseline `fe6243df337d71335514601f8ba763be0de8fd31` (W-208 completion; clean tree). No runtime, test, build, shipped-contract or protected-file change. Companion: `proposal.md`. Nothing here is approved. Authoritative PDF text was read from `C:/jake/git/pdfdocs/text` (ISO 32000-1 and -2, clauses cited below).

Method labels: **measured** = produced by a script or probe in this directory and reproducible with `run.ps1` and `inventory.py`; **inferred** = reasoning from code, spec or general PDF knowledge, not run here.

## 1. Where the library must decode (code inventory; measured by call-site search, `graphify` plus `grep`)

`PdfStreamDecoder.Decode` and `PdfStreamDecoder.OpenDecoded` are the only entry points. They are reached from exactly four places; nothing else in `src/` decodes a stream body.

| Site | Code | Decoder path | Cap |
| --- | --- | --- | --- |
| Cross-reference stream | `PdfDocumentLoader.cs:341` (`Decode`) | byte arrays; `/Filter` and `/DecodeParms` must be direct (ISO 32000-1 7.5.8.2) | 64 MiB (`XrefCap`) |
| Object stream | `PdfObjectStore.cs:257` via `ReadDecodedAsync` (`Decode`) | byte arrays | 32 MiB (`StructuralCap`) |
| Catalog `/Metadata` (XMP) | `PdfInfoMetadata.cs:310` `ReadDecoded` (`Decode`) | byte arrays | 32 MiB |
| Embedded file | `PdfEmbeddedFiles.cs:183` `OpenBody` to `OpenDecoded` | streaming wrappers | caller `maxDecodedBytes` (default unbounded, `PdfAttachment.Open()`) |

Not decoder demand: the new-file copier reads only `/Filter` and `/DecodeParms` to detect a `Crypt` filter (`PdfTypedObjectCopier.cs:117`), then copies the stream bytes still encoded; page content, image, font and appearance streams are never decoded (AGENTS.md; ACCEPTANCE line 22: "A page content stream is not decoded, including when its filter is `LZWDecode`"). Phase 1 appends never decode a copied stream either.

Two implementations of ASCIIHex and ASCII85 exist because the sites differ: array decoders `AsciiHex()` and `Ascii85()` (xref, object stream, XMP) and stream decoders `AsciiHexDecodeStream` and `Ascii85DecodeStream` (attachments). LZW and RunLength have one stream decoder each, wrapped for the array path.

## 2. Resolved-stream inventory over the pinned corpus (measured; `inventory.py`, `inventory.json`)

Corpus: the 51 tracked PDFs of `W-188/fixture-manifest.csv`, every SHA-256 re-checked (0 mismatches). qpdf 12.3.2 resolves objects inside object streams; cross-reference sections are read by following `startxref`, `/Prev` and `/XRefStm` and reading only the header at each offset (no whole-file scan). Seven password-protected fixtures were opened with the passwords in `corpus/manifest.json` and the tests (`corpus-user`, `user-secret`). **One file is not resolved**: `chrome-invoice-pyhanko-pubkey.pdf` is public-key encrypted (qpdf: "unsupported encryption filter"); only its cross-reference chain was read. This is a stated coverage gap, not a zero.

| Role | Streams | Files | Filter chains found | Abbreviations | PNG predictor |
| --- | --- | --- | --- | --- | --- |
| Cross-reference stream | 33 (plus 42 classic xref tables) | 18 | FlateDecode 33 | 0 | 26 |
| Object stream | 255 | 18 | FlateDecode 255 | 0 | 0 |
| XMP metadata | 25 | 25 | unfiltered 24, FlateDecode 1 | 0 | 0 |
| Embedded file | 22 | 7 | FlateDecode 21, unfiltered 1 | 0 | 0 |

- **Required streams using any of ASCIIHex, ASCII85, LZW or RunLength: 0 of 335**, in 50 of 51 PDFs. Abbreviated filter names in any stream, required or not: 0.
- Streams the library only copies or ignores (3,732 Flate, 413 unfiltered, 41 CCITTFax, 16 DCT, 8 JPX, 4 JBIG2, **3 RunLength**) are listed separately and are not decoder demand. The only candidate-filter streams in the whole corpus are those 3 RunLength streams, all image data in `imagemagick-scan.pdf` (ImageMagick output), never decoded by this library. LZW, ASCII85 and ASCIIHex occur nowhere in the corpus, required or not.
- Therefore the corpus cannot exercise these decoders through any real-world file. The tests that do (`LzwTests.cs`, `RunLengthTests.cs`) use synthetic bytes.

Limits of this inventory (measured facts about coverage, not a verdict on need): 51 files chosen by the project, mostly 2000s to 2020s producers (Chrome, Office, LibreOffice, pyHanko, government forms); pre-1.5 files cannot contain cross-reference or object streams (ISO 32000-1 7.5.7 and 7.5.8 introduce them in PDF 1.5), so the xref and object-stream sites say nothing about legacy producers by construction. "Missing use in this corpus is not proof of no business need" (W-209 acceptance criteria).

## 3. Minimal required-stream fixtures and probes (measured; `gen_fixtures.py`, `probe/Program.cs`, `observations.jsonl`)

149 deterministic PDFs, 20.5 MB, one candidate stream each at one required site (the other streams are plain or Flate). All are built from the same minimal catalog, one page and (for attachments) a 4,096-byte payload with repeated and zero runs. Every non-malformed fixture decodes to the same bytes in `qpdf --check` (no warnings, `qpdf-check.json`); the encoders for ASCIIHex, ASCII85, RunLength and LZW (EarlyChange 0 and 1, table-full clear) are also round-tripped through pypdf 6.21.1 in the generator. This engine's own decoders are therefore not the oracle.

- **Valid: 64 of 64 decode exactly** (16 variants x 4 sites): Flate; Flate with PNG predictor; ASCIIHex; ASCII85; LZW; LZW EarlyChange 0; LZW with PNG predictor (Up); RunLength; the abbreviations `Fl`, `AHx`, `A85`, `LZW`, `RL`; chains `A85+LZW`, `AHx+Flate`, `A85+RL`. Per-case time at most 12.0 ms (first case includes JIT, a warm-up precedes); allocation 81 KB to 361 KB.
- **Malformed (15 per site, 60 cases)** - behavior of the shipped decoders:

| Filter | Input | Result at the four sites |
| --- | --- | --- |
| ASCIIHex | non-hex character | `PdfParseException` (xref: at `Open`) |
| ASCIIHex | odd digit count | accepted; last digit padded with 0 (spec-conformant, ISO 32000-1 7.4.2) |
| ASCIIHex | no `>` EOD | accepted (lenient) |
| ASCII85 | character above `u` | `PdfParseException` |
| ASCII85 | no `~>` EOD | accepted (lenient) |
| ASCII85 | group value above 2^32 - 1 | **accepted** at the attachment site (returned 4,100 bytes for a 4,096-byte payload); failed only downstream elsewhere; spec 7.4.3 says error |
| ASCII85 | final partial group of one character | **accepted**, character ignored (output identical to the valid stream); spec 7.4.3 says error |
| LZW | code outside the table | `PdfParseException` ("LZW code is outside the table.") |
| LZW | truncated | `PdfParseException` ("LZW stream is truncated.") |
| LZW | no leading clear code | accepted |
| RunLength | truncated run | `PdfParseException` ("RunLength stream is truncated.") |
| RunLength | no EOD | accepted |
| RunLength | bytes after EOD | accepted, ignored (matches ACCEPTANCE line 80) |
| Flate (baseline) | truncated; wrong Adler-32 | `PdfParseException` (strict) |

- At the XMP site, malformed data that decodes to bytes that are not XML surfaces as `PdfXmpException`; at xref and object-stream sites a lenient or wrong decode surfaces as a `PdfParseException` about the structure, not the filter.
- **Unsupported filters (3 per site, 12 cases)**: `/DCTDecode`, `/FooDecode`, and `[/ASCIIHexDecode /DCTDecode]` all throw `PdfUnsupportedFilterException("The stream filter /X is not supported.")` today. That exception, with that message, is the error a removal would reuse; no new error type is needed (the chain case reports the unsupported member, not the first filter). **Where it surfaces and what survives** (field `stage` and `pagesAfterFailure` in `observations.jsonl`):

| Site | Failure surfaces at | Rest of the open document |
| --- | --- | --- |
| Cross-reference stream | `PdfDocument.Open` | document unreadable (no document object) |
| Object stream holding the page tree | first page access (`PageCount`) | `PageCount` keeps failing; pages, forms, attachments of that tree unreadable |
| XMP | `Metadata.OpenXmp()` | document usable (`PageCount` = 1) |
| Embedded file | `Attachment.Open()` | document usable (`PageCount` = 1) |

- **Decompression limits (13 cases, bombs just above the cap)**: every filter, including Flate, stops at the cap with `PdfObjectTooLargeException` (structural sites) or `PdfAttachmentTooLargeException` (attachments, 1 MiB caller cap). Allocation to refuse a 32 MiB-plus structural stream: 64 MiB for Flate, RunLength and LZW (inputs 33 KB, 525 KB, 26 KB), but **424 MiB for ASCII85** (an 8.4 MB input of `z`, four zero bytes each): the ASCII85 array decoder has no in-flight cap and the check ("Decoded stream is 33555456 bytes, above the 33554432 byte cap") happens after full decode, about 12.6 times the cap. The ASCIIHex stream decoder at the attachment site read 2.1 MB of input and allocated 66 MiB to refuse a 1 MiB limit (the others stayed under 10 MiB). Object-stream rows in `observations.jsonl` show exactly twice the XMP figures (128 MiB, 848 MiB for ASCII85) because the probe retries `PageCount` once after the failure to test usability, and the retry decodes again; per-attempt cost equals the XMP row. A failed decode is not cached. The xref site's 64 MiB cap was not exercised (inferred equal in kind).
- **Attachment note**: `maxDecodedBytes` is checked while streaming, so RunLength, LZW and Flate bombs cost 2.0 to 2.5 MiB; ASCII85 cost 10.1 MiB.

## 4. Quantities the criteria ask for

**Compatibility loss if a filter becomes "unsupported" at required sites**

- Measured in the pinned corpus: 0 of 335 required streams (0 of 50 resolved PDFs), 0 abbreviated names anywhere. The 3 RunLength image streams are not affected (copy path).
- Not measured, therefore not claimed: any file outside the corpus. Inferred from PDF history (not verified here): ASCII85 (often with LZW) was the default of 1990s Distiller and PostScript-derived writers for content and images, which this library never decodes; those writers produced no XMP before PDF 1.4 and no xref or object streams before PDF 1.5, so exposure is limited to XMP and embedded files in PDF 1.3 and 1.4 era files and to unusual later writers. ISO 32000-1 7.5.8's own cross-reference stream example uses `/ASCIIHexDecode`, but no producer using it was found. ISO 32000-2 7.4.4 keeps LZWDecode (title "LZWDecode and FlateDecode filters"; a text search found no deprecation sentence).
- Abbreviations are an inline-image feature (ISO 32000-1 Table 94, 8.9.7); the filter-name table for stream dictionaries (7.4.1) names no abbreviations. Accepting `Fl`, `LZW`, `RL`, `AHx` and `A85` in a stream dictionary is therefore a leniency beyond the standard that AGENTS.md's tolerated-quirk rule (closed list, real-world fixture, test, `docs/behavior.md` entry) does not currently support with a fixture. Corpus use: 0.

**Unsupported-filter error**: already defined and tested at the four sites (section 3). Wording unchanged.

**Caller recovery outside this library**: the public API returns only decoded attachment and XMP bytes (`PdfAttachment.Open`, `PdfMetadata.OpenXmp`); no member returns the encoded stream, and ACCEPTANCE line 78 pins that DCT "does not return the compressed bytes". A caller of a refused file must use another tool: qpdf (`--decode-level` and `--show-object`) or pypdf on the same bytes. That is a second tool and, for a service, a second language runtime or native process; it is not an in-library path. For cross-reference and object-stream refusals nothing in the document is readable, so recovery means rewriting the file with an external tool before opening. Inferred cost: tool installation and operational ownership, no credentials.

**Real implementation responsibility saved (measured by `loc.py`, `loc.json`; brace-matched lines including blanks and comments)**

| Candidate | Source lines in `PdfStreamDecoder.cs` | Test files (facts/theories) |
| --- | --- | --- |
| ASCIIHex + ASCII85 (two decoders each) | 367 (112 + 141 + 35 + 51 + 9 + 19) | none dedicated; mentions in `IndirectFilterTests`, `HardeningTests` |
| LZW (stream decoder, wrapper, EarlyChange) | 236 (220 + 6 + 10) | `LzwTests.cs`, 379 lines, 10 |
| RunLength | 123 (117 + 6) | `RunLengthTests.cs`, 226 lines, 8 |
| Total | 726 of 1,547 (46.9%) | plus 4 to 8 mentions in `PageConcatenationTests`, `OpenTests`, `ForeignPdfTests`, `WorkCounterTests`; fuzz `SeedWriter.cs` (8) and `Mutator.cs` (4) |

Dispatch branches in `Wrap`, `DecodeOne`, `Canonical` and `IsSupported` add about 40 lines (inferred from a read, not counted). The Flate path, `Unpredict` (shared; 26 of 33 corpus xref streams use a predictor), caps, work counters and `PdfUnsupportedFilterException` stay either way. Documentation touched by a removal: ACCEPTANCE lines 79 and 80 (and 22, 78 unchanged), `docs/behavior.md` line 43, the decode bullet of AGENTS.md (protected; owner), possibly QUALIFICATION.md rows for filters (grep found none). **Effort range** (inferred from the counts; no spike): abbreviations only 0.25 to 0.5 day; RunLength 0.5 day; ASCIIHex + ASCII85 0.5 to 1 day; LZW 0.5 to 1 day; all together 1.5 to 2.5 days including replacing deleted tests with unsupported-filter tests at the four sites and the doc updates. Savings overlap: the ASCII85 and ASCIIHex array decoders disappear with the stream decoders; the fuzz seeds and mutator entries shrink together.

## 5. Observations outside this item's recommendation (routed, not acted on)

1. **ASCII85 accepts two inputs the spec calls errors** (value above 2^32 - 1; final one-character group), measured at the attachment site, and AGENTS.md says reading is strict to ISO 32000 with a closed quirk list. The `z`-inside-a-group case was not probed. Small fix or a quirk entry with a fixture; owner choice.
2. **ASCII85 and ASCIIHex array decoders are not bounded while running** (424 MiB allocated to refuse a 32 MiB stream; 66 MiB for the ASCIIHex attachment bomb), unlike Flate, LZW and RunLength. Inputs are bounded by the file; the ratio is 12.6 times the cap. Relevant to W-149/W-168 work budgets; if the filters are retained this is a candidate bounded hardening item, if ASCII85/ASCIIHex are removed it disappears.
3. A failed decode is re-attempted on each access (object-stream `PageCount` twice allocated twice). Low priority; cached failure is a design choice.

## 6. Measured versus inferred; gaps

Measured: sections 1 (static call-site search), 2, 3, the line counts in 4. Inferred: legacy producer behavior, effort ranges, recovery cost, the 64 MiB xref cap behavior, dispatch line counts, and that removal would reuse the existing exception unchanged.

Missing tools or coverage: the public-key encrypted corpus file; a corpus of older or non-mainstream producers (none available; W-188's four review attachments remain missing); Linux, net8.0/net6.0/net48 and `netstandard2.0` runs; peak-memory measurement beyond `GC.GetTotalAllocatedBytes` and process peak working set; xref-site bombs; the `z`-in-group A85 error; EU DSS, Poppler and MuPDF cross-reads of the fixtures (qpdf and pypdf only); gigabyte files. No independent reviewer is assigned and none is claimed.
