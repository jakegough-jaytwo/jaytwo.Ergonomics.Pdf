# W-200 — ordinary metadata editing research

Research baseline: `f59997da3b2af86f94b281daa3e6abb1ea86d9e6` (W-199 committed before W-200 activation). Research date: 2026-10-26, America/Denver. One agent; no independent reviewer or owner adoption is claimed. The pre-existing untracked `W-196/labels.js` was left alone. No runtime, normal test-suite, protected-file or shipped-contract changes are included.

## Recommendation and business effect

Keep automatic creation of missing XMP for ordinary Info edits. Change synchronized setters to prepare a bounded, validated candidate and commit the Info value, corresponding XMP value, automatic modification timestamp and incidental catalog/rights changes together. Refuse an uneditable existing packet instead of reporting success with divergent stores. Preserve raw `SetXmp` as explicitly independent packet authoring. Exact candidate wording and an implementation draft are in [proposal.md](proposal.md); none is shipped or approved by this research.

The business case is reliable title/author/catalogue updates through one call, regardless of which metadata store a downstream indexer reads. Keeping creation avoids a new caller choice, raw XML construction and a two-call recovery path on Info-only business PDFs. It retains today's 1.4 catalog-version side effect on older PDFs. The alternative of Info-only writes when XMP is absent matches common lower-level APIs but saves only the minimal-packet branch and changes existing behavior. It does not remove the synchronizer, parsing or rollback responsibilities when XMP exists. No measured customer demand justifies that migration here.

The deliberate new refusal affects malformed, DTD-bearing, non-RDF, oversized, undecodable or ambiguous *existing* XMP. Recovery is to have an authorized host supply a complete corrected packet through `SetXmp`, then retry the metadata call; permission and certification checks still apply. The library cannot repair arbitrary RDF, recover lost language values or bypass an unavailable owner password. An application can continue read-only inspection of Info. Raw replacement can deliberately lose metadata: the host must preserve any required custom/profile data. Incremental replacement does not erase previous packets from historical revisions.

## Authority and standards

Authority is AGENTS.md, adopted specs, ACCEPTANCE.md, docs/behavior.md, QUALIFICATION.md, reset-plan.md, then agents records. D-66 authorizes research and proposals only. W-188's missing four review attachments remain unavailable: the disagreement in W-200's problem is a charter summary, not independently verified statements from those attachments.

Current governing anchors:

| Source | Binding scope / proposed disposition |
| --- | --- |
| phase-1 §6.3 W7 | Info writes refresh ModDate and synchronize matching XMP. Replace only after Gate 2 with the precise atomic contract. |
| ACCEPTANCE Metadata, especially third bullet | Explicitly ships absent-XMP creation and successful Info editing beside malformed/non-RDF packets. Proposed refusal supersedes this exception; it is not labeled a new regression against that exception. |
| docs/behavior Metadata row; engine guarantees; phase-1 §7.2/§7.3 | XML/resource bounds, certification, permissions, pending edits, signatures and rights remain binding. W-199's rights-first policy remains unadopted and is not implemented by W-200. |
| phase-2 preservation matrix, §4.2 and A4; D-61 | Assembly creates a separate new identity with minimal Info, no input XMP and a new ID. Ordinary edits do not inherit or change that policy. |
| D-54/D-56 and version behavior | Engine owns invariants; incremental prefix and xref format remain; header unchanged, catalog version never raised past 1.7. |

The owner's local reference library was read, without modification or copying its licensed text into this repo:

* ISO 32000-1:2008 §14.3.1–3, Table 317, PDF pages 556–559: optional Info, XMP available from 1.4 and preferred; metadata timestamp/Info modification-date precedence exists for readers. This does not require creation of missing XMP on every write.
* ISO 32000-2:2020 EC3 §14.3.1–4, Tables 347/349, PDF pages 729–734: Info other than creation/modification dates is deprecated, not prohibited; XMP supports localized titles and multiple authors. The normative mapping notes use `dc:title`, `dc:creator`, `dc:description`, `pdf:Keywords`, `xmp:CreatorTool`, `pdf:Producer`, `xmp:CreateDate`, `xmp:ModifyDate`. Erratum 296 specifies UTF-8 XMP output. §14.3.4 requires equivalent modification instants when both are written and recommends leaving existing conflicting creation dates alone on unrelated modifications.
* Dates: §7.9.4 in each edition, located via `text/clauses.tsv`. This work retains typed DateTimeOffset and the existing accepted full PDF-date syntax, not a new general partial-date parser.

Local citations: `C:/jake/git/pdfdocs/text/iso-32000-1/285-14-3-metadata.md`, `.../iso-32000-2/361-14-3-metadata.md`; authoritative originals are `pdfs/PDF32000_2008.pdf` and `pdfs/ISO_32000-2_sponsored_EC3.pdf`. The scalar/list and refusal choices in the proposal are product policies, not a claim that ISO mandates this API. This study does not silently adopt Aspose's PDF-2.0-specific storage policy.

