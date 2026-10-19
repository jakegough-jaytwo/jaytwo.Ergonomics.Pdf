# Technical Lead review: B-17

## Task and owner

B-17, G7 large-file workloads, against the D-20 working budget. Implemented by QA in `2026-10-18-qa-engineer-B-17.md`. Reviewer and verifier: Technical Lead.

## Verified facts

- The change is test-only: new `LargeWorkloadTests.cs` and `WorkloadMeter.cs`, in the `dense-memory` collection. `src/` is unchanged. The fixtures are generated in a temp directory and deleted afterwards.
- The inputs are real files on disk: a 1,073,915,943-byte dense PDF of random RGB image data, a 268,439,553-byte attachment, and a 500-page form with 1,000 fields, rewritten by qpdf with object streams. Every operation goes through the public API on `FileStream`s.
- My independent re-run: `dotnet test -f net8.0 --filter FullyQualifiedName~LargeWorkloadTests`, 3/3 passed. Peak heap growth: dense inspect 1.48 MiB, edit 2.56 MiB, sign 3.00 MiB. Attachment add 0.15 MiB, extract 0.29 MiB. 500 pages: extraction 6.96 MiB, edit 8.78 MiB, sign 7.96 MiB. Dense inspect read 8.6 MB of the 1 GiB file. Edit and sign read the source in requests of at most 1 MiB, the cross-stream copy plus the hash pass. These match QA's runs.
- The correctness checks are real: prefix hashes, a `/ByteRange` that covers the whole file except `/Contents`, the attachment hash, all 1,000 fields, and `qpdf --check` on the 500-page result.

## Budgets

The Technical Lead and QA approve these fixture-specific budgets for peak managed-heap growth, from QA's isolated runs on three frameworks and my re-run:

| Workload | Approved budget | Max measured |
|---|---|---|
| Dense 1 GiB: inspect, edit, sign | 16 MiB each | 3.06 MiB |
| 250 MiB attachment: add, extract | 8 MiB each | 0.30 MiB |
| 500 pages: extraction, edit, sign | 32 MiB each | 10.31 MiB |

The suite keeps asserting the D-20 ceilings, 256 MiB and 512 MiB. `GC.GetTotalMemory` is process-wide, and other test collections run in parallel during a full run, so asserting 8 to 32 MiB there would be flaky. The tighter budgets rest on the isolated runs, not on a permanent assertion. That is a recorded limitation.

## Defects confirmed

None. QA observation O1: the object-stream file allocates 90–175 MiB per operation while peak stays at 10.3 MiB, because each object load decodes its object stream again. That is churn, not retention. It fits the "repeated object-stream access" G7 row, which is not covered yet.

## Evidence missing

See QA's "Not run or not covered" list. G7 is not met: repeated use and dispose, encrypted xref-stream discovery, output and decompression limits, and a timing baseline remain.

## Handoff

Approved. Product Manager to accept.
