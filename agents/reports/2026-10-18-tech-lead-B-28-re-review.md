# B-28 re-review after rework

## 1. Task and owner

Technical Lead. Second review of B-28: the uncommitted rework in `PdfParser.cs`, `PdfEngine.cs`, and `OpenTests.cs`, reported in `2026-10-18-core-engineer-B-28-rework.md`, with QA sign-off in `2026-10-18-qa-engineer-B-28.md`. No code, test, or state edit. G8 is not passed. B-18 remains open.

**Approved.** The defect from `2026-10-18-tech-lead-B-28-review.md` is fixed and has regressions.

## 2. Verified facts

- **`/Index` and `/Size`.** `PdfParser.XrefIndexValue` accepts only integers from 0 to `int.MaxValue`. Reals, negatives, and larger values throw `PdfParseException`. `ParseXrefEntries` refuses `start + count > int.MaxValue + 1`, so `start + i` cannot overflow. Before each subsection it requires `count <= (decoded.Length - cursor) / entryWidth`, and it refuses a nonzero count when the total width is zero. The loop is now bounded by the decoded bytes, which `XrefCap` limits. Nothing is allocated from `/Size`. `cursor <= decoded.Length` holds because `ReadField` already checks it. Hybrid `/XRefStm` goes through the same `ParseXrefEntries`.
- **No lenient path lost.** Before the rework, a subsection with nonzero width and too few bytes already threw "Xref stream entry is truncated." The new byte check refuses the same inputs earlier. Only all-zero `/W` with a nonzero count used to "succeed". It produced type-1 entries at offset 0, which is not a usable xref.
- **`/N` and `/First`.** `PdfEngine.ObjectStreamInteger` replaces the unchecked `IntOf` narrowing. The existing `/N <= int.MaxValue / 2`, `/First <= data.Length`, and header-count checks remain, so the header loop cannot index past `parts`.
- **Tests.** Three new theories with 16 cases. Each expects `PdfParseException`, asserts the refusal reason, stays under 10 seconds, and checks that the source bytes are unchanged. QA confirmed that 13 cases fail and the zero-width cases hang without the rework.
- **Charter.** No new package, no public API change, no excluded capability, and no buffering beyond the existing decoded structural stream. `README.md` and `ACCEPTANCE.md` need no change because no documented behavior moved.

### Indirect `/N` and `/First`: accepted, not a regression

`PdfObject.Dict.Get` does not resolve references, so `IntOf` used to read an indirect `/N` or `/First` as 0:
- Indirect `/N`: the map was empty, and `LoadFromObjectStream` threw `PdfParseException` ("Object n was not found in object stream m").
- Indirect `/First` with a positive direct `/N`: the header was read from zero bytes, and the code threw `PdfParseException` ("Object stream header is truncated").

Both forms were already refused with the same exception type. The rework refuses them earlier with an accurate message. No file that opened before fails now.

On PDF semantics, ISO 32000 Table 16 lists `/N` and `/First` as integers. Unlike the xref-stream keys in Table 17, it does not require them to be direct, so an indirect value is technically conformant. Resolving it would be new read support, with its own cycle risk through the object-stream loader. That is outside a bounding task. QA and I know of no producer that writes it. Disposition: an existing limitation, recorded here. If a real file needs it, the Product Manager can open a backlog item.

## 4. Defects confirmed

None in B-28.

Outside B-28, confirmed from the code but not executed: `PdfParser.EnsureXrefColumns` sums `/W` with unchecked casts before `ReadWidths` validates it, and writes the sum into `/Columns` when a PNG predictor has no `/Columns`. `PdfStreamDecoder.UnpredictPng` allocates `previous = new byte[rowBytes]` before it checks that any row exists. Take `/W [268435455 0 0]` with `/Predictor 12` and empty decoded data. The decoder allocates about 256 MiB, and only then does `ReadWidths` throw `PdfParseException`. An explicit `/Columns 268435455` on any predictor stream does the same, so the root cause is the predictor `/Columns` bound, not B-28's paths. This does not block B-28. I recommend that the Product Manager log a new item for the Core Engineer, or feed it to B-18.

## 5. Evidence missing

These do not block approval.
- The seven original B-28 facts assert only the exception type. QA's probe shows the intended guard fires today. Adding reason assertions is an optional follow-up.
- This is not a fuzz run. G8 is not passed.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`. The qpdf 12.4.2, Poppler 26.09.0, and SoftHSM 2 paths were set as the task specifies.

- `dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj --no-incremental -warnaserror`: `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0` succeeded, with 0 warnings and 0 errors.
- `dotnet test -f net8.0 --filter "FullyQualifiedName~OpenTests" --blame-hang-timeout 60s`: 47/47 passed. On `net6.0`: 47/47 passed.
- `ObjectStreamCacheTests|ForeignPdfTests|EncryptionTests`: 43/43 passed on `net8.0` and 43/43 on `net6.0`.
- `git diff --check`: clean.

Evidence level: internally tested. Not a gate pass.

## 8. Decisions needed

None from the owner. The Product Manager decides whether to log the predictor `/Columns` allocation as a new item.

## 9. Handoff

The Product Manager accepts B-28. The Core Engineer may optionally add reason assertions to the seven original facts. G8 remains unpassed, and B-18 remains open.
