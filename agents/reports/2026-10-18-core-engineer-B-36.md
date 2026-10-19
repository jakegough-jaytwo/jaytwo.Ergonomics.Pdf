# B-36 parser and decoder hardening

## Task and owner

B-36, Core Engineer. Fix audit findings SA-01, SA-02, SA-03, SA-04 (High) and SA-09, SA-11 (Medium) from `2026-10-18-security-auditor-B-18.md`, under the disposition in `2026-10-18-tech-lead-B-18-audit-review.md`. Base is `wip` at `da08d21`. Nothing is committed.

## Verified facts

I confirmed each trigger against `da08d21` by running the new regression tests against the unfixed source in a scratch worktree. Only a compile shim was added there: the new constants, plus a parser constructor that ignores caps. Of 34 tests, 32 failed and the 2 positive controls passed. The fixed tree passes all 34 on net8.0, net6.0, and net48.

| Finding | Fix | Limit and reason | Regression test(s) | Before → after |
|---|---|---|---|---|
| SA-01 | Literal strings check the cap at the top of every loop iteration, so the escape, octal, nested-parenthesis, and CR branches are covered. Hex strings check before each byte, including an odd final nibble. Names, numbers, and keywords check length before each byte. | Strings: 32 MiB, the existing `StructuralCap`. Name, number, keyword: 65,536 bytes (`PdfParser.MaxTokenBytes`). Real tokens are tens of bytes, and 64 KiB keeps one refusal under 2 MiB of allocation. Exception: `PdfObjectTooLargeException`. | `long_name_number_and_keyword_tokens_are_refused_within_a_small_allocation` (3 cases); `every_string_branch_respects_the_string_cap` (7 branches, caller cap 64) | 1 MiB name: accepted. Number and keyword: a different `PdfParseException` after buffering the token. Every string branch over the cap: accepted. **After:** `PdfObjectTooLargeException`; a 1 MiB token refusal allocates 0.28–0.32 MB. A string at exactly the cap parses. |
| SA-02 | `ParseDictionary` scans linearly only while a dictionary has fewer than 16 entries. After that it keeps a key-to-position map and calls the new internal `Dict.Append`/`ReplaceAt`. A repeated key still replaces in place, so the last value wins. Arrays and dictionaries count their entries. | 1,048,576 entries (`PdfParser.MaxContainerEntries`). Repeated dictionary keys count, so total work is bounded. That is generous for flat `/Kids`, name-tree `/Names`, and `/Nums` arrays. | `dictionary_with_many_distinct_keys_is_parsed_without_a_per_key_scan` (150,000 keys, under 10 s); `array_and_dictionary_entry_counts_are_capped` (array, distinct keys, repeated key; caller cap 16); `production_array_cap_applies_when_a_document_opens` | Dictionary: **3 min 4 s → 1.3 s**. Over-cap array, dictionary, and repeated key: accepted → `PdfObjectTooLargeException`. |
| SA-03 xref | `ParseXrefEntries` checks every `/Index` subsection before it builds any entry. The checks, in order: the existing object-number range; the cumulative count against the decoded entries (existing message kept); end at or below `/Size` when `/Size` is present (`PdfParseException`); and end at or below the cap (`PdfObjectTooLargeException`). | Object numbers in an xref stream stay below 8,388,608 (`MaxXrefStreamObjects`): the PDF 1.7 Annex C indirect-object limit plus object 0. It caps object numbers, not entry count, so the union of every xref stream in a `/Prev` chain also stays under 8.4M keys. Classic sections stay bounded by file bytes. | `xref_stream_entry_count_is_refused_before_entries_are_built` (`/W [0 1 0]`, 8,388,609 entries); `xref_stream_subsection_past_size_is_refused` | Before: 8.4M entries built, then a failure on the missing `/Root`. **After:** refused before any entry is built; total allocation 42.0 MB, which is the Flate decode itself (bound in the test: 96 MiB). `/Size` overrun: wrong error → `/Size` error. |
| SA-03 objstm | `/N` and `/First` are validated before decoding: N pairs need 4N − 1 header bytes. The header is read pair by pair from the byte array by `PdfParser.ParseObjectStream`. No header string or token list is built. `SplitTokens` is removed. **New in this task (not in the audit):** a work budget for overlapping objects. Many header offsets into nested `((((…))))` re-read the stream for each object, which costs quadratic CPU and memory. | Parsing the objects reads at most 4 × the decoded length + 64 KiB (`ObjectStreamReadFactor`), or the parse throws `PdfObjectTooLargeException`. The factor covers the parser's lookahead of up to 2 tokens into the next object in real layouts. | `object_stream_header_is_read_pair_by_pair_without_tokenizing_the_rest` (4M filler tokens in the header vs. the same bytes in the body); `object_stream_count_that_cannot_fit_before_first_is_refused`; `overlapping_object_stream_objects_are_refused_within_a_work_bound` | Header filler: **293.7 MB vs. 42.0 MB baseline → 42.03 MB vs. 42.07 MB**. Overlap: accepted after about 67 MB of re-reads → refused after 1.86 MB of allocation. |
| SA-04 | `WalkPages` takes a depth and throws `PdfParseException("The page tree is too deep.")` above `MaxTraversalDepth`. The other recursion over attacker-controlled data was already bounded: `WalkField` and `WalkNames` at 256, parser nesting at 256, and `Load`/`Resolve` chains at 256. `NearestFieldValue` is an iterative loop with a seen-set. | 256 levels below the root, the existing `MaxTraversalDepth`. Real trees are about log(pages) deep. | `deep_page_tree_is_a_parse_error_instead_of_a_stack_overflow` (300 levels; also checks `PageCount` and that the bytes are unchanged); `page_tree_within_the_depth_limit_still_opens` (200 levels) | 300 levels: accepted → `PdfParseException`. The test never recurses deeply enough to overflow. |
| SA-09 | An omitted `/StrF` or `/StmF` defaults to `Identity`. **I also applied this to V5 (revisions 5 and 6)**, because ISO 32000-2 gives the same default and the code had the same `StdCF` substitution. | n/a | `omitted_string_or_stream_crypt_filter_defaults_to_identity`: revision 4 with StrF omitted, StmF omitted, and both omitted; revision 6 with each omitted. Each case opens, reads the title and an attachment, saves a title change plus a new attachment, checks plaintext vs. ciphertext in the appendix, and reopens. | All 5 failed ("Encrypted data/stream is truncated") → all 5 pass. |
| SA-11 | `FiltersOf` rejects a non-name scalar and any non-name array element. `ParmsOf` rejects a single dictionary for more than one filter, an array whose length differs from the filter count, and any entry that is not a dictionary or null. A null `/DecodeParms`, or one with zero filters, is ignored. Exception: `PdfParseException`. | n/a | `malformed_filter_or_misaligned_decode_parms_are_refused` (6 cases, file bytes unchanged); `well_formed_filter_arrays_with_null_parms_still_decode` | All 6 returned bytes without error → `PdfParseException`. The positive control passes before and after. |

