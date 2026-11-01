# W-218 independent font-processing diagnostic

**Measured independent-tool research, 2026-10-31; not target-library resource evidence, PDF embedding or shaping verification.** The [script](w218-font-processing.py) and [raw results](w218-font-processing.json) extend the [static inventory](w218-font-inventory.md) under [R4's protocol](../../forms.md#r4-measurement-protocol-single-size-limit-and-proportional-growth). No size limit is selected. No font program or licensed PDF reference is redistributed.

Command: `tmp/w218-bevel/venv/Scripts/python.exe docs/spec/drafting/research/w218-font-processing.py`. Environment: Windows 11 10.0.26100, x64 Python 3.13.1, fontTools 4.68.0, CPU process time with tracemalloc enabled. Each of six installed standalone static TrueType inputs ran twice in a fresh child process with the same 95 ASCII scalars. Children have a 90-second harness timeout (not a library limit). Input bytes are read before tracing; output stays in memory and is discarded after measuring. The raw record identifies input/output hashes, glyph counts, warnings and toolchain. This is an established font tool's subsetting operation, **not an established PDF tool's embedding comparison**.

| Font file | Input bytes | Subset bytes | Python traced peak bytes, excluding preloaded input | Process CPU seconds |
| --- | --- | --- | --- | --- |
| arial.ttf | 1,045,720 | 66,160 | 24,192,438?24,192,473 | 2.469?2.500 |
| timesi.ttf | 962,864 | 54,696 | 18,789,115?18,789,350 | 2.219?2.219 |
| tahoma.ttf | 919,260 | 46,148 | 18,439,074?18,439,149 | 1.938?2.016 |
| sylfaen.ttf | 260,108 | 39,976 | 3,923,101?3,924,365 | 0.438?0.453 |
| LeelawUI.ttf | 390,004 | 27,660 | 6,193,497?6,194,091 | 0.844?0.859 |
| malgun.ttf | 13,459,196 | 20,684 | 90,065,391?90,066,046 | 15.453?15.562 |

Each pair produced byte-identical output after disabling TTFont timestamp recalculation. The initial run used the default timestamp recalculation and produced different hashes; those results were replaced by the final controlled run, not presented as deterministic evidence. fontTools reported dropping unsupported `meta` tables, and `MERG` where present; the complete warning per run is in the raw record. No preservation or embedding correctness is inferred from successful subsetting. No PDF was generated or rendered.

**Inference and limits.** Input size and output subset size are distinct: Malgun Gothic's large input yields the smallest ASCII subset here. Peak traced allocation is not a retained-font budget, RSS, .NET heap measurement or a public promise. It excludes preloaded input, interpreter/native allocations and subprocess startup; process CPU timing includes tracing overhead. Different font structures/coverage confound the size comparison. Six fixed-text observations cannot establish proportional asymptotic work, a resource coefficient or a safe refusal threshold. Do not transplant these peaks into a separate working budget or reinstate 32/64/128 MiB defaults.

The standalone installed inventory still lacks Indic: a filename survey found only `Nirmala.ttc` among the checked Indic-family names, and collection extraction/support was not introduced. Portable legally usable standalone Indic and broader Southeast Asian candidates, representative business-PDF/font pairings, whole-font/no-subsetting and multiple-font workloads, established PDF embedder comparisons, fixed-font text/glyph-count sweeps, cold/warm variability and target .NET measurements remain open. No cross-script painted-text support follows from broad font cmap coverage; P1 remains unchanged.

## Structural-refusal evidence plan: engineering proposal, unexecuted

Pair each future hostile font with a size-matched valid control and an earlier accepted edit S. Record actual bytes, reader progress, elapsed CPU, allocations/retention and output for the chosen tool/version. A harness timeout/kill is a failed controlled-refusal observation, never a library refusal pass. Keep harness safeguards separate from the documented library size limit.

| Risk construction | Falsifying observation / desired target oracle |
| --- | --- |
| Self-cycle and two-glyph composite cycle | Controlled structural refusal with call restored to S; no recursion crash or repeated expansion. Size below the eventual limit is not permission to process indefinitely. |
| Acyclic increasing-depth chain | Establish the documented excessive-nesting refusal point and controlled failure on both sides, without promising an exact public composite counter. Check ordinary real composite fonts remain eligible. |
| Shared composite diamond and repeated references | Increase reuse at comparable input size. Validated work should not multiply by the number of expanded paths. Measure proportional processing or controlled structural refusal; do not equate nesting alone with malformation. |
| loca/glyf/table extent mismatch and integer-overflow declarations | Refuse malformed required structure without seeking/allocating from untrusted declarations. Pair a late-read/cancellation fault to show restoration and release of provisional retention. |
| Size L boundary, misleading Length and truncated source | Establish actual region through EOF; separate size refusal from malformed input and I/O cause. At the proposed R4 threshold, no partial result; previous edit S remains. No L is selected here. |

All target rows are **unexecuted** and remain post-adoption implementation acceptance. Independent pre-adoption parser/subsetter/embedding experiments can inform feasibility and the proposed limit but cannot substitute for these target oracles. The next pre-adoption evidence should broaden legally usable real fonts and versioned PDF embedder comparisons, then propose one documented size limit with its evidence and residual uncertainty. A proposed limit excluding a representative font that established tools embed must return under A4. This diagnostic demonstrates no such rejection and introduces no pending owner decision.
