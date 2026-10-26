# W-151 expert review: PDF seed-value enforcement and signing rollback

Owner disposition required

## Executive assessment

The proposed direction is sound, with several necessary refinements. It can meet W-151 without adding signer capabilities, cryptographic validation, document-wide staging, or new dependencies.

I recommend:

1. Enforce applicable mandatory `/SV` constraints before provider invocation, using the actual signature dictionary that will be written. In particular, respect `/SubFilter` preference ordering, the independent certificate and timestamp flags, and restrictions that do not have top-level flags.
2. Refuse requirements the existing signing boundary cannot establish, rather than presuming that arbitrary providers produce conforming content.
3. Keep optional and irrelevant constraints non-blocking. Treat unknown flag bits differently from recognized mandatory entries with malformed values.
4. Preserve the original signing exception whenever practical, attaching structured restoration-failure information. A dedicated combined exception is a legitimate alternative, but changes public exception behavior.
5. Seek owner approval before changing the charter. The existing charter's unconditional promise conflicts with its documented destination-failure exception.
6. Test output length, content, stream position, exception identity, and retry safety separately. Restoring in-memory state does not establish physical stream restoration.

There are two standards issues that especially deserve owner attention: the interpretation of PDF 2.0 certificate signature-policy seed entries without matching `/Ff` bits, and the application of signature-field seeds when reusing a field for `/DocTimeStamp`.

This is a proposal review based on the supplied facts and published standards. No repository, protected specification, or implementation has been inspected.

## A. Standards-backed `/SV` enforcement

### A1. Normative foundation

The principal references are:

