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

W-13 recorded these without fixing them and asserted each as a controlled exception in the manifest. W-66 resolved both on 2026-10-20; the manifest now records `attachments: ok` for both files, so `CorpusTests` checks their names and bytes against qpdf and pypdf.

### D1. A Flate stream with a trailing byte inside `/Length` fails with a false checksum error

- Summary: `ZlibPayloadStream` takes the last four bytes of the stream data as the Adler-32. When the zlib stream ends before `/Length`, the library compares the wrong bytes and throws `PdfParseException` ("The zlib checksum does not match.", reported at offset 0).
- Impact: reading the embedded file of the IRS 2026 Form 1040-ES (`irs-f1040es-2026`) fails. Any Flate stream with the same shape, including content the library must decode, would fail the same way.
- Reproduction: open `corpus/irs-f1040es-2026.pdf`, enumerate `Attachments`, and read `Press Quality(1).joboptions` to the end.
- Expected: RFC 1950 places the Adler-32 immediately after the deflate data. qpdf 12.4.2 (`--show-attachment`), pypdf 6.21.1, and Python's strict `zlib.decompress` all decode the 15,003 bytes. The deflate data ends at byte 3,963 of `/Length 3968`, its checksum `54fc9fc4` matches, and one `0x0A` byte follows.
- Actual: `PdfParseException` from `ZlibInflateStream.Read`.
- Severity: Medium (a predictable, controlled failure on a real file; no corruption).
- Disposition (W-66): fixed as a tolerated writer quirk. The extra byte is the end-of-line marker before `endstream`, which ISO 32000-1 7.3.8.1 excludes from `/Length`. The decoder checks the Adler-32 at the end of the stream data and, when that does not match, in front of a final CR, LF, or CR LF; nothing else is searched. Other trailing bytes, and a checksum that matches neither place, still throw `PdfParseException`, whose message now names the Adler-32 at the end of the stream data. The same shape is in seven Flate streams of `irs-f5500-2005` (three font programs, a `/ToUnicode` CMap, and three page content streams) (Acrobat Distiller 6.0.1); the library never decodes those, and `WriterQuirkTests` decodes one font program directly. Listed in `README.md` (support matrix, Writer quirks). Regression tests: `WriterQuirkTests` and the `irs-f1040es-2026` corpus entry.

### Q1. Embedded-file name-tree key in UTF-16LE without a byte-order mark

- Summary: FAA Form 8710-1 (`faa-8710-1`, Acrobat PDFMaker 15 for Word) keys its one embedded file `a\0c\0c\0r\0e\0p\0o\0r\0t\0.\0h\0t\0m\0l\0`. That is not a valid text string (ISO 32000-1 7.9.2.2), and listing attachments throws `PdfParseException` ("Undefined PDFDocEncoding byte 0x00.").
- Impact: no attachment of that file can be listed. The file spec's `/F` and `/UF` are valid (`FAA_Form_8710-1_.pdf.accreport.html`). Opening, reading fields, the metadata revision, and fill all work.
- Independent tools: qpdf 12.4.2 reports the key with embedded NULs; pypdf 6.21.1 does not match it to a file spec.
- Severity: Medium.
- Disposition (W-66): fixed as a defect in the library's reading, not added to the quirk list. ISO 32000-1 7.9.6 requires a name-tree key to be a string, not a text string, so the key is legal and the library was too strict. A key that is not a text string now names its attachment by the file specification (`/UF`, then `/F`): `FAA_Form_8710-1_.pdf.accreport.html`, which is qpdf's preferred name; the bytes match pypdf and the file's `/Params /CheckSum`. A later attachment edit keeps the key's bytes. Guessing UTF-16LE (pypdf reports `accreport.html`) was rejected as an encoding heuristic the standard does not call for. Documented in `README.md` (support matrix, Attachments) and `ACCEPTANCE.md`. Regression tests: `WriterQuirkTests` and the `faa-8710-1` corpus entry; the probe now records qpdf's `preferredname` as `fileName` and lets pypdf find the bytes by it.

### Known Phase 1 gaps the corpus now has real fixtures for

- Usage rights: before W-67, `/Perms /UR3` stayed after a metadata revision or a fill of a Reader-extended form (IRS W-9, 1040-ES, VA 21-4138, CMS-40B, Grants.gov SF-424). Disposition (W-67): spec 7.3 is applied to every write. pypdf reads `/UR3` kept after a fill (every form grants `/Form FillIn`) and removed after a metadata revision or a signature; an added attachment keeps it except on CMS-40B, which grants no `/EF` rights. Regression tests: `CorpusTests` and `UsageRightsTests`.
- Name-tree order (W-67 observation, W-86): adding an attachment whose name sorts after `Press Quality(1).joboptions` to the IRS 1040-ES, whose name-tree key is UTF-16BE, made qpdf 12.4.2 `--check` warn that the keys are not sorted (exit 3); qpdf repaired the tree in memory, so `--show-attachment` still found both files, and pypdf 6.21.1 read both. The library wrote the new key in PDFDocEncoding beside the UTF-16BE one and ordered the keys by their bytes; qpdf compares them as text. Disposition (W-86): fixed as a writer defect. ISO 32000-1 7.9.6 and ISO 32000-2 7.9.6 (PDF Association issue 214) order keys by their bytes in one self-consistent encoding, so the library now writes a new key in the tree's encoding (UTF-16BE for a new tree). `qpdf --check` passes with no warnings on the output. Regression tests: `AttachmentTests` and the Reader-extended attachment test in `UsageRightsTests`, which now adds `usage-rights.txt`.
- Hybrid XFA: filling the IRS W-9 updates the AcroForm value but not the XFA `datasets` packet. W-76.
- The VA 21-4138 first-name field is a comb field, so a fill sets `/NeedAppearances` as documented.
- Every nonzero `qpdf --check` exit on a supported original comes from linearization hint tables (CMS-40B, FAA 8710-1, OPM OF-306, NIST SP 1271, Beige Book, both NACA scans, NBS Circular 1). The library ignores hints, and its incremental output checks clean.
- No common file needed a whole-file scan or cross-reference repair. Nothing here needs escalation under spec section 8. W-66 rechecked the other section 8 candidates against every corpus file: no bytes after the final `%%EOF` beyond an end-of-line, every `startxref` lands on `xref` or an object, and every directly or indirectly given `/Length` outside object streams ends at `endstream` (allowing one end-of-line marker); the corpus opens without any of those handlers.
