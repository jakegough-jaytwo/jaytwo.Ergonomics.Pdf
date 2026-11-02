# Historical evidence and owner decision queue

Historical drafting aid only, not a live owner-decision queue. Continue from [next-session.md](next-session.md): security then inspection; shared, metadata, signing, attachments and images are adopted, and forms is frozen. The owner superseded routine detail-by-detail approval: resolve minor details by engineering judgment and escalate only concrete material decisions. This record preserves provenance, adopts no historical policy, supersedes no decision and changes no implementation guarantee.

## How the history was reviewed

The title/status catalogs of all 218 work items and 71 decision records were surveyed. The topic notes below use targeted decision text, work-item closeouts and proposal excerpts, rather than claiming a full reread of every record. Historical test outcomes were not rerun and are not fresh verification evidence.

Sources have distinct roles:

- Charter and supported.md establish the current scope baseline.
- An accepted decision records an earlier owner choice and its context. Retain that provenance and consider compatibility; do not silently treat the entire earlier design as the rebuilt contract or silently supersede it.
- A completed implementation item records delivered behavior and evidence at its recorded revision.
- A completed research item can leave its recommended product contract entirely unapproved.
- A proposed decision or unreviewed approval packet is a question, not authorization.

[W-211](../../../agents/work-items/W-211.md) completed reconciliation, explicitly adopting nothing. Its [approval ledger](../../../agents/reference/simplification/W-211/approvals.md) leaves packets P1â€“P11 and R unreviewed. [D-66](../../../agents/decisions/D-66.md) authorized research sequencing rather than product recommendations. This review neither approves those packets nor treats their completion as spec adoption.

## Opening and saving

[D-55](../../../agents/decisions/D-55.md) approved genuine asynchronous reads during opening and explicitly left later inspection synchronous. [W-132](../../../agents/work-items/W-132.md) records source ownership and snapshot implementation; [W-131](../../../agents/work-items/W-131.md) records one-editor and pending-edit lifetimes. [W-208](../../../agents/work-items/W-208.md) reports later synchronous reads and supplies research to still-proposed [W-138](../../../agents/work-items/W-138.md). [D-56](../../../agents/decisions/D-56.md) records a compatibility reason for matching the last cross-reference format, including an older size ceiling.

Current scope settles sync/async opening, streams/stable random-access sources and incremental prefix preservation. Q-03 subsequently recorded the owner's async preference; exact later coverage/fallback is open. Q-55/Q-56 subsequently selected default borrowing with optional transfer and document-visible pending edits that survive editor disposal; Q-57 selected one active editor, with no ordinary PDF writes before Save. The user subsequently supported Q-58 to Q-64 as engineering proposals covering transferred-input cleanup/acceptance, no-edit output, failed separate-save pending state, primary opening failure, cleanup-only failure and independent cleanup continuation. Owner adoption, diagnostic design and runtime verification remain separate; dependent cleanup need not proceed after a prerequisite fails. Workflow destination decisions do not automatically settle engine output rollback, cross-reference strategy, numeric limits or retry.

## Inspection

[W-150](../../../agents/work-items/W-150.md) and [W-170](../../../agents/work-items/W-170.md) record defects in historical revision/coverage interpretation. [W-210](../../../agents/work-items/W-210.md) separates rich CMS inspection from revision/coverage properties and recommends retention, without adoption. [W-156](../../../agents/work-items/W-156.md) completed a separate admission-policy API design with owner questions still open.

Current scope includes page/form inspection and structural signature/revision claims without validation/trust. Choose the reporting depth, malformed-data outcomes and completeness semantics. Do not add the proposed host-admission/policy-verdict API merely because W-156 is done.

## Forms

[D-38](../../../agents/decisions/D-38.md) included all-or-nothing bulk filling in the earlier phase scope. [W-68](../../../agents/work-items/W-68.md) implemented a typed value model; [W-96](../../../agents/work-items/W-96.md) records explicit owner confirmation of its value type, export exclusions and clearing semantics. [D-17](../../../agents/decisions/D-17.md) records password/signature-fill refusal; [D-29](../../../agents/decisions/D-29.md) records a narrow appearance extension. [W-77](../../../agents/work-items/W-77.md) records flattening and limitations. [D-70](../../../agents/decisions/D-70.md) is the later caller-TrueType scope commitment, reflected in supported.md.

Supported field families, individual/bulk population, export, caller fonts and eligible flattening are in scope. The owner selected per-call bulk atomicity; Q-66 has engineering support for workflow effective flatten selection; Q-67 to Q-72 separately support engine atomicity/effective validation and exact full-name/ambiguity/terminal targeting as draft proposals. Q-73 to Q-75 now ask about semantic typed population values, clearing and ordinary export membership. Name decoding/malformed hierarchy/reporting/error precedence and detailed value/appearance/layout/character/script rules remain open; historical API/subset details are not automatic target requirements.

## Metadata

[W-148](../../../agents/work-items/W-148.md) records hardened XML input handling with existing size values. [W-200](../../../agents/work-items/W-200.md) completed research; its [exact proposal](../../../agents/reference/simplification/W-200/proposal.md) recommends atomic coordinated Info/XMP setters, creating missing XMP, and independent raw-packet replacement. That proposal remains unadopted. [W-201](../../../agents/work-items/W-201.md) separately addresses new-document identity; it does not establish ordinary-edit metadata behavior.

Info, authorship/custom keys and XMP read/update are in scope. Q-13 to Q-30 subsequently selected the coordinated/raw/read, mapping, scalar/clear/date and preservation policies now recorded in metadata.md. Those questions are answered; exact XML/RDF/encoding subsets, numeric limits and remaining value/no-op edges are still open. The owner answers select explained clauses, not the complete W-200 proposal or its candidate implementation.

## Attachments

[W-75](../../../agents/work-items/W-75.md) records relationship/date/checksum/compression and viewer-panel functionality. [W-153](../../../agents/work-items/W-153.md) records a bounded decoded stream and safe-filename helper; its old unlimited overload and exact filename/size rules are implementation evidence.

Add/replace/remove, streaming reads/extraction, caller-controlled limits and filename helpers are explicit scope. Decide replacement identity/collisions, which metadata/options to promise, the default when a limit is omitted, and what the filename helper guarantees. Do not infer PDF/A-3 compliance or page-level annotation editing.

## Pages and assembly

[D-61](../../../agents/decisions/D-61.md) approved the earlier assembly design, including deliberate omissions, unencrypted output with explicit consent and detailed permissions. [W-190](../../../agents/work-items/W-190.md) through [W-197](../../../agents/work-items/W-197.md) completed separate preservation/refusal studies for forms, tags, navigation, annotations, empty signature fields, bookmarks, labels and unsigned derivatives. Those recommendations were not adopted by W-211.

Current scope settles 90-degree rotation and selected-page ordering/repetition, but expressly leaves preservation/omission/refusal to operation contracts. Q-01 is resolved as a feature-by-feature drafting approach, with material loss choices brought to the owner. Feature-specific subsets, new-document identity, encrypted-input/output handling, existing signatures and any explicit derivative need subsequent choices. A general loss preference does not automatically approve every proposed feature or consent option.

## Security, Reader rights and XFA

[D-24](../../../agents/decisions/D-24.md), [D-26](../../../agents/decisions/D-26.md) and [D-58](../../../agents/decisions/D-58.md) record lock/certification interpretation and fail-closed checks. [D-44](../../../agents/decisions/D-44.md), [D-45](../../../agents/decisions/D-45.md) and [D-48](../../../agents/decisions/D-48.md) record a sequence of rights-preservation and hybrid-XFA policies; read them together rather than selecting an earlier rule alone. [W-198](../../../agents/work-items/W-198.md) and [W-199](../../../agents/work-items/W-199.md) instead propose explicit preparation/removal and changed caller steps; they remain research.

[D-68](../../../agents/decisions/D-68.md) explicitly declines to settle permission setters as owner-only edits of already-encrypted files. [W-154](../../../agents/work-items/W-154.md) records authentication-role and permission outcomes. [W-155](../../../agents/work-items/W-155.md) records encryption interoperability findings; [D-67](../../../agents/decisions/D-67.md) remains proposed, with corrective items W-214â€“W-217 not implied adopted here.

Enforcement of permissions/DocMDP/FieldMDP and documented Reader-rights handling are explicit scope. Q-31/Q-32 and Q-34 to Q-39 subsequently selected grant-aware preservation, explicit unsigned nongranting-rights removal, explicit XFA abandonment, general-modification authority, per-call guarantees and separate rights-first/XFA-second preparation without an intermediate save. Q-33 signed-document rights removal remains explicitly undecided. Per-operation eligibility, encryption schemes, setter authorization and output handling remain open. Password changes/removal and adding encryption are not authorized by permission editing. [D-69](../../../agents/decisions/D-69.md) supplies the current XFA/PDF-A boundary, not a universal per-operation answer.

## Signing and timestamps

[D-14](../../../agents/decisions/D-14.md) and its revision [D-21](../../../agents/decisions/D-21.md) record timestamp nonce/certificate-request policy. [D-46](../../../agents/decisions/D-46.md) records encrypted signature-string interoperability. [D-64](../../../agents/decisions/D-64.md) approved a specific workflow signer/provider surface, staging/revision order and no automatic retries. [W-151](../../../agents/work-items/W-151.md) records owner-approved seed enforcement; [W-202](../../../agents/work-items/W-202.md) and [W-203](../../../agents/work-items/W-203.md) later researched narrower alternatives and recommended retaining the earlier behavior.

Host-controlled keys, detached CMS, PAdES B-B/B-T and the two timestamp uses are explicit scope. Ask about raw-digest versus complete-CMS integration, required key/digest subsets, seed/lock handling, provider/token checks, revision order and retry guarantees. Keep validation/trust and B-LT/B-LTA excluded under D-70/current supported scope.

## Images

[D-68](../../../agents/decisions/D-68.md) adopted the limited image exception and preserved 8-bit non-interlaced RGB/RGBA as an initial PNG implementation boundary. The current supported document commits to JPEG/PNG with explicitly specified variants, transparency/profile/coordinate behavior and resource limits.

Confirm the rebuilt target subset, alpha/transparent-color handling, color profiles, coordinates, fit/clipping, supported page dimensions and failure outcomes. Do not silently turn an initial implementation boundary into an enduring scope boundary.

## Workflows and publication

[D-60](../../../agents/decisions/D-60.md) approved deferred description, borrowed input ownership, one-run execution, merged last-value-wins fills before flattening and other operations in declaration order. [W-204](../../../agents/work-items/W-204.md) measured behavior differences from alternative ordering and recommended retention. [D-64](../../../agents/decisions/D-64.md) adds signing staging and terminal rules. [D-65](../../../agents/decisions/D-65.md) narrows publication guarantees under concurrent filesystem changes; [W-206](../../../agents/work-items/W-206.md) recommends retaining alias safeguards. [W-205](../../../agents/work-items/W-205.md) records a final-copy exception-preservation finding and a proposed correction, not a shipped fix.

Composition/defaults/order, form-export shortcuts and file publication are explicit scope. Q-40 to Q-54 selected ordinary ordering, effective fills, one-run lifetime, deferred/captured description, borrowing/session and direct-output/default/failure/positive-identity policies. Q-65/Q-66 subsequently received engineering support for explicit preparation integration and effective flatten selection, including its validation consequence; owner adoption/runtime verification remain separate. Do not repeat those policy questions. Exact API/identity mechanisms, repeated preparation declarations, special terminals, source stability/positions, workflow cleanup-only outcomes, diagnostics and cancellation remain open. Complete-file publication/crash durability are not inferred; direct files permit partial failure output.

## Shared limits and failure policy

[W-149](../../../agents/work-items/W-149.md) remains proposed for cumulative budgets. [W-167](../../../agents/work-items/W-167.md) records benign baseline work; [W-168](../../../agents/work-items/W-168.md) leaves adversarial evidence pending; [W-169](../../../agents/work-items/W-169.md) adjusts work counters. [W-209](../../../agents/work-items/W-209.md) researches decoder compatibility and caps, recommending retention with a separate abbreviation proposal.

Documented limits and explicit failures are required. Numeric caps, configurable defaults, cumulative budgets, supported decoder subsets and cancellation granularity need per-operation decisions. Historical measurements neither set a requirement automatically nor prove current qualification.

## Initial questions and owner responses

These questions were sent together during this review. Q-01 and Q-02 were answered after the requested competitor comparison: feature-by-feature assembly and per-call atomic bulk population. Q-03 has an owner direction recorded below; no preselected option or silence counts as an answer.

| ID | Decision | Alternatives presented | Affected drafts |
| --- | --- | --- | --- |
| Q-01 | Unpreservable features associated with selected pages during assembly | Default refusal with specifically documented consented losses; default documented/reported omissions; or decide the policy by feature first. | Pages, shared rules, security, workflows |
| Q-02 | Bulk population atomicity | All-or-nothing; partial success with per-field results; or both with atomic default. | Forms, workflows, failures |
| Q-03 | Async reach beyond opening | Async source-read path through inspection/editing/saving; opening only with later blocking documented; or select operations individually first. | Opening/saving, inspection, workflows |

The competitor comparison prompted a narrower follow-up question for Q-01 (develop feature-by-feature preservation/omission/refusal) and a precise Q-02 question (rollback of one bulk call while retaining prior edits). The owner selected both in the follow-up response below.

Answers settle only the choice asked. Exact subsets, exception types, default sizes, mechanisms and implementation authorization remain separate unless explicitly answered. Record the owner's wording and link it to the affected draft; draft changes still need their appropriate adoption status.

### Owner response, 2026-10-27

For Q-01 and Q-02, the owner requested competitor-informed caller expectations before selecting a policy. See the [comparison](competitor-expectations.md); its recommendations are not owner decisions.

