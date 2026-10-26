# W-151 expert review prompt

Copy everything below the separator to an expert agent. No repository access is needed. This is a proposal and question packet, not shipped behavior or an owner-approved decision.

---

Please review the following PDF signing hardening proposal. Cite primary sources (ISO 32000-1/2 clauses and tables, and relevant ETSI requirements); distinguish normative requirements from your recommendations. You have all relevant project context below. Do not assume you can inspect our repository. Flag any missing facts instead of inventing them.

## Goal and constraints

We have two managed .NET libraries: a PDF engine and a workflow layer over its public API. The current work item, W-151, must enforce mandatory signature-field `/SV` seed-value constraints and expose a failed destination rollback while preserving the original signing failure. Optional or irrelevant seed entries must not cause new refusals. Tests must cover seed values, partial writes, successful rollback, failed truncation, and original-plus-rollback failure on net10.0 and net48.

Do not add algorithms, profiles, runtime packages, signer capabilities, signature trust validation, revocation, or long-term validation. PDF internals stay internal. The engine must use bounded buffers and incremental output; it cannot stage a whole PDF. The workflow layer already stages signing privately before publishing, but copying to a caller-owned stream can still fail. An expert recommendation does not authorize a change to the owner's charter or protected specification.

## Current signing boundary

The engine reuses an existing empty signature field selected by exact name, or creates one. On reuse it rejects a non-signature, read-only, or already-populated field, keeps its page and rectangle, and assigns `/V`. Multiple matching field slots can share a value owner. It currently never reads `/SV`.

The signature dictionary always writes `/Filter /Adobe.PPKLite`. CMS signing writes `/Type /Sig` and either `/SubFilter /adbe.pkcs7.detached` (default) or `/ETSI.CAdES.detached` (PAdES baseline B-B; a provider with an embedded timestamp produces B-T). CMS supports SHA-256, SHA-384, and SHA-512, with RSA and ECDSA through the built-in provider. A separate document timestamp writes `/Type /DocTimeStamp` and `/SubFilter /ETSI.RFC3161`, using SHA-256 only. Options also expose Reason, Location, ContactInfo, SignerName, SigningTime, and reserved contents size. There is no option to request certification, certificate policy, revocation data, or a different signature handler.

`IPdfSignatureProvider` exposes only:

```csharp
PdfSignatureKind Kind { get; }
ValueTask<byte[]> CreateContentsAsync(
    PdfSignatureContext context, CancellationToken cancellationToken);
```

Context supplies the byte range, reserved size, signing time, requested digest, format, and a way to read the signed bytes. Arbitrary custom providers are supported. The engine has no provider-independent preflight access to the signing certificate, embedded signature-timestamp configuration, or certificate policy. Built-in CMS providers obtain certificate data and raw signatures from a host external signer; the host retains its private keys. Host exception details are sanitized by built-in providers.

## Current failure path

The engine builds and signs an appendix in memory, capped at 8 MiB, before writing any destination bytes. Thus provider, validation, and size failures at this stage do not modify output. A same-stream sign seeks to the source's original length and appends; a cross-stream sign copies the original into an empty destination with a 1 MiB buffer, then writes the appendix.

The write catch currently calls the following helper, then rethrows the original exception:

```csharp
static void Truncate(Stream destination, long originalLength)
{
    try
    {
        destination.SetLength(originalLength);
        destination.Seek(originalLength, SeekOrigin.Begin);
    }
    catch (Exception)
    {
    }
}
```

Finalization also uses this helper if reading destination Length or disposing an owned source during the switch to the destination fails. The helper is shared with ordinary incremental saves. The signing method restores backed-up engine edits after failure. If truncation fails, physical output may remain extended despite restoration of the pending in-memory edits. A successful SetLength followed by a failed Seek is also possible; these failures establish different facts about output length and position.

## Contract conflict requiring an owner disposition

The binding charter (AGENTS.md) says: "A failed sign or timestamp never extends the output" and host exception details must not pass through.

The adopted phase-1 specification section 2 says failure never leaves half-written output, except a destination-stream failure such as I/O error or full disk: the library attempts to restore the original length, reports failure, and documents that restoration cannot be guaranteed. Requirement W19 says signer, timestamp, size, and validation failures leave the destination unchanged. Workflow requirement F20 explicitly allows rollback to fail during final copying.

ACCEPTANCE.md's W-18 sealing bullet says partial-copy, partial-append, and finalization failures rethrow the failure and leave the destination at its starting length, with pending edits retained for retry. It does not describe failed truncation. Repository rules require us to stop affected work when sources disagree; a change to a signing guarantee needs the owner. No implementation or protected wording has changed.

## Proposed direction for review

1. Check existing-field seed requirements before provider invocation or destination modification, covering every applicable matching field owner. Compare requirements against the requested output without silently changing caller options. Refuse mandatory constraints that cannot be established through the current API. Do not treat a document timestamp as a CMS signature for seed checks without standards support.
2. Preserve unchanged-output guarantees for all failures before destination copying/appending. For destination I/O or finalization failures, attempt restoration and make a restoration failure observable together with the original failure. Avoid leaking sanitized host errors or claiming engine rollback proves physical output restoration.
3. Proposed owner-facing charter replacement for the disputed sentence: "Signer, timestamp-authority, size, and validation failures before destination writing leave the output unchanged. If destination writing or finalization fails, the engine attempts to restore the destination's original length. Restoration may fail; failure reporting preserves the original failure and makes a restoration failure observable. A destination whose restoration fails may contain partial output."

## Questions to answer

1. Give a concrete `/SV` enforcement table: entry, mandatory flag/condition, applicable PDF version, meaning, whether our current output/options can satisfy it, and refusal behavior. Cover Filter, SubFilter (including array preference/order), V, Reasons (including an empty list and an omitted reason), DigestMethod, certificate constraints, timestamp requirements, certification/MDP, legal attestations, revocation information, and any other applicable mandatory entry. Identify which nested dictionaries have their own flags. Do not assume every entry uses the top-level Ff. Explain treatment of unknown flag bits and malformed mandatory values versus irrelevant optional values.
2. Where must `/SV` be read in a field hierarchy? Is it inheritable? How should separate field dictionaries and widget dictionaries, repeated widget slots, indirect values, and multiple matching value owners be handled? Explain applicability to `/DocTimeStamp` in an existing field.
3. Which requirements can be enforced from options before invoking an arbitrary custom provider, and which cannot? Is refusal of unverifiable mandatory constraints the appropriate narrow solution? Avoid adding signature validation or provider introspection to solve this item.
4. Recommend an exception/reporting shape for original failure plus rollback failure on net48 and net10.0. Compare preserving the original exception type and identity with a dedicated exception carrying both failures. Address cancellation classification, source/destination integrity, failed Seek after successful SetLength, and retry safety after unsuccessful restoration. Should signing use a distinct rollback reporting path to avoid silently changing ordinary save behavior?
5. Review the proposed charter replacement above. Return exact recommended wording and list any separately required specification/acceptance clarifications. The owner, not you, must approve protected wording. Explain whether this is clarification of an existing destination-failure exception or an actual guarantee change.
6. Suggest a minimal set of permanent regressions that distinguishes supported mandatory constraints, unsupported mandatory constraints, ignored optional entries, pre-provider refusal, partial output, successful rollback, unsuccessful truncation, finalization failure, and combined failures. Identify any independent producer/verifier needed if signature output bytes remain unchanged.

Please return (a) the standards-backed enforcement table, (b) recommended failure reporting and exact contract wording, (c) unresolved owner choices, and (d) minimal tests. State uncertainties explicitly.