## Assumptions

- `/Filter` or `/DecodeParms` given as an indirect reference is now refused rather than ignored. Ignoring it produced wrong bytes, and the decoder has no resolver.
- An xref stream with no `/Size` skips only the `/Size` check. The object-number cap still applies.
- The production-size string cap (32 MiB) is exercised through the parser with a caller cap, as `LzwTests` and `RunLengthTests` already do. An end-to-end 32 MiB test took 6 s or more under coverlet, so I dropped it. The production constructor passes `StructuralCap`, and the plain branch had that cap before.

## Defects confirmed

All six findings were reproduced, as the before column shows. The object-stream overlap amplification is a further defect that this task found and fixed (row SA-03 objstm).

## Evidence missing

- Production-size string-cap refusal through `PdfDocument.Open`. It is not run, for the reason above.
- An independently produced revision-4 file that omits the defaults. The SA-09 evidence comes from `StandardEncryptor` and is at the internally tested level.
- No measurement of the extra memory from the dictionary position map in real workloads. It applies only to dictionaries with 16 or more entries. `DenseMemoryTests` and `LargeWorkloadTests` still pass.

## Changes made

- `src/jaytwo.Ergonomics.Pdf/Internal/PdfParser.cs`: token, string, and container caps; the internal constructor that takes caller caps; linear dictionary parsing; xref-stream subsection validation; the new `ParseObjectStream` with the pair-by-pair header and the work budget.
- `Internal/PdfObject.cs`: `Dict.Append` and `Dict.ReplaceAt` (internal).
- `Internal/PdfStreamDecoder.cs`: strict `FiltersOf` and `ParmsOf`. They live here, not in `PdfEngine.cs` as the board row says.
- `Internal/PdfSecurity.cs`: Identity defaults for revision 4 and V5.
- `Internal/PdfEngine.cs`: the depth parameter on `WalkPages`. **Boundary deviation:** in `ParseObjectStream`, `/N` and `/First` are now validated before decode and the body delegates to `PdfParser.ParseObjectStream`; `SplitTokens` is deleted. That region was not in the claim, but the brief's SA-03 instruction can only be met there. About 30 lines; Technical Lead to review or revert.
- `test/.../HardeningTests.cs` (new, 34 cases); `StandardEncryptor.cs` gained an additive `Dictionary(extra, stringFilter, streamFilter)` overload. `PdfBuilder.cs` is unchanged.
- `README.md` Limits row and a new `ACCEPTANCE.md` "Limits" section, one bullet per limit. No existing criterion was changed.

