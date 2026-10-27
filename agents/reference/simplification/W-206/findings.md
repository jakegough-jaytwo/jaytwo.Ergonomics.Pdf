# W-206: file-alias safeguards after W-185

Research date: 2026-10-26 (owner timezone). Baseline `6a2b8003fbdc2b745e5d5a16418f443288e42a82`, W-205 committed, initially clean worktree. One agent; no independent review claimed. D-65 remains binding. This is a bounded filesystem investigation, not release qualification or a request to implement a change.

## Recommendation

Keep the current safeguards and the W-185 shared publication lifecycle. No removal has been demonstrated safe under the existing refusal contract, and no new identity abstraction is justified. The metadata heuristic has a measured false positive: a separate file with the input's length and last-write time is refused. That cost is already documented. It is not evidence that the heuristic has no purpose. Windows sharing cannot substitute for the checks on Linux, and a path comparison cannot recognize a hard link.

No product defect was established. In particular, this investigation does not claim that removing the heuristic would corrupt an inode: replacing one hard-link entry preserved the other entry's old bytes in both measured environments. The heuristic enforces a conservative possible-input refusal, rather than proving universal identity or providing filesystem isolation. Removing that refusal requires an owner contract decision, not a behavior-preserving cleanup.

## Authority and production callers

Sources read: D-65, D-66, W-185/W-188/W-205, simplification charter PLAN 16/O 11, and the workflow paragraphs of `docs/behavior.md`. `WorkflowFiles.cs` explicitly identifies F17/D-60. No PDF-standard interpretation was needed; `C:\jake\git\pdfdocs` was not consulted. Missing external R1A/R1B/R2A/R2B attachments and fresh-context review remain W-188/W-211 gaps; no claims are attributed to unseen reviews.

Graph navigation: `graphify query "workflow file alias safeguards FileSaveLifecycle same path hard link symbolic link publication"`, `graphify explain WorkflowFiles`, `graphify query "WorkflowInput Normalize ValidateFileTarget SaveFileAsync SignFileAsync"`, `graphify explain PdfCombineWorkflow`. Raw reads followed located files.

| Production location at baseline | Role and ordering |
| --- | --- |
| `PdfWorkflowBuilder.cs:645` `SaveFileAsync` | Full target path/directory, known input alias and overwrite preflight before opening input; session/editor remain alive across shared publication. |
| `PdfWorkflowBuilder.Signing.cs:311` `SignFileAsync` | Same file preflight after signing argument validation; signing/document timestamps complete privately before publication. Same helper serves both terminal kinds. |
| `PdfCombineWorkflow.cs:82` `RunAsync` | Preflight compares every known input path, including a destination `FileStream` name; opens all input sessions, passes all known paths to publication, disposes sessions afterward in `finally`. |
| `Internal/WorkflowInput.cs:30,77` `FilePath`, `OpenAsync` | Known identity hint is the bound file path or a directly supplied `FileStream.Name`. Owned path input uses `FileShare.Read`. Arbitrary streams and `IPdfSource` have no discoverable backing path; borrowed resources stay borrowed. |
| `Internal/WorkflowFiles.cs:37,70` | `WriteAndPublishAsync` owns unique sibling `CreateNew`, writing, configured flush, disposal, cancellation check, `Publish`, success and best-effort cleanup. `Publish` rechecks every known input immediately before managed move/replace. |

## Safeguard inventory and disposition

