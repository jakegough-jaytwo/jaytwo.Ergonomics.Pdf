# W-188 baseline (Gate 0)

Planning editor: the single agent executing W-188 (assigned at execution start, 2026-10-26). Independent reviewer: not assigned. No independence is claimed anywhere in this directory.

## Repository state

| Item | Value | Command |
| --- | --- | --- |
| HEAD | `8a6e496e0be75dbd8cab49ebb713e5df5b608480` (`wip`, "W-186 Centralize object-store checkpoint state") | `git rev-parse HEAD` |
| Ticketing baseline cited by W-188 | `82a83855581115400d4ecb5f6884895c20ecb45b`; observations below were re-verified at HEAD above | W-188 Problem |
| Dirty tracked paths | `README.md`, `agents/work-items/W-155.md` | `git status --short` |
| Untracked paths | `CONTRIBUTING.md`, `agents/work-items/W-213.md`, `docs/behavior.md` | `git status --short` |
| Owner of dirty paths | Pre-existing, unrelated to W-188 (W-213 README restructure proposal; W-155 notes). Left untouched and unstaged. | |
| Work items / decisions | 213 / 67, `agents-check: OK` | `make agents-check` |

The W-213 README rewrite is uncommitted. Until it lands, "README.md" in the authority order is the pre-W-213 file at HEAD; W-213 would redirect shipped-behavior authority to `docs/behavior.md`. Claims here cite HEAD content only.

## Authority

Order (AGENTS.md): AGENTS.md, `docs/spec/`, ACCEPTANCE.md, README.md, QUALIFICATION.md, `reset-plan.md`, `agents/` records. Governing for this effort: D-66 (research scheduling only, no product approval), D-62 (security exit before R5), D-61 (assembly), D-64 (signing terminals), D-56, D-58. The charter is `agents/reference/reset-siimplify.md` (298 lines, version 1.0, 2026-10-26); it is a reference record, below `AGENTS.md`.

## Tools (this machine)

| Tool | Version | Source |
| --- | --- | --- |
| dotnet SDK | 10.0.402 | `dotnet --version` |
| git | 2.48.1.windows.1 | `git --version` |
| Python (system) | 3.13.1 | `python --version` |
| graphify | 0.9.92 (graph: 7758 nodes) | `graphify --version` |
| qpdf | 12.3.2 | `.tools/versions.txt` (not on PATH) |
| poppler pdfinfo | 26.08.0 | `.tools/versions.txt` |
| mupdf mutool | 1.28.5 | `.tools/versions.txt` |
| OpenSSL | 3.6.5 | `.tools/versions.txt` |
| Python (tools) / pyHanko / pypdf | 3.13.17 / 0.38.0 / 6.21.1 | `.tools/versions.txt` |
| SoftHSM | 2.5.0 portable | `.tools/versions.txt` |
| DSS / JDK | 6.3 bundle (sha256 `60452c53...fa944`) / openjdk 21.0.9 | `.tools/versions.txt` |

Not verified here: that every tool runs. This item executes no test, so none was exercised.

## Corpus

`fixture-manifest.csv` lists the 51 tracked PDFs with size and SHA-256 (`git ls-files '*.pdf'`): 33 in `test/jaytwo.Pdf.Tests/Fixtures/corpus`, 8 in `Fixtures`, 5 in `defects`, 4 in `focused-assembly-links`, 1 in `focused-assembly-invoice`. A filesystem walk of `test/` finds 561 `*.pdf` files; the other 510 are untracked build output or copies and are not corpus.

Reconciling against the reviews: R2A reported 41 fixtures and R2B a raw scan of 36 files. Neither denominator matches 51 tracked or 33 in `corpus`. Feature counts (18 tagged, 16 outlines, 6 Reader-extended, 3 XFA, 4 signed) are not reproducible until W-189 defines a feature-detection method and runs it over this manifest. No refusal total is measured here.

## Review attachments (R1A, R1B, R2A, R2B)

| ID | Charter name | Located | Hash | Usage rights | Status |
| --- | --- | --- | --- | --- | --- |
| R1A | `Pasted text (2)(1).txt` | No | n/a | Unknown | Unavailable |
| R1B | `Pasted text(5).txt` | No | n/a | Unknown | Unavailable |
| R2A | `Pasted text (2)(2).txt` | No | n/a | Unknown | Unavailable |
| R2B | `Pasted text(6).txt` | No | n/a | Unknown | Unavailable |

Searched: tracked files (`git ls-files | grep -i pasted`), `~/Downloads`, `~/Desktop`, and the repo for `R1A`/`R2B` strings. Only the charter's own source register names them. The conversation `6adfb090-51cc-83e8-bc5f-3953c14b7a78` is also not accessible from this environment. The owner must supply the four files (and say whether they may be committed) before any claim attributed to them is treated as verified; the charter's summaries of them are secondhand. This blocks reconciliation of review disagreements, not unrelated research.

## Related ticket map (no status or queue change)

| Work | Status at HEAD | Relationship |
| --- | --- | --- |
| W-138 async inspection / W-109 object-stream reuse | proposed / proposed | Retained. W-109 stays after W-138 (W-109 Notes 2026-10-23). W-208 measures async-source cost and feeds W-138; no superseding proposed. |
| W-151 seed values and signing rollback | done | Binding; W-202/W-203 assess provider and seed interaction without reopening it. |
| W-156 admission API / W-158 trust-boundary docs | proposed / proposed | Overlapping, retained. W-156 depends on W-197 and is a possible scope reduction only; W-158 follows W-210. D-66 forbids treating assembly preflight as satisfying W-156. |
| W-184 / W-185 / W-186 | done / done / done | Completed maintenance. W-195/W-196 (rollback, alias studies) reuse W-185/W-186 and must not duplicate them. |
| W-187 | done | Created W-188 to W-211. |
| W-189, W-190, W-191, W-197, W-210, W-211 | proposed | Downstream; W-188 supplies their baseline. |

Principal code and contract anchors (graphify): `PdfAssemblyInput` (`src/jaytwo.Pdf/PdfAssemblyInput.cs:7`), `PdfAssemblyInspection` (`src/jaytwo.Pdf/Internal/PdfAssemblyInspection.cs`), `PdfWorkflow.Combine` (`src/jaytwo.Ergonomics.Pdf/PdfWorkflow.cs:28`), `PageExtractionTests` (`test/jaytwo.Pdf.Tests/PageExtractionTests.cs`), `AcroFormTests` (`test/jaytwo.Pdf.Tests/AcroFormTests.cs`).
