# Review: agent role assignments

Reviewed 2026-10-18 against `AGENTS.md`, `README.md`, `ACCEPTANCE.md`, and the current public API.

Files reviewed:

| File | Title inside the file | Length |
|---|---|---|
| `agents/roles/project-manager.md` | Product Manager | 306 lines |
| `agents/roles/tech-lead.md` | Technical Lead | 490 lines |
| `agents/roles/core-engineer.md` | PDF Core Engineer | 528 lines |
| `agents/roles/acroform-engineer.md` | AcroForm Engineer | 674 lines |
| `agents/roles/cryptography-engineer.md` | Signing & Cryptography Engineer | 528 lines |
| `agents/roles/qa-engineer.md` | QA & Interoperability Engineer | 754 lines |

## Verdict

The six-way split matches the product. Product scope, technical integration, the PDF engine, AcroForm, signing, and independent verification are the right seats, and the shared evidence ladder (implemented, internally tested, independently validated, production-qualified) is the right way to talk about this library.

The set is not safe to hand to agents as written. Each prompt restates the charter in its own words, and several of those restatements contradict decisions already recorded in `AGENTS.md`, `README.md`, and `ACCEPTANCE.md`. An agent that follows its role file will reopen closed work: dependencies, `/NeedAppearances`, DocMDP, the PKCS#11 boundary, classic-xref output, and the public field shape.

There is also no precedence rule. These files are long enough to crowd out `AGENTS.md`. Whichever document is pasted as the system prompt will win.

## What holds

- Exclusions line up with the charter: no renderer, no stamping, no page-content editing, no XFA, no redaction, no general SDK.
- Incremental append, seekable streams, and “memory follows the working set” are consistent across every role.
- The private key stays in the host. The warning not to sign the raw PDF digest as if it were the CMS signed-attributes digest is the right footgun.
- CMS signature timestamps and `ETSI.RFC3161` document timestamps are kept distinct, which matches `CmsDetachedSignatureProvider` and `DocTimeStampSignatureProvider`.
- QA can withhold sign-off, and that authority is separate from the technical lead’s integration authority. Final release stays with the human owner.
- Specialists are told to inspect the repository, to avoid speculative rewrites, and to keep `PdfEngine.cs` from being edited by several agents at once.
- qpdf, pyHanko, and pypdf are the tools the suite already uses. Naming them is correct.

## Charter conflicts

These are the defects that will cause wrong work. `AGENTS.md` is the constraint. The role text below is what an agent will follow instead.

### 1. Zero runtime dependencies is softened in every role

`AGENTS.md` states that zero runtime dependencies is a constraint. The project file allows one package: `System.Memory` 4.5.5, and only on `netstandard2.0`.

Every role then says the library does not require literally zero NuGet dependencies, and that small utility packages are acceptable when they look useful. The product-manager prompt is the widest: it invites “small, well-maintained, permissively licensed utility packages.” The same product-manager prompt ends by asking for a “zero-third-party-runtime-dependency” toolkit. Those two sentences cannot both be instructions.

The `System.Memory` example is accurate. The generalization is not. A proposed runtime package is an owner decision, not an engineering preference.

### 2. `/NeedAppearances` is treated as a defect

`README.md` and `ACCEPTANCE.md` already define appearance policy:

- WinAnsi text and choice values get a Helvetica appearance, Adobe glyph widths, 1000 units per em, and `/NeedAppearances false`.
- A check box with an on-state reuses it. A check box with none gets a generated `Yes`/`Off` appearance. A single radio widget with no on-state gets an appearance named for the export value.
- Comb, rich text, non-WinAnsi, password, and signature values set `/NeedAppearances true`.
- `Sign` throws while that flag is true. Password and signature field values throw `PdfFieldValueException`.

The AcroForm role says not to rely on `/NeedAppearances` for supported fields, lists password and comb as behavior to implement, and tells the engineer to replace “approximate” character widths with correct metrics. The Adobe WinAnsi advances are the specified metric. Visual checks with an external renderer are still a real gap. Rewriting the appearance policy is not.

### 3. DocMDP and FieldMDP are already specified

The signing role says an earlier implementation rejected every modification when `/DocMDP` was present, and that this may be too strict for certifications that allow form fill. The current rules are in `README.md`:

