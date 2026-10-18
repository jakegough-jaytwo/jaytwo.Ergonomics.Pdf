# B-08 Choice field with no /Opt rejects a value

## 1. Task and owner

AcroForm Engineer. A choice field with no `/Opt` and the Edit flag clear throws `PdfFieldValueException` and does not dirty the document. The Edit flag still allows a value that is not listed. `ACCEPTANCE.md` was not edited. D-17 stays closed: password and signature fills still throw.

## 2. Verified facts

`RequireOption` returned immediately when `Options` was empty, so Edit was not required. `SetChoice` calls `RequireOption` before `EditAny`, so a throw does not mark objects dirty.

The early return is now only `slot.Editable`. An empty `/Opt` falls through and throws `PdfFieldValueException` ("The choice value is not in /Opt.").

`simple_text_generates_helvetica_and_other_values_request_appearances` fills choice `Choice`, which has no `/Opt`, with `Yes`. It now expects `PdfFieldValueException`. The reopen asserts `Choice` has no value. `choice_without_options_does_not_dirty_the_document` is that field alone: the throw, then `SaveIncremental`, and the stream length stays the original length. `choice_options_reject_unknown_values_and_multi_select_is_an_array` still expects `Generated` for `SetChoice("Editable", "custom")`.

## 6. Changes made or proposed

- `src/jaytwo.Ergonomics.Pdf/Internal/PdfEngine.cs`: `RequireOption` no longer treats an empty option list as allowed.
- `test/jaytwo.Ergonomics.Pdf.Tests/AcroFormTests.cs`: the `Choice` / `Yes` expectation in `simple_text_generates_helvetica_and_other_values_request_appearances` is the throw, and `choice_without_options_does_not_dirty_the_document` covers the unchanged length.

## 7. Verification run

```
dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~AcroFormTests" --nologo
```

Result: Passed. Failed 0, Passed 14, Skipped 0, Total 14. Duration 271 ms. `jaytwo.Ergonomics.Pdf.Tests.dll` (`net8.0`).

## 9. Handoff

QA Engineer verifies. Technical Lead reviews the engine change.
