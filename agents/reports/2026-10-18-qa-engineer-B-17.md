# QA Engineer: B-17, G7 large-file workloads

## 1. Task and owner

B-17 (board row: QA Engineer implements, Technical Lead re-runs). The three G7 file-backed workloads, measured through the public API and asserted against the D-20 working budget. No `src/` change.

## 2. Verified facts

Tests are in `test/jaytwo.Ergonomics.Pdf.Tests/LargeWorkloadTests.cs`, in the `dense-memory` collection, which disables parallelization. Fixtures are written at test time to `ExternalTool.TempDirectory()` and deleted in `finally`; no temp directories were left after the runs.

**What is measured** (`WorkloadMeter.cs`). Each window wraps only library calls on `FileStream`s. Fixture generation and verification run outside the windows.

- Baseline: `GC.Collect`, `WaitForPendingFinalizers`, `GC.Collect`, then `GC.GetTotalMemory(true)`.
- Peak: the largest `GC.GetTotalMemory(false)` seen while the operation runs. A background thread samples it on a `Thread.Sleep(1)` loop. On this machine that is about every 15 ms, the Windows timer tick. The run records sample counts, for example 237 samples in 3.62 s. One more sample is taken at return, before any collection. "Peak heap growth" is peak minus baseline. It includes garbage that has not been collected yet, so it is an upper bound on live managed memory. It excludes native memory and the OS file cache.
- Allocated: `TestCompat.TotalAllocatedBytes()` delta. That is `GC.GetTotalAllocatedBytes(true)` on net6.0 and net8.0, and `AppDomain.MonitoringTotalAllocatedMemorySize` on net48.
- Time: `Stopwatch` around the operation.
- Read evidence: the source `FileStream` is wrapped in `ProbeStream`, which records the bytes read and the largest single read request.

**Fixtures**

- Dense: 1,073,915,943 bytes (≥ 1 GiB). 64 pages, each drawing one unfiltered 2048×2731 RGB image of fixed-seed SplitMix64 random bytes, plus one text field per page. Classic xref. After the edit, 1,073,917,475 bytes; after signing, 1,073,951,670 bytes.
- Attachment: a 268,439,553-byte payload (256 MiB + 4097 bytes, random) added to a 330-byte carrier. After the same-stream save, the carrier is 268,440,446 bytes.
- 500 pages: 10 intermediate `/Pages` nodes × 50 pages. Each page has a Flate content stream of 45 text lines and a parent field with two kids, a text field and a check box with shared `/AP`. That is 1,000 terminal fields. The classic file is 566,876 bytes. qpdf 12.4.2 `--object-streams=generate` rewrites it to 256,799 bytes with `/ObjStm` and `/XRef`, and that rewrite is the workload input. It is 262,253 bytes after the edit and 300,166 bytes after signing.

**Correctness asserted.** Dense: page count, pages, title, author, no attachments, 64 field names, values, and page numbers; the original bytes are a prefix of the edited file (SHA-256), and the edited file is a prefix of the signed file; `/ByteRange` starts at 0, ends at EOF, and has the `/Contents` hex as its gap; the streamed SHA-256 of the two ranges appears in the CMS; the reopened file has the new title and a signature field. Attachment: size and description after reopen, extracted bytes equal the payload (SHA-256), and the carrier's original bytes are a prefix. 500 pages: 500 pages, all 1,000 fields by full name, type, value, check state, and page number; the same prefix and `/ByteRange` checks; `SignedCms.CheckSignature` on net8.0 only; reopen with 1,001 fields; `qpdf --check` of the signed file passes.

### Results: run 2 (run 1 within 0.9 MiB peak and 0.2 s on every row)

Peak = peak managed-heap growth. Alloc = allocated bytes. MiB = 2^20 bytes.

**Dense 1 GiB**

