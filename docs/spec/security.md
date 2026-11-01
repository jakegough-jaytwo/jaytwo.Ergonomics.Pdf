# Authentication, permissions and write restrictions

**Adopted subject contract, revision W-218-SECURITY-2026-10-31, approved by the owner on 2026-10-31 after the accessibility-bit review correction.** Adoption does not assert implementation, interoperability, executed acceptance or release qualification. Authority: [charter](../charter.md) > [supported capabilities](../supported.md) > adopted contracts. This supplies the security gates and initial permission-setting output. It introduces no password change/removal, encryption creation, public-key encryption or new rewrite strategy.

## Authentication and permission inspection: SECURITY-AUTH-01

Accept the Standard security handler: R=2 40-bit RC4; R=3 RC4 at 40-128 bits; R=4 supported RC4/AES-128 crypt filters, including distinct string/stream filters; R=5/6 AES-256. Retaining these schemes serves existing-document compatibility, not a recommendation to create RC4 files. Unsupported handlers, algorithms or filters refuse explicitly. Malformed encryption dictionaries, inconsistent encrypted permissions and encrypted cross-reference streams refuse rather than being treated as unencrypted.

Opening takes a password; null tries the empty user password, including restrictions-only files. Report unencrypted, user or owner authentication distinctly. Owner takes precedence if both roles match. Incorrect/missing credentials are authentication failures, not permission denials. Apply each revision's prescribed password preparation, without spelling variants or fallback to another scheme. Never include passwords, recovered credentials or derived keys in results/diagnostics. Dispose owned cryptographic resources and clear mutable temporary secret buffers when no longer needed; caller-owned immutable strings and runtime copies have no erasure guarantee.

Permission inspection reports the **user** settings even under owner authentication: print, high-quality print, modification, copy, annotations, existing-form fill, assembly and accessibility extraction. Keep stored bits/revision distinct from effective authority. Unencrypted inputs permit these operations; owner authentication bypasses user bits, never certification, locks, Reader rights or operation eligibility. Reading settings is not authority to change them.

Bits below are numbered from 1; R is the handler revision. R=2 folds fill into bit 6, assembly into bit 4 and high-quality print into bit 3. R>=3 fill is bit 6 **or** 9, assembly is bit 11 and high-quality print requires bits 3 and 12.

R=2 accessibility extraction follows bit 5. For R?3 under an effective pre-PDF-2.0 version, bit 10 separately reports accessibility extraction; bit 5 reports ordinary copying/extraction and does not grant accessibility extraction when bit 10 is clear. For effective PDF 2.0, bit 10 imposes no accessibility restriction; retain its stored value as a historical observation. Additive permission inspection distinguishes these settings from the existing CanExtractForAccessibility getter, whose historical bit-5-or-10 behavior remains available for compatibility. No print, accessibility or page-text extraction API is introduced.

## Operation authorization matrix: SECURITY-WRITE-01

Every row requires authentication where encrypted. The password column applies to user access on the schemes above; owner/unencrypted access bypasses it except the owner-only permission-setting row. Every gate is independent. Known denials precede pending mutation and library-caused destination changes; late failures follow the affected operation's contract. There is no universal simultaneous-error priority.

