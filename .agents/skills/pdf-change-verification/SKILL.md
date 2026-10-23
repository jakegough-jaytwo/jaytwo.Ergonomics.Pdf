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
- Grow the fixture corpus by risk: structures that broke before, structures a named real producer emits, and edges the criteria name. Do not build a full combinatorial matrix.

## Workflow

Go up these tiers and stop at the one the change needs. Run builds and tests through the quiet targets: `make quiet-build`, `make quiet-test`, and `make quiet-docker-test`. They write the full output to `out/logs/<name>.log` and print the exit status, the counts for each framework, the failing tests, the build errors, and the fuzz result. They print the log tail only when nothing else explains a failure. Read a log only to diagnose a failure that the summary does not explain.

1. Inspect the diff, task criteria, applicable acceptance rows, and documented failure guarantee.
2. Focused: run the smallest tests that can falsify the change, including a regression test for each confirmed defect, for example `make quiet-test TEST_FRAMEWORK=net10.0 TEST_FILTER=FullyQualifiedName~SignatureTests`.
3. Targets: when the change touches framework-specific code, the `netstandard2.0` build, or external interoperability or rendering checks, run the affected tests on the other targets. net48 runs only on Windows, so run it locally. The checkpoint gate runs net10.0, net8.0, and net6.0, and the `netstandard2.0` build on net8.0, so do not also run the full local matrix when a checkpoint follows.
4. Checkpoint: for an integration checkpoint, run `make quiet-docker-test`. It runs the Jenkins-equivalent `make docker-builder docker-test` with quiet output. Run it detached when the tool allows. Any new external tool is in the `Dockerfile` and `tools/test-tools/install.ps1`.
5. A passing build is the evidence for StyleCop and XML documentation. A diff with no `PackageReference` change under `src/` is the evidence for the runtime dependency policy. Do not re-read files to check them. Check by hand, where the diff touches them, what no build or test covers: public API compatibility, resource bounds, stream disposal, prefix preservation, and error-path stream behavior.
6. Confirm from `git diff --stat` that `README.md` and `ACCEPTANCE.md` changed when public behavior changed. Confirm from their changed hunks that no criterion was weakened.
7. Report exact commands, frameworks, tool versions, counts, unrun checks, and remaining limitations. Do not paste passing output. When the run is green, do not re-read the diff or the changed files to confirm what the run already showed. Update the evidence inventory only from observed results.

Do not infer rendering from a structural check, trust from certificate embedding, bounded memory from one successful run, or production qualification from green unit tests. For resource claims, record the environment, workload, and metrics, and compare before and after. Do not suppress or reclassify a failure to unblock a task or a release.

## Qualification records

These live under `agents/evidence/` and change only from observed results:

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
