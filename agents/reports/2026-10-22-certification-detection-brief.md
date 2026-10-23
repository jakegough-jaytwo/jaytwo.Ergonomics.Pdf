# Brief: detecting a certification signature without walking the field tree

Written for a reviewer with no context on this repository, to advise the owner of `jaytwo.Ergonomics.Pdf` on one design question. Please answer the questions at the end. Verify every spec reference below against ISO 32000-1 and ISO 32000-2 yourself; the author paraphrased them from memory.

## The library

`jaytwo.Ergonomics.Pdf` is a managed .NET library for business PDF operations: read, fill, flatten, metadata, attachments, signing. It never rewrites a file. Every write is an incremental update appended to the original bytes, so earlier signatures stay byte-intact. Its tenets, in priority order, include "according to the spec (ISO 32000-1, ISO 32000-2 where real files use it)" and "compatible with real-world PDFs". Reading is strict to ISO 32000, and tolerated writer quirks are a closed list, each with a real-world fixture. The repository's own rules are in `AGENTS.md`, `docs/spec/phase-1.md` (sections 7.2 and 7.3), and `ACCEPTANCE.md`.

## The rule being enforced

A certification signature (DocMDP, ISO 32000-1 12.8.2.2) limits the changes allowed after it. The library enforces this before it writes (`docs/spec/phase-1.md` 7.2):

- DocMDP `P` 1 blocks every write and signing.
- `P` 2 and `P` 3 allow fills and signing. Metadata, attachment, flatten, and rotate writes are blocked at every certification level.
- A certification that does not resolve is treated as `P` 1 (`AGENTS.md`; `ACCEPTANCE.md`: "A DocMDP certification that does not resolve blocks fills and signing, including a `/Perms`, `/Reference` entry, or `/TransformParams` that resolves as null").

If the library wrongly concludes that a certified document is not certified, it appends a forbidden change. Validators such as Acrobat and pyHanko then report the certification as violated. Breaking a signature guarantee is this library's most serious class of defect.

## What the code does now

`CertificationLevel()` (`src/jaytwo.Ergonomics.Pdf/Internal/PdfEngine.cs`, around line 3484) takes the most restrictive of two sources:

1. **The catalog `/Perms /DocMDP` entry.** A dangling reference counts as `P` 1. Otherwise `P` comes from its signature dictionary's DocMDP transform parameters.
2. **Every signature field in the AcroForm field tree.** For each signature field whose `/V` resolves to a signature dictionary, a `/Reference` entry with `/TransformMethod /DocMDP` restricts the level the same way.

Source 2 means a certification is still detected when the catalog `/Perms` is missing, stripped by another tool, or never written by a non-conforming signer. Finding every signature field requires visiting the whole field tree. Fields can nest arbitrarily under `/Fields` and `/Kids`, and they often live inside compressed object streams. So every write first decodes and parses the form.

## Why this matters

On a 500-page object-stream form (1,000 fields, 256 KB file), setting the document title and saving takes 90 ms and allocates 87 MiB, because the walk runs twice. iText 9.8.0 does the same edit in 7 ms with 1.5 MiB; it does not check certification on a general write. Planned work (W-114) makes the walk cheaper and runs it once per revision without changing what it detects. One walk per document still remains. The question is whether the library may skip even that one.

## The options

- **A. Keep the walk (planned in any case).** Detection is unchanged. A signature-only walk with a per-revision cache brings the edit to an estimated 25 to 30 MiB on this form, and the cost still grows with form size.
- **B. Trust the catalog `/Perms /DocMDP` alone.** As the author understands ISO 32000-1 12.8.2.2 and the `/Perms` table (Table 258 in ISO 32000-1), a certification signature shall be referenced from `/Perms /DocMDP`. A conforming certified file therefore always has it. Without `/Perms /DocMDP` the library would treat the file as uncertified and skip the walk. The cost is constant. A file whose certification survives only in the signature field would be written to: a non-conforming writer, or a tool that rebuilt the catalog without `/Perms`.
- **C. B, plus `/SigFlags` as a second gate.** AcroForm `/SigFlags` bit 1, SignaturesExist (ISO 32000-1 Table 219), says the document contains at least one signature field. As the author recalls it, the spec says the flag lets a processor enable signature UI "without having to scan the document". Skip the walk only when there is no `/Perms /DocMDP` and SignaturesExist is clear; otherwise walk. A certified file always has a signature field, so it can be missed only if both markers are absent or wrong.
- **D. B or C, keeping the walk for operations that need it anyway.** Flatten, rotate, fills of hybrid XFA forms, and usage-rights handling must also find approval signatures, which have no catalog marker, so they would keep the walk. The skip would apply only to metadata and attachment writes, where only certification matters. Fills check FieldMDP against signature fields, so they walk regardless.

## Evidence so far

From this repository's G2 corpus of real-world files, read with pypdf on 2026-10-22:

| File | `/SigFlags` | `/Perms` keys | Signature fields | Signed | DocMDP references |
|---|---|---|---|---|---|
| gpo-bills-118hr815enr.pdf (Acrobat certification, `P` 1) | 3 | `/DocMDP` | 1 | 1 | 1 |
| opm-of306-pyhanko-certified.pdf (pyHanko certification, `P` 2) | 3 | `/DocMDP` | 3 | 1 | 1 |
| opm-of306.pdf (unsigned) | 1 | none | 2 | 0 | 0 |
| cms-40b-pyhanko-approved.pdf (approval signature) | 3 | `/UR3` | 2 | 1 | 0 |
| cms-40b.pdf, va-21-4138.pdf (Reader-extended, unsigned) | 3 | `/UR3` | 1–2 | 0 | 0 |
| irs-f1040es-2026.pdf, irs-fw9-2024-03.pdf, grants-sf424-v4.pdf (Reader-extended) | 2 | `/UR3` | 0 | 0 | 0 |
| uscis-i9-2025.pdf | 0 | none | 0 | 0 | 0 |

Every file with a signature field has SignaturesExist set, and both certified files have `/Perms /DocMDP`. Two certifications from two producers is a small sample.

## Questions

1. Does ISO 32000-1 (and ISO 32000-2) require a certification signature to be referenced from the catalog `/Perms /DocMDP`? Quote the requirement and whether it is "shall". Is a file whose certification appears only in a signature field's `/Reference` conforming?
2. How do Acrobat, pyHanko, and iText decide that a document is certified when validating it? From the catalog `/Perms`, the signature fields, or both? In particular: if `/Perms /DocMDP` is absent but a signed field's `/Reference` carries a DocMDP transform, does Acrobat still enforce or report the certification? Give sources or code references where you can.
3. Are there known real-world producers or tools that write a certification without `/Perms /DocMDP`, or that drop `/Perms` when they rewrite or incrementally update a file? Are there producers that write signature fields without setting `/SigFlags` SignaturesExist?
4. Given the library's priorities (spec conformance, then real-world compatibility, with breaking a signature guarantee as the worst failure), which option would you recommend: A, B, C, or D? What evidence would justify the change, for example a corpus of N certified files from M producers?
5. Is there a cheap, strict alternative the author missed? For example, a signal in the cross-reference data or the revision structure that rules out any signature without parsing the form.
