# Feedback on `agent-role-assignments-review.md`

Reviewed 2026-10-18 against the role files, `AGENTS.md`, and the current tree.

## Keep

This is the review to follow for how the team should operate.

The dependency finding is right, and the `AGENTS.md` line cites check out: zero runtime dependencies is a constraint (line 9), with `System.Memory` on `netstandard2.0` as the only exception (line 21). The same is true of the metadata/attachments gap, the NeedAppearances mismatch, the top-left coordinate expansion, and the PAdES / RSA-PSS scope leak.

The linear contract in finding 3 is the most usable process proposal either review makes. One task, one implementer, QA sign-off separate from technical-lead acceptance, product manager accepts the outcome, human owner only for charter changes and a release made against withheld QA. That is the deadlock fix.

The report shape in finding 8 is also right: facts with file and test references, assumptions, defects, missing evidence, affected files, verification, decisions, handoff. Use it.

Do not add a seventh role. The review offers a Document Features Engineer as the cleaner option and Core as the workable one. Core is the better assignment. Metadata and attachments are small, structural, and already beside the engine. The review's own conclusion is that six overlapping charters are too much process. A new seat adds another handoff for two features that do not have their own fault line in the code.

## Correct before this review is used as an edit list

### The ownership matrix makes the technical lead accountable for everything

Every implementation row lists the technical lead as Accountable and a specialist as Implementation owner. The column does not decide a conflict. Drop it, or reserve it for the few areas where the technical lead really is the implementer (`PdfEngine.cs` integration, public API shape).

The DocMDP / FieldMDP row is the one that will cause wrong work. It says the signing engineer owns policy, Core supplies parsing, and AcroForm enforces fills. `AGENTS.md` already states the policy: `P` 1 blocks, `P` 2 and `P` 3 allow fills and `Sign`, metadata and attachments stay blocked, an unresolved certification is `P` 1, and FieldMDP `All` / `Include` / `Exclude` apply to fills. "Owns policy" will be read as permission to redesign that. The row should say the signing engineer owns the gate as specified, Core owns the byte-level read of the dictionaries, and AcroForm calls the gate before a fill.

### SoftHSM is required test infrastructure, and it already has a home

Finding 5 correctly says to keep `SignHash` as raw SHA-256 with RSA PKCS#1 v1.5. It does not say where PKCS#11 is allowed to appear. `AGENTS.md` line 9 requires `SigningInteropTests` to use SoftHSM 2, and those tests fail when the tool is missing. That adapter is not a library type. It is `Pkcs11Signer` inside `test/jaytwo.Ergonomics.Pdf.Tests/SigningInteropTests.cs`, which wraps the digest in DigestInfo and signs with `CKM_RSA_PKCS`.

An edit pass driven only by this review can delete the SoftHSM sections from the signing and QA roles. Those sections should be rewritten to point at the test project, not removed.

### Several charter conflicts are absent

The other review's conflict table should be merged in. This review does not mention:

- The core role's suggestion to write cross-reference streams. The charter says classic xref only.
- The core role telling that engineer to remove the 8 MiB signature appendix. The charter keeps the cap until a real revision needs more.
- The signing role's staged-output language. Failed signs leave the destination length unchanged. The host swaps files after `Sign` returns.
- QA scenarios the API cannot produce: non-seekable destinations, `CancellationToken`, a nonce the request never sends, and an untrusted-TSA test that only passes if the library grows a trust validator.

### The one role instruction that points at a real defect is missed

`HttpTimestampAuthority.ExtractToken` checks that the PKI status is 0 or 1 and that a ContentInfo follows. It does not compare the token's message imprint with the request. `CmsDetachedSignatureProvider` stores whatever token comes back. The signing role's warning about accepting an arbitrary ASN.1 blob is a fair implementation task. Trust in the TSA certificate stays out of scope, with signature validation. This review treats that whole area as scope creep. Split it the way the other review does.

### Worktrees do not isolate this tree yet

The concurrency protocol assumes a branch or worktree contains the library. The repository exists, on branch `wip`, and `HEAD` (`b832f22`, "Initial commit") contains only `LICENSE`. `src/`, `test/`, and `agents/` are untracked. A second worktree of `HEAD` does not contain `PdfEngine.cs`. Commit the tree before the technical lead starts assigning file owners and integration order. Until then, every agent shares one working tree, which is the collision the protocol is trying to prevent.

### "139 tests" is safely left unverified

This review tells the roles to delete the historical count and does not assert that 139 is still the number. That is the right caution. The current suite is 107 `[Fact]` methods, 9 `[Theory]` methods, and 38 `[InlineData]` rows. The six facts beyond a 101-fact count are `AppearanceRenderTests`, `SigningInteropTests` (four), and `DenseMemoryTests`. Those files are the rendered-appearance, SoftHSM, local-TSA, and dense-memory checks the role prompts still describe as open. Any baseline the technical lead writes should start from those files, not from the "139 tests" paragraph.

## Smaller notes

"Five parallel baseline reports" undercounts the roles. All six open with a whole-repo assessment. The recommended split still holds: technical lead writes the technical baseline, QA writes the evidence inventory, specialists stay inside their subsystem.

House style never appears: StyleCop warnings as errors, XML docs, one type per file, file-scoped namespaces, underscore fields, and the SA1201/1202/1204 pragma limited to the parser, serializer, decoder, object model, and engine. The 120–200 line prompt shape should point at `AGENTS.md` for that, or agents briefing from the shortened role alone will fail the build.

The suggested prompt outline (mission, owns, does not own, constraints, authority, inputs, outputs, handoffs, done, first assignment) is the right rewrite target. Put the ownership matrix and the seven-step contract in the shared page, not in six copies.
