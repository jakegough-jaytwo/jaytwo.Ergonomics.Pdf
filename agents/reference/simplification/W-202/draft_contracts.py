"""Generate an exact proposal diff without editing either shipped contract."""
import difflib
from pathlib import Path

out = []

def propose(path, replacements):
    original = Path(path).read_text(encoding='utf-8-sig')
    draft = original
    for before, after in replacements:
        if draft.count(before) != 1:
            raise ValueError(f'{path}: expected one occurrence of {before!r}')
        draft = draft.replace(before, after, 1)
    out.extend(difflib.unified_diff(original.splitlines(True), draft.splitlines(True),
                                    fromfile='a/' + path, tofile='b/' + path, n=0))

propose('ACCEPTANCE.md', [
    ('`Sign(options, provider)`', '`Sign(options, signer)` and `Timestamp(authority, options)`'),
    ('A provider whose kind differs from `PdfSignatureOptions.Kind` throws `PdfSignException` and does not extend the file.',
     'A CMS signing operation whose options select a document timestamp throws `PdfSignException` and does not extend the file. Document-timestamp entry points normalize Kind to DocTimeStamp and retain the existing digest and format refusals.'),
    ('before invoking the provider or writing bytes', 'before invoking the host signer or timestamp authority or writing bytes'),
    ('PDF 1.7 unknown provider key types do not exempt a mismatch; possible over-refusal is documented.',
     'The adopted PDF 1.7 digest-mismatch refusal remains in force; this API change does not relax DigestMethod checks.'),
    ('Required certificate Subject, Issuer, OID, SubjectDN, and KeyUsage restrictions are refused without provider introspection.',
     'Required certificate Subject, Issuer, OID, SubjectDN, and KeyUsage restrictions remain refused; this API change adds no certificate-constraint introspection.'),
    ('- Providers are required to honor the exact requested format, digest, and signed PDF byte sequence, with SHA-256 messageImprint for document timestamps. Arbitrary provider output is not independently checked for format, digest, byte coverage, certificate attributes, or policy. Seed tests with opaque provider contents establish preflight and provider inputs, not cryptographic validity.',
     '- Public signing uses a raw-digest `IPdfExternalSigner` and optional `ITimestampAuthority`; public document timestamps use `ITimestampAuthority`. Public complete-CMS providers, caller-defined CMS attributes and the signed-byte context are unavailable. Direct-engine same-source and cross-stream sync/async variants remain, and the workflow retains its stream/file variants. The engine constructs the requested format and digest over the signed PDF byte sequence, with SHA-256 messageImprint for document timestamps. Seed tests using internal opaque contents establish preflight and engine inputs, not cryptographic validity. The retained profiles, algorithms, additional PDF approval signatures, timestamp checks, reservation bounds, prefix preservation, permission/seed/lock enforcement, one-editor private staging, cancellation and rollback diagnostics retain their existing assertions.'),
    ('`CmsDetachedSignatureProvider` verifies with', 'Engine-generated CMS verifies with'),
    ('An `ITimestampAuthority` passed to the provider receives', 'An `ITimestampAuthority` passed to the signing operation receives'),
    ('A CMS value from `CmsDetachedSignatureProvider` that is larger', 'An engine-generated CMS value that is larger'),
    ('`DocTimeStampSignatureProvider` refuses a token larger', 'Engine document-timestamp construction refuses a token larger'),
    ('`CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider` call the nonce overload', 'Engine CMS and document-timestamp construction call the nonce overload'),
    ('`DocTimeStampSignatureProvider` sends the request', 'Engine document-timestamp construction sends the request'),
    ('surfaces from `CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider` as a fresh', 'surfaces from engine CMS and document-timestamp construction as a fresh'),
    ('inside a host timestamp authority called by either provider', 'inside a host timestamp authority called by either engine builder'),
])

propose('docs/behavior.md', [
    ('`IPdfExternalSigner.SignHashAsync`, `ITimestampAuthority.GetTimeStampTokenAsync`, and `IPdfSignatureProvider.CreateContentsAsync`',
     '`IPdfExternalSigner.SignHashAsync` and `ITimestampAuthority.GetTimeStampTokenAsync`'),
    ('waits for the provider', 'waits for the host signer or timestamp authority'),
    ('A custom `IPdfSignatureProvider` must honor `PdfSignatureContext.Format`, because the `/SubFilter` already names it.',
     'The engine constructs CMS using the requested format. Public complete-CMS providers and caller-defined CMS attributes are not supported. A service that only returns complete CMS cannot generally implement the raw-digest signer contract.'),
    ('`CmsDetachedSignatureProvider` can take an `ITimestampAuthority`.', 'A CMS signing operation can take an `ITimestampAuthority`.'),
    ('The provider rejects a token', 'The engine rejects a token'),
    ('A document timestamp is still a second revision from `DocTimeStampSignatureProvider`, and that provider applies the same checks.',
     'A document timestamp uses a separate timestamp operation and revision, and the engine applies the same checks.'),
    ('new CmsDetachedSignatureProvider(pkcs11Signer)', 'pkcs11Signer'),
    (', or an existing `IPdfSignatureProvider`', ''),
])
Path('agents/reference/simplification/W-202/contract-diff.patch').write_text(''.join(out), encoding='utf-8')
print('Wrote proposal diff only; shipped contracts unchanged.')
