# Feedback on `role-assignments-review.md`

Date: 2026-10-18

## Overall

This is a strong, evidence-driven review. The line-level citations, separation of response matching from TSA trust, stale-baseline table, and concrete edit checklist make it immediately useful. Its central conclusions are sound: the dependency language conflicts with the charter, Metadata and Attachments lack a primary owner, the prompts duplicate unstable state, and the initial assignments need an explicit execution order.

I would accept the review after updating a few conclusions for the replacement `AGENTS.md` and tightening several recommendations.

## Strongest parts

- The conflict table is the most usable section. It maps each issue to both the governing rule and the role text that could cause incorrect work.
- The distinction between validating an RFC 3161 response against the request and performing certificate trust validation is important and well stated.
- The review correctly notices that the role files turn historical caveats into de facto backlog items even when `ACCEPTANCE.md` already fixes the intended behavior.
- The artifact-location and naming observations are practical operational feedback, not merely editorial preferences.
- The per-role notes preserve valuable material instead of recommending a wholesale rewrite.

## Corrections required after the new `AGENTS.md`

### SoftHSM and Poppler are now required test infrastructure

The replacement `AGENTS.md:9` explicitly requires:

- `SigningInteropTests` with qpdf, Python, pyHanko, pypdf, and SoftHSM 2;
- `AppearanceRenderTests` with Poppler;
- failure, not skipping, when a required tool is unavailable.

The repository now contains both `SigningInteropTests.cs` and `AppearanceRenderTests.cs`. `SigningInteropTests` uses `Net.Pkcs11Interop` in the test project and talks to SoftHSM; it does not add PKCS#11 P/Invoke behavior to the shipping library.

Accordingly, revise the claims that a real PKCS#11 provider or adapter is outside this repository and that SoftHSM should be removed. The correct boundary is:

- runtime library: no PKCS#11 dependency or P/Invoke;
- test project: a test-only host implementation of `IPdfExternalSigner` using SoftHSM is required interoperability evidence.

Likewise, Poppler is no longer merely an optional tool. It is a required dependency for the named rendering test category.

### The open-gap summary is stale

The final open-work list still describes rendered appearance validation, embedded-chain validation, CMS timestamp validation, and SoftHSM validation as missing or prospective. The current test suite contains explicit coverage for those areas. The review should either re-audit the new tests or label its gap list as superseded by the later charter change.

### Do not equate static test-case counting with a passing test run

The statement that the "139 tests figure still holds" is supported by counting `[Fact]` and `[InlineData]` declarations, not by executing the suite. That establishes an approximate discovered-case count, not that those cases pass on .NET 6 and .NET 8. It conflicts with the evidence discipline the review otherwise advocates.

Prefer: "The source currently declares 139 apparent cases" and separately report actual `dotnet test` results only if executed.

## Recommendations to refine

### Avoid recommending `git init`

It is useful to report that VCS metadata is absent in this workspace and therefore worktrees are unavailable. `git init` is not the right default remedy: it creates a new repository without recovering history, remotes, branches, or the owner's intended workflow.

Recommend task-scoped exclusive file ownership or isolated directory copies for the current environment, and ask the owner to restore the real repository metadata if branch/worktree coordination is expected.

### Define precedence more precisely

The proposed shared sentence gives `AGENTS.md`, `README.md`, and `ACCEPTANCE.md` equal-sounding authority. They serve different purposes. `AGENTS.md` is the binding agent instruction set; `ACCEPTANCE.md` is the behavioral contract; `README.md` documents the public product. If those documents disagree, an agent should not choose among them silently.

A sharper rule would be:

> Follow `AGENTS.md`. Treat `ACCEPTANCE.md` as the required behavior and `README.md` as the public contract. If they disagree, stop the affected work and report the inconsistency.

### Separate QA verification from a library validation feature

Some criticism of the QA role treats verification scenarios as if they require adding signature validation to the product. QA can use pyHanko or another independent verifier to demonstrate that an untrusted TSA is rejected without adding any public validation API. The problem exists only where a role requires the library itself to establish trust.

Mark each scenario as one of:

- library input validation during signature creation;
- independent external verification by QA;
- public PDF signature validation, which remains out of scope.

This classification will make the PAdES/TSA discussion more precise.

### Keep cross-cutting source ownership task-based

The review is right that `PdfEngine.cs` is a collision point. Avoid turning it into a permanent Technical-Lead-only file. Specialist changes will sometimes correctly belong there. Require one active task owner for a shared file, an integration sequence, and Technical Lead review.

### Explain the Metadata/Attachments placement tradeoff

Assigning both to Core is a workable minimal-team choice, but the review should acknowledge that Core already owns parsing, filters, encryption, writing, and resource behavior. A combined Document Features owner is also defensible if those backlogs are active. Recommend one option based on expected workload instead of presenting Core as automatically correct.

## Suggested disposition

Keep the review's structure and most of its findings. Update the tool/test conclusions, downgrade static test counts to inventory evidence, replace `git init` with a non-destructive coordination recommendation, and sharpen the distinction among product behavior, creation-time validation, and independent QA verification.

