# Work budgets: baseline measurements and amplification analysis

Input to the W-149 decision on cumulative work budgets. W-167 wrote this report, and W-169 corrected it after the owner's review. It records:

- **Measured** results: counts observed on existing benign fixtures, from the commands in section 2.
- **Analysis**: amplification paths read from the code. They are not measured yet. W-168 owns the purpose-built adversarial fixtures that measure them.

This report proposes no strategy. W-149 cannot decide until W-168 adds adversarial evidence here.

## 1. Instruments

`jaytwo.Pdf` can keep a per-document `PdfWorkCounters`, internal and read by the tests through `PdfDocument.WorkCounters`. Counting is opt-in (W-169): a document has counters only when it opens inside a `PdfWorkCounters.Collect()` scope. An ordinary document has none, and each counting site only tests for null. Counting changes no behavior. The counters run from `Open` and are never reset.

| Counter | Counts |
| --- | --- |
| stage decodes, stage bytes | Each filter applied to a stream read whole (object streams, cross-reference streams, XMP), and the bytes each stage produced, intermediate stages included. `DecodedByteCount` (existing) counts only each stream's final output. Streamed attachment content (`OpenBody`) is not counted. |
| objstm parses | Each decode and parse of an object stream, including a re-decode after a D-49 release |
| objstm evictions, cached evictions | Object streams released by `ReleaseCleanObjectStreams`, and the cached objects released with them |
| object loads | Objects parsed on a cache miss, including object-stream members |
| resolutions | Indirect references followed |
| page visits, field visits | Page-tree and field-tree nodes entered by `WalkPages` and `WalkField` |

The tests measure source I/O with `ProbeStream` (bytes and read calls on the source stream only, not the destination) and memory with `WorkloadMeter` (peak managed-heap growth and allocated bytes).

## 2. Reproduction

Environment: Windows 11 IoT Enterprise LTSC 2024, .NET SDK 10.0.402, `net10.0`, Release, qpdf from `.tools/` (`tools/test-tools/install.ps1`). Revisions: the W-167 commit, whose parent is `86a10da`, and the W-169 commit. W-169 re-ran the baseline with opt-in counting. Every count matched, except two signed corpus files: `gpo-bills-118hr815enr` and `pyhanko-signed` each make one more read call, about 4 to 37 KB more source bytes, because W-170 re-reads each signature dictionary to locate its `/Contents`. The tables keep the W-167 values.

```sh
dotnet test test/jaytwo.Pdf.Tests -f net10.0 -c Release --nologo -p:CollectCoverage=false \
  --filter "FullyQualifiedName~WorkBaselineTests" --logger "console;verbosity=detailed"
```

`WorkBaselineTests.object_stream_form_operations` uses the G7 form shape: `G7Fixtures.WriteForm`, rewritten by qpdf with object streams and a cross-reference stream. That is the shape of `LargeWorkloadTests`. Each operation opens the file again, so every row includes the open. The "open" row is `Open` plus `PageCount`. Flatten and sign run on a filled copy. `corpus_read_operations` runs every G2 corpus entry that opens. On one document it reads pages, metadata, fields, export, every attachment's content, signatures, and revisions. The counts are deterministic for a given revision. Time and heap vary from run to run.

## 3. Measured results

### 3.1 G7 form shape, 1,000 pages: 511,983 bytes, 41 object streams (filled copy 940,021 bytes)

