# jaytwo.Pdf simplification effort

## Agent working charter and planning document

26 October 2026  |  Version 1.0  |  Owner Jake  |  Agent A and Agent B

Prepare an evidence-backed simplification plan for jaytwo.Pdf and jaytwo.Ergonomics.Pdf. Preserve useful business PDF behavior, remove responsibilities that do not earn their cost, and make meaningful losses deliberate. Deliver one reconciled contract proposal and a prioritized set of small tickets for Jake to approve.

**Authorization boundary.** Research, independent review, reconciliation, focused disposable experiments, interviews, and draft planning records are authorized. Product implementation is not authorized. Do not change runtime code, shipped behavior, protected contracts, public APIs, or the active queue until Jake approves both the exact contract wording and the corresponding tickets. Approval of this charter alone does not approve its recommendations.

## Start here

1. Each agent reads the four review attachments, current repository instructions, and relevant contracts. Record the checked-out revision, working tree changes, tool versions, and corpus inventory. Preserve unrelated work.
2. Work independently before comparing conclusions. Agent A leads engine and assembly research. Agent B leads public API, workflow, compatibility, and external-library comparisons. Both assess the disputed choices independently, then check each other's principal evidence.
3. Inventory current behavior, target behavior, closed decisions, and existing tickets. Reproduce consequential claims with small fixtures or read-only probes. Draft protected wording outside the protected files.
4. Reconcile into one evidence ledger, contract proposal, refusal-impact matrix, decision log, and backlog. Keep disagreements visible until evidence or Jake resolves them.
5. Interview Jake only about unresolved product tradeoffs. Submit concrete wording, measured impacts, ticket scopes, dependencies, and validation for approval. Stop before implementation.

## Planning deliverables

Provide a repository map with precise code and contract references; reconciliation of both review rounds; a capability-by-operation contract; versioned external-library comparisons; a measured corpus matrix; small proposed tickets; and an owner approval packet. Use existing repository record formats rather than introducing a parallel tracking system.

**Completion condition.** Jake can approve, reject, or defer each contract choice and its tickets without reconstructing the review history. Each proposed deletion names the behavior lost, responsibility actually removed, retained safeguards, and migration burden. No unresolved recommendation is represented as an approved decision.

## Settled direction and review disagreements

**Useful behavior earns complexity.** Preserve ordinary editable forms, values, appearances, page geometry, and useful business functionality. The reviews withdrew static-only assembly and blanket read-only treatment of signed or encrypted files. Do not reopen those cuts as easy savings. [R1A, R1B]

**Preserve meaningful behavior or refuse.** Replace successful loss of widgets, supported appearances, annotations, actions, and relationships with a precise supported subset and failure contract. Intentional page selection can exclude a navigation target; malformed references and unsupported semantics are different cases. Explicit derivatives must name valuable, deliberate losses.

**Field independence is fixed.** Namespace every input occurrence, including repeated occurrences of the same document, whether names collide or not. Do not join fields by matching names. Keep reference remapping and correct ownership.

**Accessibility and forms share the principle.** Tags carry functionality even when pages render identically. Apply the same usefulness, correctness, boundedness, and explicit-loss criteria to tags and forms. Their graph structures differ; equal implementation cost and blanket tagged-input refusal do not follow. Research bounded preservation before asking Jake to approve a refusal or derivative boundary. [Owner]

**Page labels need not be distinct.** Duplicate labels are acceptable. Remove uniqueness and collision reconciliation from the problem. Whether to preserve explicit labels or deliberately use new-document numbering is still a contract choice. Agent 2's exclusion proposal cannot be rejected merely because labels repeat. [Owner]

**Unsigned signature fields survive.** Follow Jake's endorsement of Agent 2: preserve empty signing locations and widgets with occurrence-local names and correctly remapped field references. Research /Lock and seed constraints; do not silently discard or weaken them. Existing signed fields are a separate derivative decision. [Owner, R2B]

**Precedent informs recommendations.** Jake requested recommendations based on other-library behavior for metadata and disputed choices. Compare PDFBox, pypdf, qpdf, and Aspose.PDF, accounting for scope and version. Neither a majority vote nor the broadest library sets this product's contract.

