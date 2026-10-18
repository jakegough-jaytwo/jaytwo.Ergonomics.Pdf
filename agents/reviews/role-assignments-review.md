# Review: agent role assignments

Date: 2026-10-18
Scope: the six files in [`agents/roles/`](../roles/). I checked each one against [`AGENTS.md`](../../AGENTS.md), [`README.md`](../../README.md), [`ACCEPTANCE.md`](../../ACCEPTANCE.md), and the current source and tests.

## Verdict

The team shape works: PM, tech lead, three subsystem owners, and an independent QA role. The roles share a good core. They use the same four evidence levels (implemented, internally tested, independently validated, production-qualified), they distrust their own round-trip tests, and each first assignment is an assessment, not a rewrite. The QA role is the strongest. Its independence rules ("may not approve your own implementation as independently validated", "a controlled, documented rejection is a successful outcome") are the right guardrails for an agent team.

Three problems need fixing before the roles go to work:

1. **They contradict the charter in several places.** `CLAUDE.md` imports `AGENTS.md`, so every agent reads both. In each conflict below, the role prompt invites work that the charter rules out. No role says which document wins.
2. **They assign completed work.** Several "historical" items are already implemented and covered by `ACCEPTANCE.md`. As written, the agents will spend their first pass re-checking that work, and some of them will reopen it.
3. **Metadata and attachments have no owner.** Both are chartered capabilities. Neither has a responsible role.

The roles also repeat a lot of shared text. That length is a maintenance problem in its own right: the copies have already started to drift apart (details below).

---

## 1. Conflicts with the charter (high)

| Topic | Charter says | Role says | Where |
|---|---|---|---|
| Runtime dependencies | "Zero runtime dependencies is a constraint, not a gap." "No runtime NuGet dependencies, except `System.Memory` on `netstandard2.0`." | "we do not require literally zero NuGet dependencies"; "Consider small, well-maintained, permissively licensed utility packages" | [project-manager.md:84-113](../roles/project-manager.md), [tech-lead.md:74-104](../roles/tech-lead.md), [core-engineer.md:156-160](../roles/core-engineer.md), [cryptography-engineer.md:99-107](../roles/cryptography-engineer.md), [qa-engineer.md:120-124](../roles/qa-engineer.md) |
| | | The PM role contradicts itself: its closing line asks for a "zero-third-party-runtime-dependency" toolkit. | [project-manager.md:306](../roles/project-manager.md) |
| Xref output format | "Write classic xref only." | "Support for writing cross-reference streams may eventually be appropriate" | [core-engineer.md:244-254](../roles/core-engineer.md) |
| 8 MiB signature appendix | "The 8 MiB signature appendix stays until a real revision needs more." | "you own any reusable revision-writing infrastructure needed to remove this limit" | [core-engineer.md:256-262](../roles/core-engineer.md) |
| Signature validation | Out of scope: "signature validation and revocation", "PAdES-LTV" | The signing role owns "Timestamp token verification", "TSA trust and certificate considerations", "Independent CMS verification", and "Relevant PAdES baseline requirements". §8 sets out B-LT and B-LTA. | [cryptography-engineer.md:77-91](../roles/cryptography-engineer.md), [:228-241](../roles/cryptography-engineer.md), [:268-285](../roles/cryptography-engineer.md) |
| | | QA requires a negative test for an "Untrusted TSA certificate". The library can only fail that test by doing trust validation. | [qa-engineer.md:429](../roles/qa-engineer.md) |
| `/NeedAppearances` | The charter fixes when the flag is set: comb, rich text, non-WinAnsi, password, and signature values. Signing then throws. | "Do not rely on `/NeedAppearances` as the normal solution", with no mention of the existing rule. An agent could read this as a mandate to generate comb or non-WinAnsi appearances, which means a font engine. | [acroform-engineer.md:314](../roles/acroform-engineer.md), [:342-354](../roles/acroform-engineer.md) |
| Geometry API | Charter item 3: name, value, rectangle, page, rotation, and media box. ACCEPTANCE says "rectangle in bottom-left user space". | Lists "reported in raw PDF coordinates" as a limitation and asks for a top-left normalized UI coordinate system, plus CropBox and UserUnit normalization. | [acroform-engineer.md:368-398](../roles/acroform-engineer.md), [:455-456](../roles/acroform-engineer.md) |
| Signer contract | `SignHash` takes a raw SHA-256 digest and must match `RSA.SignHash(hash, SHA256, Pkcs1)`. | §4 treats the mechanism as an open design question ("RSA-PSS, if introduced later"). The tech lead mentions "`IPdfExternalSigner` or its successor abstraction". | [cryptography-engineer.md:145-163](../roles/cryptography-engineer.md), [tech-lead.md:200](../roles/tech-lead.md) |
| PKCS#11 | "Do not P/Invoke PKCS#11 from this library." | "Exercise the signing abstraction through a real PKCS#11 provider"; QA mentions "the actual adapter". Neither says the adapter belongs in the test project. | [cryptography-engineer.md:398-404](../roles/cryptography-engineer.md), [:456-460](../roles/cryptography-engineer.md), [qa-engineer.md:387-395](../roles/qa-engineer.md) |
| Failure semantics | "Failed signs do not write." The destination length stays unchanged. The README tells the host to swap files only after `Sign` returns. | "Distinguish internal output construction from atomic publication. Temporary storage or staged output may be appropriate." This invites temp-file machinery that the stream API leaves to the host. | [cryptography-engineer.md:339-341](../roles/cryptography-engineer.md), [qa-engineer.md:547-558](../roles/qa-engineer.md) |
| Output stream kinds | Destinations are streams, and same-stream saves append. | QA: "Test seekable and non-seekable outputs where the API permits them" and "Cancellation occurs where supported". The public API has no `CancellationToken`. | [qa-engineer.md:542](../roles/qa-engineer.md), [:549](../roles/qa-engineer.md) |
| TSA nonce | `Rfc3161TimeStampRequest.Create` builds "a version-1 request with no nonce and certReq omitted". | QA wants a negative test for a "missing or mismatched nonce when one was requested". The library never requests one. | [qa-engineer.md:427](../roles/qa-engineer.md) |

