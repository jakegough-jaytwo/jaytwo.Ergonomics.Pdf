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

- Read amplification on object-stream forms. Signing the 493,069-byte filled 500-page form read 70,035,458 source bytes, and flattening the 974,773-byte 1,000-page form read 399,321,762. W-79 attributed this to object-stream re-decoding. D-49's measurement superseded that: about 98% of the bytes were 64 KiB cursor windows refilled after a seek. W-108 fixed it; see "W-108 source I/O" below.
- CI cost: in the Jenkins-equivalent run (Debug, coverage on), the compressed 1 GiB attachment test takes 4 min 53 s, against about 70 s in Release, because coverage instruments the per-byte Adler-32 loop. The other W-79 tests take 8 to 40 s each there.
- Signature inspection and revision listing each read 8,753,949 bytes of the 1,073,951,670-byte signed dense file, about what opening and inspecting the unsigned file reads (8,602,607), so neither reads the signed ranges.

## W-108 source I/O

D-49 replaced the explanation of the object-stream read multiples above. `PdfCursor.Seek` discarded its 64 KiB window, so the next read refilled 64 KiB even when the target was a few hundred bytes already in the discarded window. W-108 keeps the cursor's most recently filled 64 KiB windows, least recently used first out, and serves a seek inside one without I/O. The engine releases them when each public operation returns, normally or by exception, and at the end of `Open`. A fill stays 64 KiB, and parse failures keep their exception types and offsets.

### Window count

The scaling cases and the 500-page and flatten workloads were run on `net10.0` in Release with 4, 8, and 16 windows. Eight windows (512 KiB) reach the plateau: 16 read exactly what 8 read in every window, and 4 read 2,955,628 bytes for the 1,000-page flatten against 2,693,484 with 8, and 10,881,378 against 10,815,842 at 4,000 pages. Sign, extraction, edit, and fill read the same with 4, 8, and 16. `PdfCursor.DefaultRetainedWindows` is 8, under the 1 MiB ceiling of 16.

### Tripwires

`LargeWorkloadTests` gates three deterministic numbers for each window: source bytes read and read calls on the source stream (`ProbeStream`), and bytes the engine produced by decoding cross-reference, object, and other streams it reads whole (`DecodedByteCount`). Streaming decodes, such as an extracted attachment's content, are the caller's reads and are not counted. Each threshold is the measured maximum over `net10.0`, `net8.0`, `net6.0`, and `net48` plus about 10%, rounded up to three significant figures. A change that raises a number updates its threshold in the same commit, with the reason. Wall-clock time is reported and not gated.

The object-stream fixtures are written by `qpdf --deterministic-id`, the metadata edits set `ModificationDate` explicitly, the attachment carries fixed dates, and the dense fixture has a fixed `/ID`, so each output can be compared by hash with a run at another revision. That adds one Info write to each metadata-edit window, which is why the 500-page edit's base numbers below are higher than W-79's.

### Measurement

`make g7-workloads` and the scaling cases on 2026-10-22, Release, coverage off, each test in its own process, same machine and toolchain as W-79. Base is commit `aa50990` with the W-108 counters and tests and without the cursor change, in a scratch worktree. Change is the W-108 tree. Every output hash and signed-range hash matched base on every framework: 44 of 44. No window read more bytes, made more read calls, or decoded more bytes than at base. All 24 Windows workload runs and the four scaling runs passed with the gates in place.

| Window | Source bytes read, base | Change | Read calls, base | Change | Decoded bytes | Threshold: bytes / calls / decoded |
|---|---|---|---|---|---|---|
| Dense inspect | 8,602,835 | 8,466,281 | 135 | 132 | 0 | 9,320,000 / 146 / 0 |
| Dense metadata edit | 1,082,518,855 | 1,082,382,301 | 1,161 | 1,158 | 0 | 1,200,000,000 / 1,280 / 0 |
| Dense sign | 2,156,314,922 | 2,156,242,045 | 263,350 | 263,347 | 0 | 2,380,000,000 / 290,000 / 0 |
| Dense signature inspection | 8,754,633 | 8,611,346 | 143 | 137 | 0 | 9,480,000 / 151 / 0 |
| Dense revision listing | 8,754,633 | 8,611,346 | 143 | 137 | 0 | 9,480,000 / 151 / 0 |
| Compressed attachment add and save (carrier) | 1,321 | 1,177 | 6 | 5 | 0 | 1,300 / 6 / 0 |
| Compressed attachment reopen and extract | 314,118,577 to 330,136,623 | 313,987,359 to 330,005,405 | 38,318 to 40,273 | 38,315 to 40,270 | 0 | 364,000,000 / 44,300 / 0 |
| 500-page form extraction | 3,023,178 | 335,363 | 169 | 126 | 1,669,931 | 369,000 / 139 / 1,840,000 |
| 500-page metadata edit | 3,560,932 | 873,117 | 347 | 304 | 4,044,221 | 961,000 / 335 / 4,450,000 |
| 500-page bulk fill of 500 fields | 3,266,533 | 633,106 | 150 | 107 | 1,338,213 | 697,000 / 118 / 1,480,000 |
| 500-page sign | 70,035,458 | 1,788,548 | 1,327 | 285 | 2,082,495 | 1,970,000 / 314 / 2,300,000 |
| 1,000-page flatten | 399,321,762 | 2,693,526 | 6,386 | 312 | 4,100,912 | 2,970,000 / 344 / 4,520,000 |
| Encrypted xref-stream inspect, each password | 407,438 | 144,840 | 21 | 15 | 94,865 | 160,000 / 17 / 105,000 |
| Scaling: 1,000-page flatten | 399,321,720 | 2,693,484 | 6,386 | 312 | 4,100,863 | 2,970,000 / 344 / 4,520,000 |
| Scaling: 1,000-page sign | 139,316,292 | 3,019,015 | 2,622 | 542 | 4,137,574 | 3,330,000 / 597 / 4,560,000 |
| Scaling: 4,000-page flatten | 1,600,653,666 | 10,815,842 | 25,471 | 1,212 | 16,677,298 | 11,900,000 / 1,340 / 18,400,000 |
| Scaling: 4,000-page sign | 555,753,529 | 12,066,873 | 10,427 | 2,131 | 16,748,949 | 13,300,000 / 2,350 / 18,500,000 |

