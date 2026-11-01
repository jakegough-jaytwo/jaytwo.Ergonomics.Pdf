# W-218 operation-contract drafting: session handoff

> **Historical snapshot only; do not execute this handoff.** Use [next-session.md](next-session.md), the single current entry point. Shared, metadata, signing, attachments and images are adopted; next work is security then inspection. Forms remains frozen. All contrary all-draft status, question queues, font caps, shared/metadata sequencing and staging instructions below are historical. Do not stage or commit, including through a temporary index.

Prepared 2026-10-28 at the user's request to continue in a fresh session. This is a historical handoff snapshot, not the live decision ledger or an adopted operation contract. Q-33, Q-143 and the Q-97/Q-102/Q-108/Q-113/Q-118/P1 form boundaries were subsequently selected on 2026-10-30; use [live reconciliation](reconciliation.md) for their current status. Work in `C:/jake/git/jaytwo.Ergonomics.Pdf`. Continue active [W-218](../../../agents/work-items/W-218.md); preserve unrelated working-tree changes and actual staging.

## Mission and authority

Complete reviewable operation contracts from the charter and supported capabilities upward, across the subject drafts. Existing behavior, code, retired specifications, historical decisions/work items and tests are evidence, not automatic requirements. Preserve compatibility knowledge and raise deliberate differences for review. All subject documents remain drafts. Engineering support for individual proposals is not whole-contract adoption, implementation approval or runtime verification.

Read [AGENTS.md](../../../AGENTS.md), [charter](../../charter.md), [supported capabilities](../../supported.md), [spec index](../README.md), the subject drafts, [drafting guide](guide.md), [inventory](inventory.md), [history](history.md) and [live reconciliation](reconciliation.md). Use the repository task/verification skills for the documentation process. The live reconciliation plus the latest answer entries supersede earlier historical 'submitted; no answer' entries. Do not re-ask answered questions. The drafting history preserves provenance, including which answers are owner selections versus engineering support only.

Keep charter/supported scope, AGENTS, historical decision records, current behavior, acceptance and qualification records unchanged for this continuation. No runtime changes are authorized. Do not mark W-218 done, adopt a whole contract or claim qualification. Do not open a new work item or launch implementation to resolve a drafting gap.

## Latest answers: recorded, not awaiting another vote

