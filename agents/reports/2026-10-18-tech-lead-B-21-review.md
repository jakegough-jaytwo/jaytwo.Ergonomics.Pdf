# B-21 review

## 1. Task and owner

Technical Lead. Review of the Core and AcroForm clause tests. No edit to `SignatureTests.cs`, `README.md`, `backlog.md`, or product code. The two tasks are not accepted.

## 2. Verified facts

Approved. No defects.

`header_after_the_first_1024_bytes_is_not_the_header` opens a `%PDF-1.7` header that starts at offset 1016 and reads the title. A header that starts at offset 1024 throws `PdfParseException`. The opening row still says `Open` reads the header from the first 1024 bytes.

`revision2_fill_in_without_annotate_cannot_fill_a_field` uses `FillOnly`, which sets the fill-in bit and leaves annotate clear. The user password reports `CanAnnotate` and `CanFillForms` false. `SetText` throws `PdfEncryptionRestrictedException`. `SaveIncremental` leaves the stream length unchanged.

`rc4_round_trips_strings_streams_and_the_prefix` covers revision 2 at 40 bits, revision 3 at 40 and 128 bits, and revision 4 RC4 at 128 bits. `revision5_round_trips_and_keeps_the_password_bytes` covers revision 5. Each incremental appendix contains `/Encrypt` and the original `/ID` hex `0102030405060708090A0B0C0D0E0F10`, and the original bytes stay a prefix.

`kids_inherit_default_appearance` puts `/DA (/Helv 12 Tf 0 0 1 rg)` only on the parent. The filled appendix contains that font size and `0 0 1 rg`. `bare_check_box_appearance_is_named_yes_and_off` returns `Generated`, writes `/Yes` and `/Off`, and a later open reads export value `Yes`, value `Yes`, and checked. `radio_mark_is_cubic_curves_without_arc` writes the `c` operator and does not contain `arc`.

QA recorded Core 7 passed and 0 failed, and AcroForm 3 passed and 0 failed, on `net8.0` and on `net6.0`. This review did not rerun them.

## 6. Changes made or proposed

This report. The Core and AcroForm rows stay on `agents/state/board.md` until the Product Manager accepts them. B-20 is removed. G10 stays unpassed. B-12 stays because `agents/reports/2026-10-18-product-manager-B-12-accept.md` does not exist. Clauses 8 and 9 stay.

## 9. Handoff

The Product Manager accepts the two groups. This review does not.
