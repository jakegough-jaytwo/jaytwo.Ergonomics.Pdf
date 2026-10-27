# W-211 reconciliation

Package v1, based on `fbe850e`. Where this file disagrees with a per-slice proposal it says so and gives the reason. Nothing here is adopted.

## 1. Conflicts between proposals, with proposed resolution

| # | Conflict | Proposals | Resolution proposed | Needs owner? |
| --- | --- | --- | --- | --- |
| C1 | Three different replacement texts for phase-2 A3. | W-194 (empty fields, signed-input refusal), W-197 (full A3 with derivative), W-199 (rights-specific A3) | One A3: W-197's A3 as the base, W-194 slice A's empty-field sentences inserted where W-197 says "follow W-194", W-199's rights sentence reconciled by D-A below. W-199 states its A3 "cannot ship" until W-197's text is adopted; this keeps that order. | Yes: D-A, D-B |
| C2 | Usage rights with a derivative. W-197 accepts a rights-only input under `AllowUnsignedDerivative` and omits `/UR3`. W-199 says assembly never removes `/UR3` implicitly and requires explicit `RemoveUsageRights` first. | W-197 vs W-199 | **D-A.** If W-199 is adopted: rights-only unsigned input refuses and names `RemoveUsageRights`; the derivative option omits `/UR3` only on an input that also has an approval signature or document timestamp (where `RemoveUsageRights` is refused anyway). If W-199 is not adopted: W-197 as written. | Yes |
| C3 | Certified P2/P3. | W-197 decision 1 (C1 refuse, C2 admit) | Recommend C1. On the measured corpus C1 and C2 accept the same files (gpo-bills also carries a field lock; opm-of306-pyhanko-certified is already refused for field actions). Real demand unmeasured. | Yes: D-B |
| C4 | Wording that bars new locking transforms. W-203's retain text says "no ... new certification/locking transform ... is authorized"; W-194 slice B creates a FieldMDP transform and says it needs W-151's no-new-transform rule superseded. | W-203 vs W-194 B | Adopt W-203's retain text only with the clause "except as separately approved for W-194 slice B". Otherwise approving W-203 would silently foreclose slice B. Slice B stays contingent. | Yes: D-C |
| C5 | Three edits to phase-2 A9 (omit unsupported appearance). W-190 replaces the ordinary widget part, W-194 adds an empty-signature-widget exception, W-193 replaces the whole omission rule with typed refusal. | W-190, W-193, W-194 | Implement once as the "atomic appearance refusal" ticket (T-11, W-193 slice D). W-190 and W-194 sentences become the two scoped statements inside the single A9 rewrite and are not separate edits. Effort counted once. | Wording only |
| C6 | Action/event admission stated twice. W-193 refuses `/A` `/AA` on non-link, non-widget annotations; W-197 A5a refuses the same. | W-193, W-197 | One inspection owner (`PdfAssemblyInspection`); W-193's rule is a duplicate of A5a and is implemented only there. W-193's extra rule (unknown extension keys refuse) stays in W-193. | No |
| C7 | Two inspection scopes for tags. W-197 lists the structure tree as "never inspected" by its frame; W-191's temporary admission refuses any tag indicator. | W-191, W-197 | W-197's frame excludes the tree on purpose because W-191 owns it. Both run in the one preflight pass; neither adds a second scan. T-19, if approved, extends the same pass. | No |
| C8 | Temporary tag refusal turns today's silent loss into a refusal for 8 of the 30 accepted corpus files. | W-191 P1 | Present T-19 as optional. Recommend skipping it unless T-20 is deferred for long; otherwise users move from silent accessibility loss to refusal with no recovery. Owner choice. | Yes: D-D |
| C9 | Destinations that point at an unselected page: links vanish, outline items stay as headings. | W-192, W-195 | Deliberate asymmetry. A link without a target is dead; a heading still carries structure. Keep both and say so in the contract. W-195 reuses W-192's destination resolver, so T-08 precedes T-17. | No |
| C10 | Ordinary metadata edits on unsigned `/UR3` documents. W-199 would refuse them until rights are removed; W-200 assumes the current policy. | W-199, W-200 | T-25 states the dependency: if T-22 ships first, a refused metadata call restores the pre-call state and raises the preparation exception; T-25 acceptance tests both. | No |
| C11 | `AbandonXfa` and `RemoveUsageRights` must ship together. | W-198, W-199 | One joint pair T-22 then T-23 in one integration window; neither ships alone ("do not ship only half this replacement"). Savings counted once (W-199 owns the rights-interpreter savings). | Yes if either is chosen |
| C12 | A4 is edited by six proposals (tags W-191, bookmarks W-195, labels W-196, attachments/OpenAction/AA/JS W-197, initial view W-192, identity W-201). | as listed | Produce one A4 rewrite at adoption listing each exclusion once. Order: remove tags (T-20), outlines (T-17), labels (T-18) as their tickets ship; W-197 and W-192 amendments land with T-06/T-10; W-201 wording lands with T-03. Because each ticket ships separately, A4 is edited incrementally and each edit only deletes the item that ticket makes true. | Wording only |
| C13 | Ticket estimates exist for most slices but not for W-194 or W-197's two drafts. | W-194, W-197 | Do not invent an estimate. W-197 gave 7 to 11 days for the whole packet (frame 3 to 5, derivative 2 to 3, tests 2 to 3), which is used for T-06 plus T-15 together. W-194 slices have no estimate; the first ticket in that area must produce one before activation. | Gap |
| C14 | Decoder tiers B to D versus retain. | W-209 | Retain all four decoders. Only Tier A (abbreviations) is recommended. | Yes |
| C15 | Rights: retain grant-aware policy versus explicit removal. | W-199 | W-199's own recommendation is optional submission with disclosure of the fill-only regression. This package records both and recommends neither as obviously better: the explicit policy deletes the grant interpreter at the cost of refusing previously granted unsigned fills, attachment edits and existing-field signing. Default if undecided: **keep grant-aware policy**; then T-22, T-23 and the rights halves of T-06 and T-15 are not approved. | Yes: D-A |

