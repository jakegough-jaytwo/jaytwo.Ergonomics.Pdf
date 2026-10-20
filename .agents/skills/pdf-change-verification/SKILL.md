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

1. Inspect the diff, task criteria, applicable acceptance rows, and documented failure guarantee.
2. Run the smallest focused tests that can falsify the change, including a regression test for each confirmed defect.
3. Run relevant target frameworks and external interoperability or rendering checks.
4. For a checkpoint, run `make docker-builder docker-test`. It is the Jenkins-equivalent gate. Any new external tool is in the `Dockerfile`.
5. Check StyleCop, XML documentation, public API compatibility, runtime dependency policy, resource bounds, stream disposal, prefix preservation, and error-path stream behavior as applicable.
6. Confirm `README.md` and `ACCEPTANCE.md` changed when public behavior changed and that no criterion was weakened.
7. Report exact commands, frameworks, tool versions, results, unrun checks, and remaining limitations. Update the evidence inventory only from observed results.

Do not infer rendering from a structural check, trust from certificate embedding, bounded memory from one successful run, or production qualification from green unit tests. For resource claims, record the environment, workload, and metrics, and compare before and after. Do not suppress or reclassify a failure to unblock a task or a release.

## Defects

Record each defect with:

1. **Summary** of the incorrect behavior.
2. **Impact:** which supported operation or guarantee breaks.
3. **Reproduction:** minimal input and exact steps.
4. **Expected**, citing the criterion, the standard, or the independent tool.
5. **Actual**, with tool output.
6. **Severity.** Critical means silent corruption, a false signature guarantee, a security exposure, or unbounded resource use. High means a supported operation produces invalid output or fails independent verification. Medium means a predictable edge-case failure, a rendering discrepancy, or a measured regression. Low means a cosmetic or documentation issue.

Describe the defect, not the fix, unless the fix makes the defect clearer.
