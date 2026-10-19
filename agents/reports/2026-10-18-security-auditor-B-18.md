# B-18 independent security review

## 1. Task and owner

Security Auditor. Static, independent review of committed checkpoint `d4b84dde4c4e74702d65c11ab4e1ca4a364df9be`. The dirty B-34/B-35 worktree was not reviewed as product code. No product, test, fixture, or state file was changed.

Result: no Critical finding. Seven High and four Medium findings are Confirmed from control flow. They are release blockers or disposition items for the Technical Lead; this report does not pass G8 and does not replace the required fuzz run.

## 2. Verified facts

- The review used a `git archive` of `d4b84dd`, outside the repository. `git diff --name-only d4b84dd -- src test README.md ACCEPTANCE.md` confirms that the interrupted work differs in the five timestamp source files and two timestamp-test files named on the board; those differences were excluded.
- The checkpoint contains the B-28 through B-31 protections: 256-level limits on several traversals, xref and structural decode caps, predictor-width checks, the corrected DER length subtraction check, and the 2 MiB `HttpTimestampAuthority` response cap.
- `PdfSecurity` uses generic password/decryption diagnostics and fixed-length comparisons. No password or file-key disclosure was found in the reviewed code.
- `CmsDetachedSignatureProvider` constructs the signed-attributes SET with the correct DER tag for hashing, changes it to context tag 0 for `SignerInfo`, hashes the RSA signature value for the timestamp imprint, and leaves signature dictionaries plaintext. `/ByteRange` excludes exactly `<`, the reserved hex, and `>`.
- Trust and signature validation are excluded by the charter. The absence of TSA trust validation and CMS verification is therefore not a finding. The checkpoint's omitted `certReq` and absent ESS-certificate inclusion check are already B-35 under D-21, not new B-18 findings.
- Adobe's PDF 1.7 reference, Table 8.106, defines FieldMDP `Include` as locking the listed fields and `Exclude` as locking every unlisted field. Adobe's PDF 1.5 reference defines omitted `StmF` and `StrF` as `Identity`.

## 3. Assumptions

- Severity follows `agents/roles/security-auditor.md`: a process crash or uncontrolled resource use reachable from an untrusted PDF, TSA response, or signer callback is High; a bounded excessive allocation or a non-security guarantee failure is Medium.
- The caller and host process are trusted. PDF bytes, timestamp bytes, and all behavior at the external-signer callback boundary are hostile.
- Findings are Confirmed by source inspection. No finding below depends on the dirty worktree. Deliberately exhausting memory or overflowing the process stack was not executed.

## 4. Defects confirmed

### SA-01 — High — PDF lexical values bypass or lack the structural cap

- **Status:** Confirmed.
- **Location:** `Internal/PdfParser.cs:689-932` (`ReadNumber`, `ReadKeyword`, `ReadName`, `ReadLiteralString`, and `ReadHexString`).
- **Trigger:** Put an arbitrarily long non-delimited number, keyword, name, or hex string in an object reached during `Open`. Alternatively, use only escaped bytes or nested `(` characters in a literal string. The literal-string cap at line 884 is skipped by the escape and nested-parenthesis branches because they `continue` first.
- **Impact:** `StringBuilder` and `List<byte>` grow to input size, and names duplicate the bytes into a second list and a UTF-8 string. A large hostile PDF can exhaust managed memory or terminate the process before `PdfObjectTooLargeException` is reached. The documented 32 MiB structural bound does not protect these paths.

### SA-02 — High — dictionary parsing has quadratic CPU cost and no member bound

- **Status:** Confirmed.
- **Location:** `Internal/PdfParser.cs:477-494`; `Internal/PdfObject.cs:43-54`.
- **Trigger:** A trailer or other eagerly read dictionary containing many distinct short keys. Every parsed key calls `Dict.Set`, which linearly scans all prior entries before appending.
- **Impact:** Parsing `n` distinct entries performs O(`n^2`) string comparisons. A comparatively small untrusted dictionary can pin a CPU for an uncontrolled period during `Open`; there is no dictionary-member or work limit.

### SA-03 — High — xref and object-stream caps permit object-count allocation amplification

