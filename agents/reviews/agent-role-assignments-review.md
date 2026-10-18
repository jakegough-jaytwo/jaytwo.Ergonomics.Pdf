# Review of agent role assignments

Date: 2026-10-18

Reviewed:

- `agents/roles/project-manager.md`
- `agents/roles/tech-lead.md`
- `agents/roles/core-engineer.md`
- `agents/roles/cryptography-engineer.md`
- `agents/roles/acroform-engineer.md`
- `agents/roles/qa-engineer.md`
- Governing context in `AGENTS.md`, `README.md`, and `ACCEPTANCE.md`

## Executive assessment

The role set has a strong foundation. It reflects the actual technical risk of the product, gives interoperability and failure behavior appropriate weight, and repeatedly protects the project from becoming a general PDF framework. The separation among PDF core, signing, AcroForm, and independent QA is sensible.

I would not run the roles concurrently without revising them first. Three issues are significant enough to cause conflicting work or incorrect scope decisions:

1. The role prompts contradict the repository's runtime-dependency constraint.
2. Metadata and attachments are required product capabilities but have no primary implementation owner.
3. Assignment, approval, and handoff authority is distributed ambiguously among the PM, Technical Lead, and specialists.

The prompts are also much longer than they need to be: 3,280 lines across six files. Much of that is duplicated product context, policy, historical state, and testing philosophy. The duplication increases context cost and creates several places where the role briefs have already drifted from `AGENTS.md` and `ACCEPTANCE.md`.

Overall recommendation: keep the six-role model, add or explicitly assign a metadata-and-attachments owner, and rewrite each role as a short operational contract that references the repository charter instead of restating it.

## What is working well

### The specialization follows the real fault lines in the code

The Core, Signing, and AcroForm roles divide work along meaningful semantic boundaries rather than arbitrary file boundaries. Each recognizes the cross-cutting nature of `PdfEngine.cs` and the incremental writer. That is a sound model for this repository.

### The roles understand the product's narrow charter

All six briefs consistently reject rendering, stamping, general content editing, broad typography, and speculative framework building. This is one of the strongest parts of the set because those are the most likely forms of scope creep in a PDF library.

### Independent evidence is treated as a first-class deliverable

The QA role's four evidence levels—implemented, internally tested, independently validated, and production-qualified—are useful and should become the common vocabulary for every role. The engineering roles also correctly say that internal round trips do not prove interoperability.

### Large-file and failure behavior are not afterthoughts

The prompts repeatedly call out bounded memory, sparse access, decompression limits, failure atomicity, stream ownership, and preservation of prior revisions. Those expectations fit the product charter well.

### The PM and Technical Lead are differentiated conceptually

The intended division is good: the PM owns scope, priority, acceptance criteria, and release coordination; the Technical Lead owns architecture, decomposition, implementation quality, and integration. The problem is not the intended model, but that the execution protocol does not consistently enforce it.

## Findings requiring correction

### 1. The dependency policy contradicts the governing repository instructions

This is the clearest policy defect.

`AGENTS.md:9` says zero runtime dependencies is a constraint, and `AGENTS.md:21` permits exactly one exception: `System.Memory` on `netstandard2.0`. The PM, Technical Lead, Core, Signing, and QA briefs instead say that lightweight or narrowly scoped runtime packages may be acceptable, usually with only "substantial" dependencies requiring approval. For example:

- `agents/roles/project-manager.md:88`
- `agents/roles/tech-lead.md:74-104`
- `agents/roles/core-engineer.md:154-172`
- `agents/roles/cryptography-engineer.md:97-107`
- `agents/roles/qa-engineer.md:120-126`

That language authorizes a class of runtime dependency that the repository charter prohibits. It also creates ambiguity about whether the PM, Technical Lead, or product owner can approve an exception locally.

Recommendation: every role should state the exact rule from `AGENTS.md`: no runtime NuGet dependencies except `System.Memory` for `netstandard2.0`; test-only dependencies and external validation tools are allowed. A proposal to change that rule is a charter change for the human owner, not an ordinary dependency review.