| Operation | Source bytes | Reads | Decoded | Stage bytes | Objstm parses | Objstm evict. | Cached evict. | Loads | Resolutions | Page visits | Field visits | Peak heap MiB | Alloc MiB | s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| open | 275,244 | 49 | 640,176 | 640,176 | 43 | 43 | 1,011 | 1,053 | 1,012 | 1,011 | 0 | 3.7 | 8.2 | 0.04 |
| pages | 275,244 | 49 | 640,176 | 640,176 | 43 | 43 | 1,011 | 1,053 | 1,012 | 1,011 | 0 | 5.9 | 8.3 | 0.04 |
| fields | 455,216 | 155 | 2,136,518 | 2,136,518 | 149 | 149 | 5,011 | 5,053 | 4,013 | 1,011 | 3,000 | 6.3 | 28.5 | 0.14 |
| fields twice | 706,379 | 303 | 4,225,237 | 4,225,237 | 297 | 297 | 10,022 | 10,064 | 8,026 | 2,022 | 6,000 | 7.8 | 55.9 | 0.23 |
| export | 455,216 | 155 | 2,136,518 | 2,136,518 | 149 | 149 | 5,011 | 5,053 | 4,013 | 1,011 | 3,000 | 6.2 | 28.6 | 0.12 |
| metadata edit, save | 826,651 | 74 | 965,974 | 965,974 | 66 | 66 | 3,000 | 3,043 | 3,024 | 0 | 0 | 6.2 | 14.6 | 0.05 |
| bulk fill, save | 1,036,558 | 198 | 2,714,685 | 2,714,685 | 190 | 190 | 6,011 | 6,053 | 5,018 | 1,011 | 3,000 | 12.4 | 45.5 | 0.20 |
| flatten, save | 2,599,392 | 313 | 4,112,896 | 4,112,896 | 289 | 289 | 11,022 | 13,066 | 9,035 | 2,022 | 3,000 | 17.9 | 73.7 | 0.31 |
| sign | 2,780,376 | 452 | 2,979,066 | 2,979,066 | 208 | 208 | 9,012 | 9,065 | 8,031 | 2,022 | 3,000 | 6.1 | 45.6 | 0.20 |

### 3.2 G7 form shape, 4,000 pages: 2,046,184 bytes, 161 object streams (filled copy 3,783,224 bytes)

| Operation | Source bytes | Reads | Decoded | Stage bytes | Objstm parses | Objstm evict. | Cached evict. | Loads | Resolutions | Page visits | Field visits | Peak heap MiB | Alloc MiB | s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| open | 688,533 | 172 | 2,535,537 | 2,535,537 | 163 | 163 | 4,011 | 4,173 | 4,012 | 4,011 | 0 | 8.8 | 31.9 | 0.15 |
| pages | 688,533 | 172 | 2,535,537 | 2,535,537 | 163 | 163 | 4,011 | 4,173 | 4,012 | 4,011 | 0 | 9.0 | 32.2 | 0.13 |
| fields | 1,388,977 | 590 | 8,640,780 | 8,640,780 | 581 | 581 | 20,011 | 20,173 | 16,013 | 4,011 | 12,000 | 11.7 | 112.7 | 0.48 |
| fields twice | 2,364,304 | 1,170 | 17,126,765 | 17,126,765 | 1,161 | 1,161 | 40,022 | 40,184 | 32,026 | 8,022 | 24,000 | 19.3 | 221.5 | 0.93 |
| export | 1,388,977 | 590 | 8,640,780 | 8,640,780 | 581 | 581 | 20,011 | 20,173 | 16,013 | 4,011 | 12,000 | 11.7 | 112.1 | 0.46 |
| metadata edit, save | 2,887,940 | 270 | 3,897,050 | 3,897,050 | 258 | 258 | 12,000 | 12,163 | 12,024 | 0 | 0 | 8.2 | 53.5 | 0.21 |
| bulk fill, save | 3,707,501 | 754 | 11,003,768 | 11,003,768 | 742 | 742 | 24,011 | 24,173 | 20,018 | 4,011 | 12,000 | 39.5 | 175.8 | 0.91 |
| flatten, save | 10,561,588 | 1,211 | 16,725,331 | 16,725,331 | 1,129 | 1,129 | 44,022 | 52,186 | 36,035 | 8,022 | 12,000 | 55.4 | 289.7 | 1.52 |
| sign | 11,126,005 | 1,773 | 12,053,255 | 12,053,255 | 808 | 808 | 35,975 | 36,185 | 32,031 | 8,022 | 12,000 | 13.3 | 175.6 | 0.79 |

### 3.3 G2 corpus, read operations on one document

All 40 entries that open completed every read without an exception. Below are the entries whose object streams were parsed. The other 26 have no object streams, or have object streams that the reads never parsed. Their stage decodes are at most 1, they decode at most 4,067 bytes, and each finishes in at most 0.05 s with a peak heap of at most 6.6 MiB (`chrome-ledger-2000p`: 2,287 page visits).