For Q-03, the owner said: "async is highly preferred and locking operations should be the fallback when there is no async option". The draft interprets "locking" as blocking synchronous I/O and records the preference in [opening and saving](../opening-and-saving.md#owner-direction-asynchronous-operations). Exact operation coverage and fallback mechanisms remain to be specified; no implementation or wholesale historical design adoption follows.

### Owner selections after the competitor comparison

On 2026-10-27 the owner selected:

- Q-01: "Yesâ€”develop the feature-by-feature contract; ask me about the material loss choices." The pages draft now contains a feature matrix, with individual outcomes unresolved.
- Q-02: "Yesâ€”make each bulk call all-or-nothing." The forms draft records FORM-BULK-01: restore pending state before the failed call while retaining earlier successful edits.

These are specific owner decisions within drafts, not adoption of every remaining operation detail or authorization to change runtime behavior. No older decision record is edited or silently superseded.

### Assembly questions Q-04 to Q-06

| ID | Question | Status |
| --- | --- | --- |
| Q-04 | Preserve editable ordinary forms with independent values across repeated input occurrences, including colliding source names? | Accepted preservation/independence; ASSEMBLY-FORM-01. Exact naming/subsets remain separate. |
| Q-05 | Preserve source bookmarks and page labels versus fresh numbering/no source bookmarks, or decide them separately? | Accepted bookmark hierarchy and source labels; ASSEMBLY-BOOKMARK-01 / ASSEMBLY-LABEL-01. Exact unsupported cases remain separate. |
| Q-06 | When accessibility tags cannot be preserved, refuse, require explicit consent for tag loss, or allow documented/reported omission? | Accepted preservation target and default refusal; ASSEMBLY-TAG-01. Supported structures/diagnostics remain separate; no untagged derivative adopted. |

### Owner acceptance of preservation recommendations

On 2026-10-27, after the explanations of W-190/W-195/W-196 and competitor tag behavior, the owner stated: "i accept your recommendations". The pages draft records ASSEMBLY-FORM-01 (independent editable forms and preservation-or-refusal for retained widgets), ASSEMBLY-BOOKMARK-01 (hierarchy with non-clickable ancestors when destinations are excluded), ASSEMBLY-LABEL-01 (source labels, not printed folio rewriting) and ASSEMBLY-TAG-01 (preservation target with default refusal, no automatic untagged fallback).

This accepts those explained target policies rather than every sentence of the source proposals. Exact naming, repeated-page field identity, repeated bookmark targets, supported structure subsets, failure timing and limits remain open. No current runtime change, whole historical contract adoption, conformance claim, untagged derivative or blanket tagged-input refusal is authorized.

### Next detailed questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-07 | Use W-190's fixed InputN field namespace based on input position? | Accepted InputN prefixes by input position; ASSEMBLY-FORM-02. Additional repeated-field naming remains open. |
| Q-08 | Repeated pages share fields within an input, while separate input entries remain independent? | Owner selected independent fields for repeated pages, differing from W-190; ASSEMBLY-FORM-03. Multi-page grouping remains open. |
| Q-09 | Bookmarks target the first retained copy of a source page within that input? | Accepted first retained copy within each input; ASSEMBLY-BOOKMARK-02. |

### Owner selections for Q-07 to Q-09

On 2026-10-27, the owner selected InputN prefixes based on input position, independent fields on repeated copies of a page (rather than the offered W-190 shared-within-input policy), and bookmark targets at the first retained copy within the input. The pages draft records ASSEMBLY-FORM-02, ASSEMBLY-FORM-03 and ASSEMBLY-BOOKMARK-02.

W-190's observations still describe its pinned baseline: they do not verify the newly selected independent repeated-field contract. At that checkpoint, additional field names and cross-page field grouping remained unresolved; subsequent Q-10/Q-11 selections below resolve grouping, without a general split-every-widget policy. Old records and runtime behavior remain unchanged by this drafting transaction.

### Multi-page field grouping question submitted

Q-10 asks whether selection [1, 2, 1, 2] from a two-page form whose pages share a field should produce two independent form copies, four independent page-level fields, or require explicit caller grouping. The owner selected two independent form copies, preserving sharing within each, on 2026-10-27. ASSEMBLY-FORM-04 records the required [1, 2, 1, 2] result. Uneven selections such as [1, 1, 2] and exact repeated-field names are not resolved by the example.

### General repeated-form grouping question submitted

Q-11 proposes grouping by each source page's occurrence number: first copies of pages belong to form copy 1, second copies to copy 2, etc., without reordering output. For [1, 1, 2], the first page 1 and the selected page 2 share the first form copy's field; the second page 1 has an independent field in a partial second copy. Alternatives are explicit caller-defined grouping for uneven repetition or refusal of uneven repeated selections involving shared fields. Accepted by the owner on 2026-10-27: group by each source page's occurrence number, allowing partial form copies. ASSEMBLY-FORM-05 records the rule and draft examples. It has no W-190 verification claim.

### Repeated-copy naming question submitted

Q-12 proposes uniform InputN.CopyM original-name prefixes, including Copy1 when no page repeats. This would explicitly refine the earlier simple InputN example; it is not inferred from that earlier choice. Alternative: retain short InputN names for non-repeated selections and introduce CopyM for inputs with repeated pages, or leave naming undecided. Accepted on 2026-10-27: always use InputN.CopyM names, including Copy1 for non-repeated inputs. ASSEMBLY-FORM-02 now records the uniform rule, explicitly refining the earlier shorter examples.

### Field-name amendment, 2026-11-01

The owner superseded the Q-12 selection: an input entry with no repeated page keeps `InputN.` names, and an entry that repeats a page uses `InputN.CopyM.`. ASSEMBLY-FORM-03 to 05 stand, and no change to ASSEMBLY-FORM-05's uneven-repeat grouping was decided. pages.md ASSEMBLY-FORM-02 records the rule.

### Metadata questions submitted after W-200 review

| ID | Question | Status |
| --- | --- | --- |
| Q-13 | Coordinate mapped ordinary Info/XMP edits atomically, with raw packet replacement separate? | Accepted METADATA-SYNC-01; exact mappings/dates not inferred. |
| Q-14 | For coordinated edits, create minimal missing XMP rather than an Info-only update or creation opt-in? | Accepted METADATA-CREATE-01; minimal mapped-property creation without unrelated Info backfill. |
| Q-15 | Refuse uneditable existing XMP atomically and require explicit replacement before retry, versus explicit Info-only mode or per-case policy? | Accepted METADATA-REFUSE-01; atomic refusal, explicit replacement before retry, no automatic repair/fallback. |

The recommendations and partial-state/malformed-packet observations come from the pinned W-200 findings, not a fresh execution. The owner accepted the three policies on 2026-10-27; metadata.md records METADATA-SYNC-01, METADATA-CREATE-01 and METADATA-REFUSE-01. This does not adopt W-200's complete mapping, date, parser-limit, raw-packet or exception contract.

### Metadata value/date questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-16 | Plain Title/Subject update x-default while preserving other translations? | Accepted METADATA-TEXT-01; null/removal separate. |
| Q-17 | Scalar Author replaces the whole list with one literal entry, without punctuation splitting? | Accepted METADATA-AUTHOR-01; previous author list deliberately replaced by one literal entry. |
| Q-18 | Refresh modification time on ordinary metadata edits unless explicitly assigned, changing creation time only explicitly? | Accepted METADATA-DATE-01; precision/offsets/override lifetime separate. |

These recommendations come from W-200's findings/proposal. They are not inferred from the accepted synchronization/creation/refusal policies. The owner accepted the stated policies on 2026-10-27; metadata.md records METADATA-TEXT-01, METADATA-AUTHOR-01 and METADATA-DATE-01. No additional author-list API or unasked timestamp detail is adopted.

### Metadata removal and date-detail questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-19 | Clearing Title/Subject removes all translations, and clearing Author removes the entire list? | Accepted whole-property clearing; METADATA-CLEAR-01. |
| Q-20 | Explicit modification-date override lasts through the open document or only through the next successful save? | Accepted open-document lifetime, including later editors/saves; METADATA-DATE-02. |
| Q-21 | Whole-second dates with caller offset in Info and equivalent UTC in XMP, or UTC in both? | Accepted whole seconds, caller offset in Info/equivalent UTC in XMP; METADATA-DATE-03. |

W-200's whole-property removal, document-lifetime override and date representation are proposals presented with their tradeoffs. The owner separately accepted these three choices on 2026-10-27; metadata.md records METADATA-CLEAR-01, METADATA-DATE-02 and METADATA-DATE-03. This does not adopt unasked null-date/reset, subsecond conversion or date-syntax rules.

### Remaining mappings and raw-packet questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-22 | Standard Keywords/Creator/Producer/date mappings, literal Keywords and explicit-only Creator/Producer updates? | Accepted METADATA-MAP-01; literal Keywords and explicit-only Creator/Producer changes. |
| Q-23 | Unmapped custom values in Info only, with coordinated modification dates and minimal date-packet creation as needed? | Accepted METADATA-CUSTOM-01; Info-only custom values, coordinated modification date and minimal date-packet creation when needed. |
| Q-24 | Raw packet XML/security/size validation, byte preservation, no Info/date synchronization and acceptance outside the synchronization subset? | Accepted METADATA-RAW-01; exact size values and other unasked details remain separate. |

These recommendations are sourced to the W-148/W-200 historical records and proposals. The owner separately accepted these choices on 2026-10-27; metadata.md records METADATA-MAP-01, METADATA-CUSTOM-01 and METADATA-RAW-01. Numeric caps, unasked encodings, raw-call atomicity/lifetimes and full RDF-shape support are not inferred.

### Metadata date-clear/conversion and serialization questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-25 | Explicit ModDate clearing suppresses automatic dates for the open document, with mapped CreationDate clearing explicit-only? | Accepted METADATA-DATE-04; explicit absent modification date remains authoritative for the open document. |
| Q-26 | Whole-second conversion discards fractions rather than rounding or rejecting them? | Accepted METADATA-DATE-05; discard fractional seconds. |
| Q-27 | Ordinary synchronized edits preserve unrelated XMP semantics while allowing serialization/prefix/format/padding changes? | Accepted METADATA-PRESERVE-01; unrelated data preserved semantically, raw replacement remains byte-preserving. |

Recommendations are drawn from W-200. The owner separately accepted the three policies on 2026-10-27. They are recorded in metadata.md without adopting unasked limits, RDF shapes or raw/read behavior.

### Targeted synchronization, raw failure and read-view questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-28 | Validate touched properties/required shared XML-RDF structure while preserving unrelated custom data, rather than checking the entire graph? | Accepted METADATA-VALIDATE-01; exact RDF shapes/limits still separate. |
| Q-29 | Failed raw replacement leaves prior packet, Info/date state and earlier pending edits unchanged? | Accepted METADATA-RAW-02; all prior pending state unchanged, no file-publication implication. |
| Q-30 | Store-specific getters versus Info-first XMP fallback versus a designed XMP-preferred unified view? | Accepted store-specific METADATA-READ-01; no merged view or automatic fallback. |

W-148 and W-200 supply historical observations/recommendations for these choices. The owner accepted these three policies on 2026-10-27. Exact packet/shape, API and limit details not asked remain open.

### Reader-rights and hybrid-XFA questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-31 | Preserve Reader rights whose recorded grants permit an edit, deciding other cases separately, versus removal before every write? | Accepted SECURITY-RIGHTS-01: preserve granting rights; nongranting cases remain separate. |
| Q-32 | Require explicit caller action before an operation abandons hybrid XFA? | Accepted FORM-XFA-01: explicit caller action before required XFA abandonment; operation/authorization rules separate. |
| Q-33 | Prohibit removal of Reader rights from already-signed documents? | Owner explicitly left undecided; do not infer prohibition adoption or a signature-breaking mode. D-45 history remains unchanged. |

These questions do not adopt W-198/W-199 preparation APIs, their permission classification, global write refusal or signing/derivative modes. The owner selected Q-31/Q-32 on 2026-10-27 and explicitly left Q-33 undecided. No signed-rights removal policy, preparation interface or relaxed permission is inferred.

### Unsigned nongranting rights and XFA preparation questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-34 | For unsigned inputs, require explicit authorization before removing nongranting rights, versus documented automatic removal? | Accepted SECURITY-RIGHTS-02: explicit removal authorization for unsigned nongranting rights; Q-33 remains undecided. |
| Q-35 | Classify explicit XFA abandonment as general modification despite the measured stronger-credential cost? | Accepted FORM-XFA-02: general modification authority, with stronger-credential cost explicitly disclosed. |
| Q-36 | Explicit XFA abandonment is per-call atomic and repeat-safe/no-op after completed removal? | Accepted FORM-XFA-03: per-call atomicity and repeated action is a no-op; historical models remain non-engine evidence. |

The owner accepted the three policies on 2026-10-27. They do not approve runtime implementation, a complete preparation API, new rewrite strategy or weakening of other signature/certification/permission gates. Signed-document rights removal remains expressly undecided.

### Rights-removal authorization, sequencing and atomicity questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-37 | General modification authority for explicit rights removal on eligible unsigned inputs? | Accepted SECURITY-RIGHTS-03; general-modification authority, Q-33 signed eligibility remains open. |
| Q-38 | Separate rights-first then XFA preparation actions in one editing session without an intermediate save, versus a combined atomic operation? | Accepted FORM-PREPARE-01; separate rights-first then XFA actions, no intermediate save required. |
| Q-39 | Rights removal is per-call atomic and completed repeats are no-ops? | Accepted SECURITY-RIGHTS-04; per-call atomicity and no-op completed repeats, historical models not engine verification. |

The owner accepted the three policies on 2026-10-27. Exact API spelling, workflow convenience, role/error details and signed eligibility remain open. Explicit XFA abandonment already has its selected general-modification/per-call guarantees; that does not automatically adopt every rights-removal counterpart.

### Workflow ordering, duplicate assignments and lifecycle questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-40 | Stage ordinary fills as one bulk fill, then flatten, then other ordinary operations in declaration order? | Accepted WORKFLOW-ORDER-01; explicit-preparation integration remains separate and must respect required preparation order. |
| Q-41 | Deferred workflow duplicate fills use last-value-wins and validate only effective values? | Accepted WORKFLOW-FILL-01; direct-engine bulk duplicates remain separate. |
| Q-42 | Workflow descriptions execute once, including after failure/cancellation, versus reusable descriptions? | Accepted single-execution WORKFLOW-LIFE-01; retry/resource/signing details remain separate. |

W-204 supplies measured examples of staging/duplicate differences and recommends the staged policy. D-60 records earlier workflow lifecycle choices. The owner accepted these three stated policies on 2026-10-27. Other D-60/W-204 ownership, API, flatten-merging, publication and retry details remain open.

### Workflow description, ownership and session questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-43 | Defer PDF/attachment reads until terminal execution while snapshotting described scalar/selection/options values? | Accepted WORKFLOW-DESCRIBE-01; deferred reads and captured values/options, borrowed bytes not snapshotted. |
| Q-44 | Borrow caller streams/sources, keeping them open on success/failure/cancellation; own internally created/path resources? | After examples/research, owner selected WORKFLOW-OWN-01: borrow supplied resources, own/dispose internally created ones. |
| Q-45 | Ordinary existing-document edit workflow uses one document/editor session and one save? | After clarification, owner selected WORKFLOW-SESSION-01: one ordinary session/save; other terminal/revision rules separate. |

D-60 and W-204 provide the historical model and measured argument/lifecycle observations. Q-43 was accepted on 2026-10-27. For Q-44/Q-45 the owner first requested evidence/clarification, then selected borrowing and one ordinary session/save on 2026-10-27; see [ownership/session examples](workflow-ownership.md). Other lifecycle/publication details remain open.

### Workflow output and publication questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-46 | Initially require empty/writable/seekable workflow output streams, versus designing non-seekable staged output? | Accepted WORKFLOW-STREAM-01: initially empty/writable/seekable; no rollback subsequently selected in WORKFLOW-STREAM-02. |
| Q-47 | Process file terminals into a temporary sibling and publish completed output, versus direct writes? | Owner selected direct file output, differing from staged recommendation; WORKFLOW-FILE-01. Exact partial-output/cleanup/overwrite rules separate. |
| Q-48 | Caller coordinates independent saves to the same path, without a library transaction-isolation/create-if-absent promise? | Accepted WORKFLOW-CONCURRENCY-01: caller coordination; no isolation/create-if-absent promise inferred. Direct-write races need their own contract. |

W-205 supplies the distinction between pending-state restoration, physical stream restoration and publication. D-60/D-65 record earlier output strategies/limits. The owner selected the three stated policies on 2026-10-27, choosing direct file output rather than staging. This does not adopt the old completed-file race guarantee or silently change current code/D-65 history. Direct-write overwrite/failure risks are explicitly recorded for follow-up.

### Direct-file overwrite, opening and failed-output questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-49 | Require explicit overwrite consent, disabled by default? | Owner requested Linux/.NET expectations; primary-doc comparison added and question resubmitted. Owner selected create-or-overwrite by default with fail-if-exists on 2026-10-27; WORKFLOW-FILE-04. |
| Q-50 | Apply ordinary pending edits/available validation before creating or truncating output? | Accepted WORKFLOW-FILE-02: ordinary edits/available validation precede creation/truncation; later failures may still be partial. |
| Q-51 | Leave failed direct output for caller discard/replacement, without restoration/automatic deletion, versus deleting only new failed files? | Accepted WORKFLOW-FILE-03: leave failed output, no restoration/automatic deletion; owned-handle cleanup required. |

These questions follow the owner's explicit direct-file choice. On 2026-10-27 the owner selected Q-50/Q-51 and requested comparison before selecting Q-49. [Overwrite precedents](workflow-ownership.md#overwrite-conventions-q-49-follow-up) compare GNU/Bash and .NET behavior; the owner subsequently selected create-or-overwrite with a fail-if-exists option. Exact direct-write identity/create/error details remain open. These choices do not reinstate staged publication or imply rollback, automatic deletion or input-file rewriting; overwrite follows the separately selected WORKFLOW-FILE-04. Source-alias safeguards and internal signing preparation remain open; failed-stream retention follows WORKFLOW-STREAM-02.

### Alias refusal, stream contents and primary-failure questions submitted

| ID | Question | Status |
| --- | --- | --- |
| Q-52 | Conservative refusal for known/possible input aliases, accepting documented false positives and limitations? | After comparison, owner selected WORKFLOW-ALIAS-01: early refusal only for established same-file identity through library abstractions; no possible-alias stream/metadata heuristic refusal; document undetected source-corruption risk. Exact mechanisms remain open. |
| Q-53 | Failed borrowed stream output leaves partial bytes versus best-effort truncation to empty? | Accepted WORKFLOW-STREAM-02: leave partial stream output, no automatic rollback; borrowed streams remain open. |
| Q-54 | Preserve original operation/output failure as primary, reporting cleanup/restoration failures separately? | Accepted WORKFLOW-ERROR-01: preserve primary failure, report cleanup failures separately; exact diagnostics remain open. |

These questions distinguish the new direct-file contract from W-205/W-206's historical failure/publication evidence. No direct-write identity mechanism or universal isolation guarantee is inferred from that evidence. See [alias precedents and selected narrower guard](workflow-ownership.md#inputoutput-aliases-q-52-follow-up); the owner subsequently selected the narrower file-identity boundary, without adopting a particular detection mechanism.

## Follow-up questions to ask in small batches

The original broad list contained questions later answered in Q-13 to Q-54. It is superseded by the [decision reconciliation and completeness review](reconciliation.md), which separates selected policies from remaining mechanisms/subsets and prioritizes cross-contract blockers. The chronology above retains the exact question history.

### Engine lifetime questions Q-55 to Q-57 submitted

| ID | Question and recommendation | Status |
| --- | --- | --- |
| Q-55 | Require an explicit borrow/own choice at every engine open, separately from already-selected workflow borrowing? | After requested comparison, owner accepted the revised default borrowing with optional transfer; ENGINE-OWN-01. The original mandatory-argument recommendation is not selected. |
| Q-56 | Document reads see pending edits, which survive editor disposal without an automatic save? | After clarifying no writes before Save, owner accepted visible document-level pending edits retained after editor disposal; ENGINE-PENDING-01. SAVE-DEFER-01 remains the separate persistence boundary. |
| Q-57 | Allow one active editor per document, with caller-coordinated document access? | Accepted ENGINE-EDITOR-01: one active editor and caller coordination. Exact errors/read concurrency/disposal races remain open. |

The initial recommendations followed targeted rereads of W-131/W-132 and current behavior documentation, without fresh execution or a competitor comparison at that checkpoint. Their old tests were not rerun and do not establish the rebuilt target. The subsequent owner response and requested comparison are recorded below. Exact ownership/API/error/disposal/cancellation details not asked remain open. See [opening/saving](../opening-and-saving.md#engine-lifetime-decisions-submitted-for-review) for concrete effects and conditional acceptance scenarios.

### Owner response and comparison follow-up for Q-55 to Q-57

On 2026-10-27, the owner requested ownership expectations from .NET, Linux, other PDF libraries and EF/FFmpeg for Q-55; replied to Q-56, "i dont think we should be writing anything before save, why is this coming up?"; and selected the recommended one-active-editor/caller-coordination answer for Q-57.

The [engine lifetime research](engine-lifetime.md) distinguishes documented owning wrappers, EF's supplied-connection disposal rule, Unix process versus shared-object lifetimes, FFmpeg's custom-I/O boundary and PDF-library-specific APIs. It is primary-document/source review, not executed competitor evidence or a caller survey. At that checkpoint, the revised Q-55 recommendation of default borrowing with optional transfer awaited selection. The subsequent acceptance is recorded below. The initial mandatory-argument recommendation is retained as history rather than described as a universal convention.

SAVE-DEFER-01 recorded the owner's ordinary no-write-before-save direction without then inferring an answer about in-memory getters or disposal retaining/discarding unsaved edits. ENGINE-EDITOR-01 records the selected exclusivity rule with unexecuted scenarios. All subject contracts remain drafts, Q-33 remains explicitly undecided and no runtime or old decision record changes.

### Owner acceptance of revised Q-55 and clarified Q-56

On 2026-10-27, after the revised ownership recommendation and the unsaved-memory explanation, the owner answered "yes to both". This selects ENGINE-OWN-01: borrow supplied input streams/sources by default with optional explicit ownership transfer, and ENGINE-PENDING-01: document getters reflect accepted pending edits retained for a subsequent editor after editor disposal. Disposing the editor does not save. The input bytes remain unchanged before explicit Save under SAVE-DEFER-01.

The selected borrowing default differs from W-132's stream overload; W-131 records the selected pending-state model. Neither historical record is changed, and no current behavior is relabeled as implementing or verifying the new target. Exact failed-open ownership-transfer timing, no-op output, save-failure state, source stability/access, disposal mechanics and errors remain separate decisions.

### Next opening/save questions Q-58 to Q-60 submitted

| ID | Question and recommendation | Status and evidence limits |
| --- | --- | --- |
| Q-58 | Once an explicitly transferred resource is accepted for an opening attempt, own its cleanup after parsing/authentication failure or cancellation, rather than transferring ownership only on success? | Engineering recommendation supported as written; proposed ENGINE-OWN-02. Owner adoption and runtime verification separate. W-132 supplies historical evidence; Q-61 subsequently supports the explicit acceptance point as proposed ENGINE-OWN-03. |
| Q-59 | With no pending edits, ordinary distinct-destination save copies exact input bytes without a new revision; explicit source append writes nothing? | Engineering recommendation supported as written; proposed SAVE-NOOP-01. Owner adoption and runtime verification separate. Current workflow/retired F11 evidence is not a fresh pass; no-op detection/check precedence remain open. |
| Q-60 | After failed/cancelled ordinary separate-destination save, retain pre-save pending edits for caller-directed saving to fresh output when the source remains usable? | Engineering recommendation supported as written; proposed SAVE-PENDING-01. Owner adoption and runtime verification separate. Output restoration, automatic retries, workflow reuse and append/signing failures expressly outside this guarantee. |

See [opening/saving](../opening-and-saving.md#engineering-supported-openingsave-proposals-q-58-to-q-60) for the developed proposals, examples, scope/failure boundaries and unexecuted scenarios. These recommendations do not reopen the selected ownership default, editor-disposal behavior or workflow single-execution rule.

### Engineering support for Q-58 to Q-60 and acceptance-point refinement

On 2026-10-27, the user supported all three recommendations as written, requested an explicit ownership acceptance point, reiterated exact-copy/zero-write no-edit outcomes, and limited failed ordinary separate-output recovery to a usable source. The user expressly stated: "This is my engineering recommendation; owner adoption and runtime verification remain separate." Record that status rather than owner adoption of these clauses or an executed acceptance result.

Proposed ENGINE-OWN-02, SAVE-NOOP-01 and SAVE-PENDING-01 now include scenario targets. No new source/append/signing/output-rollback or workflow-reuse guarantee is inferred. Historical W-132, current workflow behavior and retired F11 remain evidence, not automatic authority.

Q-61 proposed accepting explicit ownership after source-independent argument validation and before cancellation checks or source capability/length/data access. Invalid arguments before acceptance leave ownership with the caller; valid accepted transfer with a pre-cancelled token assigns cleanup to the engine. At that checkpoint the exact ordering awaited a response rather than being inferred from Q-58 support. The subsequent engineering support is recorded below; see [the proposal](../opening-and-saving.md#q-61-proposed-ownership-acceptance-point).

### Engineering support for Q-61: acceptance and responsibility

On 2026-10-27, the user supported Q-61 as an engineering-supported draft refinement, expressly separating owner adoption and runtime verification. The user required source-independent argument validation to query no source capabilities, length or other properties; accepted ownership to retain cleanup responsibility through cancellation/source-access/parsing failure; disposal success and error representation to remain unresolved; and default borrowing to retain caller disposal responsibility.

Proposed ENGINE-OWN-03 now states those boundaries and ENGINE-OWN-E to I supplies unexecuted targets for invalid arguments combined with cancellation, valid pre-cancelled transfer, capability-access failure, parsing failure and corresponding borrowed-input cases. Disposal-failure coverage distinguishes attempting cleanup from successfully disposing. No result is recorded as executed and no current runtime behavior is inferred.

Q-62 proposed preserving an original engine opening/cancellation failure as primary when owned-resource cleanup also fails, reporting the cleanup failure separately. This recommended the workflow rule's priority for engine opening without selecting exact error/API shape or cleanup-only outcomes. W-205 observed a masking restoration exception in signed workflow final copying; that historical result is not an engine failed-open/disposal probe. At this checkpoint the recommendation awaited an answer; the subsequent support is recorded below, with exact diagnostics still open.

### Engineering support for Q-62 and remaining cleanup questions

On 2026-10-27, the user answered "yes" to Q-62, presented as an engineering draft proposal. Proposed ENGINE-ERROR-01 now preserves an established opening/cancellation failure as primary when later owned-resource cleanup fails, reporting cleanup failures separately. Owner adoption and runtime verification remain separate. This does not select exact diagnostics, exception-object identity, cleanup-only outcomes, further cleanup sequencing or post-disposal usability, and does not broaden append/signing/output-restoration guarantees.

Unexecuted ENGINE-ERROR-A/B/C scenarios develop the primary/cleanup distinction for failed and cancelled opening, successful cleanup and borrowed-input wrapper cleanup, respecting Q-61's acceptance boundary. Historical W-205 remains a different workflow-path observation, not new verification.

Q-63 asked whether a cleanup-only failure during ordinary document disposal should be reported rather than swallowed/warning-only; the recommendation was to report it, leaving diagnostics/later usability open. Q-64 asked whether to continue other independently cleanable owned-resource cleanup after one action fails, retaining borrowed resources and not automatically retrying the failed action. Those recommendations awaited answers at this checkpoint; the subsequent engineering support is recorded below. See [opening/saving](../opening-and-saving.md#engineering-supported-cleanup-proposals-q-63-and-q-64).

### Engineering support for Q-63/Q-64, 2026-10-28

The user supported both as engineering draft proposals consistent with the charter's explicit-failure requirement, while keeping owner adoption, diagnostic design and runtime verification separate. Proposed ENGINE-CLEANUP-01 reports cleanup-only failure so unsuccessful disposal cannot be mistaken for successful cleanup. Proposed ENGINE-CLEANUP-02 continues other independently cleanable owned resources after one action fails, retains all observed failures, keeps borrowed inputs open and does not automatically retry the failed action. The user expressly excluded any requirement to proceed with cleanup dependent on that failed action succeeding.

Together with Q-62, the proposed outcome policy preserves an established engine-opening primary failure, exposes cleanup-only failure and avoids abandoning independent cleanup. ENGINE-CLEANUP-A to D provides unexecuted targets, including multiple failures, a dependency-blocked action and independent cleanup still attempted. Exact diagnostics/aggregation, dependency mechanisms, post-disposal usability and manually repeated disposal remain open. No runtime claim, new cleanup framework or blanket workflow policy is inferred.

### Next composition questions Q-65/Q-66 submitted, 2026-10-28

| ID | Recommendation and concrete consequence | Status and evidence limits |
| --- | --- | --- |
| Q-65 | Allow explicit ordinary-workflow declarations for eligible rights removal and hybrid XFA abandonment, executed rights-first/XFA-second before ordinary staged edits in the same session without intermediate Save. | Engineering-supported proposed WORKFLOW-PREPARE-01; owner adoption/runtime verification separate. Rights failure stops before abandonment; abandonment failure retains prior successful rights removal pending under per-call guarantees. Q-33 remains undecided. |
| Q-66 | Merge overlapping named flatten requests; all-fields supersedes named requests, including not separately validating a discarded missing-name request. | Engineering-supported proposed WORKFLOW-FLATTEN-01, explicitly including all-fields plus a misspelled name succeeding if otherwise eligible/successful. Validate effective all-fields only; field identity, eligibility, appearances and atomicity remain separate. |

See [workflows](../workflows.md#engineering-supported-composition-proposals-q-65-and-q-66) for the developed proposed clauses and unexecuted scenarios. Q-66's acceptance of a discarded bad name under all-fields was explicitly endorsed rather than inferred from effective-fill validation. Detailed cleanup diagnostic design/lifecycle questions remain visible while independent composition drafting proceeds.

### Engineering support for Q-65/Q-66, 2026-10-28

The user supported both proposals, including Q-66's validation consequence, as engineering draft proposals without owner adoption or runtime verification. WORKFLOW-PREPARE-01 preserves explicit consent, authority, eligibility, rights-first/XFA-second then ordinary staged edits, one session and no intermediate Save. Rights-removal failure stops before abandonment; failed abandonment retains earlier successful rights removal pending under per-call guarantees. Signed-document rights removal remains undecided. The internal session's pending-state rule does not permit workflow reuse or promise a public intermediate-document API.

WORKFLOW-FLATTEN-01 merges named selections and flattens each selected field once. All-fields supersedes named selections, so the effective all-fields operation is validated while discarded names are not separately checked. The user expressly accepted flatten-all plus a misspelled name succeeding if the all-fields operation is otherwise eligible. Field identity, eligibility, appearances, named-only errors and engine atomicity remain separate. Historical W-198/W-199/W-204 studies support feasibility only, were not rerun and do not adopt or verify these clauses. Added WORKFLOW-PREPARE-A/B/C and WORKFLOW-FLATTEN-A/B/C as unexecuted targets.

### Next engine-form questions Q-67 to Q-69 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-67 | Each flattening call is atomic for pending state, retaining earlier successful edits when that call fails. | Engineering-supported proposed FORM-FLATTEN-01, including fields/widgets/affected-page restoration and required post-mutation failure verification. Owner adoption/runtime verification separate; pre-mutation refusals alone are insufficient. |
| Q-68 | Refuse individual/bulk population or named-only flatten when an effective supplied field name is absent, instead of silently skipping it. | Engineering-supported proposed FORM-TARGET-01, after workflow normalization/eventual identity resolution. Discarded all-fields names remain unchecked. Identity/ambiguity/eligibility details and exact errors remain separate. |
| Q-69 | Reject literally identical supplied names repeated within one direct engine bulk-fill call, even with equal values. | Engineering-supported proposed FORM-BULK-02; equal-value repeats also refuse. Workflow last-value-wins/separate calls remain distinct. Q-70 to Q-72 subsequently support exact default named targeting, with decoding/hierarchy/reporting still open. |

See [forms](../forms.md#engineering-supported-engine-form-proposals-q-67-to-q-69) for developed clauses and unexecuted scenarios. No historical API/value/appearance subset or runtime result is adopted by these proposals.

### Engineering support for Q-67 to Q-69, 2026-10-28

The user endorsed all three as engineering draft proposals without owner adoption/runtime verification. FORM-FLATTEN-01 restores pre-call pending fields, widgets and affected page structures after failure, preserving earlier successful edits; verification must reach failure after mutation begins. FORM-TARGET-01 refuses absent effective population/named-flatten targets after normalization and eventual identity resolution, leaving Q-66's discarded all-fields names unchecked. FORM-BULK-02 rejects literally identical names within one direct engine bulk call even when values match, without changing workflow overrides or separate individual calls. At that checkpoint, case/aliases/normalization awaited the later Q-70 to Q-72 review.

The user described the coherent boundary as workflow normalization, effective engine-request validation, then the applicable per-call atomicity; this does not establish transactions across separate calls or output rollback. Added FORM-FLATTEN-A/B/C, FORM-TARGET-A/B and FORM-BULK-A/B as unexecuted targets, keeping mid-processing failure distinct from pre-mutation refusal. Historical W-77/W-68/W-204 evidence was not rerun and is not target verification.

### Next field-identity questions Q-70 to Q-72 submitted, 2026-10-28

| ID | Recommendation and consequence | Status and evidence limits |
| --- | --- | --- |
| Q-70 | Default named mutation uses exact fully qualified exposed names, case-sensitively, without case folding, Unicode normalization or partial/short-name fallback. | Engineering-supported proposed FORM-NAME-01 after workflow normalization. Owner adoption/runtime correctness separate; decoding/malformed-name representation/reporting remain open. |
| Q-71 | Refuse named mutation when one exposed full name identifies distinct logical fields; multiple widgets of one field are not that ambiguity. | Engineering-supported proposed FORM-NAME-02. Multiple widgets of one logical field remain valid subject to other operation rules. Inspection ambiguity representation/disambiguation API stay separate. |
| Q-72 | Named population/flattening targets exact terminal fields; group names do not implicitly select descendants. | Engineering-supported proposed FORM-NAME-03. Descendant security restrictions remain independent; all-fields discarded names remain unchecked. Hierarchy decoding/classification/error precedence stay open. |

See [forms](../forms.md#engineering-supported-field-targeting-q-70-to-q-72) for the developed clauses and unexecuted scenarios. No new probes or general field-identity repair/normalization API is introduced.

### Engineering support for Q-70 to Q-72, 2026-10-28

The user supported exact fully qualified exposed-name matching without case folding/Unicode normalization/short-name fallback (FORM-NAME-01), refusal of named ambiguity between distinct logical fields while permitting one multi-widget logical field (FORM-NAME-02), and explicit terminal named targets without implicit descendant selection (FORM-NAME-03). Apply these after workflow normalization while preserving discarded all-fields names and applicable per-call atomicity. Descendant security restrictions apply independently.

The user expressly left name decoding, malformed hierarchies, ambiguity reporting and error precedence open, and separated engineering support from adoption/runtime correctness. Added FORM-NAME-A/B/C/D as unexecuted targets and reconciled inspection/workflow/shared references without creating blanket read refusal, an alias/repair API or new security exceptions. Historical full-name/ordinal matching and field-versus-widget evidence were not rerun and do not verify the new target.

### Next form-value/export questions Q-73 to Q-75 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-73 | Use semantic field-family value kinds: text strings, checkbox Booleans, radio state strings and choice option strings/collections; refuse wrong kinds instead of cross-type coercion. | Submitted; no answer. Cardinality/option matching, public type/API, decoding and detailed value rules remain separate. W-68 supplies historical model/evidence only. |
| Q-74 | Empty text/false provide text/checkbox blank/off values; semantic no-value clears radio/choice selection, as does an empty choice collection. No generic no-value-to-text/Boolean conversion. | Submitted; no answer. Null/API spelling and exact PDF storage remain open. Historical W-96 confirmed selection clearing; that does not automatically adopt this rebuilt rule. |
| Q-75 | Ordinary export emits one value per supported logical terminal field, includes read-only ordinary fields and excludes signature/push-button/password fields. | Submitted; no answer. Collection shape, encoding/ordering/unset representation, ambiguity/malformed-input reporting and full round-trip promises remain separate. W-68/W-96 history is not new verification or automatic adoption. |

See [forms](../forms.md#engineering-supported-values-and-export-q-73-to-q-75) for examples, alternatives/tradeoffs and conditional unexecuted scenarios. Historical W-96's public value-type choice is expressly not adopted by asking about semantic values.


### Q-73 to Q-75 engineering support recorded, 2026-10-28

The user supported all three semantic proposals, expressly separating engineering endorsement from whole-contract adoption and runtime verification. Developed [FORM-VALUE-01 / FORM-CLEAR-01 / FORM-EXPORT-01](../forms.md#engineering-supported-values-and-export-q-73-to-q-75): strict field-family kinds; semantic no-value selection clearing distinct from empty text and false; ordinary export per supported logical terminal field, including read-only ordinary data and excluding signatures/push buttons/password fields. Export inclusion grants no write permission or universal refill guarantee. No historical public value type, null convention or dictionary shape is adopted.

Added unexecuted FORM-VALUE-A/B, FORM-CLEAR-A/B and FORM-EXPORT-A/B, reconciling effective workflow validation, pending-state/no-save boundaries and inspection/export representation gaps. Option matching/single-multi cardinality, encoding, unset-value representation, malformed/ambiguous reporting and error precedence remain expressly open. Clearing's semantic input does not choose unset export encoding. W-68/W-96 retain historical/compatibility significance; no probes were rerun. This supersedes the preceding submitted/no-answer statuses as the live state; reconciliation owns the residual queue.

### Next choice-value questions Q-76 and Q-77 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-76 | Match listed choices by exact exposed option value, case-sensitively without trimming, Unicode normalization or display-label fallback; distinguish an exposed empty-string value from semantic no value. | Submitted; no answer. W-68 records unlisted-option refusal, not comprehensive evidence for these exact matching rules. Editable unlisted text and duplicate option values remain separate. |
| Q-77 | Allow scalar/one-element collection for single-select and scalar/collection for multi-select; refuse more than one supplied value on single-select. Keep already supported no-value/empty-collection clearing. | Submitted; no answer. W-68 records similar shapes/cardinality; this is compatibility/feasibility context, not automatic adoption. Multi-select duplicate-selection and list-order rules remain separate. |

See [forms](../forms.md#engineering-supported-choice-matching-and-cardinality-q-76-and-q-77) for examples, alternatives/tradeoffs and conditional unexecuted targets. No established-tool comparison or new runtime test is asserted, and exact field-name policy is not silently extended to option matching.


### Q-76 and Q-77 engineering support recorded, 2026-10-28

The user supported both as engineering draft proposals, separating adoption and runtime verification. Developed [FORM-CHOICE-01/02](../forms.md#engineering-supported-choice-matching-and-cardinality-q-76-and-q-77): exact, case-sensitive exposed listed option values without trimming/Unicode normalization/label fallback; an explicitly listed empty string remains a selection distinct from clearing; scalar/collection inputs retain field-specific cardinality. Every single-select collection with more than one element refuses, including equal repeated values; no deduplication first to evade that check. Semantic no-value/empty-collection clearing stays governed by FORM-CLEAR-01.

Added unexecuted FORM-CHOICE-A/B/C/D and reconciled effective workflow validation, inspection/export encoding boundaries and the live completeness map. The user expressly left editable-choice free text, duplicate option values, multi-select duplicates and ordering unresolved. Radio matching, decoding, malformed/ambiguous reporting, exact errors and remaining limits/cancellation stay open. No acceptance result or historical API is adopted. W-68's historical scalar/list and refusal checks were not rerun; this entry supersedes the preceding submitted/no-answer statuses as the live state.

### Next choice-duplicate questions Q-78 and Q-79 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-78 | Refuse value-based selection when a requested exact value matches multiple distinct option entries; do not choose the first or implicitly select all. Do not refuse an otherwise eligible unique selection merely because another unselected option value is duplicated. | Submitted; no answer. No blanket open/read refusal, index/disambiguation API, repair, clearing-eligibility or export ambiguity policy is supplied. W-68's summary does not establish this outcome. |
| Q-79 | Reject repeated exact value strings in one effective multi-select collection, even equal repeats, rather than deduplicating. | Submitted; no answer. Field-name bulk duplicates/workflow last assignment remain separate; single-select multi-element refusal is already supported. Ordering and overlapping-error precedence remain open. |

See [forms](../forms.md#engineering-supported-choice-duplicates-q-78-and-q-79) for examples, alternatives/tradeoffs and conditional unexecuted targets. No runtime probes or established-tool comparisons were performed, and neither recommendation was inferred from historical implementation.


### Q-78 and Q-79 engineering support recorded, 2026-10-28

The user supported both as engineering draft proposals, separating owner adoption and runtime verification. Developed [FORM-CHOICE-03/04](../forms.md#engineering-supported-choice-duplicates-q-78-and-q-79): refuse selection when a requested exact value identifies multiple distinct option entries, without allowing unrelated unselected duplicates to block an otherwise eligible unique selection; reject repeated exact values within an effective multi-select collection rather than deduplicating before validation. Apply both after workflow normalization with applicable bulk atomicity; direct bulk duplicate names, separate calls and workflow last assignments remain distinct.

Added unexecuted FORM-CHOICE-E/F/G/H and reconciled earlier open-choice wording, workflow/shared/inspection/index references and the live completeness map. Ordering, editable-choice free text, inspection/export reporting and error precedence remain expressly unresolved. No blanket open/read refusal, general repair/index API, cross-call/output rollback, owner adoption or runtime result is supplied. This supersedes the preceding submitted/no-answer statuses as the live state.

### Next choice-order/editability questions Q-80 and Q-81 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-80 | For supported unambiguous multi-selects, caller order has no selection meaning; expose values in existing document option-list order after fills and ordinary successful reads/export, without reordering options. | Submitted; no answer. This is an explicit proposed API semantic choice, not mandated by ascending stored PDF selection indices. Export field-entry order and malformed/inconsistent reporting remain separate. |
| Q-81 | For otherwise supported Combo+Edit fields, accept unlisted strings verbatim without changing options, including unlisted empty string as literal blank text distinct from clearing. Preserve exact listed matching/ambiguity refusal, cardinality, authority and appearances. | Submitted; no answer. Do not evade an ambiguous listed value as free text. Invalid flags, detailed stored/read representation and resource rules remain open. Format editability and current code inform feasibility, not automatic target adoption. |

Primary research: ISO 32000-1:2008 12.7.4.4 Tables 230/231 and ISO 32000-2:2020 12.7.5.4 Tables 233/234, read from local pdfdocs primary copies through generated clause text. Those distinguish combo/editable flags, values/labels and ascending selected indices; duplicate export values have indexed representations, so the supported value-only ambiguity refusal does not classify the entire document as malformed. They do not select our public list order or every free-text/empty-input outcome. The remote Adobe PDF fetch exceeded tool size limits; the [PDF Association source page](https://pdfa.org/resource/iso-32000-1/) identifies that public edition. Local references were not edited or redistributed; no large source passages copied.

Static evidence: [PdfFormFiller](../../../src/jaytwo.Pdf/Internal/PdfFormFiller.cs), inspected 2026-10-28, writes supplied multi-select items in enumeration order/removes I in ApplyChoice, and skips listed-option checks for marked Editable fields in RequireOption. These are compatibility considerations, not runtime/interop/rendering verification, proof of complete valid-flag handling or a new confirmed defect. Historical W-68 is additional context only; no probes/tests were rerun. See [forms](../forms.md#engineering-supported-choice-ordering-and-editable-text-q-80-and-q-81) for examples, alternatives, research limits and conditional unexecuted targets.


### Q-80 and Q-81 engineering support recorded, 2026-10-28

The user supported both as engineering draft proposals, expressly separating adoption, compatibility assessment and runtime/interoperability verification. Developed [FORM-CHOICE-05/06](../forms.md#engineering-supported-choice-ordering-and-editable-text-q-80-and-q-81): report supported unambiguous multi-selections in existing option-list order after fills/ordinary reads/export while preserving options; accept eligible Combo+Edit unlisted strings verbatim, including empty literal text distinct from clearing. Exact listed matches resolve first and ambiguous matches refuse without a free-text bypass. A label-only/case-distinct string can be retained as literal text in an eligible editable combo, not silently select a different listed value.

Added unexecuted FORM-CHOICE-I/J/K/L and reconciled workflow normalization, duplicate validation before canonical reporting, applicable bulk atomicity and read/persistence boundaries across subjects. Malformed-selection reporting, export field-entry ordering, exact storage, encoding/unset/blank representation and error precedence remain open. The user identified public canonical ordering as an engineering policy choice, with format support for editable combos; format/static/historical context supplies no compatibility clearance or executed result. No probes/tests were rerun. This supersedes preceding submitted/no-answer statuses as the live state.

### Next button-value questions Q-82 and Q-83 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-82 | Once supported radio on-states are exposed, match exact case-sensitive names without trim/Unicode/label/Opt-export fallback or unknown-state synthesis; reserve Off as off, using semantic no value to clear. | Submitted; no answer. State discovery/decoding, button Opt mapping, missing appearances/state generation, repeated on-states/radio flags and reporting remain separate. Q-73/Q-74 value kinds/semantic clearing are not reopened. |
| Q-83 | For otherwise supported checkbox widgets with established on-states, refuse true when names differ; do not refuse false solely for that difference, subject to other eligibility/appearance rules. | Submitted; no answer. Missing/multiple/undecodable states, generation and repair remain separate. Logical multi-widget fields are not distinct-field ambiguity. Apply eventual workflow normalization/applicable bulk atomicity without choosing diagnostics or error precedence. |

Primary context: ISO 32000-1:2008 12.7.4.2.3/4 and Table 227, and ISO 32000-2:2020 12.7.5.2.3/4 and Table 230, read from local pdfdocs button-field clauses (PDF 1.7 pages 447-450; PDF 2.0 pages 550-554). Button appearance states, optional widget export strings and radio flags are distinct mechanisms; those clauses do not select all library aliases/eligibility rules. Public remote PDF fetch exceeded tool size limits; local source copies remain unchanged, with no redistribution or large quotations.

Historical W-68 records radio on-state/unknown-state handling, checkbox differing-state validation and no button Opt export mapping. Static PdfFormFiller inspection on 2026-10-28 found exact radio string checks when states are known, true-only differing-checkbox-state refusal and separate missing-appearance paths. These are compatibility/fixture candidates, not executed evidence for reserved Off, all decoding/flags/subsets or the target contract. See [forms](../forms.md#engineering-supported-button-matching-and-checkbox-eligibility-q-82-and-q-83) for examples, alternatives, research limits and conditional unexecuted targets. No runtime/interoperability tests or comparator probes were performed.


### Q-82 and Q-83 engineering support recorded, 2026-10-28

The user supported both as engineering draft proposals without adoption/runtime verification and requested subsequent questions in batches of five. Developed [FORM-BUTTON-01/02](../forms.md#engineering-supported-button-matching-and-checkbox-eligibility-q-82-and-q-83): exact case-sensitive established exposed radio on-states without aliases/unknown-state synthesis, reserved Off and semantic no-value clearing; true refusal for differing established checkbox on-state names while otherwise eligible false can turn all widgets off. Apply after workflow normalization with applicable bulk atomicity. Added unexecuted FORM-BUTTON-A/B/C/D; individual-fill atomicity remains separately proposed, with state discovery, Opt, missing appearances/generation, radio flags, reporting/storage and error precedence open. Primary button references distinguish states/export strings/flags but do not select the API policies. No probes/tests were run. This supersedes preceding submitted/no-answer statuses as the live state.

### Next population questions Q-84 to Q-88 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-84 | Atomic individual fills, including affected widgets/appearances, preserve pre-call state and earlier edits after observed failure/cancellation. | Submitted; no answer. W-68 bulk restoration and static one-entry bulk routing suggest feasibility, not proof of individual post-mutation/cancellation coverage. No cross-call/output rollback. |
| Q-85 | Refuse ReadOnly ordinary fill targets, including equal-value or clear inputs; ordinary export inclusion is not write authority. | Submitted; no answer. Primary ReadOnly description concerns UI behavior; this is an explicit programmatic eligibility proposal. Exact API/errors and other security gates remain separate. |
| Q-86 | Refuse ordinary filling of password-flagged text fields, including empty/clear-like inputs. | Submitted; no answer. Distinct from opening encrypted PDFs and existing export exclusion. Primary password note discourages storing secret values; no ephemeral field API/flag removal proposed. |
| Q-87 | For valid applicable MaxLen, count Unicode scalar values without normalization and refuse excess instead of truncating. | Submitted; no answer. Static code uses String.Length/UTF-16 units, so compatibility assessment is required separately. Invalid/inherited limits, malformed Unicode and appearances remain open. |
| Q-88 | Accept otherwise eligible script-bearing ordinary fills, preserve definitions/calculation order, do not execute scripts/recompute dependent fields or claim script validation/formatting equivalence. | Submitted; no answer. Script non-execution derives from supported.md; accepting/preserving action-bearing inputs is separately proposed. Unsupported appearance/structure/security checks remain in force. |

Developed scope-derived FORM-SCRIPT-BOUNDARY-01 separately from Q-88, following supported.md's JavaScript authoring/editing/execution exclusion. Added conditional unexecuted targets and alternatives/tradeoffs in [forms](../forms.md#engineering-supported-fill-recovery-and-eligibility-q-84-to-q-88). No proposal above is treated as answered by history or static code.

Primary local research: ISO 32000-1:2008 field dictionaries/ReadOnly (12.7.3 Table 221), text fields (12.7.4.3 Tables 228/229) and field actions (12.6.3 Table 196), with corresponding ISO 32000-2 entries for text flags/MaxLen, field actions and calculation order. MaxLen's character wording does not itself select the proposed .NET unit; Microsoft [String.Length](https://learn.microsoft.com/en-us/dotnet/api/system.string.length) documents Char counting rather than Unicode characters. The public Adobe ISO source was found through primary-source search; unrelated search hits were not used as tool comparisons. Local primary copies remain unchanged. The specified entries inform the questions, not blanket adoption of the standards' interactive behavior.

Historical W-68 records bulk backup/restoration and ReadOnly/password/MaxLen guards, with earlier tests not rerun. Static inspection on 2026-10-28 found PdfAcroFormEditor single fill routes to a one-entry engine bulk call; PdfFormFiller uses backup/restoration, ReadOnly/Password guards and Text.Length versus MaxLength. These are implementation/compatibility observations, not executed recovery/Unicode/action-preservation evidence or a completed compatibility assessment. No code changed or runtime/interoperability probe ran; acceptance/qualification records remain untouched.


### Q-84 to Q-88 engineering support recorded, 2026-10-28

The user supported all five recommended boundaries as engineering draft proposals, explicitly withholding adoption and runtime changes. Developed [FORM-SINGLE-01 / FORM-READONLY-01 / FORM-PASSWORD-01 / FORM-TEXT-01 / FORM-SCRIPT-01](../forms.md#engineering-supported-fill-recovery-and-eligibility-q-84-to-q-88). Individual-fill failure restores pre-call pending state including post-mutation widget/appearance/resource effects while retaining earlier edits; validation-only refusal does not prove that restoration. ReadOnly targets refuse equal/changed/clear fills; password text fields refuse ordinary fill including empty attempts. Valid MaxLen uses scalar values without normalization/truncation. Otherwise eligible script-bearing fills preserve actions/definitions/calculation order without execution, script-validation/formatting claims or dependent-field recalculation.

Added unexecuted FORM-SINGLE-A/B, FORM-READONLY-A/B, FORM-PASSWORD-A/B, FORM-TEXT-A/B/C and FORM-SCRIPT-A/B/C, with explicit failure injection after mutation, earlier-edit retention and script preservation oracles. Reconciled shared/workflow/inspection/security/index/completeness and earlier open-choice wording. No code, behavior, acceptance or qualification record changed; historical tests and prior static observations are not fresh verification. This supersedes preceding submitted/no-answer statuses as the live state.

Q-87 compatibility assessment performed as bounded static reasoning: current PlanFill uses Text.Length; for well-formed UTF-16, units = scalars + supplementary-scalar count. With a fixed valid limit, the proposed scalar guard can admit supplementary text previously rejected by that guard and cannot newly reject a previously length-accepted well-formed string on that guard alone. BMP/combining-sequence counts are unchanged without normalization. The forms matrix contains derived guard comparisons, not executed engine outcomes. Inspected ASCII MaxLen examples in AcroFormTests/FormDataTests do not demonstrate the changed boundary; this is not a whole-suite coverage claim and neither test was run. Malformed UTF-16, appearance/Comb/font behavior, representative-reader/producer compatibility, diagnostics/docs and final API compatibility remain to be assessed before implementation/adoption is complete. Microsoft [Unicode/.NET documentation](https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding-introduction) supplies primary encoding context. No fresh runtime, rendering or interoperability result is asserted.

### Next form-text questions Q-89 to Q-93 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-89 | Refuse unpaired UTF-16 surrogates in supplied text-field values, without replacement/dropping. | Submitted; no answer. Existing-PDF decoding/inspection, other string parameters and exact error timing/precedence remain separate. |
| Q-90 | Refuse CR/LF in single-line text; preserve CR/LF/CRLF verbatim in otherwise supported Multiline values, counting actual scalars. | Submitted; no answer. Visual layout, other Unicode separators/controls and malformed flags remain open; no normalization or space substitution inferred. |
| Q-91 | Refuse ordinary plain filling while RichText is set, including empty text, pending a rich preservation/loss contract. | Submitted; no answer. No implicit V-only/RV-stale update, format stripping, rich editor or blanket read/export refusal selected. Compatibility implications require assessment. |
| Q-92 | Allow otherwise eligible family-appropriate Required clearing while preserving the flag; do not treat ordinary editing/save/value export as submit-form validation. | Submitted; no answer. Other flags/permission/appearance rules still apply. No submission/global validation feature added. |
| Q-93 | Allow otherwise eligible FileSelect text as literal path data, preserving the flag without named-file access/upload/attachment behavior. | Submitted; no answer. MaxLen/appearance/security and malformed flag rules remain separate; no file-picker/submission capability added. |

See [forms](../forms.md#engineering-supported-text-and-field-variants-q-89-to-q-93) for examples, alternatives/tradeoffs and conditional unexecuted scenarios. Primary ISO field/text/RV clauses distinguish the flags and submit-action Required semantics; Microsoft distinguishes ill-formed surrogate units from scalar sequences. Static ApplyText's simple path excludes RichText/FileSelect while writing V; that is a compatibility consideration, not proof of complete behavior/interop or a new confirmed defect. Existing test declarations inspected for context were not executed. Prior button discovery/Opt/flags, missing appearances and common layout/geometry remain explicitly queued rather than assumed.


### Q-89 to Q-93 engineering support recorded, 2026-10-28

The user supported all five with eligibility/unexecuted status intact. Developed [FORM-TEXT-02/03 / FORM-RICH-01 / FORM-REQUIRED-01 / FORM-FILESELECT-01](../forms.md#engineering-supported-text-and-field-variants-q-89-to-q-93): refuse unpaired supplied UTF-16 without replacement/deletion; refuse single-line CR/LF, preserve eligible Multiline value characters and count CRLF as two scalars; refuse ordinary RichText plain fills including empty text while leaving semantic clearing separate; allow otherwise eligible Required clearing with the flag preserved and no submit-readiness claim; allow literal FileSelect paths without file access/upload/attachment. The user expressly clarified character preservation does not promise encoded PDF byte identity. Adoption/runtime/rendering/interoperability remain separate.

Added unexecuted FORM-TEXT-D/E/F/G, FORM-RICH-A/B, FORM-REQUIRED-A/B and FORM-FILESELECT-A/B. Reconciled workflow/shared/inspection/index/completeness and the static Q-87 compatibility discussion: malformed supplied text now has a supported refusal proposal, but full encoder/compatibility outcomes are unverified; the well-formed guard comparison does not cover this added validity rule. No static/historical observation is reported as a fresh test result. This supersedes preceding submitted/no-answer statuses as the live state.

### Next rich-clear and button questions Q-94 to Q-98 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-94 | Support explicit paired effective current V/RV rich clearing atomically without changing other fields, preserving RichText/styles/defaults/actions/calculation order and requiring eligible blank-widget outcomes. | Submitted; no answer. Distinct from refused empty plain fill and ordinary text no-value input. Exact API/workflow integration, appearance/storage/unset representation remain open; no format stripping/history erasure or general rich editor proposed. |
| Q-95 | Discover declared existing on-state names from non-Off AP/N dictionary keys, not V/DV/AS/Opt fallback. | Submitted; no answer. Appearance usability, missing/malformed dictionaries, multiple keys per widget and generation remain separate. Static OnState first-key behavior supplies feasibility/compatibility context only. |
| Q-96 | Honor RadiosInUnison for shared states; without it refuse requested multi-widget state ambiguity locally, leaving unique alternatives eligible. | Submitted; no answer. Clearing/NoToggle, malformed flags/appearances and Opt handling remain separate. Not inferred from named-field ambiguity policy or a runtime probe. |
| Q-97 | Refuse semantic radio clear with NoToggleToOff set, preserving the flag and prior state. | Submitted; no answer. Recommendation deliberately differs from W-68's viewer-click interpretation/permissive clear. Format combines at-all-times and click wording with Off default; programmatic API policy/compatibility requires explicit review, not a claimed settled competitor result. |
| Q-98 | Expose/preserve well-formed per-widget button Opt export-text association separately from actual on-state names; keep already supported default state-name input. | Submitted; no answer. No export-text alias/index API; malformed mapping/decoding, exact API/export representation and appearance eligibility remain open. |

See [forms](../forms.md#engineering-supported-rich-clearing-and-button-structure-q-94-to-q-98) for examples, alternatives/tradeoffs, primary context and conditional unexecuted targets. Local primary ISO V/RV/style/button/Opt/flag clauses were consulted; V is inheritable, so terminal-key deletion cannot be assumed to implement effective clearing without re-exposing inherited values. Exact storage remains open. Static PdfFieldTree.OnState inspected 2026-10-28 returns the first non-Off AP/N key; it does not prove a full multi-state/discovery/appearance contract. W-68 explicitly records no Opt export mapping and clear despite NoToggleToOff; these are historical compatibility observations, not requirements or fresh verification.

Established-tool comparison is version-bound documentation only: official [Apache PDFBox 2.0.12 PDButton](https://pdfbox.apache.org/docs/2.0.12/javadocs/org/apache/pdfbox/pdmodel/interactive/form/PDButton.html) and [PDRadioButton](https://pdfbox.apache.org/docs/2.0.12/javadocs/org/apache/pdfbox/pdmodel/interactive/form/PDRadioButton.html) distinguish on-values/export values and document unison/selected exports. No current-version behavior, implementation inference or executed result is claimed. PDFBox 3.0.0 source fetch attempts were unavailable; they were not used as evidence for clearing behavior. These docs do not establish enforcement of NoToggleToOff by programmatic clear. No repository/external runtime or interoperability probe ran, and Q-97's recommendation remains explicitly a policy/compatibility decision.


### Q-94 to Q-98 engineering support recorded, 2026-10-28

The user supported all five as written without contract adoption/runtime verification and expressly required compatibility review of Q-97 before adoption. Developed [FORM-RICH-02 / FORM-BUTTON-03 to 06](../forms.md#engineering-supported-rich-clearing-and-button-structure-q-94-to-q-98): atomic explicit paired effective V/RV clearing preserving other fields/non-value styles/defaults/actions/order; AP/N declared state names without recorded-value/export fallback; unison-aware shared-state selection with local non-unison ambiguity refusal; NoToggle clear refusal; and separate well-formed per-widget Opt associations preserved through fills. Inheritance-safe rich clearing and correct blank widgets remain required outcomes with exact value/storage/appearance rules open. Ordinary empty plain RichText fill remains refused and no workflow clear API/phase is inferred.

Added unexecuted FORM-RICH-C/D and FORM-BUTTON-E to L; reconciled forms/shared/workflow/inspection/index/completeness. State declaration remains distinct from appearance usability, state names from Opt text, policy support from adoption and historical/static context from runtime/interoperability evidence. No code or acceptance/qualification record changed. This supersedes preceding submitted/no-answer statuses as the live state.

Q-97 compatibility review status: bounded static assessment completed; adoption compatibility disposition remains open. W-68 line 83 allows clear despite the flag. Current PlanFill None and ApplyRadio null paths inspected 2026-10-28 reach Off assignment without a NoToggle guard in those reviewed paths, under other eligibility gates. The proposed flag restriction changes clear acceptance, including an already-Off request, while not refusing valid selection solely for this reason. The forms matrix is historical/static reasoning, not executed outcomes or a complete gate/no-op audit. Before adoption require representative template/caller review, diagnostic/migration documentation, framework and independent-reader evidence and recorded disposition. The primary at-all-times/click wording and default Off do not conclusively settle programmatic API policy. Official PDFBox 2.0.12 docs distinguish surfaces but establish no clear-enforcement result; no comparator was run.

### Next rich-clear and appearance questions Q-99 to Q-103 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-99 | Use local blank V and consistent valid text-empty RV overrides for effective rich clear, preserving styles/shared ancestors/siblings/defaults/flag rather than exposing inherited current content. | Submitted; no answer. Exact XML/style subset, encoding and API remain open; no style stripping or general rich editor selected. |
| Q-100 | Refuse checkbox true for multiple non-Off keys within one widget, leaving otherwise eligible false possible. | Submitted; no answer. Distinct from across-widget state mismatch; radio multiple-key shapes/malformed appearance reporting remain separate. |
| Q-101 | Support limited generated basic Yes/Off checkbox appearances only for the defined blank/no-Opt/no-conflicting-value-default subset with eligible geometry/style; do not rewrite existing states or invent radio states. | Submitted; no answer. Exact appearance/decorative/geometry subset and limits remain open; current generator is feasibility context only. |
| Q-102 | Refuse an otherwise requested fill/explicit clear when a correct changed-widget appearance cannot be provided, restoring the call instead of deferring to a viewer. | Submitted; no answer. Deliberate difference from historical NeedsViewer acceptance; compatibility assessment required. Charset/font/shaping/layout and future explicit deferred mode remain separate. |
| Q-103 | Permit missing-appearance omission only for a supported provably blank/off no-visible-paint widget; otherwise refuse rather than lose background/border/mark. | Submitted; no answer. Exact blankness/default-style/appearance, optional content/print and geometry rules remain open. W-77 historical omissions are not automatically adopted. |

Primary local field/RV/XML/style and button appearance clauses consulted. W-77 records common empty/off missing appearances and dropped viewer-generated MK decorations in its omission path; those corpus observations were not rerun. Static PdfFormAppearances Yes/Off generation and PdfFormFiller V-write/AP-removal/NeedsViewer paths inspected 2026-10-28 are feasibility/compatibility evidence, not rendered/independent-tool results or newly confirmed defects. See [forms](../forms.md#engineering-supported-rich-overrides-and-appearance-eligibility-q-99-to-q-103) for examples, alternatives, limits and conditional unexecuted targets. No runtime/external-tool/interop probe ran or behavior record changed.


### Q-99 to Q-103 engineering support recorded, 2026-10-28

The user supported all five boundaries as engineering draft proposals, without adoption/runtime changes or executed scenario claims, and expressly required Q-102 compatibility review before adoption separately from Q-97. Developed [FORM-RICH-03 / FORM-BUTTON-07 / FORM-APPEARANCE-01/02 / FORM-FLATTEN-02](../forms.md#engineering-supported-rich-overrides-and-appearance-eligibility-q-99-to-q-103): target-local empty V/valid blank rich override with styles/defaults/ancestors/siblings retained; true refusal for multiple on-keys within a checkbox widget with otherwise eligible false; Yes/Off generation only for the defined blank/no-Opt/supported-geometry-decoration case; correct changed-widget appearance or per-call refusal/restoration; and flatten-time missing paint omission only when supported rules establish no visible paint loss.

Added unexecuted FORM-RICH-E/F, FORM-BUTTON-M/N, FORM-APPEARANCE-A/B/C/D/E and FORM-FLATTEN-D/E; reconciled forms/shared/workflow/inspection/index/completeness and earlier open-detail wording. Unknown/malformed/partial states are not automatically blank, changed-widget readiness is not a global repair/read policy, and omission of painting does not silently skip requested flatten processing. Exact XML/style/blank construction, layout/geometry/visibility/print/global-readiness/storage/reporting and resource/cancellation/diagnostics remain open. No implementation or evidence records changed. This supersedes preceding submitted/no-answer statuses as the live state.

Q-102 compatibility review status: bounded static assessment completed; adoption compatibility disposition remains open independently of Q-97. Reviewed ApplyText non-simple path writes V/removes AP/sets NeedAppearances/returns NeedsViewer. Proposed changed-widget appearance refusal changes that value-only/deferred acceptance when the new renderer still cannot provide a correct appearance; eventual supported TrueType/font/layout coverage may instead make some old deferred cases succeed. Do not conflate Q-91's independent RichText refusal or untouched pre-existing appearances with Q-102. Failure may move from later flatten/sign behavior recorded historically to the fill/clear call. The forms matrix is static/historical reasoning, not executed outcomes. Before adoption require representative caller/font/template/framework/failure-state/diagnostic/migration and independent-reader/rendering evidence with explicit reviewed disposition; no deferred mode or automatic migration API selected. Q-97 remains a separate open review.

### Next font-input/layout questions Q-104 to Q-108 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-104 | Borrow caller fonts; consume/cache needed data within the engine operation so later Save does not reread caller resources; workflow reads remain deferred until execution. | Submitted; no answer. Open on success/failure/cancellation; exact public source types, positions/stability/size/cache limits and cleanup diagnostics remain open. No ownership-transfer API or unlimited buffering selected. |
| Q-105 | Permit glyph-free/font-metric-independent eligible blank text/choice/rich clears without supplied font data, preserving supported decoration and Q-102 readiness. | Submitted; no answer. No default font/ignored decoration; invalid unused fonts, glyph-bearing labels/styles/captions and broader fontless fill behavior remain separate. |
| Q-106 | Initial operation subset: one static sfnt TrueType-outline face (glyf/loca), including OpenType with those outlines; reject collections, variable and CFF/CFF2. | Submitted; no answer. Identify actual structure; exact tables/cmap/composites/color variants/embedding permissions/limits remain open. Later extensions are not a charter scope exclusion. |
| Q-107 | Initial documented direct LTR scalar-to-glyph mapping; refuse missing glyphs or required reordering/contextual/joining/mark-positioning rather than approximate. | Submitted; no answer. Exact repertoire/detection required before adoption; no language list, normalized values, full shaping or runtime capability claimed. Kerning/ligatures/vertical layout remain separate. |
| Q-108 | Refuse ordinary text-field full-value overflow under selected font-size/layout rules without implicit clipping/truncation/shrink, restoring the call. | Submitted; no answer. Existing shrink/auto-size is compatibility context, not a target default. Exact size/DA-zero/wrapping/list/tolerance rules and future explicit fitting modes remain open. |

Microsoft primary OpenType file/TrueType/layout references distinguish containers/outlines and glyph substitution/positioning; they do not choose the proposed font support/repertoire subset. Local ISO variable-text/field-font/DoNotScroll clauses supply context but do not mandate the proposed lifetime or overflow default. Current behavior's Helvetica/WinAnsi and sizing constants are not silently imported; static ApplyText/EnsureHelv and historical records are not executed verification of caller-TrueType APIs. See [forms](../forms.md#engineering-supported-font-inputs-and-overflow-q-104-to-q-108) for primary links, examples, alternatives and conditional unexecuted targets. No code changed and no runtime/rendering/interoperability probe ran.


### Q-104 to Q-108 engineering support recorded, 2026-10-28

The user supported all five without adoption, implementation approval or verification, expressly requiring bounded Q-104 resource/cache rules and exact Q-107 repertoire before adoption, plus a Q-108 compatibility review separate from Q-97/Q-102. Developed [FORM-FONT-01 to 04 / FORM-TEXT-04](../forms.md#engineering-supported-font-inputs-and-overflow-q-104-to-q-108): borrowed operation-time font consumption without later Save rereads; eligible truly glyph/font-metric-independent clears without supplied fonts; initial static single-face TrueType outlines by structure; direct-LTR mapping without missing-glyph/shaping approximation; and full-value ordinary text overflow refusal with pre-call document value/state restored and caller input untruncated.

Added unexecuted FORM-FONT-A to H and FORM-TEXT-H/I/J; reconciled forms/shared/workflow/inspection/index/completeness and earlier open-detail wording. Parser/cache/format/embedding/size/layout/geometry/repertoire and diagnostics remain explicit incomplete areas, not borrowed defaults from source code. No new runtime, renderer, acceptance or qualification outcome is established. This supersedes preceding submitted/no-answer statuses as the live state.

Q-108 compatibility review status: bounded static assessment completed; adoption disposition remains open separately from Q-97/Q-102. AppearanceBuilder.Build/Wrap inspected 2026-10-28 uses height-clamped 6-to-14 size when font size is nonpositive, with fitWidth reduction/floor 6 inside that branch; positive sizes skip it. Width wrapping/line placement do not themselves establish complete overflow refusal or visible output. The forms matrix is source-derived, not executed results. Target DA-zero/default size/layout remains open, so not every former automatic-size case can be labelled a new refusal. Before adoption review representative fonts/templates/callers, sizing/state/input/refusal diagnostics/migration, frameworks and independent-reader/rendering evidence; no constants, implicit clipping or auto mode are adopted.

### Next font-budget/repertoire/size questions Q-109 to Q-113 submitted, 2026-10-28

| ID | Recommendation and boundary | Status and evidence limits |
| --- | --- | --- |
| Q-109 | Default 32 MiB per supplied font and 128 MiB retained font-program payloads/document, with explicit finite positive configuration and actual-byte/copy accounting; refuse without evicting prior live data. | Submitted; no answer. Provisional engineering defaults, not measured safety/qualification limits or total RAM bounds. Parser/peak/composite/CPU/resource controls remain open. |
| Q-110 | Document-scoped owned font retention, exact-content/relevant-parameter identity, no implicit global cache; release unneeded failed-call provisional data and document ownership at disposal. | Submitted; no answer. Do not key solely on names/paths/borrowed identities or require later caller-font reread. Algorithm, unused eviction, parser accounting and cleanup diagnostics remain separate. |
| Q-111 | Exact candidate P1: 217 enumerated graphic scalars, plus CR/LF only as the already supported Multiline controls; actual glyph/shaping/appearance eligibility still applies. | Submitted; no answer. ASCII/Latin-1-minus-soft-hyphen plus 27 explicit extras, not automatic WinAnsi byte encoding. No implied Greek/Cyrillic/CJK/emoji/combining coverage or inspection/name restriction; broader reviewed repertoires remain possible. |
| Q-112 | Version-aware fsType policy for editable-form font embedding: supported installable/editable states; refuse restricted/preview-print-only/outline-forbidden bitmap states; meaningful no-subsetting means eligible full font within budgets or refusal, without bit changes/override. | Submitted; no answer. Exact supported OS/2 versions, legacy mixed/reserved states and malformed metadata remain open. Primary metadata distinctions do not themselves choose the whole operation policy. |
| Q-113 | Glyph-bearing appearance size: explicit finite-positive caller size, else that widget's valid positive effective DA size; otherwise require caller size rather than implicit numerical/auto fitting. | Submitted; no answer. Preserve glyph-free exception; units/option granularity/DA parsing/writeback/auto modes remain open and DA-zero compatibility must be reviewed under Q-108. |

See [forms](../forms.md#engineering-supported-font-budgets-repertoire-and-size-q-109-to-q-113) for exact P1 table, examples, alternatives and conditional unexecuted targets. Microsoft primary OS/2 fsType documentation describes permissions/no-subsetting/bitmap/version differences; the Unicode-hosted Microsoft CP1252 map was used to check P1's explicit symbol list. Neither standard mandates these defaults, the initial repertoire or the library's policy. No font budget/cache/repertoire campaign, full compatibility clearance, runtime or interoperability probe ran; hypothetical guard/limit oracles remain proposals. Scope, behavior, acceptance and qualification records remain unchanged.


### Q-109 to Q-113 engineering support recorded, 2026-10-28

The user supported all five as engineering draft proposals, explicitly separating adoption and verification. Developed [FORM-FONT-05 to 08 / FORM-REPERTOIRE-01](../forms.md#engineering-supported-font-budgets-repertoire-and-size-q-109-to-q-113), with unexecuted FORM-FONT-I to P and FORM-REPERTOIRE-A/B/C. The 32/128 MiB font payload defaults are unmeasured starting policies, not parser/peak allocation/CPU bounds. Retention remains document-scoped without later caller-font rereads; exactly 217 graphic scalars comprise P1 (95 ASCII + 95 selected Latin-1 + 27 additions), with CR/LF separate. Version-aware fsType editable-form metadata handling does not settle legacy/missing/malformed cases. Positive caller/effective DA size otherwise requires caller input; glyph-free exceptions remain conditional.

P1 compatibility: static PdfTextCodec.BuildWinAnsi/reverse/CanEncodeWinAnsi/EncodeWinAnsi inspection shows U+00AD remains admitted by the current encoding guard while P1 excludes it from generated painted text. This is a potential caller acceptance change, not proof of old complete-call success, visible rendering or discretionary-hyphen semantics. Representative caller/template/font/rendering/reader and migration/refusal evidence remains open; do not silently remove or substitute the character. No executed coverage or compatibility clearance is claimed.

Q-113/D-29 reconciliation: accepted historical [D-29](../../../agents/decisions/D-29.md) explicitly approved DA-zero single-line width fitting with floor 6. The record is not altered or superseded by this endorsement. Include the proposed positive-size requirement and DA-zero migration/disposition in Q-108's separate compatibility and owner adoption review. Existing bounded static sizing matrix is comparison evidence only; Q-97/Q-102 retain their separate reviews. All subjects remain drafts, Q-33 remains undecided, and no runtime implementation is authorized.

### Next font-processing/layout questions Q-114 to Q-118 submitted, 2026-10-28

| Question | Recommendation awaiting answer | Limits and provenance |
| --- | --- | --- |
| Q-114 | Default 64 MiB peak newly owned live font-processing payload/call, configurable finite positive, including concurrent input/copy/subset/parsed-array/provisional retained payload; refuse before excess allocation. | Unmeasured; prior retained data separately under Q-109, shared storage once/copies separately. Not total heap/object/runtime overhead/process RAM; exact allocator/object/storage and other resource bounds remain open. |
| Q-115 | Default 16 composite levels, 100,000 component-reference and 1,000,000 expanded point visits/call; finite positive settings, checked actual expansion, cycle/reference refusal without trusting maxp declarations. | Unmeasured library limits, not format maxima or complete CPU/parser/cancellation rules. Charging/repeated actual work and representative compatibility/measurement oracles remain necessary. |
| Q-116 | OS/2 versions 0–5; ignore v0/1 high bits; legacy v0–2 mixed usage flags least restrictive; refuse bit 0, meaningful reserved bits, modern mixed flags and absent/truncated/unsupported metadata. | Microsoft documents legacy differences, but exact supported-version/missing/refusal policy is proposed. Apply Q-112 to version-resolved permissions; no bit editing or bypass. |
| Q-117 | One Unicode cmap chosen consistently: (platform,encoding,format) order (0,4,12), (3,10,12), (0,3,4), (3,1,4); no per-character merging/fallback; refuse selected malformed/missing needed glyph. | Microsoft recommends one map and 32-bit coverage when present, permits consistent platform choice. This exact subset/tie order is proposed, not required by the standard; unselected/duplicate structural reporting remains open. |
| Q-118 | Multiline CR/LF visual breaks (CRLF one break, two stored scalars), additional wrapping at ordinary ASCII spaces without no-break-space splitting, word fragmentation, hyphenation or shrinking. | Proposed word-wrap policy differs from static current byte/glyph wrapping. Keep full value characters; refuse unbreakable/full-value overflow under Q-108. Exact repeated/edge spaces, layout geometry/metrics/fit and Q-108 compatibility review remain open. |

See [forms](../forms.md#engineering-supported-font-processing-and-wrapping-q-114-to-q-118) for examples, alternatives, primary Microsoft glyf/maxp/OS/2/cmap findings and conditional unexecuted scenarios. Format references are not numeric-budget authority or proof of compatibility/safety. No font parser/memory/CPU, PDF engine, rendering, independent-library or interoperability probe ran; source inspection and historical tests were not fresh runtime evidence. All five remain unanswered, and drafting them adopts no contract or runtime change. Protected scope/behavior/acceptance/qualification records remain unchanged.


### Q-114 to Q-118 engineering support and session handoff, 2026-10-28

The user supported all five as engineering draft proposals only. Recorded FORM-FONT-09 to 12 / FORM-TEXT-05 with unexecuted FORM-FONT-Q to X / FORM-TEXT-K/L. Q-114's configurable 64 MiB live-payload default is unmeasured; a font below 32 MiB input can still exceed it and refuse; representative workloads must be measured before adoption. Q-115 counts actual expansion, detects cycles and restores the call on limit refusal; 16/100,000/1,000,000 are unmeasured defaults and maxp declarations alone are insufficient. Q-116's versions 0–5/refusal choices and Q-117's exact consistent one-map priority/no missing-glyph fallback are library policy informed by the cited primary sources. Q-118 ordinary-space wrapping preserves values/explicit breaks, refuses overwide runs and leaves repeated/edge-space placement open before adoption; its change remains in Q-108's review independently of Q-97/Q-102. No runtime or interoperability checks, measurement or adoption is implied.

The user requested a new-session handoff instead of another question batch now. See [handoff](handoff.md) for the read order, safeguards, latest answers, remaining gaps and required iteration/reply format. Q-119 onward has not been allocated or answered. The next session should complete useful drafting and reconciliation before presenting exactly five concrete unresolved questions. Earlier submitted/unanswered history entries are chronological and superseded by later engineering-support entries, not the live queue.

### Q-119 to Q-123 submitted after handoff, 2026-10-28

The new drafting session reconciled Q-114 to Q-118 as already engineering-supported and prepared five cross-subject open questions. Q-119 asks for a minimum positive same-file path-identity check; Q-120 asks the ordinary export outcome when distinct logical fields have the same exposed name; Q-121 asks how unset entries differ from empty text and listed empty choice in export; Q-122 asks the default assembly outcome for document-level attachments; Q-123 asks which region of a borrowed font stream is supplied to one operation. Exact examples, alternatives, recommendations and unexecuted targets are in the [live queue](reconciliation.md#engineering-supported-q-119-to-q-123) and subject drafts. None has an owner answer yet. Their recommendations are drafting proposals, not selected clauses, complete-contract adoption, runtime permission or executed evidence. No external comparison, source probe or runtime check was newly performed for this batch; the authority is charter/supported scope plus already recorded owner selections and engineering-supported clauses.

### Q-119 to Q-123 engineering support; Q-124 to Q-128 submitted, 2026-10-28

The user supported all five recommendations as draft answers only. WORKFLOW-ALIAS-02 requires early refusal for normalized absolute-path identity with case handling from filesystem evidence; link and race coverage remain open. FORM-EXPORT-02 refuses the entire ordinary export for distinct fields with the same exposed name without partial/overwritten output; inspection remains separate. FORM-EXPORT-03 includes supported unset entries with explicit null/no-value, while empty text and selected listed-empty choices remain actual empty strings. ASSEMBLY-ATTACH-01 refuses attachment-bearing inputs by default until a defined explicit preservation mode exists. FORM-FONT-13 uses the borrowed stream region from engine-call starting position to end with caller stability until return; final position and nonseekable rules remain open. The clauses and WORKFLOW-ALIAS-D, INSPECT-FORM-A/B, ASSEMBLY-ATTACH-A and FORM-FONT-Y targets are in their subject drafts. None of those scenarios was run. No complete contract was adopted, and no runtime change, compatibility clearance or qualification claim follows.

The next [five open questions](reconciliation.md#engineering-supported-q-124-to-q-128) concern duplicate-name inspection, explicit attachment-preservation input scope and name collisions, rich-clear workflow normalization, and ordinary failed source-append restoration. Q-128 uses the existing [behavior reference](../../behavior.md#engine-guarantees) only as static compatibility evidence of best-effort truncation, not as automatic target authority or a fresh failure probe. The remaining Q-97/Q-102/Q-108 compatibility reviews, Q-33, font work/layout bounds and other subject gaps remain open. No owner answer has been recorded for Q-124 to Q-128.

### Q-124 to Q-128 engineering support; Q-129 to Q-133 submitted, 2026-10-28

The user supported Q-124 to Q-128 as engineering draft proposals with qualifications. INSPECT-FORM-01 reports separate same-name fields with values/widgets and ambiguity but no implicit named mutation target; exact identity and malformed-name handling remain open. ASSEMBLY-ATTACH-02 copies the supported document-level set once per selected input entry in a future explicit mode, so a second entry for the same source creates a second copy. Adobe's PDF Reference describes document-level embedded files through the EmbeddedFiles name tree, separate from page occurrences; the once-per-entry rule is our proposed policy, not a format mandate. ASSEMBLY-ATTACH-03 refuses equal exposed names without silent replacement/renaming, including repeated entries, while name comparison/preflight must be defined before any before-write guarantee. WORKFLOW-RICH-01 normalizes fills and explicit rich clears together with last assignment effective, retaining final-action eligibility/appearance checks; atomic bulk integration remains open. SAVE-APPEND-01 attempts best-effort truncation after failed ordinary source append, preserves the append failure as primary and exposes restoration failure separately. Static writer inspection shows current Truncate suppresses failures, so separate reporting is a proposed diagnostic change, not current verified behavior. Neither outcome guarantees source usability or retry. All five clause scenarios remain unexecuted; no runtime, interoperability or failure-injection probe was run. No whole contract was adopted or runtime change authorized.

The next [five open questions](reconciliation.md#engineering-supported-q-129-to-q-133) develop inspection record identity, attachment-name comparison and preflight, rich-clear bulk atomicity, and open-document behavior after failed append restoration. Their recommendations are not answers. Q-33, the separate Q-97/Q-102/Q-108 compatibility reviews, font parser/layout bounds, associated-file handling and other subject gaps remain open.

### Q-129 to Q-133 engineering support; Q-134 to Q-138 submitted, 2026-10-28

The user supported all five as engineering draft clauses with tighter boundaries. INSPECT-FORM-02 gives each logical field a non-reused opaque read-side identity stable for one open document, including pending reads, without an identity-based write API or cross-document promise. ASSEMBLY-ATTACH-04 compares supported decoded exposed names exactly and case-sensitively without trimming/normalization; equal decoded names collide despite different PDF encodings. This is library policy, not a rule from the Adobe PDF Reference; extraction filename safety and collisions stay separate. ASSEMBLY-ATTACH-05 performs bounded name traversal/decoding/collision preflight before library-created file output or library changes to a caller-supplied stream, only for collisions discoverable from supported names; payload failures keep their ordinary destination contract. WORKFLOW-RICH-02 places final effective fills and rich clears in one atomic engine fill-phase call, restoring pre-phase pending document state on failure while earlier preparation remains pending; resource side effects, cancellation and output rollback are excluded. SAVE-APPEND-02 makes a document whose failed append restoration failed or cannot be established explicitly unusable for further reads/edits/saves while allowing disposal; a new document may be opened after external recovery, but the failed document is not retry-safe. Successful-restoration state remains open. Static inspection shows the current writer suppresses truncation errors, so separate reporting and this state transition are proposed changes, not current guarantees. INSPECT-FORM-D, ASSEMBLY-ATTACH-D/E, WORKFLOW-RICH-B and SAVE-APPEND-B are unexecuted. No runtime, failure-injection or interoperability probe ran, and no whole contract was adopted or runtime change authorized.

The next [five questions, subsequently supported](reconciliation.md#engineering-supported-q-134-to-q-138) cover undecodable field and attachment names, association preservation, mixed-call resource accounting and pending state after established successful append restoration. Q-130's exact comparison precedes Q-131's preflight; their supported answers did not resolve malformed-name decoding. Q-33 and the separate Q-97/Q-102/Q-108 compatibility reviews remain open.

### Q-134 to Q-138 engineering support; Q-139 to Q-143 submitted, 2026-10-28

The user supported all five as engineering draft clauses with two wording refinements. INSPECT-FORM-03 refuses affected form inspection and ordinary export as whole operations when a required field name cannot be decoded, never manufacturing an aliasing replacement, while independent page inspection remains available; decoder and other partial-result API rules stay open. ASSEMBLY-ATTACH-06 refuses explicit preservation during preflight before library-caused destination changes when a would-be-copied exposed name cannot be established; the authoritative source must be selected so collision comparison and refusal use the same name. ASSEMBLY-ATTACH-07 preserves supported associations to each copied file specification and refuses an affected relationship that cannot be preserved, with initial locations and repeated-entry remapping still required before the mode is offered; this does not imply PDF/A-3 conformance. The [PDF Association application note](https://pdfa.org/wp-content/uploads/2018/10/PDF20_AN002-AF.pdf) describes catalog and object-level associations but supplies no library assembly policy. WORKFLOW-RICH-03 applies one public-call budget to final effective actions: overlapping live allocations form peak, sequential released allocations do not sum to peak, shared allocations count once, distinct copies separately, and actual cumulative work counts across the call. The 64 MiB proposal remains unmeasured. SAVE-APPEND-03 retains exact pre-append pending edits and permits a new explicit engine append only after restoration and source usability are established; failed or indeterminate outcomes invoke SAVE-APPEND-02 and do not authorize failed-workflow rerun. INSPECT-FORM-E, ASSEMBLY-ATTACH-F/G, WORKFLOW-RICH-C and SAVE-APPEND-C remain unexecuted. No runtime, malformed fixture, independent-reader, memory/work or failure-injection probe ran; no whole contract, runtime change or qualification was adopted.

The next five questions, [four subsequently supported](reconciliation.md#engineering-supported-q-139-to-q-142) and [Q-143 retained open](reconciliation.md#owner-selected-initial-decision-q-143), address the authoritative attachment-name source, initial associated-file locations/remapping, version-aware field-name decoding, cancellation within a mixed fill phase and source-usability proof after append restoration. The PDF Association embedded-file overview distinguishes `EmbeddedFiles` labels from file-specification filename fields; its format explanation does not select this library's exposed name. PDF 2.0 adds UTF-8 text strings; version/refusal policy and decoder edge cases remain library decisions. All targets were unexecuted at submission. Q-33 and the separate Q-97/Q-102/Q-108 compatibility reviews remain open.

### Q-139 to Q-142 engineering support; Q-143 refined and Q-144 to Q-147 submitted, 2026-10-28

The user supported Q-139 to Q-142 as draft directions with tighter boundaries. ASSEMBLY-ATTACH-08 selects the decoded `EmbeddedFiles` key for tree-backed copied exposed names/collision preflight; file-specification `UF`/`F` remain separate and cannot rescue an undecodable key, while AF-only specifications remain unresolved. ASSEMBLY-ATTACH-09 initially preserves catalog `/AF`, remapping each supported reference to its selected input entry's copied specification and refusing an affected page/object association until occurrence mapping is defined. INSPECT-FORM-04 proposes PDFDocEncoding/UTF-16BE and effective-PDF-2.0-eligible UTF-8 field-name decoding, rejecting malformed Unicode without replacement; exact BOM/effective-version edges remain open. Static [PdfTextCodec.TryDecode](../../../src/jaytwo.Pdf/Internal/PdfTextCodec.cs) review shows unconditional UTF-8-BOM decoding and default .NET UTF-8/UTF-16BE `GetString` paths that can replace malformed sequences. This is a target/current difference, not a runtime probe. WORKFLOW-RICH-04 checks cancellation before each effective action and commit and at defined points within long font/parser work, restoring pending phase state on observation; caller-resource effects and cleanup remain separate. All four new clause scenarios remain unexecuted; no adoption or implementation permission follows.

The user expressly withheld support for Q-143's earlier proof wording: successful truncation plus fresh parse can leave a modified but parseable original prefix. The [refined open Q-143](reconciliation.md#owner-selected-initial-decision-q-143) now requires selection between a trusted stable-source guarantee covering original bytes or pre/post whole-prefix comparison/digest, followed by bounded fresh parse; absent proof, SAVE-APPEND-02's unusable-document rule applies. Q-138's conditional continued-use branch is not yet offered. Submitted Q-144 to Q-147 ask AF-only embedded-file handling, effective-version/BOM edges, internal cancellation-checkpoint units and explicit attachment-preservation limits. The [PDF Association's PDF 2.0 errata](https://pdf-issues.pdfa.org/32000-2-2020/clause07.html) describes catalog `/Version` upgrades but not this library's malformed-version rule. No malformed-name fixture, independent-reader association probe, cancellation/latency measurement, workload measurement or append failure injection ran. Q-33 and the separate Q-97/Q-102/Q-108 compatibility reviews remain open; all subjects remain drafts.

### Q-143 remains open; Q-144 to Q-147 engineering support; question cadence paused, 2026-10-28

The user kept Q-143 unanswered. A trusted stable-source route must name a concrete guarantee covering the original bytes throughout append, recovery and verification; a pre/post digest route needs stable access for both reads and an explicit collision policy. A bounded fresh parse is a further usability check, never original-byte preservation proof. If neither proof route can be established, SAVE-APPEND-02's unusable-document refusal applies. The candidate routes remain open, and Q-138's conditional continued-use branch is not offered.

The user supported Q-144 to Q-147 as engineering draft refinements. ASSEMBLY-ATTACH-10 refuses only an affected AF-only embedded specification before library-caused destination change, with referenced files and unaffected relationships separately classified; `/AF` presence alone does not refuse. INSPECT-FORM-05 uses the current revision's header plus valid catalog upgrade, never allowing invalid `/Version` to enable PDF 2.0 UTF-8. It recognizes exact `FE FF` and `EF BB BF` markers and does not apply a vague BOM-like-prefix rejection to otherwise valid PDFDocEncoding bytes. WORKFLOW-RICH-05 proposes bounded byte-scan/parser and glyph-work checkpoints, with measurement before setting thresholds/latency and no promise to interrupt a borrowed synchronous read once blocked. ASSEMBLY-ATTACH-11 proposes finite configurable copied count, per/aggregate decoded payload, traversal and decoded-metadata limits, counting repeated selected entries by produced copies, preflighting knowable structure and enforcing actual decoded bytes during streaming under later-output failure rules. Numerical defaults require measurement. All new scenarios remain unexecuted; no runtime, reader, failure-injection or workload/cancellation measurement was performed. No complete contract, runtime implementation or qualification was adopted. The user explicitly requested no next question batch and will resume later; Q-33 and Q-97/Q-102/Q-108 reviews remain open.

### Q-33 and Q-143 initial boundaries selected, 2026-10-30

The owner selected SECURITY-RIGHTS-05: otherwise eligible edits on signed inputs remain eligible when Reader rights grant them and every other check permits them; signed edits requiring Reader-rights removal refuse even with removal consent and general-modification authority. This changes the current signed-document path that leaves `/Perms` unchanged and permits otherwise eligible nongranting edits, with possible Reader feature loss. The compatibility review and signed-grant/refusal/independent-denial scenarios are unexecuted.

The owner selected the initial Q-143 failed-append boundary: after an ordinary source append begins writing and fails, attempt and separately report best-effort restoration while retaining the append failure as primary, then make that open document refuse all further reads, edits and saves even if truncation appears successful. No prefix-integrity proof follows from truncation. Q-138/SAVE-APPEND-03 conditional same-document recovery and retry is deferred, not selected for the initial contract. SAVE-APPEND-A/B/REVIEW-A remain unexecuted; no runtime or failure-injection probe ran. Both owner choices are contract boundaries, not whole-contract adoption or implementation authorization.

### Q-97/Q-102/Q-108/Q-113/Q-118/P1 form dispositions selected, 2026-10-30

After separate independent reviews and bounded current-path probes, the owner selected FORM-BUTTON-05's selected-to-Off refusal for a NoToggleToOff radio, while a consistent already-Off repeat leaves state unchanged after ordinary checks. This narrows the earlier engineering-supported blanket refusal and changes the current permissive selected clear. One public-path fixture saved a flagged radio `/V /Off` with all widgets Off and the flag retained; pypdf read it and Poppler showed no selected mark. This is current-path compatibility evidence, not a target refusal/no-op or broad interoperability test. Adobe's one-selected-at-all-times/click wording informed the judgment but does not itself select a programmatic API.

The owner selected FORM-APPEARANCE-02's appearance-ready ordinary fill/clear with call restoration on inability, plus a separate explicit data-only opt-in reporting not-ready output and permitting otherwise eligible exact-value save/export. It is not a silent fallback or visual-readiness promise. Flattening still needs its own appearance rule; signing eligibility remains in its own contract. One public-path Japanese fill saved the value without changed-widget AP and rendered blank in Poppler, while a generated Ada control rendered visibly. These observations do not execute target restoration, explicit-mode, framework or broad reader scenarios.

The owner selected FORM-TEXT-04's complete-value fit/refusal with pre-call restoration, retained FORM-FONT-08's single-line DA-zero automatic fitting and D-29's 6-point floor instead of Q-113's blanket positive-size requirement, and refined FORM-TEXT-05 to prefer ordinary-space breaks with character fallback outside NBSP-joined runs. Exact upper size, layout/fit and repeated/edge-space rules remain open. An existing net10.0 assembly, not a successful current-source rebuild, produced saved full values with clipped fixed-width/height paint, a full DA-zero fit and a 6-point-floor clip; it also split a long word while retaining its stored value. Those are bounded compatibility observations, not target implementation or verification.

The owner selected P1's initial U+00AD exclusion from generated paint, leaving existing reads and an expressly opted-in data-only exact value distinct. The existing-assembly soft-hyphen fixture saved the exact value but Poppler painted a hard-looking hyphen on an unbroken line; supplied TrueType and other readers were not tested. No target refusal/restoration, general parser/font/rendering suite, resource measurement or whole form contract was executed or adopted. All choices remain draft contract boundaries with implementation and verification separate.

### Later Fill compatibility direction, 2026-10-30

The owner subsequently required preservation of existing `Fill` signatures and behavior, including successful `NeedsViewer` results. The Q-102 appearance-ready ordinary-success wording recorded above is unresolved wherever it would change those calls. Develop explicit opt-in data-only and appearance-ready paths with clear readiness results; engineering may select dedicated methods or additive options after source and binary compatibility review. The owner did not authorize implementation or whole-form-contract adoption. Other selected stricter fill proposals, including Q-97, Q-108 and P1 where they would change existing accepted calls, also need a compatibility disposition before applying to existing `Fill`; their draft goals remain recorded.

## 2026-10-31 A4 numeric selection and next-subject draft review

Owner direct instruction selected A4 option (d′): a supplied font program of 33,554,432 bytes (32 MiB) or more refuses with a resource-limit failure. Recorded in FORM-FONT-05 and the adoption ledger, with FORM-FONT-09/10 principles unchanged. Independent [research](research/w218-font-limit-evidence.md) and [JSON](research/w218-font-limit-evidence.json) found LXGW WenKai at 24.31 MiB as the largest eligible font; 8/16 MiB would refuse fonts established embedders accept (A4 trigger), 32 MiB refuses none found. No target-library measurement ran. A representative eligible font between 24 and 32 MiB or above would reopen compatibility/A4 review as applicable. Earlier unselected-number statements are historical and superseded, not target evidence.

Read-only source tracing distinguishes the pending-dirty-font signing appendix from saved source resources and encoded assembly AP graph omission from whole-operation refusal. Boundary alignment with XMP and downstream 8 MiB compatibility remain owner questions; no code changed. A1 remains unmet. The [review packet](w218-limit-and-subject-review.md) proposes an A1-only forms freeze and exact shared/metadata completion candidates with owner questions, as progress within W-218; no whole contract was adopted and no acceptance/qualification gate changed.
