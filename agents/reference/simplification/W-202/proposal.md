# W-202: decision wording and small implementation sequence

Proposal only, 2026-10-26. Current recommendation: retain D-64/F18 and all shipped provider APIs. No protected file is edited. If Jake instead selects narrowing, approve the exact text below together with the implementation scopes. Independent reconciliation and external-consumer impact remain explicit gaps in `findings.md`.

## Candidate N: exact adoption text

New decision, superseding D-64 point 2 only (leave its completed historical record intact):

> Public engine and workflow signing accept IPdfExternalSigner and optional ITimestampAuthority. Public document-timestamp operations accept ITimestampAuthority. The engine builds every CMS signature and document-timestamp Contents value. IPdfSignatureProvider, PdfSignatureContext, CmsDetachedSignatureProvider and DocTimeStampSignatureProvider become internal implementation details. Public complete-CMS callbacks, caller-defined CMS attributes and complete-CMS service integrations are removed. A service offering only complete CMS has no general replacement through IPdfExternalSigner. This loss is accepted; changing vendor, protocol or product path may be necessary. There is no durable external prepare/complete signing API. Existing profiles, algorithms, defaults, ordinary additional PDF approval signatures, signature timestamps, document timestamps, timestamp-request matching and certificate-inclusion checks, reservation limits, private staging, one-editor execution, permission/seed/lock checks and rollback diagnostics remain unchanged. No new algorithms, CMS custom-attribute extension, trust validation or automatic retry is authorized.

Phase 1 F18: replace its entire requirement text with the following (status remains Exists only when implementation ships):

> **Signing terminals.** `Sign` and `SignAsync` execute the described ordinary edits and one CMS signature. They accept an `IPdfExternalSigner`, an optional `ITimestampAuthority`, and `PdfSignatureOptions`. `Timestamp` and `TimestampAsync` execute the described ordinary edits and one document timestamp through `ITimestampAuthority`. Each terminal offers separate stream and file-path outputs; async forms take a `CancellationToken`. Public complete-CMS providers are not supported. The engine constructs all signature Contents values; existing options, algorithms and defaults remain unchanged. A CMS signature may include an RFC 3161 signature timestamp. A run adds exactly one CMS signature or one document timestamp. Adding multiple signatures, adding a signature followed by a separate document timestamp, and signing-to-source conveniences are not supported. Existing signatures in the input remain subject to the engine's preservation and permission checks. F1 to F3 and F5 to F17 continue to apply where relevant.

Append to reset-plan section 10 Scope (do not rewrite historical R1 migration requirements):

> After the owner adopts the W-202 narrowing decision, replace the public complete-CMS provider boundary with engine-owned CMS and document-timestamp entry points. Preserve direct-engine same-source and cross-stream variants, workflow private staging through one editor, and all existing signing guarantees. The original R4 qualification remains historical; verification of the replacement API is attached to its implementation checkpoint.

In `docs/behavior.md`, apply these exact textual replacements when the change ships:

1. In Engine guarantees / Async signing and saving, replace `` `IPdfExternalSigner.SignHashAsync`, `ITimestampAuthority.GetTimeStampTokenAsync`, and `IPdfSignatureProvider.CreateContentsAsync` `` with `` `IPdfExternalSigner.SignHashAsync` and `ITimestampAuthority.GetTimeStampTokenAsync` ``. Replace `waits for the provider` with `waits for the host signer or timestamp authority` in that paragraph.
2. In PKCS#11 stays in the host, replace `A custom `IPdfSignatureProvider` must honor `PdfSignatureContext.Format`, because the `/SubFilter` already names it.` with `The engine constructs CMS using the requested format. Public complete-CMS providers and caller-defined CMS attributes are not supported. A service that only returns complete CMS cannot generally implement the raw-digest signer contract.`
3. In Timestamps, replace `` `CmsDetachedSignatureProvider` can take an `ITimestampAuthority`. `` with `A CMS signing operation can take an `ITimestampAuthority`.` Replace `A document timestamp is still a second revision from `DocTimeStampSignatureProvider`, and that provider applies the same checks.` with `A document timestamp uses a separate timestamp operation and revision, and the engine applies the same checks.` Remaining request/token checks are retained verbatim.
4. In the engine example, replace `new CmsDetachedSignatureProvider(pkcs11Signer)` with `pkcs11Signer`, using the engine overload below. In Workflows, replace `, or an existing `IPdfSignatureProvider`` with the empty string.
5. Add: `Public engine Sign/SignAsync operations accept options and a raw-digest signer, plus an optional timestamp authority. Timestamp/TimestampAsync operations accept an authority and optional options. Both offer same-source and explicit destination-stream variants, with existing ownership, cancellation and restoration guarantees. Complete-CMS integrations require another product path unless their service also supplies the supported raw-signing operation.`

`contract-diff.patch`, generated by `python agents/reference/simplification/W-202/draft_contracts.py`, supplies the exact proposed ACCEPTANCE and behavior hunks, including provider-name replacements and the affected kind/seed assertions. It takes precedence over the explanatory substitution list above. No numerical limit or supported seed-policy decision is weakened. Its central replacement bullet is:

> Public signing uses a raw-digest `IPdfExternalSigner` and optional `ITimestampAuthority`; public document timestamps use `ITimestampAuthority`. Public complete-CMS providers, caller-defined CMS attributes and the signed-byte context are unavailable. Direct-engine same-source and cross-stream sync/async variants remain, and the workflow retains its stream/file variants. The engine constructs the requested format and digest over the signed PDF byte sequence, with SHA-256 messageImprint for document timestamps. Seed tests using internal opaque contents establish preflight and engine inputs, not cryptographic validity. The retained profiles, algorithms, additional PDF approval signatures, timestamp checks, reservation bounds, prefix preservation, permission/seed/lock enforcement, one-editor private staging, cancellation and rollback diagnostics retain their existing assertions.

All of these are target assertions conditional on Candidate N adoption and implementation; the historical interop claims would need the migration checkpoint before being cited for its new public APIs. This is a reviewable wording proposal, not approval to apply it.

README and XML docs must remove claims of a public provider and cross-references to its public callback; linked examples use raw signers. No charter change is proposed: the existing host raw-digest and engine CMS ownership rules already hold. No QUALIFICATION or skill change is necessary for this research; if adoption requires a restatement correction, supply its literal diff for approval under the repository rules.

## Replacement engine API shape (candidate, unavailable today)

Namespace stays `jaytwo.Pdf`; no whole-PDF byte-array API or workflow parsing/signing code is introduced. Preserve argument order for existing options-first same-source operations:

```csharp
void Sign(PdfSignatureOptions options, IPdfExternalSigner signer, ITimestampAuthority? timestampAuthority = null);
void Sign(Stream destination, PdfSignatureOptions options, IPdfExternalSigner signer, ITimestampAuthority? timestampAuthority = null, bool leaveOpen = false);
Task SignAsync(PdfSignatureOptions options, IPdfExternalSigner signer, ITimestampAuthority? timestampAuthority = null, CancellationToken cancellationToken = default);
Task SignAsync(Stream destination, PdfSignatureOptions options, IPdfExternalSigner signer, ITimestampAuthority? timestampAuthority = null, bool leaveOpen = false, CancellationToken cancellationToken = default);
void Timestamp(ITimestampAuthority authority, PdfSignatureOptions? options = null);
void Timestamp(Stream destination, ITimestampAuthority authority, PdfSignatureOptions? options = null, bool leaveOpen = false);
Task TimestampAsync(ITimestampAuthority authority, PdfSignatureOptions? options = null, CancellationToken cancellationToken = default);
Task TimestampAsync(Stream destination, ITimestampAuthority authority, PdfSignatureOptions? options = null, bool leaveOpen = false, CancellationToken cancellationToken = default);
```

Timestamp normalizes Kind as the existing workflow does; nondefault forbidden digest/format still refuses. Do not remove options.Kind or change the seed policy. Existing workflow raw signer and authority signatures stay. Examples:

```csharp
// Engine today:
await editor.SignAsync(destination, options,
    new CmsDetachedSignatureProvider(signer, authority), leaveOpen: true, cancellationToken: token);
// Candidate after adoption:
await editor.SignAsync(destination, options, signer, authority, leaveOpen: true, cancellationToken: token);
// Workflow already available today:
await PdfWorkflow.From(source).SignAsync(destination, signer, options, authority, token);
```

Complete-CMS-only service: no equivalent example exists. `CompleteCms` in the probe is intentionally a demonstrated lost integration, not an adapter recipe. Raw signer adapters need certificate/chain retrieval and the correct hash/algorithm/signature-encoding operation; they cannot simply forward the PDF hash or return CMS bytes. Credential provisioning and service approval belong to the host.

## Proposed follow-ups, no IDs or queue insertion yet

1. **Add engine raw signer and timestamp entry points** (only if additive convenience or Candidate N is approved). Scope: editor, internal engine forwarding/provider construction, XML docs, focused engine tests and examples; keep current APIs during this commit. Acceptance: all eight shapes work, preserve source/destination ownership and refused-call state, and route to unchanged builders/checks. Validate focused net10 signing/timestamp/async/seed/rollback tests; build affected targets. This is behavior/API work, not a behavior-preserving structural refactor.
2. **Migrate repository consumers and hide the public provider surface** (depends on 1, exact Candidate N wording and owner acceptance of compatibility losses). Scope: the 45-file inventory's actual consumers, provider/context accessibility, workflow provider overloads, relevant contracts and examples. Keep engine-only internal test seams, migrate workflow tests through public raw signer/authority APIs. Acceptance: no public custom-Contents entry point; all migration examples compile; existing reservation, refusal, prefix, countersigning, cancellation, staging, rollback and output-identity assertions remain. Do not delete the 17 doubles' assertions to achieve a green run. Produce an explicit public API before/after diff. The ticket's allowed paths must be enumerated from the inventory plus each approved contract/record path before activation.
3. **Verify the signing API migration checkpoint**, if Candidate N proceeds. Separate worktree; retain algorithm/profile tests, direct-engine/workflow signing, signature timestamp vs document timestamp, encrypted/certified input checks, named PDF-aware verifier/profile properties, appropriate framework matrix and Docker gate. This is an integration checkpoint, not an automatic release or unbounded new fuzz campaign. Link existing R4 qualification as baseline; refreshed evidence describes only checks actually rerun.

Gate 2 must select retain, additive convenience, or Candidate N; approve exact outstanding wording and allocated tickets; and specify queue insertion. W-203 retains its own seed decision and W-210 its own reader/forensic decision. No implementation may begin from this document alone. W-211 consumes these findings and reconciles the remaining review gaps.
