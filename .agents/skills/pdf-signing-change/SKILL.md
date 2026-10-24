---
name: pdf-signing-change
description: Change PDF signatures, CMS encoding, external signing, RFC 3161 timestamps, or certification gates. Use when implementation work touches those areas.
---

# PDF signing change

Follow the signer contract, supported algorithms, certification rules, and exclusions in `AGENTS.md`. Work from that text, not from memory. Read only the applicable `ACCEPTANCE.md` and `README.md` sections and the current task record.

Read `references/signing-matrix.md` before claiming signing evidence. It separates the properties a test or verifier can establish and names what establishes each.

## Working rules

- Until reset stage R4, change signing only for a regression or migration breakage (D-54).
- Keep distinct the `/ByteRange` digest, CMS signed attributes, their DER encoding, the signature over those bytes, the CMS object in `/Contents`, and a timestamp imprint over the signature value. Never hand the signer the raw document digest as if it were the signed-attributes digest.
- Preserve the `IPdfExternalSigner` boundary. The host owns private keys and PKCS#11; the library receives the specified raw SHA-256 signing contract.
- Hardware mechanisms differ on whether they hash their input. A mechanism that hashes internally hashes the digest a second time. State this in the contract docs, and test it through a real token in the test project.
- Treat four results as separate claims: the signature is intact, it covers the latest revision, certification policy permits it, and it chains to a trusted root. Never infer one from another. Certificate embedding helps a verifier build a path. It does not make the signature trusted.
- Keep CMS signature timestamps and document timestamp revisions separate. They are not interchangeable.
- Keep three kinds of checking apart. Input checks while signing, such as matching a timestamp response to its request by status, imprint, and digest algorithm, are in scope. Verification by external tools is evidence, not a library feature. Trust paths, revocation, signature validation, and long-term validation are excluded.
- Enforce DocMDP and FieldMDP exactly as specified. Do not reinterpret settled behavior.
- Preserve the documented destination-stream failure guarantee and the bounded signature appendix. The engine does not invent staging or temporary-file schemes; publishing is the host's or destination adapter's job. An Ergonomics file convenience may write a temporary sibling and replace the target after success (`reset-plan.md` section 4.4).
- Never log keys, PINs, passwords, or token secrets. Do not hide a cryptographic failure behind a generic error.
- Use framework cryptographic primitives. Implement only the PDF-, CMS-, and RFC 3161-specific structures required here. Do not build a general ASN.1 or PKI toolkit.

## Done

- Independent verification of the signing output names which of the four results it establishes.
- The signer contract docs still match the code.
- Signing a large file still hashes incrementally with bounded buffers.

A change to the signer contract, algorithms, or certification behavior is an owner decision. A requirement that needs signature validation, revocation data, or a new profile is a scope change. Use `pdf-core-change` only when object access, encryption, or the writer changes. Use `pdf-acroform-change` only when field discovery or appearances change. Finish with `pdf-change-verification`.
