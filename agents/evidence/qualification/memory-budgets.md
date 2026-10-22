# Approved memory budgets

Migrated from `agents/archive/state-v1/status.md` under D-33; D-36 and W-56 record its removal, and its pre-removal state is [squashed].

Maintained through `pdf-task-workflow` and verified with `pdf-change-verification`, under `QUALIFICATION.md` gate G7. The owner approved the working budget on 2026-10-18 (D-20): 256 MiB of peak managed-heap growth for inspect, edit, and sign, and 512 MiB for complex traversals. The suite asserts those ceilings. The fixture-specific budgets below were approved on 2026-10-18 under B-17. They come from isolated runs, because the heap counter is process-wide.

| Workload (`LargeWorkloadTests`) | Approved budget | Max measured, `net8.0`/`net6.0`/`net48` |
|---|---|---|
| Dense 1 GiB PDF: inspect, metadata edit, sign | 16 MiB each | 3.06 MiB |
| 250 MiB attachment: add, extract | 8 MiB each | 0.30 MiB |
| 500-page form: extraction, metadata edit, sign | 32 MiB each | 10.31 MiB |

W-79 replaced the 250 MiB uncompressed attachment with the 1 GiB compressed one below, and the owner's W-79 approval superseded the 500-page budget of 32 MiB with 128 MiB. These rows stay as the B-17 record.

## W-79 workloads

W-79 added the remaining G7 workloads and re-measured the B-17 ones in Release. The owner approved these budgets on 2026-10-22: `WorkloadMeter` measures total managed-heap growth, including uncollected garbage, so the budgets hold on every supported target under that measurement, Linux `net6.0` included. The 128 MiB budgets stay inside the D-20 ceilings. The owner reviewed the recorded evidence and did not rerun the workloads.

| Workload (`LargeWorkloadTests`) | Approved budget | Max measured, Release, every target |
|---|---|---|
| Dense 1 GiB PDF: inspect, metadata edit, sign | 16 MiB each (B-17) | 2.60 MiB |
| Signed dense 1 GiB PDF: signature inspection, revision listing | 16 MiB each | 1.17 MiB |
| 1 GiB compressed attachment: add and save, extract | 8 MiB each | 0.45 MiB |
| 500-page form: extraction, metadata edit, sign | 128 MiB each, superseding B-17's 32 MiB | 79.94 MiB |
| 500-page form: bulk fill of 500 fields | 128 MiB | 65.06 MiB |
| 1,000-page form: flatten of 2,000 fields | 128 MiB | 81.40 MiB |
| Encrypted xref-stream 1 GiB PDF: inspect under the user and the owner password | 16 MiB each | 6.96 MiB |
| Repeated open and dispose, repeated object-stream reads: retained heap after the warm-up cycle | 1 MiB growth | 0.34 MiB |

The suite asserts the D-20 ceilings and the 1 MiB retained growth. The fixture budgets are checked against these isolated runs. "Max measured" covers every Windows and Linux run below; the 500-page and flatten maxima are the Linux `net6.0` windows, where no collection runs, as the Linux section shows. Every run is within its approved budget.

### Measurement

`make g7-workloads` on 2026-10-22, over base `W-50` (commit `4af4a41`) with the W-79 test changes: each test in its own `dotnet test` process, Release, coverage off. Windows 11 IoT Enterprise LTSC 2024 (10.0.26100), Intel Core i7-9700, 63 GiB RAM, NVMe SSD temp directory; .NET SDK 10.0.402 with runtimes 10.0.13, 8.0.32, and 6.0.36, and .NET Framework 4.8 (4.8.09221); qpdf 12.4.2. All 24 runs passed. `WorkloadMeter` records the sampled managed-heap peak over a fully collected baseline, which excludes native memory such as the zlib state and the file-system cache.

