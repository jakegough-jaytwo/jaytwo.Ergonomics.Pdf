# W-192 navigation and URI research

Completed 2026-10-26. Research baseline `eb5ce53b5ac332d4e2db949b509ef61e4e97c298`; initially clean working tree. D-66 authorizes this research only. No runtime, test-suite, protected-file, shipped-contract or scheduling change. Queue bookkeeping marks W-192 done as required by agents-check, retaining the remaining order. Findings are measured unless explicitly labeled proposal/inference. No independent reviewer was assigned; independent-tool checks below are not an independent human/context review.

## Reproduce and evidence register

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File agents/reference/simplification/W-192/run.ps1
```

This uses the existing pinned tool paths in `.tools/test.runsettings`, generates original MIT-licensed synthetic inputs with pypdf, builds a disposable public-engine-API console probe in `out/W-192`, and independently inspects outputs. No actions are executed. Source is retained here; generated PDFs and build output stay outside the corpus in ignored `out/`. `inputs.json` records all 31 input hashes, provenance and sizes. `results.json` records 33 engine operations, errors, destination byte lengths and omission counts. `inspection.json` records output hashes, page-target indices, URI strings, catalog/annotation keys and tool versions. `candidate.json` records the explicitly inferred counterfactual classifications. New output `/ID` values make output hashes run-specific.

| ID | Evidence/command | Result and limit |
| --- | --- | --- |
| E192-1 | `graphify query 'AssemblyLinksTests PdfPageExtractor URI GoTo destinations actions' --budget 2300`, confirmed at source locations below | Caller/contract inventory; truncated graph traversal was followed by the located files, not treated as exhaustive proof |
| E192-2 | `run.ps1`: pypdf 6.21.1 on Python 3.13.17 generates fixtures; net10.0 public `PdfDocumentEditor.Concatenate` probe | 33/33 operations accepted; build 0 warnings/errors. Extraction semantics use one occurrence through the same public assembly path. No workflow file-publication claim |
| E192-3 | `inspect.py`: pypdf 6.21.1 target/URI/count assertions; qpdf 12.3.2 `--check` on each engine output | 33/33 structurally accepted and semantic assertions passed. No viewer activation, appearance rendering or URI endpoint validation |
| E192-4 | qpdf 12.3.2 `input --pages . selection -- output` and `--empty --pages input selection -- output` | Four comparative outputs on navigation/open-view fixtures, inspected by pypdf; details below. A precedent, not a substitute contract |
| E192-5 | ISO 32000-1:2008 local clause references below, supplied by the owner | Standard semantics used to separate destination names, Named commands, remote destinations and click maps; no protected text copied into this packet |

## Verified current behavior and anchors

Binding shipped rules: phase-2 A2/A4/A6–A10/A14; ACCEPTANCE.md lines 230–234; docs/behavior.md line 143. The simplification charter is a lower-authority proposal, not an override. Current successful omissions are documented behavior, not confirmed defects.

| Structure | Located implementation | Measured result / source-only limit |
| --- | --- | --- |
| Local explicit/name/string destinations | `PdfAssemblyLinks.Destination`, lines 151–205; `FindName`, 208–251 | `navigation`: self-links hit output pages 1/2 respectively; other direct and named links hit page 3. Second occurrence self-links hit 5/6 and others 7. No cross-occurrence targets; no names tree retained |
| Existing target outside selection | `Destination`, 170–174; extractor occurrence map 153–174 | `navigation-exclude` [1,1] retains two self-links and drops six other links, `MissingDestination=6` |
| Malformed/unresolvable destination | `Destination`, 151–201 | Missing name, dangling page ref, non-page target, unknown mode and wrong coordinate type each accept with the link absent, `MissingDestination=1`. These are distinct from deliberate selection but currently share its reason |
| Both Dest and A | `Rebuild`, 55–57 | Complete link omitted, `UnsupportedLink=1` |
| URI structural filtering | `TryUri`, 102–143 | Spaces and bad percent escape omit complete link, `UnsupportedLink=1`; supported relative, query-only and network-path inputs resolve to asserted absolute strings |
| URI policy | `WebUri`, 146–149; `TryUri`, 137 | HTTP userinfo, file and custom absolute schemes, explicit file base and userinfo-bearing HTTP base omit complete link. `mailto` survives. No filesystem base fallback |
| IsMap | `Rebuild`, 69–73 | true omits; false retains URI link and does not retain the flag |
| Named / GoToR | `Rebuild`, 83–85 | Each omits complete link, `UnsupportedLink=1`. `/Named /NextPage` is not a destination name |
| Next / annotation AA | `Rebuild`, 36–38, 55–57 | URI chain, `/Next null`, link AA each omit entire link and count `UnsupportedLink` |
| Catalog OpenAction / AA, selected page AA | New catalog in `PdfPageExtractor`, 91–107; page allowlist `Policy` at 351 onward | Destination array and GoTo dictionary open views, URI open action, catalog AA and page AA are all dropped; unrelated URI link survives; omissions empty. These five fixtures expose successful losses without counters |
| Drop option | `Rebuild`, 63–67; repository `AssemblyLinksTests` lines 65–80 | Source inventory only in this item: valid URI links are explicitly dropped; current Drop check precedes URI/IsMap validation but follows AA/Next checks. This precedence must change if draft R192-4 is adopted |

The probes use benign strings and small original structures. They cover 31 synthetic input feature cases, not 31 customer workflows. Repetition is applied only to navigation, the distinct identity risk; other cases use [1]. The dangling input is deliberately malformed. No need to execute copied actions to establish loss/preservation.

## Authoritative semantics

Read-only source: `C:/jake/git/pdfdocs/text/iso-32000-1/`, indexed through its `keys.tsv`, `tables.tsv`, and `clauses.tsv` following that repository's AGENTS.md.

- `180-12-3-document-level-navigation.md`, §12.3.2, PDF pages 373–375, Table 151: local explicit destinations identify a page object and a view. Named destinations resolve indirectly. This differs from the `/Named` action subtype. Null coordinate allowance depends on the destination mode; a follow-up must use the table rather than allowing null uniformly.
- `223-12-6-4-7-uri-actions.md`, §12.6.4.7, PDF page 432, Tables 206/207: URI and base strings are ASCII. IsMap changes click behavior using coordinates. The standard allows document-location fallback for a missing base; this repository deliberately does not infer a filesystem base, preserving meaning when producing a relocated new file.
- `227-12-6-4-11-named-actions.md`, §12.6.4.11, PDF pages 434–435, Table 211: NextPage/PrevPage/FirstPage/LastPage are commands over the viewed document. Copying the name into a reordered assembly does not establish preservation of the source navigation.
- `219-12-6-4-3-remote-go-to-actions.md`, §12.6.4.3, PDF page 427, Table 200: GoToR uses a file specification, remote page index or destination name, and optional new-window behavior. Treating it as a URI would lose semantics.
- `214-12-6-2-action-dictionaries.md`, §12.6.2, PDF page 422, Table 193: Next expresses ordered subsequent effects; it is not decorative metadata. OpenAction can describe an action; §12.3.2 also permits an initial destination.

These citations support the proposed typed subset. They do not imply that every standard feature must be supported by this narrow library. No full malformed-destination compliance audit was performed; no new defect is claimed from unprobed parameter-mode combinations.

## Independent-tool precedent

E192-4 applies identical page selection to the same input. Both qpdf modes keep link annotations; they do not reproduce this library's own-copy rule. On the repeated navigation fixture, the first self-link on output page 2 still targets page 1 in both modes. The primary-input mode retains catalog `/Names` and `/Dests`; `--empty` has neither while an old-style name destination remains in a link. The inspector records unresolved name tokens as such, without claiming they work. Primary-input mode retains `/OpenAction`; empty mode drops it. These structural observations show why inheriting a first-input catalog or copying reachable actions is not a suitable replacement for explicit assembly semantics. No claim of a qpdf bug, general merger ranking, viewer outcome or exhaustive name-resolution comparison is made.

## Recommendation and compatibility cost

Exact draft wording and three bounded implementation slices are in [proposal.md](proposal.md). Retain A6 occurrence rules. Distinguish excluded targets from malformed references. Recommend removing scheme/userinfo sanitization as an assembly responsibility, retaining syntax/ASCII validation, explicit usable bases and typed action reconstruction. Refuse unsupported meaningful behavior before output. Retain W-197's distinction between destination initial view and active open actions, adding an observable initial-view omission count rather than a competing policy.

Counterfactual classification against the **33 accepted baseline operations** (inference from structures, not an implemented candidate): 18 would newly refuse, 15 remain accepted. The 18 are five malformed targets; ambiguous Dest/A; Named; GoToR; chain; Next-null; link AA; IsMap true; missing relative base; malformed escape; URI space; catalog AA; selected-page AA; URI OpenAction. These categories are disjoint by fixture construction; this is not a customer refusal rate. Five currently omitted URI cases become preserved links (absolute userinfo/file/custom; explicit file base; explicit userinfo base). Of the remaining ten accepted operations, three exercise navigation, five retain already-supported URI links, and two deliberately lose initial view with a new count. No category totals should be added to W-189/W-197 corpus unions: those studies overlap this scope and their whole-input/selection denominators differ.

Business use: excerpts and assembled business packets keep working local navigation, mail links, intranet/custom-protocol links and authenticated URL spelling. Refusals make unavailable buttons/events visible to the caller instead of reporting success with silent loss. Costs: documents using Named commands, remote PDF links, click maps, action sequences or event actions become blocked for the affected selection; obtaining a simpler source or preparing an explicit external derivative is required. There is no in-library action/script stripping operation. Excluding a page can avoid its page-local unsupported actions but cannot bypass catalog-level rules; repairing a malformed link is outside this library. An unavailable relative base requires an explicit source base supplied by the producer/external preparation; the proposed slice adds no API for guessing one. No extra password/credential collection is introduced. Preserved userinfo may already contain sensitive data, so diagnostic messages must not copy it.

Responsibility removed: assembly-specific scheme and userinfo allowlists. Retained: RFC resolution, syntax checks, destination resolution, source ownership, occurrence identity, typed copying, permissions, budgets and output guarantees. Added: error classification, complete preflight and two fixed aggregate reasons. This is semantic simplification with likely net code growth, not a deletion-savings claim. Estimates and overlap accounting are in the proposal; no runtime package is proposed.

## Limits and follow-up boundary

No reviewer independence, browser/viewer activation, rendering, commercial-library comparison, broad URI conformance sweep, Unicode/invalid-byte probe, all destination mode probe, performance qualification, framework matrix, fuzz campaign or Docker gate. The bounded public-API probe builds only net10.0. Existing PDF Association real link fixtures and tests were inventoried, not rerun here. Customer demand and newly refused corpus totals for these exact R192 rules remain unmeasured; Gate 2 must not treat the synthetic counts as adoption evidence. The four review attachments missing in W-188 remain missing.

Further validation required by implementation: non-ASCII bytes, reserved escapes and opaque-base handling; Drop precedence and malformed action types; cyclic/dangling named trees and resource-limit classes; unselected-page scope; all supported destination modes with mode-specific null rules; input occurrence diagnostics and 0-byte refusal; workflow error propagation using the shared admission frame. W-193 owns outline/label projection, W-194 owns widget/form handling, W-197 owns the admission frame. No preservation addition here is counted as copier-removal savings from W-207. Approval of this research closeout does not approve any product slice.

`graphify update .` completed after setting process-local `XDG_CONFIG_HOME` to `out/W-192/config` to avoid an inaccessible user git-ignore path. The graph remains ignored. It reported partial extraction of the pre-existing `SignatureTests.cs` and some scripts yielding no symbols; the graph is a navigation aid, not complete evidence. All consequential anchors above were read directly at the located files.
