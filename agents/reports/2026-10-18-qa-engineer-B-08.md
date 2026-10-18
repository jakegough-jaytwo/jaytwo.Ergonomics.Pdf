# B-08. Choice field with no /Opt rejects a value

## Task and owner

QA Engineer. Read-only except this report. No product code and no `agents/state/` edits. B-09 was not started.

**Signed off.** The B-08 acceptance criteria are met. Evidence required: internally tested.

## Verified facts

- `RequireOption` returns only when `FieldSlot.Editable` is true. An empty option list falls through and throws `PdfFieldValueException` ("The choice value is not in /Opt."). `Editable` is set only for a choice field whose flags include `FlagEdit` (`1 << 18`).
- `SetChoice` calls `RequireOption` before `EditAny`, so that throw does not mark objects dirty.
- `choice_without_options_does_not_dirty_the_document` opens a choice widget with no `/Opt`, asserts `Options` is empty and `Editable` is false, expects `PdfFieldValueException` from `SetChoice("Choice", "Yes")`, then `SaveIncremental`. The stream length stays the original length.
- `simple_text_generates_helvetica_and_other_values_request_appearances` expects that same throw for choice `Choice`. After the other fields are saved, a reopen reads no value for `Choice`.
- `choice_options_reject_unknown_values_and_multi_select_is_an_array` still accepts `SetChoice("Editable", "custom")` as `Generated`. That field's `/Ff` is `262144`, the Edit flag, and `/Opt` is `[(A)]`. A reopen reads `custom`.
- The choice sentence in `ACCEPTANCE.md` is unchanged: a value that is not listed throws `PdfFieldValueException` and does not dirty the document, unless the Edit flag is set. `git diff` of `ACCEPTANCE.md` contains none of that sentence.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~AcroFormTests" --nologo`

Passed 14, failed 0, skipped 0. Duration 262 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~AcroFormTests" --nologo`

Passed 14, failed 0, skipped 0. Duration 247 ms. Target `net6.0`.

Not run, and not counted as passes: the rest of the suite, and `netstandard2.0` / `netstandard2.1`.

## Changes made or proposed

This report only. `dotnet test` wrote gitignored coverage under `out/coverage/` because `test/Directory.Build.props` sets `CollectCoverage`.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone.
