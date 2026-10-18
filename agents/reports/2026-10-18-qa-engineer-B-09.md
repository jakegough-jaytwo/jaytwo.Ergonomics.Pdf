# B-09. Evidence that an info edit refreshes ModDate in UTC

## Task and owner

QA Engineer. The only product-tree edit is `test/jaytwo.Ergonomics.Pdf.Tests/MetadataTests.cs`. The metadata row in `ACCEPTANCE.md` was not changed. No `agents/state/` edits. No commit.

**Passed.** Not a defect. An info edit that does not set `ModificationDate` writes `ModDate` in UTC, and a later read returns that UTC value.

## Verified facts

- `info_edit_refreshes_moddate_in_utc` opens a file whose `ModDate` is `D:20200101120000-05'00'`, sets `Title` to `New`, and does not set `ModificationDate`.
- After `SaveIncremental`, a later open reads title `New`. `ModificationDate` has offset zero, falls within one second of `DateTimeOffset.UtcNow` around the edit, and the info text starts with `D:` and ends with `Z`.
- `trapped_is_a_name_and_info_dates_round_trip_as_text` still sets `ModDate` explicitly. That fact was not the refresh assertion.

## Verification run

SDK `10.0.100-rc.1.25451.107` (`NETSDK1057` preview notice).

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net8.0 --filter "FullyQualifiedName~info_edit_refreshes_moddate_in_utc" --nologo`

Passed 1, failed 0, skipped 0. Duration 45 ms. Target `net8.0`.

`dotnet test test/jaytwo.Ergonomics.Pdf.Tests/jaytwo.Ergonomics.Pdf.Tests.csproj -f net6.0 --filter "FullyQualifiedName~info_edit_refreshes_moddate_in_utc" --nologo`

Passed 1, failed 0, skipped 0. Duration 51 ms. Target `net6.0`.

Not run, and not counted as a pass: the rest of `MetadataTests`, and the rest of the suite.

## Changes made or proposed

- `test/jaytwo.Ergonomics.Pdf.Tests/MetadataTests.cs`: added `info_edit_refreshes_moddate_in_utc`.

`dotnet test` wrote gitignored coverage under `out/coverage/` because `test/Directory.Build.props` sets `CollectCoverage`.

## Handoff

Technical Lead reviews. The Product Manager is not asked to accept this from the report alone.