| Entry | File bytes | Object streams | Source bytes | Decoded | Objstm parses | Parses per stream | Cached evict. | Loads | Field visits | Peak heap MiB | Alloc MiB | s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| faa-8710-1 | 409,501 | 28 | 2,372,138 | 7,637,561 | 446 | 15.9 | 1,591 | 1,612 | 1,564 | 7.2 | 87.3 | 0.46 |
| uscis-i9-2025 | 524,095 | 24 | 1,992,757 | 6,371,511 | 363 | 15.1 | 625 | 640 | 548 | 10.8 | 79.0 | 0.44 |
| cms-40b | 108,533 | 12 | 624,079 | 771,146 | 79 | 6.6 | 210 | 223 | 188 | 5.8 | 9.5 | 0.05 |
| cms-40b-pyhanko-approved | 141,868 | 12 | 888,710 | 770,787 | 78 | 6.5 | 210 | 223 | 188 | 5.8 | 9.7 | 0.05 |
| va-21-4138 | 881,905 | 10 | 712,545 | 306,793 | 41 | 4.1 | 164 | 175 | 144 | 5.2 | 5.2 | 0.02 |
| grants-sf424-v4 | 149,909 | 1 | 122,971 | 60,203 | 7 | 7.0 | 15 | 18 | 0 | 1.2 | 1.2 | 0.01 |
| irs-f1040es-2026 | 331,490 | 50 | 846,601 | 351,855 | 62 | 1.2 | 571 | 603 | 516 | 6.0 | 6.0 | 0.02 |
| opm-of306 | 171,239 | 13 | 563,535 | 262,753 | 27 | 2.1 | 290 | 302 | 272 | 6.0 | 6.0 | 0.02 |
| opm-of306-pyhanko-certified | 179,882 | 13 | 606,840 | 182,149 | 22 | 1.7 | 287 | 301 | 276 | 4.4 | 4.4 | 0.02 |
| irs-fw9-2024-03 | 140,815 | 17 | 526,593 | 97,552 | 19 | 1.1 | 122 | 135 | 108 | 2.4 | 2.4 | 0.01 |
| nist-sp1271 | 462,621 | 9 | 399,144 | 4,138 | 2 | 0.2 | 1 | 8 | 0 | 0.6 | 0.6 | 0.00 |
| frb-beige-book-2024-01 | 1,027,803 | 58 | 922,980 | 3,282 | 2 | 0.0 | 7 | 65 | 0 | 1.0 | 1.0 | 0.00 |
| inkscape-network | 345,719 | 2 | 68,400 | 1,184 | 2 | 1.0 | 2 | 6 | 0 | 0.2 | 0.2 | 0.00 |
| qpdf-object-streams | 584 | 1 | 2,450 | 508 | 2 | 2.0 | 2 | 5 | 0 | 0.1 | 0.1 | 0.00 |

The object-stream count is the number of `/Type /ObjStm` dictionaries in the file, across all revisions. The full rows for every entry are in the test output.

### 3.4 Findings from the measurements

