# Drafting guide

This is authoring guidance, not a product contract. Use the [index](../README.md) to find subject drafts and the [inventory](inventory.md) to check coverage.

## Derive the target before comparing implementation

Start with the charter's purpose, principles, and architectural constraints, then the capability and boundary in supported.md. Describe the caller's intended result in product terms. Identify the inputs, preservation and authorization decisions needed to make that result precise, and leave genuinely unresolved choices explicit.

Build acceptance scenarios from that proposed contract. Then compare implementation, existing tests, behavior documentation, retired specifications and research to identify reusable evidence, implementation gaps, incompatibilities and decision costs. Existing functionality is not the default answer: an API shape, format subset, numeric limit, algorithm list, ordering rule or failure guarantee needs its own justification in the target contract. Existing public commitments still require deliberate compatibility review before changing behavior; that review does not automatically adopt them as the rebuilt target.

A draft should distinguish scope already committed to by higher-level documents, a proposed operation requirement awaiting adoption, an open decision, and observed implementation evidence. Do not use implementation constraints to silently narrow scope or existing functionality to expand it. This organization step adopts no operation contract and authorizes no runtime changes.

## Write readable contracts

Organize each subject around its operations and caller-visible results. Use prose, tables and examples where useful; use stable requirement IDs only when a precise requirement needs traceability. The outline below is a completeness aid, not a mandatory ticket form. Ticket status, assignees, estimates, dependencies and implementation tasks belong in work items. Draft/adoption status identifies document authority, not delivery progress.

## Document ownership and authority

| Document | Responsibility |
| --- | --- |
| [Charter](../../charter.md) | Purpose, principles, architecture, and governance. |
| [Supported capabilities](../../supported.md) | Capability commitments and durable product boundaries, independent of implementation status. |
| Adopted operation specifications | Precise normative contracts: required scope, inputs, outcomes, preservation, authorization, limits, refusal, and failures. |
| [Behavior reference](../../behavior.md) | Current implementation, including present guarantees, limitations, and separately labeled planned additions. |
| [Acceptance records](../../acceptance.md) | Trace requirements to verification scenarios and record outcomes enforced by tests. |
| [Qualification](../../qualification.md) | Release evidence against qualification gates; passing tests alone do not establish qualification. |

Normative precedence is charter > supported capabilities > adopted specifications. A draft is not binding. A difference between an adopted contract and the implementation is an implementation gap; conflicting governing requirements require an owner decision. Acceptance records verify requirements rather than independently defining product scope.

The owner moved the previous specifications to [the retired spec directory](../../_retired_/spec/). Retired specifications, behavior descriptions, tests, work items, and research are evidence or drafting input, not automatic authority for new requirements. Existing public compatibility commitments still need deliberate consideration; retirement does not itself authorize weakening shipped guarantees.

## Conservative transfer process

1. Identify the capability or boundary in supported.md and the applicable charter principle. Retain both in their owning documents.
2. Draft the operation mechanics with explicit source labels and open decisions. Compare current behavior and tests to the draft without assuming either defines the target.
3. Obtain owner adoption of the exact contract before implementation. Any scope expansion, architectural exception, weakened acceptance requirement, or qualification change needs explicit owner approval.
4. Only then shorten genuinely duplicated mechanics in supported.md, replacing them with a link to the adopted section. Preserve the full capability commitment and durable boundary, including the distinctions in the [transfer inventory](inventory.md). No scope wording is removed by this organization step.
5. Record the destination section, requirement IDs, approval reference, and transfer status. Update behavior and acceptance records from actual implementation and executed verification; record gaps explicitly.

## Operation contract template

Use the relevant headings as a completeness check, adapting them into readable prose for the operation. This outline is not a ticket form. Replace placeholders with justified proposed requirements or explicit questions, not copied implementation rules.

