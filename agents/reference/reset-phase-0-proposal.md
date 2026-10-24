# Reset Phase 0 authority proposal

Status: **proposed under W-127, 2026-10-23; awaiting the owner's approval of the wording.** Nothing here is in force. W-128 applies the approved text.

This proposal is the first exit step of `reset-plan.md` section 6. It lists every passage in the protected sources that contradicts the approved reset, with the exact replacement text. It removes contradictions only; it does not rewrite guidance that still holds. `reset.md` is the owner's product direction and `reset-plan.md` the approved migration; where `reset.md` differs from the plan (phase order, dependency wording), the plan's later decisions govern.

Section 1 lists the questions whose answers change the wording. Sections 2 to 6 give the wording under the recommended answers.

## 1. Questions for the owner

1. **Stage labels.** `docs/spec/` uses Phase 1 (read and append) and Phase 2 (new files). `reset.md` section 11 numbers Foundational Core, Signing, Assembly, Advanced. `reset-plan.md` numbers Authority, Engine, Workflows, Assembly, Signing, Advanced as Phases 0 to 5. The same words mean three different things. **Recommended:** keep "Phase 1" and "Phase 2" as the spec's scope names, and rename the reset plan's phases to **stages R0 to R5**. W-128 renames them in `reset-plan.md` with no other change. The text below assumes this.
2. **W-109 and W-116.** The plan's queue table does not place them. W-109 (reuse decoded object streams under a hard cap) is a bounded cache, which `reset-plan.md` section 4.3 now places above the source abstraction. W-116 (certification detection without a field-tree walk) is a write-path optimization. **Recommended:** keep both `proposed` and reassess after stage R1 step 4 (`IPdfSource`), under migration rule 6 (no algorithm rewrites during movement).
3. **Authority of the plan.** `AGENTS.md` lists six sources; `reset-plan.md` is not among them. **Recommended:** insert it as source 6, ahead of work items and decisions, until reset completion (its section 14). `reset.md` stays an input, not a source.
4. **Dependency policy.** `reset.md` section 3 allows optional integration packages and "lightweight platform compatibility packages" in general. `AGENTS.md` keeps a closed set. **Recommended:** keep the closed set for both libraries; a storage adapter is a separate package whose dependencies are a stage R5 owner decision.
5. **Destination guarantee for new files.** `phase-1.md` tenet 3 promises no half-written output. That holds for today's incremental writes, which restore length on seekable destinations, and `reset-plan.md` section 4.4 keeps it. `phase-2.md` section 2 drafts output to a non-seekable writable stream, which cannot be restored. **Recommended:** leave tenet 3 unchanged now and settle new-file destinations in the Phase 2 interview before stage R3.
6. **Phase 2 interview timing.** `AGENTS.md` blocks Phase 2 work until its spec is adopted. **Recommended:** keep that rule; the interview on `phase-2.md` section 5 questions that affect N1 and N2 is the first R3 work item.

## 2. `AGENTS.md`

**A1. Sources list.** After item 5, insert, and renumber the current item 6 to 7:

> 6. `reset-plan.md`: the owner-approved architectural reset, its target boundaries, and its stage order (D-54), until its section 14 completion criteria are met.

**A2. Product charter, first paragraph.** Replace:

> A permissively licensed, managed .NET library for common business PDF operations, built to stay sparse on gigabyte files and on documents with thousands of pages. It is not a general PDF toolkit and does not compete with qpdf, iText, PDFsharp, or Aspose. There is no layout engine, fluent document model, or renderer.

with:

> Two permissively licensed, managed .NET libraries for common business PDF operations, built to stay sparse on gigabyte files and on documents with thousands of pages (D-54):
>
> - **`jaytwo.Pdf`** is the PDF engine. It owns parsing, the object model, writing, encryption, forms, appearances, signatures, CMS, and every document invariant.
> - **`jaytwo.Ergonomics.Pdf`** is a workflow layer over the public `jaytwo.Pdf` API. It describes operations, chooses defaults, validates and orders them, runs them through one editor session, and translates failures into task-level errors. It contains no PDF parsing, serialization, encryption, signing, or object manipulation.
>
> The dependency runs one way: `jaytwo.Ergonomics.Pdf -> jaytwo.Pdf`. Neither library is a general PDF toolkit or competes with qpdf, iText, PDFsharp, or Aspose. There is no layout engine, renderer, or fluent document model. A workflow builder that records operations on an existing document is not a document model.

