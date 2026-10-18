# B-16 review

## 1. Task and owner

Technical Lead. Review of the corrected Poppler appearance checks. No edit to `AppearanceRenderTests.cs` or `backlog.md`. The task is not accepted. G6 is not passed.

## 2. Verified facts

Approved. No defect.

The first QA report did not sign off. The center word only had to sit more than 80 points from its neighbors. The corrected test orders the three `Align` words by XMin and requires 30–60, 174–180, and 280–340. The center assertion is `Assert.InRange(aligned[1].XMin, 174, 180)`. The gap checks are gone. QA signed that correction off. `AppearanceRenderTests` passed 2 and failed 0 on `net8.0` and on `net6.0`. This review did not rerun it.

`poppler_renders_generated_text_choice_checkbox_and_radio_appearances` still requires unfilled text without `Ada`, filled text with `Ada` and `Blue`, and a raster change in the four rectangles. The new fact uses `pdftotext` and `pdftoppm` only. `pdftocairo` is not called. The PDF is built in the test. No new fixture file. The implementer reports no `src/`, `README.md`, or `ACCEPTANCE.md` edit.

A second renderer was not added. G6 stays unpassed.

## 6. Changes made or proposed

This report. B-16 stays on `agents/state/board.md` until the Product Manager accepts it.

## 9. Handoff

The Product Manager accepts the task. This review does not, and it does not pass G6.
