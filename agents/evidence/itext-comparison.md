# iText comparison

W-111. The G7 workloads run through this library and through iText Core on the same fixtures, the same machine, and the same meter. This evidence covers one machine and one run. It is not a G7 gate, and it makes no claim about iText beyond what was measured.

## Method

`make compare-itext` builds `test/jaytwo.Ergonomics.Pdf.Comparison` in Release on `net10.0` with coverage off. The project is outside `jaytwo.Ergonomics.Pdf.sln`, so CI never restores iText. `FixtureSetup.prepare` rewrites the fixtures in a process of its own. Each test then runs one workload for one library in its own `dotnet test` process.

- **Fixtures.** `G7Fixtures` writes the same bytes as `LargeWorkloadTests`. That covers the dense 1 GiB file (64 pages of random-RGB images, classic xref) and qpdf's AES-256 object-stream copy of it. It also covers the 500-page and 1,000-page forms, rewritten by qpdf with object streams, and the 1 GiB CSV payload. The filled forms that sign and flatten use are filled by this library and then rewritten by qpdf into one revision; iText cannot open this library's incremental output on an object-stream file (finding 1). The attachment-extract source is this library's attachment added to the G7 carrier.
- **Measurement.** Every workload gets one cold pass and then warm passes: two warm passes for the 1 GiB files, one for the attachment, four for the forms. "Warm" is the fastest warm pass. `WorkloadMeter` gives peak managed-heap growth (largest of any pass) and allocated bytes (last pass). Peak working set is the process's peak. It includes the runtime and test host, which take about 95 MiB before the first pass, and native memory the heap counter does not see. Source bytes read are counted by a `ProbeStream`, so they are reported only where a library reads a caller's stream.
- **Same work.** Inside the window, both libraries assert the same field names, values, and page numbers on inspect and extraction. Writes are incremental: this library's cross-stream `SaveIncremental` and `Sign`, and iText's `StampingProperties.UseAppendMode()`. After the passes, each output is checked outside the window. The source must be a byte prefix of the output. This library must read back the title, the 500 filled values, the signature (reason and coverage to end of file), or the attachment bytes. qpdf `--check` runs on the form outputs, and the extracted payload must match the original byte for byte.
- **iText configuration.** iText gets its best large-file setup. It opens files by path, and `PdfSigner` writes its interim output to a temporary file. Three extra rows show other configurations: `Stream` input as this library's callers use it, `PdfSigner`'s default in-memory signer, and a full-rewrite flatten.

## Environment

Run on 2026-10-22, tree at `3df78e1` with the W-111 changes. Windows 11 IoT Enterprise LTSC 2024 (10.0.26100), Intel Core i7-9700, 63 GiB RAM, NVMe SSD temp directory. .NET SDK 10.0.402, runtime 10.0.13. qpdf 12.4.2. iText Core 9.8.0 (`itext`, `itext.bouncy-castle-adapter`, `itext.commons`) with BouncyCastle.Cryptography 2.7.0. All 27 tests passed.

## Results

| Workload | Warm time, s (this / iText) | iText / this | Peak heap growth, MiB (this / iText) | Peak working set, MiB (this / iText) |
|---|---|---|---|---|
| Dense 1 GiB: inspect | 0.003 / 0.007 | 2.3x | 1.94 / 6.19 | 93.97 / 100.58 |
| Dense 1 GiB: metadata edit | 0.551 / 1.378 | 2.5x | 3.48 / 1.25 | 99.63 / 100.61 |
| Dense 1 GiB: sign | 3.528 / 9.343 | 2.6x | 3.64 / 7.15 | 103.51 / 114.77 |
| 1 GiB attachment: add and save | 33.560 / 43.767 | 1.3x | 1.94 / 1.46 | 97.94 / 104.01 |
| 1 GiB attachment: extract | 5.815 / 6.405 | 1.1x | 1.24 / 4395.24 | 96.84 / 4463.51 |
| 500-page form: extraction | 0.075 / 0.076 | 1.0x | 6.24 / 9.17 | 99.74 / 108.80 |
| 500-page form: metadata edit | 0.132 / 0.007 | 0.1x | 8.72 / 1.90 | 106.16 / 97.52 |
| 500-page form: bulk fill of 500 fields | 0.099 / 0.372 | 3.8x | 11.20 / 12.27 | 112.88 / 130.29 |
| 500-page form: sign | 0.099 / 0.159 | 1.6x | 7.55 / 10.69 | 107.53 / 120.04 |
| 1,000-page form: flatten of 2,000 fields (iText: full rewrite) | 0.250 / 0.344 | 1.4x | 20.90 / 20.22 | 129.13 / 133.02 |
| Encrypted 1 GiB: inspect, user password | 0.012 / 0.054 | 4.5x | 5.29 / 5.44 | 98.15 / 103.22 |
| Encrypted 1 GiB: inspect, owner password | 0.013 / 0.053 | 4.1x | 5.99 / 5.26 | 98.24 / 103.76 |

