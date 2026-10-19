# B-36 QA verification: parser and decoder hardening

## Task and owner

B-36, implemented by the Core Engineer. QA Engineer verification of the uncommitted `wip` tree (base `da08d21`). **QA sign-off: withheld**, because of defect QA-B36-01 (SA-03 xref amplification still reaches out-of-memory at the new limit). SA-01, SA-02, SA-04, SA-09, SA-11, and the object-stream part of SA-03 are verified.

## Verified facts

| Finding | Evidence | Result |
|---|---|---|
| SA-01 | Code read of every lexer branch (`PdfParser.cs`). Own probes: unterminated nested, escaped, and octal or CR literals with caller cap 16 throw `PdfObjectTooLargeException` before EOF. Name, number, and keyword boundaries: 65,536 bytes is not refused by the cap, 65,537 is. | Pass |
| SA-02 | Own probe at production scale: 1,048,577 repeated `/A` keys (5.2 MB file) → `PdfObjectTooLargeException`, 4.3 s, input unchanged. 1,048,576 → opens, the last value wins, and a cross-stream `SaveIncremental` succeeds. Revert check below. | Pass |
| SA-03 objstm | The header is parsed pair by pair, and `/N` is checked before decode. The overlap budget is `4 × decoded + 64 KiB`. | Pass |
| SA-03 xref | `/Index` overflow probes: `[2147483647 2]`, `[0 1 2147483647 2]`, `[0 2147483648]`, `[0 -1 0 3]` → `PdfParseException`. `[2147483646 1]` and `[8388608 1]` → `PdfObjectTooLargeException`. The input is unchanged. At the cap, out of memory: see QA-B36-01. | **Fail** |
| SA-04 | Own trigger: a page tree with 3 `/Kids` per level (a leaf on each side of the next level). 256 levels opens with 513 pages. 257 levels → `PdfParseException("…too deep")` from `PageCount`, and also from `Sign` into a separate destination. The destination stays 0 bytes and the source is unchanged. About 2 MB allocated, 31 ms. Revert check below. | Pass |
| SA-09, SA-11 | Code read. `HardeningTests` cases pass on every framework. | Pass |

**Revert checks** (net8.0, coverage off):
- SA-04: deleting only the `depth > MaxTraversalDepth` block → `deep_page_tree_is_a_parse_error_instead_of_a_stack_overflow` fails (`Assert.Throws() Failure`).
- SA-02: restoring the HEAD `ParseDictionary` body → `dictionary_with_many_distinct_keys…` fails ("Parsing took 00:02:45.78"), and `array_and_dictionary_entry_counts_are_capped` fails for `dictionary` and `repeated-key`.
- Restored from backup. The SHA-256 of `PdfParser.cs` (`09cfa35b…`) and `PdfEngine.cs` (`3f26d83f…`) and of `git diff HEAD -- src` (`fd5cbccd…`) are identical before and after.

**Docs.** The constants match the docs: `MaxTokenBytes` 65,536; `MaxContainerEntries` 1,048,576; `MaxXrefStreamObjects` 8,388,608; `MaxTraversalDepth` 256; `ObjectStreamReadFactor` 4 plus 64 KiB slack; `StructuralCap` 32 MiB for strings. The 257-versus-256 page-tree boundary and the 65,536-versus-65,537 token boundary match the wording. `git diff HEAD -- ACCEPTANCE.md README.md` adds lines only. The README Limits row keeps every earlier sentence. No criterion was weakened. One imprecision (Low): the ACCEPTANCE xref bullet says that listing an object at or above 8,388,608 throws `PdfObjectTooLargeException`. That happens only when `/Size` and the decoded entries both cover the subsection. Otherwise the `/Size`, entry-count, or int-range check throws `PdfParseException` first. Both are document exceptions.

## Defects confirmed

**QA-B36-01. An xref stream at the new object-number cap still exhausts memory. Severity High (the residual of SA-03). Suggested owner: Core Engineer.**
- **Impact:** `PdfDocument.Open` on a tiny untrusted file. The SA-03 impact ("can terminate the process with `OutOfMemoryException`") is still reachable. The cap moved from 67M to 8.4M entries, but entries are not bounded by input size, and a `/Prev` chain repeats the cost.
- **Reproduction:** `1 0 obj << /Type /XRef /Size 8388608 /W [0 1 0] /Index [0 8388608] /Filter /FlateDecode >> stream <zlib of 8,388,608 zero bytes> endstream endobj startxref 9 %%EOF` (8,319 bytes). For a chain, repeat the object with `/Prev` pointing to the previous one.
- **Actual** (Release, net8.0, Windows, coverage off):
  - One section: 2,100,841,920 bytes allocated, peak working set 1.82 GB, 0.9 s, then `PdfParseException` (no `/Root`).
  - Three sections (24,926 bytes): 4.23 GB allocated, peak working set 3.43 GB.
  - With `DOTNET_GCHeapHardLimit=0x40000000` (1 GiB), the single 8,319-byte file makes `Open` throw `OutOfMemoryException`.
  - With 2 GiB, one section completes and three sections throw `OutOfMemoryException`. The live set grows with each section.
  - `LoadXref` allows 256 sections, so a file of about 2 MB can need far more memory than any host has.
