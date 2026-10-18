# B-21. Core and AcroForm clauses

## Task and owner

QA Engineer. Read-only except this report. No product-code edits and no `agents/state/` edits.

**Core group: signed off.** **AcroForm group: signed off.** Evidence required: internally tested. This report does not cover clause 10.

## Verified facts

Core:

- `header_after_the_first_1024_bytes_is_not_the_header` opens a `%PDF-1.7` header at offset 1016 and reads the title. A header at offset 1024 throws `PdfParseException`.
- `revision2_fill_in_without_annotate_cannot_fill_a_field` opens a revision 2 file built with `FillOnly`. The user password reports `CanAnnotate` and `CanFillForms` false. `SetText` throws `PdfEncryptionRestrictedException`. `SaveIncremental` leaves the stream length unchanged.
- `rc4_round_trips_strings_streams_and_the_prefix` is four rows: revision 2 at 40 bits, revision 3 at 40 and 128 bits, and revision 4 RC4 at 128 bits. Each incremental appendix contains `0102030405060708090A0B0C0D0E0F10`.
- `revision5_round_trips_and_keeps_the_password_bytes` asserts that same ID in the revision 5 incremental appendix.

AcroForm:

- `kids_inherit_default_appearance` puts `/DA (/Helv 12 Tf 0 0 1 rg)` only on the parent. The filled appendix contains `/Helv 12 Tf` and `0 0 1 rg`.
- `bare_check_box_appearance_is_named_yes_and_off` expects `Generated`, appendix keys `/Yes` and `/Off`, and on reopen export value `Yes`, value `Yes`, and `IsChecked`.
- `radio_mark_is_cubic_curves_without_arc` fills a radio with no appearance. The appendix contains the `c` operator and does not contain `arc`.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --nologo --filter "FullyQualifiedName~header_after_the_first_1024_bytes_is_not_the_header|FullyQualifiedName~revision2_fill_in_without_annotate_cannot_fill_a_field|FullyQualifiedName~rc4_round_trips_strings_streams_and_the_prefix|FullyQualifiedName~revision5_round_trips_and_keeps_the_password_bytes"`

Passed 7, failed 0, skipped 0. Duration 133 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --nologo --filter "FullyQualifiedName~header_after_the_first_1024_bytes_is_not_the_header|FullyQualifiedName~revision2_fill_in_without_annotate_cannot_fill_a_field|FullyQualifiedName~rc4_round_trips_strings_streams_and_the_prefix|FullyQualifiedName~revision5_round_trips_and_keeps_the_password_bytes"`

Passed 7, failed 0, skipped 0. Duration 165 ms. Target `net6.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --nologo --filter "FullyQualifiedName~kids_inherit_default_appearance|FullyQualifiedName~bare_check_box_appearance_is_named_yes_and_off|FullyQualifiedName~radio_mark_is_cubic_curves_without_arc"`

Passed 3, failed 0, skipped 0. Duration 89 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --nologo --filter "FullyQualifiedName~kids_inherit_default_appearance|FullyQualifiedName~bare_check_box_appearance_is_named_yes_and_off|FullyQualifiedName~radio_mark_is_cubic_curves_without_arc"`

Passed 3, failed 0, skipped 0. Duration 65 ms. Target `net6.0`.

Not run, and not counted as passes: the rest of each class, and the rest of the suite.

## Changes made or proposed

This report only.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone.