| Operation | Password authority | DocMDP / FieldMDP | Reader-rights grant; signature consequence |
| --- | --- | --- | --- |
| Page/form/metadata/security/signature inspection, attachment listing/decoded reads/extraction and in-memory ordinary form export | Authentication only; no new extraction-bit gate | Write locks do not prohibit reads; extraction destination rules stay in attachments | No removal consent or trust verdict |
| Existing-field fill, including explicit rich clear and appearance preparation integral to fill | R=2 bit 6; R>=3 bit 6 or 9 | P=1 refuses; P=2/3 may permit; refuse affected locked fields/descendants | Form/FillIn; earlier bytes retained without validator-acceptance promise |
| Flatten | Legacy calls retain bits 4 and 6; additive proposal requires bit 4, plus fill permission if the same call changes values | Every DocMDP P refuses; affected field locks apply by logical identity | No granting category; approval signatures/document timestamps need breaking opt-in |
| Metadata edits | Bit 4 | Every DocMDP P refuses; preserve locks/structures | No granting category; incremental retention is not validity |
| Attachment add / replace / remove | Bit 4 | Every DocMDP P refuses; preserve unrelated locks | EF/Create, EF/Modify, EF/Delete; compound changes need every applicable grant |
| Page rotation | R=2 bit 4; R>=3 bit 11 | Every DocMDP P refuses; preserve field/widget relationships and locks | No granting category; approval signatures/document timestamps need breaking opt-in |
| Existing-page images / watermark | Bit 4 | Every DocMDP P refuses; refuse inability to preserve protected structures | No granting category; signature-breaking opt-in per adopted images |
| Approval signing into an existing empty signature field | R=2 bit 6; R>=3 bit 6 or 9 | P=1 refuses; P=2/3 may permit; target unlocked | Signature/Modify; no earlier-validity claim |
| Signing with creation of a field/widget | Bits 4 and 6 | Same signing certification checks; preserve locks and apply adopted signing's seed/field rules | Form/Add and Signature/Modify; never replace an occupied field |
| Document timestamp | Bit 6, retaining the existing engine boundary | P=1 refuses; P=2/3 may permit under adopted signing; preserve locks | No granting category; distinct from signing an existing field |
| Explicit unsigned rights removal / XFA abandonment | Bit 4 plus respective explicit consent | No certification; no affected protected-field change | Separate atomic preparations; rights rules below |
| Extract / concatenate | R=2 bits 4 and 5; R>=3 bits 5 and 11, per encrypted input occurrence | New derivative, not certified update; retained locked structures follow pages contract | Source unchanged; derivative loses signatures/certification/encryption only under pages' explicit loss/consent rules; no source-rights removal |
| Change permission settings | Owner authentication; R=5/6 initially | All signed/certified inputs refuse | No granting category; unsigned nongranting rights need explicit removal consent |
| Save accepted pending edits / no-op copy | Recheck effective changes' authority; no-op copy adds no edit-permission requirement | Save supplies no authority; P=1 still refuses an appended revision | Retain granting rights; Save never waives an earlier gate |

The assembly row does not adopt its still-draft preservation profile. New blank-page/image creation has no input-document security gate; image/output contracts apply.

