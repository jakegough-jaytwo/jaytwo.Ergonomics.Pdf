# Document information and XMP metadata

**Adopted subject contract, revision W-218-METADATA-2026-10-31, approved by the owner on 2026-10-31.** It consolidates the owner-selected metadata decisions of 2026-10-27 and resolves the remaining representation, input, lifetime, failure and acceptance details by engineering judgment. It is a target contract: it does not claim the current implementation already meets every clause, and it changes no behavior, acceptance or qualification record. Authority: [charter](../charter.md) > [supported capabilities](../supported.md) ("User metadata": document information, authorship and custom key/value properties, and XMP) > adopted contracts. Current behavior ([behavior](../behavior.md)) and tests are comparison evidence.

Applicable shared rules: [shared](shared.md). Authorization: [security](security.md). Saving and pending state: [opening and saving](opening-and-saving.md). Workflow defaults and ordering: [workflows](workflows.md).

## Model

A document has two metadata stores: the **information dictionary** (Info) and the **XMP packet**. They are read separately and written together for mapped properties. Edits are pending document state, visible to reads, and reach output only at an explicit save. The existing public surface (named properties where `null` removes a value, a string key indexer, `SetXmp`, `OpenXmp`, `HasXmp`, `Keys`) is preserved; additions are additive and must not change the outcome of an existing call.

## Reading: METADATA-READ-01