- `P` 1 blocks fills, attachment edits, metadata writes, and `Sign`.
- `P` 2 and `P` 3 allow fills and `Sign`. Metadata and attachments stay blocked at every level.
- A certification that does not resolve is `P` 1.
- FieldMDP `All`, `Include`, and `Exclude` apply to fills. Reads stay available.

An agent following the role will treat a finished permission model as an open design problem.

### 4. PKCS#11 stays out of this library

`IPdfExternalSigner.SignHash` takes the raw 32-byte SHA-256 and must match `RSA.SignHash(hash, SHA256, Pkcs1)`. `CKM_SHA256_RSA_PKCS` would hash that digest again. The library does not P/Invoke a PKCS#11 module. `AdditionalCertificates` is already the chain beyond the leaf.

The signing role’s priority 4, and the QA role’s SoftHSM section, ask for a real PKCS#11 token and “the mechanism-specific semantics of the adapter.” There is no adapter in this repository. A test signer that calls `RSA.SignHash` on the raw digest is the contract. SoftHSM would test a host component that does not live here.

The technical-lead prompt also calls `IPdfExternalSigner` “or its successor abstraction,” which invites replacing a settled interface.

### 5. Several “investigate this” items are already acceptance criteria

The prompts freeze an older baseline into all six files: 139 tests, appearances not visually checked, certificate chains and CMS timestamps not validated, encrypted xref initialization recently corrected, clean object streams possibly still cached, an 8 MiB signature appendix that someone might remove.

`ACCEPTANCE.md` already requires the structural versions of several of those items:

- Additional certificates are embedded beside the leaf.
- An `ITimestampAuthority` receives an imprint over the RSA signature value, stored as unsigned attribute `1.2.840.113549.1.9.16.2.14`.
- Clean objects from an object stream are released when the public operation returns. A map that holds a dirty object stays.
- Encrypted xref streams are rejected. `/Encrypt` is resolved from the xref map without a whole-file scan.
- A signature appendix past the reserved gap throws `PdfSignatureTooLargeException` and does not extend the file. `AGENTS.md` keeps the 8 MiB cap until a real revision needs more. The caller saves first, then signs.
- Writes are classic xref only. Past offset 9,999,999,999, save throws `PdfTooLargeForClassicXrefException` and writes nothing.

A live TSA, a rendered appearance check, and a dense multi-gigabyte measurement are still open. The prompts do not separate those open gaps from behavior the suite already locks. The core role’s note that writing xref streams “may eventually be appropriate” conflicts with “write classic xref only.”

### 6. The public field model is one row per widget

`PdfField` is one widget: `FullName`, `WidgetIndex`, `Rect` in bottom-left user space, `PageNumber`, `PageRotation`, `MediaBox`. Radio widgets that share a name are separate rows. `GetFields` returns every widget with that name.

The AcroForm role’s `PdfFormField` / `PdfFieldWidget` sketch is marked illustrative, and it does say the current API may expose one result per widget. The sketch is still the only API picture in a 674-line prompt, and it will be what the agent “improves” toward.

The same role, and the QA role, require `/UserUnit` and coordinates normalized to a top-left UI. Neither appears in the charter, the README, or `ACCEPTANCE.md`. `PdfPage` reports media box, crop box, and rotation. That geometry work is a product change.

### 7. Encryption and the target frameworks are underspecified

The core role says “standard password security.” The charter is specific: revisions 2 through 6, RC4 and AES as chosen by the crypt filter, public-key encryption refused, null password meaning the empty user password, user-password permission bits, owner password unrestricted, signature dictionaries left plaintext. None of that is in the role, so a “simplification” can violate `ACCEPTANCE.md` without the agent noticing.

The prompts talk about .NET 6 and .NET 8. The library also targets `netstandard2.1` and `netstandard2.0`.

### 8. House style is absent

`AGENTS.md` requires StyleCop warnings as errors, XML docs on the public API, one top-level type per file, file-scoped namespaces, and an underscore prefix on private fields. Implementation types stay `internal`. `SA1201`, `SA1202`, and `SA1204` are suppressed only in the parser, serializer, decoder, object model, and engine. No role mentions any of this. Agents will either fail the build or “fix” member order in the files where that order is intentional.

The type is not thread-safe, and `Dispose` does not save. That is missing too.

## Naming

Peers do not use one name for the same seat.

