# Decisions

Owner: Product Manager. The human owner decides. Decided items are closed. Agents do not reopen them. See `team.md` section 1.

## Decided

### D-01. Durable roles are separate from project state

Decided 2026-10-18 by the owner.

Role files and `team.md` describe seats and the workflow. Status, baselines, gaps, priorities, test counts, and tool versions live in `agents/state/`. The original role files, kept in `agents/archive/roles-v1/`, mixed the two. All three reviews found that agents would treat that embedded history as their work list.

### D-02. Team shape and names

Decided 2026-10-18. Six seats: Product Manager, Technical Lead, Core Engineer, AcroForm Engineer, Signing Engineer, QA Engineer. Filenames match the names. `project-manager.md` became `product-manager.md`, and `cryptography-engineer.md` became `signing-engineer.md`.

### D-03. Metadata and attachments belong to the Core Engineer

Decided 2026-10-18. No seventh seat for now. The surface is small, and almost every change there also passes through the engine, encryption, and the certification gate. `team.md` section 12 gives the trigger for splitting out a Document Features Engineer later.

### D-04. Precedence

Decided 2026-10-18. `AGENTS.md` binds. `ACCEPTANCE.md` is required behavior. `README.md` is the public contract. Then `team.md`, the role, and the task. When two disagree, the agent stops and reports. Recorded in `team.md` section 1.

### D-05. One owner per area, with a single workflow

Decided 2026-10-18. The ownership table and the six-step workflow in `team.md` replace the per-role "coordinate with" lists. The Signing Engineer owns the DocMDP and FieldMDP gate end to end. The other seats consult. The Technical Lead is not listed as accountable for every area.

### D-06. Shared files are claimed per task, not reserved for one seat

Decided 2026-10-18. The engine is not Technical-Lead-only. A task claims a region on the board. The owning specialist edits it. The Technical Lead reviews every change to a shared integration file. Reviewers split on this. Reserving the file would have made the Technical Lead a bottleneck.

### D-07. QA does not change `src/` by default

Decided 2026-10-18. QA writes tests, fixtures, and harness code. A testability change under `src/` is assigned by the Technical Lead and verified by someone other than QA. QA never validates its own production change.

### D-08. Who updates the docs

Decided 2026-10-18. Whoever changes behavior updates `README.md` and `ACCEPTANCE.md` in the same change. The Technical Lead reviews. The Product Manager checks that no criterion was weakened. `AGENTS.md` and the durable team files change only with the owner.

### D-09. PKCS#11 lives only in the test project

Decided 2026-10-18, restating `AGENTS.md`. The library never calls PKCS#11. SoftHSM and `Pkcs11Interop` are required test infrastructure in the test project. Two early reviews recommended deleting SoftHSM testing. The charter and the later reviews overrule that.

### D-10. Three kinds of checking

Decided 2026-10-18. Input checks during signature creation are in scope, such as matching a timestamp response to its request. Independent verification by external tools is QA evidence. Signature validation as a library feature stays excluded. Recorded in `roles/signing-engineer.md`.

### D-11. Kickoff order

Decided 2026-10-18. Read-only work runs in parallel, one report per seat, in the `team.md` format. No seat edits files until the Technical Lead publishes a board. Specialists audit only their own area against `ACCEPTANCE.md`. They do not write a product backlog. The plan is in `state/board.md`.

### D-12. Version control

Decided 2026-10-18. Commit the existing tree to the existing repository. Do not run `git init`. One review recommended it before the repository existed.

### D-13. Target frameworks stay as they are

Decided 2026-10-18 by the owner. Supporting `netstandard2.1`, `netstandard2.0`, `net6.0`, and `net8.0` is intentional. A target is dropped only for a compatibility reason, not because its runtime left vendor support. Do not propose removing a target on support-lifecycle grounds.

Still open as a test question, not a target question: no test runs the `netstandard2.0` build, the only one that uses `System.Memory`. QA raises it in the gap analysis, backlog item B-06, under gate G10.

### D-14. Timestamp requests carry a nonce

Decided 2026-10-18 by the owner. Each RFC 3161 request carries a random nonce. The response must echo it, or the token is rejected. `certReq` stays omitted until a verifier needs the TSA certificate in the token. This is part of backlog item B-04.

### D-15. `QUALIFICATION.md` is the release bar

Decided 2026-10-18 by the owner. The owner supplied the Production Qualification Standard for release 1.0. It is in the repository root, and `team.md` places it in the precedence order after `README.md`.

On adoption, five passages were reworded so the standard agrees with `AGENTS.md`. The owner should confirm them:

1. **G4.** Trusted validation is done "by the independent verifier." The library does not validate trust.
2. **G5.** The library rejects malformed responses, wrong imprints, and mismatched nonces. The independent verifier rejects untrusted timestamps. The library does not validate TSA trust.
3. **G5.** A note was added that `AGENTS.md` currently excludes PAdES-LTV, which covers B-LT and B-LTA.
4. **G10.** The dependency line now cites the `AGENTS.md` rule, no runtime package except `System.Memory` on `netstandard2.0`. It replaces "the self-contained PDF implementation policy," which was the looser wording the reviews flagged in the original roles.
5. **G10.** "Zero NuGet PackageReference entries" was removed from the list of non-requirements. With `System.Memory` in place it was literally true, but it read as loosening the charter.