**Separate proposals from settled direction.** R2A recommends tag refusal, label preservation, absent-XMP creation, and current workflow ordering. R2B recommends tag/label exclusions, no absent-XMP creation, and rejection of duplicate or contradictory fills. These are unresolved alternatives. The owner's references to numbered points do not establish missing wording; map each to a named decision rather than guessing.

**Count savings honestly.** Distinguish deletion of responsibility, policy reduction, API reduction, contract correction, and new preservation work. A preparation call retains transformation code. Do not double-count Reader-rights and hybrid-XFA savings, count removed tests as product simplification, or equate physical lines with value.

## Assembly pages forms and accessibility

Agent A owns the structural analysis; Agent B checks the caller contract and compatibility. Produce one disposition per capability for concatenation and extraction, with whole-input admission distinct from retained-page inspection.

**Pages and AcroForms.** Keep encoded content copying, inherited geometry/resources, values and editability, default-appearance font remapping, widget ownership, field pruning, and independent input occurrences. Specify shared resources, multiple widgets, repeated inputs/pages, cycles, and partial selection of multi-page fields. No content decoding, automatic flattening, or collision-dependent namespacing.

Validate appearances and later editing as well as dictionary shape. Identity maps must not join repeated inputs or bind a repeated page's widget to the wrong output instance. State the exact namespace spelling and ordering.

**Accessibility research.** Map StructTreeRoot, ParentTree, StructParents, marked-content identifiers, RoleMap, ClassMap, language, alternate text, reading order, annotation object references, and page references. Determine what must be remapped for concatenation, reordering, extraction, and repeated pages. Identify unsupported structures with fixtures. Copying a tree or clearing MarkInfo does not prove semantic preservation.

Compare bounded ordinary tag preservation with explicit refusal cases; temporary tagged-input refusal pending support; and an explicitly authorized untagged derivative if a real use case warrants it. Silent exclusion remains a disputed proposal. Estimate work and measure acceptance impact for each candidate. Do not add a runtime accessibility validator or promise PDF/UA conformance.

**Tag evidence.** Include a minimal tagged document, tagged form, role/class mappings, annotation reference, multi-page structure tree, subset/reordered pages, repeated pages, and malformed references. Inspect with an independent reader and a structure-aware accessibility tool or documented manual procedure. Rendering validates visual fidelity, not reading order. Record unavailable semantic checks; do not claim complete preservation without them.

**Preflight and failures.** Retain structural preflight before output. Enumerate its scope and budget; do not claim arbitrary behavior detection. Source mutation, late read errors, and destination failures retain their separately documented consequences. A preflight guarantee does not mean every possible later failure is prevented. Verify both engine destinations and workflow publication paths.

**Contract output.** For each assembly feature, state Preserve, Refuse, Exclude by selection, Exclude by an explicitly approved new-document rule, or Explicit derivative/preparation. State how unsupported variants fail and how callers distinguish them. Ordinary functionality cannot become a fallback after preservation fails.

## Assembly navigation annotations and signatures

**Bookmarks.** Recommend bounded ordinary outline import in input order, retaining duplicate titles and remapping supported destinations. Resolve the two review rules for excluded targets: retain necessary ancestor headings, or remove an item and promote surviving children. Show concrete trees and propose one fixed rule. Unsupported actions must not become misleading title-only entries. Validate parent/sibling links, counts, open state, named destinations, cycles, and repeated inputs.

**Page labels.** Compare preserving each selected page's displayed label with fixed new-document numbering. No cross-input uniqueness rule. If preserving, cover Roman/decimal styles, prefixes, restarts, repeated pages, unlabeled inputs, and extraction across ranges. Rebuild ranges and inspect displayed labels independently. Keep this decision separate from bookmarks and tags.

**Links.** Preserve supported GoTo destinations and URI meaning. Keep the existing self-link rule: self-links target their own output copy; other links to repeated pages target the first selected copy within that input occurrence, unless approved wording supersedes it. Separate deliberately unselected targets from malformed references. Resolve relative URIs against an explicit usable source base; never infer a filesystem base.

Recommend removing scheme/userinfo sanitization as an assembly responsibility while retaining structural validation. Research IsMap, Named and GoToR actions, /Next chains, /AA, and document-open behavior separately. Copying an action does not authorize executing it. Specify the supported action subset; refuse unsupported meaningful behavior. Do not replace typed rules with unrestricted reachable-object cloning.

