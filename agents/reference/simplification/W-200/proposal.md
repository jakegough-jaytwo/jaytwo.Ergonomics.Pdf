# W-200 — exact unadopted contract and implementation draft

Status: proposal for Jake's Gate 2 review, 2026-10-26. This file changes no authority, requirement status, API or shipped behavior. Evidence and limits: [findings.md](findings.md), [summary.json](summary.json). Keep missing-XMP creation; adopt per-call atomic synchronization and the bounded scalar mappings below. This is ordinary incremental editing, separate from assembly identity.

## 1. Proposed phase-1 §6.3 wording

Replace the W7 requirement text with the following at owner-approved adoption (use `Change` until implementation ships, then `Exists` in the implementing transaction):

> Write document information keys and a raw XMP packet. Each ordinary information-key setter atomically prepares and commits the requested key, its mapped XMP property and the modification date. Missing Info and XMP are created as needed. Existing XMP that cannot be safely synchronized causes refusal without changing pending document state. A raw SetXmp call replaces only the packet and does not synchronize Info or refresh modification dates. The mappings and failure rules below apply.

Append these exact paragraphs under W7:

> Ordinary setters are the named metadata properties and the information-key indexer, including custom keys and Trapped. A successful setter updates ModDate to the current UTC time to whole-second precision, together with xmp:ModifyDate, unless ModificationDate/ModDate was explicitly assigned earlier in the current pending editing state. An explicit modification date, including null, remains authoritative for later ordinary setters. A failed date assignment does not set or clear that explicit-date state. CreationDate is changed only by an explicit creation-date assignment. Untouched creation-date conflicts are not reconciled automatically. Creator and Producer change only when assigned.

> Explicit modification-date state lasts for the lifetime of the open PdfDocument, including successive editors and successful saves or signatures, matching the existing later-edit override behavior. Reopening the saved document starts a new automatic-date state. A failed operation restores the prior explicit-date state; disposing an editor neither saves nor clears pending edits.

> A missing information dictionary is created. Missing XMP is created as a minimal document packet, containing the assigned mapped property when any and the resulting modification date when any. Other existing Info values are not automatically copied into the new packet. An unmapped custom key or Trapped changes Info and the modification date only. No arbitrary custom Info key is translated into an XMP namespace. Empty strings remain explicit empty strings; null removes the assigned property. A caller removing the final key may leave empty metadata structures.

> Existing conflicts in an assigned mapped property are resolved by the caller's new value, in both stores. Untouched properties, including conflicting creation dates, remain unchanged. The setters are not a global metadata-reconciliation service. Metadata reads retain their existing store-specific behavior; this change adds no XMP-to-Info getter fallback or merged metadata view.

Mapping table to append:

| Info key | XMP expanded name | Setter semantics |
| --- | --- | --- |
| Title | `{http://purl.org/dc/elements/1.1/}title` | Lang Alt: set or add `rdf:li xml:lang="x-default"`; preserve other language items. |
| Subject | `{http://purl.org/dc/elements/1.1/}description` | Same Lang Alt policy as Title. |
| Author | `{http://purl.org/dc/elements/1.1/}creator` | Replace the ordered sequence with one item containing the entire assigned string. Do not split commas or semicolons. |
| Keywords | `{http://ns.adobe.com/pdf/1.3/}Keywords` | One literal string; do not tokenize it or update `dc:subject`. |
| Creator | `{http://ns.adobe.com/xap/1.0/}CreatorTool` | One literal string. |
| Producer | `{http://ns.adobe.com/pdf/1.3/}Producer` | One literal string. |
| CreationDate | `{http://ns.adobe.com/xap/1.0/}CreateDate` | Same whole-second instant as the assigned PDF date. |
| ModDate | `{http://ns.adobe.com/xap/1.0/}ModifyDate` | Same whole-second instant as the assigned or automatic PDF date. |
| Trapped | No mapping in this API | Validate True/False/Unknown and store a PDF name; synchronize only the resulting ModDate. |
| Any other Info key | No mapping | Text string in Info; synchronize only the resulting ModDate. |

