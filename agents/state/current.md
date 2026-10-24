# Next

1. W-170
2. W-169
3. W-151
4. W-152
5. W-153
6. W-154
7. W-155
8. W-156
9. W-157
10. W-158
11. W-168
12. W-149
13. W-159
14. W-161
15. W-162
16. W-163
17. W-164
18. W-165
19. W-166

Architectural reset stage R2, ergonomic workflows (`reset-plan.md` section 8, D-54). W-141 is done: D-60 is accepted, and `docs/spec/phase-1.md` section 6.7 carries the workflow requirements F1 to F16 (`agents/reference/r2-workflow-proposal.md`). W-142 is done: the pipeline ships with metadata. W-143 is done: fill and flatten ship. W-144 is done: attachments and rotation ship. W-145 is done: form export ships. W-146 is done: the exit criteria except the renderer one are verified at an integration checkpoint. W-80 is done: MuPDF is the second renderer for every G6 render check, which ends R2.

Stage R1 is complete. Architectural reset stage R1, engine foundation (`reset-plan.md` section 7, D-54). W-129, W-130, W-131, and W-132 finished R1 steps 1 to 4; W-135 split the parser so W-132 could make it async-capable (D-55). W-136 updated two skill references to the new names. W-137 split the engine into one type per responsibility before the remaining R1 steps, as W-135 split the parser. W-133 finished R1 step 5. W-134 finished R1 step 7, the verification baseline, so stage R1 is complete; W-117 and W-99 are step 6, done in the engine context, under the owner's dispositions D-56 and D-57. W-117 and W-99 are done. W-140 fixed the checkpoint failure W-99 left: its fixtures moved out of the G2 corpus under D-59. W-109 was reassessed after W-132 and stays `proposed`: it belongs with the R5 storage adapters, after W-138 settles asynchronous inspection. W-116 is done: the owner kept the signature-field walk (D-58). Later stages: W-80 ends R2; W-103 and W-104 belong to R4. W-81 is cancelled with the 1.0 release track.

W-148 through W-159 are the owner-requested consolidated security-hardening backlog, ordered by P0, P1, then P2 priority after the current R2 checkpoint. W-149 and W-155 are investigation-first; W-156 is design-first. They produce owner decisions and follow-up implementation items rather than changing behavior in the research item. W-158 follows W-150 so its terminology matches the corrected inspection contract, and W-159 follows the prerequisite hardening and design items so it documents shipped guarantees rather than proposals. The owner split W-149: W-167 is done and recorded the benign baselines in `agents/reference/w-149-work-budgets.md`, W-168 carries the deferred adversarial-fixture evidence, and W-149 stays the decision gate after both. The owner's review of W-167 and W-150 added W-169 and W-170, the corrections that run first. W-168 is eligible as soon as its owner-directed fixture task delivers. W-149 stays ahead of W-159 and the R3 items.

Stage R3, document assembly (`reset-plan.md` section 9, D-61). W-160 is done: the owner adopted `docs/spec/phase-2.md` for N1 and N2 with requirements A1 to A15. W-161 to W-166 implement them in order: writer foundation, extraction, links and appearances, concatenation with forms and encrypted input, `PdfWorkflow.Combine`, and the exit-criteria checkpoint. W-164 needs W-154. The owner sets whether this sequence runs before or after the hardening backlog.
