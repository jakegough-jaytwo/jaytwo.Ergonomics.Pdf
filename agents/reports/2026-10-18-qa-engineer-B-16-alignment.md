# B-16 alignment re-check

## Task and owner

QA Engineer. Read-only except this report. No product-code edits. No commit.

**Alignment claim: signed off.** The center `Align` word's XMin is asserted in 174–180, and both frameworks passed. **The whole B-16 test is now signed off.** The earlier alignment failure is corrected. `agents/reports/2026-10-18-qa-engineer-B-16.md` stays the record that the first version was not signed off. **G6 is not passed.**

## Verified facts

The updated report sentence matches the test. The three `Align` words, ordered by XMin, are required in 30–60, 174–180, and 280–340. The two greater-than-80 gap checks are gone.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice). `JAYTWO_POPPLER` was `%LOCALAPPDATA%\jaytwo-tools\poppler\poppler-26.09.0\Library\bin`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~AppearanceRenderTests" --nologo`

Passed 2, failed 0, skipped 0. Duration 500 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~AppearanceRenderTests" --nologo`

Passed 2, failed 0, skipped 0. Duration 642 ms. Target `net6.0`.

## Changes made or proposed

This report only.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone. G6 stays unpassed.