**Annotations.** Inventory current subtypes and key policies. Evaluate static Stamp, FreeText, Caret, Popup, replies, Parent, IRT, RT, appearances, and resource closure. Use output-page-instance identity where needed. Test shared appearances, cycles, cross-page relationships, selected/excluded pages, malformed references, and resource limits. A subtype name does not prove passive semantics. Preserving an existing stamp does not introduce a stamping API.

**Signature fields.** Distinguish empty fields, approval-signed fields, certification, timestamps, Reader rights, and already-locked fields. Preserve unsigned fields/widgets; namespace /Lock field references where supported. Research seed requirements and whether transferred constraints remain meaningful. Unsupported mandatory constraints require precise refusal, not silent weakening. Include signing into a preserved empty field in the approved subset.

**Validation.** Combine structural assertions, independent-reader inspection, rendering for affected appearances, and edit/sign smoke tests. Reordered and repeated inputs must not cross-link fields, comments, destinations, or locks. Bookmark, label, and annotation additions correct contracts and may add code; do not count them as deletion savings.

## Refusal derivatives XFA and Reader rights

Agent B proposes the public operations; Agent A proves structural and permission boundaries. Prefer a small set of named operations to a general policy engine. Names remain illustrative until approved.

**Default refusal.** Propose a fixed rule for active behavior, embedded/associated files, optional content, unpreservable appearances/relationships, signed input, and unsupported XFA. Tags follow their approved decision. Specify whole-input checks, selected-page checks, retained graphs, and budgets. Do not promise to detect JavaScript anywhere without an enumerated inspection scope.

**Unsigned derivatives.** Specify whether assembly may create an unsigned derivative by explicit authorization. Cover approval signatures, certification, document timestamps, signed fields/widgets, usage rights, lock dictionaries, and painted signature marks individually. R2A proposes a narrower boundary than R2B for certification and locks. Recommend a narrow initial contract and show excluded business workflows. Authorization cannot silently bypass authentication, assembly permissions, DocMDP, or FieldMDP. Source files remain intact; cryptographic validity never transfers. Content-painted marks cannot be erased without interpretation outside scope.

**Hybrid XFA.** Recommend named preparation that deliberately abandons XFA while retaining the supported AcroForm representation. Fill, flatten, and assembly should not perform this conversion implicitly. Dynamic/XFA-only editing remain excluded. Define ordering with rights removal, permissions, signed/certified refusal, rollback, repeated invocation, and errors. Test whether assembly sees pending edits before relying on preparation in one session. Compare integrated Combine preparation with a documented separate step; avoid speculative API expansion.

**Reader rights.** Investigate replacing per-write /UR3 grant interpretation with explicit removal on otherwise unsigned documents. Keep detection and signature/certification safeguards. Separate usage-rights signatures from approval signatures in errors and corpus counts. No silent rights removal on every mutation. Measure fill-only credential barriers and legacy Reader usability: an extra call may not overcome missing modification permission. Supersede closed decisions only with approved wording.

**Keep explicit consent.** Retain AllowUnencryptedOutput and per-input assembly permissions. Do not add BestEffort, StripUnsupported, a flag per subtype, or generic remove-scripts/remove-attachments/remove-tags operations. A demonstrated use case can justify a later named derivative with its own contract and ticket.

**Refusal evidence.** Cover each admission rule, overlapping features, malformed structures, exhausted budgets, and failure before output. Error messages name operation, input occurrence, structure/category, and supported next step without revealing credentials or sensitive host exceptions. Show output/state behavior for later I/O failures as a separate case.

## Metadata and signing public API

**Separate editing from assembly.** Assembly creates a new document identity; metadata editing changes an existing document. Propose exact wording for each rather than applying an assembly exclusion to ordinary setters.

For editing, recommend atomic synchronization of supported high-level properties when Info and XMP both exist, with a precise failure instead of successful divergence. Research whether absent XMP should be created or left absent: the reviews disagree. Compare versioned documentation and minimal experiments in the four named libraries. Recommend the smallest useful behavior consistent with the existing high-level promise. Separate raw SetXmp packet authoring from synchronized setters.