The table above is not all bad advice. One role instruction points at a real gap. `HttpTimestampAuthority` checks the PKI status and returns the token. Neither it nor `CmsDetachedSignatureProvider` compares the token's message imprint with the request. The signing role's "Do not accept an arbitrary ASN.1 response merely because it contains something resembling a timestamp token" is a fair task. The fix is to draw the line explicitly. Checking that the response matches the request (status, imprint, digest algorithm) is in scope. Validating trust in the TSA certificate is signature validation, which is out of scope.

**Fix:** add one shared rule at the top of every role:

> `AGENTS.md`, `README.md`, and `ACCEPTANCE.md` are the charter. Where this role conflicts with them, the charter wins. Stop and escalate instead of choosing for yourself.

Then edit each row above so the role agrees with the charter, or move the topic into §6 as a decision for you.

## 2. Stale baselines send agents back over finished work (high)

Every role has a "historical baseline" section that asks the agent to verify it. That is sound in principle, but several items are already settled and covered by acceptance tests:

| Role framing | Actual state |
|---|---|
| "Earlier font measurement used approximate character widths… replace approximations" ([acroform-engineer.md:332-336](../roles/acroform-engineer.md), [:453](../roles/acroform-engineer.md)) | Adobe Helvetica advances are used. ACCEPTANCE checks that `W` is wider than `i`. |
| "The existing implementation historically rejected all modifications when `/DocMDP` was present" ([cryptography-engineer.md:300-304](../roles/cryptography-engineer.md)) | DocMDP `P` 1/2/3 and FieldMDP All/Include/Exclude are implemented and tested. |
| Priority 2 "Certificate-chain embedding" and Priority 3 "Implement or complete signature timestamp attributes" ([cryptography-engineer.md:444-454](../roles/cryptography-engineer.md)); PM P1 "Complete… certificate-chain embedding, CMS-integrated RFC 3161" ([project-manager.md:231-235](../roles/project-manager.md)) | Both are implemented. `AdditionalCertificates` is embedded, and the token is stored as unsigned attribute `…16.2.14`. What remains is **independent** validation against a real TSA, not implementation. |
| Encrypted xref initialization "reportedly corrected" ([core-engineer.md:264-272](../roles/core-engineer.md)) | Pinned in ACCEPTANCE: `/Encrypt` is resolved "without scanning the file". |
| Object caching "Verify the current behavior" ([core-engineer.md:216-232](../roles/core-engineer.md)) | Documented in the README ("Clean objects loaded from an object stream are released when the public operation returns") and tested in `ObjectStreamCacheTests`. |
| "Do not silently corrupt larger files" past 9,999,999,999 ([core-engineer.md:252](../roles/core-engineer.md)) | `PdfTooLargeForClassicXrefException` is thrown before any write. |

