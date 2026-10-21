# Repo Instructions

## Working model

Use one agent for ordered repository work. Do not simulate Product Manager, Technical Lead, implementer, and QA handoffs or create separate reports for those personas.

Repository workflows are skills. Skill `<name>` is the file `.agents/skills/<name>/SKILL.md`. When a rule below applies, read that file before starting the work, whether or not your tool lists the skill natively. Load each skill only when its rule applies:

- `pdf-task-workflow` (`.agents/skills/pdf-task-workflow/SKILL.md`): selecting, scoping, executing, or recording a work item, or assessing release readiness.
- The one primary domain skill that matches the change: `pdf-core-change` (`.agents/skills/pdf-core-change/SKILL.md`), `pdf-acroform-change` (`.agents/skills/pdf-acroform-change/SKILL.md`), or `pdf-signing-change` (`.agents/skills/pdf-signing-change/SKILL.md`). Load another only when the task crosses that boundary.
- `pdf-change-verification` (`.agents/skills/pdf-change-verification/SKILL.md`): after runtime, test, build, or behavior-documentation changes, and for acceptance or qualification claims.
- `pdf-fuzz-qualification` (`.agents/skills/pdf-fuzz-qualification/SKILL.md`): the fuzz harness, the smoke run, a G8 fuzz campaign, or triage of a fuzz finding.
- `pdf-security-review` (`.agents/skills/pdf-security-review/SKILL.md`): only for an explicitly requested read-only audit or `QUALIFICATION.md` gate G8 evidence.

`.agents/skills/` is the only copy. Do not mirror it into `.claude/skills/`, `.cursor/skills/`, or `.grok/skills/`. Cursor scans `.claude/skills/` as well as `.agents/skills/`, so a mirror loads every skill twice.

Read only the state and supporting documents needed for the task. Keep work single-agent unless the user explicitly requests delegation. A fresh human or agent context may still be required when an evidence claim genuinely depends on reviewer independence; changing persona in one context does not provide that independence.

Sources, in order of authority:

1. `AGENTS.md`: binding constraints and the charter.
2. `docs/spec/`: scope and target behavior.
3. `ACCEPTANCE.md`: shipped behavior that the tests enforce.
4. `README.md`: the public contract for shipped behavior.
5. `QUALIFICATION.md`: release evidence.
6. Work items, decisions, evidence, and other records under `agents/`.

A spec requirement marked New or Change is target behavior. It is not a disagreement with `ACCEPTANCE.md` or `README.md`, and it must not be advertised as shipped. The work item that implements it, in the same commit, changes its status to Exists, updates `ACCEPTANCE.md` and the shipped-behavior sections of `README.md`, and adds the required evidence. From then on, a contradiction among the implementation, `ACCEPTANCE.md`, `README.md`, and that requirement is a disagreement. A spec that contradicts `AGENTS.md` is always a disagreement. When sources disagree, stop the affected work and report the disagreement to the owner. Do not choose between them yourself.

`AGENTS.md`, `docs/spec/`, `QUALIFICATION.md`, and `.agents/skills/` change only with the owner's approval. Propose an edit to them instead of making it.  Exception (D-43): the work item that ships a requirement marked New or Change sets its status to Exists in the same commit, together with `ACCEPTANCE.md` and `README.md`, without separate approval. Changing a requirement's text, adding or removing a requirement, or moving one between phases still needs the owner.

Agents commit task work without being asked each time (D-31). A commit holds one work item's changes and starts its subject with that item ID. Intermediate commits are allowed. A second item does not become active until the first item's tracked changes are committed. A task commit runs the verification its item requires. `make docker-builder docker-test` gates integration checkpoints, not every task commit. This does not authorize pushing.

Work items are stable `agents/work-items/W-NN.md` files under D-33. Their front matter owns lifecycle status and exact or directory-prefix `allowed_changes`; status changes never move files. `agents/state/current.md` is the only maintained ordering file, and `make agents-status` derives the overview. Decisions are separate records in `agents/decisions/`. Run `make agents-check` before task work and `make task-check` against the staged transaction before committing. One commit may activate an item, carry its work, and complete it (D-41, D-42): `task-check` reads the staged index and checks every staged path other than work-item records and `agents/state/current.md` against the `allowed_changes` of the one item the transaction activates or completes, or else the item still active. At most one item is `active`; an externally blocked item may become `waiting` only at a committed, verified safe checkpoint.