## Pinned precedent and limits

| Library / operation | Pin and evidence | Result / relevance |
| --- | --- | --- |
| PDFBox `PDDocumentInformation.setTitle`, ordinary `save` | Installed PDFBox and pdfbox-io 3.0.5 from the pinned DSS installation; SHA-256 in tool-hashes.json. OpenJDK 21.0.9+10. Eleven shared fixtures, `MetadataProbe.java`, pdfbox.txt, inspection.json; installed bytecode in pdfbox-bytecode.txt. | Sets Info; leaves decoded XMP bytes unchanged, including absent, invalid and conflicting packets. Not a synchronized-setter precedent. qpdf exit 3 on all 11 outputs: missing self-entry for the xref stream. These outputs were readable by pypdf; they are **not clean qpdf passes**. |
| pypdf `PdfWriter(clone_from=...).add_metadata` | 6.21.1; eleven same fixtures; pinned [writer source](https://raw.githubusercontent.com/py-pdf/pypdf/6.21.1/pypdf/_writer.py) and [metadata guide](https://pypdf.readthedocs.io/en/6.21.1/user/metadata.html). | Info-only edit preserves XMP bytes or absence. Separate XMP authoring API is documented. This narrower API leaves consistency to the caller. |
| qpdf rewrite with no transformations | 12.3.2; eleven same fixtures; [versioned CLI](https://qpdf.readthedocs.io/en/12.3/cli.html), [pinned manual source](https://raw.githubusercontent.com/qpdf/qpdf/v12.3.2/manual/cli.rst). | Retains Info/XMP including malformed content. This is a preservation control, not a high-level title setter. Its documented separate Info/Metadata removal switches do not establish atomic synchronization. |
| Aspose.PDF for .NET `Document.Info` / `Document.Metadata` | Documentation retrieved 2026-10-26: [metadata guide](https://docs.aspose.com/pdf/net/pdf-file-metadata/), [DocumentInfo API](https://reference.aspose.com/pdf/net/aspose.pdf/documentinfo/); [26.10 release notes](https://releases.aspose.com/pdf/net/release-notes/2026/aspose-pdf-for-net-26-10-release-notes/) identify a concrete candidate release. | Guide distinguishes PDF 1.x Info storage from PDF 2.0 routing into XMP. No installed licensed commercial binary or source; no fixture executions, absent/malformed atomicity result, or claim that the unversioned guide is pinned to 26.10. This licensed/version-specific comparison is explicitly unavailable. Aspose FOSS is a different product and is not substituted. |

PDFBox 3.0.5 web source/Javadoc URLs could not be fetched; the 3.0.6 source was accessible but is not used as evidence for the installed 3.0.5 behavior. Bytecode directly confirms the installed setter only calls the Info dictionary string setter. Source availability and commercial licensing limit claims; no library vote establishes the best product contract. No external package was added to either library.

## Reproduction and measured results

Run from repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File agents/reference/simplification/W-200/run.ps1
```

Prerequisites are existing `.tools/test.runsettings`, Python/pypdf, qpdf, DSS's PDFBox jars, its JDK, and SDK 10.0.402. The runner makes disposable PDFs/projects under ignored `out/W-200`. `inputs.json` records hashes and decoded initial states; encryption is randomized and its hash changes on regeneration. Archived JSON describes the final run, not deterministic wall-clock timestamps or encrypted bytes. `engine.json` separates operation exceptions from save results; an accepted save after a refused operation is a copy, not a successful metadata write. `inspection.json` records pypdf structural/XML observations and qpdf diagnostics. No proposed synchronizer is implemented in the probes.

Eleven shared synthetic fixtures: neither store; Info only; XMP only; matching stores; conflicting title; malformed XML; well-formed XML without RDF; custom namespace with nested data/attributes; multilingual title with French preceding x-default; duplicate title properties; DTD/external entity declaration. Three additional synthetic DocMDP P1/P2/P3 fixtures test gates only; a qpdf AES-256 restricted fixture is exercised as user and owner. These certification dictionaries are not real CMS evidence and make no cryptographic or validator claim.

The six ordinary probes are set title, remove title, replace author string, explicit +05:30 ModDate followed by title, raw well-formed non-RDF replacement, and refused DTD replacement after a pending custom-Info edit. A seventh probe supplies a title containing XML-illegal U+0001 to observe failure atomicity. Counts and final results are in summary.json. The source is unchanged and destinations have zero bytes at every setter return/exception. Every saved output has the original prefix, including copies after refusals.

Final run: 112 engine runs, 108 saves and four outer refusals (the preparatory custom edit in raw-invalid is blocked by the three certifications and the restricted user role). There are 45 caught operation exceptions; a caught exception is followed by save to inspect what remained pending, so 108 saves is not an operation pass count. Eleven PDFBox, eleven pypdf and eleven qpdf outputs bring inspection to 141 files: 130 clean qpdf checks and the 11 PDFBox warnings described above. These denominators describe this synthetic experiment only.

Principal observed facts:

* Missing XMP is created by ordinary setters, with catalog `/Version /1.4` on the 1.3 header fixtures; the header stays 1.3. XMP-only input gets an Info dictionary on edit. Unrelated missing properties are not globally backfilled.
* Malformed XML, DTD-bearing XML and non-RDF XML keep their exact decoded bytes while Info title changes successfully. Raw `SetXmp` accepts well-formed `<raw>packet</raw>` without RDF, leaves Info alone, and rejects DTD packets. A rejected raw packet preserves the preceding pending `Department=pending` edit.
* In `multilingual`, a title assignment changes the French first item and leaves x-default as Old. In `duplicate`, it updates one title and leaves the other as Other. Replacing Author changes the first sequence item to the whole literal `Ada, Bob; Company` and leaves the second Bob item. These are reproducible representational hazards; the detailed scalar policy was not previously specified. The proposal removes this ambiguity rather than presenting it as an already shipped guarantee.
* The +05:30 explicit ModDate survives a later title edit. Info keeps its offset and XMP uses the equivalent UTC instant (`2024-02-02T22:35:06Z`). The custom-namespace subtree compares equal semantically after title editing; serialization identity of the entire packet is not promised.
* With well-formed XMP, a title containing U+0001 throws `ArgumentException` during XML serialization **after** Info has changed. For `consistent`, the following save writes that new Info title alongside unchanged old XMP. The retained assertion in research.py proves this failure-state hazard. With malformed/non-RDF XMP, the same string bypasses serialization and the setter succeeds. This is a documented current limitation and a concrete reason for the proposed atomicity requirement; this study does not claim the new per-setter guarantee was previously shipped. The implementation draft explicitly requires a permanent fixed-behavior regression test, not this observation assertion as proof of a fix.
* P1/P2/P3 block ordinary and raw metadata writes with `PdfCertificationLockedException`; AES-256 user modify denial throws `PdfEncryptionRestrictedException`, owner succeeds. Reads and subsequent no-change saves remain available. The U+0001 and raw-invalid preparatory edit are also subject to these gates.

These bounded observations do not measure corpus-wide refusal prevalence, large-packet peak allocation, all encodings/RDF forms, real signed-document classification, workflow file publication faults, or adversarial XML work. Existing tests were read for shipped limits; they were not rerun or called new evidence. The proposal's output-size limit, complete rollback and all RDF-shape refusals still need implementation tests. Assembly is excluded. No rendering, PDF/A/UA/X conformance, signature trust or production qualification is claimed.

## Responsibility and cost

At baseline PdfInfoMetadata.cs is 338 lines and XmpEditor.cs 239, including whitespace/comments; these are context sizes, not removable-line estimates. Retained: metadata discovery/decoding, eight mappings, Info dates/text/name encoding, minimal XMP authoring, secure XML parser/serializer, encryption and version handling, permission/certification/signature gates, incremental writer and workflow publication. Raw packet validation remains a separate responsibility. W-199 owns any adopted rights-policy simplification; W-198 owns XFA. Their shared rollback/gate costs must be counted once.

Delete the successful-divergence fallback in SyncXmp and first-item mutation assumptions; replace, rather than count as removed, that work with targeted subject/property selection, scalar/container validation, x-default editing, complete author-sequence replacement, candidate-output cap checks and per-call transaction state. Reuse W-186's existing store checkpoint. Include `_modDateExplicit` and `_touchingModDate` in rollback reasoning; a store snapshot alone does not prove these private flags reset. Unknown RDF remains opaque; this does not require a general RDF engine, XMP reader precedence redesign, partial-date API or multilingual public API.

Estimated implementation: 3–5 engineer days for candidate preparation/target resolution and transaction wiring, 2–3 for focused engine/workflow regression and independent checks, 0.5–1 for adopted wording/public docs: roughly 5.5–9 days total, inferred from two existing implementation files plus editor/workflow tests. No throughput or LOC saving is measured. Gross deletion is two best-effort fallback paths and replacement of three first-item helpers, with a likely **net addition** of validation responsibility. Independent review and any new general RDF support are outside that estimate. A broader XMP toolkit would need separate scope approval.

The owner can approve the contract and implementation draft independently of other simplifications. Missing commercial experiments/review attachments remain evidence gaps; they block claims about those products/reviews, not the measured current behavior. No active-queue insertion or product implementation is authorized by this closeout.
