# W-211 draft implementation tickets

Package v1, based on `fbe850e`. These are **drafts**. They are not work items, have no allocated W numbers (W-218 is the next free number at this revision, to be re-checked), and become `proposed`/`ready` work items only for the packets Jake approves in [approvals.md](approvals.md). Each real item gets exact `allowed_changes` (files or directory prefixes ending in `/`, no globs) at creation; the paths below are the candidates named by the source proposals, with `src/jaytwo.Pdf/Internal/` abbreviated `Internal/`.

Common to every ticket: owner and approver Jake; reviewer is a fresh human or agent context where an independence claim is made (none assigned); the implementing commit changes spec status to Exists, `ACCEPTANCE.md` and `docs/behavior.md` together (D-43), lists any `QUALIFICATION.md` or skill restatement edits (D-47), and adds permanent regressions for every confirmed defect. Load the domain skill named in `AGENTS.md` (`pdf-core-change` for engine work, `pdf-acroform-change` for form work, `pdf-signing-change` for signing, `csharp-refactoring` for T-01) and `pdf-change-verification`. No ticket needs the full framework matrix or Docker gate except at the checkpoints in [queue-insertion.md](queue-insertion.md).

## 1. Packet P1: cleanup and defect (no contract decision)

| Ticket | Outcome and kind | Candidate allowed paths | Prerequisites | Acceptance (incl. negative) | Validation | Effort (source basis) |
| --- | --- | --- | --- | --- | --- | --- |
| T-01 | Remove unused copier policy modes. Behavior-preserving cleanup. | `Internal/PdfTypedObjectCopier.cs`, `test/jaytwo.Pdf.Tests/NewFileWriterTests.cs` | none | Diff in `W-207/proposed-removal.diff` applies; `Remap`, `Reject`, `CopyDecision.Value/Message`, `CopyContext.Value` gone; cycle registration, source-qualified key, raw stream copy, budgets and `PdfExtractor.Policy` unchanged; output byte-identical on the assembly tests | focused assembly and `NewFileWriterTests` on net10.0; build all targets with StyleCop | under 1 hour (measured probe) |
| T-02 | Preserve the original exception when final-copy restoration throws an unexpected type. Defect fix (confirmed by W-205 probe `workflow-copy-unexpected-restoration`). | `src/jaytwo.Ergonomics.Pdf/Internal/WorkflowFiles.cs`, `test/jaytwo.Ergonomics.Pdf.Tests/WorkflowSigningTests.cs` (or one fault-stream file), `docs/behavior.md`, `ACCEPTANCE.md`, XML remarks in `PdfWorkflowBuilder.Signing.cs` | owner approves the W-205 paragraph | 17-byte copy failure followed by `SetLength` throwing `InvalidOperationException` surfaces the original `IOException`; partial output remains; borrowed resources stay open; one provider call; seek-failure case reports length restored; cancellation keeps token and type; expected-restoration cases unchanged. Regression fails on the baseline first. | focused workflow tests on net10.0; net48 if the item requires target evidence | 0.5 to 1 day |

## 2. Packet P2: documentation and test lock-down (no runtime change)

| Ticket | Outcome | Candidate paths | Acceptance | Effort |
| --- | --- | --- | --- | --- |
| T-03 | Lock down assembly identity and viewer defaults (W-201) | `ACCEPTANCE.md`, `docs/behavior.md`, `src/jaytwo.Pdf/PdfDocumentEditor.cs` remarks, one new test file under `test/jaytwo.Pdf.Tests/`, phase-2 text only after approval | Metadata-rich extract and two reverse-ordered inputs get fresh IDs and no input Info, XMP, PDF/A markers, print preferences, PageMode or PageLayout; later Title/Author assignment creates new values; inputs unchanged. A discovered defect gets a scoped fix and regression. | 1 to 2 days, zero expected production change |
| T-04 | Explanatory documentation: duplicate-fill override paragraph and example (W-204), blocking-trade-off sentence for async hosts (W-208 section 4) | `docs/behavior.md` only | Wording matches the proposals exactly; no behavior claim beyond measured. Optional, may fold into T-02. | under 0.5 day |

## 3. Packet P3: small contract change

| T-05 | Accept only full filter names in stream dictionaries (W-209 Tier A). Contract change. | `AGENTS.md` decoder sentence (owner-approved wording), `Internal/PdfStreamDecoder.cs`, decoder tests, `ACCEPTANCE.md` lines 79 and 80, `docs/behavior.md` line 43 | owner approves exact wording | `Fl`, `LZW`, `RL`, `AHx`, `A85` in a stream `/Filter` throw `PdfUnsupportedFilterException` at all four sites; full names unchanged; Flate baseline passes; inline-image handling unaffected | focused decoder tests; fuzz seeds that used abbreviations updated | 0.25 to 0.5 day |