## 2. Protected-clause edit map

Each protected clause that more than one proposal edits, with the merge rule. Exact replacement text lives in the cited proposal; nothing is edited here.

| Protected text | Proposals editing it | Merge rule |
| --- | --- | --- |
| `docs/spec/phase-2.md` A3 | W-194, W-197, W-199 | One text per C1, C2 |
| A4 | W-191, W-192, W-195, W-196, W-197, W-201 | Incremental per C12 |
| A5, and new A5a | W-197 (A5a), W-198 (XFA), W-199 (rights) | A5a from W-197; XFA sentence from W-198; rights from W-199 |
| A6, A7, A8, A10 | W-192, W-193, W-195, W-197 | W-192 owns A6 to A8; A10 gains `ExcludedDestination`, `InitialViewDropped`, `ExcludedOutlineDestination`, `ApprovalSignature`, `DocumentTimestamp`, `UsageRights` and drops the annotation omissions W-193 retires; counts stay fixed-cardinality |
| A9 | W-190, W-193, W-194 | One rewrite per C5 |
| Phase-2 section 3 preservation table and form-field rule | W-190, W-191, W-194, W-195, W-196, W-197, W-199 | Each row edited by its ticket only |
| `docs/spec/phase-1.md` section 6.3 W7 | W-200 | single |
| phase-1 section 7.3, W4, section 7.2 sentence | W-198, W-199 | joint, per C11 |
| `AGENTS.md` usage-rights bullet | W-199 | single, exact two-sentence replacement in W-199 |
| `AGENTS.md` decoder bullet | W-209 Tier A | single |
| D-44, D-45, D-48 (supersede), D-64 (only if Candidate N) | W-199, W-198, W-202 | new decision record per adopted packet; old records stay frozen |
| `QUALIFICATION.md` G3, G8 phrase | W-199 | single; allowed under D-47 when the owner approves the rule change, listed in the commit message |
| `ACCEPTANCE.md`, `docs/behavior.md` | all | edited in the implementing commit, never earlier (D-43) |

## 3. Overlap resolutions required by the acceptance criteria

| Item | Resolution |
| --- | --- |
| W-138 (async inspection design) | Stays `proposed` and sole owner of the inspection API design. Append W-208's section 2 note after approval. No change to depends_on or order. |
| W-109 (object-stream reuse) | Stays `proposed`, after W-138. Append W-208's section 3 note after approval. |
| W-156 (admission inspection API) | Retain. R197 is assembly-specific fail-closed preflight and answers none of W-156's parse-versus-completeness-versus-policy questions. W-156 starts from R197's category vocabulary and budget model, so it follows T-06. Add W-197's retain note to W-156 Notes after approval. |
| W-158 (signature documentation) | Retain. Use W-210's replacement scope text, acceptance additions and documentation block. W-158 runs as written if the owner retains R9/R10; if the owner approves a W-210 cut, drop sentences for removed members. |
| W-151 (seed policy) | Binding and unchanged (D-66 context). T-14 preserves seeds and defers enforcement to it. |
| W-149/W-168 (work budget) | T-27 (ASCII85/ASCIIHex bounds) coordinates with W-149; no new budget strategy is proposed by any slice. |
| W-185, W-186 | Done; alias and rollback studies reused them. No alias ticket exists to coordinate. |

## 4. Savings accounting (no double counting)

Most proposals add code. Real deletions:

| Ticket | Deleted | Added | Net |
| --- | --- | --- | --- |
| T-01 copier modes | 39 lines, two decision kinds, two branches | 7 lines | about 32 lines removed, no behavior change |
| T-05 abbreviations | about 25 lines of canonicalization | tests | about 25 lines removed; one behavior change (0 observed loss) |
| T-22/T-23 rights and XFA | 70 to 120 runtime lines of grant interpretation | 60 to 130 lines for gates and API | about zero net (W-199 estimate: possibly 60 added, 60 removed) |
| All assembly preservation, refusal, outline, label, tag, metadata tickets | none | new code | net growth; they correct contracts, they do not simplify |

Retained-by-decision (no savings taken): provider API (W-202), seed policy (W-203), async (W-208), four decoders (W-209), forensics (W-210), aliases (W-206), workflow ordering (W-204). Candidate savings these would have given are listed in [tickets.md](tickets.md) section 5, priced but not recommended. Removed tests are not counted as product simplification.

## 5. Contrary findings preserved

- R2A vs R2B (tags refuse vs exclude; labels preserve vs exclude; absent XMP create vs not; duplicate fills): resolved per slice above; R2B positions are retained as alternatives in W-191 (P3 untagged derivative), W-196 (N), W-200 (Info only), W-204 (reject duplicates).
- W-199 explicit policy vs keeping the grant-aware policy (C15).
- W-209 removal order if the owner wants a smaller decoder set: abbreviations, RunLength, ASCIIHex, ASCII85, LZW.
- W-202 Candidate N (narrow providers) and W-203 R203 (refuse mandatory seeds) both priced; both lose compatible cases (R203: 12 accepted CMS examples plus 3 timestamp preflights in the 28-fixture corpus).
- W-210 cuts: A1+ 1 to 2 days, A2 2 to 3, B 2 to 4; none recommended.
- Unexplained observation: `ExtractPages` of two pages read 12,523 times (15.8 times the file). Routed as T-26, a research ticket, because it touches the large-file charter.