**A3. Scope paragraph.** Replace from "Scope is defined in `docs/spec/` (D-38):" through the Phase 2 bullet with:

> Scope is defined in `docs/spec/` (D-38, D-54). The spec phases name scope, not releases:
>
> - **Phase 1**, `docs/spec/phase-1.md`: reading a PDF and appending incremental updates. Forms (read, fill, bulk fill, export, flatten), page rotation, document information and XMP, document-level attachments, password-encrypted files and their permissions, signature inspection, revision listing, and PKCS#11 signing with RFC 3161 timestamps.
> - **Phase 2**, `docs/spec/phase-2.md`: writing new files. Page extraction and concatenation are scheduled in reset stage R3; compaction, removing revisions, and removing passwords are evaluated in R5. No Phase 2 work starts until the owner adopts the spec sections it implements.
>
> Delivery order follows `reset-plan.md` stages R0 to R5. The 1.0 release track is suspended: no package is released, and evidence recorded under the pre-reset project is historical until a later owner decision adopts packaging and versioning for both libraries. Until stage R4, signing changes only for a regression or migration breakage.
>
> Stage R1 moves the code to the engine boundary, namespace, source, and document/editor rules below. Until the R1 work item that implements one of those rules is done, existing code that predates it is not a disagreement with it. New code follows it.

**A4. Dependency paragraph.** Replace the last sentence, "The parser, CMS encoder, and PDF encryption stay in this library.", with:

> The set applies to both libraries; `jaytwo.Ergonomics.Pdf` adds only its project reference to `jaytwo.Pdf`. The parser, CMS encoder, and PDF encryption stay in `jaytwo.Pdf`. A storage adapter, when adopted, is a separate package whose dependencies are an owner decision and never enter either library.

**A5. API bullet.** Replace:

> Public API lives in namespace `jaytwo.Ergonomics.Pdf` and its child namespaces for AcroForm, metadata, attachments, and signatures. The parser, serializer, and engine stay `internal` in `jaytwo.Ergonomics.Pdf.Internal`. Open a readable seekable `Stream`. Do not add an API that takes or returns a `byte[]` of the whole PDF. Offsets are `long`.

with:

> Engine public API lives in namespace `jaytwo.Pdf` and its child namespaces for AcroForm, metadata, attachments, and signatures. The parser, serializer, object model, and engine stay `internal` in `jaytwo.Pdf.Internal`; raw dictionaries, tokens, and indirect objects are not public. Workflow API lives in `jaytwo.Ergonomics.Pdf`. A document opens from an `IPdfSource`, a stable random-access snapshot (`reset-plan.md` section 4.3), or from a readable seekable `Stream` through the engine's adapter, with ownership explicit at every entry point. `PdfDocument` is read-only and lazily loaded; `PdfDocument.Edit()` returns the `PdfDocumentEditor` that owns every change, save, and signature. Do not add an API that takes or returns a `byte[]` of the whole PDF. Offsets are `long`.

**A6. PKCS#11 bullet.** Replace "Do not P/Invoke PKCS#11 from this library." with "Do not P/Invoke PKCS#11 from either library."

**A7. Threading bullet.** Replace "The type is not thread-safe. `Dispose` does not save." with:

> `PdfDocument` and `PdfDocumentEditor` are not thread-safe, and the engine serializes its reads from a source. Disposing an editor does not save.

**A8. Skill routing.** After the domain-skill bullet ("The one primary domain skill that matches the change: ..."), append to that bullet:

> A change confined to `jaytwo.Ergonomics.Pdf` that composes existing engine behavior needs no PDF domain skill.

**A9. Code style, internals bullet.** Replace "Implementation types stay `internal`. `InternalsVisibleTo` the test assembly." with:

> Implementation types stay `internal`. `jaytwo.Pdf` grants `InternalsVisibleTo` only to its own test, framework-lane, and fuzz assemblies. `jaytwo.Ergonomics.Pdf` tests use only the public API of both libraries.

**A10. CI, first bullet.** Append:

> While the release track is suspended, `jaytwo.Pdf` sets `IsPackable=false` and no package is pushed. The R1 boundary item states how `pack` and `nuget-check` behave with an unpackaged engine.