## 4. Assembly packets (ordered; dependencies in the last column)

### P4 admission frame

| Ticket | Outcome | Candidate paths | Acceptance | Effort | Depends |
| --- | --- | --- | --- | --- | --- |
| T-06 | Default refusal frame and diagnostics (W-197 P197-A, A5a, diagnostics, A10 counts) | `Internal/PdfAssemblyInspection.cs`, `PdfPageExtractor.cs`, `PdfAssemblyForms.cs`, `src/jaytwo.Pdf/PdfAssemblyNotSupportedException.cs`, new refusal enum, assembly tests, `test/jaytwo.Ergonomics.Pdf.Tests/` (publication), A3/A4/A5a/A10 wording, `ACCEPTANCE.md`, `docs/behavior.md` | Each R197 category refuses from both terminals with zero destination bytes and the occurrence in the message; discarded-page signatures refuse, discarded-page actions do not; exhausted field budgets refuse; workflow file and stream refusals publish nothing; messages carry no credentials, URIs, paths, names or host text | with T-15: 7 to 11 days (frame 3 to 5, derivative 2 to 3, tests and checks 2 to 3) | owner D-A, D-B |

### P5 forms, annotations, navigation

| Ticket | Outcome | Candidate paths | Acceptance | Effort | Depends |
| --- | --- | --- | --- | --- | --- |
| T-07 | Freeze ordinary-form namespace and ownership contract (W-190) | `PageConcatenationTests.cs`, `PageExtractionTests.cs`, `PdfAssemblyForms.cs`/`PdfPageExtractor.cs` only if a mismatch is found, approved text, docs | Empty earlier occurrence consumes its ordinal; reciprocal widget/page ownership on repeated pages; partial multi-page selection prunes; later edit changes one occurrence; independent reader and render comparisons | 1 to 2 days | wording approval |
| T-08 | Destination classification (W-192 slice A) | `Internal/PdfAssemblyLinks.cs`, `PdfOmissionReason.cs`, link tests | Real excluded target counts `ExcludedDestination`; missing name, dangling, non-page, malformed refuse with zero bytes; repeated pages and two occurrences stay independent | 1 to 2 days | T-06 frame |
| T-09 | URI semantics (W-192 slice B) | same link files and tests | absolute retained without scheme allowlist; relative resolved only against explicit valid `/URI /Base`; missing base refuses; malformed and non-ASCII refuse; `Drop` validates ordering | 1 to 3 days | T-08 |
| T-10 | Meaningful-action and initial-view admission (W-192 slice C) | `PdfAssemblyLinks.cs`, `PdfAssemblyInspection.cs`, `PdfPageExtractor.cs`, tests | `IsMap`, `Named`, `GoToR`, `Launch`, `JavaScript`, `Next`, link `/AA` refuse; valid destination `/OpenAction` omitted and `InitialViewDropped` counted once per occurrence | 2 to 4 days | T-06 |
| T-11 | Typed annotation ownership and relationships, with atomic appearance refusal (W-193 slices A and D, plus the single A9 rewrite of C5) | new internal annotation importer files, `PdfPageExtractor.cs`, `PdfAssemblyAppearances.cs`, `AssemblyLinksTests.cs` and new annotation tests | Popup reciprocity, reply/Group/state chains preserved per output page instance; invalid owner, cycle, cross-page, dangling target refuses; unsupported appearance of a retained ordinary widget, empty signature widget or supported annotation refuses; objects/depth/bytes budgets covered; later-occurrence failure leaves destination empty | A 3 to 5 plus D 1 to 3 (ship together) = 4 to 8; W-190's refusal item (1 to 3) and W-194's A9 sentence are inside this, counted once | T-06 |
| T-12 | Passive Stamp and Caret (W-193 slice B) | importer, tests | Existing AP plus Name or RD/Sy preserved; no AP refuses; rotated and state APs render the same in Poppler and MuPDF | 1 to 2 days | T-11 |
| T-13 | Plain FreeText (W-193 slice C) | importer, default-resource helpers, tests | Contents/DA/Q/BS/RD preserved; font binding remapped across occurrences; missing AP, font or unsupported DA, rich text, callouts refuse; viewer edit check | 2 to 4 days | T-11, T-07 |

