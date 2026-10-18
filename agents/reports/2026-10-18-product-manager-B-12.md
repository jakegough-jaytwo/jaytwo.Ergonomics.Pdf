# B-12 support matrix

## Task and owner

Product Manager. Wrote the G1 support matrix. Edited `README.md` only. B-12 is not closed. G1 is not passed.

## Verified facts

The matrix is the **Support matrix** section in `README.md`, after **Out of scope** and before **Usage**.

It separates read from modify. Opening a file, including a signed file, is a read. A fill, metadata write, attachment edit, or signature is a modify, and DocMDP, FieldMDP, and the user-password bits gate it.

The rows cover PDF 1.0 through 1.7, classic xref, xref streams, hybrid `/XRefStm`, object streams, encryption revisions 2 through 6, AcroForm fields and appearances, CMS detached signing and a separate document timestamp, metadata, attachments, and the decompression, appendix, copy, and classic-xref offset limits.

Password and signature fields throw `PdfFieldValueException` and do not dirty the document. Comb, rich text, and non-WinAnsi values set `/NeedAppearances`. The out-of-scope list is unchanged. No existing README sentence was rewritten.

## Handoff

The Technical Lead reviews the matrix. G1 stays open until that review. B-12 stays open.
