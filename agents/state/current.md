# Next

1. W-145
2. W-146
3. W-80

Architectural reset stage R2, ergonomic workflows (`reset-plan.md` section 8, D-54). W-141 is done: D-60 is accepted, and `docs/spec/phase-1.md` section 6.7 carries the workflow requirements F1 to F16 (`agents/reference/r2-workflow-proposal.md`). W-142 is done: the pipeline ships with metadata. W-143 is done: fill and flatten ship. W-144 is done: attachments and rotation ship. W-145 adds form export. W-146 checks the exit criteria at an integration checkpoint, and W-80 (MuPDF) ends R2.

Stage R1 is complete. Architectural reset stage R1, engine foundation (`reset-plan.md` section 7, D-54). W-129, W-130, W-131, and W-132 finished R1 steps 1 to 4; W-135 split the parser so W-132 could make it async-capable (D-55). W-136 updated two skill references to the new names. W-137 split the engine into one type per responsibility before the remaining R1 steps, as W-135 split the parser. W-133 finished R1 step 5. W-134 finished R1 step 7, the verification baseline, so stage R1 is complete; W-117 and W-99 are step 6, done in the engine context, under the owner's dispositions D-56 and D-57. W-117 and W-99 are done. W-140 fixed the checkpoint failure W-99 left: its fixtures moved out of the G2 corpus under D-59. W-109 was reassessed after W-132 and stays `proposed`: it belongs with the R5 storage adapters, after W-138 settles asynchronous inspection. W-116 is done: the owner kept the signature-field walk (D-58). Later stages: W-80 ends R2; W-103 and W-104 belong to R4. W-81 is cancelled with the 1.0 release track.
