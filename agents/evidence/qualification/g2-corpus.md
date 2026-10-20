# G2 interoperability corpus

Work item: W-13. Gate: `QUALIFICATION.md` section 5. The manifest is `test/jaytwo.Ergonomics.Pdf.Tests/Fixtures/corpus/manifest.json`; it is the per-file record (ID, SHA-256, size, provenance and acquisition date, license and redistributability, producer and version, transformations, PDF version and features, classification and justification, operations and expected results, validators). This file summarizes it and records the findings later items need. Run results, with commits, are in the W-13 closeout and `status.md`.

## Composition

41 entries. Every file is committed and redistributable; none needs the fetch path.

| Category | Files | External | Transformed | Test generator |
| --- | ---: | ---: | ---: | ---: |
| forms | 12 | 9 | 3 | 0 |
| office-browser | 8 | 7 | 1 | 0 |
| scans-linearized | 7 | 7 | 0 | 0 |
| reports | 6 | 6 | 0 | 0 |
| synthetic | 8 | 0 | 0 | 8 |
| Total | 41 | 29 | 4 | 8 |

33 files sit outside the synthetic category (gate: 30). 29 of 41 (71%) are authored by tools other than pypdf, qpdf, and pyHanko (gate: two thirds). The four transformed files are real documents that pyHanko signed, certified, or encrypted, or pypdf rewrote; they count with the test generators.

External producers: Adobe LiveCycle Designer 6.4 and 6.5 (IRS W-9, VA 21-4138, Grants.gov SF-424), Antenna House PDF Output Library 6.6 (IRS 1040-ES), Acrobat Distiller 6.0.1, 23.0, and 24.0 (IRS 5500 with PageMaker 7, USCIS I-9, GPO enrolled bill with iText 7.2.3 signing), Adobe PDF Library 4.0, 15.0, 17.0, and 21.5 (Acrobat Capture 3.0 scan; PDFMaker for Word; InDesign 20.3; PDFMaker for PowerPoint), Microsoft Word for Microsoft 365 with Acrobat fields (OPM OF-306), Acrobat PDFWriter 2.01 (NACA scan), LuraDocument 2.65 (Internet Archive scan), PDFlib+PDI 9.3 (Federal Reserve Beige Book), LibreOffice 26.8.1 (Writer form, encrypted memo, PDF/A-3 hybrid, Calc report), Microsoft Word, Excel, and PowerPoint 2016 (build 16.0.20430), Microsoft Print to PDF, Chrome 155 and Edge 155 (Skia/PDF m155), ImageMagick 7.1.2, cairo 1.18.4 through Inkscape 1.4.4, and matplotlib 3.11.2.

Sources: US federal publications (IRS, USCIS, VA, HHS Grants.gov, CMS, FAA, OPM, GPO, NASA NTRS for NACA reports, NIST including NBS Circular 1, Federal Reserve Board), all public domain, and documents authored for the corpus from the committed sources in `Fixtures/corpus/authoring/`. No owner documents.

Structures covered by supported files, as `corpus_probe.py` reports them: classic and stream cross-references, Office's hybrid `/XRefStm` layout, object streams, linearized files (several with inconsistent hint tables), multiple revisions, page attributes inherited from the page-tree root (LibreOffice writes `/Resources` there; one pypdf-transformed form inherits `/MediaBox`, `/Resources`, and `/Rotate 90`), a balanced 287-node page tree with 2,000 pages, standard security revisions 3 (LibreOffice, RC4-128) and 6 (qpdf, AES-256), AcroForm text, choice, and button fields, signature fields, approval and certification signatures (GPO DocMDP P 1; pyHanko DocMDP P 2), Reader-extension usage rights, hybrid XFA and an XFA form with no AcroForm fields, `/NeedAppearances`, XMP, embedded files including PDF/A-3 `/AF` with `/AFRelationship /Source`, tagged structure trees, outlines, document JavaScript, and CCITT, JBIG2, JPX, and DCT images. One file is deliberately unsupported: public-key (`/Adobe.PubSec`) encryption, which the charter excludes.

## Checks

`CorpusManifestTests` is the manifest validator. It requires every entry to be complete; IDs, files, and hashes to be distinct; every committed hash and size to match; every fixture PDF to have an entry; the G2 thresholds (hard-coded from `QUALIFICATION.md`, not read from the manifest); the required coverage among supported files; and qpdf, pypdf, and pyHanko, run through `corpus_probe.py`, to re-derive every recorded fact on every run. A non-redistributable entry fails the validator until the corpus has a fetch path.

