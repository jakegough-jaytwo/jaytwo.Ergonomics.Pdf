# W-195 ordinary bookmark projection research

Completed 2026-10-26 against product revision `ae632d071cc8fc29464b6166bc6cce0c9c147019`. D-66 authorizes research, not implementation. No runtime, repository test-suite, protected contract or shipped behavior changed. No independent fresh-context reviewer was assigned; external tools give independent implementation evidence, not independent review. The four review attachments remain unavailable as recorded by W-188.

## Reproduce and evidence

Run `powershell -NoProfile -ExecutionPolicy Bypass -File agents/reference/simplification/W-195/run.ps1` from the repo root. `generate.py` builds synthetic outline fixtures (it prints "Object 9999 0 not defined." for the deliberately dangling cases; expected). The disposable probe uses the public `PdfDocumentEditor.Concatenate` API. `project.py` is a disposable reference model of both rules; `inspect.py` is an independent pypdf checker. Retained: `inputs.json`, `cases.json`, `results.json`, `projection.json`, `inspection.json`, `probe/`. Generated PDFs stay in ignored `out/W-195`. `run.ps1` ran end to end with zero failures.

### Measured

- The probe accepted all 48 operations and emitted no outline and no omission count in any output. Malformed and unsupported outlines therefore succeed silently today (Bookmarks: Dropped).
- The reference model projected both rules onto the engine's real output for the 27 operations the engine accepts. All 52 outputs (rules A and B) pass qpdf 12.3.2 `--check`. pypdf 6.21.1 found 0 problems in links, Count sign and magnitude, cycles and direct page destinations. MuPDF 1.28.5 `mutool show outline` printed the same tree as the model for all 52.
- 40 hand-authored oracle trees (20 operations x 2 rules) all matched.
- The model refused 21 operations: 19 single-defect fixtures (`cycle-next`, `cycle-first`, `shared-item`, `parent-mismatch`, `dangling-first`, `no-title`, `dest-dangling`, `dest-nonpage`, `dest-intpage`, `name-missing`, `both-dest-action`, `mode-bad`, `mode-short`, and URI, GoToR, Launch, Named, JavaScript and GoTo-with-`/Next` actions); one malformed item that stays refused even when its target page is unselected; one depth-300 chain refused as a limit failure.
- Rules A and B differ in 7 of the 48 matrix operations. They keep the same set of live items; A adds exactly its retained dead ancestors.
- Repository corpus scan of 169 tracked PDFs: 121 encrypted (not read), 2 unreadable, 30 no outline, 16 with one outline. The strict reader classified 15 supported, 1 empty outline, 0 refused. These are repository fixtures, not customer prevalence.
- Real-file examples: `opm-of306.pdf` page 2 under A gives the document title as an inert heading with four section children, under B four anonymous top-level sections. `faa-8710-1.pdf` page 3 under A keeps the instruction-pages root plus chapter and child, under B only chapter and child.
- ISO 32000-1 12.3.2 and 12.3.3 checked read-only in `C:/jake/git/pdfdocs`.

### Inferred

- The 256 traversal-depth and 1,000,000 mapped-object limits were read from source, not measured.
- Effort and line counts have no implementation behind them.

### Gaps

- No Acrobat or Preview reader, and Poppler has no outline dumper, so click behavior is unmeasured.
- No encrypted-input outline; no tagged-input `/SE` case beyond a dropped structure element.
- Two corpus titles contain PDFDocEncoding bytes pypdf cannot decode (U+FFFD); titles must be copied as raw bytes, which the probe cannot prove.
- No independent reviewer.

## Concrete trees

Notation: `Title@N` targets output page N; `+` open with children, `-` closed with children; no `@` is a heading. Fixture `book`: Ch1@1+ (1.1@1, 1.2@2), Ch2@3- (2.1@3, 2.2@4), Ch3@4.

| Case (source pages kept) | Rule A: retain ancestor headings | Rule B: remove, promote children |
| --- | --- | --- |
| `[1,2,3,4]` (all) | identical to source | identical to source |
| `[2]` | `Ch1+` / `1.2@1` | `1.2@1` |
| `[2,3]` | `Ch1+` / `1.2@1`; `Ch2@2-` / `2.1@2` | `1.2@1`; `Ch2@2-` / `2.1@2` |
| `[4]` | `Ch2-` / `2.2@1`; `Ch3@1` | `2.2@1`; `Ch3@1` |
| reorder `[3,1]` | `Ch1@2+` / `1.1@2`; `Ch2@1-` / `2.1@1` | same |
| reverse `[4,3,2,1]` | source tree order kept, targets remapped: Ch1@4+ (1.1@4, 1.2@3), Ch2@2- (2.1@2, 2.2@1), Ch3@1 | same |
| repeat `[1,1,2]` | `Ch1@1+` / `1.1@1`, `1.2@3` (first copy of each page) | same |
| same doc twice, `[1]` then `[2]` | `Ch1@1+` / `1.1@1`; `Ch1+` / `1.2@2` | `Ch1@1+` / `1.1@1`; `1.2@2` |

Duplicate-title fixture `dupes`: Summary@1, Summary@2+ (Notes@1, Notes@2), Summary@3. Selecting `[1]`: Rule A gives `Summary@1`; `Summary+` / `Notes@1`. Rule B gives `Summary@1`; `Notes@1`. This is the case against B: the surviving Notes sat under the second Summary but now appear beside the first.

Headings with no destination (`Part I` with children) are kept when they have surviving children and are not counted as exclusions. Inputs with no outline or an empty root contribute nothing and are not an error.

## Distinctions

- Unselected target: structurally valid destination to a page the occurrence did not select; not an error.
- Malformed link: cycle, shared item, parent mismatch, dangling or non-page destination, bad mode, missing title; refused regardless of selection.
- Unsupported action: any `/A` other than a lone `/GoTo` without `/Next`; refused, never emitted as a title-only item.

## Recommendation

Rule A. It preserves hierarchy, open state and a document-title root without inventing parentage. A and B are the same bottom-up pass (about 20 lines in the model). A retained ancestor that navigates to its first surviving child was rejected because it invents a destination.

Residual risks: real inputs with unusual outlines newly refuse (0 of 15 readable repository files did); title byte handling; encrypted-input outlines; reader differences for destinations.

Complexity: adds responsibility (outline reader and projector, one omission reason, refusal cases); deletes none. Business use: reports and manuals keep navigable bookmarks after extraction or combination. Loss today: all bookmarks dropped. Bookmark work is not counted as deletion savings.