Read rationale: existing structured field inspection/export is not page-text extraction or an FDF/XFDF export action. Add no bit-5 denial or Reader-rights export gate. Flatten rationale: [PdfFlattener](../../src/jaytwo.Pdf/Internal/PdfFlattener.cs) currently requires Modify and Annotate. The additive bit-4 proposal follows [forms' source interpretation](forms.md#cross-contract-authorization-and-output-mapping) without changing that legacy gate. Security adoption does not itself adopt form mutations (forms is adopted separately as W-219-FORMS-2026-10-31-R2) or narrow legacy Fill/FillForm. Form ReadOnly/password/rich/flag eligibility remains separate from document credentials.

## Certification and field locks: SECURITY-LOCK-01

Use the most restrictive applicable DocMDP claim. P=1 denies appended edits/signing; P=2/3 allow only the supported fill/signing rows, not arbitrary annotations or page content. Absent P in otherwise well-formed parameters defaults to 2. Invalid P, unresolved certification, malformed parameters or an unclassifiable referenced certification is restrictive P=1, never absence. Keep recorded claims separate from this conservative effective policy.

FieldMDP All locks all fields; Include locks named fields and descendants; Exclude locks all except named fields and descendants. Combine restrictions across signatures. Unknown actions, malformed/unresolved lists, undecodable names or unsafe hierarchy matching lock all potentially affected fields; if that set cannot be bounded, all fields lock. Never guess an unlocked target. An unsigned signature field's prospective Lock is not an applied FieldMDP transform; signing handles it under the adopted seed/lock contract. Unrelated edits preserve restrictions or refuse if preservation cannot be established. Reads may report malformed restrictions without asserting valid signatures.

## Reader rights: SECURITY-RIGHTS-01 to 05

### Grant-aware preservation: SECURITY-RIGHTS-01

Preserve rights granting every requested edit. Initially interpret catalog UR3 signatures with well-formed UR3 transform references, version name 2.2 (absent defaults to 2.2), and category name arrays as in the matrix. Every applicable reference must grant the operation. FullSave alone grants none of the edits above. Form/FillIn covers the supported fill/save action; Signature/Modify includes saving that signature. No independent new FullSave gate is imposed on those established workflows. Unknown categories confer no additional operation.

Legacy UR, unknown versions, missing transforms and malformed/dangling grants are **nongranting**, not validated permission. A malformed enclosing Perms structure whose unrelated entries cannot be preserved refuses mutation/removal. Retain the established Form/FillIn, EF/Create/Modify/Delete and Signature/Modify cases; creating a signature field additionally needs Form/Add. A grant never overrides passwords, certification, locks or operation eligibility, and proves neither cryptographic validity nor legacy Reader usability.

### Explicit removal authorization for unsigned nongranting rights: SECURITY-RIGHTS-02

An unsigned nongranting input refuses the dependent edit without explicit rights-removal consent, separate from the edit request and credentials. Preserve granting rights without compulsory removal. A caller may explicitly remove rights as a separate eligible preparation; a following edit remains a separate call.

### Rights-removal authority: SECURITY-RIGHTS-03

Removal needs general modification authority (user bit 4), even if a following fill needs only form permission. Remove only catalog Perms/UR and Perms/UR3 entries, preserving unrelated entries/associations, XFA and earlier pending edits. An empty Perms dictionary may remain. No historical-revision deletion or erasure of prior rights bytes is promised. Refuse signed removal and unestablishable unsigned status; usage-rights signatures alone do not count as already-signed for this policy.

### Atomic and repeat-safe rights removal: SECURITY-RIGHTS-04

One removal call succeeds completely or restores pre-call rights, XFA and earlier pending edits. Repeating completed removal is a no-op after normal lifetime, argument, authority, consent and unsigned checks. No-op does not bypass credentials. A later XFA-abandonment failure leaves an earlier successful removal pending. This is per-call pending-state atomicity, not destination rollback or a cross-call transaction.

### Owner-selected signed Reader-rights boundary: SECURITY-RIGHTS-05 (Q-33)

Keep signed granting edits eligible when every other gate permits them; preserve their grants. Refuse removal and every signed removal-dependent edit even with explicit consent, owner credentials or breaking opt-in. Signature presence is not blanket refusal. Signed means approval signature, document timestamp or certification, not the rights signature alone. Malformed structures preventing proof of the unsigned prerequisite refuse removal. Current code retains nongranting signed rights while permitting edits: that is an implementation gap against this selected target.

## Permission-setting output: SECURITY-SET-01

Accept an explicit replacement set of the eight user settings, not raw encryption dictionaries or passwords. Validate representability: high-quality print needs print; fill/accessibility requests must agree with effective bit semantics; effective PDF 2.0 cannot request accessibility denial via bit 10. For effective PDF 2.0 output, set bit 10 to 1 and refuse a request to deny accessibility extraction through that bit. For earlier versions at R?3, serialize the requested accessibility setting through bit 10 independently of bit 5. Serialize reserved bits as the handler requires. Refuse inconsistent requests rather than silently returning different settings. Return the resulting user-settings snapshot, distinguishing pending from persisted.

**Initial subset:** owner-authenticated, unsigned R=5/6 inputs. Update P and its matching encrypted 16-byte Perms block with the existing file key. Preserve R/V, filters, EncryptMetadata, O/U/OE/UE, file IDs, passwords and unrelated objects. Use ordinary incremental save, redefining the existing uncompressed encryption-dictionary object; original bytes remain a prefix. Historical permissions remain in old revisions: no secure erasure or revocation of copies. Pending reads show replacement settings. A failed setting call restores previous settings and earlier edits. No destination writes until explicit save; output/append rules belong to [opening/saving](opening-and-saving.md) and [shared](shared.md).

Refuse setting on unencrypted inputs (no encryption creation), R=2/3/4, signed inputs, unsupported schemes or inability to preserve key/credentials. This leaves R=2-4 opening/editing supported and their permission-changing output as a deferred capability gap.

**Output constraint and adopted boundary:** [PdfSecurity.FileKey](../../src/jaytwo.Pdf/Internal/PdfSecurity.cs) hashes PermissionBytes for R=2-4; changing P changes the key for existing objects. Replacing only the latest dictionary makes old ciphertext unreadable. R=5/6 instead validate encrypted Perms under a separately recovered key. ISO 32000-1 7.6.3 algorithms 2/3 and ISO 32000-2 7.6.4.3-4 describe this distinction (local reference collection `C:/jake/git/pdfdocs/text/`). The owner adopted R=5/6 first and explicit R=2-4 deferral, without a re-encryption rewrite authorization. The proposed incremental dictionary replacement still needs reopen/interoperability evidence; source analysis is not an output experiment.

## Limits, refusal, cancellation and failure: SECURITY-FAIL-01

Classification follows touched structures with proportional work/bounded buffering under [inspection's structural limits](inspection.md#limits-and-read-failures-inspect-fail-01), never allocation from untrusted counts. No universal password-size budget, timeout or cancellation latency is introduced. Cycles, structural limits and unsupported data produce controlled shared failure categories.

Observe supplied cancellation before authentication/classification, between phases and before committing a mutation; blocked synchronous reads need not interrupt. Tokenless synchronous properties gain no token API. Authentication failure returns no usable document; source ownership/cleanup follows opening. Read failure returns no fabricated authority and changes no pending state. Setting/removal failure or observed cancellation restores that call, retaining earlier edits. Borrowed inputs stay open without position restoration. These calls write/publish nothing; subsequent save/sign/workflow failures retain their own destination boundaries. Keep primary failure and report cleanup/restoration failures separately. No automatic retry or general rollback.

## Acceptance map

All targets are **unexecuted**, including compatibility/independent-tool checks.

| ID | Scenario and required observation |
| --- | --- |
| SECURITY-A1 | R=2-6 fixtures: empty/user/owner/wrong passwords, distinct roles/settings, supported filters, malformed/unsupported refusal and secret-free diagnostics; distinguish additive pre-2.0 accessibility bit 10 from copy bit 5 and the preserved legacy getter. |
| SECURITY-A2 | Each matrix row under independent bits, owner access, DocMDP P=1/2/3 and affected/unaffected FieldMDP; no gate overrides another. Compare legacy and additive flatten separately. |
| SECURITY-A3 | Granting unsigned/signed fills/EF edits retain rights; independent denials deny; signed nongranting edits refuse even with consent/owner/breaking opt-in. Signature reuse versus field creation. |
| SECURITY-A4 | Unsigned nongranting UR/UR3 removal needs consent and bit 4, is repeat-safe, preserves unrelated Perms/XFA/earlier edits, and restores on late failure/cancellation; malformed enclosing Perms refuses. |
| SECURITY-A5 | Dangling/malformed certification, locks, names and grants classify conservatively; independent inspection remains available without trust claims. |
| SECURITY-A6 | Owner R=5/6 set/save/reopen: unchanged passwords, intended effective bits, preserved key/cipher/IDs/unrelated data and original prefix, independently checked. Pre-2.0 output sets bit 10 independently of bit 5; PDF 2.0 output sets bit 10 to 1 and refuses an accessibility-denial request. R=2-4/unencrypted/signed setting refuses unchanged. |
| SECURITY-A7 | Fail authentication, classification, setting, removal and subsequent save separately; assert each source/pending/destination boundary and separate primary/cleanup diagnostics. |

## Remaining engineering gaps

Explicit consent/atomic preparation, Q-33 signed refusal, malformed restriction classification, additive matrix APIs and permission output need implementation/evidence. Preserve legacy contracts during additive flatten work. Additive accessibility settings must distinguish bit 10 from bit 5 without changing the historical getter; target inspection and permission-output evidence is unexecuted. R=5/6 key/dictionary state, later edits and independent save/reopen need proof; R=2-4 output remains deferred. Structure tests establish neither trust nor Reader features.

## Adoption record

Approved by the owner on 2026-10-31 after applying the supplied review's accessibility correction to SECURITY-AUTH-01 and SECURITY-SET-01: pre-PDF-2.0 R?3 accessibility uses bit 10 separately from bit 5, the historical CanExtractForAccessibility getter remains available for compatibility, and PDF 2.0 output sets bit 10 to 1 and refuses denial through that bit. The complete matrix, conservative locks/nongranting rights and Q-33 boundary stand. Approval retains owner-only unsigned R=5/6 initial permission setting and R=2-4 deferral; no new rewrite strategy is authorized. This is contract adoption and an output-feasibility decision, not an assertion of implementation, interoperability, executed acceptance or release qualification. Runtime, behavior, acceptance outcomes and qualification records remain unchanged. History: [security](drafting/history.md#security-reader-rights-and-xfa).
