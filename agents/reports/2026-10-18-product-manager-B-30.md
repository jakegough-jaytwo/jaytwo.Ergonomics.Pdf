# B-30 acceptance

## 1. Task and owner

Product Manager. Accept or return B-30, "Bound the predictor `/Columns` allocation", against `agents/state/backlog.md` and `team.md` section 8. Uncommitted on `wip`. G8 is not passed.

## 2. Verified facts

- The uncommitted diff matches the reviewed versions. `git hash-object` gives `e43f1de` for `PdfParser.cs`, `ec7b807` for `PdfStreamDecoder.cs`, and `842540a` for `OpenTests.cs`. These are the hashes QA recorded.
- Criteria. The two backlog cases, and the wrapping-sum case the Technical Lead added, each throw `PdfParseException`. Each allocates under 8,388,608 bytes, against about 268.5 MB unfixed, and leaves the stream bytes unchanged. The PNG predictor 12 xref row in `ACCEPTANCE.md` still passes (`xref_stream_with_predictor_and_object_stream_reads_info`). Source: `agents/reports/2026-10-18-qa-engineer-B-30.md`.
- Section 8:
  1. Criteria met at the internally tested level, on `net8.0` and `net6.0`.
  2. The four library targets built with 0 warnings and 0 errors.
  3. Each confirmed defect has a test that fails when its fix alone is reverted. This held for the decoder and, after the rework, for the parser.
  4. The refusal is `PdfParseException`, and the file is not extended.
  5. `README.md` and `ACCEPTANCE.md` are unchanged. No criterion was weakened.
  6. Limitations are written down in the Core Engineer report.
  7. QA signed off, and the Technical Lead approved in `agents/reports/2026-10-18-tech-lead-B-30-re-review.md`.

## 5. Evidence missing

- No test for an explicit `/Columns` on a non-xref predictor stream (`OpenDecoded`). It is covered by reading the code only. The criterion is met by the xref case.
- The full suite was not run. Acceptance rests on the targeted `OpenTests` runs. This is not the fuzz run.

## 6. Changes made

`agents/state/backlog.md`: B-30 is closed, and the header paragraph is updated.

## 9. Handoff

Accepted. The Technical Lead commits. B-18 and B-31 stay open. G8 is not passed.