Append after the table:

> PDF dates use D:YYYYMMDDHHmmSSZ or D:YYYYMMDDHHmmSS±HH'mm'. Typed DateTimeOffset values retain their offset in Info, and XMP emits the equivalent UTC yyyy-MM-ddTHH:mm:ssZ value; both discard subsecond precision. The key indexer accepts the existing complete PDF-date syntax. Invalid date strings are refused before changes. Partial-date parsing is not extended. Null CreationDate removes CreateDate; null ModDate removes ModifyDate and suppresses automatic replacement until that explicit pending-date state is changed.

> Scalar Title and Subject assignments preserve translations, including translations equal to the old default. They do not infer a translation relationship. Null removes the entire corresponding property from both stores, including all language alternatives. Null Author removes the entire creator sequence. Replacing Author deliberately replaces any previous multi-author list with one literal item. A caller requiring multilingual or multi-author authoring supplies a complete raw XMP packet and is responsible for any intentionally different Info representation.

> For synchronization, the packet must have exactly one rdf:RDF under its xmpmeta root or as its root, with document-subject rdf:Description elements whose rdf:about is empty or omitted. Descriptions for other subjects are preserved and are not candidates. Equivalent expanded namespace names are used regardless of XML prefix. A missing document description may be added. For each property touched by this call, at most one occurrence across all document descriptions is allowed; element-plus-attribute and duplicate elements are ambiguous and are refused, even when their values match. Unrelated properties are not normalized or validated as a general RDF graph.

> Title and Subject must be a simple rdf:Alt of text rdf:li items with unique nonempty xml:lang values and at most one x-default item. Author must be a simple rdf:Seq of text rdf:li items. Simple properties may be a text element or a description attribute, but not both. A missing property/container may be created. An existing target with qualifiers, a resource reference, unexpected nested content, an unsupported container, duplicate language items or any other shape the setter cannot preserve is refused rather than silently simplified. Null assignments use the same target validation. Existing target properties on non-document subjects do not satisfy the assigned document property.

> Preserve unrelated elements, attributes, namespaces and language alternatives semantically; serialization, whitespace, prefix selection, packet padding and XML byte identity may change. Do not update profile declarations, custom namespaces, xmp:MetadataDate, xmpMM identity or history as an inferred side effect. Adding or changing XMP does not establish PDF/A, PDF/UA, PDF/X or any other conformance. The incremental header is unchanged; the catalog version is raised only as required by the existing version policy, at least to 1.4 when XMP is added and never above 1.7. Existing PDF 2.0 files are not downgraded; this API does not migrate or delete deprecated Info keys.

> Malformed XML, DTDs, external resolution, undecodable packets, missing RDF, ambiguous touched properties, unsupported touched RDF shapes, invalid XML characters in values to be written into XMP, or a candidate packet exceeding 32 MiB are refused. XML input retains the existing 32 MiB byte and character limits and prohibits DTDs and external resolution. UTF-8 and UTF-16 input remain readable; synchronized packet output is UTF-8. Bound the serialized candidate as well as the decoded input. No whole-document buffering or repair scan is introduced.

> PdfXmpException identifies malformed or unsupported synchronization and XML-character or packet-size failures, with a message identifying the affected property/reason. Existing argument exceptions remain for invalid key, date or Trapped arguments, PdfEncryptionRestrictedException for permission denial and PdfCertificationLockedException for certification. Existing source/decompression/filter failures retain their exception types. There is no fallback that reports success while changing only one store. Failure of a setter leaves all earlier pending edits, dirty state, object allocations, catalog version, usage rights and explicit modification-date state exactly as before that call. Each setter writes no destination bytes. Atomicity is per setter, not a new batch API: earlier successful assignments remain pending when a later assignment fails.

