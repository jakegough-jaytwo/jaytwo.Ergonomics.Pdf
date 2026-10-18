# B-28 rework: bound xref `/Index` and object-stream `/N` and `/First`

## 1. Task and owner

Core Engineer. Rework of B-28 after the Technical Lead refusal in `2026-10-18-tech-lead-B-28-review.md`. No parser rewrite. G8 is not passed, and B-18 remains open.

## 2. Verified facts

1. `/Index` entries, and `/Size` when it supplies the default index, must be integers from 0 to `int.MaxValue`. A real, a negative value, or a larger value throws `PdfParseException`.
2. A subsection whose `start + count` passes the 32-bit object number range throws `PdfParseException`.
3. Before a subsection is read, its count is checked against the decoded bytes left: `count <= remaining / (W0 + W1 + W2)`. A zero total width with a nonzero count is refused. No array of `/Size` is allocated. The loop is bounded by decoded data, which `XrefCap` limits.
4. Object-stream `/N` and `/First` must be direct integers from 0 to `int.MaxValue`. A missing value, a real, a negative value, or a larger value throws `PdfParseException`. The existing `/N` multiplication and `/First` range checks remain.
5. The new facts check that the refusal reason is in the exception message, so a different parse failure cannot satisfy them. Without the source fix, 13 cases failed and the zero-width huge-count cases hung until the 60-second blame timeout. With the fix, all pass in milliseconds.

## 6. Changes made or proposed

- `src/jaytwo.Ergonomics.Pdf/Internal/PdfParser.cs`: `XrefIndexValue` validation in `ReadIndex`, plus range and decoded-byte bounds in `ParseXrefEntries`.
- `src/jaytwo.Ergonomics.Pdf/Internal/PdfEngine.cs`: `ObjectStreamInteger` replaces the unchecked `IntOf` narrowing for `/N` and `/First`.
- `test/jaytwo.Ergonomics.Pdf.Tests/OpenTests.cs`: three theories with 16 cases.
  - `malformed_xref_index_values_are_parse_errors_without_extending_the_file` has 7 cases: negative, real, and out-of-range values, and `start + count` overflow.
  - `huge_xref_index_count_is_bounded_by_decoded_entries_without_extending_the_file` has 3 cases: `/W [0 0 0] /Index [0 2147483647]` with 0 and 3 bytes, and `/W [1 1 1]` with 3 bytes.
  - `malformed_object_stream_count_or_first_is_a_parse_error_without_extending_the_file` has 6 cases: negative, real, and out-of-range values for `/N` and `/First`.
  - Each case expects `PdfParseException` and the refusal reason, finishes in under 10 seconds, and leaves the source bytes unchanged. `AssertMalformedDoesNotExtend` now returns the exception. Existing callers are unchanged.

## 7. Verification run

SDK `10.0.100-rc.1.25451.107`. qpdf 12.4.2, Poppler 26.09.0, and SoftHSM 2 paths were set.

- `dotnet build src/jaytwo.Ergonomics.Pdf/jaytwo.Ergonomics.Pdf.csproj --nologo --no-restore --no-incremental -warnaserror`: succeeded on `net8.0`, `net6.0`, `netstandard2.1`, and `netstandard2.0`, with 0 warnings and 0 errors.
- `dotnet test --filter "FullyQualifiedName~OpenTests"`: `net8.0` passed 47/47; `net6.0` passed 47/47. The test build had no warnings.
- Adjacent xref and object-stream checks (`EncryptionTests`, `ForeignPdfTests`, `ObjectStreamCacheTests`, and `DenseMemoryTests`): `net8.0` passed 44/44; `net6.0` passed 44/44.
- Pre-fix check: the source changes were stashed and the new facts run on `net8.0`. Thirteen failed and three hung, as described in section 2. The stash was then restored.
- `git diff --check`: passed.

Evidence level: internally tested. The full suite was not run. This is not a fuzz run and does not pass G8.

## 9. Handoff

Technical Lead re-reviews. QA Engineer then independently verifies all B-28 regressions.
