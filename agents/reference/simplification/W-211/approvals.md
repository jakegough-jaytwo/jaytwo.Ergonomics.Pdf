# W-211 approval packet and ledger

Package v1, based on `fbe850e`. Submission for approval is W-211's outcome; this file is not approval. Only Jake's entries in section 2 change a status. A recommendation is never an approval, and silence or approval of a neighboring packet does not authorize a packet.

## 1. Approval packets (versioned)

Each packet is approvable alone. "Version" is the package version plus the source `proposal.md` it reproduces; re-check the source's content hash at approval time.

| Packet | Decision asked | Tickets | Source proposals | Recommendation | Cost of recommendation | Missing evidence |
| --- | --- | --- | --- | --- | --- | --- |
| P1 | Approve cleanup and one defect fix | T-01, T-02 | W-207, W-205 | Approve | none to users; T-02 corrects which exception surfaces | none |
| P2 | Approve documentation and tests that lock current identity behavior | T-03, T-04 | W-201, W-204, W-208 | Approve | none | actual viewer/printer behavior, real-corpus loss rates |
| P3 | Accept full filter names only | T-05 | W-209 | Approve | 0 corpus streams affected; nonconforming writers lose | out-of-corpus filter use |
| P4 | Adopt assembly refusal frame and diagnostics | T-06 | W-197 | Approve, after D-A and D-B | 21 of 51 corpus files already refused today; refusal frame adds categories without changing today's 30 accepted unless P7/P8 refusals are also approved | demand; Fill, flatten, owner-password measurements |
| P5 | Adopt form, annotation and navigation preservation with refusal in place of omission | T-07 to T-13 | W-190, W-192, W-193 | Approve T-07, T-08, T-11 first; T-09, T-10, T-12, T-13 separable | callers who today get silent omission get a refusal or a preserved file | FreeText regeneration in viewers; relative-URI prevalence |
| P6 | Preserve empty signature fields; explicit unsigned derivative | T-14, T-15; T-16 contingent | W-194, W-197 | Approve T-14; T-15 after D-A, D-B; T-16 only with D-C | derivative drops signatures; certified inputs refuse | W-194 effort estimate; candidate signing evidence |
| P7 | Outlines and page labels | T-17, T-18 | W-195, W-196 | Approve Rule A and labels P | added code (3 to 6 days each) | nested-label reader disagreement; GUI behavior |
| P8 | Tag preservation | T-20; T-19 optional; T-21 deferred | W-191 | Approve T-20; skip T-19 unless T-20 is deferred | 7 to 12 days; tagged files outside the subset refuse | tagged-form fixture; subset coverage on the 8 real tagged files |
| P9 | Rights and XFA made explicit (joint) | T-22, T-23 | W-198, W-199 | **Decide D-A first.** Either choice is defensible | refuses previously granted unsigned fills, attachment edits and existing-field signing until rights removal | legacy Reader usability; fill-only credential barrier |
| P10 | Atomic metadata | T-25 | W-200 | Approve | 5.5 to 9 days | licensed Aspose run; large-packet memory |
| P11 | Research and records | T-26, T-27, T-28 | W-208, W-209, W-138, W-109, W-156, W-158 | Approve | none | none |
| R | Record retain decisions | none | W-202, W-203, W-204, W-206, W-208, W-209, W-210 | Approve retention, with D-C clause in the W-203 text | no savings taken | external consumers of the provider API |

## 2. Owner choices and ledger

Fill in the right-hand columns only. Record the approved wording by version or diff, not by "agreed".

| Choice | Question | Recommended default | Alternatives | Jake's decision | Approved version / diff | Date |
| --- | --- | --- | --- | --- | --- | --- |
| D-A | Usage rights: explicit removal (W-199) or keep grant-aware policy; and how assembly treats rights | Undecided by evidence; if not decided, keep the grant-aware policy and drop the rights halves of T-06, T-15, T-22, T-23 | explicit removal; derivative omits `/UR3` only with signatures | | | |
| D-B | Certified P2/P3 in the derivative | C1 refuse | C2 admit P2/P3 | | | |
| D-C | Allow Include-only lock enforcement, superseding W-151's no-new-transform rule for that case | do not decide now; keep retain text with the exception clause so T-16 stays possible | refuse all locked fields; approve T-16 | | | |
| D-D | Temporary tag refusal (T-19) before T-20 ships | skip | adopt | | | |
| D-E | Label contract | P preserve | N omit | | | |
| D-F | Absent XMP on edit | create (W-200) | leave absent, Info only | | | |
| D-G | Decoder removal beyond abbreviations | none | tiers B, C, D | | | |
| D-H | Signing provider surface | retain | additive entry points; Candidate N | | | |
| D-I | Seed policy | retain W-151 | R203 | | | |
| D-J | Forensic surface | retain R9, R10 | cuts A1+, A2, B | | | |
| D-K | Ticket priority and queue position | as [queue-insertion.md](queue-insertion.md) | any reorder | | | |

Versioned approvals, rejections and deferrals by packet:

| Packet | Status | Approved version | Rejected / deferred parts | Date |
| --- | --- | --- | --- | --- |
| P1 to P11, R | unreviewed | | | |

## 3. Draft decision record for the retain dispositions (packet R)

Not created. If approved, one decision record (next free D number at that time) states:

> Retain, as shipped and unchanged: the D-64 signing provider surface and W-151 seed enforcement (with the exception clause for any separately approved W-194 slice B); workflow ordering and last-value-wins fill (F5); the post-W-185 file publication lifecycle and alias safeguards (D-65); `OpenAsync` and `IPdfSource.ReadAsync` (D-55); the four non-Flate decoders and the decompression caps; and the signature inspection and revision surface (R9, R10). Each retention records the cost it declines to take. W-138 and W-109 remain proposed; W-156 and W-158 remain proposed with their stated sequencing. A retained item may be reopened by a new work item with fresh evidence.

## 4. Review brief for a fresh context

This package was produced by one agent (the W-211 planning editor) and no independence is claimed. Before Gate 2, ask a fresh human or agent context to check: (1) each conflict in [reconciliation.md](reconciliation.md) section 1 against the cited proposals; (2) that [tickets.md](tickets.md) does not allocate or imply any work item; (3) the protected-clause merge map; (4) the effort sums and overlap notes; (5) that no packet text edits a protected file. The reviewer should not re-derive the underlying measurements in W-189 to W-210; those carry their own limits.
