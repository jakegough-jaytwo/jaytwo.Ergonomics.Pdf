# W-211 traceability: PLAN 01 to PLAN 19 and O 01 to O 12

Package version v1, based on `fbe850e`. Research only; nothing here is adopted. "Source" is the work item whose folder under `agents/reference/simplification/` holds the evidence and the exact per-slice wording. Ticket labels `T-nn` are draft labels in [tickets.md](tickets.md), not allocated W numbers.

Dispositions: **Recommend** (do), **Retain** (keep shipped behavior; a decision record only), **Open** (owner choice with a recommended default), **Gap** (evidence missing, blocks only the dependent claim).

## PLAN map

| PLAN | Outcome | Source | Evidence | Disposition | Tickets / existing items | Unresolved gap |
| --- | --- | --- | --- | --- | --- | --- |
| 01 | Reconcile reviews, map authority | W-188, W-211 | `W-188/baseline.md`, `evidence-ledger.md`; this package | Done (baseline) / this package | none | Four review attachments never found (W-188); R2A/R2B can only be cited secondhand |
| 02 | Refusal impact by operation | W-189 | 51 tracked PDFs; combine-all 30 accepted / 21 refused; counterfactual unions | Measured for Combine only | feeds every refusal ticket | Fill, flatten, metadata, sign, rotate, owner-password and repeated-occurrence runs not measured; selected-page reach not measured (whole-graph over-estimate) |
| 03 | Tags and admission scope | W-191 (+W-189) | 10 tag probes, 8 of 30 accepted inputs tagged | Recommend bounded page-local preservation; temporary refusal optional | T-19, T-20 | No ordinary unscripted tagged-form fixture; usefulness of the subset on the 8 real tagged files unmeasured |
| 04 | Assembly contract wording | W-190, W-191, W-192, W-193, W-194, W-195, W-196, W-197, W-201 | per-slice `proposal.md` | Recommend, merged through [reconciliation.md](reconciliation.md) | T-06 to T-21 | Merged A3/A4/A9 wording needs owner choice on D-A (rights) and D-B (certified P2/P3) |
| 05 | Remove unused copier modes | W-207 | `proposed-removal.diff`; 0 production callers of Remap/Reject | Recommend | T-01 | none material |
| 06 | Meaningful omission becomes refusal | W-197, W-193, W-190 | R197 frame; E190-9; S193-D | Recommend | T-06, T-11 | Late-failure behavior stays documented, not strengthened |
| 07 | URI meaning and navigation | W-192 | 33 operations, pypdf/qpdf checks | Recommend | T-08, T-09, T-10 | Relative-URI prevalence in the corpus (3 files) not itemized |
| 08 | Annotations and relationships | W-193 | 44 operations, 12 raster comparisons | Recommend (A, D first; B, C separable) | T-11, T-12, T-13 | Viewer regeneration of FreeText unverified |
| 09 | Unsigned signature fields | W-194 | seed subset table; P194 drafts | Recommend slice A; slice B Open | T-14, T-16 | W-194 gave no effort estimate; no candidate signing evidence into preserved fields |
| 10 | Bookmarks and labels | W-195, W-196 | 52 outputs, 40 oracle trees; label clauses | Recommend outline Rule A and label preservation P | T-17, T-18 | Nested label tree reader disagreement; GUI behavior; encrypted/malformed labels |
| 11 | Tag behavior and derivative | W-191 | as PLAN 03 | Recommend P2; P1 optional; P3 deferred | T-19, T-20, T-21 | as PLAN 03 |
| 12 | Rights and XFA preparation | W-198, W-199 | 6 rights, 3 XFA fixtures | Recommend as one joint pair; alternative retained | T-22, T-23 | Legacy Reader usability and fill-only credential impact unmeasured |
| 13 | Info/XMP editing | W-200 | 11 fixture families, 4 libraries | Recommend | T-25 | Licensed Aspose run unavailable; large-packet memory unmeasured |
| 14 | Narrow signing provider API | W-202 | 45-file inventory, 8 replacement shapes | **Retain** | none (3 conditional tickets in [tickets.md](tickets.md) section 5) | External consumers unknown |
| 15 | Workflow ordering and rollback | W-204, W-205 | ordering probes; rollback probes | Retain ordering; Recommend one defect fix | T-02, T-04 | none material |
| 16 | Aliases | W-206 | Windows junction and Linux symlink probes | **Retain** | none | Other filesystems not qualified |
| 17 | Async source direction | W-208 | 52 MB / 12,000-page latency fixture | **Retain** `OpenAsync`; W-138 and W-109 stay proposed | notes only (T-28) | No gigabyte or real-remote measurement; ExtractPages read amplification unexplained (T-26) |
| 18 | Decoder retention | W-209 | 335 required streams in 50 of 51 PDFs | **Retain** four decoders; Recommend full-name-only filters | T-05, T-27 | Out-of-corpus filter demand unknown |
| 19 | Forensics and seed policy | W-210, W-203 | seven CMS members traced; 28-fixture seed corpus | **Retain** R9/R10 and W-151 | T-28 (W-158 wording) | Cuts A1+/A2/B priced, not recommended |

## Owner decision log map (O 01 to O 12)

| O | Charter status | Proposed status after this package | Recommendation | Alternatives preserved |
| --- | --- | --- | --- | --- |
| O 01 Forms and namespaces | settled principle | settled; edge cases specified (W-190) | `InputN` wording; refuse unpreservable widget | none |
| O 02 Tags | settled principle | settled; implementation recommendation pending | page-local subset (T-20) | R2A refuse; R2B exclude; temporary admission T-19; derivative T-21 |
| O 03 Duplicate labels | settled constraint | settled; contract choice pending | preserve labels (P) | new-document numbering (N) |
| O 04 Empty signature fields | settled direction | settled; lock/seed subset pending | slice A (no Lock); slice B Include-only contingent | refuse any field with Lock |
| O 05 Refusal and unsigned derivatives | recommendation pending | recommendation pending | default refusal frame plus `AllowUnsignedDerivative`; certified refused | C2 admit certified P2/P3; admit when no locked field selected |
| O 06 Info/XMP and identity | evidence pending | recommendation pending | create absent XMP; fresh assembly identity | Info only (R2B) |
| O 07 XFA and rights | recommendation pending | recommendation pending (joint W-198/W-199) | explicit `AbandonXfa` and `RemoveUsageRights` | retain grant-aware policy |
| O 08 Provider and seed policy | evidence pending | recommendation: retain | retain D-64 provider surface and W-151 | Candidate N; additive convenience; R203 narrowing |
| O 09 Ordering and rollback | evidence pending | recommendation: retain, plus one defect fix | retain F5; fix final-copy exception replacement | duplicate-fill rejection; declaration order |
| O 10 Async, decoders, forensics | evidence pending | three independent decisions: retain all; one small decoder change | see PLAN 17 to 19 | removal tiers; forensic cuts A1+/A2/B |
| O 11 Navigation, annotations, aliases, copier modes | recommendation pending | recommendation pending (navigation, annotations, copier); retain (aliases) | see PLAN 05, 07, 08, 16 | W-206 alternatives |
| O 12 Contract adoption and queue insertion | owner approval required | this package submits it | [queue-insertion.md](queue-insertion.md), [approvals.md](approvals.md) | per-packet approve, reject, defer |

No O entry is moved to approved, rejected or deferred here; only [approvals.md](approvals.md) records Jake's decisions.