| Safeguard | Actual protected condition / limitation | Disposition |
| --- | --- | --- |
| Full-path normalization and platform case comparison (`SamePath`, lines 12–25) | Dot/relative spelling and ordinary Windows/macOS case aliases. Case comparison is a platform policy, not filesystem-volume identity; Linux uses ordinal comparison. | Keep. Windows/Linux spelling probes agree; macOS and Windows case-sensitive directories unmeasured. |
| Every-segment link/reparse traversal (`Normalize`, line 176) | Intermediate directory junction/symlink as well as final links can name the input. .NET 6+ resolves links; older targets conservatively refuse reparse points. Recursion bound and resolution failures prevent unbounded traversal. | Keep. Removing final or intermediate traversal misses distinct aliases. Junction measured on Windows; symlinks measured on Linux. |
| Pre-open known-alias refusal in each terminal | Avoids input reads and, for signing, host signing calls for a destination already recognized as an input. Terminal-specific argument ordering remains significant. | Keep. A final-only check would change timing and could invoke expensive/nonrepeatable host code first. |
| Detected-existing destination check before open and at publication | Explicit overwrite required; detects a destination introduced while processing. Does not implement atomic create-if-absent across operations. | Keep D-65 wording. Concurrent tests judge completed output/cleanup, not exclusive publication. |
| Repeat path check for every input at publication | Detects aliases introduced during work before the final check; preflight alone is insufficient. | Keep. Late hard link is caught by metadata; final path comparison separately covers resolvable path aliases. Post-check mutation is still possible. |
| Existing destination length + last-write equality (`Publish`, lines 98–103) | Possible hard-link refusal, including a late hard link. Also refuses independent copies whose metadata matches. It is a heuristic and not sufficient identity evidence. | Keep under current contract. Independent-copy false positive measured, already documented. Zero approved deletion savings. |
| Input metadata read fails closed | Missing path metadata must not silently turn into permission to overwrite. Open handles do not ensure path names remain valid on Unix. | Keep. Missing path refused; Linux open-handle rename/delete succeeded while Windows blocked both. |
| Inputs open through publication | Windows sharing restricts rename/delete of those inputs. On Linux an open descriptor still reads original bytes after unlink/rename, but does not freeze the name. | Keep ownership/lifetime. Do not promote it to universal path isolation. |
| Unique sibling, flush/close before move/replace | Processing/flush/cancellation failures do not publish partial PDF work. `CreateNew` prevents opening an existing temporary pathname for overwrite; GUID-derived name is not a directory security boundary. | Keep W-185 shared owner; no destination framework or duplicated terminal lifecycle. |
| `TryDelete` after unsuccessful publication | Attempts removal without masking original failure for IO/access cleanup failures. An open Windows handle without delete sharing can prevent removal; Unix unlink can succeed. | Keep best-effort policy. Locked-file probe is a primitive demonstration, not a reproduced leaked workflow temporary. |

Managed-platform limits are intentional D-65 constraints: no portable universal inode/file-ID test, no native identity/publication calls, no new runtime dependency, no cross-process lock framework, no hostile-directory or transactional-filesystem guarantee. Stable input names and caller-controlled output directories remain necessary. A borrowed custom source's physical backing identity cannot be inferred. Directory durability and power-loss survival are not claimed.

## Measured evidence

`probe/Program.cs` links the exact production `WorkflowFiles.cs` and existing test-only `FileLinks.cs`. Payloads are tiny ASCII byte sequences, not PDFs, so these cases isolate filesystem behavior without testing parsing or cryptographic validity. The fixture is generated in source; hashes of input and output appear in the metadata cases. The helper is compiled from baseline source, not reimplemented. This is disposable research material, not a permanent regression suite.

| Evidence | Observation | Limit |
| --- | --- | --- |
| E206-W: `windows.json` | Windows 10.0.26100, NTFS, .NET 10.0.13. 20 named cases; 17 exercised, 3 unavailable file-symlink cases; 48 assertions; no unexpected failures. Directory junction resolves. Same/dot/case comparison, hard links open/closed, equal/different timestamps, late hard link, unavailable metadata, rename/delete sharing, new/replace/write-failure/cancel/publication-failure/cleanup and old hard-link content exercised. | File symlink creation returns Win32 1314; no privileged file-symlink or arbitrary reparse-provider claim. First run exposed this setup limitation; runner then classified only the specific privilege failure as unavailable and reran from fresh scratch. |
| E206-L: `linux.json` | Ubuntu 26.04.1, .NET 10.0.13, cached Docker builder, `/work` tmpfs. All 20 cases exercised, 49 assertions, no unexpected failures. Final symlink alias resolves, dangling target normalizes to its absent target path, cycle throws `IOException`. Open input rename/delete succeeds; read handle still works. | Container-local tmpfs only; not ext4/XFS/network filesystem, macOS or all supported runtimes. A dangling normalized path is not proof that a target exists. |
| E206-T10: existing focused Windows net10 tests | 27 passed, 0 failed, 3 platform skips. Alias, intermediate junction, hard link, independent-copy false positive, changed timestamp, late hard link, concurrent publication, overwrite, cancellation and cleanup coverage in `WorkflowFileSaveTests`, plus signing preflight and every-combine-input checks. | Linux-only symbolic-link and missing-metadata workflow tests skipped. No race reproduction inferred from a passing concurrent test. |
| E206-T48: same filter on net48 | 27 passed, 0 failed, 1 platform skip. Older-target directory reparse refusal exercised by the existing conditional test. | File-symlink tests compile only on modern targets; missing-path test is Linux-only. |
| E206-C: focused combine failure net10 | 1 passed, 0 failed/skipped: `file_failure_preserves_existing_destination_and_closes_all_owned_inputs`. | Separate run corrected an initially nonmatching filter suffix; first two runs did not select this case. |

