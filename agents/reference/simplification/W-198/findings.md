# W-198: explicit hybrid-XFA preparation

Research at `639da93e34bfcfd6cc5f8c69ce78d7b341bfdc66`, 2026-10-26. No product, protected-file, shipped-contract or queue changes. Recommendations await Gate 2 approval. One agent gathered independent-tool evidence; no independent reviewer or XFA-aware viewer was used. W-188's missing review attachments remain missing.

## Reproduce and evidence boundaries

Run `powershell -NoProfile -ExecutionPolicy Bypass -File agents/reference/simplification/W-198/run.ps1` from the repository root. Requires the installed paths in `.tools/test.runsettings`. Scratch PDFs and executable project live in ignored `out/W-198/`; source templates and observations are archived here. SDK 10.0.402, net10.0, pypdf 6.21.1, qpdf 12.3.2. Build: zero warnings/errors. `inputs.json` pins the eight original inputs (five original MIT synthetic fixtures plus three unchanged corpus files) and their hashes. Four additional copies model removal of `/XFA` and `/UR3` using pypdf; one AES-256 copy is made with qpdf. Models rewrite new files, and do not exercise a production preparation API, incremental removal, permissions, signature checks or rollback.

`results.json`: 70 operation/credential observations, 46 accepted, 24 refused. All source streams unchanged; every refusal had zero destination bytes. `inspection.json`: pypdf read the fields and rotation in all 46 successful outputs; qpdf accepted all 46 with exit 0. This proves the named structural observations only, not visual equivalence, XFA script behavior or real-world XFA validity. Synthetic XML packets are classification fixtures, not complete working XFA applications. `ordering-model.json` is an executable pure-state model of the proposed ordering and repeat semantics, not engine evidence.

## Current implementation, measured and located

| Question | Observation |
| --- | --- |
| Pending state visible to assembly? | Both `ExtractPages` and static `Concatenate` preserve unsaved `Name=pending` as `Input1.Name`, and rotation 90. Source bytes stay unchanged. Confirmed on synthetic hybrid, hybrid+rights, plain and modeled prepared copies. Existing fill is the surrogate mutation; no explicit preparation API exists. |
| Hybrid conversion today | Synthetic hybrid and hybrid+rights extract successfully without preparation. Output omits XFA. Two successive fills succeed, change the live hybrid flag to false, and an incremental save removes XFA and rights. This repeat probe concerns fills, not proposed preparation. |
| Refused operation with earlier edits | Unknown-name fill refuses on a hybrid; later save retains `Name=old` and XFA while preserving earlier rotation 90. Dynamic/XFA-only fill refuses with `PdfUnsupportedFormException`; earlier rotation still saves. Rights may be removed by the rotation under today's policy, so this does not prove rights retention after an isolated refusal. |
| Unsupported XFA | Synthetic dynamic (`NeedsRendering=true`) and XFA-only (no widgets), and corpus Grants SF-424 refuse extraction with `PdfAssemblyNotSupportedException`, before output. Their fills refuse with `PdfUnsupportedFormException`. No modeled preparation is offered for them. |
| Independent preparation model | Removing XFA and rights from synthetic hybrids and corpus W-9/VA 21-4138 leaves fields readable and all four modeled copies extract successfully. The two corpus originals also extract successfully today. These are four accepted modeled cases, not four new recoveries. |
| Credential barrier | qpdf's AES-256 input has P=-44: forms, copying and assembly allowed; modify-other and annotations denied (`encryption.txt`). All five current operations succeed under both user and owner passwords. Proposed general-modification permission would refuse preparation under that user password: an intentional policy cost, not a current defect. |

Anchors: `PdfEngine.AssemblyStore` returns the same store used by the editor (`src/jaytwo.Pdf/Internal/PdfEngine.cs:43`); `PdfPageExtractor.Assemble` obtains it for each occurrence (`PdfPageExtractor.cs:72`). `PdfAssemblyInspection.Catalog` currently refuses only dynamic/XFA-only forms (`PdfAssemblyInspection.cs:18`). `PdfFieldTree.ClassifyXfa` classifies by resolved XFA, NeedsRendering and widgets without reading packets (`PdfFieldTree.cs:344`). `PdfFormFiller.FillMany` plans before applying and backs up edits around XFA/rights removal (`PdfFormFiller.cs`). `PdfWriteGate` owns certification, password and signature-state checks, and usage-rights interpretation. `PdfObjectStore.BeginBackup`, `Restore`, `EndBackup` preserve the prior dirty set; preparation should reuse this boundary rather than clear all edits.

## Standards and governing contract