| File | What this file calls itself | What other files call it |
|---|---|---|
| `project-manager.md` | Product Manager | PM |
| `cryptography-engineer.md` | Signing & Cryptography Engineer | Signing Engineer |
| `qa-engineer.md` | QA, Interoperability & Reliability Engineer, then QA & Interoperability Engineer | QA Engineer |

“Project manager” and “product manager” are different jobs. This document is product scope, acceptance, and a light backlog. The filename says project tracking. An orchestrator that matches on the filename will brief the wrong job.

Pick one name per file and use it in every other prompt. Filename, title, and cross-references should be the same string.

## Ownership gaps

Two of the five charter operations have no specialist:

- Document info and XMP live in `Metadata/PdfMetadata.cs` and `Metadata/XmpEditor.cs`.
- Embedded files live in `Attachments/`.

The product manager lists both. QA is told to test both. Nobody is told to own name trees, `/AF`, XMP in-place edits, malformed XMP, or the 64 KiB attachment copy. They will be absorbed into `PdfEngine.cs` by whoever touches them, or left as leftovers.

`PdfEngine.cs` is the real integration surface. AcroForm fill, metadata, attachments, encryption gates, DocMDP, and `Sign` all meet there. The technical lead is the only person who should edit it unless a task explicitly assigns a region. The prompts say “coordinate” and do not say who owns the file.

Nobody owns updates to `README.md`, `ACCEPTANCE.md`, and `AGENTS.md`. The quality gates say the public docs must stay consistent. The technical lead should own those edits when behavior changes, with the product manager checking that acceptance criteria did not quietly shrink.

QA “must not approve its own implementation as independently validated,” which implies QA might change product code. The role should say QA writes tests and fixtures only, and does not change `src/`.

## How the prompts will behave in a team

### Six copies of one briefing

Dependency policy, non-goals, the evidence ladder, collaboration rules, and the definition of done are pasted into every file. They have already drifted: the product-manager dependency section is longer and looser than the others, and the signing engineer’s name is not stable. The next charter edit will land in one file.

A shared constraints page that points at `AGENTS.md`, plus a short role page that contains only ownership, boundaries, and the current assignment, will stay aligned. The unique content of these files is a fraction of their length. QA at 754 lines is the clearest case.

### Six identical first assignments

Every role opens by inspecting the whole repository and writing an assessment: current state, risks, and a recommended backlog. The product manager and the technical lead both produce assignments. The four specialists produce overlapping assessments of the same baseline.

Launched together, that is six reports, six slightly different “current states,” and a pile of tasks that re-implement acceptance criteria from the previous section. The prompts also forbid concurrent edits to shared files, then send everyone into the same tree to form an opinion about it.

One baseline is enough. The product manager and the technical lead should write it. QA should inventory evidence independently, because that is the point of the seat. Core, AcroForm, and signing should receive a scoped question after that baseline exists, and their first output should be a review of their own subsystem against `ACCEPTANCE.md`, not a second product plan.

### The historical baseline will be treated as the work list

Each prompt says the “139 tests” paragraph is historical. It then spends the priority section assigning that paragraph as the job. Models follow the concrete list. The open items worth keeping are:

- Rendered appearance checks for the Helvetica subset the charter already specifies.
- A dense large-file measurement, distinct from the 3 GiB sparse test already in `ACCEPTANCE.md`.
- A real or independently implemented TSA, distinct from the in-process `ITimestampAuthority` tests already specified.
- Broader foreign fixtures where a concrete producer is named.

Acrobat, PDFBox, Poppler, MuPDF, pdfsig, and SoftHSM are listed as if they were available. The suite’s required external tools are qpdf and pyHanko, and `ForeignPdfTests` fails when either is missing. Extra tools are optional evidence. They are not a gate, and a missing optional tool must not be reported as a failed test or as a pass.

### Priorities restart finished work

Product-manager P1 tells the team to “complete” certificate-chain embedding and CMS timestamps. Both are already acceptance criteria. The remaining work is external evidence, which belongs to QA.

Signing priorities 2 through 4 overlap that P1 and add the out-of-scope PKCS#11 adapter.

The core role’s investigation list (object-stream lifetime, encrypted xref initialization, the 8 MiB appendix, classic-xref offset ceiling) describes behavior the README already states. Verifying it against the tests is reasonable. Designing a cache budget or an xref-stream writer is not, unless a measurement shows the current limits are the wrong product decision.

## Per role