## 3. `docs/spec/phase-1.md`

**S1. Line 5.** Replace "Phase 1 is release 1.0. It ships production-qualified on its own. Phase 2 (`phase-2.md`) adds to it in 1.x without breaking it." with:

> Phase 1 is the read-and-append scope. Its capabilities move into the `jaytwo.Pdf` engine in reset stage R1 and gain `jaytwo.Ergonomics.Pdf` workflows in R2 (`reset-plan.md`, D-54). The 1.0 release track is suspended; section 10 is the bar a later release adopts, not current work. Phase 2 (`phase-2.md`) adds new-file writing.

**S2. Section 1.** Replace "A permissively licensed, managed .NET library for the common business PDF operations" with "Two permissively licensed, managed .NET libraries, the `jaytwo.Pdf` engine and the `jaytwo.Ergonomics.Pdf` workflow layer, for the common business PDF operations".

**S3. Tenet 1.** Append: "A workflow builder that records operations on an existing document is not a document model."

**S4. Section 4, first bullet.** Replace "Input is a readable, seekable `Stream`. Offsets are `long`. Callers with a non-seekable source copy it to a file first." with:

> Input is an `IPdfSource` (R13) or a readable, seekable `Stream` through its adapter. Offsets are `long`. Callers with a non-seekable source copy it to a file first.

**S5. Section 5 table.** Add two rows:

> | R13 | **Random-access source.** Open from an `IPdfSource` or a seekable-`Stream` adapter, synchronously or with `OpenAsync` and a `CancellationToken`. The source is one stable snapshot read at absolute `long` offsets; reads may be partial, a zero-byte read is end of source, and the engine serializes its reads. Ownership is explicit at every open entry point. Cancellation publishes no partially parsed document state. A source change detected during use fails instead of mixing versions (`reset-plan.md` section 4.3). | New |
> | R14 | **Inspection and editing.** `PdfDocument` is read-only and lazily loaded. `PdfDocument.Edit()` returns a `PdfDocumentEditor`; every write in section 6, and every save and signature, is an editor operation. | Change |

**S6. Section 6 intro.** Replace "All writes are pending until `SaveIncremental`, `Sign`, or their async forms. Several edits in one save produce one revision. Dispose never saves." with:

> All writes are made through a `PdfDocumentEditor` (R14) and are pending until its save or sign operation. Several edits in one save produce one revision; a signature or document timestamp adds the separate revision its semantics require. Disposing an editor never saves.

**S7. W20.** Replace "`SaveIncrementalAsync` and `SignAsync` exist beside the synchronous methods. Reads stay synchronous." with "Asynchronous save and sign exist beside the synchronous methods, and opening and source reads are asynchronous too (R13). Synchronous conveniences remain where they do not hide blocking remote I/O." Change its status from Exists to Change.

**S8. Section 6.6 example.** Replace the code block with:

```csharp
await using var doc = await PdfDocument.OpenAsync(source, options, cancellationToken);

var data = doc.AcroForm.Export();                       // R6
foreach (PdfSignatureInfo sig in doc.Signatures) { }    // R9
foreach (PdfRevision rev in doc.Revisions) { }          // R10

await using var editor = doc.Edit();                    // R14
editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> // W2
{
    ["Name"] = "Ada",
    ["Agree"] = true,
});
editor.Pages[0].Rotation = 90;                          // W6
editor.Attachments.Add("factur-x.xml", xml, new PdfAttachmentOptions
{
    Relationship = PdfAttachmentRelationship.Alternative, // W9
    Compress = true,                                       // W11
});

await editor.SignAsync(output, new PdfSignatureOptions
{
    Format = PdfSignatureFormat.PadesBaselineB,         // W17
}, new CmsSignatureProvider(cloudKmsSigner, timestampAuthority), cancellationToken);
```

**S9. Section 9.** Replace "Targets" at the start of the first bullet with "Both libraries target". Replace "Runtime dependencies:" with "Runtime dependencies of both libraries:" and append to that bullet "`jaytwo.Ergonomics.Pdf` adds only its reference to `jaytwo.Pdf`." Replace "Not thread-safe. One `PdfDocument` per operation." with "`PdfDocument` and `PdfDocumentEditor` are not thread-safe. The engine serializes its reads from one source."

