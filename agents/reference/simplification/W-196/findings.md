# W-196: page labels in assembled documents

Research completed 2026-10-26 against product revision `80144afc9e47d5ade193064d3bfebf2e197a6c06`. D-66 authorizes research only. Recommendations below await Gate 2 approval. No shipped behavior or protected wording changed. One agent conducted this research; independent-tool results do not constitute fresh-context review. W-188's missing review attachments remain unavailable.

## Evidence and reproduction

Run `powershell -NoProfile -ExecutionPolicy Bypass -File agents/reference/simplification/W-196/run.ps1` from the repository root. Requires installed `.tools/test.runsettings` tools and .NET SDK. The script generates five small original synthetic fixtures, builds the archived disposable public-API probe, runs ten selections (three ExtractPages and seven Concatenate), and checks both alternatives. PDFs and compiled probes remain in ignored `out/W-196`. Retained JSON includes input SHA-256 hashes, literal label oracles, public API outcomes and independent-reader observations. The probe uses whole-file arrays only for these tiny disposable outputs; it proposes no runtime buffering.

Measured: all ten engine operations succeeded, emitted no `/PageLabels`, and returned empty omission dictionaries. All ten preservation models matched the hand-authored expected labels through pypdf 6.21.1 on Python 3.13.17. qpdf 12.3.2 `--check` accepted all twenty outputs. The net10.0 probe and referenced engine built with zero warnings and errors. This verifies label strings as resolved by an independent PDF reader and structural acceptance, not GUI presentation or rendered page content.

The preservation model copies the source range descriptor and adjusts `/St` for each selected source page, emitting one range per output page. It does not construct the output from the reader's generated strings. This intentionally simple model demonstrates semantics; it is not a bounded production parser, range coalescer, malformed-input policy implementation or performance measurement. Five fixture inputs also match their literal oracles through pypdf. Production support for arbitrary number trees remains unproven here.

## Authorities consulted locally

Read-only sources in `C:/jake/git/pdfdocs`: ISO 32000-1:2008 clauses 12.4.2, Table 159 (PDF pages 382-383), and 7.9.7 (99-100); ISO 32000-2:2020 EC3 clause 12.4.2, Table 161 (473-474), and 7.9.7, Table 37 (137-138). No licensed specification text is copied into this repository.

These distinguish physical page index from optional labels. Label ranges use integer keys, must include index zero, and describe style, prefix and positive starting number. A missing style means prefix-only, potentially empty; it does not mean decimal. Number trees can use indirect children and limits. PDF 2.0 EC3 includes an alphabetic-style correction at Issue 432; the alphabetic probe covers only Z/AA and z/aa, not later alphabetic blocks.

Current product anchors: `docs/spec/phase-2.md` sections 3, 5.7 and A4 explicitly drop labels; `docs/behavior.md` assembly section agrees. `PdfPageExtractor.Assemble` builds a fresh catalog containing Pages and optionally AcroForm (lines 86-96 at this revision), with no label handling. This research does not classify the specified omission as a defect.

## Concrete alternatives

Rule P preserves each selected page's source label, including implicit source ordinals when no label tree exists. Rule N drops the tree and uses output ordinals 1 through N, as current output does. No uniqueness reconciliation under either rule. In `inspection.json`, every case records both complete observed sequences.

| Selection | Rule P | Rule N |
| --- | --- | --- |
| Mixed source pages 2,3,4,7,8,9 | ii, iii, 1, A-8, A-9, Part-IV | 1,2,3,4,5,6 |
| Mixed pages 10,2,2,7,4,1 | Part-V, ii, ii, A-8, 1, i | 1,2,3,4,5,6 |
| Restarted exhibit, all pages | Ex-7, Ex-8, Ex-1, Ex-2, Contents | 1,2,3,4,5 |
| Unlabeled source pages 3,5,3 | 3,5,3 | 1,2,3 |
| Same input occurrence repeated | Ex-1, Ex-2, Ex-1, Ex-2 | 1,2,3,4 |
| Prefix-only and empty labels | Résumé-, Résumé-, empty, empty, Résumé- | 1,2,3,4,5 |
| Upper/lower alphabetic starts at 26 | Z, AA, z, aa | 1,2,3,4 |
| Unlabeled pages 4,5 then mixed pages 1,7 | 4,5,i,A-8 | 1,2,3,4 |