`CorpusTests` runs, for each entry: open (or the expected controlled exception), then the library's page count, first-page box and rotation, Info title and producer, XMP presence, widget names, embedded-file names and bytes, and permissions against the probe; a metadata revision; and, for forms, a text fill. Each write must keep the original as a prefix, pass `qpdf --check` with no warnings, and leave pages, widgets, attachments, and every earlier signature (pyHanko: intact and cryptographically valid) unchanged, with the new value confirmed by pypdf. A documented refusal must throw its exception and write nothing.

## Findings

These are recorded, not fixed, under W-13. Each one is asserted as a controlled exception in the manifest, so a change in behavior fails the corpus tests until the manifest is updated.

### D1. A Flate stream with a trailing byte inside `/Length` fails with a false checksum error

- Summary: `ZlibPayloadStream` takes the last four bytes of the stream data as the Adler-32. When the zlib stream ends before `/Length`, the library compares the wrong bytes and throws `PdfParseException` ("The zlib checksum does not match.", reported at offset 0).
- Impact: reading the embedded file of the IRS 2026 Form 1040-ES (`irs-f1040es-2026`) fails. Any Flate stream with the same shape, including content the library must decode, would fail the same way.
- Reproduction: open `corpus/irs-f1040es-2026.pdf`, enumerate `Attachments`, and read `Press Quality(1).joboptions` to the end.
- Expected: RFC 1950 places the Adler-32 immediately after the deflate data. qpdf 12.4.2 (`--show-attachment`), pypdf 6.21.1, and Python's strict `zlib.decompress` all decode the 15,003 bytes. The deflate data ends at byte 3,963 of `/Length 3968`, its checksum `54fc9fc4` matches, and one `0x0A` byte follows.
- Actual: `PdfParseException` from `ZlibInflateStream.Read`.
- Severity: Medium (a predictable, controlled failure on a real file; no corruption).
- Disposition: G2 release blocker, owned by W-66 (`QUALIFICATION.md` section 5: fix, exclude with justification, or block). Whether trailing bytes after the zlib stream are tolerated is a quirk-list decision; locating the checksum after the deflate data, and the misleading message, need fixing either way. Regression test: the manifest's `attachments: PdfParseException` for `irs-f1040es-2026`, asserted by `CorpusTests`.

### Q1. Embedded-file name-tree key in UTF-16LE without a byte-order mark

- Summary: FAA Form 8710-1 (`faa-8710-1`, Acrobat PDFMaker 15 for Word) keys its one embedded file `a\0c\0c\0r\0e\0p\0o\0r\0t\0.\0h\0t\0m\0l\0`. That is not a valid text string (ISO 32000-1 7.9.2.2), and listing attachments throws `PdfParseException` ("Undefined PDFDocEncoding byte 0x00.").
- Impact: no attachment of that file can be listed. The file spec's `/F` and `/UF` are valid (`FAA_Form_8710-1_.pdf.accreport.html`). Opening, reading fields, the metadata revision, and fill all work.
- Independent tools: qpdf 12.4.2 reports the key with embedded NULs; pypdf 6.21.1 does not match it to a file spec.
- Severity: Medium.
- Disposition: G2 release blocker, owned by W-66, which decides whether this quirk joins the tolerated list (spec section 8) or is excluded with justification. Regression test: the manifest's `attachments: PdfParseException` for `faa-8710-1`.

### Known Phase 1 gaps the corpus now has real fixtures for

- Usage rights: after a metadata revision or a fill of a Reader-extended form (IRS W-9, 1040-ES, VA 21-4138, CMS-40B, Grants.gov SF-424), `/Perms /UR3` is still present. Spec 7.3 requires keeping it only when it provably permits the write. W-67.
- Hybrid XFA: filling the IRS W-9 updates the AcroForm value but not the XFA `datasets` packet. W-76.
- The VA 21-4138 first-name field is a comb field, so a fill sets `/NeedAppearances` as documented.
- Every nonzero `qpdf --check` exit on a supported original comes from linearization hint tables (CMS-40B, FAA 8710-1, OPM OF-306, NIST SP 1271, Beige Book, both NACA scans, NBS Circular 1). The library ignores hints, and its incremental output checks clean.
- No common file needed a whole-file scan or cross-reference repair. Nothing here needs escalation under spec section 8.