| Window | Peak heap growth, MiB (`net10.0` / `net8.0` / `net6.0` / `net48`) | Max | Time, s (`net10.0` / `net8.0` / `net6.0` / `net48`) | Source bytes read |
|---|---|---|---|---|
| Dense inspect | 1.12 / 1.12 / 1.18 / 1.16 | 1.18 | 0.03 / 0.03 / 0.05 / 0.06 | 8,602,607 |
| Dense metadata edit | 2.24 / 2.24 / 2.27 / 2.50 | 2.50 | 0.47 / 0.49 / 0.51 / 0.58 | 1,082,518,551 |
| Dense sign | 2.54 / 2.54 / 2.57 / 2.60 | 2.60 | 3.58 / 3.60 / 3.60 / 3.73 | 2,156,313,858 |
| Dense signature inspection | 1.10 / 1.10 / 1.13 / 1.15 | 1.15 | 0.02 / 0.02 / 0.03 / 0.03 | 8,753,949 |
| Dense revision listing | 1.11 / 1.11 / 1.11 / 1.17 | 1.17 | 0.01 / 0.01 / 0.01 / 0.01 | 8,753,949 |
| Compressed attachment add and save | 0.23 / 0.23 / 0.34 / 0.25 | 0.34 | 33.80 / 55.36 / 55.02 / 71.53 | 2,147,491,842 |
| Compressed attachment reopen and extract | 0.32 / 0.32 / 0.37 / 0.36 | 0.37 | 5.87 / 6.45 / 7.03 / 7.27 | 314,118,577 to 330,136,623 |
| 500-page form extraction | 5.93 / 6.11 / 5.86 / 6.70 | 6.70 | 0.18 / 0.18 / 0.15 / 0.18 | 3,023,178 |
| 500-page metadata edit | 7.82 / 7.72 / 6.92 / 7.27 | 7.82 | 0.27 / 0.29 / 0.20 / 0.28 | 3,443,824 |
| 500-page bulk fill of 500 fields | 11.57 / 11.57 / 12.29 / 11.48 | 12.29 | 0.17 / 0.19 / 0.13 / 0.15 | 3,266,533 |
| 500-page sign | 7.04 / 7.05 / 5.75 / 5.84 | 7.05 | 0.22 / 0.22 / 0.16 / 0.17 | 70,035,458 |
| 1,000-page flatten | 21.24 / 21.27 / 21.26 / 22.50 | 22.50 | 0.57 / 0.57 / 0.38 / 0.40 | 399,321,762 |
| Encrypted xref-stream inspect, user password | 4.58 / 2.73 / 3.79 / 4.45 | 4.58 | 0.06 / 0.05 / 0.07 / 0.17 | 407,438 |
| Encrypted xref-stream inspect, owner password | 1.08 / 1.39 / 1.23 / 2.31 | 2.31 | 0.01 / 0.02 / 0.01 / 0.05 | 407,438 |

Retained heap after a full collection, growth over the first of 20 cycles (`net10.0` / `net8.0` / `net6.0` / `net48`): open, field and page read, and dispose 249.6 / 245.6 / 347.0 / 0.0 KiB in 2.33 / 2.29 / 1.52 / 2.51 s; field reads on one open document 0.0 / 0.0 / 0.6 / 0.0 KiB in 0.97 / 1.04 / 1.01 / 1.84 s. The growth is a one-time runtime warm-up step: a 100-cycle run plateaued at 247.3 KiB from cycle 4 on `net10.0` and at 343.8 KiB from cycle 34 on `net6.0`, and never grew after that.

### Linux

`make docker-builder docker-run DOCKER_RUN_MAKE_TARGETS=g7-workloads` on 2026-10-22 in the Jenkins builder image (Ubuntu, 8 CPUs, no memory limit; runtimes 10.0.13, 8.0.32, and 6.0.36; qpdf 12.3.2), same tree. All 18 runs passed.