### P6 signature fields and derivative

| Ticket | Outcome | Candidate paths | Acceptance | Effort | Depends |
| --- | --- | --- | --- | --- | --- |
| T-14 | Preserve empty signature fields without Lock (W-194 P194-A) | `PdfPageExtractor.cs`, `PdfAssemblyForms.cs`, field helpers, assembly tests, A3/A9 wording | Plain, seeded, null-valued and read-only fields preserved under `InputN` with reciprocal widget ownership; unsupported mandatory seed or appearance refuses at zero bytes including a bad later input; real CMS signs the exact retained full name and leaves other occurrences untouched; pypdf, qpdf, pyHanko inspect | not estimated in W-194 (gap: first item must estimate) | T-06, T-11, D-C |
| T-15 | Explicit unsigned derivative (W-197 P197-B) | `PdfAssemblyOptions.cs`, `PdfOmissionReason.cs`, assembly paths, same tests | With `AllowUnsignedDerivative`: approval, timestamp (and per D-A rights) inputs accepted, counted per occurrence, output has no signature dictionary, `/Perms` or signed widget; certified, locked, malformed inputs still refuse; encrypted inputs still need both consents; real engine-signed source unchanged | in the 7 to 11 above | T-06, D-A, D-B |
| T-16 (contingent) | Include-only `/Lock` support with FieldMDP creation at signing (W-194 P194-B) | `Internal/PdfSigner.cs`, write-gate and field-restriction paths, copy/remap policy, tests | Needs D-C (supersedes W-151 no-new-transform for this case). Source and preserved fields sign with a FieldMDP Reference local to the occurrence; All/Exclude/P refuse; fill and flatten of locked target refuse after reopen | not estimated in W-194 | T-14, D-C |

### P7 navigation tree

| Ticket | Outcome | Candidate paths | Acceptance | Effort | Depends |
| --- | --- | --- | --- | --- | --- |
| T-17 | Ordinary outline projection (W-195 Rule A) | new `PdfAssemblyOutlines.cs`, extractor, omission enum, tests | Findings trees reproduced; every R195-2 case refuses with zero bytes; qpdf, pypdf, MuPDF agree | 3 to 6 days | T-08, T-06 |
| T-18 | Page-label preservation (W-196 P) | engine label-tree reader/projector under `Internal/`, tests, small original fixtures, docs | Label sequences correct through both terminals including nested trees, repeats, reordering, prefix-only/empty; malformed/cyclic/overflow refuse with unchanged destination; two independent readers incl. one other than pypdf | 3 to 6 days | none (separate from tags and outlines) |

### P8 tags

| Ticket | Outcome | Candidate paths | Acceptance | Effort | Depends |
| --- | --- | --- | --- | --- | --- |
| T-19 (optional) | Transitional tag admission refusal (W-191 P1) | `PdfAssemblyInspection.cs`, caller, tests | Any StructTreeRoot, `MarkInfo.Marked true` or parent key in scope refuses before bytes, including a later occurrence; converts 8 of 30 accepted corpus inputs to refusals | 1 to 2 days | T-06; see C8 and D-D |
| T-20 | Page-local tag preservation (W-191 P2) | new internal tag importer, extractor, inspection, fixtures, docs | Ten probes become permanent support/refusal cases; fresh ParentTree keys, MCID slots, annotation ownership, languages; mixed tagged/untagged refuses; independent reader and manual reading-order inspection; no PDF/UA claim | 7 to 12 days | T-11, T-07, T-06 |
| T-21 (deferred) | Explicit untagged derivative (W-191 P3) | small assembly option/terminal | Named loss only; never a fallback after preservation fails | 2 to 4 days | real use case, T-06 |

## 5. Rights, XFA, metadata

