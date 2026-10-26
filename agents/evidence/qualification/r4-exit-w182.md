# R4 signing integration exit (W-182)

Checkpoint revision: `4cae92b009dc6db1894ac0d57d4ecc72637370f1`, the record-only W-182 activation over final implementation/evidence revision `0bcf5b8`. Both platform runs use the detached worktree `.tools/worktrees/w182`. This checkpoint assesses reset-plan section 10 and D-64; packaging and release qualification remain suspended.

## Evidence freshness

Compared the DSS-producing revision `71f9971` with the checkpoint across both runtime libraries, the DSS wrapper and both `DssInteropTests` files. The only change in those paths is the bounded archive downloader, `tools/test-tools/dss/download.py`; runtime, wrapper and classifier assertions are unchanged. W-103's Windows, Linux focused and Linux integration results therefore remain applicable. The current matrix also executes the DSS cases again.

Compared campaign revision `704ba5c` with the checkpoint across `src/` and `tools/fuzz/`: no changes. The completed CMS/RFC 3161 allocations and W-183 startup disposition remain fresh. Later W-181 changes add certification tests and evidence records, not parser or harness changes. A new two-hour campaign is not needed for this record-only checkpoint.

Reopened all four prior archives, verified each archive SHA-256 against its external index, and verified all 2,056 indexed file hashes: W-103 Windows 498, Linux focused 498, Linux integration 511, W-181 549. Records and original results:

- [DSS classification, cryptography and policy results](r4-dss.md), with three original archives and checksum indices.
- [Signing interoperability and Release resource observations](r4-signing-refresh.md), with `r4-signing-w181.zip` and its checksum index.
- [Completed CMS/RFC 3161 campaign](../../reports/2026-10-25-W-181-fuzz.md), including the initial failed startup, W-183 correction and complete rerun.

The DSS 6.3 mismatched-timestamp T-label limitation remains explicit: positive B-T checks require successful timestamp verification, not the label alone. Poppler does not validate document timestamps; OpenSSL and pyHanko supply that evidence. Explicit local test anchors establish the named test trust results, not public identity. Campaign evidence is neither an independent security review nor a full G8 pass.

## D-64 contract and named coverage

The framework TRX results are the execution evidence; the names below identify the assertions. Direct-engine coverage remains separate from public workflow coverage.

| Contract | Named coverage / evidence |
|---|---|
| Shape validation and consumption | `WorkflowSigningTests.shape_errors_are_thrown_before_the_input_opens`, `a_signing_workflow_runs_once_and_borrowed_streams_stay_open` |
| One original open and one editor | `the_original_input_opens_once`, `an_encrypted_input_is_edited_and_signed_through_one_editor`; source inspection of `StageSignedAsync` confirms one `session.Document.Edit()` followed by `Apply`, save and sign on that editor in both sync/async branches, without reopening |
| Revision ordering and unchanged inputs | `signing_an_unchanged_document_adds_one_signature_revision`, `edits_then_signing_add_an_edit_revision_and_a_separate_signature_revision`, `a_document_timestamp_adds_its_own_revision_and_an_embedded_timestamp_adds_none` |
| Existing fields and signed-prefix preservation | `an_existing_empty_signature_field_is_signed_by_name`; `WorkflowSigningInteropTests.signature_timestamps_verify_on_new_and_existing_fields`, `a_permitted_later_fill_keeps_the_signature_and_both_verifiers_detect_tampering`, `certified_workflow_fills_obey_docmdp_and_fieldmdp`; complete-prefix comparisons in both W-181 resource workloads |
| Staging and publication | `an_attachment_larger_than_the_signature_appendix_is_staged_and_signed`, `file_outputs_keep_the_alias_and_overwrite_rules`; shared `WorkflowFileSaveTests` cover publication, flush, alias and cancellation rules. W-181's isolated 1 GiB dense/attachment workloads measure managed growth, read bounds, staging disk, cleanup and publication after signing |
| Failure, cancellation and no automatic retries | `a_timestamp_authority_failure_leaves_the_outputs_unchanged_and_is_not_retried`, `a_signer_failure_before_the_copy_leaves_a_stream_and_an_existing_file_unchanged`, `a_reservation_overflow_fails_without_calling_the_signer_again`, `cancellation_before_the_copy_leaves_the_outputs_unchanged`, `a_failure_while_copying_sets_the_destination_back_to_empty` |
| Algorithms, formats, encryption and certification | Direct `SigningInteropTests`, `SigningAlgorithmTests`, `SignatureTests` and timestamp tests, including certification/field-lock refusals; separate workflow interoperability tests; both projects' `DssInteropTests`. W-181 records the supported RSA/P-256/P-384 and SHA-256/384/512 matrix, basic CMS/PAdES, encrypted roles, timestamps, permitted later revisions and tamper controls |
| Inspection and fuzz boundaries | `CmsFuzzTests`, deterministic nine-target smoke, and the completed direct CMS/RFC 3161 campaign; no runtime signature validation or new algorithm/profile introduced |

