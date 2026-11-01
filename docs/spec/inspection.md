# Document, form and signature inspection

**Adopted subject contract, revision W-218-INSPECTION-2026-10-31, approved by the owner on 2026-10-31 with the corrected security contract.** Adoption does not assert implementation, interoperability, executed acceptance or release qualification. Authority: [charter](../charter.md) > [supported capabilities](../supported.md) > adopted contracts. Inspection reports structures, recorded values and claims; it neither renders nor extracts page text, validates signatures/trust, repairs a document, nor authorizes a write.

## Read inputs, results and compatibility: INSPECT-BOUNDARY-01

Read an open, authenticated document under [security](security.md). No new extraction-bit, DocMDP or FieldMDP denial is imposed on ordinary structured inspection/in-memory form export. Results reflect accepted pending edits at invocation, including after editor disposal; reading does not persist them. New complete-form results are immutable snapshots, with read-only ordered collections and copied value bytes; they do not change while the caller retains them. No concurrent-read/write snapshot isolation is promised; source stability and lifetime remain in [opening/saving](opening-and-saving.md).

Page, complete-form, signature and revision reads are independent requests. Malformed CMS need not prevent page/form reads; malformed revision footers need not prevent other usable inspection. A requested complete result either succeeds under its declared completeness rules or refuses without a partial result presented as complete. A reported unknown subproperty is allowed only where this contract explicitly permits it. Inspection changes no pending state, XFA, rights, signatures, source bytes or destination and publishes no file. Hybrid AcroForm inspection is supported independently of XFA; XFA-only forms report that classification and no AcroForm fields, not that the XFA has no data. PDF/A inputs receive normal checks without conformance claims.

The logical-field/tagged-value surface below is additive. Keep existing widget-oriented `PdfField`, getters and export shapes available; neither adoption nor this contract silently changes legacy Fill/FillForm or claims a lossless mapping into their string values. Public spelling/overloads are engineering work; the observable distinctions below are the contract.

## Pages, boxes and widget coordinates: INSPECT-PAGE-01

Pages are enumerated in page-tree order with **1-based numbers**; page count is the established number of leaf pages, not an unchecked Count entry. Zero, negative and out-of-range requested numbers are invalid arguments. Refuse cycles/repeated ownership or malformed hierarchy that prevents a complete count/list; never return a guessed count.

Return effective inherited MediaBox, inherited CropBox (MediaBox if absent), and inherited rotation (0 if absent). Valid integral multiples of 90 normalize to 0/90/180/270, including negative values. Invalid present boxes/rotation are malformed, not absence. Retain rectangle coordinates in **unrotated default PDF user space**, including nonzero/negative offsets; do not subtract the crop origin, clip boxes/widgets to the crop or rotate/swap them for display. Four finite coordinates are required; reversed corners remain the recorded coordinates rather than a repair. Report degenerate rectangles as such, not a usable placement area.