## Product charter

A permissively licensed, managed .NET library for common business PDF operations, built to stay sparse on gigabyte files and on documents with thousands of pages. It is not a general PDF toolkit and does not compete with qpdf, iText, PDFsharp, or Aspose. There is no layout engine, fluent document model, or renderer.

Scope is defined in `docs/spec/` (D-38):

- **Phase 1**, `docs/spec/phase-1.md`, release 1.0: reading a PDF and appending incremental updates. Forms (read, fill, bulk fill, export, flatten), page rotation, document information and XMP, document-level attachments, password-encrypted files and their permissions, signature inspection, revision listing, and PKCS#11 signing with RFC 3161 timestamps.
- **Phase 2**, `docs/spec/phase-2.md`, release 1.x: writing new files. Page extraction, concatenation, compaction, removing revisions, and removing passwords. No Phase 2 work starts until the owner adopts its spec.

Tenets, in priority order when they conflict: not a general toolkit; usable; production-ready; according to the spec (ISO 32000-1, ISO 32000-2 where real files use it, ETSI EN 319 142-1 for PAdES); compatible with real-world PDFs; maintainable.

Never in scope: rendering, text extraction, stamping and watermarks, layout or creating a PDF from scratch, rewriting a file in place, redaction, signature validation (trust, revocation, or cryptographic verification), PDF/A conversion or PDF/A conformance claims, XFA editing, JavaScript, annotation editing beyond what flatten requires, page-level file attachment annotations, public-key encryption, and adding encryption to a file. Caller-supplied fonts and PAdES long-term validation are planned for 1.x, not Phase 1.

The runtime dependency set is closed and settled: `System.Memory` (D-16) and `Microsoft.Bcl.AsyncInterfaces` with the `System.Threading.Tasks.Extensions` package it brings (D-39), on `netstandard2.0` only, so public methods can take `ReadOnlySpan<byte>` and use `ValueTask` and `IAsyncDisposable`. Other targets have no runtime package dependencies. The owner accepted these. They are not findings, gaps, or precedents, so do not flag them, propose removing them, or cite them to justify another package. Any other runtime package is an owner decision. The parser, CMS encoder, and PDF encryption stay in this library.

Tests may use external tools. `ForeignPdfTests` and `SigningInteropTests` require qpdf (`JAYTWO_QPDF` or `PATH`) and Python with pyHanko and pypdf. `AppearanceRenderTests` requires Poppler (`JAYTWO_POPPLER` or `pdftotext` on `PATH`). `SigningInteropTests` also requires SoftHSM 2 (`JAYTWO_PKCS11` pointing at `softhsm2-x64.dll` on Windows or `libsofthsm2.so` on Linux, with `softhsm2-util` in the sibling `bin` directory or on `PATH`). `OpenSslTimestampTests` and `UsageRightsTests` require OpenSSL 3 (`JAYTWO_OPENSSL` or `openssl` on `PATH`) as a local RFC 3161 authority and token verifier. Those tests fail when a tool is missing. Those tools are not runtime dependencies.

Large-file behavior is part of the design. Memory follows the working set of the operation. Do not buffer a PDF because a streaming decoder is theoretically possible. The 32 MiB and 64 MiB caps are decompression limits. The 8 MiB signature appendix stays until a real revision needs more.