- **Expected:** B-36 scope requires "bounded limits that fail with a document exception". The existing `HardeningTests` 96 MiB bound covers only the over-cap refusal, not a stream at the cap.
- **No regression test is added.** Current behavior would allocate gigabytes in the shared test process. The fix should land with a bounded test built from the reproduction above.

## Evidence missing

- Production-size (32 MiB) string refusal through `Open`. It is not run here either. It is shown only with a caller cap.
- Observation, not a B-36 blocker (Medium, existed before B-36): parsing an object-stream body allocates about 100 bytes per decoded byte. 31 MiB decoded (a 32,296-byte file) allocates 3.23 GB in about 6 s, but the live set fits under a 1 GiB heap limit. The Technical Lead decides whether to backlog it or seed the B-18 fuzz harness with it.

## Changes made

- `test/jaytwo.Ergonomics.Pdf.Tests/HardeningTests.cs`: 14 cases added (34 → 48):
  - `unterminated_nested_literal_string_is_refused_at_the_cap_before_eof` (3)
  - `token_of_exactly_the_documented_length_parses_and_one_more_byte_is_refused` (3)
  - `xref_stream_index_that_overflows_or_passes_the_object_cap_is_refused` (6)
  - `deep_page_tree_with_several_kids_per_level_refuses_sign_without_growing_the_destination` (2)
  - Supporting changes: the `WidePageChain` helper and the `FixedContents` provider.
- No `src/` change. The temporary probe file was deleted.

## Verification run

.NET SDK 10.0.100-rc.1 on Windows. The env sets `JAYTWO_QPDF` (qpdf 12.4.2), `JAYTWO_POPPLER` (26.09.0), `JAYTWO_PKCS11` (SoftHSM2), and `JAYTWO_OPENSSL`.

| Run | Result |
|---|---|
| `dotnet test -f net8.0` (Windows, full) | 260/260 passed, 46 s |
| `dotnet test -f net6.0` (Windows, full) | 260/260 passed, 44 s |
| `dotnet test -f net48` (Windows, full) | 260/260 passed, 46 s |
| `make docker-builder docker-test` (Ubuntu 26.04, Docker 27.5.1) | net8.0 260/260 passed, 41 s. net6.0 260/260 passed, 42 s. Exit 0 |
| Linux HardeningTests timing (coverage on) | The 150,000-key dictionary takes 1.0 s on net8.0 and 1.23 s on net6.0, against a 10 s bound. The token refusals (asserted under 2 MiB), the xref over-cap refusal (under 96 MiB), and the header-filler and overlap bounds all pass on both frameworks. |

## Decisions needed

- Technical Lead: assign the QA-B36-01 fix. It could be within B-36, or a new task that blocks B-36 acceptance. One option is to bound the xref-stream entry count by input or decoded size rather than by object number, and to bound the total across a `/Prev` chain. The fix design belongs to the Core Engineer.
- Product Manager: the SA-09 criterion sentence that was added to `ACCEPTANCE.md`, which the Technical Lead has already accepted.

## Handoff

Core Engineer fixes QA-B36-01 and adds its bounded regression test. Then QA re-verifies B-36 for sign-off.

## Re-verification (rework for QA-B36-01)

**QA sign-off: given for B-36.** QA-B36-01 is fixed. One new finding, QA-B36-02, covers classic xref sections. It existed before B-36, it is not amplified by compression, and it does not block B-36. It is recorded below for B-41.

### Verified facts

**Repros.** Run in a separate process (a Release net8.0 probe app in the scratchpad that references `src/`, opening from a `FileStream`) with `DOTNET_GCHeapHardLimit=0x10000000` (256 MiB):

