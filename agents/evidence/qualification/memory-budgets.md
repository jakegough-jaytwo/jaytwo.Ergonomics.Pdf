# Approved memory budgets

Migrated from `agents/archive/state-v1/status.md` under D-33; D-36 and W-56 record its removal, and its pre-removal state is [squashed].

Maintained through `pdf-task-workflow` and verified with `pdf-change-verification`, under `QUALIFICATION.md` gate G7. The owner approved the working budget on 2026-10-18 (D-20): 256 MiB of peak managed-heap growth for inspect, edit, and sign, and 512 MiB for complex traversals. The suite asserts those ceilings. The fixture-specific budgets below were approved on 2026-10-18 under B-17. They come from isolated runs, because the heap counter is process-wide.

| Workload (`LargeWorkloadTests`) | Approved budget | Max measured, `net8.0`/`net6.0`/`net48` |
|---|---|---|
| Dense 1 GiB PDF: inspect, metadata edit, sign | 16 MiB each | 3.06 MiB |
| 250 MiB attachment: add, extract | 8 MiB each | 0.30 MiB |
| 500-page form: extraction, metadata edit, sign | 32 MiB each | 10.31 MiB |