| Window | Peak heap growth, MiB (`net10.0` / `net8.0` / `net6.0`) | Time, s (`net10.0` / `net8.0` / `net6.0`) |
|---|---|---|
| Dense inspect | 1.11 / 1.12 / 1.15 | 0.03 / 0.03 / 0.05 |
| Dense metadata edit | 2.27 / 2.26 / 2.30 | 1.08 / 0.87 / 0.79 |
| Dense sign | 2.55 / 2.55 / 2.59 | 3.24 / 3.24 / 3.26 |
| Dense signature inspection | 1.11 / 1.11 / 1.14 | 0.02 / 0.02 / 0.03 |
| Dense revision listing | 1.11 / 1.11 / 1.11 | 0.01 / 0.01 / 0.00 |
| Compressed attachment add and save | 0.31 / 0.36 / 0.45 | 37.31 / 67.62 / 68.82 |
| Compressed attachment reopen and extract | 0.34 / 0.34 / 0.38 | 5.71 / 7.16 / 7.73 |
| 500-page form extraction | 7.29 / 6.94 / 69.04 | 0.23 / 0.23 / 0.16 |
| 500-page metadata edit | 9.32 / 9.05 / 79.94 | 0.42 / 0.38 / 0.20 |
| 500-page bulk fill of 500 fields | 11.51 / 11.58 / 65.06 | 0.23 / 0.24 / 0.16 |
| 500-page sign | 7.14 / 7.23 / 79.82 | 0.28 / 0.28 / 0.16 |
| 1,000-page flatten | 21.25 / 21.28 / 81.40 | 0.78 / 0.76 / 0.40 |
| Encrypted xref-stream inspect, user password | 5.95 / 5.98 / 6.59 | 0.06 / 0.05 / 0.08 |
| Encrypted xref-stream inspect, owner password | 5.88 / 5.95 / 6.96 | 0.02 / 0.02 / 0.01 |

Retained growth over the first of 20 cycles: open, read, and dispose 248.8 / 251.0 / 352.5 KiB; reads on one document 0.0 / 0.0 / 12.8 KiB.

On Linux `net6.0` the object-stream windows' peak equals what they allocate (69.04 MiB peak for 68.82 MiB allocated in extraction): .NET 6's default gen0 budget in the container is larger than the window's allocation, so no collection runs and `WorkloadMeter` counts the garbage. Rerunning the 500-page and flatten workloads in the same image with `DOTNET_GCgen0size=0x1000000` (16 MiB) gave the same allocation and peaks of 16.23, 18.71, 19.17, and 16.81 MiB for extraction, edit, fill, and sign, and 25.19 MiB for flatten. The excess is uncollected garbage, not live memory.

### Timing baseline

The times above are the G7 baseline: one machine, one run each, not targets. A later run that is materially slower on the same machine is investigated. The compressed attachment add is two full Flate passes over 1 GiB (spec W10 and W11), and it is the slowest window; `net10.0` is faster than the others there.

### Observations

- Read amplification on object-stream forms (explained, not a defect). Signing the 493,069-byte filled 500-page form reads 70,035,458 source bytes, and flattening the 974,773-byte 1,000-page form reads 399,321,762. The engine keeps at most one clean object stream decoded (`PdfEngine.LoadFromObjectStream` and `ReleaseCleanObjectStreams`), so an operation that alternates between objects in different object streams decodes them again. That bounds memory to one object stream at the cost of repeated reads, which take 0.16 to 0.57 s here. Extraction walks in stream order and reads 3,023,178 bytes.
- CI cost: in the Jenkins-equivalent run (Debug, coverage on), the compressed 1 GiB attachment test takes 4 min 53 s, against about 70 s in Release, because coverage instruments the per-byte Adler-32 loop. The other W-79 tests take 8 to 40 s each there.
- Signature inspection and revision listing each read 8,753,949 bytes of the 1,073,951,670-byte signed dense file, about what opening and inspecting the unsigned file reads (8,602,607), so neither reads the signed ranges.