**S10. Section 10, first sentence.** Replace "1.0 is released when every requirement marked in sections 5 and 6 passes the gates below." with "A future 1.0 release requires every requirement in sections 5 and 6 to pass the gates below. The release track is suspended (D-54); these gates are not current work."

## 4. `docs/spec/phase-2.md`

**P1. Status line.** Append: "Reset stage R3 implements N1 and N2 after the owner settles the section 5 questions they depend on; N3 to N5 are evaluated in R5 (D-54)."

**P2. Line 5.** Replace "Phase 2 ships in 1.x, after Phase 1 is production-qualified. It adds operations and does not break the Phase 1 API." with:

> Phase 2 adds new-file operations to the `jaytwo.Pdf` engine, with an assembly workflow in `jaytwo.Ergonomics.Pdf`. Release timing is set by the later packaging decision (D-54). It adds operations without breaking the Phase 1 API in force when it starts.

## 5. `QUALIFICATION.md`

**Q1. Line 3.** Replace with: "jaytwo.Pdf and jaytwo.Ergonomics.Pdf. Owner: human owner. Adopted 2026-10-18; revised for the Phase 1 scope under D-38; release track suspended under D-54."

**Q2. After the line 5 paragraph, insert:**

> **Suspended (D-54).** No release is being qualified during the architectural reset. Evidence under `agents/evidence/` was recorded against the pre-reset project and API. It is historical input and does not count toward a gate for `jaytwo.Pdf` or the new API. The decision that adopts packaging and versioning for both libraries re-adopts these gates. Until then, work items verify under D-53, and the gates below describe the eventual bar.

## 6. Skills and decisions

**K1. `pdf-signing-change`, working rules.** Replace "Do not invent staging or temporary-file schemes. Publishing the file is the host's job." with "The engine does not invent staging or temporary-file schemes; publishing is the host's or destination adapter's job. An Ergonomics file convenience may write a temporary sibling and replace the target after success (`reset-plan.md` section 4.4)." Add a first working rule: "Until reset stage R4, change signing only for a regression or migration breakage (D-54)."

**K2. `pdf-task-workflow`, Escalation.** In the ask-the-owner list, after "adding a runtime package;" insert "changing the `jaytwo.Ergonomics.Pdf -> jaytwo.Pdf` dependency direction or making the raw object model public; new signing scope before stage R4;".

**K3. `pdf-fuzz-qualification`, Layout.** Replace "`test/jaytwo.Ergonomics.Pdf.Tests`" with "`test/jaytwo.Pdf.Tests`". The R1 test-move item applies this under D-47, when the path changes.

**K4. New decision D-54, "Architectural reset: engine and workflow libraries".** Context: `reset.md`, the 2026-10-23 interview, `reset-plan.md`. Decision: the twelve decisions of `reset-plan.md` section 1; stage labels R0 to R5; the spec phases name scope, not releases; the 1.0 release track is suspended and pre-reset evidence is historical; signing changes only for regressions or migration breakage until R4. Consequences: supersedes D-38's mapping of Phase 1 to release 1.0 and of Phase 2 to 1.x, and nothing else in D-38; D-15 stands but has no release to gate until packaging is decided; D-49's bounded cursor windows carry into the `IPdfSource` caching rules and the R1 source item states how; `agents/reference/charter-reset-plan.md` (W-61) is historical. Authority: the owner, 2026-10-23.

## 7. Records W-128 changes outside the protected files

- `reset-plan.md`: rename Phases 0 to 5 to stages R0 to R5, and point the Phase 0 work list at this proposal. No other change.
- `agents/reference/charter-reset-plan.md`: a status line marking it historical, superseded by D-54.
- Open work items get a dated Notes line with their disposition; their status stays `proposed` unless noted:

| Item | Disposition |
| --- | --- |
| W-117 | Retain; stage R1 step 6 |
| W-99 | Retain; stage R1 step 6 |
| W-109 | Retain; reassess after R1 step 4 (question 2) |
| W-116 | Retain; reassess after R1 step 4 (question 2) |
| W-80 | Retain; end of stage R2 |
| W-103 | Retain; stage R4 |
| W-104 | Retain; stage R4 |
| W-81 | `cancelled`; the 1.0 release track is suspended |

- `agents/state/current.md`: the reset sequence, starting with the R1 boundary item W-128 creates.