## Verification run

Tools: .NET SDK 10.0.100-rc.1, Windows. Coverlet is on by default.

- `dotnet build src/jaytwo.Ergonomics.Pdf -c Release --no-incremental`: net8.0, net6.0, netstandard2.1, and netstandard2.0. 0 warnings, 0 errors (`TreatWarningsAsErrors`, StyleCop on).
- `dotnet test -f net8.0|net6.0|net48 --filter FullyQualifiedName~HardeningTests`: 34/34 passed on each, in 7–8 s per target.
- `dotnet test -f net8.0` filtered to OpenTests, EncryptionTests, ForeignPdfTests, ObjectStreamCacheTests, AcroFormTests, AttachmentTests, MetadataTests, LzwTests, RunLengthTests, ScaleTests, DenseMemoryTests, and LargeWorkloadTests, with `JAYTWO_QPDF`, `JAYTWO_POPPLER`, `JAYTWO_PKCS11`, and `JAYTWO_OPENSSL` set: **158/158 passed**. Per class: Open 51, Encryption 36, AcroForm 17, Lzw 14, RunLength 14, Metadata 9, Foreign 6, Attachment 3, LargeWorkload 3, Scale 3, DenseMemory 1, ObjectStreamCache 1.
- Before-fix run: the same 34 tests against `da08d21` plus the compile shim, net8.0, coverage off. 32 failed and 2 passed (the positive controls). The worktree was removed afterward.
- I did not run the full suite or the Docker build.

## Decisions needed

- Technical Lead: accept or revert the `ParseObjectStream` change outside the claimed region, and the overlap work budget, which goes beyond the audit text.
- Technical Lead and Product Manager: extending SA-09 to V5. If accepted, the encryption criterion needs a sentence. The brief limited me to Limits rows, so I propose it here: "A revision 4, 5, or 6 file that omits `/StrF` or `/StmF` treats those strings or streams as `Identity`: they are read in the clear, written in the clear, and reopen." The Signing Engineer is consulted for the signature-dictionary exemption. It is unaffected, because a signature dictionary is plaintext either way.
- AcroForm Engineer: consult on the `WalkPages` depth bound, per the board row. I did not contact them.