Define property mappings, date/time/list encoding, multilingual titles, conflicting values, unknown namespaces, custom fields, malformed/unsupported packets, absent packets, ModDate, version changes, permissions, and signed-document effects. Preserve unrelated XMP where promised; retain secure XML parsing and size limits. Failure must retain approved pending-state and publication guarantees. Cover Info-only, XMP-only, consistent/conflicting pairs, malformed XMP, custom namespaces, and refused writes.

For assembly, recommend fresh minimal identity metadata and viewer defaults instead of selecting one input's identity or inheriting conformance claims. Explicitly decide custom XMP, PDF/A identifiers, print preferences, page mode, and non-script open actions. Tags and page labels have their own decisions. No conformance claims arise from copying metadata.

**Signing extensibility.** Investigate removing public arbitrary-CMS generation through IPdfSignatureProvider and PdfSignatureContext while retaining library-owned CMS, host-held keys through IPdfExternalSigner, and timestamp authorities. Inventory engine/workflow callers, overloads, tests, and docs. Identify complete-CMS services, custom attributes, and externally assembled two-phase signing that lose support. Raw-digest HSM/cloud signing is a distinct integration path; do not call the migration free.

Retain profiles, key/digest algorithms, countersigning, document timestamps, timestamp checks, reservation bounds, private staging, and rollback diagnostics unless a separate business and implementation case supports removal. A smaller interface does not delete CMS or RFC 3161 implementation. Amend D-64 and R4 wording only after approval.

**Seed values.** Simplified refusal of mandatory constraints needs its own decision. Compare valid signing cases lost with measurable branch reduction. Optional constraints, format requirements, field locks, and provider removal interact. Demonstrate retained safety before recommending a cut; coordinate with W-151 rather than treating its completed policy as undecided.

## Workflow sources decoders and internal cleanup

**Workflow.** Compare current bulk fill, last-value-wins, fill-before-flatten, and declared ordering for other operations with early rejection of duplicate/contradictory fills. Current fixed staging is a deliberate contract, not automatically a heuristic. Use caller examples and analogous precedent. Recommend keeping current semantics absent evidence for a more useful replacement. Keep one editor, atomic bulk fill, ownership, pending-edit lifetime, and dedicated signing terminals explicit.

Preserve signer failure before publication, physical-output guarantees, rollback observability, staging, rebinding, and managed publication limits. Rollback unification may strengthen behavior but add code. Ticket pending edits surviving editor disposal only if a concrete contract problem remains.

**Async sources.** Trace Open/OpenAsync, IPdfSource, parser/loader modes, preload behavior, cancellation, sparse remote reads, synchronous inspection, adapters, and entry points. Measure reads, allocations, and blocking on large local and delayed range-backed sources. Compare retaining current opening, completing needed async behavior, and removal. Async syntax is not duplicated responsibility. A seekable network Stream may block or require staging; it is not automatically equivalent. Separate signer/timestamp/output async. Reconcile W-138 and W-109.

**Decoders.** Inventory ASCIIHex, ASCII85, LZW, RunLength, abbreviations, filter chains, predictors, and each required decoding site. Inspect resolved object/xref streams, metadata, and attachments. Raw byte search cannot establish use inside compressed objects. Separate copied encoded page/image streams from bytes the library must consume. Specify minimal required-stream fixtures per filter, malformed data, and limits. Removal needs measured compatibility cost, a defined unsupported-filter error, and an account of callers unable to recover within this library.

**Signature forensics.** Partition basic detection, rich CMS/BER reading, revision attribution, public coverage hints, signing DER, timestamp-response validation, and historical xref reading. Trace consumers. Compare basic listing, removal of forensic properties, and removal of the rich reader. Do not count mandatory signing/xref machinery as savings. Inspection is distinct from trust, revocation, and cryptographic validation. Reconcile W-158 and the settled signature-field walk.

**Aliases and copier modes.** Prove same-path, symlink, hard-link, reparse, sharing, race, cleanup, and rename behavior across supported platforms before removing alias heuristics. Coordinate with W-185. Search production callers of Remap/Reject policy variants and remove only unused machinery. Keep actual reference remapping, source-qualified identity, cycle registration, ownership, and budgets. Split these into small independent tickets.

## Evidence standards and library precedent

Every consequential claim needs an evidence ID, repository revision, file/symbol or contract clause, fixture hash, reproducible command, tool version, observed result, interpretation, limitation, and linked decision/ticket. Label verified current behavior, review-reported observation, proposal, inference, and unresolved question. Re-run stale claims at the planning baseline.