The "139 tests" figure still holds: 101 `[Fact]`s plus 38 `[InlineData]` rows, on each of net6.0 and net8.0.

**Fix:** delete the baseline sections. Replace each with one line: "`ACCEPTANCE.md` is the current verified behavior; the open gaps are listed in `<backlog file>`." Keep the list of open gaps in one place that the PM maintains, not in six prompts.

## 3. Unowned areas (medium)

- **Metadata and XMP.** Info/XMP sync, PDFDocEncoding, date parsing, and malformed-XMP handling (`XmpEditor.cs`, `PdfMetadata.cs`). No role owns these. The tech lead's collaborator list ([tech-lead.md:255-258](../roles/tech-lead.md)) leaves them out, and none of the PM priorities touch them.
- **Attachments.** Name trees, `/AF`, streamed copy, and `/EFF` handling (`Attachments/`). Same gap. The core role owns "large-file I/O" but not the attachment semantics.
  *Suggestion:* give both to the core engineer explicitly, since they are small and structural. Otherwise add them to the tech lead's direct ownership.
- **`ACCEPTANCE.md` itself.** The PM "owns acceptance criteria", but no role says acceptance criteria live in `ACCEPTANCE.md`, or that a behavior change must update it in the same change.
- **Target frameworks.** The library ships `netstandard2.0` and `netstandard2.1`, but the tests run only on `net6.0` and `net8.0`, so the `System.Memory` path is never exercised. `net6.0` is out of support, and `net8.0` support ends in November 2026. No role owns TFM policy or a `net4x`/`netstandard` test leg.
- **Build hygiene.** StyleCop is treated as errors. The other rules: XML docs, one type per file, file-scoped namespaces, `_` field prefixes, the scoped SA1201/1202/1204 pragma policy, and the Jenkins pipeline. `AGENTS.md` covers these, so agents will see them. The roles' own "implementation standards" sections never point there, though, so an agent following its role alone could produce changes that fail the build.

## 4. Coordination mechanics are missing (medium)

- **There is no git repository.** The working directory is not a git repo. Every engineering role says "use isolated branches or worktrees where available". Neither is available, so concurrent agents will edit one tree. `PdfEngine.cs` is 3,650 lines, and four of the five engineering roles will want to touch it. Run `git init` before starting the team, or state a serial integration order.
- **No artifact locations.** The roles ask for a backlog, assessments, defect reports, evidence inventories, and a readiness assessment, but never say where any of these go. Pick paths, for example `agents/backlog.md`, `agents/assessments/<role>.md`, and `agents/defects/`.
- **Five parallel first assignments.** Every role starts by inspecting the whole repo and writing an assessment. That is five overlapping audits of the same 4k-line core. A better order: PM and tech lead first, which produces the backlog and ownership. Then specialists assess only their own area against that backlog. Or let QA's evidence inventory serve as the shared baseline.
- **Names don't line up:**
  - The file `project-manager.md` defines a "Product Manager".
  - The file `cryptography-engineer.md` defines a "Signing & Cryptography Engineer", and the other roles call it the "Signing Engineer".
  - The QA file's title is "QA, Interoperability & Reliability Engineer", and the other roles call it the "QA & Interoperability Engineer".

  Use one name per role, and match the file names to it.

## 5. Size and duplication (low, but it compounds)

The six files total 3,274 lines. Most of every file repeats the same shared material: product context, exclusions, dependency philosophy, evidence levels, the "historical baseline" disclaimer, and escalation boilerplate. The copies have already drifted:

- The capability lists differ from role to role.
- The PM includes "Associated Files" and metadata consistency; the core role doesn't.
- The tech lead lists "Efficient sparse reads on a 3 GiB test document" as done. QA lists it as only a sparse test.

Every agent also loads its 300–750 lines on top of `AGENTS.md`.

**Suggestion:** create `agents/roles/_team.md` for the shared material: charter precedence, evidence levels, escalation triggers, and artifact locations. Cut each role down to its delta: ownership, boundaries, definition of done, and first assignment. A target of about 100–200 lines per role looks realistic. The current files are mostly short paragraphs that restate the same idea.

