# Agreed PDF skill plan

Date: 2026-10-19

Status: agreed by the authors of the two skill reviews. Approved by the owner on 2026-10-19 (D-27, D-28). Steps 1–3 are done.

## Sources

This plan consolidates:

- `agents/skill-reviews/2026-10-19-pdf-correctness-skill-review.md` (Agent C);
- `agents/skill-reviews/2026-10-19-production-readiness-skill-review.md` (Agent P);
- the agreement recorded in `agents/skill-reviews/agent-comms.md`.

The source reviews were removed under D-34 and W-54; their pre-removal state is [squashed]. This document is the single recommended path forward.

## Outcome

Keep the six existing skills and the single-agent workflow. Strengthen the existing domain skills with focused PDF-specific references, make `pdf-change-verification` route to specialized qualification workflows, and add five new skills:

1. `pdf-fuzz-qualification`;
2. `pdf-interop-qualification`;
3. `pdf-performance-qualification`;
4. `pdf-spec-conformance-review`;
5. `pdf-release-qualification`.

The skills should produce or validate reproducible evidence rather than accumulate more prose. Qualification evidence must identify the exact commit, fixtures, environment, tools, property established, and result. The release workflow must detect stale evidence.

This plan changes no product scope. Completeness means complete support and evidence for the advertised subset, not implementing every valid PDF feature.

## Boundaries that remain fixed

- Keep one active agent for ordered repository work.
- Keep `pdf-core-change`, `pdf-acroform-change`, and `pdf-signing-change` as the implementation-domain skills.
- Keep `pdf-security-review` read-only and independent from remediation.
- Keep implementation evidence, repository-test evidence, independent-tool evidence, and genuinely independent review distinct.
- Do not create separate metadata or attachment skills yet.
- Do not add signature validation, rendering, general repair, or any other excluded capability through a skill workflow.
- Do not put large fixtures or generated evidence in skill folders. Skills define workflows; ordinary repository locations hold fixtures, manifests, baselines, and qualification results.
- Scripts run by Jenkins or `make docker-test` live in ordinary repository test/tooling space. Agent-only helpers may live under a skill's `scripts/`.
- Missing or unexecuted checks are never passes.

## Suspected code findings to triage

Code inspection supports the mechanisms below, but none is a confirmed defect yet. Each requires a focused fixture or probe, an externally grounded expected result, and a regression test before it is recorded as confirmed.

| ID | Suspected behavior | Initial disposition |
|---|---|---|
| F-1 | Undefined or free indirect references throw rather than resolve as null; generation matching may also be incomplete. | Reproduce and cite the applicable ISO rule. Then ask the owner whether this is required in-charter conformance or a documented limitation. |
| F-2 | Check-box widgets sharing one field name but having different on-state names all receive the first widget's state. | Reproduce first. If confirmed, treat the silent wrong appearance as High and fix before the B-18 campaign. |
| F-3 | Button `/Opt` export values are not mapped; button export values come from appearance-state names. | Reproduce with an independently produced form and assign severity from observed public behavior. |
| F-4 | Choice filling does not distinguish combo boxes from list boxes and does not update or remove `/I`. | Reproduce logical and rendered behavior; separate the appearance issue from the stale-index issue. |
| F-5 | Auto font size uses height but not width, and generated appearances do not consult `/MK`. | Reproduce clipping and appearance loss with an independent renderer. |
| F-6 | The corpus lacks a linearized file and files authored by mainstream document tools. | Fold into B-13 and `pdf-interop-qualification`; this is missing evidence, not a product defect. |

Any confirmed Critical or High defect is fixed before the B-18 qualification campaign. Confirmed Medium and Low findings receive a normal disposition but do not automatically delay fuzzing.

## Enhancements to existing skills

### `pdf-task-workflow`

Add a task-to-evidence contract. Each task identifies:

- affected public capability;
- exact `ACCEPTANCE.md` row, or that it is evidence-only;
- applicable `QUALIFICATION.md` gate;
- documented failure and destination-stream guarantee;
- risk class;
- required evidence level and independent oracle;
- artifacts to update.

Add a compact routing matrix from risk class to the applicable domain and qualification skills. Evidence used for release assessment must be tied to a commit and checked for freshness.

### `pdf-core-change`

Add `references/structural-invariants.md`, initially as one focused reference. Split it later only if progressive disclosure warrants it.

Cover:

- classic, stream, hybrid, linearized, and incremental xref behavior;
- `/Prev`, `/XRefStm`, trailer inheritance, object generations, free entries, and generation-65535 heads;
- offsets, preambles, long arithmetic, and classic-xref output limits;
- object-stream indexing, lifetime, and allocation behavior;
- direct, indirect, wrong, and object-stream `/Length` values;
- filter order, predictors, decoded-size limits, and decompression amplification;
- `stream` and `endstream` line-ending variants;
- trailing bytes and multiple `%%EOF` markers;
- security revisions, crypt filters, plaintext exceptions, unsigned `/P`, and `/ID` variants;
- catalog `/Version` versus the header;
- cycles, recursion, attacker-controlled counts, allocation timing, and algorithmic complexity;
- prefix preservation and failure behavior.

Add one rule: tolerance explicitly required by a cited PDF rule is conformance, not a repair heuristic. This does not authorize general recovery from malformed files.

Add a writer rule: every key the library writes must be valid for the file's declared PDF version (header or catalog `/Version`). The Arlington PDF Model can check this mechanically; confirm any finding against the applicable declared-version rule.

For parser, decoder, encryption, and writer changes, require foreign-producer or differential evidence when a suitable independent implementation exists. Review arithmetic and complexity whenever input controls counts, offsets, lengths, nesting, or buffer sizes.

### `pdf-acroform-change`

Add `references/field-matrix.md` covering topology, inheritance, appearances, and real-world variants:

- terminal and nonterminal fields;
- merged, separate, and repeated widgets;
- button `/Opt`;
- check boxes sharing a name with different on-states;
- radio groups, RadiosInUnison, and NoToggleToOff;
- Combo versus list box, `/I`, and `/TI`;
- inherited `/FT`, `/V`, `/Ff`, `/DA`, and `/Q`;
- `/DA` size zero and fonts outside the supported subset;
- `/MK`, boxes, rotation, nonzero origins, transforms, and clipping;
- widgets found through `/P` or page annotations;
- kids without `/T`, dotted or Unicode names, and pre-existing `/NeedAppearances true`;
- choice export values versus display values.

Include a per-setter invariant table for `/V`, `/AS`, `/AP`, `/I`, and `/NeedAppearances`. For every appearance change, name the rendered property and the independent renderer that establishes it.

### `pdf-signing-change`

Add `references/signing-matrix.md` that keeps these properties separate:

- byte ranges and placeholder exclusion;
- CMS signed attributes and DER encoding;
- signature value and signer certificate/key match;
- chain contents, cryptographic integrity, revision coverage, and trust;
- DocMDP and FieldMDP policy;
- `/SV` and `/Lock` when reusing a signature field;
- CMS signature timestamps versus document timestamps;
- timestamp imprint, nonce, algorithm, certificate inclusion, and trust;
- encrypted-PDF behavior;
- provider failure, size failure, timeout, and destination rollback.

Where ISO behavior is ambiguous, the evidence record names the reference interpretation, such as pyHanko or Acrobat. Qualification work triggers a second PDF-aware verifier; a CMS-only verifier does not satisfy that role.

### `pdf-change-verification`

Keep this skill as the common evidence vocabulary and focused-change workflow. Add routing to the specialized qualification skills.

Add these rules:

- For a confirmed defect, the regression test must fail against the defective implementation when a safe, practical negative control exists.
- Differential reads use pypdf, `qpdf --json`, or another independent reader only for properties that tool exposes reliably. Record the property and the oracle's limitations.
- Change-set checks compare normalized semantics against an operation-specific allowlist. They do not assume only one indirect object changes or that qpdf JSON is the normative model.
- MuPDF `mutool draw` is the preferred second renderer for representative G6 evidence. `QUALIFICATION.md` calls a second renderer desirable, so absence alone is not a mandatory-gate failure unless the owner changes the requirement.
- Qualification performance evidence uses Release builds with coverage disabled and process isolation where counters are process-wide.

Stage evidence recording:

1. Add commit, date, and dirty-tree state to the existing evidence inventory and perform a freshness check.
2. Introduce machine-readable corpus and fuzz records with their qualification skills.
3. Generalize the evidence schema with `pdf-release-qualification`.

The eventual shared evidence schema records task, capability, acceptance row, gate, fixture hashes and provenance, command, framework, build configuration, environment, tool versions, property established, result, artifacts, and limitations. Results distinguish pass, fail, not run, not applicable, and expired.