Info getters report Info only. They do not merge stores, fall back to XMP when an Info value is absent, or choose between conflicting stores; an XMP-only document may report an absent Info Title. XMP is read separately as the packet bytes after PDF stream decoding (UTF-8 or UTF-16 with byte-order mark), without merging or mutation. Absent is reported as absent; a malformed packet or unsupported requested representation is a controlled error ([`PdfXmpException`](shared.md#shared-fail-01-failure-categories) for XMP), never fabricated absence. Opening never fails because XMP is malformed. Reads need no write permission, observe cancellation before work and while decoding, return no partial result on failure, and leave pending state unchanged. Decoded packet size is limited as in METADATA-RAW-03, measured on decoded bytes, not stored length.

## Inputs: METADATA-INPUT-01

* **Values** are Unicode text that must be well-formed (unpaired surrogates are an invalid argument). Empty text is a value; `null` (or the clear action of an additive API) removes the property. Missing and present-empty are distinguishable on read.
* **Dates** are typed offset dates. Whole seconds are kept; fractional seconds are discarded without rounding (METADATA-DATE-05). The existing string indexer for `CreationDate`/`ModDate` continues to accept PDF date strings; no wider textual-date parser is added.
* **Keys** are case-sensitive PDF name contents, compared ordinally; the empty key is an invalid argument. Keys are never interpreted as paths or namespaces.
* **Reserved names** are Title, Author, Subject, Keywords, Creator, Producer, CreationDate and ModDate (mapped, METADATA-MAP-01) and Trapped (Info only; True, False or Unknown, existing validation). The key indexer addresses these through their own rules, never as generic custom values. Unknown existing Info entries are left untouched, and non-text entries are not fabricated as text values.

## Mapped properties: METADATA-MAP-01

Prefixes name namespaces, not required serialized spellings. dc = `http://purl.org/dc/elements/1.1/`, pdf = `http://ns.adobe.com/pdf/1.3/`, xmp = `http://ns.adobe.com/xap/1.0/`.

| Info | XMP | Assign | Clear |
| --- | --- | --- | --- |
| Title | dc:title | Set the `x-default` entry of the language alternative; keep other languages (METADATA-TEXT-01). | Remove the whole property, all languages. |
| Subject | dc:description | Same as Title. | Same as Title. |
| Author | dc:creator | One literal entry replacing the list; no splitting on punctuation (METADATA-AUTHOR-01). | Remove the whole list. |
| Keywords | pdf:Keywords | One literal string in both stores; no tokenizing and no dc:subject inference. | Remove in both stores. |
| Creator | xmp:CreatorTool | Coordinated value, changed only on explicit assignment. | Remove in both stores. |
| Producer | pdf:Producer | Coordinated value, changed only on explicit assignment. | Remove in both stores. |
| CreationDate | xmp:CreateDate | Explicit assignment only. | Remove in both stores (METADATA-DATE-04). |
| ModDate | xmp:ModifyDate | Automatic or explicit (METADATA-DATE-01 to 03). | Remove in both stores; counts as an explicit override. |

Untouched Creator and Producer are never replaced with the library's identity by an ordinary edit. New-document assembly identity is a separate contract.

## Coordinated edits: METADATA-SYNC-01, METADATA-CREATE-01, METADATA-REFUSE-01

* An ordinary setter or clear for a mapped property updates the Info and XMP representations, and any required dates, in one call. The call is atomic over pending state: on any failure both stores and the date-override state are as before the call, and earlier successful edits stay pending. A one-sided result is never reported as success.
* With no XMP packet, a mapped edit creates a minimal packet containing the assigned property and the required date properties. Unrelated Info values are not backfilled into it. A clear removes the property from whichever stores hold it. Creation sets the file's required minimum version as the engine does today and claims no PDF/A or other profile conformance.
* If an existing packet cannot be edited safely for the touched properties (below), the ordinary edit is refused with no change. There is no Info-only fallback, no silent replacement and no repair. To proceed the caller replaces the packet explicitly with the raw operation, accepting what that packet retains; the later setter is still subject to authorization.

## Text and author: METADATA-TEXT-01, METADATA-AUTHOR-01, METADATA-CLEAR-01

Assigning plain Title or Subject text writes the `x-default` language alternative entry, adding the entry (and the alternative) when absent and preserving other languages; translations are never overwritten or translated. Assigning Author writes exactly one literal dc:creator entry (a one-item ordered list), whatever the old list held. Clearing Title, Subject or Author removes the whole XMP property and the Info key, leaving no residual translations or extra authors.

## Dates: METADATA-DATE-01 to 05

* **Automatic ModDate.** Any ordinary metadata edit (mapped or custom) sets ModDate to the current UTC time, sampled once at the start of the call and truncated to whole seconds, unless the caller has made ModDate explicit. CreationDate changes only on explicit assignment.
* **Explicit override lifetime.** An explicit ModDate assignment or clear stays authoritative for the lifetime of the open document across editors and successful saves, and ends at reopen. A failed call neither sets nor changes the override. No reset-to-automatic action is introduced.
* **Representation.** Info keeps the caller's offset; XMP holds the equivalent UTC instant. Their texts need not match; their instants must.
* **Clear.** Clearing CreationDate removes both stored values. Clearing ModDate removes both and keeps ModDate absent under later ordinary edits in that document until the caller assigns a new value.
* **Fractions.** Fractional seconds are discarded (04:05:06.900 becomes 04:05:06).
* Raw replacement does not change dates (METADATA-RAW-01).

## Custom properties: METADATA-CUSTOM-01

A custom key is stored in Info only; no XMP property or namespace is invented. The call still refreshes ModDate under METADATA-DATE-01, coordinating it with XMP (creating a minimal packet for the date if none exists, without backfilling other Info values). If the date cannot be synchronized safely, the whole call is refused: no one-sided custom edit remains. Reserved names are not custom keys (METADATA-INPUT-01). Clearing a custom key removes only that Info entry, with the same date coordination.

## Structured XMP edits: METADATA-VALIDATE-01, METADATA-PRESERVE-01

Validation is targeted. The editor inspects only the properties an edit touches and the XML/RDF structure needed to reach them safely; it does not validate or normalize the rest of the graph, and an unfamiliar untouched property never causes refusal.

* **Accepted shape.** One well-formed XML document containing one `rdf:RDF` whose top-level `rdf:Description` children are treated as one document-level property set, whatever their `rdf:about` value (real files commonly use a document identifier there) and in whatever number. Nested descriptions are not document-level properties, and there is no separate-resource tracking or reconciliation. Ordinary valid spelling variation is accepted: any namespace prefix, element or attribute form for simple scalar properties, `rdf:Alt`, `rdf:Seq` and `rdf:Bag` containers where the property defines them, and an optional `x:xmpmeta` wrapper and packet padding.
* **Locating a touched property.** A touched property must be defined in exactly one place across the descriptions, or not at all. Any other case (defined more than once, in competing forms, or with duplicate `xml:lang` entries) is refused, with no conflict resolution, precedence or deduplication: the edit fails with no change, Info is not touched and the packet stays as supplied. Untouched properties of any shape are not examined.
* **Normalization limits.** The edit rewrites only the touched property's value. A missing `x-default` is added. The editor may reserialize the packet, changing formatting, prefix spelling, whitespace and padding, but never unrelated properties, attributes, language values or custom data (semantic preservation; there is no byte-identity promise for an edited packet, in contrast to raw replacement).
* **Other roots.** A well-formed packet with a different root or without `rdf:RDF` can be installed with the raw operation and read, but an ordinary edit that must touch it is refused.
* **Safe processing.** DTDs and external resolution are prohibited; no network access occurs; processing is streaming or bounded and never allocates from an untrusted length.

## Raw XMP: METADATA-RAW-01 to 03

* **METADATA-RAW-01.** Raw replacement validates the supplied packet as well-formed XML with no DTD and no external resolution, then stores its exact bytes. It does not synchronize Info, refresh dates, normalize bytes or claim RDF correctness or conformance. Well-formed packets outside the editable shape are accepted. Write authorization applies. The input is UTF-8 via the existing `SetXmp(ReadOnlySpan<byte>)` surface; the library never rereads caller input at save.
* **METADATA-RAW-02.** A failed raw replacement (malformed, prohibited, over the limit, I/O failure, observed cancellation, authorization) leaves the old packet, Info, date and override state and earlier pending edits unchanged. It restores no destination bytes and gives no publication or durability promise.
* **METADATA-RAW-03 (limits).** A decoded packet above 32 MiB (33,554,432 bytes; refused only when larger) is refused with a resource-limit failure, measured on decoded bytes as consumed. This is the existing documented XMP limit; supplied fonts have no comparable limit by owner decision. No additional XML depth or node-count limits are adopted: bounded streaming processing and the byte limit are the stated bounds, and a concrete need would bring a specific restriction with its concrete impact.

## Authorization: METADATA-AUTH-01

Metadata writes need the applicable credential and permission, are blocked by a certifying DocMDP signature at every `P` level, and must respect field locks and Reader-rights handling as [security](security.md) defines; a signed, removal-dependent edit is refused under the owner-selected signed-rights boundary (Q-33). Metadata operations grant no rights and drop none implicitly. Reads need no write permission.

## Failure, cancellation and output: METADATA-FAIL-01

Failures use [shared categories](shared.md#shared-fail-01-failure-categories): invalid argument, unsupported or malformed input, authorization, resource limit, observed cancellation, processing or I/O. The first determinable failed gate is reported with the store, property and location when known; no universal fault priority exists. Cancellation is observed before work, between input chunks or XML processing steps, and immediately before the pending-state commit; a blocked synchronous borrowed read need not be interruptible. Every mutation restores both stores and date state on failure or observed cancellation and keeps earlier edits. Metadata operations write no PDF output; the explicit save, append and workflow publication keep their own destination guarantees, including after a failed append write. Pending-state restoration never implies destination restoration or retry.

## Acceptance map

All targets are unexecuted. Compare independently of the writer (for example an RDF-aware reader and a second PDF reader), assert the stated outcome, not message text, and report the implementation gaps they reveal.

| ID | Scenario |
| --- | --- |
| META-A1 | Info-only, XMP-only and conflicting fixtures: Info getters report Info only; XMP read is separate; malformed packet is an error, not absence (READ-01). |
| META-A2 | Assign each mapped property; both stores agree. Keywords with separators stay one string. Untouched Creator/Producer survive (MAP-01). |
| META-A3 | Translated title before `x-default`: assign changes only the default, keeps translations, Info agrees; missing `x-default` added. Author list becomes one literal entry. Clear removes all variants (TEXT, AUTHOR, CLEAR). |
| META-A4 | Edit with XMP absent: minimal packet, no Info backfill; clear with no packet creates none (CREATE-01). |
| META-A5 | Dates: automatic UTC whole-second ModDate; offset and fraction near the next second; explicit override survives edits, editor change and save, ends at reopen; ModDate clear stays absent (DATE-01 to 05). |
| META-A6 | Custom key stays Info-only with a coordinated ModDate; a date-sync refusal leaves no one-sided edit (CUSTOM-01). |
| META-A7 | Packet with several `rdf:Description` blocks, non-empty `rdf:about`, attribute-form scalars and an unfamiliar foreign property: Title edit succeeds, foreign data survives. Duplicate touched definition, competing forms or duplicate `xml:lang` refuse with no change (VALIDATE-01, PRESERVE-01). |
| META-A8 | Raw: exact bytes kept, Info and dates unchanged; later ordinary edit may refuse; malformed, DTD, over-32-MiB (decoded, including small-encoded large-decoded) refuse; throwing or non-seekable stream leaves old state; caller stream open (RAW-01 to 03). |
| META-A9 | Inject I/O or cancellation after provisional Info then XMP changes: both stores, date override and earlier edits match their pre-call state; Save/reopen with a usable source afterwards (SYNC-01, FAIL-01). |
| META-A10 | Metadata write under certifying DocMDP and signed removal-dependent cases: refused; reads still work (AUTH-01). |

## Remaining gaps (engineering, none need an owner answer)

Public spelling of any additive overloads; exact error types beyond `PdfXmpException`; the implementation changes the target requires (the editor's duplicate-touched-definition refusal was implemented in W-221); independent-reader checks of edited packets. None changes a selected decision.

## Adoption record

Approved by the owner on 2026-10-31 as written, with these clarifications: top-level `rdf:Description` elements form one document-level property set regardless of `rdf:about` and nested descriptions are not document-level properties; edits are refused when the targeted property has multiple definitions; the existing 32 MiB limit and bounded processing are retained with no additional arbitrary limits absent a demonstrated need. Adoption changes no behavior, acceptance or qualification record and claims no executed result; the implementation changes the target requires remain gaps. Decision principle: see the [shared adoption record](shared.md#adoption-record). History: [metadata topic history](drafting/history.md#metadata).