The thresholds in the standard are proposals: the fixture count, producer count, memory budgets, and fuzzing hours. The Technical Lead and QA Engineer record the approved fixture-specific memory budgets under gate G7 in `state/status.md`.

### D-17. Password and signature fills throw

Decided 2026-10-18 by the owner, closing D-O5. Option 2. Password and signature fills throw `PdfFieldValueException` and do not dirty the document. Comb, rich text, and non-WinAnsi values still set `/NeedAppearances`.

The owner rejected option 1. A signature field's value is a signature dictionary, written by `Sign` with `FieldName`, not by a text fill. The password flag means the value is not stored in the file. `SetText` already throws for a password field and for a field that is not text, and `ACCEPTANCE.md` already requires that throw. `AGENTS.md` is updated to match. The README appearances bullet gets the same sentence when B-04 releases its claim on `README.md`.

### D-16. `System.Memory` is settled

Decided 2026-10-18 by the owner. `System.Memory` on `netstandard2.0` is an accepted runtime dependency, not an exception under review. Every review round raised it, because `AGENTS.md` said "zero runtime dependencies" and the README said "no runtime dependencies" while the project file references a package. Both now describe a closed, settled set. Agents do not flag the package, propose removing it, or cite it as precedent for another one. A different runtime package is still an owner decision under `team.md` section 9.

### D-18. OpenSSL is a test-only timestamp authority and verifier

Decided 2026-10-18 by the owner, for B-15. OpenSSL is approved as a test-only external tool. Git for Windows ships OpenSSL 3.2.4 at `/mingw64/bin/openssl`. Tests use `openssl ts` as a local RFC 3161 authority, and `openssl ts -verify` as a verifier where it applies. They find it through `JAYTWO_OPENSSL` or `PATH`, and fail when it is missing, like the other required tools. It is never a runtime dependency. The Technical Lead drafts the one-line addition to the tool list in `AGENTS.md`, and that edit is approved.

### D-19. A `net48` test target is approved

Decided 2026-10-18 by the owner. The test project gains a `net48` target. The library targets do not change (D-13). The purpose is to cover the B-31 .NET Framework abort-by-dispose path and the G10 gap: no test runs the `netstandard2.0` build on .NET Framework. Tests that need a tool or an API the target lacks may be excluded on `net48`. Each exclusion is listed in the task report.

### D-20. G7 working memory budget

Decided 2026-10-18 by the owner. The starting budget in `QUALIFICATION.md` section 9 is the working budget: at most 256 MiB of peak managed-heap growth for inspect, edit, and sign, and at most 512 MiB for complex structural traversals. B-17 may be boarded against it. This approves the working budget only. It does not by itself meet G7: every G7 acceptance criterion still needs its own evidence.

### D-21. Timestamp requests set certReq (revises D-14, closes D-O6)

Decided 2026-10-18 by the owner. Each RFC 3161 request sets `certReq` true by default. Under RFC 3161, as updated by RFC 5816, the TSA must then include the signing certificate that `ESSCertID` or `ESSCertIDv2` identifies. This makes the token self-contained for independent verifiers. It is an RFC-supported behavior, not a pyHanko workaround. The nonce rule in D-14 stands. No public option to omit the certificate unless a use case is demonstrated. Required: the request sets the flag explicitly, the library checks that the response includes the expected TSA signing certificate, a regression test has pyHanko validate the token with only the trust anchor configured, and the signature placeholder sizing fits the larger token. Certificate inclusion, cryptographic validity, and trust stay distinct. Including the certificate does not establish trust, and the chain is not guaranteed.

### D-22. Test infrastructure and small fixes do not need the owner

Decided 2026-10-18 by the owner. Installing test-only tools, generating or obtaining fixtures, and small in-charter bug fixes are coordinated by the Product Manager and the Technical Lead. They are not owner decisions. This round, that covers B-34, B-13 (independent PDF producers, preferably LibreOffice and/or PDFBox; separate independent producers from tools that only transform or sign), and B-14 (Poppler `pdfsig`, through a separate test environment or container if needed; PDFBox is an acceptable additional verifier when its limits are recorded). Escalate only genuine product decisions, material architectural changes, substantial runtime dependencies, significant cost, and changes to security guarantees.

### D-23. Fuzzing budget and an independent security audit

Decided 2026-10-18 by the owner, for B-18. Up to 24 aggregate CPU-hours of fuzzing on existing development or CI machines. Test-only fuzzing dependencies are allowed. No new paid cloud resources or uncontrolled compute spending. A separate Security Auditor agent, which has not implemented the affected code, reviews the parser, stream decoders, PDF encryption, CMS and DER encoding, signature construction, and the related failure paths. It reports to the Technical Lead and the Product Manager. An independent agent review is enough to begin G8 qualification work. A human review may be considered before final production deployment.

## Open

Each item needs an owner decision. The Product Manager keeps the recommendation current.

### D-O4. Caller-facing coordinates

The charter reports raw user space, the page boxes, and rotation. The original AcroForm role asked for top-left viewport coordinates and `/UserUnit` normalization.

Recommendation: keep the charter. Revisit only with a named consumer that needs it.