| Phase | Framework | Peak MiB | Alloc MiB | Time s | Bytes read | Largest read | Budget | Result |
|---|---|---|---|---|---|---|---|---|
| Inspect | net8.0 | 1.48 | 1.46 | 0.05 | 8,602,607 | 65,536 | 256 MiB | pass |
| Inspect | net6.0 | 1.49 | 1.47 | 0.06 | 8,602,607 | 65,536 | 256 MiB | pass |
| Inspect | net48 | 1.53 | 1.53 | 0.07 | 8,602,607 | 65,536 | 256 MiB | pass |
| Metadata edit, cross-stream save | net8.0 | 2.56 | 2.54 | 0.49 | 1,082,518,551 | 1,048,576 | 256 MiB | pass |
| Metadata edit, cross-stream save | net6.0 | 2.58 | 2.56 | 0.55 | 1,082,518,551 | 1,048,576 | 256 MiB | pass |
| Metadata edit, cross-stream save | net48 | 2.85 | 2.85 | 0.58 | 1,082,518,551 | 1,048,576 | 256 MiB | pass |
| Sign to file | net8.0 | 3.00 | 2.98 | 3.62 | 2,156,313,858 | 1,048,576 | 256 MiB | pass |
| Sign to file | net6.0 | 3.02 | 2.99 | 3.67 | 2,156,313,858 | 1,048,576 | 256 MiB | pass |
| Sign to file | net48 | 3.06 | 3.05 | 3.74 | 2,156,313,858 | 1,048,576 | 256 MiB | pass |

Whole test: 19 to 21 s per framework, including generating 1 GiB and hashing 3 GiB. Inspect reads 0.8% of the file. No window grows the heap by more than 3.1 MiB.

**Attachment 256 MiB + 4097 bytes**

| Phase | Framework | Peak MiB | Alloc MiB | Time s | Bytes read | Largest read | Budget | Result |
|---|---|---|---|---|---|---|---|---|
| Add from file, same-stream save | net8.0 | 0.15 | 0.15 | 0.22 | 268,439,553 (payload) | 65,536 | 256 MiB | pass |
| Add from file, same-stream save | net6.0 | 0.17 | 0.15 | 0.23 | 268,439,553 | 65,536 | 256 MiB | pass |
| Add from file, same-stream save | net48 | 0.19 | 0.17 | 0.23 | 268,439,553 | 65,536 | 256 MiB | pass |
| Reopen, extract to file | net8.0 | 0.29 | 0.28 | 0.28 | 268,769,157 (PDF) | 65,536 | 256 MiB | pass |
| Reopen, extract to file | net6.0 | 0.29 | 0.28 | 0.28 | 268,769,157 | 65,536 | 256 MiB | pass |
| Reopen, extract to file | net48 | 0.28 | 0.27 | 0.29 | 268,769,157 | 65,536 | 256 MiB | pass |

Whole test: 2 s per framework.

**500 pages, object streams (qpdf)**

| Phase | Framework | Peak MiB | Alloc MiB | Time s | Bytes read | Budget | Result |
|---|---|---|---|---|---|---|---|
| Form extraction | net8.0 | 7.42 | 89.56 | 1.15 | 3,023,178 | 512 MiB (traversal) | pass |
| Form extraction | net6.0 | 6.95 | 89.61 | 1.12 | 3,023,178 | 512 MiB (traversal) | pass |
| Form extraction | net48 | 6.75 | 95.01 | 1.10 | 3,023,178 | 512 MiB (traversal) | pass |
| Metadata edit | net8.0 | 9.40 | 165.27 | 2.10 | 3,443,824 | 256 MiB | pass |
| Metadata edit | net6.0 | 9.46 | 165.28 | 2.06 | 3,443,824 | 256 MiB | pass |
| Metadata edit | net48 | 10.27 | 175.20 | 1.97 | 3,443,824 | 256 MiB | pass |
| Sign to file | net8.0 | 7.59 | 130.68 | 1.65 | 3,659,139 | 256 MiB | pass |
| Sign to file | net6.0 | 7.67 | 130.69 | 1.62 | 3,659,139 | 256 MiB | pass |
| Sign to file | net48 | 9.41 | 138.48 | 1.52 | 3,659,139 | 256 MiB | pass |

Form extraction is held to 512 MiB because it is a complex structural traversal: a full walk of the page tree and the field tree, with 2,500 objects resolved through object streams. Edit and sign are held to 256 MiB.

## 3. Assumptions

- `GC.GetTotalMemory(false)` sampled about every 15 ms catches any allocation large enough to matter. A whole-file buffer would live far longer than 15 ms while it fills from disk.
- The runs used Debug with coverlet instrumentation on, because the test `Directory.Build.props` sets `CollectCoverage` to true. Timings include that overhead.

## 4. Defects confirmed

None against D-20. Two throughput observations are not defects, but are recorded for G7's criterion "no known serious performance regression remains unexplained":

