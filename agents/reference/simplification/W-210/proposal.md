# W-210 proposal (draft; nothing here is approved, shipped, or applied)

Companion to `findings.md`. Protected files (`AGENTS.md`, `docs/spec/`, `QUALIFICATION.md`, `.agents/skills/`) and W-158 are **not edited**; this file holds proposed wording for the owner. Queue order is unchanged.

## 1. Shipped trust-boundary text that must stay while anything is unresolved

These statements ship today and are the minimum W-158 cannot weaken. They hold under every candidate.

| Statement | Location (at `f78114b`) |
| --- | --- |
| "None of these properties establishes cryptographic signature validity or trust. No trust, revocation, or cryptographic validation." | `docs/spec/phase-1.md` R9, §11 |
| "Offset coincidence is independent of signature validity and is not cryptographic coverage." | `docs/spec/phase-1.md` R10 |
| "Inspection is structural. It does not verify the CMS signature value or the signed digest, the signer's identity or certificate trust, revocation, a time-stamp token, PAdES conformance, or whether a later change was permitted… A signature can be reported in full, with a well-formed byte range, and still be invalid." | `PdfSignatureInfo` remarks |
| "It is an offset match only: the byte range's structure, the signature, and the covered bytes are not checked." | `PdfRevision.EndsAtSignatureByteRange` |
| "validates nothing: not the digest, the signature, trust, or revocation. A CMS that does not parse throws `PdfParseException`; field reads still work." | `docs/behavior.md` line 56 |
| "Signature inspection reports what the file claims; it does not validate." | `README.md` line 17 |
| Strict failure list for malformed `/ByteRange`, `/M`, `/Contents`, CMS | `ACCEPTANCE.md` line 124 |
| Existing signatures stay a byte prefix of later saves; flatten/rotate need explicit opt-in | `docs/behavior.md` line 56, `AGENTS.md` signature rules |

No candidate may remove or soften a row without a replacement that says the same thing about the members that remain.

## 2. Disposition of W-158

**Retain, unchanged in purpose; clarify dependency and wording.** Reasons: every statement W-158 must add is property-neutral (five definitions, historical-content warning, fail-closed rule); D-66 already says the guidance stays necessary if properties are narrowed; the research shows no candidate moves a retained safety consumer, so no W-158 sentence becomes false under any candidate.

Mechanical effect: W-210 completing satisfies W-158's `depends_on: W-210`. W-158 should be executed after the owner picks a candidate **only if** that candidate removes a member W-158's examples would name; otherwise it can run as written. No rescope, supersession or cancellation is proposed.

### Proposed replacement text for W-158 (for the owner's approval; not applied)

Front matter: unchanged.

Scope paragraph, replacing "Update public documentation and relevant API remarks after W-150 settles the inspection names.":

> Update public documentation and relevant API remarks so they use the member names that exist when the item runs. Describe only members that ship; when W-210's follow-up items remove a member, drop its sentence rather than rewording around it.

Acceptance criteria, appended to the existing four:

> - The documentation names the structural facts that the library does and does not read, using only shipped members: it reads `/ByteRange` structure and the two gap delimiter bytes of `/Contents`; it never reads the signed bytes and never hashes them.
> - A reader who finds a well-formed `ByteRangeStatus` or a revision whose end matches a byte range is told that this is a structural statement and not evidence the signature is valid, current, or trusted.
> - A reader is told which questions need an external validator: digest and signature verification, certificate trust, revocation, time-stamp token verification, PAdES profile classification, and whether a later change was permitted.

Evidence line, replacing "focused inspection tests sufficient to confirm every behavioral statement already ships":

> Documentation link and terminology checks, XML-documentation build, and focused inspection tests (`SignatureInspectionTests`, `RevisionTests`) sufficient to confirm every behavioral statement already ships. Each sentence cites a member or rule that exists at execution time.

### Proposed documentation block (target `docs/behavior.md` after the logical-revisions bullet; wording for W-158 to adopt, trimmed to shipped members)

> **What signature inspection tells you.** `Signatures` and `Revisions` are structural. They report what the file says about each signature and where each revision ends. They do **not** verify the signature, recompute any digest, evaluate certificate trust or revocation, verify a time-stamp token, classify PAdES conformance, or decide whether a later change was permitted. A signature can be listed with a well-formed byte range and still be invalid, and a revision that ends at a byte range's end is an offset match, not proof of coverage. Use a validator for those questions. Write permissions are decided separately, from the document's DocMDP and FieldMDP and each field's lock, not from the inspection properties; a malformed inspection value does not change what a write may do.
>
> **Earlier revisions are not removed.** An incremental save or signature appends a revision and keeps the earlier bytes. Earlier revisions, including any signature and any content that was later changed, remain in the file and are recoverable by anyone holding it. Publish or share a rewritten file, not an incrementally saved one, when earlier content is sensitive; this library does not rewrite a file in place or redact.
>
> **Malformed structures.** By default `Signatures` throws `PdfParseException` when a signature dictionary or its CMS is malformed, and a certification that does not resolve is treated as `P` 1 by the write rules. Other reads on the same document still work.