```markdown
# <Operation name>

Status: Draft | Adopted
Contract ID: <stable identifier when assigned>
Owner approval: <reviewed revision and owner decision; absent while draft>
Scope authority: <charter and supported capability/boundary sections>
Related/shared contracts: <applicable sections>

## Purpose and scope
<Caller result, supported subset and exclusions; engine/workflow responsibilities.
Distinguish higher-level commitments from proposed refinements.>

## Inputs and lifetime
<Source/destination types, defaults, identity/order/duplicates, units/indexing,
source stability, ownership/disposal, concurrency, async and cancellation.>

## Required outcomes
<Precise observable results, return values, side effects, no-op and bulk semantics;
assign stable requirement IDs where useful for traceability.>

## Preservation and output strategy
<What survives as bytes or semantics, changes, omissions, disclosure and consent;
incremental prefix, append/new-file/rewrite strategy and required approvals.>

## Authentication and authorization
<Credentials/roles, permissions, DocMDP/FieldMDP/Reader rights, signature
restrictions, unresolved data, inspection versus validation/trust.>

## Limits and cancellation
<Applicable operational limits, units and enforcement; decide per operation
whether numeric values, caller controls and defaults are needed, preserving explicit
scope commitments. Buffering/work/storage bounds, cancellation and disposal;
label proposed refinements and unresolved choices.>

## Refusal and failures
<Unsupported, malformed, unauthorized, argument, processing, provider, output
and cancellation outcomes and timing. For each phase/destination, state source,
pending-edit and destination bytes/length/position, cleanup and publication,
rollback feasibility, provider side effects and retry/concurrency/durability limits.>

## Acceptance and traceability
<Requirement -> scope authority and provenance -> scenario/fixture/check ->
observed result and revision -> behavior section or implementation gap.
Distinguish internal testing, independent-tool evidence and qualification.>

## Open decisions
<Question, alternatives, scope/compatibility consequences, evidence needed,
and owner decision where applicable. No unresolved requirement is adopted.>
```

## Traceability and adoption guidance

Use stable contract and requirement IDs once assigned; avoid reusing retired IDs in a way that implies continued adoption. For each proposed requirement, record provenance as one of: existing charter/supported commitment; current behavior evidence; retired/historical wording; or a new proposal awaiting owner decision. A behavior/test citation does not substitute for scope authority or contract adoption.

Maintain a compact mapping in each contract or its linked acceptance section:

| Requirement ID | Scope authority | Provenance and owner adoption | Behavior or gap | Acceptance scenario/check | Observed evidence |
| --- | --- | --- | --- | --- | --- |
| <OP-01> | <supported section + charter if relevant> | <source kind, exact section, approval revision/decision> | <behavior section, mismatch, or not implemented> | <acceptance ID, fixture and executable test/command; missing if absent> | <revision, command/result and date; not run if unexecuted> |

Before adoption, check each supported capability and durable boundary against the inventory and contract destinations. Identify any compatibility loss or target/current mismatch explicitly. Requirements with implementation gaps stay requirements once adopted; do not edit scope to make the gap disappear. A conflicting governing requirement needs an owner decision, while an implementation mismatch needs a tracked implementation disposition.

After adoption, record the exact destination section and transfer status in this inventory, link acceptance scenarios, and update current behavior only when implementation evidence supports it. Keep qualification criteria unchanged. Unexecuted acceptance scenarios are not passing evidence, and a passing test does not promote a draft to an adopted specification.

## Charter review

No current charter passage needs removal solely to make room for specifications. Retain permissive licensing, deliberate scope, competitive reliability/performance, architectural principles, production readiness, compatibility policy, and governance. Contracts make those principles concrete; benchmark methods, comparison tools, workloads and numeric performance budgets belong in verification plans and evidence under the charter's quality standard.

## Outstanding drafting decisions

- Confirm shared-rule boundaries, assign stable requirement IDs as contracts become precise, and choose an owner adoption record format. The subject grouping is established in the index.
- Set supported input/structure subsets and preservation/refusal rules, especially assembly, rich signature/revision inspection, XFA and Reader rights. Existing research remains input, not an adopted answer.
- Define caller-font characters/scripts/shaping and image variant/transparency/profile/coordinate contracts; distinguish planned support from current behavior.
- Define permission-change schemes, authorization and output strategy. Resolve password changes/removal explicitly if proposed; do not infer scope from permission changes or historical implementation.
- Specify per-operation resource budgets, cancellation, destination failure/rollback/retry and publication guarantees without weakening current compatibility commitments or qualification criteria.
- Rebuild acceptance mappings from approved requirements and actual tests; decide how current guarantees will be reconciled with each proposed contract before adoption.
