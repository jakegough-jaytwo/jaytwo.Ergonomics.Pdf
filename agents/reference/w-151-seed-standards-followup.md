# W-151: focused seed-value standards follow-up

Copy the prompt below to an expert agent. It is self-contained; no repository access is needed.

---

Please resolve the remaining standards questions for seed-value enforcement in a managed .NET PDF signing engine. Give an actionable rule for each scenario below, with primary-standard clauses/table entries and short supporting excerpts. Distinguish a normative conclusion from an interoperability policy, and identify any uncertainty that the standards do not resolve. We cannot choose an interpretation by silently adding signing capabilities.

## Settled scope and verified sources

The engine writes incremental signature revisions using a bounded appendix. It reuses an exact named empty signature field, writes `/Filter /Adobe.PPKLite`, and writes either CMS `/Type /Sig` with `/SubFilter /adbe.pkcs7.detached` or `/ETSI.CAdES.detached`, or document timestamp `/Type /DocTimeStamp` with `/SubFilter /ETSI.RFC3161`. CMS options select SHA-256, SHA-384, or SHA-512; timestamps use SHA-256 only. CMS options also expose Reason. The timestamp dictionary omits Reason. No certification-creation, document-locking, signature-policy, certificate-policy, revocation, or appearance-selection options exist. We may refuse unsupported applicable mandatory constraints before provider invocation; optional or irrelevant entries must not cause new refusals.

The caller supplies `IPdfSignatureProvider` with only `Kind` and `CreateContentsAsync(context, cancellationToken)`. Context exposes the requested format/digest, signed byte ranges, signing time, reservation size, and a stream of signed bytes. The engine cannot preflight a custom provider's certificate or embedded timestamp configuration. Built-in providers use a host signer and may optionally obtain an embedded signature timestamp; the general interface exposes neither detail. No algorithms, profiles, signature validation, certificate validation, signer capabilities, or provider introspection may be added.

We directly read ISO 32000-1:2008 section 12.7.4.5 and Tables 232–235 from Adobe's official copy:
https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/PDF32000_2008.pdf

This confirms ordered SubFilter selection, the special required-Reasons omission rule, independently flagged Cert and TimeStamp dictionaries, and the general rule that an absent optional key imposes no constraint. We also read the official PDF 2.0 errata:
https://pdf-issues.pdfa.org/32000-2-2020/clause12.html

The errata confirms unordered DigestMethod names, the exception for mechanisms requiring a specific digest, parser capability V=3 for PDF 2.0, and that a document timestamp dictionary is a signature-field value. The publicly available errata does not provide the complete PDF 2.0 seed/certificate tables. The sponsored full specification is distributed through the PDF Association's checkout; an Adobe-hosted FDIS link in web search returns 404. Please consult the actual published PDF 2.0 text, including applicable errata, rather than treating a draft or third-party schema as authoritative.

The previous expert review explicitly left the following questions unresolved. The owner has approved the separate rollback failure-reporting contract; no further advice on rollback is requested.

## 1. Unflagged PDF 2.0 certificate signature-policy entries

Table 238 reportedly defines SignaturePolicyOID, SignaturePolicyHashValue, SignaturePolicyHashAlgorithm, and SignaturePolicyCommitmentType without assigning individual Cert/Ff bits. The earlier review said it could not determine whether populated entries are advisory or independently prescriptive.

For `/SV << /Cert << /Ff 0 /SignaturePolicyOID (...) >> >>`, is the policy mandatory for a noninteractive signing engine? What changes if Cert/Ff contains only unrelated flags or is absent? What if the hash/commitment entries appear without SignaturePolicyOID? Give a per-entry presence/dependency rule grounded in the exact published wording. Should this engine ignore, reject as malformed, or refuse as an unsupported mandatory requirement? We must not misrepresent an advisory entry as mandatory or invent flag bits.

## 2. Existing-field seeds when applying a document timestamp

For a field that will receive `/Type /DocTimeStamp /SubFilter /ETSI.RFC3161`, classify these as applicable-and-enforceable, applicable-but-unsupported (refuse), irrelevant (ignore), or malformed, explaining the normative basis:

- Required Filter matching Adobe.PPKLite and ordered SubFilter containing ETSI.RFC3161.
- Required Reasons absent, singleton period, or a nonempty list of approval reasons; the engine writes no timestamp Reason.
- Required DigestMethod containing SHA256, or excluding SHA256. Does this constrain the RFC 3161 message-imprint algorithm, the TSA's CMS signature digest, or neither?
- Cert/Ff requiring Subject or Issuer. Does this constrain the TSA credential or apply only to a human/organization approval signer?
- TimeStamp/Ff=1. Does an independent document timestamp satisfy this, or is a CMS signature with an embedded signature-time-stamp required? Is this entry irrelevant when the requested operation is itself a document timestamp?
- MDP/P=0. Must it refuse a document timestamp because it requires an approval/author signature, or is it irrelevant? MDP/P=1–3 cannot be satisfied because the engine cannot create certification signatures.

We need to avoid both bypassing applicable mandatory restrictions and refusing irrelevant ones. If the standards do not determine these cases, recommend the smallest explicit owner disposition and identify which exact cases it must cover.

## 3. Mandatory DigestMethod with an arbitrary provider

For an ordinary CMS signature, the engine can enforce that the requested digest belongs to the seed array and pass that digest through the provider context. It cannot independently establish the digest actually embedded in arbitrary returned CMS without parsing/checking provider output, which is not authorized here. Does a conforming handler contract permit request-level enforcement with the provider responsible for honoring context, or must this engine refuse all mandatory digest constraints for arbitrary providers? Distinguish seed enforcement from cryptographic validation. If an explicit provider responsibility is sufficient, propose exact documentation wording without expanding the interface.

## 4. PDF 2.0 LockDocument in a noninteractive engine

The PDF Association issue tracker still marks the locking-mechanism question open:
https://github.com/pdf-association/pdf-issues/issues/760

This engine does not create locking transforms or expose locking choices. For a present required LockDocument, can `/auto` be satisfied by choosing no new locking without UI? Can required `/false` be satisfied by adding no locking while preserving any existing field `/Lock`, or must an existing `/Lock /P` or FieldMDP-related rule also be inspected? Required `/true` cannot be implemented and would be refused. Separate the normative obligation from Acrobat-specific behavior; do not recommend new locking support.

Please return a compact decision table, citations to the published standard/errata, any essential missing facts, and a minimal list of cases requiring an explicit owner policy. Do not label the unresolved choices as settled ISO requirements.