- **O1. Object-stream allocation churn.** On the 257 KB object-stream file, the 500-page edit allocates 165 MiB, sign 131 MiB, and extraction 90 MiB. Peak heap stays ≤ 10.3 MiB. This fits the single retained object stream that `ObjectStreamCacheTests` pins: each object load re-decodes its stream. Suggested owner: Core Engineer, if the Product Manager wants it tracked.
- **O2. Classic-xref read amplification.** In a diagnostic net8.0 run, the 500-page test was temporarily pointed at the 567 KB classic file and then reverted. It read 131,671,651 bytes during extraction, about 232 times the file size and consistent with a 64 KiB read window per object load. It took 0.29 s and allocated 17 MiB. That run is not permanent evidence.

## 5. Evidence missing (G7 not run or not covered)

- Sparse ≥ 3 GiB: not run here. That is `ScaleTests` (row 12), and its metadata-appendix assertion is B-10.
- Repeated object-stream access with no retained-memory growth: not covered. Nothing asserts retained memory across repeated traversals or across open and dispose cycles.
- Encrypted xref-stream PDF with no full-file discovery scan: not covered.
- "Memory does not grow indefinitely during repeated use or document disposal": not covered.
- "Output limits documented and fail safely" and "resource limits against excessive allocation and decompression": not covered by B-17. The workloads never reach the 32 and 64 MiB decompression caps or the 8 MiB appendix.
- Large encrypted documents, timestamped signing (TSA latency), and large Flate-compressed streams: not measured.
- 1 GiB signature: checked for coverage and digest only. Neither `SignedCms` nor pyHanko verified it; the test would have to buffer 1 GiB. The 500-page signature is CMS-verified on net8.0 only.
- Fixtures are synthetic: our writer, then a qpdf rewrite. No real-world producer.
- Native memory and process working set: not measured.
- Timing baseline: one machine and two runs. There is no earlier baseline to compare against.

## 6. Changes made

- New `test/jaytwo.Ergonomics.Pdf.Tests/LargeWorkloadTests.cs`: three tests in `[Collection("dense-memory")]`.
- New helper `test/jaytwo.Ergonomics.Pdf.Tests/WorkloadMeter.cs`: the sampler, following `DenseMemoryTests.Measure`, plus timing and sample count.
- `TestCompat.cs` and `src/` unchanged. Nothing committed.

**Proposed fixture-specific budgets.** These are for Technical Lead and QA approval. The tests still assert the D-20 working budget. The largest measured peak in each family is about one twentieth of its D-20 cap or less. The attachment fixture shows why the cap is weak: at 256 MiB it catches whole-attachment buffering by only 4,097 bytes.

| Fixture | Max measured peak (6 runs) | Proposed budget |
|---|---|---|
| Dense 1 GiB: inspect, edit, sign | 3.06 MiB | 16 MiB each |
| 250 MiB attachment: add and save, extract | 0.30 MiB | 8 MiB each (matches the `DenseMemoryTests` bound) |
| 500 pages: extraction | 7.42 MiB | 32 MiB |
| 500 pages: edit, sign | 10.31 MiB | 32 MiB |

Also proposed: keep the source-read assertions (inspect reads < 16 MiB of the dense file, and no single read request exceeds 1 MiB) as permanent evidence of no whole-document materialization.

## 7. Verification run

- Environment: Windows 11 IoT Enterprise LTSC 2024 (10.0.26100), i7-9700, 63.3 GB RAM, NVMe SSD temp. .NET 8.0.31, .NET 6.0.36, .NET Framework 4.8.1 (release 533509). qpdf 12.4.2 (`JAYTWO_QPDF`).
- `dotnet build test/jaytwo.Ergonomics.Pdf.Tests`: succeeded, 0 warnings (StyleCop as errors), all three targets.
- `dotnet test test/jaytwo.Ergonomics.Pdf.Tests --no-build -f <tfm> --filter "FullyQualifiedName~LargeWorkloadTests"`, twice for each of net8.0, net6.0, and net48: 3/3 passed in every run. TRX: `%LOCALAPPDATA%\Temp\jaytwo-b17\b17-net8-run1.trx`, `b17-net8.0-run2.trx`, `b17-net6.0-run{1,2}.trx`, `b17-net48-run{1,2}.trx`. Test duration was about 30 s per framework.

## 8. Decisions needed

- Technical Lead and QA: approve the fixture-specific budgets in section 6, or set others. G7 stays unmet until then, and for the not-covered items in section 5.
- Product Manager: whether O1 and O2 become backlog items.

## 9. Handoff

Technical Lead re-runs the workloads (board verifier).