For each candidate, record business use, responsibility removed, code retained/added, behavior lost, migration cost, affected guarantees, and dependencies. Estimate net implementation changes as ranges with a stated basis. Tests establish behavior and regression protection, not demand. Prefer the smallest test that could falsify a claim.

| Library | Initial primary evidence | Required research |
| --- | --- | --- |
| PDFBox | Merger source distinguishes form merge modes and structure-tree-related behavior. [P1] | Pin release/commit and reproduce forms, tags, labels, outlines, and metadata in the applicable mode. |
| pypdf | Merging guide documents grouping fields with add_form_topname. [P2] | Test repeated occurrences, selected pages, relationships, and metadata; do not assume every input is automatically namespaced. |
| qpdf | Page selection documentation supplies document/page behavior context. [P3] | Compare a primary input with --empty; test navigation, labels, tags, and signatures using a pinned version. |
| Aspose.PDF | Metadata guide exposes DocumentInfo/XMP and version-dependent storage. [P4] | Identify product/version/API and verify PDF 1.x editing; do not generalize PDF 2.0 storage behavior. |

Use the same small fixtures across applicable libraries. Record defaults, opt-ins, preserved/lost/refused semantics, errors, absent/malformed XMP, and caller burden. Explain unsupported comparisons and licensing/tool limits. No commercial runtime dependency is implied. Documentation is input; reproduce ambiguous behavior. Save commands and outputs. Explain why a precedent fits this narrower product rather than copying its full feature set.

**Reconcile facts before preferences.** Classify differences as revision, operation, factual error, scope, or product tradeoff. Reproduce factual differences. For tradeoffs, present options with recommended wording and costs. Do not vote, average incompatible contracts, or turn agent agreement into owner approval.

Agent B checks Agent A's principal reproductions and vice versa. Use independent readers for structure, renderers for appearance, and signature tools for signing claims. Two agents repeating an assertion or a writer reading its own output is not sufficient corroboration. Missing tools remain explicit evidence gaps.

## Test corpus refusal impact matrix

Build a machine-readable row per fixture and operation plus a short summary. Pin corpus revision, count, hashes, provenance, fixture usage rights, operation parameters, credential role, and classifier limits. Separate fixture coverage from representative customer demand.

**Recount the reviews.** R2A reported 41 fixtures: 18 tagged, 16 with outlines, 6 Reader-extended, 3 XFA, and 4 signed. R2B described a raw scan of 36 files for rare filters. Denominators and methods differ. Recount at one revision. Overlapping feature counts are not measured refusal totals.

| Matrix field | Required observation |
| --- | --- |
| Scenario | Fixture ID/hash; extract/combine/fill/flatten/metadata/sign; pages and repeated occurrence; user/owner authentication. |
| Features | Tags; forms/scripts; XFA kind; rights; approval/certification/timestamp; locks; encryption; attachments/AF; layers; labels; outlines; actions; filters. |
| Current result | Accepted, refused, successful omission, or unrelated failure; exact reason and observed functionality. |
| Candidate result | Preserve, refuse, selected-page exclusion, or explicit derivative/preparation; all applicable reasons, not merely the first failure. |
| Recovery and cost | Available explicit path; required credentials; deliberately lost behavior; remaining refusal; workflow/API steps. |
| Evidence | Structural/render/semantic checks; versions; measured versus inferred findings; missing coverage. |

Report newly refused fixtures relative to baseline for each operation, per-category counts, category intersections, the union of all refusals, accepted-after-preparation totals, and remaining blocked workflows. Always include denominators. Do not sum overlapping categories or publish a pass rate that hides successful loss. Analyze counterfactual rules through read-only classification or isolated probes; do not implement the proposed runtime to manufacture results.

**Fill coverage gaps.** Specify minimal fixtures for tagged forms, scripted public forms, hybrid plus rights, signed versus certified/locked inputs, malformed XMP/destinations, repeated pages with widgets/replies, relative URIs, label restarts, each candidate filter in a required stream, and large sparse documents. Keep malformed/adversarial cases distinct from ordinary compatibility samples.