**Product manager.** The authority split is right: scope, acceptance, release readiness, no routine implementation, escalate dependencies and charter expansions. The dependency section contradicts the charter and contradicts the prompt’s own last sentence. P0 (verify the baseline) is the right first job. P1 needs to be rewritten so it asks for missing external evidence, not for a second implementation of sealing. There is no instruction to read `ACCEPTANCE.md` before writing a new acceptance criterion.

**Technical lead.** Best structural prompt of the six: ownership, escalation, review checklist, and the warning about `PdfEngine.cs`. It still carries the softened dependency rule, the “successor abstraction” for `IPdfExternalSigner`, and a first assignment that duplicates the product manager. The review checklist omits StyleCop, XML docs, and the target frameworks. “You may implement cross-cutting fixes” needs a boundary, or this seat becomes the person who edits every file the specialists were told not to share.

**PDF core.** Ownership of the parser, decoders, incremental writer, encryption, and resource limits is right, and “do not build a cache framework without measurements” matches the project. The prompt is missing the encryption matrix, the filter abbreviations (`Fl`, `LZW`, `RL`, `AHx`, `A85`), the classic-xref write rule, and the decompression caps (32 MiB and 64 MiB). Metadata and attachments should be named as either in this seat or explicitly out of it.

**AcroForm.** The field-versus-widget discussion, inherited attributes, and “`/V` alone is not a populated field” are the right engineering focus. The prompt then undoes the appearance charter, sketches a different public model, and adds UserUnit and top-left viewport coordinates. Geometry ownership should be restated as what `PdfField` and `PdfPage` already return. Signature-field discovery stays here; signing stays with the signing engineer. That boundary is already drawn well.

**Signing.** The CMS-versus-PDF digest distinction, certificate embedding versus trust, and the two timestamp mechanisms are accurate and specific. DocMDP, SoftHSM, PAdES B-LT/B-LTA, and RSA-PSS are the problems. `AGENTS.md` already excludes signature validation, PAdES-LTV, and ECDSA. “Do not introduce long-term validation without prioritization” is weaker than a prohibition. Failure behavior should cite the real guarantee: the provider runs after `/ByteRange` is patched and before the appendix is appended, and a throw leaves the destination length unchanged. Rollback of an arbitrary output stream is not the API.

**QA.** The independence section, the evidence levels, and “qpdf `--check` does not prove a field renders” are the strongest quality text in the set. The file is also a catalog of every test that could exist. An agent will start a compatibility matrix. The first assignment already warns against that, and then sections 6 through 13 specify the matrix anyway. Cut the catalog down to gaps that are not already rows in `ACCEPTANCE.md`. State that a skipped optional tool is “not run,” never a pass and never by itself a release blocker. Severity labels are fine. SoftHSM and in-library PKCS#11 testing should be removed.

## Changes to make before launching anyone

1. Add one line at the top of every role: if this file disagrees with `AGENTS.md`, `README.md`, or `ACCEPTANCE.md`, those documents win. Do not “improve” a closed decision; report the disagreement.
2. Replace the dependency sections with the charter rule. `System.Memory` on `netstandard2.0` is the only runtime package. Anything else waits for the owner.
3. Delete the settled-work investigations: DocMDP redesign, xref-stream writer, 8 MiB appendix removal, SoftHSM, PKCS#11 adapter, successor to `IPdfExternalSigner`, UserUnit, top-left normalized coordinates, and a new `PdfFormField` model.
4. Restate appearance policy in the AcroForm role as the WinAnsi Helvetica rule, including the cases that set `/NeedAppearances` and cannot be signed.
5. Give metadata and attachments an owner. The core engineer is the natural owner unless you want a seventh role. A seventh role is not worth it.
6. Name `PdfEngine.cs` as technical-lead territory. Specialists propose a change; they do not edit it in parallel.
7. QA does not modify `src/`.
8. Collapse the duplicated charter text into one short shared page. Leave each role with ownership, boundaries, and the current assignment.
9. One first assignment: product manager and technical lead write the baseline; QA inventories evidence; the other three wait.
10. Use one name per seat in the filename, the title, and every cross-reference.

The open work the roles correctly identify, once the conflicts above are removed, is external evidence: rendered appearances for the supported Helvetica subset, a dense large-file measurement, and a real or independent TSA. That is a QA-led backlog with narrow engineering follow-up, not a six-agent rewrite.