| Workload | Library | Passes | Cold, s | Warm, s | Peak heap growth, MiB | Allocated, last pass, MiB | Peak working set, MiB | Source bytes read |
|---|---|---|---|---|---|---|---|---|
| Dense 1 GiB: inspect | jaytwo.Ergonomics.Pdf | 3 | 0.030 | 0.003 | 1.94 | 1.90 | 93.97 | 8,466,281 |
| Dense 1 GiB: inspect | iText 9.8.0 | 3 | 0.137 | 0.007 | 6.19 | 0.80 | 100.58 | - |
| Dense 1 GiB: inspect | iText 9.8.0, Stream input | 3 | 1.423 | 1.173 | 4609.09 | 5120.97 | 4200.71 | 1,073,916,019 |
| Dense 1 GiB: metadata edit | jaytwo.Ergonomics.Pdf | 3 | 0.522 | 0.551 | 3.48 | 2.77 | 99.63 | 1,082,382,301 |
| Dense 1 GiB: metadata edit | iText 9.8.0 | 3 | 1.749 | 1.378 | 1.25 | 0.15 | 100.61 | - |
| Dense 1 GiB: sign | jaytwo.Ergonomics.Pdf | 3 | 3.623 | 3.528 | 3.64 | 2.89 | 103.51 | 2,156,298,320 |
| Dense 1 GiB: sign | iText 9.8.0 | 3 | 10.038 | 9.343 | 7.15 | 1.48 | 114.77 | - |
| Dense 1 GiB: sign | iText 9.8.0, in-memory signer | 3 | 8.941 | 8.290 | 4609.70 | 5121.66 | 5226.94 | - |
| 1 GiB attachment: add and save | jaytwo.Ergonomics.Pdf | 2 | 33.432 | 33.560 | 1.94 | 1.22 | 97.94 | 1,524 |
| 1 GiB attachment: add and save | iText 9.8.0 | 2 | 45.120 | 43.767 | 1.46 | 0.33 | 104.01 | - |
| 1 GiB attachment: extract | jaytwo.Ergonomics.Pdf | 2 | 5.913 | 5.815 | 1.24 | 0.35 | 96.84 | 315,110,348 |
| 1 GiB attachment: extract | iText 9.8.0 | 2 | 6.664 | 6.405 | 4395.24 | 5418.14 | 4463.51 | - |
| 500-page form: extraction | jaytwo.Ergonomics.Pdf | 5 | 0.172 | 0.075 | 6.24 | 68.40 | 99.74 | 335,363 |
| 500-page form: extraction | iText 9.8.0 | 5 | 0.242 | 0.076 | 9.17 | 12.33 | 108.80 | - |
| 500-page form: metadata edit | jaytwo.Ergonomics.Pdf | 5 | 0.297 | 0.132 | 8.72 | 127.39 | 106.16 | 756,009 |
| 500-page form: metadata edit | iText 9.8.0 | 5 | 0.125 | 0.007 | 1.90 | 1.47 | 97.52 | - |
| 500-page form: bulk fill of 500 fields | jaytwo.Ergonomics.Pdf | 5 | 0.210 | 0.099 | 11.20 | 65.00 | 112.88 | 553,862 |
| 500-page form: bulk fill of 500 fields | iText 9.8.0 | 5 | 0.644 | 0.372 | 12.27 | 161.85 | 130.29 | - |
| 500-page form: sign | jaytwo.Ergonomics.Pdf | 5 | 0.289 | 0.099 | 7.55 | 104.86 | 107.53 | 1,333,274 |
| 500-page form: sign | iText 9.8.0 | 5 | 0.384 | 0.159 | 10.69 | 19.98 | 120.04 | - |
| 1,000-page form: flatten of 2,000 fields | jaytwo.Ergonomics.Pdf | 5 | 0.534 | 0.250 | 20.90 | 209.41 | 129.13 | 2,819,355 |
| 1,000-page form: flatten of 2,000 fields | iText 9.8.0, full rewrite | 5 | 0.997 | 0.344 | 20.22 | 576.02 | 133.02 | - |
| Encrypted 1 GiB: inspect, user password | jaytwo.Ergonomics.Pdf | 3 | 0.109 | 0.012 | 5.29 | 6.54 | 98.15 | 144,840 |
| Encrypted 1 GiB: inspect, user password | iText 9.8.0 | 3 | 0.222 | 0.054 | 5.44 | 5.10 | 103.22 | - |
| Encrypted 1 GiB: inspect, owner password | jaytwo.Ergonomics.Pdf | 3 | 0.054 | 0.013 | 5.99 | 7.06 | 98.24 | 144,840 |
| Encrypted 1 GiB: inspect, owner password | iText 9.8.0 | 3 | 0.218 | 0.053 | 5.26 | 4.98 | 103.76 | - |