### 2. Metadata and attachments have no primary implementation owner

Metadata/XMP and embedded files are named as required capabilities in every role's product summary, but none of the specialist ownership sections assigns their feature semantics and public behavior to anyone.

The Core role owns parser, decoding, encryption, incremental writing, and resource management, but not metadata or attachment semantics. The AcroForm and Signing roles correctly exclude them. The Technical Lead therefore becomes the accidental implementer by default, despite being responsible for integration across all workstreams.

This is especially risky because these features are not trivial wrappers:

- Metadata includes Info/XMP synchronization, XML preservation, date handling, encoding, and certification restrictions.
- Attachments include name trees, Associated Files, streaming, replacement/removal semantics, encryption exemptions, and large-input ownership.

Recommendation: add a **Document Features Engineer** responsible for Metadata and Attachments, with Core owning the underlying object, stream, encryption, and writer primitives. If the team should remain at six roles, explicitly assign Metadata and Attachments to the Core Engineer and acknowledge the added load. The first option is cleaner; the second is workable for a small backlog.

### 3. The assignment and approval chain can deadlock or duplicate work

The PM's first assignment recommends specialist ownership. The Technical Lead's first assignment also recommends ownership and says it will delegate. Specialists are told to coordinate with the Technical Lead, while AcroForm and Signing additionally wait for the PM to confirm priority. QA coordinates with both and can withhold sign-off.

This leaves practical questions unanswered:

- Who creates the authoritative task?
- Who chooses the implementer?
- Can the Technical Lead start approved backlog work without returning to the PM?
- Who decides whether acceptance criteria have been met?
- Who closes a task after QA finds a non-blocking limitation?
- What happens when the PM and Technical Lead independently assign overlapping work?

Recommendation: define one linear operating contract:

1. PM creates and prioritizes the task, with scope, exclusions, and measurable acceptance criteria.
2. Technical Lead approves the technical boundary, identifies shared-file risk, and assigns one implementation owner.
3. The specialist implements within that boundary and reports evidence.
4. QA independently verifies the applicable acceptance criteria and records failures or sign-off.
5. Technical Lead approves code and integration quality.
6. PM accepts the product outcome or returns the task to the backlog.
7. Human owner decides only charter changes, major architecture changes, and releases made despite withheld QA sign-off.

Routine work should not require separate PM permission after the PM has prioritized and defined the task.

### 4. Shared source-of-truth material is duplicated and already drifting

The prompts repeat the product charter, dependency philosophy, tool list, historical test count, and reported implementation state. The exact "139 tests" baseline appears in the PM, Technical Lead, and QA roles, while the other roles include similar historical claims. Each prompt then warns the agent not to trust those claims.

This costs context without providing stable instructions. It also encourages every role to spend its first assignment independently re-auditing the whole repository. Five parallel baseline reports would be expensive, repetitive, and likely inconsistent.

Recommendation:

- Treat `AGENTS.md`, `README.md`, and `ACCEPTANCE.md` as the normative sources.
- Remove historical implementation/test counts from role prompts.
- Put changing project state in one status artifact if a persistent status document is useful.
- Have the Technical Lead own the technical baseline and QA own the evidence inventory. Other specialists should inspect only their assigned subsystem and report deltas or contradictions.

### 5. Some specialist prompts invite work beyond the accepted charter

The Signing role spends a full section on PAdES profiles and mentions RSA-PSS as a possible future algorithm. The governing charter explicitly excludes PAdES-LTV and ECDSA and precisely fixes the current external-signing contract to raw SHA-256 with RSA PKCS#1 v1.5 (`AGENTS.md:16` and `AGENTS.md:20`). The role should preserve that contract, not reopen algorithm or profile design unless the human owner changes scope.

The AcroForm role makes normalized top-left UI coordinates and `/UserUnit` normalization part of its first assessment. The current public contract instead exposes the field rectangle plus page number, rotation, and page boxes (`README.md:11`). Normalized UI coordinates may be useful, but they are a new API/product decision rather than an existing defect.

