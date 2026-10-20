# Fuzz campaign triage, 2026-10-19

Work item: W-18. This is the triage of the campaign's crash artifacts for the `open` and `edit` targets. It is not the campaign record: minimization, fixes, regression tests, the rerun, and the independent review are still open.

## Campaign facts

From `out/fuzz/run.txt` and `out/fuzz/summary.txt` (local, untracked):

- Build: B-41 working tree, dirty. The library and harness code in that build matched the B-18 harness checkpoint; the recorded `src` and `tools/fuzz` diff was empty.
- Started 2026-10-19T20:17:28Z. SharpFuzz 2.3.0, libfuzzer-dotnet v2025.05.02.0904, .NET 8.0.426, Linux 5.15.167.4-microsoft-standard-WSL2 x86_64, 8 CPUs, 7 workers.
- Limits: `-timeout=10 -rss_limit_mb=2048`.

| Target | Seconds | Exit | Seeds | Corpus | Artifacts |
| --- | ---: | ---: | ---: | ---: | ---: |
| open | 3103 | 0 | 51 | 1666 | 108 |
| edit | 3078 | 77 | 51 | 2220 | 141 |
| decode | 1548 | 0 | 11 | 1521 | 0 |
| object | 1028 | 0 | 6 | 737 | 0 |
| objstm | 1028 | 0 | 3 | 869 | 0 |
| tsr | 1046 | 0 | 8 | 123 | 0 |
| der | 773 | 0 | 6 | 61 | 0 |
| xmp | not yet complete | | | | not triaged |

The `xmp` target was still running when this triage was done (the campaign container was up), so `out/fuzz/artifacts/xmp/` was not read. `xmp` is recorded as not yet complete, not as a pass.

## Reproduction

Reproduced under W-18 (`W-18: resume for fuzz triage`; `src/` and `tools/fuzz/` matched the B-18 harness checkpoint), Release:

```
dotnet run --project tools/fuzz/jaytwo.Ergonomics.Pdf.Fuzz.csproj -c Release -- run open out/fuzz/artifacts/open
dotnet run --project tools/fuzz/jaytwo.Ergonomics.Pdf.Fuzz.csproj -c Release -- run edit out/fuzz/artifacts/edit
```

Logs were kept outside the repo. Signatures are the exception type plus the top three `jaytwo.Ergonomics.Pdf` frames.

## Outcome counts

| Target | Inputs | Accepted | Controlled rejection | Uncontrolled failure | Did not reproduce |
| --- | ---: | ---: | ---: | ---: | ---: |
| open | 108 | 0 | 0 | 108 | 0 |
| edit | 141 | 0 | 0 | 141 | 0 |

Every artifact is a failure the campaign already classed as uncontrolled, and every one reproduces. None are accepted or controlled rejections. The harness `run` mode reports only uncontrolled failures, so a controlled rejection would print `ok`; none did.

## Signatures

All four are unexpected exception types from a supported operation (`PdfDocument.Open` read walk, or an Info setter), so each is at least Medium. None is a hang or memory blowup, so none is High from observed behavior.

| Label | Target (count) | Exception and message | Top library frames | Artifacts | Smallest artifact | Severity | Suspected cause |
| --- | --- | --- | --- | ---: | --- | --- | --- |
| FZ-4 | open (71), edit (83) | `ArgumentOutOfRangeException`: "Hour, Minute, and Second parameters describe an un-representable DateTime." (open) | `PdfTextCodec.ParseDate` < `PdfEngine.GetInfoDate` < `PdfMetadata.get_CreationDate` | 154 | `crash-712cc9a93d169a1aed065ca50e6a32589f851ef9` (edit, 1253 bytes) | Medium | `ParseDate` builds a `DateTime` from out-of-range PDF date fields without range checks. |
| FZ-5 | open (10), edit (1) | `ArgumentOutOfRangeException`: "Hour, Minute, and Second parameters describe an un-representable DateTime." (open); "Year, Month, and Day parameters ..." (edit) | `PdfTextCodec.ParseDate` < `PdfEngine.GetInfoDate` < `PdfMetadata.get_ModificationDate` | 11 | `crash-4124af24570d0d158f998e27549cc52c78ccca97` (open, 1422 bytes) | Medium | Same defect as FZ-4, reached through `ModificationDate`. Likely one fix. |
| FZ-6 | open (27), edit (55) | `CryptographicException`: "The input data is not a complete block." | `PdfSecurity.AesEcb` < `PdfSecurity.ValidatePerms` < `PdfSecurity.CreateAes256` | 82 | `crash-042787f849e1ca2a238685e78f902dad9a2673e5` (edit, 1163 bytes) | Medium | Revision 5/6 `/Perms` is not 16 bytes, and `AesEcb` is called without a length check. This is in password-based open, so a hostile file escapes `PdfException`. |
| FZ-7 | edit (2) | `InvalidCastException`: "Unable to cast object of type 'Name' to type 'Dict'." | `PdfEngine.EditInfo` < `PdfEngine.SetInfoText` < `PdfMetadata.set_Title` | 2 | `crash-4b82f427e6c4f2ff2a90f394648beda6f4827a0d` (1067 bytes) | Medium | `/Info` resolves to a name, not a dictionary, and `EditInfo` casts without a type check. |