Ranges are across frameworks; the attachment's compressed size differs by runtime. Every other number is the same on all four. The scaling forms are 974,779 bytes (1,000 pages) and 3,922,614 bytes (4,000 pages) after the fill. Source bytes read for flatten and sign there fell by 97.8% to 99.3%: flatten from 410 to 2.8 times the file size and sign from 143 to 3.1 times, the same multiple at both sizes. Decoded bytes did not change anywhere. Base's owner-password encrypted window is from the first base run (`407,438` bytes, 21 calls); in the second, the user-password gate failed first, as base was expected to.

Peak heap growth, MiB (`net10.0` / `net8.0` / `net6.0` / `net48`), change: dense inspect 2.00 / 1.99 / 2.04 / 2.03; dense edit 2.79 / 2.79 / 2.82 / 3.06; dense sign 2.98 / 2.98 / 3.02 / 3.04; signature inspection 1.67 / 1.67 / 1.69 / 1.71; revision listing 1.67 / 1.67 / 1.67 / 1.73; attachment add 0.25 / 0.25 / 0.32 / 0.26; extract 0.51 / 0.53 / 0.54 / 0.54; 500-page extraction 5.27 / 6.63 / 5.79 / 2.23; edit 7.45 / 8.11 / 9.40 / 9.47; fill 11.53 / 11.53 / 12.26 / 10.05; sign 6.34 / 6.57 / 7.58 / 5.15; 1,000-page flatten 21.10 / 22.25 / 21.92 / 21.22; encrypted inspect 4.45 / 3.84 / 3.83 / 4.39 (user) and 1.39 / 1.44 / 1.07 / 2.57 (owner). Each is within its approved fixture budget and D-20 ceiling. The dense windows grew by 0.4 to 0.9 MiB against base, inside their 16 MiB budgets, and their allocation grew by the same amount (dense inspect allocated 1.98 MiB against 1.11 MiB on `net10.0`). That is churn, not live memory: each public operation fills new windows after the previous operation's were released, and the cursor keeps one 64 KiB buffer between operations, as before. At most 512 KiB of windows is live at once. Times were within the W-79 baseline: 1,000-page flatten 0.51 / 0.37 / 0.33 / 0.58 s, 500-page sign 0.21 / 0.21 / 0.15 / 0.17 s.

### Linux

`make docker-builder docker-run DOCKER_RUN_MAKE_TARGETS=g7-workloads` on 2026-10-22 in the Jenkins builder image (Ubuntu, 8 CPUs; runtimes 10.0.13, 8.0.32, and 6.0.36; qpdf 12.3.2), with the gates in place. All 18 runs passed. Every source-byte, read-call, and decoded-byte figure equals the Windows figure, except the attachment extract, whose compressed carrier depends on the runtime's zlib (313,987,359 to 315,110,348 bytes and 38,315 to 38,452 calls, within the threshold). The output hashes equal the Windows hashes on every framework, except the compressed attachment carrier on `net8.0` and `net6.0`, for the same reason.

Peak heap growth, MiB (`net10.0` / `net8.0` / `net6.0`): dense inspect 1.99 / 1.99 / 2.06; dense edit 2.80 / 2.80 / 2.86; dense sign 2.97 / 2.98 / 3.04; signature inspection 1.67 / 1.67 / 1.69; revision listing 1.67 / 1.67 / 1.67; attachment add 0.32 / 0.36 / 0.46; extract 0.52 / 0.54 / 0.57; 500-page extraction 7.37 / 7.27 / 68.81; edit 9.03 / 9.65 / 80.94; fill 11.53 / 11.55 / 65.00; sign 7.63 / 7.73 / 79.65; 1,000-page flatten 21.02 / 21.22 / 82.01; encrypted inspect 5.86 / 5.83 / 6.75 (user) and 5.96 / 5.80 / 7.01 (owner). The `net6.0` object-stream peaks are the uncollected garbage described under W-79's Linux section, not live memory. The largest, 82.01 MiB for the flatten, is within its 128 MiB budget and 0.61 MiB above W-79's 81.40 MiB.

`make quiet-docker-test` (Debug, coverage on) passed 4323/4323 on each of `net10.0`, `net8.0`, and `net6.0`, and the fuzz smoke run passed.