1. **Object streams are re-decoded inside a single operation, and real files show it most.** The D-49 release decodes an object stream again each time a walk returns to it. On the G7 shape, a field walk parses each object stream 3.6 times (581 parses over 161 streams), flatten 7.0 times, and sign 5.0 times. Two real government forms reach 15 to 16 times. `faa-8710-1` decodes 7.6 MB, 18.6 times its file length, for one round of reads. The absolute cost is small: at most 0.46 s and 87 MiB allocated on the corpus.
2. **Operations share no work.** Listing the fields twice on one open document roughly doubles the walk's traversal and decoding: 1,161 object-stream parses against 581, and 24,000 field visits against 12,000. The open's own work is paid once, so the totals are a little under twice the one-listing row (17,126,765 decoded bytes, not 17,281,560). This is because D-49 releases clean object streams when each public operation returns.
3. **This fixture family scales in proportion to its size.** For the G7 form shape only, from 1,000 to 4,000 pages, the decode, parse, eviction, load, and visit counters grow by 3.9 to 4.0 times. Source bytes grow by 2.5 to 4.1 times, because one cursor window serves neighbouring objects. Per page, the counts hold steady: about 0.15 object-stream parses for a field walk, and 3 field visits plus 1 page visit per walk. That is an observation about one generated family, not a bound for arbitrary PDFs.
4. **The examined benign files use no filter chain on a structural stream.** Stage bytes equal final decoded bytes in every row of the G7 fixtures and the G2 corpus. So these files never exercise intermediate-stage work, and `DecodedByteCount` would not show it if a file did. The synthetic `WorkCounterTests` fixture does use a chain, `/ASCIIHexDecode /FlateDecode`, to check the stage counter. Other real files may use chains.
5. **Traversal is bounded by the structure.** Page visits are one per page-tree node per walk (4,011 = 4,000 pages + 10 intermediate nodes + the root). Flatten and sign walk the page tree twice. The field walk visits each field node once. Object loads exceed the object count because evicted objects are loaded again (20,011 cached evictions for a 4,000-page field walk).
6. **Observed managed heap growth is small.** The largest peak heap growth measured is 55.4 MiB (4,000-page flatten). `WorkloadMeter` samples the managed heap, so this is not the engine's accounted structural allocation, not native or process memory, and not a guarantee of the D-20 budgets for other inputs. Retained object streams stay at one or fewer (`ObjectStreamCacheTests`).
7. **Source I/O follows the re-decoding.** On the G7 shape, read-only operations read 34 to 89% of the file. On real forms, every re-decode reads the object stream's raw bytes again, unless a retained cursor window still holds them. `faa-8710-1` reads 2.37 MB from a 409 KB file (5.8 times), and `uscis-i9-2025` reads 3.8 times its length. Saves and signatures read the whole file at least once, because the cross-stream save copies the prefix, and signing reads it again for the digest.

## 4. Analysis: amplification paths (not measured; W-168)

Each expression below comes from reading the code. They are illustrative amplification expressions, not proven worst-case bounds. The counters in section 1 can measure the paths they cover, but no fixture in this report exercises them.

Three quantities differ, and the central finding is that the first and third can stay bounded while the second grows large:

| Quantity | Meaning | What bounds it today |
| --- | --- | --- |
| Unique objects | Size of the structural map | The entry budget: one entry per 8 file bytes, at most 2,097,152 |
| Object loads and decodes | Cumulative work of one operation | Nothing at operation level. Each unit has a cap, and one object or object stream can be loaded or decoded many times. |
| Retained decoded objects | Memory held at a moment | D-49: at most one clean object stream, plus dirty and pinned objects |

| Path | Code | Per-unit cap | What bounds the repetition | Theoretical work per operation |
| --- | --- | --- | --- | --- |
| A. Object-stream re-decode after a release | `PdfObjectStore.LoadFromObjectStreamAsync`, `ReleaseCleanObjectStreams` (D-49) | 32 MiB decoded per object stream (`StructuralCap`) | The number of object-stream switches in one walk. No operation-level limit applies. The entry budget bounds unique objects, not loads or switches, because a walk can return to an evicted object or stream. | Switches × up to 32 MiB. Benign files reach 16 decodes per stream (3.4). |
| B. Filter chains | `PdfStreamDecoder.Decode` | The cap applies to each stage's output, not to the chain | Filter array length, up to `PdfParser.MaxContainerEntries` (1,048,576) | Stages × cap for each stream read whole. Only stage bytes see it. |
| C. Cross-reference sections at open | `PdfDocumentLoader.LoadXrefAsync`, `ReadXrefStreamAsync` | 64 MiB per stage (`XrefCap`). The stream is decoded before its entries are checked against the entry budget. | `/Prev` chain depth, up to 256 (`MaxTraversalDepth`) | Up to 256 sections × chain stages × 64 MiB |
| D. A combined with B | as above | | | Multiplicative: switches × stages × 32 MiB |
| E. Tree walks and reference chains | `PdfPageTree.WalkPages`, `PdfFieldTree.WalkField`, `PdfObjectStore.ResolveAsync` | Visited sets reject shared nodes and cycles. Depth and chain length are at most 256. | One or two walks per operation, as measured | Linear in the object count. It matters only through path A, because each load after an eviction can decode a stream. |
| F. Repeated public calls | callers | Each call is isolated (D-49) | The caller | Linear in the number of calls, which is the caller's choice |
| G. Predictor processing | `PdfStreamDecoder.Unpredict` | Row width below `int.MaxValue`; the output follows the filter output | Rows × row width, with an output copy | Work and allocation beyond what stage bytes count, for large `/Columns` and `/Colors` |
| H. LZW and RunLength decoding | `LzwDecodeStream`, `RunLengthDecodeStream`, `ReadAll` | The cap counts output bytes | Input bytes and decoder iterations, with buffer growth in `ReadAll` | Work and allocation the output-byte cap does not measure directly |
| I. Deep parsing and nested containers | `PdfParser`, `PdfLexer` | 1,048,576 entries per container; 64 KiB per token; the parse depth | Nesting, and the number of containers per object | Token and allocation work that the object-traversal limits do not bound |
| J. Form appearance and widget processing | `PdfFormAppearances`, `PdfFlattener` | Per field: the WinAnsi Helvetica subset | Widgets per page, value length, resources shared per widget | Appearance and resource work with no large stream decode |