| Source                                                                                                                                             | Relevant provisions                                                                                                              |
| -------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| [ISO 32000-1:2008](https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/PDF32000_2008.pdf?utm_source=chatgpt.com)                         | §12.7.3 field dictionaries; §12.7.4.5 signature fields; Tables 232–235; §12.8 digital signatures; Table 252 signature dictionary |
| [ISO 32000-2:2020](https://www.pdfa-inc.org/product/iso-32000-2-pdf-2-0-bundle-sponsored-access/?utm_source=chatgpt.com)                           | §§12.7.4, 12.7.5.5, 12.8; Tables 235–238, 255, 257 and 260                                                                       |
| [PDF Association's ISO 32000-2 errata](https://pdf-issues.pdfa.org/32000-2-2020/clause12.html?utm_source=chatgpt.com)                              | Corrections to Tables 237 and 238, including `/DigestMethod`, `/V`, and `/KeyUsage`                                              |
| [ETSI EN 319 142-1 V1.2.1](https://www.etsi.org/deliver/etsi_EN/319100_319199/31914201/01.02.01_60/en_31914201v010201p.pdf?utm_source=chatgpt.com) | §4.1, §§5.2–5.4, §§6.2–6.3: PAdES baseline signature requirements                                                                |
| [ETSI EN 319 142-2 V1.2.1](https://www.etsi.org/deliver/etsi_EN/319100_319199/31914202/01.02.01_60/en_31914202v010201p.pdf?utm_source=chatgpt.com) | §§4.2.1–4.2.6: CMS signatures, timestamps, revocation and seed values                                                            |

PDF 1.7 defines seed values beginning in PDF 1.5, with subsequent entries introduced in PDF 1.6 and 1.7. PDF 2.0 adds entries for document locking and signature appearance. The standards distinguish requirements from preferences; a seed dictionary is not a command to reject every unfamiliar or unfulfilled suggestion. ISO 32000-1 also instructs signature handlers to disregard private seed entries that are not pertinent to them.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://developer.adobe.com\&sz=32)

Adobe Developer

+2



An important version detail: use the PDF 2.0 errata-adjusted text for `/DigestMethod`. The earlier RSA/DSA-specific language has been corrected. Under the corrected text, the digest array is unordered, and signature mechanisms requiring a specific digest can disregard the entry. The corrected `/V` also expressly defines parser capability level 3 for PDF 2.0.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://pdf-issues.pdfa.org\&sz=32)

Errata for PDF specifications

+1



### A2. Flag mapping

The top-level seed-value dictionary has its own `/Ff`, defaulting to zero.

| Bit position | Mask    | Constraint          | Introduced                                                |
| ------------ | ------- | ------------------- | --------------------------------------------------------- |
| 1            | `0x001` | `/Filter`           | PDF 1.5; required-handler behavior specified from PDF 1.7 |
| 2            | `0x002` | `/SubFilter`        | PDF 1.5                                                   |
| 3            | `0x004` | `/V`                | PDF 1.5                                                   |
| 4            | `0x008` | `/Reasons`          | PDF 1.5                                                   |
| 5            | `0x010` | `/LegalAttestation` | PDF 1.6                                                   |
| 6            | `0x020` | `/AddRevInfo`       | PDF 1.7                                                   |
| 7            | `0x040` | `/DigestMethod`     | PDF 1.7                                                   |
| 8            | `0x080` | `/LockDocument`     | PDF 2.0                                                   |
| 9            | `0x100` | `/AppearanceFilter` | PDF 2.0                                                   |

Crucially, `/Cert`, `/MDP`, and `/TimeStamp` do not have top-level `/Ff` bits. `/Cert` contains an independently flagged dictionary; `/TimeStamp` contains its own required-timestamp flag; `/MDP` specifies signature type and permissions through its contents.

These mappings follow ISO 32000-1 Table 234 and ISO 32000-2 Table 237.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://developer.adobe.com\&sz=32)

Adobe Developer

+1



### A3. Concrete enforcement table

In the table below, allow means that the named requirement can be established without expanding the current API. Refuse means fail before calling the provider. A row marked conditional must first be evaluated for applicability; it is not a reason to reject unrelated optional values.

| `/SV` entry                | Mandatory condition and meaning                                                                                                            | Can current signing satisfy it?                                                                                        | Recommended treatment                                                                                                                                                                         |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/Type`                    | If present, shall identify an SV dictionary as `/SV`. PDF 1.5+. Not `/Ff`-flagged.                                                         | Yes, by parsing.                                                                                                       | Check structural validity when using the dictionary.                                                                                                                                          |
| `/Ff`                      | Identifies required constraints. Missing means `0`. PDF 1.5+.                                                                              | Yes.                                                                                                                   | Interpret recognized bits independently. Do not treat unset bits as required.                                                                                                                 |
| `/Filter`                  | Bit 1; PDF 1.7 required-handler semantics. Specifies the signature handler.                                                                | Yes, when `/Adobe.PPKLite`.                                                                                            | Required and different: refuse. Matching: allow. Unflagged preference: ignore if unsuitable.                                                                                                  |
| `/SubFilter`               | Bit 2; PDF 1.5+. Ordered array of acceptable encodings. First supported encoding wins.                                                     | Yes, for the relevant configured output: `/adbe.pkcs7.detached`, `/ETSI.CAdES.detached`, or timestamp `/ETSI.RFC3161`. | Require the selected output to equal the first encoding actually supported for the operation. Do not silently change options.                                                                 |
| `/V`                       | Bit 3; minimum seed-dictionary parser capability. Levels 1, 2 and, in PDF 2.0, 3.                                                          | Conditional on actual parser coverage.                                                                                 | Allow only a capability level the implementation genuinely recognizes. Otherwise refuse. Not a PDF header version or signature-profile selector.                                              |
| `/Reasons`                 | Bit 4; restricts signature `/Reason`. PDF 1.5+.                                                                                            | Yes, from Reason option and actual output omission.                                                                    | Apply exact matching and special omission cases described below.                                                                                                                              |
| `/DigestMethod`            | Bit 7; unordered set of acceptable digest names. PDF 1.7+.                                                                                 | Yes for the requested digest; provider's actual digest may be unverified.                                              | Enforce the requested SHA-256/384/512 against the allowed set when applicable. Refuse unsupported requested digests. Do not confuse checking the request with proving arbitrary CMS contents. |
| `/Cert`                    | Nested certificate restrictions. No top-level bit. PDF 1.5+; additional fields PDF 1.7/2.0.                                                | Generally no.                                                                                                          | Evaluate nested flags independently. Refuse applicable mandatory requirements that cannot be established.                                                                                     |
| `/MDP`                     | PDF 1.6+. `/P 0` means approval/author signature; `/P 1–3` requires certification with corresponding DocMDP permissions. No top-level bit. | Approval only.                                                                                                         | Allow `/P 0` for a genuine approval signature. Refuse `/P 1–3`. Absent `/P` imposes no rule.                                                                                                  |
| `/TimeStamp`               | PDF 1.6+. Nested `/Ff 1` requires a timestamp on the signature; default 0. URL identifies an RFC 3161 TSA.                                 | Requirement cannot be verified before arbitrary provider invocation.                                                   | Refuse mandatory timestamp requirements. Optional timestamp hints must not block signing.                                                                                                     |
| `/LegalAttestation`        | Bit 5; permitted legal attestations. PDF 1.6+.                                                                                             | No corresponding signing facility.                                                                                     | Refuse applicable mandatory requests; ignore advisory values.                                                                                                                                 |
| `/AddRevInfo`              | Bit 6; if true and required, check relevant certificate revocation and embed the revocation information. PDF 1.7+.                         | No.                                                                                                                    | Refuse required `true`; do not refuse merely because the key exists with `false` or is advisory.                                                                                              |
| `/LockDocument`            | Bit 8; PDF 2.0. `/true`, `/false`, `/auto` govern document locking at signing.                                                             | No general locking option or proved locking policy.                                                                    | Refuse required `/true`. Allow required `/false` only if no locking is actually imposed. Treat required `/auto` according to its conditional semantics rather than automatically refusing it. |
| `/AppearanceFilter`        | Bit 9; PDF 2.0. Named signature appearance must be available and used when required.                                                       | Not established from supplied API.                                                                                     | Refuse required named appearances unless the existing appearance is demonstrably the requested named appearance. Ignore optional preferences.                                                 |
| Private or unknown entries | Handler-specific extension rules; no automatic standard obligation.                                                                        | Not necessarily applicable.                                                                                            | Ignore entries that do not pertain to this handler. Do not invent extension semantics.                                                                                                        |

The `/MDP`, `/TimeStamp`, `/LegalAttestation`, `/AddRevInfo`, locking and appearance distinctions come from the entries of ISO 32000-1 Table 234 and ISO 32000-2 Table 237.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://developer.adobe.com\&sz=32)

Adobe Developer

+2



#### `/SubFilter`: membership alone is insufficient

ISO says to choose the first supported encoding from the seed array. Consequently, the following matters:

Suppose the seed value is:

```
/SV <<
  /Ff 2
  /SubFilter [
    /ETSI.CAdES.detached
    /adbe.pkcs7.detached
  ]
>>
```

Both CMS formats are available in the described engine. If the caller requests the default `/adbe.pkcs7.detached`, it is not sufficient to observe that this value occurs in the array. The first supported value is `/ETSI.CAdES.detached`.

Recommendation: refuse the default request instead of switching it silently to PAdES.

An unsupported earlier entry does not necessarily prevent signing: scan in order, skip unsupported encodings, and use the first supported encoding for the applicable signing operation.

Do not assume that `/ETSI.RFC3161` is supported as a CMS signature encoding merely because the engine separately implements document timestamps.

For a claimed PAdES baseline signature, ETSI EN 319 142-1 §6.3 also requires `/ETSI.CAdES.detached`; the ordinary `/adbe.pkcs7.detached` mode cannot be represented as PAdES B-B simply because it carries CMS.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://developer.adobe.com\&sz=32)

Adobe Developer

+1



#### `/Reasons`: three materially different cases

| Required seed             | Requested `/Reason`   | Result                                                   |
| ------------------------- | --------------------- | -------------------------------------------------------- |
| `[(Approved) (Reviewed)]` | `(Approved)`          | Allow                                                    |
| `[(Approved) (Reviewed)]` | Omitted, or `(Other)` | Refuse                                                   |
| `[(Approved) (Reviewed)]` | Any                   | Allow only exact listed text                             |
| `[]`                      | Any                   | Refuse: there is no permitted choice                     |
| `[(.)]`                   | Omitted               | Allow                                                    |
| `[(.)]`                   | Any populated reason  | Refuse                                                   |
| `/Reasons` absent         | Omitted               | Allow; mandatory bit with absent entry requires omission |

The last case is important. ISO 32000-1 Table 234 expressly requires omission of `/Reason` when mandatory `/Reasons` is omitted or contains the sole period marker. An empty array is not the period marker.

For an array containing the period marker alongside other values, do not extend the special singleton rule by assumption. Follow the ordinary array semantics or flag this unusual producer behavior for an interoperability fixture.

The intended comparison is the decoded PDF text-string value, not the original byte spelling or display formatting.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://developer.adobe.com\&sz=32)

Adobe Developer



#### `/DigestMethod`: enforce the correct thing

For PDF 1.7, ISO 32000-1 Table 234 originally describes the digest constraint in RSA-specific terms. PDF 2.0's corrected Table 237 removes that narrow distinction and says that mechanisms inherently requiring a particular digest disregard this seed entry.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://pdf-issues.pdfa.org\&sz=32)

Errata for PDF specifications



For this work item:

- A requested SHA-256 with required `[/SHA256 /SHA384]` is a supported preflight match.
- A requested SHA-512 with the same required array is a preflight mismatch.
- SHA-1, RIPEMD-160, and other unsupported algorithms should not be added just to satisfy seeds.
- A custom provider receiving `RequestedDigest = SHA256` is not cryptographic proof that its returned signature actually uses SHA-256.

Recommended boundary: enforce what the engine controls and explicitly qualify what provider output cannot be established to satisfy. If W-151's definition of enforcement requires a guarantee about the bytes inside an arbitrary provider's CMS response, the current interface cannot provide it. That requires an owner decision to refuse such cases, not an unapproved provider-introspection or signature-validation feature.

### A4. Nested `/Cert` dictionary

ISO 32000-1 Table 235 and ISO 32000-2 Table 238 give `/Cert` its own `/Ff`, defaulting to zero.

| Certificate bit | Mask   | Entry        | Meaning and recommended treatment                                                                                                         |
| --------------- | ------ | ------------ | ----------------------------------------------------------------------------------------------------------------------------------------- |
| 1               | `0x01` | `/Subject`   | Allowable DER-encoded signing certificates. Required: refuse without reliable signing-certificate identity.                               |
| 2               | `0x02` | `/Issuer`    | Allowable direct or indirect issuers. Required: refuse without certificate-chain information.                                             |
| 3               | `0x04` | `/OID`       | Required certificate-policy OIDs. Refuse if required and unverifiable. ISO also conditions its use on nonempty `/Issuer`.                 |
| 4               | `0x08` | `/SubjectDN` | Required distinguished-name attributes. Refuse if required and unverifiable.                                                              |
| 5               | `0x10` | Reserved     | No defined certificate restriction. Do not invent one.                                                                                    |
| 6               | `0x20` | `/KeyUsage`  | Required key-usage pattern or patterns. Refuse if required and unverifiable.                                                              |
| 7               | `0x40` | `/URL`       | Usage determined by `/URLType`. Default `/Browser` is credential-enrollment guidance, for which ISO says to ignore the URL mandatory bit. |

The `URLType=Browser` exception is easily overlooked. A set URL bit is not always a reason to refuse.

Likewise, `/OID` has a dependency on `/Issuer`, and `/KeyUsage` has its own nine-position pattern semantics. A strict implementation must not interpret a key-usage pattern as an arbitrary opaque string.

These details are specified by ISO 32000-1 Table 235 and carried into PDF 2.0 Table 238.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://developer.adobe.com\&sz=32)

Adobe Developer

+2



PDF 2.0 additionally defines certificate seed entries:

- `/SignaturePolicyOID`
- `/SignaturePolicyHashValue`
- `/SignaturePolicyHashAlgorithm`
- `/SignaturePolicyCommitmentType`

These have no individually assigned `/Cert /Ff` bits. Their semantics concern the signature policy or commitment represented in the signature, rather than simply the X.509 certificate-policy extension associated with the older `/OID` key. They must not be conflated.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://developer.adobe.com\&sz=32)

Adobe Developer

+2



There is a genuine interpretive issue here. The new entries are optional to include, yet language such as the policy to use when signing may carry a substantive constraint even without a corresponding flag. I would not automatically classify all populated PDF 2.0 policy entries as ignorable advice, nor automatically refuse every occurrence.

Owner disposition needed: decide whether W-151 treats applicable, unflagged PDF 2.0 signature-policy requirements as independently prescriptive, or reserves them for a separately specified feature. Do not invent flag mappings for them. Where the chosen interpretation makes a requirement mandatory, the present API must refuse because it cannot establish signature-policy conformance.

### A5. Unknown bits, malformed values, and optional entries

I recommend the following error policy; it is an implementation recommendation, not a separate ISO error taxonomy.

| Input condition                                         | Recommendation                                                                                                            |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `/SV` absent                                            | No seed enforcement required.                                                                                             |
| `/Ff` absent                                            | Treat as zero.                                                                                                            |
| Known mandatory bit with valid matching entry           | Evaluate and enforce.                                                                                                     |
| Known mandatory bit with wrong type or malformed value  | Refuse before provider invocation when applicable.                                                                        |
| Known optional entry with malformed value               | Do not introduce a seed-constraint refusal solely for it; ignore as advice, subject to existing PDF parsing requirements. |
| Unknown higher flag bit                                 | Do not infer a new constraint or refuse solely because the bit is set.                                                    |
| Required `/V` beyond recognized parser capability       | Refuse.                                                                                                                   |
| Unrecognized private entry not pertinent to the handler | Ignore.                                                                                                                   |
| Malformed `/SV /Ff` itself                              | Refuse if mandatory status of applicable entries cannot be determined safely.                                             |
| Invalid `/MDP /P`                                       | Refuse if its intended signature restriction cannot be safely interpreted.                                                |

The engine should distinguish invalid PDF structure from an unfulfilled mandatory seed. W-151 should not become an excuse to make all optional seed data fatal.

For future flag bits, ignoring an unknown bit is different from claiming that an unknown mandatory extension has been satisfied. A genuinely required future parser capability declared through `/V` must still be honored or refused.

### A6. Separate `/Lock` dictionary

A signature field may also contain `/Lock`, defined separately from `/SV`.

ISO 32000-1 Table 233 and ISO 32000-2 Table 236 describe field-locking rules. PDF 2.0 additionally defines permission-related behavior.

`/Lock` is not another entry governed by seed `/Ff`. Likewise, `/LockDocument` is not a substitute for the full lock-dictionary or DocMDP permission semantics.

Recommendation: account for locking when deciding whether a required `/LockDocument /false` can actually be satisfied, but do not silently expand W-151 into complete field-lock or certification support.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://developer.adobe.com\&sz=32)

Adobe Developer

+1



## B. Field hierarchy and `/DocTimeStamp` applicability

### B1. `/SV` is not inheritable

ISO 32000-1 §12.7.3 and Table 220, and ISO 32000-2 §12.7.4.1 and Table 226, distinguish explicitly inheritable field attributes from other attributes. `/SV` is a signature-field-specific entry, not a designated inheritable common-field attribute. In PDF 1.7, its value is specified as an indirect reference.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://opensource.adobe.com\&sz=32)

Adobe Open Source

+1



The correct lookup model is:

Conceptual field-tree example, not a claim about the project's actual object structure.

For the current engine:

1. Resolve the exact requested fully qualified name through the AcroForm field hierarchy.
2. Identify every actual terminal signature field/value owner the signing operation will populate.
3. Read each owner's own `/SV`; resolve its indirect reference and nested references using the normal PDF object resolver.
4. Evaluate all applicable mandatory constraints before invoking the provider.
5. Do not independently inherit an ancestor's `/SV` unless a future applicable specification explicitly defines such inheritance.
6. Treat multiple widget slots attached to one value owner as one signature-value constraint set; do not duplicate evaluations for every appearance.
7. Where separate matching value owners have distinct seeds, require that the chosen output satisfy all applicable constraints. Conflicting mandatory seeds mean refusal.

A field dictionary merged with its sole widget annotation is still a field dictionary. Read `/SV` there. Conversely, `/SV` placed only on a separate widget annotation does not become the terminal field's seed dictionary merely because the widget refers to that field.

One missing fact: the packet does not establish whether each shared value owner is represented by the same underlying indirect PDF object or by multiple distinct field objects that the engine groups logically. Tests must distinguish those cases without changing the public PDF API.

### B2. Existing field used for `/DocTimeStamp`

PDF 2.0 §12.8.5 defines document timestamps separately from ordinary CMS approval signatures. The PDF 2.0 errata clarify that a document timestamp dictionary is the value of a signature field, and that `/ETSI.RFC3161` is among the defined signature subfilters.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://pdf-issues.pdfa.org\&sz=32)

Errata for PDF specifications



That establishes the relevance of the selected field and the generated timestamp dictionary, but does not establish that every seed rule intended for a certificate-based approval signature has identical semantics for a document timestamp.

Recommended behavior:

| Constraint category               | Document timestamp treatment                                                                                                                                                               |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Required `/Filter`                | Compare to the actual `/Adobe.PPKLite` emitted.                                                                                                                                            |
| Required `/SubFilter`             | Compare against `/ETSI.RFC3161` using the supported-encoding rule for timestamp operations.                                                                                                |
| Required `/V`                     | Apply actual parser capability.                                                                                                                                                            |
| Required certificate restrictions | Do not pretend a TSA certificate is the signer certificate of a CMS approval signature.                                                                                                    |
| Required `/Reasons`               | Do not silently introduce an approval-signature reason into the timestamp dictionary. Evaluate any defined omission requirement; otherwise treat incompatible requirements as unsatisfied. |
| Required `/MDP` certification     | Do not claim the document timestamp is an approval/certification signature.                                                                                                                |
| Required `/TimeStamp /Ff 1`       | Do not assume the standalone document timestamp satisfies a CMS signature's required timestamp.                                                                                            |
| Required digest restrictions      | Do not automatically transfer certificate-signing digest semantics to RFC 3161 timestamp-token generation.                                                                                 |

ETSI EN 319 142-1 distinguishes the signature-time-stamp attribute from the document-time-stamp mechanism, including in its B-T requirements. They can contribute to time evidence in different ways without becoming the same operation.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://www.etsi.org\&sz=32)

ETSI



For W-151, I recommend enforcing the clear dictionary-level constraints and refusing incompatible mandatory CMS-signature requirements rather than claiming compliance through the timestamp path.

Unresolved standards-policy question: whether a document timestamp may reuse every seed-bearing signature field using this applicability split, or whether field reuse with mandatory signer-specific seeds should be refused categorically. The narrowest defensible approach is not to extend CMS-specific requirements to document timestamps without an explicit supporting rule.

## C. Recommended failure reporting

### C1. Keep pre-write failures distinct from destination failures

The existing engine boundary provides two fundamentally different failure classes.

| Failure point                                | Destination guarantee                | Rollback needed?                    |
| -------------------------------------------- | ------------------------------------ | ----------------------------------- |
| Seed validation                              | No destination bytes written         | No                                  |
| Provider failure                             | No destination bytes written         | No                                  |
| TSA failure during appendix preparation      | No destination bytes written         | No                                  |
| Reserved-content overflow                    | No destination bytes written         | No                                  |
| Appendix-size validation                     | No destination bytes written         | No                                  |
| Cross-stream copy fails after writing        | Partial output possible              | Yes                                 |
| Same-stream append fails after writing       | Partial output possible              | Yes                                 |
| Destination write cancellation               | Partial output possible              | Yes                                 |
| Finalization fails after destination writing | Partial output possible              | Yes                                 |
| `SetLength` succeeds, `Seek` fails           | Length restored; position uncertain  | Report position-restoration failure |
| `SetLength` fails                            | Physical length cannot be guaranteed | Report length-restoration failure   |

This is an application-level transaction policy, not a guarantee supplied by ISO 32000 or ETSI.

Recommendation: record whether destination modification was attempted, and distinguish that from whether destination restoration succeeded.

Do not infer restoration success from restored engine edits, a successful provider result, or a successful in-memory appendix build.

### C2. Exception designs

There are three reasonable designs on both net48 and net10.0.

| Design                                                                            | Advantages                                                                                  | Disadvantages                                                                           |
| --------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Rethrow original exception with structured `Exception.Data`                       | Preserves original type, object identity, stack, cancellation behavior and existing catches | Consumers must inspect documented data; default exception logging may not display it    |
| Dedicated `PdfSigningRollbackException` carrying original and rollback exceptions | Strongly typed, discoverable, explicit combined failure                                     | Changes top-level exception type, exception catches and cancellation classification     |
| `AggregateException(original, rollback)`                                          | Built into both target frameworks; exposes both errors                                      | Weak domain semantics; changes top-level exception type and original exception identity |

### Preferred narrow solution

For W-151, preserve the original exception and attach structured, signing-specific rollback information.

For example, conceptually:

```
catch (Exception original)
{
    var rollback = TryRestoreSigningDestination(
        destination,
        originalLength);

    if (!rollback.FullyRestored)
    {
        original.Data[PdfSigningRollbackFailureDataKey] =
            rollback;
    }

    throw;
}
```

The pseudocode illustrates the desired contract, not a proposed patch.

The attached value should have a stable, documented shape, such as:

```
public sealed class PdfSigningRollbackFailureInfo
{
    public long OriginalLength { get; }

    public bool TruncationAttempted { get; }
    public bool TruncationSucceeded { get; }

    public bool PositionRestoreAttempted { get; }
    public bool PositionRestoreSucceeded { get; }

    public Exception RestorationException { get; }
}
```

This does not require exposing PDF internals.

Use one public data key rather than several unrelated string keys. A consumer should be able to distinguish:

- Original signing failure.
- Failure to restore length.
- Failure to restore position after successful truncation.

If both restoration operations are attempted independently and both fail, retain both restoration exceptions or a structured collection. Do not silently discard the second failure.

Important limitation: attaching to `Exception.Data` exposes information programmatically but does not guarantee inclusion in ordinary logs. If owner requirements demand visibility in generic exception logging and catch behavior, the dedicated-exception design is stronger.

### C3. Dedicated exception alternative

If explicit top-level discoverability matters more than retaining existing catch behavior, a dedicated exception is reasonable:

```
public sealed class PdfSigningRollbackException : IOException
{
    public Exception OriginalException { get; }
    public Exception RollbackException { get; }

    public PdfSigningRollbackStage Stage { get; }

    // InnerException is the original signing failure.
}
```

I would require that design to:

- Preserve the exact original exception object as `InnerException` or `OriginalException`, not create a textual substitute.
- Retain the rollback exception separately.
- Include both failures in diagnostic formatting without exposing sanitized signer-host details.
- Clearly document that consumers must inspect the original exception to determine whether the first failure was cancellation, I/O, or another cause.

I would not recommend a bare `AggregateException` as the public signing contract.

The decision is not merely aesthetic. Consider:

```
catch (OperationCanceledException)
{
    // Existing consumer cancellation path
}
```

A dedicated outer `PdfSigningRollbackException` prevents that catch from matching. The operation is then generally classified as faulted rather than canceled.

Preserving the original exception plus attached restoration diagnostics retains cancellation classification, even when the destination may need repair. That is both an advantage and a diagnostic tradeoff.

### C4. Cancellation, integrity, and retry safety

### Restoration outcome model

Length and position restored

Rollback complete under the stream contract. Original failure remains primary.

Length restored, position failed

Length is restored; position cannot be assumed safe for subsequent operations.

Truncation failed

Partial output may remain. Physical destination integrity is uncertain.

Failure before writing

The engine has not modified destination bytes. Rollback is unnecessary.

Several details follow.

A successful `SetLength` is not the same as successful rollback. A subsequent `Seek` failure means that physical length was restored, but the stream position may not be. Record those outcomes separately. For an ordinary conforming append-only stream, successful truncation eliminates the appended tail, but cannot establish restoration of position or guarantee arbitrary custom-stream behavior.

A failed `SetLength` does not establish the resulting length. The method may throw without changing anything, after partially changing the stream, or because truncation is unsupported. Do not infer that the stream necessarily remains extended by exactly the number of bytes written.

A partially failed destination is not automatically retry-safe. The engine may retain pending edits, but it must not treat the physical destination as an established original-length base. The caller should repair or replace the destination, or establish its integrity independently, before retrying.

For source integrity, distinguish the paths:

| Operation                           | Consequence of failed restoration                                                                                                                        |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Cross-stream signing                | The separate source should remain unmodified by the engine; the destination may contain partial copied/signature bytes.                                  |
| Same-stream signing                 | The source is itself the destination; it may remain extended or its position may be unknown.                                                             |
| Workflow staged signing, final copy | Private staged result may remain valid while the caller-owned destination is partial. The destination failure must not be represented as signer failure. |

On cancellation, avoid using an already-canceled token to suppress the rollback attempt. Perform the bounded best-effort restoration and preserve both the cancellation and any restoration failure.

Do not assume a `CanSeek`/`CanWrite`/`CanRead` property proves that the corresponding operation will succeed. Injecting failures from individual stream methods is essential.

### C5. Finalization and shared helper

Signing should use a distinct restoration-reporting path.

The existing helper:

```
static void Truncate(Stream destination, long originalLength)
```

silently swallowing all restoration exceptions is inappropriate for the new signing contract.

I recommend leaving its behavior unchanged for ordinary incremental saves unless their owner-approved specification independently authorizes a change.

Signing should instead use a helper that returns a restoration outcome, including stage and exception details.

The signing path must cover both the write catch and finalization catch. In particular:

- Capture the correct destination starting length before destination modification.
- Do not needlessly truncate when the destination has not been modified.
- If reading destination `Length` during finalization fails after writing, use a previously established rollback length where available.
- If disposing an owned source fails after destination writing, treat the source-disposal exception as the original failure and report any destination-restoration failure separately.
- Avoid replacing the original exception with an exception thrown by reporting, cleanup, or restoration-status construction.

On net48 and net10.0, `throw;` retains the original stack when still inside its catch. If propagation must cross another method or catch scope, `ExceptionDispatchInfo` can preserve the original exception object and stack information.

### C6. Host exception sanitization

Restoration reporting should not undo the existing sanitization boundary.

Specifically, do not expose raw external signer-host exceptions through `OriginalException`, `Exception.Data`, inner exceptions, diagnostic serialization, or newly added messages when the built-in provider has already sanitized them.

The original exception to preserve is the exception visible at the engine's existing public signing boundary.

One stated limitation remains: arbitrary custom providers can throw arbitrary exceptions. The packet does not establish a general sanitization guarantee for those providers. W-151 should not silently introduce one or weaken the existing built-in provider boundary.

## D. Exact recommended contract wording

### D1. Charter replacement

I recommend replacing the disputed sentence with the following wording, subject to owner approval.

**Failed signing and document-timestamp output integrity**

A signing or document-timestamp operation that fails before modifying the destination leaves the destination's bytes unchanged by the engine. This includes failures during seed-value validation, signer or timestamp-authority invocation, and signature-size validation.

If destination copying, appending, or finalization fails after destination writing has begun, the engine attempts to restore the destination to its starting length and position. Restoration is best-effort and is not guaranteed for a destination stream that fails or does not support the required operations.

The original operation failure remains available to the caller. Any failure to restore the destination's length or position is also observable, with those outcomes distinguished. A destination whose length could not be restored may retain partial output. Successful restoration of length does not by itself establish successful restoration of position.

Restoration of the engine's pending in-memory edits does not establish restoration of the physical destination. A destination whose restoration was unsuccessful shall not be assumed safe for retry until its integrity has been independently established.

Existing requirements preventing disclosure of external signer-host exception details remain in force.

This wording deliberately avoids the ambiguous phrase never leaves half-written output.

It also distinguishes length from position and explicitly ties retry safety to physical destination integrity.

### D2. Is this a clarification or a guarantee change?

Both, depending on which existing authority is being considered.

Against the adopted phase-1 specification §2, the proposed wording largely clarifies an already documented destination-stream exception. That specification expressly admits that destination restoration cannot be guaranteed.

Against the charter's literal sentence—failed signing never extends output—it is an actual relaxation of an unconditional guarantee.

Against ACCEPTANCE.md W-18, it also changes the stated observable outcome for failures where the destination refuses restoration. W-18 presently demands starting length for partial-copy, append, and finalization failures without stating this limitation.

These cannot all be true for an arbitrary caller-owned stream that rejects `SetLength`.

The contradiction is substantive. It is not safely solved by calling the replacement editorial clarification.

The owner must approve the charter disposition, and affected work must remain subject to the project's rule for conflicting protected requirements.

### D3. Separately required specification and acceptance clarifications

| Source                    | Required clarification                                                                                                                                                                            |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AGENTS.md                 | Replace absolute failed-signing guarantee with a phase-aware, best-effort destination restoration guarantee.                                                                                      |
| Phase-1 specification §2  | Explicitly distinguish failures before writing from failures during writing/finalization. Document failed `SetLength` and failed `Seek` separately.                                               |
| W19                       | Clarify that signer/TSA/size/validation failures covered by unchanged-output requirements occur before the destination-write boundary; do not generalize this to subsequent destination failures. |
| ACCEPTANCE.md W-18        | Make starting-length assertion conditional on successful truncation. Require original and restoration failures to be observable together when restoration fails.                                  |
| ACCEPTANCE.md W-18        | Clarify whether pending edits retained for retry means logical state only, not automatic safety of a damaged same-stream destination.                                                             |
| Workflow F20              | Confirm that privately staged signing does not guarantee successful publication to a caller-owned destination, and identify which layer reports final-copy rollback failures.                     |
| Signing API documentation | Define the combined failure-reporting shape, cancellation behavior, rollback status and consumer retry obligations.                                                                               |
| W-151 tests               | Define independent expectations for truncation failure, position-restoration failure and finalization failure on both frameworks.                                                                 |

The tests must not be adopted as the de facto contract before these conflicts are resolved.

## E. Unresolved owner choices

The following are choices or missing implementation facts, not matters this review can approve.

| Issue                                                                   | Recommended disposition                                                                                                                                    |
| ----------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Absolute charter guarantee versus phase-1 destination failure exception | Approve the revised limited guarantee before affected implementation work.                                                                                 |
| Public failure shape                                                    | Prefer original exception identity with documented structured restoration information; explicitly approve if a dedicated top-level exception is preferred. |
| Cancellation with failed restoration                                    | Preserve cancellation classification with the preferred original-exception design, but always expose destination uncertainty.                              |
| PDF 2.0 `/V 3`                                                          | Establish the parser's actual recognition coverage before claiming support.                                                                                |
| Custom provider `/DigestMethod`                                         | Establish whether request-level enforcement is sufficient for W-151 or whether mandatory constraints about actual provider output must be refused.         |
| Certificate signature-policy entries without `/Ff` bits                 | Decide applicability and mandatory interpretation explicitly, rather than fabricating bits.                                                                |
| `/DocTimeStamp` reuse                                                   | Approve the applicability split or impose a documented conservative refusal rule for mandatory signer-specific seeds.                                      |
| `/LockDocument /auto`                                                   | Document the existing noninteractive behavior and whether the application can establish a conforming outcome.                                              |
| Shared field owners                                                     | Confirm exact object/value ownership and how conflicts are currently represented.                                                                          |
| Unsupported or malformed PDF inputs                                     | Confirm that the proposed narrow seed refusal policy does not conflict with existing generic parsing rules.                                                |
| Finalization                                                            | Confirm the exact order of writing, length acquisition, source disposal, destination switching and engine-edit restoration.                                |
| Retry after failed restoration                                          | Explicitly prohibit assumptions of physical safety unless the destination has been repaired or independently checked.                                      |

The existence of a PAdES B-T-capable built-in provider does not, by itself, close the `/TimeStamp` preflight gap: the interface cannot establish that the particular provider invocation has an embedded signature timestamp configured.

Likewise, the presence of a configured hash algorithm does not establish the exact cryptographic behavior of an arbitrary custom provider.

## F. Minimal permanent regressions

I recommend a compact set of parameterized test families rather than an independent test for every entry and combination. Run all applicable managed regressions on net10.0 and net48.

### F1. Seed-value enforcement tests

| ID    | Fixture / scenario                                                                                                                              | Required assertion                                                                                                         |
| ----- | ----------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| SV-01 | Required `/Filter /Adobe.PPKLite`; matching required `/SubFilter`; selected supported SHA-256 and reason                                        | Sign succeeds; field page/rectangle, selected owner and expected signature dictionary are preserved.                       |
| SV-02 | Required `/Filter` mismatch or required `/SubFilter` absent from supported choices                                                              | Refuse before provider call; destination unchanged.                                                                        |
| SV-03 | Ordered `/SubFilter` array with two supported formats; caller requests the second                                                               | Refuse rather than silently switch. Include a variant where earlier values are unsupported.                                |
| SV-04 | Mandatory `/Reasons` with permitted value, nonpermitted value, omitted reason, `[]`, singleton `[(.)]`, and absent `/Reasons`                   | Distinguish all required behavior.                                                                                         |
| SV-05 | Required `/DigestMethod` matching/mismatching requested digest; valid `/V` and unsupported required `/V`; applicable digest-mechanism exception | Exercise corrected ISO 32000-2 behavior and supported parser capability.                                                   |
| SV-06 | Unsupported mandatory `/Cert`, `/TimeStamp /Ff 1`, `/MDP /P 1`, `/LegalAttestation`, `/AddRevInfo true`, locking and appearance requirements    | Refuse without calling arbitrary provider. Confirm correct controlling flags for each case.                                |
| SV-07 | Optional unsupported values; optional malformed entries; private irrelevant entries; unknown flag bits                                          | Do not introduce a seed refusal. Successful paths remain available.                                                        |
| SV-08 | Indirect `/SV` with separate field/widget; merged field/widget; repeated widgets; two distinct matching value owners with conflicting seeds     | Check each relevant owner once, no inherited or widget-only phantom constraints, and refuse incompatible mandatory owners. |
| SV-09 | Existing field reused for `/DocTimeStamp` with required matching `/Filter`/`/SubFilter`; conflicting mandatory CMS-signature requirements       | Enforce the owner-approved timestamp applicability rule.                                                                   |

There should also be explicit cases for the certificate `/URLType /Browser` exemption, required `/OID` with missing issuer context, and required `/LockDocument /false` when locking behavior is known.

SV-06 and SV-07 should be table-driven. That keeps the suite relatively small without overlooking the distinction between present, mandatory, and applicable.

For every preflight refusal, assert:

```
Provider invocation count == 0
Destination bytes == original bytes
Destination length == starting length
```

Where applicable, verify that logical pending engine edits remain available and that a valid later attempt can still proceed.

### F2. Destination and rollback tests

| ID    | Injected scenario                                                                                                                  | Assertions                                                                                                                  |
| ----- | ---------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| RB-01 | Cross-stream copy fails after partially writing original bytes; `SetLength` and `Seek` succeed                                     | Original exception retained; destination returned to starting length; source unchanged.                                     |
| RB-02 | Same-stream appendix write fails after partial append; rollback succeeds                                                           | Original content and length restored; destination position restored; pending edits retained as specified.                   |
| RB-03 | Partial write followed by `SetLength` throwing                                                                                     | Original exception retained; truncation failure observable; do not assert destination length restoration.                   |
| RB-04 | `SetLength` succeeds, restoring length, then `Seek` throws                                                                         | Original exception retained; length restoration recorded as successful; position-restoration failure separately observable. |
| RB-05 | Destination copy/write succeeds, then finalization fails (destination `Length` access or owned-source disposal); rollback succeeds | Finalization failure remains primary; destination restored.                                                                 |
| RB-06 | Finalization failure followed by truncation failure                                                                                | Both failures observable; physical destination possibly extended.                                                           |
| RB-07 | Cancellation after partial destination write, with successful and unsuccessful rollback variants                                   | Preserve approved cancellation classification and expose restoration result; no canceled token suppressing restoration.     |

Use deterministic fault-injecting streams that can throw on the Nth write, after writing only a selected prefix, on `SetLength`, on `Seek`, on `Length`, and on source `Dispose`.

These should exercise both a destination starting at zero and same-stream signing starting at the source's original length.

### F3. Exception contract assertions

Under the preferred `Exception.Data` design, test that:

```
ReferenceEquals(caught, original) == true
```

and that:

```
caught.GetType() == original.GetType()
```

The test must also inspect the documented restoration-information value and verify that it retains the actual rollback exception.

For a dedicated wrapper design instead, assert that `OriginalException`/`InnerException` is the original object, the rollback exception is independently accessible, the correct failure stage is reported, and cancellation behavior matches the owner-approved contract.

Use a built-in provider whose host dependency throws a distinctive secret-bearing exception. Verify that newly added combined-failure reporting does not reveal those original host details.

### F4. Independent producer and verifier

Because the change is a preflight/rollback hardening item rather than a new signature format, use an independent producer to create selected seed-bearing PDFs and an independent verifier to check successful outputs.

Suitable options include:

- Independent fixture producer: Adobe Acrobat, iText, or pyHanko, rather than the engine itself.
- Independent signature verifier: a separate PDF signature implementation such as pyHanko or a suitable PDFBox-based verification harness.
- Deterministic byte regression: use a controlled fake provider where possible to compare successful appendix contents and ByteRange calculations before and after the change.

The independent verification should establish structural signature validity and expected CMS/PAdES or timestamp encoding; it need not introduce trust-chain validation, revocation validation, or LTV features into the product.

For successful PAdES B-B/B-T outputs, ETSI EN 319 142-1 §6.3 requires the correct `/SubFilter`, byte-range coverage and appropriate CMS encoding. For document timestamps, its timestamp-specific requirements apply separately.&#x20;

A static fixture can also assert that successful output bytes remain unchanged on a deterministic signing path. Do not require naturally nondeterministic signatures with signing times or TSA tokens to be byte-for-byte identical unless their inputs are fixed.

## G. Final recommendation

W-151 should be implemented as a narrow enforcement and failure-observability change, not as an expansion of the signing engine.

The core implementation rules should be:

1. Resolve and inspect the applicable existing signature-field owners, their indirect `/SV` dictionaries, and their mandatory constraints before provider invocation or destination modification.
2. Compare the required signature properties with the caller's selected output; refuse conflicts rather than changing selections implicitly.
3. Reject unverifiable applicable mandatory obligations while ignoring optional, irrelevant and handler-private recommendations.
4. Keep the existing bounded-buffer, incremental-write architecture and pre-write unchanged-output guarantee.
5. Use signing-specific restoration reporting that distinguishes failed truncation from failed position restoration and preserves the original failure.
6. Preserve the shared ordinary-save helper's existing behavior unless separately authorized.
7. Obtain owner approval for the charter/acceptance conflict and the identified ambiguous policy decisions before finalizing the affected behavior.

Standards-backed conclusion: ISO 32000-1/2 require honoring applicable mandatory seed-value constraints, including independently defined nested requirements. ETSI does not authorize violating a PAdES profile to satisfy conflicting seeds; EN 319 142-2 §4.2.6 explicitly prohibits seed values that would require such violations.&#x20;

[image](https://www.google.com/s2/favicons?domain=https://developer.adobe.com\&sz=32)

Adobe Developer

+2



Engineering conclusion: preserving the original failure while reporting restoration failure is both achievable and appropriate on net48 and net10.0. The most compatible solution retains the original exception identity with a documented structured rollback outcome.

Governance conclusion: the proposed contract change is justified by the already recognized destination-failure exception, but it changes the literal charter guarantee and therefore requires owner approval. No expert recommendation alone authorizes the edit.

The remaining uncertainties are bounded: PDF 2.0 policy-entry mandatory semantics, timestamp-specific seed applicability, custom-provider digest guarantees, actual `/V` parser coverage, and the owner's choice of public combined-failure shape. None justifies adding new algorithms, signature validation, provider introspection, or whole-file staging under W-151.
