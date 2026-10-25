# Next

## Reset stage R3


## Reset stage R4

1. W-179
2. W-180
3. W-103
4. W-104
5. W-181
6. W-182

## Security hardening phase

7. W-151
8. W-152
9. W-155
10. W-156
11. W-158
12. W-168
13. W-149
14. W-159

Architectural reset stage R2, ergonomic workflows (`reset-plan.md` section 8, D-54). W-141 is done: D-60 is accepted, and `docs/spec/phase-1.md` section 6.7 carries the workflow requirements F1 to F16 (`agents/reference/r2-workflow-proposal.md`). W-142 is done: the pipeline ships with metadata. W-143 is done: fill and flatten ship. W-144 is done: attachments and rotation ship. W-145 is done: form export ships. W-146 is done: the exit criteria except the renderer one are verified at an integration checkpoint. W-80 is done: MuPDF is the second renderer for every G6 render check, which ends R2.

Stage R1 is complete. Architectural reset stage R1, engine foundation (`reset-plan.md` section 7, D-54). W-129, W-130, W-131, and W-132 finished R1 steps 1 to 4; W-135 split the parser so W-132 could make it async-capable (D-55). W-136 updated two skill references to the new names. W-137 split the engine into one type per responsibility before the remaining R1 steps, as W-135 split the parser. W-133 finished R1 step 5. W-134 finished R1 step 7, the verification baseline, so stage R1 is complete; W-117 and W-99 are step 6, done in the engine context, under the owner's dispositions D-56 and D-57. W-117 and W-99 are done. W-140 fixed the checkpoint failure W-99 left: its fixtures moved out of the G2 corpus under D-59. W-109 was reassessed after W-132 and stays `proposed`: it belongs with the R5 storage adapters, after W-138 settles asynchronous inspection. W-116 is done: the owner kept the signature-field walk (D-58). Later stages: W-80 ends R2; W-103 and W-104 belong to R4. W-81 is cancelled with the 1.0 release track.

Security hardening is its own phase after R4 signing and before R5 advanced capabilities (D-62). It consists of W-148 through W-159 and their split or corrective items W-167 through W-170, with one scheduling exception: W-154 runs in R3 because W-164 requires its authentication and permission correction for encrypted input. W-148, W-150, W-153, W-157, W-167, W-169, and W-170 are done. The remaining phase work is ordered by P0, P1, then P2 priority. W-149 and W-155 are investigation-first; W-156 is design-first. They produce owner decisions and follow-up implementation items rather than changing behavior in the research item. W-158 follows W-150 so its terminology matches the corrected inspection contract, and W-159 follows the prerequisite hardening and design items so it documents shipped guarantees rather than proposals. The owner split W-149: W-167 recorded the benign baselines in `agents/reference/w-149-work-budgets.md`, W-168 carries the deferred adversarial-fixture evidence, and W-149 stays the decision gate after both. W-168 is eligible as soon as its owner-directed fixture task delivers. The phase ends when every included item is done or cancelled through an owner-approved disposition; R5 follows it.

Stage R3, document assembly (`reset-plan.md` section 9, D-61). W-160 is done: the owner adopted `docs/spec/phase-2.md` for N1 and N2 with requirements A1 to A15. W-161 to W-163 are done: the writer foundation, extraction, and links and appearances ship. W-154 is done: encrypted-open failures and authentication roles are typed, and rotation uses assembly permission. W-164 is done: engine concatenation preserves occurrence-local fields and links, and encrypted input requires consent and per-input permission checks. W-165 is done: `PdfWorkflow.Combine` ships with deferred inputs and safe file publication. W-166 is done: the full framework matrix, Docker gate, independent-tool sweep, and large-input memory checks verify the R3 exit criteria. Stage R3 is complete; repeated structural-read amplification on the 100,000-page workload is recorded in its closeout.

Stage R4, signing (`reset-plan.md` section 10, D-54), follows R3. W-172 is done and D-64 adopts dedicated signing/document-timestamp terminals, private staging with one editor, and fresh signing evidence. W-179 adopts the exact protected wording proposed in `agents/reference/r4-signing-proposal.md`; the owner has approved the direction but not that wording. W-180 implements the terminals, W-103 adds pinned DSS classification, W-104 adds direct CMS smoke coverage, W-181 refreshes interoperability and bounded campaign evidence, and W-182 verifies the R4 exit. No engine API migration is scheduled without a demonstrated gap. Each remaining security item depends on W-182. The security phase follows the R4 exit checkpoint and precedes every R5 advanced-capability item.
