# Kickoff prep

## 1. Task and owner

Read-only preparation for the Technical Lead. Not the planning report. No code, board, or status edits. B-04 is not boarded.

## 2. Verified facts

Commands: `git status --porcelain=v1`, `git branch -vv`, `git rev-parse`, `git ls-tree --name-only`, `git log --oneline -5`. The suite was not run.

**Repository section of `agents/state/status.md` disagrees with the tree.** Trust the tree.

- Current branch is `wip`. `HEAD` is `11da851` (`11da851c8e3b9c2d9ce9071078b607b117715047`), message `genesis`, tracking `origin/wip`.
- `git ls-tree --name-only HEAD` includes `src`, `test`, `agents`, and the root docs. `git status --porcelain=v1` is empty.
- `main` and `develop` are `b832f22` (`b832f22dcf95c13812f1b3641d5ec0ab0ab83922`), message `Initial commit`. That commit’s tree is `LICENSE` only.
- Status says `wip` `HEAD` is `b832f22`, that `main` points at the same commit, and that `src/`, `test/`, `agents/`, and the root docs are untracked. Those sentences are false for current `wip`.
- B-01’s acceptance check (`git ls-tree HEAD` lists `src/`, `test/`, and `agents/`) passes on `wip`. Closing B-01 is the Product Manager’s.

**Code map and technical baseline match.**

- `Internal/PdfEngine.cs` is 3650 lines. Status says about 3,650.
- Test inventory, from `test/jaytwo.Ergonomics.Pdf.Tests/*.cs` only: 107 `[Fact]`, 9 `[Theory]`, 38 `[InlineData]`. No `[MemberData]` or `[ClassData]`. This is a count, not a result.
- Library targets in `jaytwo.Ergonomics.Pdf.csproj`: `net8.0`, `net6.0`, `netstandard2.1`, `netstandard2.0`, with `System.Memory` only on `netstandard2.0`. Test targets: `net8.0` and `net6.0`. Test packages: `Pkcs11Interop`, and `System.Security.Cryptography.Pkcs` on `net8.0`.
- Listed area paths exist. `Internal/IsExternalInit.cs` is an unlisted `netstandard` polyfill. The map does not claim to name every file.
- The known gap is still true. `Rfc3161TimeStampRequest.Create` emits version 1 plus a SHA-256 imprint and nothing else (`Signatures/Rfc3161TimeStampRequest.cs`). `HttpTimestampAuthority.ExtractToken` accepts PKI status 0 or 1 and returns the following sequence. `CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider` both call `Create` and do not compare imprint, digest algorithm, or nonce. `DocTimeStampSignatureProvider` also returns the token with no empty-token check.

**B-04 file boundary, for the later board. Not a claim yet.**

Signing Engineer may change:

- `src/jaytwo.Ergonomics.Pdf/Signatures/Rfc3161TimeStampRequest.cs`
- `src/jaytwo.Ergonomics.Pdf/Signatures/CmsDetachedSignatureProvider.cs`
- `src/jaytwo.Ergonomics.Pdf/Signatures/DocTimeStampSignatureProvider.cs`
- `src/jaytwo.Ergonomics.Pdf/Signatures/DerCodec.cs` (`Integer(long)` cannot encode a nonce whose high bit is set)
- one new internal type under `Signatures/` if the two providers would otherwise duplicate the token check
- `test/jaytwo.Ergonomics.Pdf.Tests/SignatureTests.cs`
- `README.md` and `ACCEPTANCE.md`, in the same change

`SigningInteropTests.cs` stays off the claim unless the existing local-TSA test cannot pass without an edit.

Shared engine: no claim. `PdfEngine.Sign(Stream, ...)` calls `provider.CreateContents` at line 966 and writes the destination only at lines 981–992. The `catch` at 996 restores engine fields and rethrows. README says a throw leaves the destination length unchanged. A `PdfTimestampException` from `CreateContents` already meets “do not extend the file.” Do not edit `Internal/PdfEngine.cs`. If that order is not enough, stop; the Core Engineer is consulted before any appendix change.

Do not change `ITimestampAuthority`. A signature change there is a breaking public API. Do not put the imprint, algorithm, and nonce check only in `HttpTimestampAuthority`; backlog B-04 requires it in both providers so every `ITimestampAuthority` is covered.

`test/jaytwo.Ergonomics.Pdf.Tests/Fixtures/tsa_server.py` is QA-owned. It passes the request to pyHanko `DummyTimeStamper.request_tsa_response`. Whether that token echoes a nonce was not checked. Claim this file only if the implementer finds it does not, and consult QA.

## 3. Assumptions

- A clean porcelain status plus `ls-tree` means `11da851` holds the library the working tree has. The owner has not been asked whether that commit closes B-01.
- Parallel reports were not read.

## 4. Defects confirmed

The B-04 gap is still in the code, as status describes it. No new defect beyond that and the stale repository section.

## 5. Evidence missing

- No suite run. QA owns B-02.
- Nonce echo from the local TSA was not established.

## 6. Changes made or proposed

This report only. Proposed later claim is the file list in section 2. No assignment this turn.

## 7. Verification run

Git commands above. Line and attribute counts via PowerShell `Get-Content` and `Select-String` on the test project. No `dotnet test`.

## 8. Decisions needed

The public-API choice is still open. The board records it before B-04 starts.

- Nonce overload: `Create(ReadOnlySpan<byte>)` keeps its current bytes. SHA-256("abc") stays the 56-byte vector in `SignatureTests.rfc3161_request_matches_the_sha256_abc_vector` (`KnownRequest`) and in `ACCEPTANCE.md`. That path is additive.
- Changing `Rfc3161TimeStampRequest.Create` so its output includes a nonce changes those bytes. That is a breaking public API change. The owner decides. This prep does not choose it.

Product Manager: B-01’s acceptance check passes on `wip`. This report does not close it.

## 9. Handoff

Technical Lead, after `agents/reports/2026-10-18-qa-engineer-B-02.md` and the three B-05 reports exist. Then write the planning report and update the board.
