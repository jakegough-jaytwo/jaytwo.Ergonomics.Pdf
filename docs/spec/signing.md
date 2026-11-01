# Signature construction and timestamps

**Adopted subject contract, revision W-218-SIGNING-2026-10-31, approved by the owner on 2026-10-31 after review corrections.** Authority: [charter](../charter.md) > [supported capabilities](../supported.md) > adopted contracts. Apply [shared rules](shared.md). Current [behavior](../behavior.md#engine-signing-output-failures) is compatibility evidence; adoption claims no implementation or executed result.

## Inputs and result: SIGN-CONSTRUCT-01

The caller chooses detached CMS (`adbe.pkcs7.detached`, default), PAdES B-B (`ETSI.CAdES.detached`), optionally with an RFC 3161 signature timestamp for B-T, or a separate document timestamp (`ETSI.RFC3161`). One call appends one signature revision; an embedded signature timestamp adds none. Original input bytes remain an identical prefix. Private keys remain with the host. Trust assessment, signature validation and B-LT/B-LTA construction are excluded; a document timestamp alone establishes no B-LTA conformance.

CMS options supply field name, new-widget page/rectangle, digest, reservation, reason, location, contact, signer name and claimed signing time. SHA-256 is default; SHA-384 and SHA-512 are accepted. Claimed time defaults to current UTC, not trusted time. A document timestamp uses SHA-256, no Reason or claimed M; incompatible format/digest options refuse rather than being substituted.

## Signer and provider: SIGN-PROVIDER-01

* `IPdfExternalSigner` supplies a nonempty DER leaf and optional DER chain certificates. Snapshot the leaf once and enumerate additional certificates once before signing. Embed them without trust, chain or revocation validation. Supported keys are RSA PKCS#1 v1.5 and ECDSA P-256/P-384, each with SHA-256/384/512. Other keys/digests refuse before signer invocation.
* The built-in CMS constructor hashes the final two PDF ByteRange segments, excluding only the serialized Contents gap, and puts that digest in the CMS message-digest attribute. The host receives the selected digest of the DER SET encoding of signed attributes and signs it without hashing again. RSA returns its PKCS#1 v1.5 signature; ECDSA returns IEEE P1363 `r||s` (64/96 bytes), converted to DER by the library. Empty results and incorrectly sized ECDSA encodings refuse before destination writing; RSA value correctness remains the host obligation.
* **Key consistency remains the host's obligation.** Construction does not verify the returned signature against the leaf. A well-encoded wrong-key signature may be emitted: success means construction under the host obligation, never verified cryptographic correctness. Acceptance independently verifies correct-key output; no runtime validation API is added.
* `IPdfSignatureProvider` returns complete contents. Its context supplies reservation, digest/format, absolute ByteRange and a disposable read-only concatenation of the exact signed segments, usable during the call. The host must return the requested CMS/token for those bytes. Engine field/operation and reservation checks still apply, but arbitrary provider output is not independently checked for format, digest, coverage or certificate consistency. Built-in CMS/timestamp checks do not extend to arbitrary providers.
* Each provider, signer and authority is invoked at most once per operation. No automatic retry or reservation enlargement. Host calls can have irreversible effects even if later processing, cancellation or output fails.

## Fields and seeds: SIGN-FIELD-01

Pages are 1-based. A named existing field must be an empty signature field; retain its page and rectangle. Occupied, ambiguous or non-signature targets refuse. Otherwise create the requested name, or next unused SignatureN when absent, on the selected page. A new widget defaults to an invisible zero-area rectangle in the engine's existing unrotated PDF page coordinates. No visible signing artwork is authored. Required appearance readiness, including NeedAppearances, is checked before the host call; viewer-generated future appearances are insufficient.

Check existing-field seeds before host invocation/output. Retain the current noninteractive subset:

| Required constraint | Outcome |
| --- | --- |
| Filter, ordered SubFilter, parser capability V, Reasons, DigestMethod | Must match the operation. Check using the first supported encoding in seed order: the two detached CMS formats or RFC3161 for timestamps. Never silently switch the requested format. Compare decoded reason text exactly. |
| Reasons absent or `[(.)]`; empty Reasons array | First case requires omitting Reason; an empty array permits no reason. |
| Certificate restrictions | Refuse when the provider interface cannot establish them. Certificate flags apply independently of top-level flags. Browser enrollment ignores its URL mandatory bit; SignaturePolicyOID/dependent policy entries are advisory. No EPES construction. |
| Timestamp | A standalone document timestamp satisfies a required timestamp without URL. CMS cannot establish required embedded timestamp use through the general provider contract and refuses. A required TSA URL also refuses because its use cannot be established. |
| MDP | CMS accepts P=0 and refuses certification construction. A document timestamp refuses P=0 through 3. No certification/locking transform is added. |
| Effective PDF 2.0 LockDocument | Required true refuses; auto adds no lock; false is accepted only without a target Lock/P instruction. Preserve existing field-only locks and earlier restrictions. |
| Named appearance, legal attestation, revocation inclusion | Refuse. Required AddRevInfo=false is compatible. |

Optional/irrelevant entries alone do not cause refusal. Malformed or unrecognizable mandatory constraints refuse. Timestamp flags apply independently of top-level flags. Preserve conservative PDF 1.7 mandatory-digest mismatch refusal even for an arbitrary provider of unknown key type; no mechanism-fixed-digest exception is promised.

## Timestamp subset: SIGN-TIME-01

`ITimestampAuthority` receives a DER TimeStampReq and returns the TimeStampToken ContentInfo, not its status wrapper. Built-in providers request SHA-256, a random nonce and certReq=true: over the CMS signature value for an embedded timestamp, or final signed PDF segments for a document timestamp. Require a structurally supported token, matching imprint, algorithm and nonce, and the embedded TSA signing certificate identified by its ESS signing-certificate attribute. Missing, malformed, mismatched and oversized tokens refuse before output. These are correlation/inclusion checks, not TSA signature, certificate trust, validity-period or revocation validation.

Transport belongs to the host authority, or `HttpTimestampAuthority` using the caller's HttpClient/configured timeout. That helper limits actual response body bytes, including wrapper, to 2 MiB, checks status and returns the token. Reservation separately limits token size. Host authorities own their transport limits; no universal network retry/deadline is implied.

## Authorization and preservation: SIGN-AUTH-01

Apply [security](security.md): password permission, DocMDP, field locks and Reader rights are independent gates. P=1 certification blocks signing; P=2/3 permit otherwise eligible approval signing. Never overwrite a signed field, bypass a locked target or add certification. Preserve earlier signature bytes without claiming validity or that validators permit every later change. Q-33 preserves granting rights and refuses signed removal-dependent operations; unsigned removal follows security's consent/authorization rules. Signing neither edits nor silently abandons XFA; unusable target/appearance refuses. Preserve unrelated content; PDF/A inputs receive normal checks without a conformance promise.

## Reservations, revisions and output: SIGN-OUTPUT-01

Contents reservation is **1 through 1,048,576 decoded bytes inclusive**, default 16,384; hex serialization takes twice the reservation plus delimiters. Built-in CMS certificate enumeration stops above 1 MiB total DER bytes. Contents/token overflow reports PdfSignatureTooLargeException with actual/reserved lengths, without another host call. The engine stages at most **8 MiB (8,388,608 bytes inclusive)** of serialized signature appendix, counting every pending object written. Larger appendices refuse before output: save pending edits incrementally first, then sign. No whole-document memory staging is introduced.

Engine signing prepares contents/appendix before writing to the opened source or an empty writable seekable destination. Pre-write failure preserves destination bytes and pre-call pending edits. Output/finalization failure restores pending edits separately, attempts truncation to original destination length then seeking to that length, and preserves the original exception. `PdfSigningRollbackFailure.Get` reports failed restoration and length/seek results. Failed truncation may leave partial bytes; failed seeking may leave uncertain position. A failed source append makes the open document unusable under [shared state rules](shared.md#shared-state-01-failure-leaves-separate-states), even after successful truncation. No automatic retry; manual retry requires usable input and destination.

Workflow signing keeps its existing stronger staging: ordinary edits save through one editor to private disk, then a separate signature revision is added, and only the completed result is copied/published. With no edits there is only a signature revision. Before final copy, failure leaves stream empty/file unchanged. Stream-copy failure attempts best-effort truncation; file output uses temporary-sibling publication. Temporary disk follows output size. Signing to the workflow input and multiple signing terminals are refused. [Workflows](workflows.md) owns publication details; ordinary direct workflow save has its own guarantees.

## Failure and cancellation: SIGN-FAIL-01

Use [shared categories](shared.md#shared-fail-01-failure-categories). Known argument, field, seed, authorization and reservation failures precede host invocation. Host details remain sanitized: external signer failures become generic CryptographicException, host authority failures generic PdfTimestampException; arbitrary complete-provider exceptions propagate without an added sanitization guarantee. Built-in host cancellation becomes a fresh OperationCanceledException when the caller token is canceled. The HTTP helper retains its documented exception behavior; destination exceptions retain original identity and restoration diagnostics.

Async cancellation checkpoints are before preparation, at provider/authority boundaries, before output and between copy blocks; pass the token to host calls. Blocked synchronous source reads and hosts ignoring cancellation need not be interruptible. Sync signing uses CancellationToken.None and waits. Observed cancellation gives no success, follows pending/output restoration above and does not undo host effects. Borrowed streams, HttpClient, signer and authority stay caller-owned; dispose signed-byte views and owned staging resources. Report cleanup failure separately from the primary cause.

## Acceptance map

All targets are **unexecuted**. Independently verify signatures and inspect prefix/revision structure; this library's inspection alone is insufficient.

| ID | Scenario |
| --- | --- |
| SIGN-A1 | RSA/both ECDSA curves and each digest: detached/B-B/B-T outputs independently verify and cover the exact ByteRange. |
| SIGN-A2 | Well-encoded wrong-key signer and wrong-digest arbitrary provider demonstrate host obligations and narrower success; empty/wrong-size ECDSA signatures and unsupported algorithms refuse. |
| SIGN-A3 | New/reused fields, occupied/ambiguous/locked targets and mandatory seed classes: pre-write refusal and host-call counts. |
| SIGN-A4 | Embedded/document timestamps: wrong imprint/algorithm/nonce/certificate, malformed/oversized token, HTTP timeout/body limit; no trust claim. |
| SIGN-A5 | Reservation and 8-MiB appendix boundaries; overflow calls host at most once; large edits use save-before-sign/workflow disk staging. |
| SIGN-A6 | Provider/cancellation, output/finalization and truncate/seek failures: pending state, output, diagnostics, source usability and irreversible effects separately. Inject a workflow final-copy write failure followed by restoration failure; require the primary cause to survive, cleanup failure to be reported separately and partial bytes to follow the best-effort restoration rule. |

## Remaining engineering gaps

Map target clauses to existing tests and independent verification; explicitly demonstrate wrong-key behavior without calling it runtime validation. Confirm ambiguous-field/malformed-seed checks, failed-append unusability integration and cleanup paths. Workflow final-copy restoration can currently mask the primary output failure when restoration throws an unexpected exception type ([W-205 findings](../../agents/reference/simplification/W-205/findings.md#observed-diagnostic-masking-problem)); this is an implementation gap, not a verified guarantee. No runtime verification ran in this adoption session.

## Adoption record

Approved by the owner on 2026-10-31 after the supplied agent review was addressed. Retains host responsibility for key/signature consistency and arbitrary provider correctness, current algorithms, seeds, reservations, staging and restoration guarantees. SIGN-A6 now explicitly exercises workflow final-copy restoration failure; its known implementation gap is recorded above. Acceptance refinements clarify verification of existing clauses; no runtime cryptographic verification, scope or qualification expansion is adopted. All target scenarios remain unexecuted. History: [signing](drafting/history.md#signing-and-timestamps).