- Public API lives in namespace `jaytwo.Ergonomics.Pdf` and its child namespaces for AcroForm, metadata, attachments, and signatures. The parser, serializer, and engine stay `internal` in `jaytwo.Ergonomics.Pdf.Internal`. Open a readable seekable `Stream`. Do not add an API that takes or returns a `byte[]` of the whole PDF. Offsets are `long`.
- Phase 1 writes are incremental classic-xref appends. The original bytes stay a prefix. Same-stream saves append. Cross-stream saves copy with a 1 MiB buffer into an empty destination, then append. Phase 2's new-file writer is the only other write path; it copies streams without decoding them and follows `docs/spec/phase-2.md`.
- Read PDF 1.0 through PDF 1.7, plus the PDF 2.0 structures the Phase 1 spec lists. An incremental save keeps the original header. It may raise the catalog `/Version`, never above `/1.7`. Writing `/AF` and `/AFRelationship` is a documented PDF 1.x compatibility extension, not a reason to raise the version (spec §7.5).
- Read classic xref (CRLF, LF, SP LF, and SP CR), xref streams with Flate and PNG predictors, hybrid `/XRefStm`, and object streams. Decode `LZWDecode`, `FlateDecode`, `RunLengthDecode`, `ASCIIHexDecode`, and `ASCII85Decode` when a stream's bytes are required, including the abbreviations `LZW`, `Fl`, `RL`, `AHx`, and `A85`. Write classic xref only. Do not allocate an array of `/Size`.
- Reading is strict to ISO 32000. Tolerated writer quirks are a closed list, each with a real-world fixture, a test, and a `README.md` entry (spec §8). Never scan the whole file to repair a cross-reference.
- Do not decode page content streams. Flatten adds content streams to a page; it does not read or rewrite the existing ones.
- Password-based standard security opens with the password passed to `Open`. Revision 2 is 40-bit RC4. Revision 3 is RC4 at 40 to 128 bits. Revision 4 is AES-128 or RC4, chosen by the crypt filter. Revision 5 and revision 6 are AES-256. Decrypt strings and streams on read. Encrypt them on write with that same cipher. Keep `/Encrypt` and `/ID` on the new trailer. Leave signature dictionaries plaintext. Decode a cross-reference stream before resolving `/Encrypt`. An encrypted xref stream is rejected. Refuse public-key encryption. A null password tries the empty user password, which opens a restrictions-only file. A user password honors the permission bits for every write; the owner password does not. An unencrypted document allows every operation.
- Generated appearances cover WinAnsi text in Helvetica, using Adobe Helvetica advances. A value the library cannot draw sets `/NeedAppearances true`; signing and flattening refuse while it is set. Password and signature fields cannot be filled, and a refused fill does not dirty the document.
- DocMDP `P` 1 blocks every write and signing. `P` 2 and `P` 3 allow form fill and signing; every other write is blocked at every certification level. A certification that does not resolve is treated as `P` 1. FieldMDP applies to fills and flatten. On a document with an approval signature, flatten and rotate require the caller's explicit opt-in, and signed signature fields are never flattened. Usage rights (`/UR3`) are kept when they provably permit the write. Otherwise they are removed in the same revision, except on a document that already has a signature, where they are left in place (spec §7.3). Reads stay available. Incremental saves keep existing signature bytes as a prefix.
- PKCS#11 stays in the host through `IPdfExternalSigner`. Do not P/Invoke PKCS#11 from this library. The host receives the raw digest and returns the raw signature value; the library builds the CMS. An RFC 3161 token from an `ITimestampAuthority` is stored as a CMS unsigned attribute over the signature value; a document timestamp is a separate revision. A failed sign or timestamp never extends the output, and exceptions from host code are not passed through with their details.
- Tests may reference packages. StyleCop warnings are errors. XML docs on the public API. One top-level type per file. File-scoped namespaces. Private fields use an underscore prefix.
- The type is not thread-safe. `Dispose` does not save.

## CI

- Jenkins builds the `builder` stage of the `Dockerfile` on Ubuntu and runs `make unit-test` there on net10.0, net8.0, and net6.0. net48 tests run on Windows only. Before you commit a checkpoint, run `make docker-builder docker-test`. It reproduces the Jenkins run locally and must pass.
- The Linux image installs every tool the tests require. A change that adds a test needing a new external tool also adds that tool to the `base` stage of the `Dockerfile`.
- Tests find tools through `ExternalTool.Locate` and `ExternalTool.Executable`. Do not hard-code `.exe` names, `where.exe`, or absolute Windows paths. On Unix, `Environment.SetEnvironmentVariable` does not change the native environment that an in-process native library reads.

## Code style

- Match the sibling repos (`jaytwo.Ergonomics.Images`, `jaytwo.Ergonomics.S3`, `jaytwo.AdoTools`, `jaytwo.Ergonomics.Logging`): `src/` and `test/`, shared `Directory.Build.props`, xUnit, Jenkins `nugetProject.build`.
- Implementation types stay `internal`. `InternalsVisibleTo` the test assembly.
- Prefer fixing member order. `SA1201`, `SA1202`, and `SA1204` are suppressed with a file pragma only where the parser, serializer, decoder, object model, and engine interleave static helpers with the operations they serve.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