**Approval gate.** Before a new refusal or removal is approved, Jake receives its ordinary-workflow impact, explicit recovery path, limits, and missing coverage. No numerical acceptance threshold is preapproved. Unavailable measurements remain pending and block the claims that require them. Ticket gaps rather than treating an unmeasured category as absent.

## Sequence and approval gates

**Gate 0 establishes the baseline.** Record revision, existing changes, authority, related ticket states, tools, and corpus. The two-agent request authorizes independent planning; serialize repository writes and retain the one-active-item rule. Keep shared outputs under one designated planning editor.

**Stage 1 is independent discovery.** Agent A maps engine/assembly preservation and refusal. Agent B maps API/workflow impact and precedent. Both independently assess tags/forms, metadata, and migration costs before exchanging conclusions. Use disposable probes; no product implementation or protected-file edits. Propose a large spike for approval rather than starting it.

**Gate 1 is reconciliation.** Exchange evidence, rerun disputes, complete the matrix, and identify unresolved product choices. Produce one proposed contract per operation, with explicit open choices and linked contrary findings. A research gap becomes a research ticket.

**Stage 2 prepares decisions and tickets.** Draft superseding decisions and exact edits to affected AGENTS.md bullets, docs/spec clauses, acceptance wording, public examples, and dependent records. Keep target behavior distinct from shipped behavior. Translate every necessary correction and research gap into small proposed tickets; deduplicate existing work.

**Gate 2 requires Jake's explicit approval.** Submit exact contract wording, evidence, compatibility cost, final ticket scope, dependencies, validation, priority, and proposed queue insertion. Jake may approve slices. Unresolved slices remain proposed; silence or approval of a neighboring ticket does not authorize them.

**Stage 3 begins only after Gate 2.** Suggested dependencies: unused copier modes; admission/refusal and diagnostics; URI semantics; annotation relationships; unsigned fields; ordinary bookmarks and labels; approved tag behavior; explicit derivatives; engine/workflow API cleanup. Keep separately verifiable items and preserve usable behavior while intermediate work lands.

Run Reader-rights and XFA preparation as coordinated separate items. Metadata, signing API, seed policy, aliases, and rollback each need their own approved contract. Async, decoders, and forensics remain independent investigations, generally after cheaper bounded work. Unresolved large removals should not block unrelated approved tickets.

**Gate 3 verifies integration.** Use focused tests per ticket. A named cross-area checkpoint runs make docker-builder docker-test and applicable independent tools, with the worktree arrangement required by the approved item. Gate 4 reconciles implemented behavior, contract/docs, evidence, and remaining tickets. Neither gate authorizes a package release.

The inspected queue lists W-184, W-185, W-186 before the remaining security phase beginning W-155. Verify again before proposing insertion. Coordinate alias work with W-185. Existing W-156/W-158 need explicit dispositions, not silent cancellation. Jake approves reprioritization.

## Ticket template and readiness

Use agents/templates/work-item.md and stable W identifiers allocated from the repository. Temporary PLAN labels are not reserved W numbers. Do not reopen a frozen completed item silently. Preserve required front matter: id, title, status, kind, created, completed, source, legacy_ids, depends_on, blocked_by, decisions, allowed_changes.

**Title.** One observable outcome, for example Preserve empty signature fields during assembly.

**Problem.** Concrete trigger and current result, with revision, symbol/clause, fixture, evidence ID, and user consequence.

**Scope and exclusions.** One contract slice, engine/workflow surfaces, docs, dependencies, migration, and non-goals. Exact files or directory prefixes ending in / for allowed_changes; no globs. Classify research, behavior-preserving cleanup, contract correction, or capability removal.

**Proposed behavior.** Default, supported subset, refusals, deliberate losses, explicit authorizations, and retained guarantees. Link the decision and exact approved text. Until approval, keep proposed. A research ticket ends in a recommendation and follow-up tickets, not runtime changes.

**Acceptance criteria.** Observable outcomes, including negative and failure cases where relevant. Example: Given two occurrences containing an empty signature field and supported lock references, assembly preserves two independent fields and widgets, namespaces references, and introduces no cross-occurrence lock. Unsupported mandatory constraints refuse before output and retain the specified state. Add repetition/selection cases for distinct risks.

**Evidence required.** Smallest focused tests that can falsify behavior; fixture IDs; independent inspection where needed; changed failure guarantee; expected observation. Name broader framework, Docker, rendering, or performance runs only for a distinct risk or checkpoint. A confirmed defect gets a permanent regression test.