Read local authoritative ISO 32000-1:2008 via `C:/jake/git/pdfdocs/text/keys.tsv`: Table 218, clause 12.7.2, PDF page 439 (`235-12-7-2-interactive-form-dictionary.md`) defines XFA as a stream or packet-name/stream array. Table 28, clause 7.7.2, PDF page 83 (`043-7-7-2-document-catalog.md`) describes NeedsRendering as regeneration on opening, default false. These support the structural inputs; they do not establish that an AcroForm fallback is equivalent to XFA or that removing XFA is a permitted fill. No standards text or PDFs are redistributed here.

Shipped authority remains phase-1 R12/W4 and section 7.3, D-48, phase-2 A5, ACCEPTANCE's XFA/flatten/rights rows, and `docs/behavior.md`. Proposed text in `proposal.md` supersedes only after owner approval. Existing classifications are retained rather than adding an XML/XFA interpreter. A structural hybrid classification is not a guarantee of useful fallback content.

## W-189 recovery accounting

W-189's denominator is 51 tracked PDFs; 30 current default Combine accepts, 31 with unencrypted-output consent. Of the 30, W-9 and VA are both hybrid, Reader-extended, tagged and active-content-bearing; Grants SF-424 is the third XFA file and is already refused. Rechecked these three original XFA files here; the whole 51-file census was not rerun. Their pinned hashes are in `inputs.json` and can be compared with W-189.

Under an XFA-only candidate refusal, preparation recovers eligibility for two currently accepted corpus inputs, not two net additional accepts. Rights removal affects the same two files: the union is **two**, not four. Under W-189's combined candidate rules those two remain blocked by active content (and tags absent an approved preservation/derivative policy). This research does not strip scripts or claim that XFA removal removes active content throughout the PDF. W-192 and W-197 remain separate proposals. Dynamic/XFA-only recovery is zero. No customer-frequency inference follows from fixture counts.

| Route for one hybrid+rights input | Explicit caller work before assembly | Credential and recovery | Loss |
| --- | --- | --- | --- |
| Current engine | Open + assemble; conversion/omission implicit | User with copy+assemble sufficient | XFA and rights omitted without named consent |
| Recommended engine route | Open + remove rights + abandon XFA + assemble, same document/editor; no intermediate save required | Proposed modify permission plus existing copy+assemble/unencrypted-output consent; obtain owner or suitably permissive source if denied | XFA templates, datasets, binding, scripts, calculations, validation, dynamic presentation and legacy Reader privileges cease to be authoritative/available |
| Separate file before Combine | Open + two preparations + incremental save; reopen that artifact as Combine input + assemble | Extra persisted artifact, storage lifecycle and second open; same credentials, not a bypass | Same semantic loss; old packets remain in incremental history |
| Integrated Combine option | Describe per-input consent(s) + Combine/save; no intermediate artifact | Host still must supply adequate password; input ownership and private-state policy needed | Same loss; hiding the steps does not remove transformation responsibility |

The loss list is inferred from abandoning the XFA representation, not measured application behavior. Existing AcroForm values/appearances are retained, not synchronized from XFA datasets. An owner credential may be unavailable; recovery may require asking the producer for an AcroForm-native document. Legacy Reader usability after rights removal and XFA-aware rendering are unmeasured; W-199 owns the broader rights analysis. Signed/certified hybrids have no preparation recovery in this proposal, even with `AllowBreakingSignatures`.

## Complexity and recommendation

Recommend the separate engine call first, with explicit rights-first ordering coordinated with W-199. A public call adds a policy boundary and retains classification, dictionary editing, permission/signature checks and rollback. It deletes implicit conversion branches from fill/flatten/assembly policy; it does not delete the XFA transformation responsibility. Shared rights-grant-interpreter savings belong to W-199 exactly once. Whole-document buffering, packet decoding and XML parsing are unnecessary.

Estimate (engineering judgment, not measured implementation): 2–4 engineer-days for the engine API, operation refusals, focused regression/error-path tests and docs; 1–2 additional days for coordinated rights integration and credential/corpus checks, counted jointly with W-199. An integrated Combine route likely adds 2–4 days for per-input descriptors, private preparation lifecycle, file/stream ownership and failure semantics; no such scope is authorized here. Unknowns include malformed form topology, cache invalidation, and exact rights policy adoption. XFA-aware viewer review would add tool/access effort and is still absent.

No current contract violation was confirmed: the observed implicit conversions are documented shipped behavior. No runtime regression test or product fix is warranted by this research alone. `proposal.md` supplies exact wording and a separately scoped implementation candidate; it does not adopt either.
