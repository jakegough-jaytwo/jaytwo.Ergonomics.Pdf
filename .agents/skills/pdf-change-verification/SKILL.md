---
name: pdf-change-verification
description: Verify repository changes and qualification evidence. Use after runtime, test, build, or behavior-documentation changes, or when assessing an acceptance or qualification claim.
---

# PDF change verification

Verify the change itself; do not act as a separate QA persona or manufacture an approval handoff.

## Evidence

Use these terms precisely:

- **Implemented:** code exists.
- **Internally tested:** repository tests verify it.
- **Independently validated:** an implementation written elsewhere accepted or verified the named property; record the tool, its version, and the property.
- **Production-qualified:** every applicable gate in `QUALIFICATION.md` passed.

The same agent running qpdf, pyHanko, pypdf, Poppler, SoftHSM, or OpenSSL can gather independent-tool evidence. It is not an independent human or fresh-context review.

Claim a test passed only when you ran it and have the output. Counting test declarations is an inventory, not a result.

## Principles

- Our parser agreeing with our writer proves little. They can share one misunderstanding. Interoperability evidence comes from implementations written elsewhere. One context now writes both the code and its tests, so derive tests from the criterion, the standard, and independent tools, not from the implementation.
- Test both directions where the change allows: an independent tool produced the input, the library read it correctly, the operation ran, an independent tool accepted the output, the intended result was independently confirmed, and unrelated content survived. For incremental output, the original bytes are a prefix.
- Name the property. A structural check does not show that a field renders. A trusted signature under a test root does not show public identity.
- `AGENTS.md` names the required tools. A missing required tool fails its tests. Any other missing tool makes its check "not run," never a pass. A missing optional tool blocks release only when a `QUALIFICATION.md` gate depends on its evidence.
- Test what the public API can do. A test that passes only if the library gains an excluded feature, such as trust validation, is a scope request.
- Rejection is a result. For malformed or unsupported input, a controlled, documented exception is success. Look for uncontrolled allocation, infinite loops, stack overflow, partial output, silent corruption, and false success.
- Failure guarantees are specific. Preserving the input, leaving the destination unchanged, and publishing a complete file are different guarantees. Test the one the API documents.
- Keep the fast loop fast. Keep unit, interoperability, rendering, signing, performance, and fuzzing tests separable. Fuzzing uses fixed seeds and saves minimized inputs. Nondeterministic fuzzing stays out of ordinary CI.
- Classify a test by what it touches, never by how long it took (D-52). A test carries a `Category` trait (`Lane.Interop`, `Lane.External`, `Lane.Corpus`, `Lane.LargeData`, or `Lane.Sweep`) for each of these it does: an implementation written elsewhere judges the result; it starts an external tool or loads a native tool library only to make input; it opens G2 corpus files; it builds data sized like a production workload; it repeats an operation across every offset, failure point, or fixture. A test that asserts on elapsed wall-clock time carries `Lane.Clock`, so `make unit-test` runs it after the parallel hosts.
- Reproduce a load-dependent failure by forcing its condition, for example `xUnit.MaxParallelThreads=1` with a filter that adds a slow synchronous class, not by repeating loaded full runs. A loaded full run may confirm the fix once.
- Grow the fixture corpus by risk: structures that broke before, structures a named real producer emits, and edges the criteria name. Do not build a full combinatorial matrix.

## Workflow

Go up these tiers and stop at the one the change needs. Run builds and tests through the quiet targets: `make quiet-build`, `make quiet-test`, and `make quiet-docker-test`. They write the full output to `out/logs/<name>.log` and print the exit status, the counts for each framework, the failing tests, the build errors, and the fuzz result. They print the log tail only when nothing else explains a failure. Read a log only to diagnose a failure that the summary does not explain.

1. Inspect the changed contract, the diff, and only the applicable acceptance rows and failure guarantee.
2. Focused (the default): run the smallest tests that can falsify the change, including at least one regression test for each confirmed defect, for example `make quiet-test TEST_FRAMEWORK=net10.0 TEST_FILTER=FullyQualifiedName~SignatureTests`. Do not expand a focused test into a combinatorial matrix unless each case covers a distinct risk.
3. Fast lane (opt-in): run `make quiet-test LANE=fast TEST_FRAMEWORK=net10.0` only when focused coverage is insufficient, the change is cross-cutting, or the work item requires it. It leaves out the five categories, and in it the helpers that start tools, load iText, or open corpus files refuse, so an unclassified test fails with the trait to add. When the change touches a category's area and needs that evidence, run only the affected categorized tests.
4. Targets: when the change touches framework-specific code or the `netstandard2.0` build, run the fast lane on the other targets too: `make quiet-test LANE=fast` runs every framework and the `netstandard2.0` lane. net48 runs only on Windows, so run it locally.
5. Checkpoint: run `make quiet-docker-test` only for a named integration or release checkpoint. It runs the Jenkins-equivalent `make docker-builder docker-test` with quiet output, every category included. Run it detached, from a separate git worktree at the commit, so the next item can proceed. A checkpoint failure in work already committed is its own item. Any new external tool is in the `Dockerfile` and `tools/test-tools/install.ps1`.
6. A passing build is the evidence for StyleCop and XML documentation. A diff with no `PackageReference` change under `src/` is the evidence for the runtime dependency policy. Do not re-read files to check them. Check by hand, where the diff touches them, what no build or test covers: public API compatibility, resource bounds, stream disposal, prefix preservation, and error-path stream behavior.
7. Confirm from `git diff --stat` that `README.md` and `ACCEPTANCE.md` changed when public behavior changed. Confirm from their changed hunks that no criterion was weakened.
8. Report the commands and outcomes needed to understand the change. Include framework, tool-version, count, and unrun-check detail only when relevant to the claim. Do not paste passing output. When the run is green, do not re-read the diff or the changed files to confirm what the run already showed. Update the evidence inventory only for release qualification or when the work item explicitly produces reusable qualification evidence.

Do not infer rendering from a structural check, trust from certificate embedding, bounded memory from one successful run, or production qualification from green unit tests. For resource claims, record the environment, workload, and metrics, and compare before and after. Do not suppress or reclassify a failure to unblock a task or a release.

## Qualification records

These live under `agents/evidence/` and change only from observed results during release qualification or an explicit qualification work item. Routine feature and defect work does not maintain them opportunistically:

- The evidence inventory and gate status, with the freshness check of `QUALIFICATION.md` section 3. The release-gate summary is generated from the inventory, not kept as a second board.
- The G2 corpus manifest. Run its validator whenever a corpus fixture is added or changed.
- The G7 workloads, their approved budgets, and the timing baseline, measured with Release builds, coverage off, and process isolation where counters are process-wide.

## Defects

Record each defect with:

1. **Summary** of the incorrect behavior.
2. **Impact:** which supported operation or guarantee breaks.
3. **Reproduction:** minimal input and exact steps.
4. **Expected**, citing the criterion, the standard, or the independent tool.
5. **Actual**, with tool output.
6. **Severity.** Critical means silent corruption, a false signature guarantee, a security exposure, or unbounded resource use. High means a supported operation produces invalid output or fails independent verification. Medium means a predictable edge-case failure, a rendering discrepancy, or a measured regression. Low means a cosmetic or documentation issue.

Describe the defect, not the fix, unless the fix makes the defect clearer.
