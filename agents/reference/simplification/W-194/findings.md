# W-194: empty signature fields in assembly

Research completed 2026-10-26 at product revision `93509b1cce735ac45314c33ab1b6475aaf33d9ef`. Single planning editor; no independent human or fresh-context review. W-188's four missing review attachments remain unavailable. This is a proposal and bounded evidence, not product adoption, a defect fix, or release qualification.

## Recommendation

Preserve empty signing locations in an occurrence-local field tree. First support fields without `/Lock`, retaining W-151 seed enforcement and refusing unsupported mandatory constraints before assembly output. Preserve read-only fields as read-only. Do not advertise a copied lock as enforced: adding FieldMDP transforms at signing is a separate owner decision, explicitly excluded by W-151. A later `/Include` lock slice can be supported only after that decision and end-to-end evidence. Initially refuse `/All`, `/Exclude`, `/P`, dangling targets and partial target subtrees rather than inventing their meaning across combined inputs.

The settled O04 direction earns additional preservation work, not a deletion saving. The current A3 contract deliberately omits all signature fields. That shipped behavior and the target preservation direction are not a source disagreement. Exact proposed wording and separate follow-up scopes are in `proposal.md`; nothing there is approved.

## Reproduction and measured evidence

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File agents/reference/simplification/W-194/run.ps1
```

`-NoRestore` reuses an existing restore. The script uses the pinned Python/qpdf locations in `.tools/test.runsettings`, a disposable console in ignored `out/W-194/probe`, and the public engine API. Its local empty Directory.Build.props excludes analyzers from disposable code; the engine keeps its normal build properties. The `.csproj.txt` template avoids solution discovery. No test project, friend assembly, product edit or new runtime dependency is used. The first build selected the void extraction overload; supplying explicit assembly options corrected that harness error before evidence collection.

Nine original MIT-licensed pypdf fixtures, two pages each, have one empty signature field with two separate widgets and an ordinary text field only on page 2. `fixture-manifest.json` records their SHA-256 hashes. Inputs do not contain actual signatures, certifications, timestamps, encryption or prior FieldMDP restrictions. They are focused synthetic cases, not the W-189 corpus or a business refusal-rate denominator. Source bytes are checked unchanged. Outputs and ephemeral RSA keys/certificates are not committed; keys live only in the console process.

| Evidence | Operation / denominator | Measured result |
| --- | --- | --- |
| E194-1 | 9 extractions of page `[1]` | All accepted; each counts one SignatureWidget omission; no output signature field or widget. |
| E194-2 | 9 concatenations: same open source, occurrence 1 `[2,1,1]`, occurrence 2 `[1]` | All accepted; each counts four SignatureWidget omissions. pypdf confirms four output pages and no signature field. Only Input1.Other survives on output page 1. |
| E194-3 | Sign originals: plain, explicit-null V, SHA256 seed, encoding seed, Include, All, Exclude | 7 successes, one host RSA call each. pypdf confirms source prefix and a signature dictionary. pyHanko confirms CMS byte integrity and cryptographic validity for all seven. This does not establish public trust, a timestamp, PAdES classification or rendering. |
| E194-4 | Sign original with mandatory SHA384 seed using default SHA256; sign read-only original | PdfSignException and PdfFieldReadOnlyException respectively; zero host calls and zero destination bytes in both cases. |
| E194-5 | Three successful original-field signatures with Lock Include/All/Exclude | Lock remains on field; no signature Reference/FieldMDP transform is created. Reopen and fill Other returns Generated for all three. Include and All would lock Other under a functioning transform; Exclude intentionally would not. Structural copying alone cannot establish the intended post-sign restriction. |
| E194-6 | 9 inputs + 18 assembled outputs + 7 signed originals | 34 qpdf checks pass; pypdf independently inspects structure; seven signature prefixes match original inputs. These are structural checks, not full ISO conformance. |

Tools actually exercised: .NET SDK 10.0.402 / net10.0 engine; Python 3.13.17; pypdf 6.21.1; pyHanko 0.38.0; qpdf 12.3.2. Durable `probe-results.json` and `inspection.json` contain outcomes and tool versions. Validation asserts the 27 operation outcomes, omission counts, failures before host/output, raw lock dictionaries, absence of Reference, and cryptographic checks. It does not promote every probe to a shipped regression test.

### Independent seed-reader disagreement

The SHA256 seed fixture uses `/DigestMethod [/SHA256]`, an array of names as specified by ISO 32000-2 Table 237. Engine signing accepts it, and CMS cryptography verifies. pyHanko 0.38.0 reports `seed_value_ok=false`: fields.py reads each item with `s.lower()` (lines 1003 onward), while pdf_embedded.py compares that result with `sha256` (lines 706 onward). A name retains its slash, producing `/sha256`. Its serializer instead emits strings. This source-level explanation is distinct from a standards ruling on every seed case. The diagnostic is deliberately visible during the run and retained in inspection.json; seed agreement is not counted as a pass for that fixture. The separate mandatory SubFilter fixture passes both CMS and seed checks. No input was silently rewritten to appease the reader. W-151's name-based engine policy remains unchanged. A version upgrade or separate upstream report would need its own scope.

### Candidate signing evidence gap

There is no preserved-field candidate implementation in this task. Every current assembly drops the target, so signing `Input1.SignHere` afterward could create a new field; that would not demonstrate reuse or preservation. We deliberately make no such claim. Original-field CMS smoke establishes only the reusable signer path and selected constraints. End-to-end signing into a preserved candidate, both original widget ownership and occurrence-local lock enforcement, remains a mandatory implementation acceptance gate. W-194 expressly permits this explicit evidence gap; it prevents calling the proposal implemented or independently validated as preservation.

## Authority and implementation map

- `docs/spec/phase-2.md` capability table and A3 require omission of all signature fields; A9 permits unsupported appearance omission. The page-assembly sections of ACCEPTANCE.md and docs/behavior.md describe the same shipped behavior. A3 must be owner-approved before replacement; O04 alone does not authorize it.
- `PdfPageExtractor.Import`, src/jaytwo.Pdf/Internal/PdfPageExtractor.cs lines 160–190: inherited Sig type causes counted omission before widget registration. Repeated selected pages therefore count repeated omissions.
- `PdfAssemblyForms.Read`, src/jaytwo.Pdf/Internal/PdfAssemblyForms.cs lines 165–180: Sig subtrees return null before ordinary-field copying. The ordinary InputN tree alone cannot preserve seeds or locks.
- `PdfSigner.SealExistingField`, src/jaytwo.Pdf/Internal/PdfSigner.cs lines 262–326: checks field type, read-only status, existing signature dictionary and seeds by distinct value owner; sets V on each distinct owner and signature flags. It creates no FieldMDP transform in this path. docs/behavior.md line 155 and the W-151 owner disposition explicitly say no new locking transforms. E194-5 is a known capability boundary, not a demonstrated violation of that approved exclusion.
- `PdfSignatureSeedValues.Check` and helpers, src/jaytwo.Pdf/Internal/PdfSignatureSeedValues.cs: required format/digest/reason/version constraints and the W-151 noninteractive policies. Copying SV must not bypass these checks, change effective-version semantics or erase optional entries simply because this engine does not use them.
- ISO 32000-2:2020 EC3, local reference `C:/jake/git/pdfdocs/text/iso-32000-2/295-12-7-5-5-signature-fields.md`, Tables 235–238: indirect Lock/SV dictionaries, lock actions, field-name arrays and seed requirements. Local `306-12-8-3-signature-interoperability.md`, section 12.8.3.1: signing a locked field creates signature-reference/transform dictionaries from the lock instructions. Table 259 in `305-12-8-2-transform-methods.md` supplies FieldMDP semantics. These primary references were read directly; no conformance claim follows from a qpdf pass. Published errata also consulted: https://pdf-issues.pdfa.org/32000-2-2020/clause12.html . Full standard text is not reproduced here.

## Signed inputs and W-197 boundary

An empty field has absent or resolved-null V and no existing signature dictionary; malformed, unresolved, cyclic or ambiguous values require refusal, not classification as empty. A signature dictionary still makes a field signed when ByteRange or Contents is broken: discovery is not trust validation. Approval signatures, certification, document timestamps and UR3 are separate categories. Keeping another empty field must not imply that an earlier signature survives a new-file write.

Proposal: default whole-input refusal for detected approval signatures, timestamps, certification or existing FieldMDP restrictions, including ones on discarded pages. A read-only flag alone is not evidence of a prior signature and is preserved. Unresolvable certification is not permission to proceed. Rights alone are not an approval signature, but must follow the separate rights-preparation decision. Existing authentication, CanCopy/CanAssemble and AllowUnencryptedOutput requirements remain prerequisites. Any signed derivative is a separately named, explicitly consented W-197 proposal, never implicit fallback; consent cannot override DocMDP/FieldMDP. This classification was not exercised by the nine unsigned probes. W-197 must reconcile its bounded inspection, incomplete history, painted marks, refusal diagnostics and W-156 overlap; this item does not settle those questions by implication.

## Cost, migration and limits

Business value: preserve a packet's designated future signing locations, including when one source appears repeatedly. Loss today: the locations and their future constraints disappear. The proposal preserves supported locations or refuses; it does not silently replace constrained fields with unconstrained new ones. No credential burden is added to unencrypted supported inputs. Encrypted inputs retain existing passwords/consent; signed or unsupported constrained inputs may require a supported original from the issuer. There is no promise that unlocking, flattening or obtaining an owner password makes a certified derivative admissible.

Responsibilities retained: sparse graph traversal, pruning, occurrence identity, font/appearance handling, permissions, signature discovery, seed enforcement and output staging. Responsibilities added: signature-field copy policy, precise seed admission, refusal diagnostics, lock-name mapping and (only if approved) transform creation. Nothing here deletes CMS, cryptography, certification or prior-lock handling. W-190/W-197 share field/admission work; count it once. Rough implementation ranges: no-Lock preservation/admission 2–4 engineering days; Include-only transform/remapping slice 3–6 days; independent-tool and refusal/publication verification 1–3 days across those slices. Basis: the localized exclusion paths and missing transform path above, not measured implementation time or a guaranteed schedule. All/Exclude/P semantics need separate evidence before any estimate of support.

Unmeasured: real producer prevalence, nested/Unicode/duplicate signature names, merged widgets, page-less empty fields, unsupported AP, direct or inherited malformed SV/Lock, malicious budgets, encrypted constrained fields, multi-signature workflows, render fidelity, cross-framework behavior, signed derivatives, and candidate transforms. Existing W-190 form probes do not supply those signature-specific results. Independent review remains outstanding for Gate 2; no subagents or persona handoffs were used. Product tests, framework matrix, fuzz, Docker and release gates were not required or run. Proposed implementation must add permanent regressions for its shipped contract; this research confirms no new shipped-contract defect requiring a runtime fix here.