The AcroForm prompt also states broadly that populated fields should not rely on `/NeedAppearances`, while the accepted behavior deliberately uses `/NeedAppearances` for comb, rich text, non-WinAnsi, password, and signature values and prevents signing until appearances have been generated (`AGENTS.md:18`, `ACCEPTANCE.md:43`, and `ACCEPTANCE.md:51`). The role needs the same explicit supported/unsupported boundary.

Recommendation: replace aspirational capability lists with the exact current contract. Put future algorithms, coordinate normalization, richer font behavior, and broader PAdES profiles in the product backlog only after explicit prioritization.

### 6. Cross-cutting ownership is described but not decided

Several important behaviors span roles:

- Encryption of new strings and streams during form, metadata, attachment, and signing revisions.
- DocMDP and FieldMDP parsing, policy interpretation, and enforcement.
- Signature-field structure and AcroForm hierarchy.
- Incremental-revision construction and signing placeholder finalization.
- Page-tree traversal and widget geometry.

The prompts repeatedly say "coordinate," but do not identify the accountable owner for the final behavior. Coordination language alone is not enough when most of the implementation passes through shared engine files.

Recommendation: include a small ownership matrix in the common team instructions. A proposed matrix appears below.

### 7. The concurrency guidance needs an enforceable work protocol

The roles correctly warn against simultaneous edits to shared files and suggest isolated branches or worktrees. They do not require a named file owner, an integration order, or a pre-edit check. With `PdfEngine.cs` as a cross-cutting integration point, two technically valid tasks can still collide.

Recommendation: before implementation, the Technical Lead should record for each active task:

- task ID and implementation owner;
- expected files or subsystems touched;
- branch/worktree, if used;
- dependencies on other active tasks;
- integration order;
- reviewer and independent verifier.

Only one task should own a shared file in a working tree at a time. A specialist who discovers an unplanned shared-file change should stop at the boundary and ask the Technical Lead to resequence or repartition the work.

### 8. Deliverables are described by topic, not by artifact or completion signal

Every role has a substantial first assignment, but none says where the assessment goes, how another agent discovers it, what minimum evidence it must include, or who acknowledges completion. As written, agents can produce excellent isolated essays without creating an executable team plan.

Recommendation: standardize a short report format:

- task and owner;
- verified facts with file/test references;
- assumptions;
- confirmed defects;
- missing evidence;
- proposed changes and affected files;
- verification commands/tools;
- decisions required;
- handoff recipient.

The initial PM report should be the only broad product-status assessment. The Technical Lead should turn it into an ownership/integration plan. Specialist and QA reports should attach to specific tasks.

## Recommended ownership matrix

| Area | Accountable | Implementation owner | Independent verification | Product acceptance |
|---|---|---|---|---|
| Product scope and priorities | Product Manager | N/A | Technical Lead and QA consulted | Human owner for charter changes |
| Parser, xrefs, object streams, filters | Technical Lead | PDF Core Engineer | QA Engineer | Product Manager against acceptance criteria |
| Incremental writer and stream/resource behavior | Technical Lead | PDF Core Engineer | QA Engineer | Product Manager |
| Standard-security read/write behavior | Technical Lead | PDF Core Engineer | QA Engineer, with Signing consulted for signature exemptions | Product Manager |
| AcroForm discovery, values, appearances, geometry | Technical Lead | AcroForm Engineer | QA Engineer | Product Manager |
| CMS, signature fields, RFC 3161, signing revisions | Technical Lead | Signing & Cryptography Engineer | QA Engineer | Product Manager |
| DocMDP/FieldMDP policy | Technical Lead | Signing Engineer owns policy; Core supplies parsing; AcroForm enforces fill decisions | QA Engineer | Product Manager |
| Metadata and XMP | Technical Lead | Document Features Engineer, or explicitly Core if no new role is added | QA Engineer | Product Manager |
| Attachments and Associated Files | Technical Lead | Document Features Engineer, or explicitly Core if no new role is added | QA Engineer | Product Manager |
| Public API consistency and integration | Technical Lead | Relevant specialist | QA Engineer | Product Manager; human owner for breaking changes |
| Release qualification | Product Manager | Technical Lead assembles candidate | QA may withhold sign-off | Human owner makes final release decision |