**Complexity and compatibility.** Branches/APIs/tests removed; code retained/added; overlapping savings; measured refusal impact; caller migration; residual risk. Give effort ranges with a basis. Split work when one failure would leave unrelated outcomes inseparable.

**Closeout.** Observed result, revision, exact verification commands/outcomes, limitations, changed shipped docs, date, and follow-ups. Never weaken a criterion to close an item.

**Ready means approved and executable.** Contract/scope approved; prerequisites available; allowed_changes complete; validation executable; queue permits start. After authorization, use proposed, ready, active, waiting, done, cancelled under repository rules. Planning does not make implementation tickets active. Preserve one work item per task commit, required checks, and no push without authorization.

## Proposed backlog and priorities

P0 blocks a truthful contract or protects a guarantee. P1 delivers bounded useful simplification. P2 needs compatibility-sensitive investigation. These labels do not imply a security vulnerability or override the queue. Split combined planning rows into small W tickets.

| ID | Priority and outcome | Evidence or dependency |
| --- | --- | --- |
| PLAN 01 | P0 Reconcile reviews and map authority | Baseline, governing clauses, evidence ledger. |
| PLAN 02 | P0 Measure refusal impact by operation | Single corpus revision; overlap and recovery. |
| PLAN 03 | P0 Decide tags and admission scope | Forms/tag comparison, semantic checks, PLAN 02. |
| PLAN 04 | P0 Approve assembly contract wording | Fields, signatures, links, annotations, outlines, labels, tags, derivatives. |
| PLAN 05 | P1 Remove unused copier policy modes | Production-caller proof; graph safeguards retained. |
| PLAN 06 | P1 Replace meaningful omissions with refusal | PLAN 04; detection, diagnostics, output/state tests. |
| PLAN 07 | P1 Preserve URI meaning and navigation | Approved action and excluded-target rules. |
| PLAN 08 | P1 Preserve annotations and relationships | Split subtype support from ownership where useful. |
| PLAN 09 | P1 Preserve unsigned signature fields | Approved lock/seed subset; signing smoke test. |
| PLAN 10 | P1 Add bookmarks and decide labels | Separate tickets; approved tree projection and labels. |
| PLAN 11 | P1 Deliver approved tag behavior and derivative | Separate preservation/admission and derivative scopes. |
| PLAN 12 | P1 Make rights and XFA preparation explicit | Separate operations; permissions and ordering. |
| PLAN 13 | P1 Make Info/XMP editing predictable | Precedent on absent/malformed packets. |
| PLAN 14 | P1 Narrow signing provider API if approved | Consumers, retained formats/timestamps, migration. |
| PLAN 15 | P1 Resolve workflow ordering and rollback | Separate decisions; reliability preserved. |
| PLAN 16 | P1 Prove and simplify alias handling | Coordinate W-185 before overlapping edits. |
| PLAN 17 | P2 Decide async source direction | W-138/W-109; delayed-range measurements. |
| PLAN 18 | P2 Decide decoder retention | Decoded required-stream scan and filter fixtures. |
| PLAN 19 | P2 Decide forensics and seed policy | Separate consumers/safety; coordinate W-158/W-151. |

Each real ticket names owner, reviewer, decision, dependencies, allowed paths, acceptance, and validation. Separate decisions from implementation, count additions honestly, and attach priority to the proposed decision itself. Unresolved choices stay out of the execution queue.

## Owner decision log and interview protocol

Maintain one log with ID, question, current authority, status, recommendation, alternatives, evidence IDs, impact, exact approved wording, owner/date, superseded clauses, and linked tickets. Statuses: settled principle, recommendation pending approval, evidence pending, approved, rejected, deferred. Record an approved version or diff rather than a vague agreement.

