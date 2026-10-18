# B-12 review

## 1. Task and owner

Technical Lead. Review of the support matrix in `README.md`. No edit to `README.md`, `ACCEPTANCE.md`, or `backlog.md`. The task is not accepted. G1 is not passed.

## 2. Verified facts

Approved. No mismatches.

The **Support matrix** section has separate Read and Modify columns. The introduction says opening a file, including a signed file, is a read, and a fill, metadata write, attachment edit, or signature is allowed only when a row allows it. The Signing read cell says opening the file does not allow those modifications. The modify cell then states the DocMDP and FieldMDP rules: `P` 1 blocks them, and `P` 2 and `P` 3 allow fills and `Sign` while metadata and attachments stay blocked.

The AcroForm modify cell says password and signature fields throw `PdfFieldValueException` and do not dirty the document. `/NeedAppearances true` is limited to comb, rich text, and non-WinAnsi values. That matches D-17 and `AGENTS.md`. Those fields do not set `/NeedAppearances`.

The matrix says the **Out of scope** operations stay excluded, and there is no experimental tier. That list still excludes rendering, page-content text extraction, merge, split, rotate, rewriting in place, creating encryption, public-key encryption, signature validation, PAdES-LTV, ECDSA, XFA, JavaScript, annotation editing, and redaction. Page content streams stay undecoded. None of those is a supported row.

The version, xref, encryption, signing, metadata, attachment, and limit rows match the charter: PDF 1.0 through 1.7, classic xref on write, revisions 2 through 6, host RSA SHA-256 CMS plus a separate document timestamp, and the decompression, appendix, copy, and classic-xref offset limits.

## 6. Changes made or proposed

This report. `agents/state/board.md` marks B-12 in review. The row stays. B-19 stays.

## 9. Handoff

The Product Manager accepts the matrix. This review does not, and it does not pass G1.