Totals: 154 + 11 + 82 + 2 = 249 = 108 + 141. FZ-4 and FZ-5 may be merged into one defect at disposition; they are kept apart here because the top frames differ.

## Non-reproducing artifacts

None. 249 of 249 reproduced.

## Still open

- Minimization of each signature's artifacts; the representatives above are only the smallest by size.
- Fixes for FZ-4 through FZ-7, owned by the metadata and security domains.
- Deterministic regression tests with minimized inputs added to the seed corpus.
- Triage of the `xmp` target once the campaign container finishes.
- Rerun of affected targets from the fixed code.
- The independent hostile-input review (`QUALIFICATION.md` section 10).
- 2026-10-19 disposition, FZ-4 and FZ-5: fixed under W-18. `PdfTextCodec.ParseDate` throws `PdfParseException` for out-of-range fields, matching its other unparseable-date errors. Tests: `out_of_range_creation_date_throws_parse_exception`, `out_of_range_modification_date_throws_parse_exception`. Seeds: `tools/fuzz/regressions/edit/fz4-crash-712cc9a9`, `tools/fuzz/regressions/open/fz5-crash-4124af24`.
- 2026-10-19 disposition, FZ-6: fixed under W-18. `PdfSecurity.ValidatePerms` rejects a `/Perms` that is not 16 bytes with `PdfEncryptedException` before `AesEcb`. Test: `aes256_perms_of_the_wrong_length_throws_encrypted_exception`. Seed: `tools/fuzz/regressions/edit/fz6-crash-042787f8`.
- 2026-10-19 disposition, FZ-7: fixed under W-18. `PdfEngine.EditInfo` treats a non-dictionary `/Info` as absent, as the getter does. Replaying the artifact then exposed a second defect, a catalog `/Names` that is not a dictionary, which made `WriteAttachments` throw `InvalidCastException`; it is fixed the same way in the same commit, treating it as absent as the reader does. Tests: `non_dictionary_info_reads_absent_and_an_edit_replaces_it`, `non_dictionary_names_reads_absent_and_an_attachment_add_replaces_it`. Seed: `tools/fuzz/regressions/edit/fz7-crash-4b82f427`.

## Campaign completion (appended 2026-10-19)

- The campaign finished at 2026-10-19T23:43:59Z (`out/fuzz/run.txt`). All eight targets are in `out/fuzz/summary.txt`.
- `xmp`: 776 s, exit 0, 3 seeds, corpus 7, artifacts 0. Nothing to triage. Its final coverage was `cov: 55`, compared with 6,734 for `open`. This is an instrumentation limit, not evidence of robustness. `XmpEditor.TryParse` parses with `XDocument.Load` from the .NET base library, and SharpFuzz instruments only `jaytwo.Ergonomics.Pdf.dll`, so libFuzzer has no coverage feedback inside the XML parser, and most mutants fail to parse and return early. Count `xmp` as weak evidence for G8 until it has structured XMP seeds or a dictionary.
- Budget: the per-target seconds sum to 12,380, times 7 workers, which is about 24.07 CPU-hours estimated from wall time. The schedule was 12,300 s (23.92 CPU-hours), and per-target wrap-up added about 80 s. That exceeds D-23's 24 CPU-hour cap by about 0.07 CPU-hours by this estimate. Actual CPU time was not measured and is likely lower, because other load shared the 8 CPUs.
- `edit` exited 77 in `summary.txt`, while every other target exited 0. Its 141 artifacts are triaged above. The meaning of exit code 77 under `-fork` with `-ignore_crashes=1` has not been checked.

