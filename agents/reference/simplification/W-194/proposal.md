# W-194 approval packet: empty signing locations

Status: recommendation pending Gate 2 owner approval. No wording below is adopted, no implementation ticket is activated, and no queue insertion is requested by implication. O04 settles preserving empty locations in principle; this packet supplies a bounded support contract and exposes its missing evidence. W-151's no-new-transform policy remains binding until expressly superseded for the second slice.

## Exact proposed contract, slice A

Replace phase-2 A3 and the capability-table Signatures row with the following wording, adjusting only table formatting:

> Assembly preserves a selected empty signature field and its retained widgets when its ownership, appearance and seed constraints are supported. Empty means that its resolved value is absent or null; an existing signature dictionary is never treated as empty because its contents are invalid. Supported empty fields have no Lock dictionary. Unsupported or ambiguous retained signature fields cause refusal before destination output, rather than field or widget omission. Field names use the existing InputN occurrence namespace. Repeated pages within one occurrence retain one logical field with distinct widgets owned by their respective output pages; separate occurrences have distinct fields. Read-only flags are preserved and later signing remains prohibited for a read-only field. Seeds are preserved and checked again when the field is signed. Preserving an empty field does not preserve any earlier signature, certification, timestamp or usage right.

Append the following proposed admission wording, subject to W-197's reconciliation before adoption:

> The default assembly operation refuses an input with a detected approval signature, document timestamp, certification or existing FieldMDP restriction, even when the corresponding page is not selected. An unresolved certification is a refusal. Source authentication, per-input copy and assembly permissions, and explicit consent to unencrypted output remain required. No derivative consent overrides certification or field restrictions. Usage-rights preparation and explicitly requested unsigned derivatives have separate contracts; neither is an automatic fallback.

Corresponding proposed ACCEPTANCE.md bullets, after implementation, and equivalent docs/behavior.md paragraphs:

> Extracting and concatenating supported empty signature fields preserves their field names under InputN, read-only flags, retained widget rectangles, supported appearances and seed dictionaries. Distinct output-page widgets have reciprocal field/page ownership. Signing the exact assembled full name reuses that field and all its retained widgets, without adding a replacement field or affecting another occurrence. A required seed mismatch refuses before invoking the provider or writing destination bytes. Unsupported retained constraints refuse assembly before destination output. The original input remains unchanged.

This explicitly replaces signature-widget omission for empty retained fields only. Existing omission accounting for unsupported signed inputs must be reconciled with W-197's default refusal/derivative decision, not silently left contradictory. A9 must receive an owner-approved exception saying that unsupported appearances on retained empty signature widgets refuse rather than omit. Exact proposed A9 addition:

> For a retained empty signature widget, an unsupported, unclassifiable or locally over-budget appearance instead refuses the assembly operation before destination output; it does not omit the signing location.

No appearance generator or renderer is added. Absence of AP on an empty signature widget is permitted. Copied AP graphs use existing bounds. New-file identity is not an incremental prefix guarantee. Preflight success does not guarantee that later source reads or physical destination writes cannot fail; no stronger rollback promise is proposed.

## Precise seed subset for slice A

These are proposed assembly-admission rules. The later signer retains W-151's existing policies, provider contract and checks. Satisfiability at assembly is not a guarantee that arbitrary future signing options are compatible.