## Checkpoint execution

Windows full gate passed (exit 0, 26m57s): 22,146 passed, six Linux-only skips, zero failed. Each engine row includes its 24 serial clock cases. The netstandard2.0 library lane runs on net8.0. Workflow clock selections contain no cases and report no-match results; these are not additional test passes.

| Windows project | Framework | Passed | Skipped | Failed |
|---|---|---:|---:|---:|
| Engine | net10.0 | 4681 | 0 | 0 |
| Engine | net8.0 | 4207 | 0 | 0 |
| Engine | net6.0 | 4200 | 0 | 0 |
| Engine | net48 | 4200 | 0 | 0 |
| Engine netstandard2.0 lane | net8.0 | 4207 | 0 | 0 |
| Workflow | net10.0 | 195 | 2 | 0 |
| Workflow | net8.0 | 152 | 2 | 0 |
| Workflow | net6.0 | 152 | 2 | 0 |
| Workflow | net48 | 152 | 0 | 0 |

Windows skips are the two Linux-only symbolic-link cases in `WorkflowFileSaveTests` on each modern target; neither is a missing-tool skip. All 14 DSS test cases per project passed, with all 36 original invocation reports retained. Deterministic fuzz smoke passed all nine targets, including CMS seeds and 2000 mutations per target. SDK 10.0.402, Windows 10.0.26100; installed tool versions and `dotnet --info` are retained.

The first Linux attempt failed (outer make exit 2, 28m06s), despite zero failed test assertions: Coverlet 6.0.4 raised `Unable to read beyond the end of the stream` in `Coverage.CalculateCoverage` after the net10.0 workflow host's 195 passes. `unit-test` consequently stopped before report aggregation and fuzz smoke. The wrapper also failed to copy a temporary CLR debug pipe to Windows. This attempt is not a passing gate; its full log is retained. The partially copied result directory is not used as final Linux evidence.

Replayed only the affected workflow host in the exact same builder image, with coverage enabled and no source change: 195 passed, two platform skips, exit 0 in 1m13s, including successful coverage calculation. The coverage truncation did not reproduce; no product defect is demonstrated. The full Docker rerun then passed at the same revision, including concurrent coverage collection, report aggregation and fuzz smoke. No expectation or filter was changed.

Commands, in the detached worktree (Windows Docker recipes set `MSYS_NO_PATHCONV=1` and `MSYS2_ARG_CONV_EXCL=*`):

```text
dotnet tool restore
python tools/agents/quiet_run.py --name w182-windows --tests -- make "SHELL=C:/Program Files/Git/bin/bash.exe" unit-test
make "SHELL=C:/Program Files/Git/bin/bash.exe" quiet-docker-test DOCKER_TAG=jaytwo_ergonomics_pdf_w182_4cae92b "DOCKER_RUN_MAKE_TARGETS=test JAYTWO_DSS_EVIDENCE=/build/out/w182/linux/dss JAYTWO_R4_EVIDENCE=/build/out/w182/linux/signing JAYTWO_EVIDENCE_COMMIT=4cae92b"
python tools/agents/quiet_run.py --name w182-coverlet-replay --tests -- docker run --rm jaytwo_ergonomics_pdf_w182_4cae92b__builder dotnet test test/jaytwo.Ergonomics.Pdf.Tests --no-build -f net10.0 -p:CollectCoverage=true
```