The additive page observation supplies page-local UserUnit, default 1, as a separate positive finite scale; it is not inherited. One user-space unit is UserUnit/72 inch. Invalid present UserUnit makes a requested physical-scale result fail; raw boxes/rotation remain independently readable. Reading geometry does not multiply the returned coordinates by UserUnit. Bleed/Trim/Art boxes and content bounding-box inference are outside the initial required surface. [Images](images.md#coordinates-and-placement-image-place-01) deliberately uses displayed-crop physical points; that does not replace these coordinates or adopted signing's existing widget coordinates.

Widget observations retain their rectangle in the same space, 1-based owning page, page rotation and MediaBox. Multiple widgets of one field remain distinct, with zero-based widget order within that logical field. Resolve placement from page annotation membership with a consistent P reference when present. A widget absent from all pages is explicitly unplaced, never assigned page 1. Conflicting ownership, an unresolved claimed page or malformed Rect is an explicit geometry error/unknown in the additive record; it does not invent a rectangle or lose an otherwise establishable field value. A caller requesting a complete geometry-only result refuses if required geometry is unestablishable. Zero-area signature widgets are legitimate invisible rectangles, not missing geometry.

## Logical form records

### Duplicate-name inspection: INSPECT-FORM-01 (Q-124)

Complete form inspection returns one record per logical terminal field, with its full/partial name, effective type/flags/value, options where applicable and all its widget observations. Follow Fields/Kids order depth first; preserve widget/option order. Include supported widgetless terminals with an empty widget collection. Distinct fields sharing a full name remain separate records with an ambiguity indication and their own values/widgets. Do not merge them or make either an implicit named mutation target. An ancestor name segment contributes to the dotted full name; do not trim, case-fold, normalize Unicode or invent fallback aliases.

### Stable read-side field identity: INSPECT-FORM-02 (Q-129)

Assign each logical field an opaque read identity stable within one open document, including across accepted pending edits and repeated snapshots. Removed identities are never reused for another field in that document. A merged field/widget and a multi-widget logical field each have one field identity. No stability across reopen/assembly, identity-based mutation API or exposed PDF-object handle is promised. Refuse cycles, conflicting parents or hierarchy ambiguity preventing establishment of these identities. Distinct valid same-name fields are name ambiguity, not hierarchy failure.

### Undecodable field names: INSPECT-FORM-03 (Q-134)

A requested complete form read or ordinary export refuses as a **whole** if a required identity or semantic value cannot be established. No silent omission, partial success, replacement-decoded alias or guessed unset is allowed. This includes an undecodable required name, unresolved effective type/value, contradictory button state or unresolvable selected choice. A genuine unset value remains explicit no-value. Unsupported field types are reported as types in a structural inventory; a complete semantic read requiring their unknown value refuses. Independent pages and otherwise independent signatures/revisions remain available.

### Version-aware field-name decoding: INSPECT-FORM-04 (Q-141)

Decode required names and semantic text with strict PDFDocEncoding, UTF-16BE, or effective-PDF-2.0 UTF-8 under the exact classifier below. Refuse undefined PDFDocEncoding mappings, malformed selected Unicode and unsupported object types without substitution or fallback. No generated-appearance repertoire restriction applies to existing values, names or options. Supported effective text V streams (PDF 1.5+) use ordinary decryption/filter decoding, not a second PDF literal-string unescape pass; T and Opt retain their required string types. Do not parse arbitrary RV markup to invent a missing plain V. Explicit existing empty text stays empty.

### Effective version and exact BOM classification: INSPECT-FORM-05 (Q-145)

Use a valid file header and current catalog Version upgrade; a valid lower catalog version does not downgrade the header. This is current-revision interpretation, not a historical conformance audit. Absent Version uses the header. Invalid/unresolved Version never enables UTF-8: a requested effective-version result refuses as malformed, and a text request needing that version refuses; otherwise independently decodable PDFDocEncoding/UTF-16BE remains readable. An invalid header cannot supply a guessed version.

Only exact FE FF selects UTF-16BE and exact EF BB BF selects UTF-8, the latter requiring effective PDF 2.0. Incomplete FE or EF BB and FF FE use PDFDocEncoding subject to its own validity, not a new 'BOM-like' refusal. Once selected, malformed Unicode refuses without fallback: paired UTF-16 surrogates, complete units, and strict UTF-8 scalars are required. Carry split markers/scalars across stream chunks; preserve interior BOM characters, CR/LF, supplementary characters and U+00AD without normalization.

For Unicode language annotations, use the already-developed [read-side language rule](forms.md#strict-text-stream-decoding-proposal): ESC, exactly two raw ASCII letters for language, optionally two for country, closing ESC; UTF-16BE uses 00 1B delimiters and raw ASCII code bytes. Recognize before scalar decoding. Well-formed annotations become ordered language spans over plain text (start/length in UTF-16 code units, language/optional country), not value characters; each lasts to the next annotation or text end, and a final annotation has zero length. Retain adjacent spans. Malformed/unclosed/ambiguous ESC usage refuses the affected complete result rather than stripping or displaying guessed text. PDFDocEncoding ESC receives no invented Unicode annotation semantics. Plain-name/option comparison uses decoded plain text; equal annotated/plain names remain ambiguous, not merged. This settles read results only, without reopening forms' write/layout design. Existing three-reader experiments are historical supporting evidence, not target execution or Acrobat validation.

## Values and ordinary export

### Engineering-supported ordinary export refinements: Q-120/Q-121

The additive value result is tagged: NoValue, Text (including empty), Boolean, RadioState (lossless PDF name bytes with canonical ASCII spelling), or Choices (ordered export-text sequence). Name bytes are not PDF text strings: do not apply BOM/Unicode substitution to button state names. Return raw declared state names and valid per-widget Opt text associations separately; declarations prove neither appearance usability nor mutation eligibility. Required/ReadOnly/Password/RichText and choice flags are observations, not permission or readiness claims. General inspection includes existing password-field text under normal authenticated reads; ordinary export excludes password fields. No new redaction of legacy reads is introduced.

| Field family | Complete semantic observation |
| --- | --- |
| Text / rich text | Strict effective plain V: NoValue if genuinely absent/null after inheritance, otherwise Text, including empty. RV remains separate presence/content information; missing plain V with nonblank RV cannot be guessed empty. FileSelect text is data, never a file access. |
| Checkbox | Boolean when effective V/widget states establish consistent selected versus Off. Missing V is false only when states establish Off; contradiction refuses. False is distinct from unset/empty text. |
| Radio | Lossless selected RadioState; NoValue for established no selection. Preserve declared states/Opt associations, never substitute a display label for a state. Contradictory V/AS or unestablishable selection refuses. |
| Listed choice | Choices in existing option-list order, with resolved zero-based option indices and separate export/display labels. A selected empty export is one empty string, not no selection. No selection is NoValue. Resolve stored V/I consistently; indices may distinguish duplicate exports only when well-formed and consistent. Ambiguous/contradictory selection refuses; unrelated unselected duplicate options do not alone refuse. |
| Editable combo literal | Text with explicit literal association, including empty literal text, only when not a resolved listed selection. Preserve option list. No normalization or label fallback. |
| Signature / push button | Type/flags/widgets and explicit NotApplicable ordinary-data value; signature details are a separate request. Never decode CMS bytes as a text field. |

No collection entry is a promise that it can be refilled through a legacy input API. Existing rich, read-only, Required, NoExport, action-bearing and password flags do not confer write authority. Inspection executes no scripts/calculation and never prepares appearances.

### Duplicate exposed names: FORM-EXPORT-02 (Q-120)

Ordinary export includes one entry per supported logical terminal text/checkbox/radio/choice field, including read-only fields; excludes signatures, push buttons and password fields. Required and NoExport do not silently remove ordinary in-memory data. Preserve tree order for an ordered export view. Distinct **included** fields with the same exposed full name refuse the whole name-keyed export, never overwrite. Complete identity/value refusal applies to required export members; excluded fields' independent CMS/appearance data need not be decoded. Inspection may still report duplicate-name identities separately.

### Explicit unset state: FORM-EXPORT-03 (Q-121)

Every included unset field is present with explicit null/NoValue; actual empty text, false, selected listed-empty choice and empty literal text remain distinct. Multi-selection follows option-list order and retains its associations. The tagged surface is additive; this contract adopts no JSON/FDF/XFDF serializer or automatic refill conversion. A streaming serializer, if later offered, needs its own destination-failure contract; whole-result refusal here is not stream rollback.

## Signatures and revisions

Return structural observations, never valid/invalid cryptographic verdicts, trusted identity, revocation status, permitted-change certification, PAdES conformance or verified timestamp time. Keep populated-field, CMS parse, ByteRange status and revision association separate. Existing [PdfSignatureInfo](../../src/jaytwo.Pdf/Signatures/PdfSignatureInfo.cs) properties remain compatible.

The initial CMS subset is SignedData with one SignerInfo under adbe.pkcs7.detached, adbe.pkcs7.sha1, ETSI.CAdES.detached and ETSI.RFC3161. Accept DER plus existing tolerated BER indefinite outer containers with bounded nesting; permit zero reservation padding after the CMS object, refuse nonzero trailing junk. Unsupported subfilters retain their PDF-level observations with an explicit unsupported CMS state. Malformed CMS within a requested supported profile refuses that signature-detail request; it does not invent absence or block independent page/form reads. Multiple SignerInfos are explicitly unsupported, not silently reduced to one.

| Result | Required reporting subset |
| --- | --- |
| Signature field/dictionary | Read-side identity/full name, empty versus populated dictionary, subfilter, recorded Name/Reason/Location/ContactInfo, PDF M claimed time, recorded/effective DocMDP level distinguished. Malformed optional date is a malformed observation, not a trusted or absent time. |
| CMS claims | Digest/signature OIDs, optional recognized digest name, embedded DER certificate sequence and uniquely identified signer certificate by issuer/serial or subject-key identifier (absent/ambiguous explicitly distinguished), CMS signing-time, first signature-timestamp token genTime or document-token genTime. Unknown OIDs remain OIDs; no algorithm suitability verdict. Attribute certificates/other certificate choices have explicit unsupported/presence indication rather than a claim to return every certificate kind. |
| ByteRange | Four nonnegative integer offsets/lengths with checked arithmetic. Missing is distinct from malformed; wrong shape/negative/overflow refuses. Report first applicable structural status in legacy order: Missing, NotFromFileStart, EmptyRange, PastEndOfFile, Overlapping, GapNotContents, WellFormed. Check the gap against this signature dictionary's own serialized Contents hex span, not merely arbitrary angle brackets. Compressed signature dictionaries cannot prove that span. No signed-byte hashing. |
| Revision relation | ByteRange exclusive end and exact matching logical revision number if established, regardless of whether other ByteRange checks pass. Later revision means a later cross-reference section, not arbitrary trailing bytes. Unknown relation is distinct from false. A matching end never proves complete coverage or that the signature dictionary is itself signed. |
| Logical revisions | Oldest first, 1-based numbers and exclusive end offsets from structurally linked xrefs and their startxref/EOF footers. Hybrid auxiliary sections and linearized main/first-page pairs count as one. Malformed/missing required footers refuse the revision request while independent usable reads remain available. EndsAtSignatureByteRange is only an offset match. |

PDF, CMS and timestamp times remain separately labelled claims. No timestamp imprint/signature check, certificate-chain/trust assessment, signature-policy/ESS/revocation interpretation, recursive counter-signature inventory or all-signer forensic reporting is promised. Historical revisions are persisted source revisions; pending edits add no fictional revision. If revision enumeration fails, an additive signature observation may report its relation Unknown while retaining separately established PDF/CMS data; existing strict APIs may still throw for that combined request.

## Limits and read failures: INSPECT-FAIL-01

Use sparse source access and proportional traversal/touched-object buffering; do not read page content or the bytes claimed signed merely to report structure. Retain the engine's existing structural safety envelope rather than creating a new global budget: per traversal path, depth above 256 refuses; per parsed container, more than 1,048,576 entries refuses; actual decoded structural streams above 32 MiB and xref streams above 64 MiB refuse during decoding, never on advertised length alone. These bound recursion/container/decompression risks, not total document size or RAM. In CMS, nesting above 64 or more than 65,536 certificate choices per CMS object refuses. Revision-footer search is bounded to 64 KiB after each xref section; a required footer not established within it produces explicit refusal, not a truncated revision list. No new arbitrary document/form/CMS payload cap or caller work-counter API is introduced.

Use [shared failure categories](shared.md#shared-fail-01-failure-categories): invalid requested page is an argument error; malformed required structure/value a malformed-data refusal; unsupported type/encoding/profile an unsupported-input refusal; a safety cap a resource-limit failure. The first established failure is reported without universal error priority. There is no partial complete-form result on any such failure.

Observe supplied cancellation before work, between page/field/signature/revision batches, during chunked decoding and before returning a result. Tokenless synchronous properties do not gain a cancellation parameter. A blocked synchronous source read need not interrupt. Failure/cancellation returns no successful result and leaves pending/source bytes unchanged; borrowed sources stay open, positions are not restored. Library-owned temporary resources are cleaned up, with cleanup errors separate from the primary failure. No destination writes/publication, retry, repair or usable-source guarantee after an independent source failure is implied.

## Acceptance map

All targets are **unexecuted**; old code/tests/research are comparison evidence only.

| ID | Scenario and required observation |
| --- | --- |
| INSPECT-A1 | Inherited/nonzero boxes, negative rotation multiples, UserUnit, zero-area widget and cropped/rotated pages: raw user-space geometry retained; compare image/signing coordinate boundaries; malformed physical scale fails only the dependent result. |
| INSPECT-FORM-C/D | Duplicate-name fields versus one multi-widget field, widgetless terminal and repeated snapshots after pending edits: separate stable nonreused identities, ordered widgets and explicit ambiguity; export refuses duplicate included names. |
| INSPECT-FORM-A/B/E | Unset, empty text, false, selected empty choice, empty literal and ordered multi-selection; read-only export inclusion and excluded fields; malformed identity/value beside valid fields refuses whole requested complete result, independent pages still work. |
| INSPECT-FORM-F/G | Version/header/catalog, exact/incomplete BOM, strict malformed Unicode, chunk boundaries, V text streams, valid language spans and malformed ESC; no replacement aliases, second unescape or silent stripping. |
| INSPECT-A5 | Checkbox/radio contradictions, lossless nontext state names, valid/contradictory V/I, duplicate exports and unsupported fields; exact value or controlled complete refusal, never invented selection. |
| INSPECT-A6 | Ordinary signature, document timestamp, later revision, unknown subfilter/OID, malformed CMS, ambiguous/missing signer certificate and ByteRange edge cases; compare each named structural claim with independent tools without trust assertions. |
| INSPECT-A7 | Classic/hybrid/linearized logical revisions, exact end match, trailing junk and malformed footer: correct numbering/offset relation or request-specific refusal; pending edits create no source revision. |
| INSPECT-A8 | Structural limit/cycle/cancellation/source failure: no partial complete result or mutation, borrowed lifetime preserved, primary/cleanup errors distinct; retained snapshot unchanged after later accepted edits. |

## Remaining engineering gaps

Add logical identities/tagged snapshots and UserUnit/explicit unknown observations without breaking legacy widget/value APIs. Strict/version-aware text decoding, semantic V/I/button consistency and language spans differ from current paths and require target fixtures. Signature CMS/revision optional-state reporting and legacy combined requests need compatibility verification. Numeric safety limits above retain existing defenses; they do not establish measured performance or qualification. No form layout, font research, write storage or frozen-form A1 work is authorized here.

## Adoption record

Approved by the owner on 2026-10-31 together with W-218-SECURITY-2026-10-31 after its accessibility-bit correction. Retain ordinary/signing user-space coordinates; additive complete logical-field/tagged snapshot/export results with strict identity/value refusal; version/language decoding and independent read errors; and the single-signer CMS/ByteRange/revision subset with claims-only reporting. Legacy Fill/FillForm and existing APIs remain available; no new serialization format or validation scope is adopted. Adoption does not assert implementation, interoperability, executed acceptance or release qualification. Runtime, behavior, acceptance outcomes and qualification records remain unchanged. History: [inspection](drafting/history.md#inspection).
