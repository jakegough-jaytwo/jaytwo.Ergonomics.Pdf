# Feedback on `role-assignments-review.md`

Reviewed 2026-10-18 against the role files, `AGENTS.md`, `README.md`, `ACCEPTANCE.md`, and the current source.

## Keep

This is the review to follow for what the role text got wrong about the product.

The conflict table is the best artifact either review produced. It quotes the role, cites the line, and puts the charter sentence beside it. The rows that the other review missed are real: classic-xref writes, the 8 MiB appendix, staged output versus "failed signs do not write," non-seekable destinations, cancellation, and the nonce test. The illustrative `PdfFormField` sketch is correctly flagged, including `object? Value` against the existing `string?` `PdfField.Value`.

The imprint-versus-trust split is the sharpest technical call in either review, and the code matches it. `HttpTimestampAuthority.ExtractToken` rejects PKI status greater than 1 and requires a ContentInfo. It does not compare the token's message imprint, digest algorithm, or (vacuously) nonce with `Rfc3161TimeStampRequest`. Checking status, imprint, and digest algorithm on the way into a signature is in scope. Validating trust in the TSA certificate is the signature-validation feature `AGENTS.md` excludes.

The `SignHash` pitfall is stated correctly, and the test adapter already implements it. `Pkcs11Signer.SignHash` in `SigningInteropTests` builds a DigestInfo for SHA-256 and signs with `CKM_RSA_PKCS`. `CKM_SHA256_RSA_PKCS` would hash the raw digest again. The role edit should cite that class. It should not describe the adapter as something still to be placed.

Per-role notes and the edit checklist are specific enough to apply. Charter precedence as one shared sentence is the right first edit. Assign metadata and attachments to the core engineer. That recommendation is better than a seventh role.

## Correct before this review is used as an edit list

### The git claim is wrong

Section 4 says the working directory is not a git repository, so branches and worktrees are unavailable, and the fix is `git init`.

`git rev-parse --is-inside-work-tree` is true. The current branch is `wip`. `HEAD` is `b832f22`, "Initial commit", and that commit contains only `LICENSE`. `main` and `develop` point at the same commit. `src/`, `test/`, and `agents/` are untracked.

Branches and worktrees work. A worktree of `HEAD` does not contain the library, and switching branches in this directory does not isolate untracked files. The fix is to commit the tree, then use the technical lead's integration order. Do not initialize a second repository.

### The test count does not still hold, and it hides the new evidence

Section 2 says the "139 tests" figure still holds: 101 `[Fact]` methods plus 38 `[InlineData]` rows.

The tree now has 107 `[Fact]` methods, 9 `[Theory]` methods, and 38 `[InlineData]` rows. The six facts missing from that 101 are:

| File | Facts | What it already covers |
|---|---|---|
| `AppearanceRenderTests.cs` | 1 | Poppler text and raster check of generated text, choice, checkbox, and radio appearances |
| `SigningInteropTests.cs` | 4 | pyHanko trust of an embedded chain, pyHanko trust of an RFC 3161 token from a local TSA, and a SoftHSM `IPdfExternalSigner` |
| `DenseMemoryTests.cs` | 1 | Peak managed memory on dense files, including a qpdf object-stream rewrite |

Section 2 then says certificate-chain embedding and CMS timestamps are implemented and that what remains is independent validation against a real TSA. `SigningInteropTests` is that validation, using pyHanko and `Fixtures/tsa_server.py`, plus the SoftHSM path `AGENTS.md` requires. Rendered appearances and a dense-memory measurement are the other two gaps the role prompts still list as open, and both now have tests.

I did not re-run the suite. The claim to correct is the inventory: those files exist, so "139 still holds" and "independent validation has not been done" should not survive into the backlog the product manager writes. A skipped run because Poppler, qpdf, or SoftHSM is absent is a not-run, which the QA role already says must not be reported as a pass.

### Two items in section 6 are already decided

The section asks to settle five topics in `AGENTS.md`. Three of them are genuine owner decisions or missing sentences:

- Top-left UI coordinates. The charter keeps bottom-left user space, rotation, and the media box. Adding a viewport coordinate is a product change.
- TSA response checks. Imprint, status, and digest algorithm are a fair library check. Nonce and `certReq` are not sent today (`Rfc3161TimeStampRequest` says so). Trust stays out. Write that down once.
- What "production-qualified" requires, in validators and fixtures, so the fourth evidence level has a bar.

The other two should not be reopened. Classic-only xref output, the 8 MiB appendix, and "no runtime package except `System.Memory`" are already in `AGENTS.md`. The edit is to delete the role sentences that disagree. A second decision invites the agents to treat them as open.

### The TFM note recommends a target the library does not have

Tests run on `net8.0` and `net6.0` only. The library also builds `netstandard2.1` and `netstandard2.0`, so the `System.Memory` compile is not exercised by the test assembly. That is a fair hole for the QA role.

The suggested "`net4x` / `netstandard` test leg" does not fit. Nothing in the charter targets .NET Framework, and `netstandard` is not a runnable TFM. .NET 6 is out of support, and .NET 8 support ends on 10 November 2026. That is a product-owner decision about the next TFM. It is not a defect in these role files, and it should not be added to a specialist prompt as standing work.

### "Five overlapping audits" undercounts

All six roles start with a whole-repo assessment. The proposed order is still right: product manager and technical lead first, then specialists against that backlog, with QA's evidence inventory as the shared baseline.

## Smaller notes

The six role files are 3,280 lines (306 + 490 + 528 + 674 + 528 + 754). The review says 3,274. `PdfEngine.cs` is 3,651 lines. Neither difference changes the recommendation. The shared `_team.md` plus a 100–200 line role delta is the right compression, and it matches the other review's prompt outline. Put the conflict-table fixes into that shared page so they are not copied six times and drifted again.

Section 3 is right that no role owns updates to `ACCEPTANCE.md`. Give that to the technical lead in the same change as the behavior, with the product manager checking that a criterion was not quietly weakened.

The closing checklist is safe to execute after the git line, the test-count line, and the "decide the 8 MiB cap again" line are struck. Charter precedence, dependency wording, the signing-role scope cut, the AcroForm API sketch, the impossible QA scenarios, role names, and artifact paths can be applied as written.