The Docker quiet target wraps the required `make docker-builder docker-test`. Windows runs also set `JAYTWO_DSS_EVIDENCE=out/w182/windows/dss` and `JAYTWO_R4_EVIDENCE=out/w182/windows/signing` as absolute paths, and `JAYTWO_EVIDENCE_COMMIT=4cae92b`; `.tools/test.runsettings` is copied from the installed tool configuration, with its absolute tool paths. Full matrix runs use the repository's default Debug/coverage configuration. W-181's isolated Release/coverage-off resource observations remain separate. Builds enforce StyleCop and public XML documentation; existing third-party test-package support warnings on net6.0 are retained in the logs.

Linux full gate passed on rerun (exit 0, 13m50s): 17,794 passed, six platform skips, zero failed, all nine fuzz-smoke targets passed. All required Docker tools were present; no tool-availability check was skipped. Artifact copying completed without the first attempt's debug-pipe error. Engine totals include the serial clock cases:

| Linux project | Framework | Passed | Skipped | Failed |
|---|---|---:|---:|---:|
| Engine | net10.0 | 4681 | 0 | 0 |
| Engine | net8.0 | 4207 | 0 | 0 |
| Engine | net6.0 | 4200 | 0 | 0 |
| Engine netstandard2.0 lane | net8.0 | 4207 | 0 | 0 |
| Workflow | net10.0 | 195 | 2 | 0 |
| Workflow | net8.0 | 152 | 2 | 0 |
| Workflow | net6.0 | 152 | 2 | 0 |

Linux skips are `WorkflowFileSaveTests.a_destination_that_differs_from_the_input_only_by_case_is_refused` (Windows/OSX only) and `WorkflowExportTests.export_reads_no_bytes_that_the_engine_does_not` (Windows only), on each target. Windows runs cover both. Interoperability runs on net10.0 under D-51; the framework matrix retains the output-identity checks on every target. Both net10.0 projects passed all 14 DSS cases again, with the exact expected 36-report invocation set. Ubuntu 26.04, SDK 10.0.402, pyHanko 0.38.0, pypdf 6.21.1, qpdf 12.3.2, pdfsig 26.01.0, MuPDF 1.27.0, OpenSSL 3.5.5, SoftHSM 2.6.1; DSS/JDK remain pinned to 6.3/21.0.9+10. Original runtime/tool outputs are retained.

## R4 exit assessment

| reset-plan section 10 criterion | Result and basis |
|---|---|
| Direct-core and ergonomic signing produce independently verified results | **Pass.** Separate direct/workflow tests pass in both current net10.0 platform runs. W-181's pyHanko/pdfsig/OpenSSL properties and fresh DSS classification/crypto/trust results retain their separate claims and negative controls. |
| Workflow validation prevents unsafe operation ordering | **Pass.** The named shape, consumption, edit-before-sign, revision, certification and field-lock cases pass; inspection confirms one editor and save-before-sign on both execution branches. |
| Existing RSA, ECDSA, digest, CMS/PAdES, timestamp, encryption, certification and failure guarantees remain covered | **Pass.** Both full matrices pass the existing engine guarantees and separate public workflow coverage. W-181's exact supported matrix, large staging workloads and failure assertions remain applicable to the unchanged runtime. |
| Signing-specific fuzz and independent-verifier evidence is fresh for the final implementation | **Pass.** Source-tree identity and unchanged harness establish applicability of the completed W-181 CMS/RFC 3161 campaign. Current DSS cases and deterministic nine-target smoke pass on both platforms; prior archives and every indexed file hash were verified. |

No missing checkpoint assertion or newly demonstrated product defect required a code change. No check marked skipped, unsupported or not run is counted as a pass. The initial failed Docker attempt remains a failed attempt. This completes R4 and unblocks the remaining D-62 security phase only. R5 still follows that phase; packaging, release gates and production qualification remain suspended.

## Retained results

[Original reports, public fixtures, full gate logs, TRX results, tool/runtime versions and freshness review](r4-exit-w182.zip), SHA-256 `b729016d63b46bff6d8bcf797e5964f0c72fb6a1875f3d9a0178c8452266b607`, with a [result/checksum index](r4-exit-w182.json). All 1,403 indexed files were reopened from the archive and their SHA-256 hashes verified. The index checks final TRX provenance and totals, both 14-case DSS selections on each platform, and all 72 original DSS invocation reports against the expected case sets. The first failed Docker log and successful isolated coverage replay are included; partial first-attempt copies are excluded. No private keys are retained.
