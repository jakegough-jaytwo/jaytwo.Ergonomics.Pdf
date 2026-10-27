# W-211 capability-by-operation contract (proposed)

Package v1. Operations: **Extract** (`ExtractPages`), **Concat** (`Concatenate`), **Edit** (ordinary incremental editor writes: fill, flatten, metadata, attachments, rotation), **Sign** (signature or document timestamp). Dispositions: **Preserve**, **Refuse**, **Excl-sel** (excluded by page selection), **Excl-new** (approved new-document exclusion), **Deriv** (explicit named derivative or preparation), **Retain** (shipped behavior unchanged), **n/a**.

Every Refuse is typed, occurs before the first destination byte, names operation, 1-based input occurrence and category, and never includes credentials, URIs, paths, script text, names or host exception text (W-197 diagnostics frame). Failures after output begins keep the separately documented guarantees; preflight is not a promise against later I/O failure. Exact wording sources are in the last column; where more than one proposal edits one protected clause, [reconciliation.md](reconciliation.md) section 2 gives the merge.

| Capability | Extract / Concat | Edit | Sign | Failure semantics | Wording source |
| --- | --- | --- | --- | --- | --- |
| Page content, inherited geometry, resources | Preserve (encoded bytes copied) | n/a | n/a | existing | shipped A1 to A13 |
| Ordinary AcroForm fields, values, editability | Preserve, `InputN` per occurrence; Excl-sel for unselected widgets | Retain | Retain | unpreservable widget or appearance: Refuse (`PdfAssemblyNotSupportedException`); local budget: Refuse; global budget: `PdfResourceLimitException` | W-190 |
| Field/widget actions, calculation order | Refuse (existing) | Retain | Retain | typed, pre-output | W-190, W-197 |
| Empty signature fields (no Lock) | Preserve under `InputN`; seeds preserved | n/a | Sign into exact retained full name; seeds rechecked | unsupported mandatory seed or appearance: Refuse | W-194 slice A |
| Empty signature field with `/Lock` Include only | Preserve only if slice B approved; else Refuse | n/a | FieldMDP transform created (slice B) | All/Exclude/P: Refuse | W-194 slice B (contingent) |
| Approval signature, timestamp | Refuse by default; Deriv with `AllowUnsignedDerivative` (signatures, `/DocMDP`, `/DSS` omitted, counted) | Retain | Retain | consent never replaces `AllowUnencryptedOutput` or permission checks | W-197 A3 |
| Certification (any P, or unresolved) | Refuse with or without consent (C1) | Retain | Retain | Open: C2 admits P2/P3 into the derivative | W-197 decision 1 |
| Signed field carrying Lock or FieldMDP | Refuse with or without consent | Retain | Retain | Open alternative deferred to slice B | W-197 decision 2 |
| Usage rights `/UR3` | Refuse unless prepared; Deriv omission only alongside signatures (see D-A) | Refuse nonempty write on unsigned document until `RemoveUsageRights`; signed: Perms left unchanged | Refuse before host call until removed | `PdfUsageRightsPreparationRequiredException`; Open: retain grant-aware policy | W-199 (D-A in reconciliation) |
| Hybrid XFA | Refuse; Deriv by `AbandonXfa` (after rights removal) | fill/flatten Refuse until `AbandonXfa` | n/a | dynamic or XFA-only: Refuse always | W-198 |
| Document JavaScript, `/OpenAction` actions, catalog `/AA`, attachments, `/AF`, media annotations, optional content | Refuse (inspected scope enumerated; no whole-file detection claim) | Retain | Retain | attachments: recover by editor `Attachments.Remove` then assemble | W-197 A5a |
| Destination `/OpenAction` | Excl-new, counted `InitialViewDropped` once per occurrence | n/a | n/a | malformed: Refuse | W-192 R192-5 |
| Internal links (`/Dest`, single `/GoTo`) | Preserve; self-link to own copy; others to first selected copy in the occurrence | n/a | n/a | target page unselected: Excl-sel, whole link, counted `ExcludedDestination`; missing/dangling/malformed: Refuse | W-192 R192-1, R192-2 |
| URI links | Preserve absolute; resolve relative only against explicit valid `/URI /Base`; no scheme allowlist | n/a | n/a | missing base for relative: Refuse; `Drop` option still removes whole annotation | W-192 R192-3, R192-4 |
| `/IsMap`, `/Named`, `/GoToR`, `/Launch`, `/JavaScript`, `/Next`, link `/AA` | Refuse | n/a | n/a | never emitted as inert annotation | W-192 R192-4 |
| Markup annotations (listed subtypes), Popup, replies | Preserve with occurrence/page-instance ownership | n/a | n/a | invalid owner, cycle, cross-page, dangling: Refuse | W-193 R193-1, R193-3 |
| Stamp, Caret (existing AP) / plain FreeText (existing AP and DA font) | Preserve | n/a | n/a | no AP or unsupported DA: Refuse | W-193 R193-1, R193-2 |
| Other annotation subtypes and meaningful keys | Refuse (replaces successful omission) | n/a | n/a | `UnsupportedAnnotation` / `UnsupportedAppearance` omissions cease | W-193 R193-6 |
| Outlines | Preserve in input order; ancestor headings kept for unselected targets (Rule A); no wrapper | n/a | n/a | bad structure, non-GoTo action: Refuse; counted `ExcludedOutlineDestination` | W-195 |
| Page labels | Preserve selected pages' labels, rebuild ranges (P) | Retain | n/a | invalid tree: Refuse; alternative N omits all labels | W-196 |
| Tagged structure | Preserve page-local subset (T-20); mixed tagged/untagged occurrences Refuse; optional temporary Refuse (T-19); Deriv untagged (T-21, deferred) | Retain | Retain | outside subset: Refuse; never silent loss | W-191 |
| Info and XMP identity (assembly) | Excl-new: fresh minimal identity, viewer defaults omitted, no conformance claim | n/a | n/a | fixed; no input identity inherited | W-201 |
| Info and XMP editing | n/a | Atomic per-call synchronization; create absent XMP; refuse unsafe XMP without state change | Retain | raw `SetXmp` replaces packet only | W-200 |
| Encrypted input | Retain: `AllowUnencryptedOutput`, per-input permission | Retain | Retain | existing | shipped A11 |
| Workflow ordering, duplicate fill | n/a | Retain F5 (last value wins; fill, flatten, then declared order) | dedicated terminals retained | no duplicate rejection | W-204 |
| Workflow final-copy restoration | n/a | original exception preserved whatever restoration throws | same | partial output stays; discard destination | W-205 (T-02) |
| File alias safeguards | n/a | Retain all (D-65) | Retain | possible-alias refusal | W-206 |
| Async open and `ReadAsync` | Retain | Retain | Retain | W-138 owns inspection design | W-208 |
| Stream decoders | Retain four decoders and caps; full filter names only (T-05) | same | same | `PdfUnsupportedFilterException` | W-209 |
| Signature inspection, revision listing | Retain R9/R10 | Retain | Retain | trust boundary text must stay (W-210 section 1) | W-210 |
| Signing provider API, seed enforcement | n/a | n/a | Retain D-64 surface and W-151 policy | no narrowing | W-202, W-203 |

## Bounded failure semantics common to all Refuse rows

1. Preflight completes for every occurrence before any destination byte. A refusal returns no result.
2. Inspected scope is enumerated in W-197's table; it never decodes content streams, scans the whole file for repair, or walks discarded pages except the whole-input field walk and signature checks.
3. An input that cannot be fully inspected (unresolved, cyclic, over budget) is refused; it is never admitted unexamined.
4. Source files are never modified. Destination failures after output starts leave partial bytes in an engine destination; a workflow stream destination is truncated best-effort; a workflow file is never published.