The full mixed input covers Roman front matter, a decimal restart, appendix prefixes and uppercase Roman numbering. The cross-range selection starts inside ranges; repeats and reverse order require independent output ranges. Prefixes are text strings, not page content. Neither rule changes a number printed within an existing content stream. Outlines and accessibility tags are separate W-195/W-190 responsibilities.

## Compatibility and recovery, linked to W-189

`corpus-labels.json` rereads the seven label-bearing PDFs from W-189, verifying every file's SHA-256 against its recorded baseline. Four have labels differing from physical ordinals: the two CMS fixtures, NACA and NBS. GPO, IRS F5500 and USCIS I9 have explicit labels that equal ordinals over the whole source. This is a semantic distinction the earlier presence-only scan could not make.

W-189 measured four accepted consent-run outputs losing label trees: GPO, IRS F5500, NACA and NBS. Two of those inputs (NACA and NBS) have different resolved label strings from default ordinals; the other two do not lose displayed strings for whole-document extraction in that baseline. Subsets and concatenation can change even an implicit ordinal. Do not turn four lost trees into four proven visible changes, or extrapolate these fixtures into customer demand. W-189's acceptance/refusal results are reused, not rerun here; this item does not recompute the 51-file refusal matrix or combine loss counts with tags/outlines.

Business use (inference): citations to exhibit, appendix or front-matter labels can continue to identify the same source page under P. Duplicate labels may require identifying the input document as well. N creates a simple packet-wide ordinal but breaks such label references; it also cannot make printed folios match output ordinals. Hosts can provide a source-to-output page map or create replacement labels with a different tool. With N the library offers no label repair API, and correcting printed folios would require out-of-scope content editing. P needs no new credential for an otherwise admitted input; existing encrypted-input authentication, permission and unencrypted-output consent rules remain applicable. Rewriting an encrypted original externally may require credentials and separate consent; dropping labels itself is not a reason to request an owner password.

## Recommendation and cost

Recommend Rule P as a small, separately approved preservation feature, not a simplification saving. It preserves business references under extraction and repeated inputs without collision machinery or a new public option. Rule N is a defensible lowest-complexity alternative if the owner deliberately accepts citation recovery in exchange for retaining the current implementation. The corpus supports concrete loss examples, not relative customer preference.

P adds: bounded number-tree traversal and validation; text prefix handling; source-range lookup; `/St` adjustment with checked arithmetic; output-range rebuilding/coalescing; budget accounting and pre-output failures; regression and interoperability coverage. Retain physical input selection, occurrence ordering and existing writer/permission rules. Add no renderer, printed-folio rewriting, unique-label reconciler or public label-editing API. N deletes no current responsibility: the omission already exists. P shares only generic writer/budget infrastructure with W-195; do not double-count outline traversal or destination remapping as label savings. Worst-case output ranges follow output page count; source inspection follows label-tree size, not all content streams. These are design requirements, not measured resource guarantees.

Effort estimate (inferred): 2-4 engineer-days for bounded parsing, projection and budgeted emission, plus 1-2 days for adversarial cases, independent-reader checks and contract updates; total 3-6 days. Basis is the new parser/serializer surface and range discontinuity tests, not line-count savings. No benchmark, large-file claim or deletion estimate is made. N would need only explicit contract clarification and focused verification, approximately half to one day if approved.

## Limits and disposition

An initial mixed fixture used two indirect number-tree leaves with valid-looking Limits. pypdf reported inability to determine several labels and failed the literal oracle; it was replaced by a flat tree for this comparison. The failure was not investigated sufficiently to assign responsibility to pypdf or the fixture. Nested-tree interoperability must be verified with a second label reader before production acceptance. Main-run JSON is the final flat-tree evidence, not evidence that nested trees work.

Other gaps: no GUI label navigation check, encrypted labels, corrupt trees, cycle/depth/entry limits, integer overflow, enormous prefixes, complete alphabetic blocks, source integrity failures or large-file measurements. qpdf checks structural acceptance, not semantic labels. No independent human/fresh-context review occurred. Proposed implementation acceptance explicitly covers these parser risks rather than treating this model as shipped behavior. No runtime defect was confirmed and no regression fix is claimed. No qualification record, framework matrix, Docker run or fuzz campaign is required by this research item.