## Findings

1. **iText cannot open this library's incremental output on a file with a cross-reference stream.** On qpdf's object-stream 500-page form filled by this library, iText 9.8.0 throws `PdfException: Invalid page structure. /Pages must be PdfDictionary.`, even for a read-only open. It opens the unfilled source normally. qpdf `--check` accepts the same file, and the G7 tests accept this library's signed output on such files. The likely mechanism comes from iText's Java `PdfReader` on the `develop` branch, read 2026-10-22; that is not a source reading of the 9.8.0 tag. After a classic `xref` section, `readXref` reads every `/Prev` with `readXrefSection()`, which requires the `xref` keyword. Our classic section's `/Prev` points to the source's cross-reference stream, so the read fails. Under the default strictness iText then calls `rebuildXref()`, a line scan that indexes only top-level `N G obj` objects. Objects in object streams, `/Pages` among them, are lost. The cause is the charter's classic-xref-only write rule, so any change is an owner decision.
2. **iText buffers the whole file on three common paths.** On a 1 GiB file, iText's `PdfReader(Stream)` reads the whole stream into memory: 4.6 GB of peak heap growth and 4.2 GB of working set. Its default `PdfSigner` buffers the signed document in a `MemoryStream`, also 4.6 GB. Embedded-file extraction through `PdfStream.GetBytes()` decodes the whole payload, 4.4 GB. Opened by path and given a temporary file for signing, iText stays within a few MiB on every 1 GiB workload except extraction.
3. **iText refuses to flatten in append mode** (`PdfException: Field flattening is not supported in append mode.`). Its flatten row is a full rewrite, so it is not the same operation as this library's incremental flatten.
4. **Where this library is slower.** On the 500-page form metadata edit, this library takes 0.132 s and allocates 127 MiB; iText takes 0.007 s and allocates 1.5 MiB. Form extraction allocates 68 MiB against iText's 12 MiB, and form sign 105 MiB against 20 MiB. These are leads, not defects. Peak heap growth stays within the G7 budgets.
5. **Where this library is faster.** It is 1.0 to 4.5 times as fast on every other workload. The largest margins are bulk fill (3.8x), encrypted inspect (4.1x and 4.5x), and the dense edit and sign (2.5x and 2.6x).

## Limitations

- One run on one Windows machine, `net10.0` only. Timings under 0.01 s are near the timer's resolution, and the heap meter samples about every millisecond.
- iText's time includes its own work beyond this library's. Its metadata edit also updates `/Producer` and `/ModDate`, and its signature dictionary and CMS layout differ. This library's cross-stream saves copy the source through the counted stream, and iText copies it too.
- The attachment workloads use cross-stream saves for both libraries, where G7 uses a same-stream save.
- Signature inspection and revision listing are not compared. iText's `SignatureUtil` parses and hashes for verification, and its revision count covers signatures only.

## W-114 signature-state walk

Measured on 2026-10-22 in the repository Ubuntu builder image with .NET 10.0.13 and qpdf 12.3.2. A detached `a844155` baseline and the W-114 tree ran `FixtureSetup.prepare` and the two named `ErgonomicsWorkloads` in separate processes. Each row had one cold and four warm passes; the table reports the fastest warm pass, the largest sampled heap growth, and the last pass's allocation.

| Workload | Revision | Warm, s | Peak heap growth, MiB | Allocated, MiB | Source bytes read |
|---|---|---:|---:|---:|---:|
| 500-page form: metadata edit | `a844155` | 0.350 | 10.02 | 127.39 | 756,009 |
| 500-page form: metadata edit | W-114 | 0.051 | 6.44 | 20.64 | 447,304 |
| 500-page form: sign | `a844155` | 0.293 | 8.36 | 104.15 | 1,333,274 |
| 500-page form: sign | W-114 | 0.208 | 8.24 | 78.90 | 1,192,407 |

The metadata edit allocates 83.8% less and stays below W-114's 30 MiB limit. Signing allocates 24.2% less. These are resource measurements; signature integrity, revision coverage, and trust remain established by the unchanged signing interoperability tests, not by this comparison.