| Structure | Proposed admitted subset | Refusal / later enforcement |
| --- | --- | --- |
| SV | Absent/resolved null, or a bounded indirect dictionary with well-formed known mandatory controls | Unresolvable or ambiguous graphs refuse. Unknown required flag bits refuse. Do not rewrite it into a weaker dictionary. |
| Filter, SubFilter | Mandatory Filter Adobe.PPKLite; ordered SubFilter list whose first engine-supported CMS encoding is adbe.pkcs7.detached or ETSI.CAdES.detached | Empty/malformed lists or no supported CMS encoding refuse. Later signing must use the first supported encoding in seed order. No provider introspection is claimed. |
| V | Mandatory integer in 1–2 for effective PDF 1.x, 1–3 for effective PDF 2.0 | Preserve effective version; do not promote a source to evade or change constraint interpretation. |
| Reasons | Mandatory array of strings, including sole dot as omission; absent required Reasons also means omission | Empty/malformed arrays refuse. Matching or omitted Reason is checked at signing. Nonempty allowed lists remain future caller choices. |
| DigestMethod | Mandatory array of names containing at least one SHA256/SHA384/SHA512 | Unsupported-only/malformed arrays refuse. Retain all original values; signing checks the selected digest. W-151 unknown-provider over-refusal remains. |
| AddRevInfo | Mandatory false | Mandatory true or malformed required value refuses; no revocation-support expansion. |
| MDP | Absent, or P absent/0 for the candidate CMS path | P 1–3 or malformed applicable P refuses. No certification creation. |
| LockDocument, PDF 2.0 | Mandatory auto or false without Lock/P | Mandatory true refuses under W-151; no new locking is implied by auto. |
| LegalAttestation, AppearanceFilter | No applicable required entry | Applicable required entries refuse. |
| Cert, TimeStamp | Advisory controls as interpreted by W-151; no applicable mandatory certificate/endpoint/timestamp requirement for this initial CMS preservation slice | Such requirements refuse assembly even where a later standalone-timestamp operation might have a different satisfiable subset. This deliberately narrower assembly subset does not narrow the general signing API. No certificate trust, endpoint access or enrollment is inferred. |
| Optional known entries | Retained structurally; do not refuse solely because an optional constraint cannot be met | W-151 decides optional/irrelevant semantics. Structural corruption, active behavior or exhausted bounds still refuses. |
| Unknown entries | No arbitrary extension guarantee | If meaningful mandatory semantics cannot be classified, refuse. Harmless bounded optional data may survive only under an explicitly reviewed copy policy; do not drop it and claim full preservation. |

These rules must inspect the actual field/value owners used by signing. Inherited or merged structures whose seed ownership is ambiguous refuse until a separate regression proves correct handling. Unknown control structures must not fall through because ordinary signature fields were formerly skipped before validation.

## Occurrence and partial-selection rules

1. Keep the current positional InputN namespace, including empty occurrences consuming their ordinal. No name-based merging across sources. Preserve exact PDF text names beneath the occurrence root; reject ambiguous duplicate full names before output.
2. One source field is one logical field per occurrence. Selecting a source page repeatedly creates fresh widget objects, with each widget P pointing to the correct destination page and reciprocal Parent/Kids references. Signing that logical field covers all its retained widgets. Callers wanting independent signatures use separate occurrences.
3. Retain only selected-page widgets; omit an unsigned field whose every widget is unselected and prune empty ancestors. A page-less empty field is not a signing location in this initial subset: if its semantics intersect a retained constraint, refuse. Do not silently synthesize a page/rectangle.
4. Slice A supports no Lock dictionary. Do not remove Lock to make a field admissible. A future Include target must resolve uniquely within the same input occurrence, map through the actual field map rather than string concatenation alone, and retain the entire named field subtree. If any named target/subtree is pruned, malformed or unsupported, refuse. No cross-occurrence reference, dangling name or retargeting to a similarly named field is permitted.
5. Future All/Exclude cannot be copied literally: both quantify over the output document and could lock unrelated occurrences. Options include refusal or an owner-approved expansion to an occurrence-local Include list under a budget. This packet recommends refusal until that expansion has separate evidence. Lock/P is also refused: an occurrence namespace cannot localize document permissions.

## Exact proposed extension, slice B

Only after explicit owner approval superseding the W-151 no-new-transform rule for this narrow case, replace the sentence "Supported empty fields have no Lock dictionary" above with:

> Supported empty fields have either no Lock dictionary or an indirect SigFieldLock dictionary with Action Include, no P entry, and a Fields array naming uniquely resolvable, fully retained fields or field subtrees in the same input occurrence. Every target name is remapped to that occurrence. Other lock actions, document-permission instructions, dangling targets, partially retained target subtrees and malformed constraints refuse before output. Signing a preserved Include-constrained field creates the corresponding FieldMDP signature-reference and transform dictionaries; subsequent fills and flattening honor that restriction. No transform can target another occurrence.

Proposed signing-contract addition:

> Signing a supported Include-constrained empty field materializes its remapped field-lock instructions as a FieldMDP transform in the new signature revision. Existing certification and field restrictions still apply. This narrow transform creation does not create a certification signature, a trust-validation feature, a general document-lock API or a new signing algorithm.

Whether to apply that support also to original nonassembled fields must be part of the approval, not an accidental branch difference. Recommended: use the same supported Include-only path for both, with equivalent refusal and verification. This is a change in supported signing responsibility and needs the owner's exact wording approval; W-194 does not authorize it.

## Draft follow-ups for approval (no new active work items)

| Draft | Scope and candidate allowed paths | Observable acceptance and focused validation |
| --- | --- | --- |
| P194-A: Preserve unconstrained empty locations | PdfPageExtractor.cs, PdfAssemblyForms.cs and reviewed typed-copy policy/field-helper paths under src/jaytwo.Pdf/Internal/; assembly tests under test/jaytwo.Pdf.Tests/; protected A3/A9 wording only after exact approval; ACCEPTANCE.md and docs/behavior.md | Preserved plain/seeded/null-valued/read-only locations; partial/repeated pages and two occurrences; no-Lock refusal, unsupported AP/mandatory seed refusal at zero output including a bad later input. Real CMS into the exact retained full name, raw identity checks and independent pypdf/qpdf/pyHanko checks; unchanged other occurrence and source. Merged/nested owners must either pass a distinct regression or refuse. |
| P194-B: Give supported Include locks effect | Depends on P194-A and explicit signing-policy approval. PdfSigner.cs, relevant PdfWriteGate/field-restriction paths, copy/remapping policy under src/jaytwo.Pdf/Internal/; focused assembly/signing tests; exact approved contract paths | Source and preserved fields sign with a generated FieldMDP Reference; target map is local to occurrence. Fill/flatten of the locked target refuses after reopen, another occurrence remains editable. Inspect transform independently and exercise Include parents, repeated widgets, missing/partial targets, All/Exclude/P refusals, malformed locks, cancellation and destination refusal guarantees. Retain W-151 preflight and provider safeguards. |
| P194-C: Explicit signed derivative, if justified | W-197 owns classification, default refusal, exact derivative name/consent, loss reporting and workflow publication. Candidate engine assembly and workflow paths only after W-197 selects them. Separate from A/B; not a lock workaround. | Approval/certification/timestamp/UR3/FieldMDP and painted marks classified separately; whole-input versus selected-page scope explicit; consent never overrides authentication/permissions/certification/locks. Inputs with merely empty fields do not require derivative consent. W-156 receives explicit retain/rescope disposition rather than silent replacement. |

Each actual implementation item must enumerate exact paths before activation, identify its evidence tier and add permanent tests. Shared admission/form work with W-190 and W-197 is counted once. No full matrix or Docker checkpoint is implied by these draft scopes; framework-sensitive signing changes must name the appropriate target checks. No new qualification evidence is claimed.

## Approval and unresolved evidence

Approval is needed for exact A3/A9/shipped-contract wording and implementation ticket insertion. Slice B additionally needs explicit supersession of W-151's no-new-transform exclusion, not just approval of field copying. W-197 must reconcile the proposed signed-input admission wording before it is adopted. Missing independent review and candidate signing evidence remain explicit; these proposals are reviewable, not adoption-ready evidence of preserved locked-field behavior. No user decision is needed to commit this research record.