- **Status:** Confirmed.
- **Location:** `Internal/PdfParser.cs:296-329`; `Internal/PdfEngine.cs:1276-1302` and `1428-1479`, especially line 1451; `Internal/PdfEngine.cs:3531-3554`.
- **Trigger:** For an xref stream, decode exactly 64 MiB with `/W [0 1 0]` and `/Index [0 67108864]`. The count passes the decoded-byte check and attempts 67,108,864 `Dictionary<int,XrefEntry>` insertions. For an object stream, set `/First` to a 32 MiB header containing millions of one-character tokens; `Encoding.ASCII.GetString` and `SplitTokens` materialize the whole string and one substring per token before `/N` is used to bound required header parts.
- **Impact:** A highly compressible stream within the advertised byte cap expands into hundreds of MiB or multiple GiB of dictionaries, strings, and list storage and can terminate the process with `OutOfMemoryException`. The decode cap therefore does not bound the working allocation or object count.

### SA-04 — High — an acyclic page tree can overflow the process stack

- **Status:** Confirmed.
- **Location:** `Internal/PdfEngine.cs:1811-1866`, `WalkPages`.
- **Trigger:** A deeply nested chain of distinct `/Pages` objects. `seen` detects cycles, but `WalkPages` has no depth parameter or `MaxTraversalDepth` check and recursively calls itself for every child.
- **Impact:** `PageCount`, `Pages`, field operations, and `Sign` can cause an uncatchable `StackOverflowException` and terminate the host process on an untrusted but acyclic page tree.

### SA-05 — High — signer and timestamp callback output is unbounded before DER/CMS copies

- **Status:** Confirmed.
- **Location:** `Signatures/CmsDetachedSignatureProvider.cs:60, 78, 96, 121-135`; `Signatures/DocTimeStampSignatureProvider.cs:49-51`; `Signatures/DerCodec.cs:10-27, 120-161, 263-279`; the final size check is only in `Internal/PdfEngine.cs:966-970` after `CreateContents` returns.
- **Trigger:** A hostile signer returns an oversized certificate, signature, or additional-certificate list, or a non-HTTP `ITimestampAuthority` returns an oversized token. `DerCodec.ReadTlv` allocates both `Full` and `Content` for every level, and `Set`, `Sequence`, and `Concat` repeatedly copy the same payload. The 1 MiB reserved-contents bound is checked only after all provider construction completes. `HttpTimestampAuthority`'s 2 MiB cap protects only that concrete implementation.
- **Impact:** Callback-controlled bytes can force allocation amplification, integer-overflow/runtime exceptions in `Concat`, or process-wide OOM before the bounded signature-gap check can reject them.

### SA-06 — High — external-signer diagnostics are sanitized only for `SignHash`

- **Status:** Confirmed.
- **Location:** `Signatures/CmsDetachedSignatureProvider.cs:60, 78-83, 121-124`.
- **Trigger:** `CertificateDer` or `AdditionalCertificates` throws an exception containing a PIN, provider credential, or key-related diagnostic. These callback accesses occur outside the `try/catch` that replaces exceptions from `SignHash`.
- **Impact:** The original message, `Data`, inner exception, and stack flow back to the caller and can enter application logs. This is a secret-disclosure path at a callback boundary the audit role requires treating as hostile. The destination is not extended, but the diagnostic guarantee is not met.

### SA-07 — High — actual signature appendix size is not capped

- **Status:** Confirmed.
- **Location:** `Internal/PdfEngine.cs:917-928` and `950-993`; `WriteAppendix` at `2889-2965`.
- **Trigger:** Reuse an existing empty signature field whose owning dictionary contains an unrelated literal string larger than 8 MiB but below the parser's nominal 32 MiB limit, then call `Sign`. `SealExistingField` dirties and rewrites that whole dictionary. The estimate counts 8,192 bytes, twice the reserved gap, and dirty stream bodies only; it omits serialized dictionaries, arrays, strings, names, and xref text.
- **Impact:** `WriteAppendix` can allocate and append more than the documented 8 MiB limit. With SA-01 input forms the growth is not bounded by the nominal structural cap. This is both a resource path and a direct violation of the product's retained appendix limit.

### SA-08 — Medium — FieldMDP `Include` and `Exclude` are reversed

- **Status:** Confirmed.
- **Location:** `Internal/PdfEngine.cs:2346-2374`, especially `return !names.Contains(fullName)` for `Include` and `return action == "Exclude" && names.Contains(fullName)`.
- **Trigger:** Fill a field named by an `Include` transform, or fill an unlisted field under an `Exclude` transform.
- **Impact:** The first modification is allowed even though the PDF transform locks the named field; the second is allowed even though `Exclude` locks every unlisted field. Conversely, allowed fields are blocked. Output can violate the signed field-modification policy and be rejected by a PDF-aware verifier. `README.md` documents the same reversed interpretation, so code and public contract both need correction rather than a test-only change.

