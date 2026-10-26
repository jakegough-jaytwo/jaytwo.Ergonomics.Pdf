# W-152: publication race — expert review request

Historical review request. On 2026-10-26 the owner approved the managed-only scope and exact concurrency wording recorded in D-65. The questions and pre-approval verification below preserve the evidence that led to that decision; they are no longer pending.

---

We maintain managed .NET PDF workflow libraries targeting net10.0, net8.0, net6.0, and netstandard2.0 (also tested on Windows net48). Please recommend a precise, implementable file-publication contract and design for the race below. Do not assume access to our repository.

## Approved contract and constraints

- Save a separate output through a unique temporary sibling created with `FileMode.CreateNew`. Finish processing, flush (disk flush defaults on), close the temporary file, then publish. Preserve complete-file atomic publication. No directory fsync or power-loss durability guarantee.
- Replace an existing destination only with explicit `Overwrite = true`. On processing failure, preserve the input and prior destination and remove the temporary file where possible.
- Reject normalized input/output aliases. For an existing destination, conservatively reject matching length and last-write time against every file-backed input. False positives are accepted. Inputs opened from paths remain open with `FileShare.Read` until publication completes.
- These alias checks are explicitly best effort. The approved design accepts the final check-to-publication race, concurrent filesystem mutation limitations, and the inability to identify arbitrary caller-supplied sources. It does not promise universal file identity.
- The approved publication primitives are `File.Move(temporary, target)` for an absent destination and `File.Replace(temporary, target, null)` for overwrite. Native runtime interop and additional runtime dependencies require owner approval. Tests may use native calls to construct links.
- The current work item asks us to strengthen path isolation while preserving atomic publication, test intermediate/final symlinks, hard links, concurrent publishers and failed publication on Windows and Unix where supported, and document remaining races precisely. A design change needs an owner decision.

## Implementation under review

```csharp
// Run after processing, while file-backed inputs are still open.
RecheckNormalizedPathsAgainstEveryInput(target);
var existing = new FileInfo(target);
if (!existing.Exists)
{
    File.Move(temporary, target); // no overwrite argument
    return;
}
if (!overwrite) throw new IOException("Destination exists");
CompareInputAndDestinationLengthAndModificationTime();
File.Replace(temporary, target, null);
```

Independent fixes currently in the working tree resolve intermediate as well as final symbolic links on modern runtimes, refuse unresolvable reparse points on older targets, recheck normalized paths at publication, and refuse overwrite when input-path metadata is unavailable. They do not close the final race.

## Observed failure and standard-library evidence

On Linux, .NET runtime 10.0.13, SDK 10.0.402, two independent saves to one initially absent destination, both with `Overwrite = false`, can both return success. A barrier synchronizes their first input reads, after destination preflight; each writes its own complete temporary sibling. A test expecting exactly one successful publisher failed because both succeeded. This establishes an observed no-overwrite race, not a deterministic reproduction on every run. The final output was a complete PDF; the other successful publisher's output was replaced.

The [.NET 10 Unix FileSystem implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/IO/FileSystem.Unix.cs#L172-L214) explains the race: the no-overwrite move uses `LStat` and then `Rename` when the destination appears absent; another publisher can create the destination between those calls. It uses link/unlink only on other branches. Thus adding another `File.Exists` check cannot close this race.

Focused tests passed on Windows net10.0 and net48. Linux tests excluding the two concurrency cases passed, including symlinks, hard-link substitution during processing, missing input metadata, and publication-failure cleanup. No cross-process exclusion or hostile-directory guarantee has been established. Windows symbolic-link creation was unavailable (OS error 1314); directory junctions exercised intermediate reparse points there.

## Questions for your recommendation

1. How should we distinguish the accepted best-effort *alias* race from the explicit no-overwrite publication rule? Does accepting the former justify the observed replacement of another successful publisher's output? Please propose exact public wording, without claiming stronger safety than the mechanism provides.
2. Is there a portable managed-only approach on these targets that both atomically publishes a complete file and atomically refuses an existing destination across independent processes? Evaluate cooperative lock files or named mutexes separately from arbitrary concurrent filesystem changes. Explain crash/stale-lock recovery, namespace aliasing, ownership, and whether the solution changes the approved design. An in-process lock alone does not cover separate processes.
3. If a strict no-overwrite guarantee requires a platform-specific atomic primitive, recommend the smallest owner-approved exception, supported-platform/filesystem limits, failure behavior, and regression strategy. Compare that with explicitly retaining a documented race. Do not propose copy-to-visible-output or an empty destination placeholder as preserving complete-file atomic publication.

Please recommend one approach and list the exact owner decisions required before implementation. We have not weakened the failing assertion or marked this work complete.

---

## Verification record (2026-10-26)

- `make agents-check`: passed before task work (183 items, 65 decisions).
- Focused filter: `FullyQualifiedName~WorkflowFileSaveTests|FullyQualifiedName~WorkflowCombineTests|FullyQualifiedName~WorkflowSigningTests`, project `./test/jaytwo.Ergonomics.Pdf.Tests`.
- Windows `make quiet-test` with that filter: net10.0 60 passed / 3 platform skips; net48 60 passed / 1 platform skip. Builds passed with warnings as errors.
- Initial Linux focused run on a current tracked-source snapshot over the existing W-182 builder: 62 passed / 1 failed / 1 platform skip. This run included one subsequently removed redundant final-link test; the failure was `concurrent_publications_leave_one_complete_output(overwrite: False)`, expecting one successful publisher but observing more than one.
- Linux current snapshot with the focused filter additionally excluding `FullyQualifiedName~concurrent_publications`: 60 passed / 1 platform skip. The inner `make quiet-test` returned success. The outer quiet wrapper reported failure with child exit 0 because it could not parse another quiet wrapper's abbreviated output; this is not a test failure or evidence that the excluded concurrency cases pass.
- Runtime implementation source was checked read-only; no downloaded source was added to the runtime or tests.
- No full framework matrix, integration checkpoint, or qualification claim. No commit while the required concurrency evidence remains unresolved.

## Owner disposition and final verification (2026-10-26)

D-65 records explicit owner approval of the narrower scope and exact F17/public wording. After recording approval, the concurrency test was revised to allow either one or both saves to succeed and to require a complete final output and cleanup. It does not assert that the race reproduces. The managed publication mechanism is retained.

The final full focused selection passed: Windows net10.0 60 passed / 3 platform skips; Windows net48 60 passed / 1 platform skip; Linux net10.0 62 passed / 1 platform skip. Workflow fast selections on Windows net8.0 and net6.0 each passed 155 tests with 3 platform skips. W-152 contains the commands, environment, and remaining limitations. These results supersede the pre-approval exclusion of concurrency cases; they do not erase the original observation or establish exclusive publication.