| Input | Result |
|---|---|
| QA one-section repro, 8,325 bytes, `/Index [0 8388608]` | `PdfObjectTooLargeException` ("more than 65536 distinct objects"). 24.1 MiB allocated, peak working set 51 MiB, 75 ms. |
| QA three-section `/Prev` repro, 24,944 bytes | Same exception. 24.1 MiB, 50 MiB, 84 ms. |
| Own: one section at the ceiling (16 MiB padding, 2,097,152 entries) | Finishes with `PdfParseException` (no `/Root`), not OOM. 155 MiB allocated, 147 MiB peak working set. |
| Own: 16 `/Prev` sections, each adding 131,072 new numbers (exactly 2,097,152 unique) | Finishes with `PdfParseException` (no `/Root`), not OOM. 156 MiB allocated, 148 MiB peak working set. |
| Own: the same with a 17th section | `PdfObjectTooLargeException` ("more than 2097152"). 156 MiB, 148 MiB peak working set. |

**Revert check.** With `XrefEntryBudget` returning `int.MaxValue`:
- `xref_streams_listing_more_objects_than_the_file_can_back_are_refused_before_entries_are_built` (1 and 3 sections) fails: no `/Root` instead of the budget refusal.
- `xref_entry_budget_follows_the_file_length` fails.
- `xref_map_memory_grows_with_unique_objects_not_with_prev_sections` still passes. It measures the single merged map, not the budget.

I restored `PdfParser.cs` from the backup. Its SHA-256 (`b882cd94…`) and that of `git diff HEAD -- src` (`c9c79d5a…`) match before and after.

**Docs.** The constants match the docs: `XrefFileBytesPerEntry` 8, `MinXrefEntryBudget` 65,536, `MaxXrefEntries` 2,097,152, and the repro's 25 MB figure. The xref bullet now lists the `PdfParseException` cases first and conditions the 8,388,608 exception on `/Size` and the decoded entries, so my Low finding is fixed. `git diff HEAD -- ACCEPTANCE.md` removes no lines. The README diff removes only the old Limits row, and the new row keeps every earlier sentence. Minor: the README Limits row does not mention the `/Index` ordering rule. `ACCEPTANCE.md` does.

### Defects confirmed

**QA-B36-02. A classic xref section is built in full before the entry budget applies. Severity Medium. Suggested owner: Core Engineer, as part of B-41.**
- **Impact:** a large classic-xref file can exceed the D-20 256 MiB working budget and fail with `OutOfMemoryException` instead of a document exception. Memory is proportional to file size (classic entries are 20 file bytes each), and there is no compression amplification. The problem existed before B-36 and is no worse now.
- **Reproduction:** `%PDF-1.7`, then `xref 0 N` followed by N lines of `0000000000 65535 f\r\n`, a trailer `<< /Size N >>`, and `startxref`. Open the result from a `FileStream`.
- **Actual** under a 256 MiB hard limit:
  - N = 1,000,000 (20 MB): no `/Root`, 142 MiB peak working set.
  - N = 2,000,000 (40 MB, inside the budget): `OutOfMemoryException`.
  - N = 2,097,152: no `/Root`.
  - N = 2,200,000: `PdfObjectTooLargeException`.
  - N = 3,000,000 (60 MB): `OutOfMemoryException`.
  - The outcome is not monotonic in N, which suggests Dictionary resize sizes.
  - Without a heap limit, N = 3,000,000 gives the documented `PdfObjectTooLargeException`, with 871 MiB allocated and 319 MiB peak working set.
- **Expected:** per the rework's stated goal, `Open` stays within 256 MiB or throws a document exception first. Today the per-section `Dictionary` is unbudgeted, and it is live at the same time as the merged map.
- **No regression test added:** the test would fail now and needs about 40 MB inputs. It belongs with the B-41 fix.

### Changes made

`HardeningTests.cs`: added `xref_stream_index_subsections_must_ascend_without_overlap`, 5 cases covering descending, overlapping, and duplicate subsections, plus two ascending positive controls. This covers the new ordering rule, which had no test. `HardeningTests` now has 57 cases. No `src/` change.

### Verification run

| Run | Result |
|---|---|
| Windows `dotnet test -f net8.0` (full, with the tool env) | 269/269 passed, 43 s |
| Windows `net6.0` | 269/269 passed, 40 s |
| Windows `net48` | 269/269 passed, 40 s |
| `make docker-builder docker-test` (Linux) | net8.0 269/269, 40 s. net6.0 269/269, 41 s. Exit 0 |
| Linux `HardeningTests.xref_*` (in the builder image) | 17/17 on net8.0 and on net6.0. `xref_map_memory_grows_with_unique_objects_not_with_prev_sections` passed in 430 ms on net8.0 and 414 ms on net6.0, within its 128 B per entry and 48 B per extra-section entry bounds. |

### Handoff

The Technical Lead reviews and integrates B-36. The Product Manager accepts the 2,097,152 ceiling and the `/Index` ordering rule as limits, and boards QA-B36-02 under B-41 or separately.