### SA-09 — Medium — revision-4 crypt-filter defaults are wrong

- **Status:** Confirmed.
- **Location:** `Internal/PdfSecurity.cs:249-262`, especially lines 256-257.
- **Trigger:** Open a valid revision-4 standard-security PDF whose encryption dictionary defines crypt filters but omits `StrF` and/or `StmF`. PDF 1.5 and later define each omitted value as `Identity`; the implementation substitutes `StdCF`.
- **Impact:** Clear strings or streams are decrypted as RC4/AES, producing garbage or a parse/decryption failure, and later writes can encrypt data that the source policy left clear. This breaks the advertised revision-4 read/write matrix but does not disclose a key.

### SA-10 — Medium — timestamp matching accepts trailing and semantically malformed DER

- **Status:** Confirmed.
- **Location:** `Signatures/DerCodec.cs:120-161`; `Signatures/Rfc3161TimeStampToken.cs:20-116`; `Signatures/CmsDetachedSignatureProvider.cs:96-102`; `Signatures/DocTimeStampSignatureProvider.cs:49-51`.
- **Trigger:** Return a token whose first top-level TLV contains the expected imprint and nonce but has trailing bytes after that TLV, trailing values in nested structures, wrong/unvalidated version-policy-serial field tags, or extra fields after the first matching nonce. `ReadTlv` returns the first TLV without requiring full-span consumption, and `CheckTstInfo` returns immediately on the first matching INTEGER nonce.
- **Impact:** The library reports successful timestamp construction for structurally malformed data. The CMS provider then embeds the original token byte array, including top-level trailing garbage, so the produced CMS can be rejected by an independent verifier. This finding does not request trust or signature validation; it is strict structural consumption of the data already parsed.

### SA-11 — Medium — non-name `/Filter` entries are silently ignored

- **Status:** Confirmed.
- **Location:** `Internal/PdfStreamDecoder.cs:67-123`, `FiltersOf` and `ParmsOf`.
- **Trigger:** Supply `/Filter` as a non-name, or put non-name values among names in a filter array. `FiltersOf` silently drops them while `ParmsOf` can retain the original positional entries.
- **Impact:** Malformed or unsupported encoded streams may be exposed as raw bytes or decoded with misaligned parameters instead of failing predictably. For supported attachment extraction this can return incorrect bytes while reporting success; for structural streams it can change which bytes the parser interprets.

## 5. Evidence missing

- No crash/OOM proof was run; deliberately destabilizing the shared process was not justified for the read-only review. Each resource finding names a concrete construction suitable for a bounded regression harness.
- The required fixed-seed fuzz harness and 24 aggregate CPU-hour run remain outstanding. This report alone does not pass G8.
- B-34 and B-35 evidence remains with the active implementation/QA workflow. This review neither accepts nor rejects that dirty work.

## 6. Changes made or proposed

Changed only this report.

Proposed disposition: treat SA-01 through SA-07 as G8 release blockers until fixed with permanent bounded regression tests. Board SA-08 through SA-11 for standards/correctness fixes or explicitly document a release-blocking limitation; SA-08 conflicts with signed-field policy and should not be accepted as a documentation-only limitation.

## 7. Verification run

Commands used for baseline and inspection:

```text
git rev-parse d4b84dd
git status --short
git diff --name-only d4b84dd -- src test README.md ACCEPTANCE.md
git archive --format=tar --output=<temp> d4b84dd
rg --files src/jaytwo.Ergonomics.Pdf
rg -n <targeted parser/signing/security patterns> src/jaytwo.Ergonomics.Pdf
```

Baseline resolved to `d4b84dde4c4e74702d65c11ab4e1ca4a364df9be`. No build or test command was run. No finding is reported as dynamically reproduced.

Standards references used for SA-08 and SA-09: Adobe PDF Reference 1.7, Table 8.106, and Adobe PDF Reference 1.5, encryption dictionary entries `StmF` and `StrF`.

## 9. Handoff

Technical Lead assigns disposition and regression ownership. Product Manager treats all seven High findings as incompatible with G8 acceptance until disposition and evidence exist. QA uses the named triggers as seeds for the B-18 fuzz harness and bounded regressions. Security Auditor remains read-only.