### `pdf-security-review`

Add a trust-boundary threat model for hostile PDF bytes and graphs, compressed data, encryption dictionaries and passwords, signer callbacks and certificate enumerations, TSA responses, and transport behavior.

Add review prompts for integer arithmetic, cycles, recursion, parser progress, algorithmic complexity, allocation-before-validation, decompression amplification, exception sanitization, secret lifetime, output integrity, denial of service, and the revision from which certification policy is read.

Include incremental-saving, signature-wrapping, and shadow-attack classes only to evaluate this library's signature construction, revision selection, certification enforcement, and failure guarantees. Do not turn the review into an excluded signature-validation feature.

## New skills

### `pdf-fuzz-qualification`

Purpose: create, run, minimize, and record bounded fuzz campaigns for G8. Do not use it for an ordinary regression test or the independent read-only security review.

It owns:

- entry-point selection for object parsing, xrefs, object streams, filters, predictors, encryption, field walks, XMP, DER/CMS, and RFC 3161;
- structure-aware mutations plus numeric and boundary mutations;
- fixed-seed CI smoke runs versus bounded qualification campaigns;
- per-input time, memory, nesting, decoded-size, and process limits;
- corpus seeding from minimized historical defects and valid foreign files;
- crash and hang deduplication and minimization;
- classification of expected document exceptions versus uncontrolled failures;
- promotion of confirmed defects to deterministic regressions;
- campaign records containing commit, dirty-tree state, harness and engine versions, seeds, corpus hashes, duration, CPU-hours, coverage where available, failures, and dispositions.

Recommended references: `references/targets.md` and `references/campaign-record.md`. Repository-visible runners used by CI belong under `tools/` (D-28) or the existing test infrastructure.

### `pdf-interop-qualification`

Purpose: build and validate the foreign-PDF corpus and independent semantic evidence for G2 through G6. Do not use it for one focused regression test.

Its central artifact is a corpus manifest containing:

- fixture ID and SHA-256;
- provenance, acquisition date, redistributability, and license;
- producer and version;
- whether each tool authored, transformed, signed, rendered, or inspected the file;
- PDF version and structural features;
- encryption, forms, signatures, metadata, and attachments;
- supported, unsupported, or malformed classification with justification;
- operations and expected semantic results;
- validators and the exact property each establishes;
- output-prefix and unrelated-content preservation checks.

The workflow should add meaningful files from at least the producer classes required by G2, including mainstream authoring tools and a linearized file. It should validate the manifest, detect duplicate or trivial variants, summarize producer/feature coverage, and identify acceptance rows without independent evidence. A qpdf rewrite or pyHanko signature is recorded as a transformation, not automatically counted as another originating producer.

### `pdf-performance-qualification`

Purpose: measure file-backed memory, I/O, retention, and timing against approved G7 workloads and budgets. Do not use it for ordinary unit-test timing.

Require:

- Release builds, coverage disabled, file-backed inputs, isolated processes, controlled warm-up, and repeated samples;
- managed allocation, retained heap, process working set, bytes read and written, seeks where measurable, elapsed time, CPU time, output size, and correctness result;
- inspect, edit, save, sign, attachment streaming, repeated object-stream access, repeated open/dispose, encrypted-xref discovery, and limit-failure workloads;
- baselines keyed by commit, fixture hash, runtime, OS, architecture, storage class, build configuration, and tool versions;
- absolute product ceilings kept separate from regression tolerances;
- before/after measurements and explanations for material variance;
- real dense files where sparse files would not establish the property.

Use B-42 as an early measured case.

### `pdf-spec-conformance-review`

Purpose: perform a read-only review of an advertised capability against applicable PDF rules, clarifications, historical producer behavior, and the repository contract. Do not use it for ordinary implementation work.

Add `references/sources.md`, classifying every source as normative, clarifying, interoperability evidence, or product-decision input. Include:

- ISO 32000-1 by clause;
- applicable ISO 32000-2 errata or clarification, without silently expanding the PDF 1.0–1.7 charter;
- PDF Reference 1.7 Appendix H, Compatibility and Implementation Notes;
- the Arlington PDF Model;
- RFC 5652, RFC 3161, and RFC 5816.