(The last sentence of the first paragraph is measured by probe P2: `Pages`, `Permissions`, `Revisions` and a metadata save all succeeded with a malformed CMS that made `Signatures` throw.)

## 3. Separate follow-up scopes (each needs its own owner-approved item)

Names are suggested; IDs are the owner's to assign.

### 3.1 Item "Remove the rich CMS reader from signature inspection" (cut B)

- Purpose: delete the hostile-`/Contents` BER parser and the seven CMS-derived members.
- Allowed paths (starting point): `src/jaytwo.Pdf/Signatures/CmsSignatureReader.cs`, `PdfSignatureInfo.cs`, `src/jaytwo.Pdf/Internal/PdfSignatureInspector.cs`, `tools/fuzz/CmsFuzzTarget.cs`, `tools/fuzz/CmsSeeds.cs`, `tools/fuzz/cms-seeds.py`, `tools/fuzz/seeds/cms/`, `tools/fuzz/FuzzTargets.cs`, the oracle/inspection tests named in `findings.md`, `ACCEPTANCE.md`, `docs/behavior.md`, `README.md`, plus `docs/spec/phase-1.md` R9 and the G7 large-file gate ("signature inspection and revision listing on a 1 GiB file") **with owner approval of wording**. The BER tolerated quirk is documented only in `docs/behavior.md` line 60 and `ACCEPTANCE.md` line 126; `docs/spec/phase-1.md` §8 lists candidates only and does not name it.
- Draft spec wording for R9 (owner to approve): replace "signer certificate (DER), chain certificates present in the CMS, digest and signature algorithm, `/M` and CMS signing time" with "`/M`", and drop "whether a timestamp token is embedded and its time". Retain the trust sentence unchanged.
- Compatibility/recovery: pre-1.0, no package. Caller recovery for `WellFormed` signatures in `findings.md` P5 (about 12 lines and a caller-owned CMS library). No credential or key burden. Gap: signature dictionaries in object streams are reported `GapNotContents` today, so the retained `ByteRange` does not locate their `/Contents`.
- Verification (proportionate): focused `SignatureInspectionTests`, `RevisionTests`, `CorpusTests`, fuzz smoke (`pdf-fuzz-qualification` rules), and a named signing-interop checkpoint because test oracles change; no matrix unless the owner names it.
- Effort 2–4 engineer-days (inferred). Residual: loss of signer identity display; ACCEPTANCE line 125's agreement claim shrinks to retained members.

### 3.2 Item "Narrow revision and coverage properties" (cut A1+, optionally A2)

- Purpose: remove `ByteRangeRevisionNumber`, `Revisions`/`PdfRevision` (A1+); optionally `ByteRangeStatus`, `ByteRangeEnd`, `HasLaterRevisions` (A2).
- Allowed paths (starting point): `PdfRevision.cs`, `PdfDocument.cs` (`Revisions`), `PdfSignatureInfo.cs`, `PdfSignatureInspector.cs`, `PdfByteRangeStatus.cs` (A2), `RevisionSection.cs` and `RevisionSections` recording, `PdfDocumentLoader.cs`/`PdfObjectStore.cs` (recording only), `RevisionTests.cs`, `LargeWorkloadTests.cs`, `XrefStreamAppendTests.cs`, comparison `OutputCheck.cs`, `tools/fuzz/FuzzTargets.cs`, docs rows. R10 would need owner-approved removal text, and G7 would lose "revision listing" (the 1 GiB sparse check on `Signatures` would remain).
- Draft R10 wording if removed (owner to approve): "Revision listing is not offered. The raw `/ByteRange` of each signature is reported; callers compare `ByteRange` endpoints with the file's length."
- Compatibility/recovery: callers compare `ByteRangeEnd` (or `ByteRange`) with their own file length; loses the xref-based nuance that trailing bytes with no xref section are not a revision. A2 additionally loses the library's gap/containment checks.
- Verification: focused revision/inspection tests, `XrefStreamAppendTests`, large-workload sparse checks, no matrix.
- Effort 1–2 days (A1+), 2–3 days (A2) (inferred). Do not take A2 without owner acceptance of the lost self-consistency check.

### 3.3 Not proposed

- Basic-listing-only (cut C): savings are two disjoint cuts that already have separate scopes above.
- Per-signature failure isolation: changes a documented guarantee and adds API; outside this research.
- Any validation, trust or revocation feature: excluded by the charter.

## 4. Open owner choices

1. Retain R9/R10 (recommended default), or price 3.1 and/or 3.2 as separate items.
2. If 3.1: accept the loss of signer identity, certificate list, algorithm OIDs, CMS signing time and token time from `Signatures`, and of the BER tolerated quirk.
3. If 3.2: accept losing `Revisions` and the xref-based "later revision" nuance, and whether `ByteRangeStatus` stays.
4. Approve the W-158 wording in section 2 or return it.