## Handoff

QA Engineer verifies the acceptance bullets. Specifically: the string cap is shown only through a caller cap; the 10 s dictionary timing and the allocation bounds hold on Linux net8.0 and net6.0 in Docker; and no committed fixture regresses in classes I did not run (SignatureTests, InteropTests, SigningInteropTests, AppearanceRenderTests). Then the Technical Lead reviews and runs the full suite and the Docker build at the checkpoint.

Not fixed and out of scope, for the backlog: classic xref subsection `start` and `count` are cast from `long` without a range check (`PdfParser.ParseClassicXref`).

## Rework (QA-B36-01)

**Task.** Fix QA-B36-01 from `2026-10-19-qa-engineer-B-36.md`, returned once by the Technical Lead. The goal: `Open` stays within the D-20 budget of 256 MiB peak managed-heap growth, or fails with a document exception first. Memory must not grow with `/Prev` sections beyond the count of unique object numbers.

**Design.** Three parts. Together they bound the merged xref map absolutely and by file length, and the map is the only copy.

1. **Entry budget.** The distinct object numbers in the merged map are capped at `min(2,097,152, max(65,536, fileLength / 8))`. The constants are `PdfParser.MaxXrefEntries`, `MinXrefEntryBudget`, and `XrefFileBytesPerEntry`. A stream section is refused before any entry is built when its `/Index` total exceeds the budget. Every merge, stream or classic, refuses a new key past the budget. Either refusal throws `PdfObjectTooLargeException`.
   - *Why 8 bytes:* an in-use object needs at least 19 file bytes (`1 0 obj null endobj`), so the ratio binds only on entries the file cannot back.
   - *Why the 65,536 floor:* small files keep headroom for free-entry ranges, and the floor costs at most about 5 MB.
   - *Why the 2,097,152 ceiling:* a file-length ratio alone is not enough. A padded 64 MiB file listing 8,388,608 entries allocated 686 MB and hit `OutOfMemoryException` under a 512 MiB heap limit.
   - *Worst case at the ceiling:* the map plus one Dictionary resize is about 84 bytes × 2.1M ≈ 176 MB.
   - No array of `/Size` is allocated.
2. **No per-section copy.** `ParseXrefEntries` now merges each xref-stream section straight into `_xref`, using the existing rule (the top section overwrites, older sections keep newer entries). A classic section's file-bounded dictionary is released before the `/Prev` chain continues. As a result, the encryption lookup for a stream section reads the merged map.
   - **New rejection:** `/Index` subsections must ascend without overlap, which is ISO 32000 7.5.8.2. Without this, first-wins merging would change the within-section result.
3. **Compact entry.** `XrefEntry` is now one packed `long`: 2 bits of type (3 means any other type), 40 bits of field 1, and 22 bits of field 2.
   - A Dictionary slot drops from 44 bytes to 28.
   - A type-1 offset outside the range is stored as 2^40−1, past any real file, so `Load` still fails with `PdfParseException`.
   - An object-stream number keeps its low 32 bits, the same as the old `(int)` cast.
   - Field 2 saturates, and a negative value still reads back negative. Nothing reads field 2 except the object-stream index sign check.

**Repro numbers** (Release net8.0, Windows, coverage off, probe app in the scratchpad):