> Raw SetXmp validates XML security, input size and well-formedness and stores the caller's bytes exactly, without requiring synchronization-compatible RDF or changing Info/ModDate. It may create absent XMP; it retains existing version, encryption, permission and certification handling. A refused raw packet changes no pending state. The caller owns raw-packet semantics, namespaces and consistency; a subsequent ordinary setter can refuse an otherwise well-formed raw packet that it cannot synchronize.

These shape limits intentionally trade some existing successful edits for predictable refusal. Before implementation, review real-corpus consequences of strict targeted RDF handling and include at least attribute/simple, split-description, qualified-target and duplicate fixtures. The proposal is a bounded scalar editor, not an implementation of all XMP serialization forms. No measured corpus refusal rate is supplied here.

## 2. Proposed ACCEPTANCE.md Metadata replacement

Keep its first bullet (encoding, keys, custom data, Creator/Producer and Trapped). Keep its second bullet (date formats, explicit date and UTC refresh). Replace its third bullet with:

> With no XMP, an ordinary info edit creates a minimal packet and updates the assigned mapped dc/pdf/xmp property and the resulting modification date; it does not backfill unrelated Info values. An XMP-only document gains Info on assignment. Both stores show the assigned mapped value and equivalent ModDate instants after save. UTF-8 and UTF-16 input packets are supported; synchronized output is UTF-8. Title/Subject update x-default and preserve translations; null removes the entire mapped property. Author is replaced as one literal sequence item. Keywords are not tokenized. Existing targeted conflicts are resolved by the assigned value; unrelated properties and custom namespaces survive semantically. Malformed XML, DTDs, absent RDF, ambiguous or unsupported touched RDF shapes, invalid XML characters, and input or serialized output beyond 32 MiB refuse without changing earlier pending state, usage rights, catalog version or explicit-date flags. Later save after refusal publishes only previously successful changes. Raw SetXmp stores the supplied XML bytes without Info synchronization or ModDate refresh; malformed, DTD-bearing and oversized raw packets throw PdfXmpException without changing pending state. Neither setter writes output before save. Permissions and every DocMDP certification level still block metadata changes.

Implementation must update the existing malformed/non-RDF-success tests to the owner-adopted refusal behavior in the same transaction. Do not describe those old acceptance cases as regressions accidentally fixed.

## 3. Proposed docs/behavior.md wording

Replace only the Metadata row's Write cell with:

> Info setters update the assigned mapped XMP property and the modification date atomically. Missing stores are created; malformed or unsupported existing XMP refuses with no change to pending edits. Title/Subject edit x-default and preserve translations; null removes the whole mapped property. Author becomes one literal ordered item, and Keywords remains a literal string. Unrelated XMP survives semantically. Explicit modification dates survive later info edits. Secure XML and both input and serialized-output limits are 32 MiB. Raw SetXmp stores well-formed packet bytes independently of Info and ModDate. Its caller owns packet semantics and consistency. Certified files block all metadata writes, and user-password modification permission is required. Adding XMP follows the existing PDF-version policy. No profile-conformance claim is made.

Add immediately after the compatibility table:

> Metadata synchronization follows the phase-1 §6.3 mappings and supported RDF shapes. Repair or deliberately replace an uneditable packet with SetXmp before retrying an ordinary setter. That recovery still requires write authority and can discard custom data if the caller omits it. Replacing a packet incrementally does not remove its historical bytes. An ordinary edit preserves existing document identity; extraction and concatenation retain their separate new-identity rules.

## 4. Other governing files and API docs

No AGENTS.md or closed-decision wording changes are needed: the proposal preserves engine ownership, secure limits, dependencies, signature/certification/permission guarantees, incremental behavior and source ownership. D-66 remains a research approval only; create a distinct adoption decision if Jake accepts this proposal. D-61/phase-2 A4 are unchanged. No QUALIFICATION.md or skill restates the superseded malformed-packet success exception, so no restatement edit is proposed. Keep existing evidence historical; do not change qualification status.

On adoption, named `PdfMetadataEditor` property docs and the indexer remarks should use this exact shared summary:

> Assigns the Info value and mapped XMP property atomically, creating missing metadata as needed. Existing XMP that cannot be synchronized throws PdfXmpException and leaves pending edits unchanged. Scalar Title and Subject assignments update x-default; assigning null removes the whole mapped property. Author is one literal item. See the metadata contract for supported RDF shapes and modification-date handling.

`SetXmp` remarks should read:

> Replaces only the packet with the supplied well-formed XML bytes. Does not update Info or modification dates. The caller is responsible for RDF semantics and cross-store consistency. A later ordinary metadata edit may refuse a packet it cannot synchronize.

Public examples should show a normal Title assignment/save and, separately, a host-supplied complete packet followed by retry. Do not provide a generic packet-repair algorithm or imply credentials are bypassed. Workflow `WithInfo` inherits engine refusal; existing private staging and path publication guarantees remain. No fluent API or additional option is proposed.

## 5. Focused follow-up implementation draft (not an active work item)

Suggested title: **Atomically synchronize ordinary metadata setters**. Owner adoption is a prerequisite; no ID, queue insertion or implementation is authorized here. One item can include core engine handling and its thin workflow behavior. Load pdf-core-change and pdf-change-verification when implementation starts. Candidate allowed paths, to confirm at activation:

* `src/jaytwo.Pdf/Internal/PdfInfoMetadata.cs`, `src/jaytwo.Pdf/Metadata/XmpEditor.cs`, `src/jaytwo.Pdf/Metadata/PdfMetadataEditor.cs` and any narrowly required existing checkpoint owner.
* `test/jaytwo.Pdf.Tests/MetadataTests.cs` plus focused helper files; `test/jaytwo.Ergonomics.Pdf.Tests/WorkflowMetadataTests.cs` or the actual existing metadata workflow test file located through graphify.
* The owner-approved spec status/wording, ACCEPTANCE.md and docs/behavior.md edits above, and public example location if an example changes. Protected wording must be approved, not inferred from this draft.

Implement candidate validation/serialization before state publication; reuse existing object-store checkpoints for exceptions during publication. Cover metadata-local explicit-date flags too. Do not change source APIs, signing, assembly, general source loading or workflow ordering. Coordinate any adopted W-199 explicit-rights preparation rather than counting gate/rollback work twice. Prior to its adoption retain the current UR3 policy, but any refused metadata call must restore it.

Required focused checks:

1. Turn the retained U+0001 reproduction into a permanent regression that fails on this baseline: exception with unchanged Info/XMP, dirty state and preceding custom edit. Add one failure after candidate serialization to test rollback at publication, including version/rights and explicit ModDate state.
2. Use the same eleven fixture families; assert new absent/malformed behavior, exact targeted mappings, x-default selection, translation preservation/removal, singleton authors, custom-namespace preservation and duplicate/qualified-target refusal. Extend only for distinct RDF encoding risks (attribute, split description, non-document subject), not a permutation matrix.
3. Date equivalence at a nonzero offset and whole-second precision, explicit null/date behavior after a later failed assignment, existing untouched creation-date conflict, and serialized output exceeding the cap despite bounded input. DTD/external entity and UTF-16-input/UTF-8-output checks remain.
4. User/owner modification permission and P1/P2/P3 refusal; an existing real approval signature/document timestamp remains a prefix and retains existing permission semantics, without a new trust claim. A raw replacement remains exact and independently authored.
5. A workflow refusal never publishes a path and preserves caller source/destination guarantees; a successful normal workflow has the same values as direct engine editing. Exercise pending-first-edit/later-refusal separately from file publication.
6. Focused net10.0 build/tests with StyleCop and XML docs; independent pypdf value/XML checks plus qpdf structural checks on engine outputs. No full matrix, fuzz campaign or Docker gate unless the resulting item is cross-cutting/framework-sensitive or a named checkpoint.

Acceptance is the adopted precise behavior, not a particular helper layout or deletion count. No performance, corpus-compatibility or production-qualification claim follows from the research's synthetic fixture results. Independent reviewer availability, licensed Aspose runs and the original four review attachments remain explicit evidence limitations.