## Role-by-role notes

### Product Manager

The role is directionally strong and appropriately discourages implementation by default. Tighten it around backlog authority, acceptance decisions, and evidence status. Remove technical design detail that belongs in the charter or Technical Lead role. Resolve the dependency contradiction and specify the task-brief format the PM must hand to the Technical Lead.

### Technical Lead

This is the most important coordination role and is broadly well written. It is currently overloaded: it claims parser/object-model correctness while the Core Engineer is the primary implementation owner, and it becomes the implicit owner of unassigned product features. Clarify that the Technical Lead is accountable for technical outcomes and integration, while specialists are responsible for implementation. Add the concrete concurrency and handoff protocol.

### PDF Core Engineer

The technical boundary is coherent. Add explicit interfaces with Metadata/Attachments and state who owns shared permission enforcement. Replace the permissive dependency language with the repository rule. Keep performance work measurement-driven. Avoid turning historical implementation notes into standing instructions.

### Signing & Cryptography Engineer

The role demonstrates the right cryptographic distinctions and failure mindset. Make the current contract prescriptive: RSA/SHA-256/PKCS#1 v1.5, host-owned PKCS#11 integration, no ECDSA, no general signature validation, and no PAdES-LTV. Retain independent external validation, but remove future-profile design from the default assignment. State clearly that timestamp-response checks performed while creating a signature are allowed, while a public signature-validation subsystem is not.

### AcroForm Engineer

The field/widget and appearance ownership is strong. Align its definition of successful appearance generation with the intentionally limited behavior in `ACCEPTANCE.md`. Treat normalized UI coordinates and broader font/Unicode behavior as proposed product changes, not assumed obligations. Keep raw PDF geometry semantics explicit.

### QA & Interoperability Engineer

This is the strongest individual brief, but also the longest. Preserve its independence, evidence taxonomy, and authority to withhold sign-off. Reduce the encyclopedic test lists to risk-based selection rules plus task-specific matrices. Distinguish required baseline tooling—currently qpdf and Python with pyHanko for `ForeignPdfTests`—from optional tools such as Acrobat, PDFBox, MuPDF, and pdfsig. QA should own evidence, not the implementation fix, and should not independently certify a substantive library change it authored.

## Suggested prompt shape

Each role can likely be reduced to roughly 120-200 lines using the same structure:

1. Mission.
2. Owns.
3. Does not own.
4. Binding constraints: reference `AGENTS.md`, `README.md`, and `ACCEPTANCE.md`; repeat only role-critical rules.
5. Authority and escalation.
6. Inputs required before work starts.
7. Expected outputs and evidence.
8. Handoffs.
9. Definition of done.
10. Current first assignment.

Avoid embedding current test counts, tool versions, historical defects, or speculative future capabilities in the role definition. Those belong in task briefs or a single current-status document.

## Recommended revision order

1. Fix the runtime-dependency language in all affected roles.
2. Assign Metadata and Attachments to a named implementation owner.
3. Add the common assignment, approval, QA, and integration workflow.
4. Add the cross-cutting ownership matrix.
5. Align Signing and AcroForm scope with the exact charter and acceptance criteria.
6. Remove repeated historical baseline material.
7. Shorten each role to its durable operational responsibilities.

## Final verdict

The roles show good technical judgment and are worth preserving, but they currently function more like six overlapping project charters than six executable job descriptions. After the policy contradictions, missing ownership, and handoff ambiguity are corrected, the specialization should work well. The highest-value change is not adding more detail; it is making authority, ownership, allowed scope, and evidence flow unambiguous.