Existing coverage anchors: `WorkflowFileSaveTests.cs:18` intermediate directory link pre-open timing; `:70` late destination; `:88` late hard link; `:111` missing input metadata; `:136` concurrent publication; `:391` metadata-copy refusal; `:421` hard link; `:435,454` Linux symlink behavior. `WorkflowSigningTests.cs:348` alias/overwrite preflight and zero signer calls on refusal. `WorkflowCombineTests.cs:164,178` destination preservation, owned input closure and every-input safety. Locations identify baseline code; they are not a claim that unrun Linux workflow tests passed.

## Inferences and open evidence

- Source inspection, not a forced race: a rename or directory-link change after final comparison can redirect publication or invalidate a temporary pathname. There is no filesystem transaction holding normalized names stable. Existing concurrency tests passed on Windows but do not show that the Linux double-success race reproduced here; D-65 records its earlier reproduction and remains authoritative.
- Windows sharing is one additional defense, not the alias specification. Linux rename/delete probes show why substituting Windows's refusal for the cross-platform policy would be unjustified.
- Missing evidence: Windows privileged file symlinks, unknown/unresolvable reparse providers, macOS, case-sensitive Windows directories, network filesystems, Linux public workflow tests at this revision, other Linux runtime targets, hostile-directory post-check swaps, crash/power-loss and universal file identity. No stronger guarantee follows from the two local filesystems.
- W-185's Linux closeout was explicitly unrun; this packet supplies narrower Linux helper evidence, not a retroactive pass for W-185's entire terminal suite. W-205 rollback-state and signed-stream restoration findings remain separate; alias checks do not repair a failed destination.
- No independently reviewed recommendation or complete reconciliation of unseen review attachments is claimed. These gaps do not prevent a keep recommendation; they prevent using this study as authorization to weaken safeguards.

## Cost, compatibility and follow-through

Business use: preserve original invoices/contracts and prior outputs while producing edited, combined or signed copies. Cost: a timestamp-preserving duplicate at the destination may be refused even with `Overwrite`; caller can choose a fresh output path. A caller needing an incremental change to the original uses the appropriate append API; signing file terminals continue to require separate output. Do not advise falsifying metadata or deleting a destination as an automatic bypass.

No additional recovery or credential burden is introduced; rerunning signing still can incur host signing/timestamp cost, and no automatic retry is proposed. Input ownership and publication ordering remain unchanged. Host serialization, stable paths, directory control and checking any physically failed destination remain host responsibilities.

Responsibility accounting: recommended deletions **0** runtime branches/files/lines; recommended additions **0** runtime branches/files/lines and **0** public APIs/dependencies. This packet adds evidence only. W-185 already owns lifecycle deduplication; count none of that saving again. Replacing the heuristic with identity services would add platform/native/filesystem policy and testing, with no demonstrated net saving and outside D-65's accepted scope.

Narrow follow-through for W-211: retain O 11 alias safeguards and carry the evidence gaps into any future proposed reduction. If the owner later requests stronger evidence, a small evidence-only follow-up can run the existing Linux workflow alias/publication tests and privileged Windows symlink cases; estimate 0.5–1 day assuming environments already work, based on this subday local probe/test run. Unknown reparse providers or network-filesystem qualification requires a separately bounded platform choice; no universal effort estimate is justified. No product implementation ticket, queue insertion or protected wording change is recommended now.
