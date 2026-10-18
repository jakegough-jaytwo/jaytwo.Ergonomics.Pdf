# B-08 review

## 1. Task and owner

Technical Lead. Review of the AcroForm Engineer's B-08 change. No product code, `README.md`, or `backlog.md` edit. The task is not accepted.

## 2. Verified facts

Approved. No defects.

The only product diff against `11da851` in `PdfEngine.cs` is `RequireOption`. The early return is now `slot.Editable` alone. An empty `/Opt` falls through the option loop and throws `PdfFieldValueException` ("The choice value is not in /Opt."). `Editable` is set only for a choice field with `FlagEdit` (`1 << 18`). `SetChoice` calls `RequireOption` before `EditAny`. `EnsureFieldChange` and `RequireFill` do not add to `_dirty`. A same-stream `SaveIncremental` with an empty dirty set does not append.

`choice_without_options_does_not_dirty_the_document` uses a choice widget with no `/Opt`, asserts `Options` is empty and `Editable` is false, expects the throw from `SetChoice("Choice", "Yes")`, and asserts the stream length stays the original length. `simple_text_generates_helvetica_and_other_values_request_appearances` expects that throw and a null value for `Choice` after reopen. `choice_options_reject_unknown_values_and_multi_select_is_an_array` still expects `Generated` for `SetChoice("Editable", "custom")` on `/Ff 262144`.

`git diff HEAD -- ACCEPTANCE.md` does not touch the choice sentence. That sentence still says a value that is not listed throws `PdfFieldValueException` and does not dirty the document, unless the Edit flag is set. Password and signature fields still throw, in that file and in the simple-text test. D-17 stays closed.

QA signed off: `AcroFormTests` 14 passed, 0 failed, on `net8.0` and `net6.0`. This review did not rerun them.

Checklist: one condition removed. No new API, package, or excluded capability. The failure happens before any object is edited. The regression test is the new fact plus the updated `Choice` expectation.

## 6. Changes made or proposed

This report. `agents/state/board.md` marks B-08 in review. The row stays.

## 9. Handoff

The Product Manager accepts the task. This review does not.