| Decision | Starting status and next action |
| --- | --- |
| O 01 Forms and occurrence namespaces | Settled direction. Retain and specify edge cases. |
| O 02 Tags follow the same preservation principle | Settled principle. Research implementation and admission. |
| O 03 Duplicate page labels are acceptable | Settled constraint. Recommend preservation or fixed exclusion. |
| O 04 Empty signature fields survive | Settled direction. Resolve supported lock/seed semantics. |
| O 05 Refusal and unsigned derivatives | Recommendation pending approval. Resolve certification, timestamps, locks. |
| O 06 Info/XMP and new identity | Evidence pending. Recommend present/absent/malformed behavior. |
| O 07 XFA and Reader-rights preparation | Recommendation pending approval. Quantify workflow/credential friction. |
| O 08 Signing provider and seed policy | Evidence pending. Retain signing capabilities absent separate approval. |
| O 09 Workflow ordering and rollback | Evidence pending. Current contract remains binding. |
| O 10 Async decoders and forensics | Evidence pending. Three independent decisions. |
| O 11 Navigation annotations aliases copier modes | Recommendations pending approval. Separate policy and cleanup. |
| O 12 Contract adoption and queue insertion | Owner approval required. Exact edits and ticket scopes attached. |

**Interview after research.** Ask when defensible product contracts remain, customer demand decides value, or an authority conflict needs Jake. Do not ask him to find code, choose routine implementation details, estimate effort, or repeat settled principles. Missing measurements trigger research, not a preference poll.

Batch at most three related questions. Each names the decision, concrete workflow, observed current behavior, options, recommendation, lost capability, migration/refusal cost, and proposed wording. For example, ask whether title editing should create missing XMP or update Info alone only after presenting measured precedent and version side effects. Record the answer with date and tickets; read back material ambiguity.

If Jake defers, preserve current behavior and mark the affected proposal with the missing decision. Continue independent research. Do not infer consent from elapsed time, another agent's recommendation, or unresolved numbering in the earlier conversation.

## Constraints sources and final handoff

Retain managed .NET engine/workflow boundaries, the closed runtime dependency policy, sparse large-file behavior, long offsets, source snapshots/ownership, raw content copying, bounded graph traversal, permissions, encryption, DocMDP/FieldMDP, incremental prefix preservation, and signing/staging safeguards. No general PDF toolkit, renderer, script engine, signature trust validator, conformance claim, or generalized policy framework.

Current authority is AGENTS.md, docs/spec, ACCEPTANCE.md, README.md, QUALIFICATION.md, reset-plan.md, then agents records. Closed decisions remain binding until superseded. Protected wording requires Jake's approval, subject to documented D-43/D-47 exceptions after authorization. Keep target and shipped behavior distinct. Do not push, release a package, or repurpose extraction as compaction, revision/password removal, or sanitization.

## Source register

Owner discussion: Evaluate Optional Complexity, conversation 6adfb090-51cc-83e8-bc5f-3953c14b7a78, plus the current request. Preserve the four attachments under stable planning references. Reverify historical locations, sizes, and counts against the planning baseline.

| ID | Source and contribution |
| --- | --- |
| R1A | Pasted text (2)(1).txt. Withdraws static-only/read-only cuts; policy reductions and retained guarantees. |
| R1B | Pasted text(5).txt. Revised review; async, decoders, aliases, workflow, metadata alternatives. |
| R2A | Pasted text (2)(2).txt. Contract/matrix; tag refusal, labels, XMP creation, current workflow. |
| R2B | Pasted text(6).txt. Contract/plan; tag/label exclusions, unsigned fields, absent-XMP and ordering alternatives. |
| GOV | AGENTS.md; pdf-task-workflow skill; work-item template; current queue; D-61. Inspected 26 October 2026. |

Primary-source starting points checked 26 October 2026. Pin versions and expand the evidence before adopting behavior.

P1 PDFBox PDFMergerUtility source

https://github.com/apache/pdfbox/blob/trunk/pdfbox/src/main/java/org/apache/pdfbox/multipdf/PDFMergerUtility.java

P2 pypdf merging guide

https://pypdf.readthedocs.io/en/latest/user/merging-pdfs.html

P3 qpdf page selection

https://qpdf.readthedocs.io/en/stable/cli.html#page-selection

P4 Aspose.PDF metadata guide

https://docs.aspose.com/pdf/net/pdf-file-metadata/

## Final handoff check

Independent findings and disagreements are traceable. Every workstream has a disposition or research ticket. The matrix reports measured overlap and recovery cost. Owner questions address unresolved product choices. Exact contract diffs and small tickets include priorities, dependencies, and validation. Implementation remains unstarted until Jake approves the relevant contracts and tickets.