Some work is expected, not amplification. Signing hashes the whole document, a cross-stream save copies the prefix, and attachment extraction streams the payload. That work is linear in the legitimate input or output, and a cumulative budget should account for it separately from repeated work on the same structures (paths A, B, D, and E through A).

Paths G to J are added from the owner's review (W-169). W-168 measures them with A to D.

Paths A to D have caps on each unit and none on the operation as a whole. That is the gap W-149 names. A "material" verdict on any path needs W-168's measurements at production-sized inputs.

## 5. Cancellation limitations (inventory from the code)

| Public operation | Checks the token | Runs with no checkpoint |
| --- | --- | --- |
| `PdfDocument.OpenAsync` | Each cross-reference section (`LoadXrefAsync`), cursor window reads, source reads, object loads on the asynchronous open path | Decoding one cross-reference or object stream, up to 64 MiB per stage |
| `Pages`, `AcroForm.Fields`, `GetFields`, `Export`, metadata, attachments, `Signatures`, `Revisions`; editor `Fill`, `Flatten`, rotation, metadata and attachment edits | Never: these APIs are synchronous and take no token | The whole operation, including every object-stream decode it triggers (paths A, B, D) |
| `PdfDocumentEditor.SaveIncrementalAsync` | Entry; each 1 MiB chunk of the cross-stream prefix copy; destination writes | Appendix serialization |
| `PdfDocumentEditor.SignAsync` | Entry; the provider's entry check; the external signer and timestamp-authority calls get the token; once more after the contents are built | Building the signature dictionary and appendix; `CmsDetachedSignatureProvider` hashes the signed ranges (the whole file) with `HashAlgorithm.ComputeHash` and no checkpoint |
| `PdfWorkflow.ExportFormValuesAsync` | The open; once before the export | The synchronous `Export` |
| `PdfWorkflowBuilder.SaveAsync`, `AppendToSourceAsync` | Entry, the open, and the engine save above; once more before a file save is published | `Apply`: every queued fill, flatten, rotation, metadata, and attachment step, synchronously |

Cancellation latency is not measured: no benign operation here lasts more than 1.52 s. W-168 measures it on fixtures that keep an operation running inside a path with no checkpoint. It records the time from the cancellation request to the operation's return, separately from the operation's total work. An async signature is not evidence that an operation can be interrupted; W-149's strategy needs cooperative checkpoints in the expensive loops. Cancellation responsiveness and cumulative work limits are measured and decided as separate dimensions.

## 6. What the baseline means for W-149 (preliminary, not a decision)

- **Counter scope and reset boundary.** Every public engine operation already takes the D-49 lease (`ReleaseAfter`). That makes the lease a natural place to start and reset an operation's counters, and it keeps operations isolated. Per document, the existing counters only ever grow.
- **Headroom.** The largest benign per-operation stage-byte total observed is 16.7 MB (4,000-page flatten). Benign per-operation re-decoding reaches 16 times per object stream on real forms. Any limit must leave that room at the production sizes the D-20 workloads name.
- **Cache.** The benign evidence does not justify an operation-local cache. Re-decoding costs at most 0.46 s and 87 MiB allocated on the corpus, and a cache would trade retained memory against D-49. Whether adversarial path A justifies one, or a bound on re-decoding, waits for W-168.
- **Cancellation.** Any budget strategy has to choose whether the synchronous read and edit APIs get a checkpoint, or whether a work limit replaces cancellation inside them.