The output distinguishes normative requirements, clarifications, historical conventions, product choices, supported behavior, valid but unsupported features, malformed inputs, suspected or confirmed defects, and ambiguities requiring an owner decision. Build a clause-to-contract-to-test map for the supported subset. Arlington findings must be confirmed against the applicable declared-version rule.

### `pdf-release-qualification`

Purpose: assess one exact release candidate against G1 through G10 and produce the capability-specific qualification decision. Use only for explicit release-readiness or production-qualification work.

It must:

- consume recorded evidence rather than turn confidence into evidence;
- detect evidence that predates relevant changes or refers to another commit or fixture;
- reconcile the support matrix, acceptance rows, gate inventory, known defects, and limitations;
- verify package contents, runtime dependencies, XML documentation, examples, target frameworks, API compatibility evidence, and reproducible build evidence;
- keep pass, fail, not run, not applicable, and expired distinct;
- refuse whole-library qualification when a production-supported capability lacks an applicable gate;
- produce the release-decision contents required by `QUALIFICATION.md`;
- preserve the human owner's final release authority.

Automation may validate records, flag staleness, summarize coverage, and draft a report. It must not decide that semantic evidence establishes a property the underlying tool did not check.

## Durable artifacts

The combined workflow should produce:

1. a foreign-corpus manifest;
2. a qualification evidence inventory tied to commits and fixture hashes;
3. a performance workload and baseline manifest;
4. fuzz campaign records and a minimized regression corpus;
5. a supported-subset clause-to-contract-to-test map;
6. a generated release gate summary whose source evidence remains inspectable.

## Agreed execution order

This is the agreed order. The plan itself executes none of these tasks. Steps 1–3 needed no skill change and are done. The owner approved step 4 onward under D-27.

1. Complete B-41, as ordered by the board state preserved through D-36 and W-56; that board's pre-removal state is [squashed].
2. Reproduce and triage F-1 through F-5. Fix confirmed Critical or High findings before the B-18 campaign. Give every other confirmed finding a normal disposition without automatically delaying fuzzing.
3. Correct the stale G10 evidence row and add commit, date, and dirty-tree state to evidence entries.
4. With owner approval, add `pdf-fuzz-qualification`, add the three domain references, and execute the B-18 harness, smoke run, and bounded campaign. Seed it with confirmed historical triggers and the domain variants.
5. Add `pdf-interop-qualification` and its corpus manifest and validators; execute B-13/F-6 and B-14.
6. Enhance `pdf-change-verification` with routing, negative controls, scoped oracles, and staged evidence records.
7. Add `pdf-performance-qualification`, complete the missing G7 workloads, and measure B-42.
8. Add `pdf-spec-conformance-review` and grow the clause map from high-risk areas.
9. Add `pdf-release-qualification` after the evidence-producing workflows have realistic records to consume.

Steps 1 through 3 are ordinary backlog and state work under `pdf-task-workflow`; they do not by themselves authorize an edit to an owner-controlled file. Step 4 onward requires owner approval for the `.agents/skills/` changes. All implementation still follows the board one task at a time.

## Owner decisions

All three are decided:

1. The skill enhancements and the five new skills are approved (D-27).
2. F-1 follows the spec: unresolvable references are null (D-26, closed by B-44).
3. A top-level `tools/` directory is approved for CI-visible validators and runners (D-28).

## Acceptance criteria for the skill work

The skill enhancement effort is complete when:

- each new skill has a discriminating description and does not attract ordinary changes unnecessarily;
- detailed conditional procedures live in references rather than bloating `SKILL.md`;
- repeated deterministic work is implemented as tested scripts;
- CI-visible scripts live in repository test/tooling space;
- the skills route cleanly without duplicating domain ownership;
- every qualification record identifies the exact commit, dirty-tree state, fixtures, tools, environment, property, and result;
- stale evidence is detectable;
- unavailable or unexecuted checks cannot become passes;
- each independent oracle is credited only for properties it actually establishes;
- fuzzing, interoperability, performance, security review, and release assessment remain separable;
- confirmed defects have regression tests that fail against the defective implementation where a safe negative control is practical;
- the skills pass `quick_validate.py`;
- realistic forward tests show that another agent can use each skill to produce the intended artifacts without expanding the charter.

## Final recommendation

Adopt this plan as the single path forward. Its central change is to make PDF qualification reproducible and domain-aware: cited rules, real producer variants, fixture provenance, scoped independent oracles, isolated performance measurements, bounded fuzzing, fresh evidence, and a release decision tied to one exact candidate.