## 6. Decisions to make once, in the charter

These topics come up in several roles with different leanings. Settle each one in `AGENTS.md`, or record it as an open owner decision, so agents don't resolve them independently:

1. **Geometry coordinates.** Keep raw user space plus rotation and media box (the current charter), or add normalized top-left output for browser overlays?
2. **TSA response checks.** Should the library check imprint, status, and digest algorithm (yes, in my view), and should it request a nonce and `certReq`? State that TSA trust stays out.
3. **The 8 MiB appendix and classic-only xref writing.** The charter already decides both. Remove the language in the core role that reopens them.
4. **Dependency wording.** The roles' "lightweight packages acceptable" stance and the charter's "zero, except System.Memory" cannot both stand.
5. **Production-qualified.** Every role uses the term. None defines the bar, such as which validators, which fixtures, and which memory numbers. Its absence makes "production-qualified" impossible to reach or easy to claim.

## 7. Per-role notes

**Project manager.** The backlog item template and the escalation list are good. The "Required capabilities" list (lines 27-59) restates the README charter, but in different words. Point to the README instead. Add metadata and attachments to the priorities, or say explicitly that they have no open gaps. The closing sentence contradicts §1.

**Tech lead.** The decision-authority split (lines 426-444) is clear and useful. Line 200 ("or its successor abstraction") and the collaborator list (lines 253-258) need fixes, as described above. The concurrency section is right, but there is no git, so it can't be followed yet.

**Core engineer.** The performance method (lines 386-394) and "Do not build permissive repair heuristics merely to make malformed test fixtures pass" are good guidance. Remove or reframe "Large offsets and output" and "Revision buffering" (lines 244-262), which reopen charter decisions. Take ownership of metadata and attachments.

**Signing & cryptography engineer.** §4 (signed-attributes digest vs. document digest) and §9 ("Do not confuse…") are the most valuable technical content in the set. Keep them. Bring §2, §6, and §8 back within the charter: no trust validation and no PAdES-LTV. Replace the generic mechanism discussion in §4 with the actual `SignHash` contract and its known pitfall: `CKM_SHA256_RSA_PKCS` hashes a second time, so the host has to wrap the digest in DigestInfo and sign with `CKM_RSA_PKCS`. Say that the SoftHSM adapter lives in the test project. Update §13 to "validate", not "implement".

**AcroForm engineer.** Remove the illustrative `PdfFormField` / `PdfFieldWidget` API (lines 211-236). Disclaimers aside, it primes an agent toward an API redesign. It also uses `object? Value` where the existing `PdfField.Value` is `string?`. State the charter's `/NeedAppearances` policy in §7, so that "report the limitation clearly" means the existing `PdfAppearanceDisposition` return value, not new font work. Make §8 geometry normalization an owner decision (§6.1 above), not a standing mandate.

**QA engineer.** The best of the six. The rule that "tests must not silently pass because a validator was unavailable" matches how `ForeignPdfTests` already behaves. Remove or condition the scenarios the library can't produce: untrusted TSA, nonce mismatch, non-seekable outputs, and cancellation. Add one sentence telling QA to record the property each independent tool established, using the existing ACCEPTANCE "Foreign files" section as the format to extend.

## 8. Edit checklist

- [ ] Add charter precedence to every role (or to a shared `_team.md`).
- [ ] Fix the dependency wording in all five places, plus the PM's closing line.
- [ ] Remove the invitations to write xref streams and to lift the 8 MiB appendix from the core role.
- [ ] Bring the signing role's validation, trust, and PAdES-LTV language within the charter, and split response checks from trust validation.
- [ ] Replace the generic signing-mechanism discussion with the actual `SignHash` contract, and say where the PKCS#11 test adapter lives.
- [ ] Remove the illustrative API from the AcroForm role, and state the `/NeedAppearances` policy.
- [ ] Remove the QA scenarios the API can't produce.
- [ ] Replace the historical baselines with a pointer to `ACCEPTANCE.md` plus a single backlog file.
- [ ] Assign owners for metadata, attachments, `ACCEPTANCE.md` upkeep, and TFM coverage.
- [ ] Unify role names and file names.
- [ ] Define artifact paths and the order of the first assignments.
- [ ] Run `git init` (or define a serial integration order) before running agents concurrently.
- [ ] Decide the §6 items, and record them in `AGENTS.md`.