| Input | Before (QA) | After |
|---|---|---|
| 1 section, 8,319 bytes, `/Index [0 8388608]` | 2.10 GB allocated, 1.82 GB peak working set; `OutOfMemoryException` under a 1 GiB limit | 25.3 MB allocated, 60 MB peak WS, 0.07–0.12 s; `PdfObjectTooLargeException`; same under a 256 MiB `DOTNET_GCHeapHardLimit` |
| 3 sections, 24,926 bytes | 4.23 GB, 3.43 GB peak WS | 25.3 MB, 60 MB; `PdfObjectTooLargeException`; same under 256 MiB |
| Ceiling: 16 MiB padding, 2,097,152 entries, 1 section / 3 sections | not measured | 163 MB / 176 MB allocated. Under a 256 MiB limit, 3 sections finish with the document exception (no `/Root`) |
| 64 MiB padding, 8,388,608 entries | not measured (686 MB and OOM under 512 MiB in the interim design, before the ceiling) | 25.3 MB; `PdfObjectTooLargeException` |

Peak working set includes the probe's own copy of the PDF.

**Tests** (in `HardeningTests.cs`, now 52 cases with QA's additions):

| Test | Before rework | After |
|---|---|---|
| `xref_streams_listing_more_objects_than_the_file_can_back_are_refused_before_entries_are_built` (1 and 3 sections) | wrong exception (no `/Root`) after all entries were built | refused, 0.87–0.92 MB allocated (bound: 8 MiB) |
| `xref_entry_budget_follows_the_file_length` (65,537 entries: unpadded file refused, padded file opens) | the unpadded file opened | as designed |
| `xref_map_memory_grows_with_unique_objects_not_with_prev_sections` (valid one-page files, 16,384 and 65,536 entries, 1 and 3 sections) | 223.2 B per entry, 120.9 B per extra-section entry | 86.7 B and 23.3 B (bounds: 128 and 48) |

The reduced in-process counts are 262,144 and 65,536. A regression would cost hundreds of MB, not gigabytes. The full-size repro ran only in the separate probe process.

The "before" figures for the tests come from a scratch worktree holding B-36 without the rework. There, `XrefEntry` is the HEAD version, the per-section dictionary is restored, and the budget is unbounded. The worktree was removed afterward.

**Docs.**
- In `ACCEPTANCE.md`, the xref bullet is rewritten (QA's Low finding). `PdfParseException` is listed first for range, order, decoded-count, and `/Size` failures. The 8,388,608 `PdfObjectTooLargeException` applies only when `/Size` and the decoded entries cover the subsection.
- A new bullet states the entry budget and the repro outcomes.
- The `README.md` Limits row gains one sentence.
- The Technical Lead's SA-09 sentence is untouched.

**Verification.**
- `dotnet build src/jaytwo.Ergonomics.Pdf -c Release --no-incremental` (all four targets): 0 warnings, 0 errors.
- `HardeningTests`: 52/52 on net8.0, net6.0, and net48.
- net8.0 with the tool env vars: `OpenTests`, `ObjectStreamCacheTests`, `ForeignPdfTests`, `ScaleTests`, `DenseMemoryTests`, and `LargeWorkloadTests` passed **65/65** (51, 1, 6, 3, 1, 3).
- Extra net8.0 check, because the encryption lookup and `XrefEntry` changed: Encryption, AcroForm, Attachment, Metadata, Lzw, RunLength, and Signature tests passed 137/137.
- Not run: the full suite and Docker.

**Files.** `Internal/XrefEntry.cs`; `Internal/PdfParser.cs` (budget, `MergeXrefEntry`, merge-in-place `ParseXrefEntries`, ascending `/Index`); `Internal/PdfEngine.cs` (`LoadXref` and `ReadXrefStream` merge region only); `HardeningTests.cs`; `ACCEPTANCE.md`; `README.md`.

**Decisions needed.**
- Technical Lead and Product Manager: the 2,097,152 ceiling is a product limit. It refuses a legitimate file with more than 2M distinct objects, which opened before if the host had the memory. It is the price of a hard 256 MiB bound with `Dictionary` storage. Raising it needs a custom map type in a new file.
- Technical Lead: the new rejection of non-ascending or overlapping `/Index` subsections.

**Handoff.** QA re-verifies QA-B36-01 and the slope test on Linux.