The latest user answer supported Q-114 through Q-118 as engineering draft proposals. They have been incorporated in [forms](../forms.md#engineering-supported-font-processing-and-wrapping-q-114-to-q-118), history, reconciliation, shared rules, workflows and the index:

- FORM-FONT-09 / Q-114: configurable 64 MiB peak live processing payload per call, an unmeasured starting default. A font under the separate 32 MiB input cap can still exceed this working cap and refuse. Representative workload measurement is required before adoption. It is not total heap/process RAM.
- FORM-FONT-10 / Q-115: configurable defaults 16 composite levels, 100,000 component-reference visits and 1,000,000 expanded-point visits per call. Count actual expansion, detect cycles, enforce limits and restore the call on refusal. All numbers remain unmeasured; declared maxp maxima alone are insufficient.
- FORM-FONT-11 / Q-116: OS/2 versions 0–5, version-aware legacy mixed/ignored bits, refusal when supported permissions cannot be established. Format distinctions come from Microsoft; supported versions/refusal are our proposed policy.
- FORM-FONT-12 / Q-117: choose one cmap in exact (platform, encoding, format) priority (0,4,12), (3,10,12), (0,3,4), (3,1,4); use consistently without missing-glyph fallback to another map. Exact priority is our policy.
- FORM-TEXT-05 / Q-118: wrap at ordinary spaces, retain stored characters and explicit line breaks, refuse overwide unbreakable runs. Repeated/leading/trailing-space placement must be resolved before adoption. Include the wrapping change in Q-108's compatibility review.

Unexecuted scenarios FORM-FONT-Q through X and FORM-TEXT-K/L accompany these clauses. No fresh runtime, rendering, measurement or interoperability verification was performed. No next batch has been selected or submitted: the next available question is **Q-119**.

## Decisions and boundaries to retain

Use the complete decision ledger rather than this compact reminder:

- Deferred resource reads; snapshot described values/options. Single workflow execution even after failure/cancellation. Borrow caller resources, close only internally owned resources. Engine supplied-source borrowing is the default, with separately described explicit ownership transfer/acceptance and cleanup rules.
- Ordinary existing-document workflows use one document/editor session and one save. Workflow fills first, flattening second, remaining ordinary operations in declaration order. Last workflow assignment wins; validate only the effective value. Flatten-all supersedes discarded named selections, including misspellings.
- Output streams initially empty/writable/seekable. Direct file create-or-overwrite by default, with fail-if-exists option. Apply edits and available validation before creating/truncating output. Failed output is left for the caller; no automatic restoration/deletion/retry. Preserve primary failure and report cleanup failures separately. Callers coordinate competing saves.
- Refuse positively established same-file source/destination aliases early; exact mechanisms remain open. Do not reject streams for possible shared resources or use size/timestamp suspicion. Document corruption risk for undetected aliases; explicit source append is separate.
- One active editor; caller coordinates access. Document-visible pending edits survive editor disposal without Save. Single/bulk/flatten call restoration is distinct from output rollback, whole-workflow transactions or validation-only refusal. Proposed ordinary separate-save failure preserves pre-save pending edits when the source remains usable, excluding append/signing failures.
- Preserve selected form naming/value/choice/button/rich-clear/export/appearance, metadata, assembly, Reader-rights and XFA policies already recorded. Do not recreate their question history from scratch. **This handoff predates the 2026-10-30 Q-33 signed granting/refusal selection.**

## Compatibility reviews that remain open

- **Q-97:** proposed NoToggleToOff radio clear refusal changes historical W-68 permissive clearing. Static comparison is recorded; adoption disposition/runtime evidence remain open.
- **Q-102:** correct changed-widget appearance or refusal/restoration changes historical NeedsViewer/value-only outcomes. Keep its review separate from Q-97 and Q-108.
- **Q-108:** overflow refusal/no implicit shrink, Q-113 positive caller/effective DA size and Q-118 wrapping require compatibility and owner adoption review. Accepted historical [D-29](../../../agents/decisions/D-29.md) approved DA-zero single-line width fitting with a 6-point floor; engineering endorsement does not supersede it. Current Build/Wrap observations are static, not rendered fit results.
- **P1 / Q-111:** exact 217 graphics = 95 ASCII + 95 selected Latin-1 + 27 additions; CR/LF are separate Multiline controls. Current PdfTextCodec encoding guard admits U+00AD while P1 excludes it from generated painted text. Assess compatibility without claiming old complete-call success, discretionary-hyphen semantics or rendering. Never silently delete/substitute caller text.

The 32/128 MiB supplied/retained budgets, 64 MiB working budget and composite bounds are unmeasured proposals. A byte cap is not parser/CPU/total-memory qualification. P1 eligibility is distinct from scalar MaxLen counting, actual glyph coverage, shaping, field values/names/export keys and visual fit.

## What to do next

Reconcile the latest supported clauses with live open details and review scope, inputs, outcomes, preservation, authorization, limits, refusal, failure/cancellation and scenarios. Continue useful authorized drafting across affected subjects. Prioritize questions that unblock other contracts rather than endlessly polishing one font corner.

Substantive remaining gaps include general font parser/byte-scan/CPU/object/peak bounds and cancellation checkpoints; font source positions/stability; precise repeated/edge-space layout, metrics/content box/fit tolerances; unselected/duplicate cmap validation; font instruction/table/subsetting handling; choice presentation and rich-clear XML/style/API integration; malformed form reads/hierarchies/encoding/export representation/global readiness; positive file identity and remaining save/source-append lifecycle details; assembly preservation subsets and other operation budgets. These are gaps, not selected answers or an allocated question batch. Choose the next five after reading current drafts and checking dependencies.

If historical research bears on a question, explain findings and limitations. Use primary sources for established-tool comparisons; distinguish documented behavior, source-derived inference and executed versioned probes. Local PDF reference material is in `C:/jake/git/pdfdocs`; follow its instructions and do not reproduce licensed material. Existing Microsoft OpenType primary links are in forms.md. Do not run probes or report historical tests as fresh evidence merely to make a draft look verified.

## Required response in every drafting iteration

The next agent is the **drafting agent**, not an endorsement-only reviewer. When the user supports proposals, update the files and reconcile references before asking more questions. Do not merely reply 'I support these; no files changed.'

Each iteration should:

1. Read and incorporate the latest answers with their exact qualifications. Separate an owner selection, engineering-supported draft proposal, whole-contract adoption and executed verification.
2. Complete authorized drafting between questions: stable clause IDs, preservation/authority/failure limits, explicitly unexecuted scenarios, provenance and cross-subject references. Retain unresolved details rather than filling them with assumptions.
3. Run the applicable documentation checks and record actual results in W-218 Closeout. Keep the item active and unrelated work/staging intact.
4. End with a concise account of delivered progress, material unresolved decisions and checks actually run/not run. The owner superseded the fixed five-question cadence on 2026-10-31: resolve routine details through engineering judgment and ask only when a material owner decision is actually needed. Do not manufacture a question batch or seek endorsement of every draft clause.

The earlier Q-119 five-question cadence is historical and superseded by the owner's progress direction below. Do not resume it.

The expected user reply can be 'Support Q-119–Q-123 as engineering draft proposals, with these qualifications…', individual answers, requests for more evidence or a rejection. Preserve uncertainty: do not treat engineering support as whole-contract adoption. If the reply is ambiguous, keep the affected detail open and continue independent drafting rather than guessing. Do not require the user to repeat already settled boundaries.

## Verification and worktree safeguards

The inherited tree contains extensive unrelated tracked/untracked edits and deletions, including scope/behavior/build/test/skill work. Inspect it; never reset, clean or stage the whole repository. This drafting work remains uncommitted. Baseline protected files and the real index before editing; check that your iteration preserved them. The current owner instruction is no staging or committing; do not stage through a temporary index either. Report staged-only verification as unavailable until staging is authorized.

Follow [task workflow](../../../.agents/skills/pdf-task-workflow/SKILL.md) and [verification](../../../.agents/skills/pdf-change-verification/SKILL.md). Run make agents-check and scoped working-tree whitespace checks; validate local Markdown links/anchors, table structure and fences across docs/spec. Staged task-check remains unavailable while staging is prohibited. Record commands/counts actually observed, with missing/unrun checks explicit. The previous iteration checked 19 Markdown files; adding this handoff makes 20. Documentation-only work does not require runtime tests or a Docker integration checkpoint. Do not populate acceptance/qualification records with hypothetical outcomes.

## 2026-10-31 limit selection and breadth checkpoint

Historical checkpoint, superseded: the earlier A4 font threshold was withdrawn by the owner; there is no font-specific size limit. Forms is frozen, shared/metadata and the signing/attachments/images batch are adopted, and their review queues are closed. The [old subject review packet](w218-limit-and-subject-review.md) preserves provenance only. Continue solely from [next-session.md](next-session.md); it names the remaining work and preserves the unmet forms A1 exit.

## Owner direction: proportional effort and engineering discretion, 2026-10-31

The owner considers forms effort disproportionate and directs significant progress across the library without repeated decisions on minutiae. Resolve routine details when charter, established-tool behavior and a simple approach converge. Where tools differ on a minor interpretive point, select the simplest compatible approach and document the result briefly; divergence alone is not an owner blocker. Do not perform new competitor research for every minor choice: use relevant evidence already available, and investigate only uncertainty material to an observable requirement.

Use precise requirements for user-visible outcomes, preservation, compatibility and controlled failures. Words such as exact, must and cannot do not create extra precision, arbitrary recognizer restrictions, exhaustive proof campaigns or universal guarantees beyond those outcomes. Distinguish a concrete correctness risk from an imagined edge case. Prefer a small representative check capable of falsifying the promise; research stops when it supports a reasonable decision. Do not invent limits to make a draft look complete.

Freeze further forms elaboration now. Keep existing evidence and genuine unresolved risks visible; do not claim A1 completion, weaken its exit or reinterpret it as requiring exhaustive pre-adoption implementation proof. Continue shared then metadata within W-218 without another sequencing approval request. Settle minor details as engineering choices, then move through the remaining subjects. Present a consolidated contract for adoption instead of seeking approval clause by clause. This instruction does not itself adopt a whole contract or authorize governing changes, scope expansion, architectural exceptions, weakened acceptance/qualification or material compatibility loss. Escalate those concrete cases with a recommendation and evidence, not hypothetical decision trees.