| Ticket | Outcome | Candidate paths | Acceptance | Effort | Depends |
| --- | --- | --- | --- | --- | --- |
| T-22 | Explicit unsigned usage-rights removal and ordinary-write gate (W-199 P1) | `PdfDocument.cs`, `PdfDocumentEditor.cs`, new `PdfUsageRightsPreparationRequiredException.cs`, engine files named in W-199, `UsageRightsTests.cs`, exact AGENTS/spec/D-44/45/48/QUALIFICATION edits, docs | Full list in `W-199/proposal.md`; first-signature refusal before host call; refused metadata call restores state (C10) | 2 to 4 days + 1 to 2 joint with T-23 | D-A; jointly with T-23 |
| T-23 | Explicit `AbandonXfa` (W-198) | `PdfAcroFormEditor.cs`, `PdfEngine.cs`, `PdfFieldTree.cs`, `PdfFormFiller.cs`, `PdfFlattener.cs`, `PdfAssemblyInspection.cs`, tests | Per `W-198/proposal.md`; fill/flatten of hybrid refuse until prepared; rollback restores pre-call state | 2 to 4 days + joint | T-22 |
| T-25 | Atomic metadata setters (W-200) | `Internal/PdfInfoMetadata.cs`, `Metadata/XmpEditor.cs`, `Metadata/PdfMetadataEditor.cs`, metadata tests, workflow metadata test, docs | U+0001 reproduction becomes a permanent failing regression (state unchanged on refusal); mappings, x-default, singleton authors, custom namespaces; offset-date equivalence; P1/P2/P3 and permission cases; workflow refusal publishes nothing | 5.5 to 9 days | none; coordinate T-22 |

## 6. Research and record tickets (no product change)

| Ticket | Outcome | Paths | Effort |
| --- | --- | --- | --- |
| T-26 | Explain `ExtractPages` read amplification (12,523 reads for 2 pages of a 12,000-page file) | `agents/reference/simplification/T-26/` equivalent folder | research, 1 to 2 days (W-211 inference) |
| T-27 | ASCII85 strictness (group above 2^32 - 1, lone final character) and unbounded ASCII85/ASCIIHex array decoding (424 MiB to refuse 32 MiB) | `Internal/PdfStreamDecoder.cs`, tests; coordinate W-149 and W-168 | not estimated by W-209; a confirmed defect needs a regression |
| T-28 | Record notes: W-138 (W-208 section 2), W-109 (W-208 section 3), W-156 (W-197 disposition), W-158 (W-210 replacement text) | the four work-item files, Notes only (dated appendages; frozen records excluded) | under 0.5 day |

## 7. Not recommended or contingent (priced, not ticketed)

| Option | Source | Why not now | Cost if chosen |
| --- | --- | --- | --- |
| Candidate N: hide `IPdfSignatureProvider` and `PdfSignatureContext` (3 tickets: raw entry points, migrate 45 files, integration checkpoint) | W-202 | Loses complete-CMS services, custom attributes and externally assembled two-phase signing; migration not free; no external-consumer evidence | 1 to 2 + 2 to 4 + 1 to 2 days |
| R203 mandatory-seed refusal | W-203 | Loses 12 accepted CMS examples and 3 compatible timestamp preflights of 28 fixtures; saves 182 physical lines and 28 lexical `if`s | one item on `PdfSignatureSeedValues.cs`, net10/net48 focused |
| Decoder tiers B, C, D | W-209 | Zero corpus use is not zero demand; refusal at xref/object-stream site makes the document unreadable | 0.5 / 0.5 to 1 / 0.5 to 1 day |
| Forensics cuts A1+, A2, B | W-210 | Trust boundary text must stay; pricing only | 1 to 2 / 2 to 3 / 2 to 4 days |
| Label alternative N (omit labels) | W-196 | O 03 allows either; recommendation is P | smaller than P, loses labels |
| Duplicate-fill rejection or global declaration order | W-204 | Behavior change; evidence supports retain | needs exact rules and migration |
| Remove alias safeguards | W-206 | Measured loss of protection | multi-day plus platform work |
| Remove async path | W-208 | Contradicts R13, W20, D-55 | 1 to 2 days public-only; reversing D-55 multi-day to multi-week |

## 8. Effort summary (gross, inferred)

Sum of source ranges for estimable tickets: T-01 to T-02 0.6 to 1.1; T-03 to T-05 1.25 to 3; T-06 with T-15 7 to 11; T-07 to T-13 gross 14 to 30 (W-190 2 to 5, W-192 5 to 11, W-193 7 to 14, before removing the overlap in C5 and C6); T-17 and T-18 6 to 12; T-19 to T-20 8 to 14 (T-21 2 to 4 more); T-22 with T-23 5 to 10; T-25 5.5 to 9. Total about 47 to 90 engineer-days **gross**, excluding T-14, T-16, T-26, T-27 (no source estimate), T-21 and the optional Combine preparation route (W-198, 2 to 4). Overlap reductions are real but not quantified in the sources: shared refusal frame (T-06 serves T-07, T-08, T-10, T-11, T-14, T-17, T-20), shared field/annotation mapping (T-07, T-11, T-14), shared docs edits. Treat the total as an upper bound, not a plan. All figures are inference from code size and test scope; none was timed.
