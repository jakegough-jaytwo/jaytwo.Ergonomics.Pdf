# W-209 proposal (draft; nothing here is approved, shipped, or applied)

Companion to `findings.md`. Protected files (`AGENTS.md`, `docs/spec/`, `QUALIFICATION.md`, `.agents/skills/`), `ACCEPTANCE.md`, `docs/behavior.md`, product code, tests and the queue in `agents/state/current.md` are **not edited** except that `current.md` records completion. This file holds the proposed dispositions and exact wording for the owner's Gate 2 decision.

## 1. Disposition

| Candidate | Proposed disposition | Reason |
| --- | --- | --- |
| Keep FlateDecode, its predictors and both caps | **Retain.** | Every required stream in the corpus is Flate or unfiltered (335 of 335). Not a candidate. |
| ASCIIHexDecode, ASCII85Decode, LZWDecode, RunLengthDecode (shipped, tested) | **Retain for now; no removal recommended without out-of-corpus evidence.** | Zero required-stream use in 50 of 51 PDFs measures only that this corpus cannot justify them, not that no business PDF needs them. A refusal at the cross-reference or object-stream site makes the whole document unreadable, and the public API gives callers no encoded-bytes path to recover (findings section 4). The saving is real (726 of 1,547 lines, 46.9% of `PdfStreamDecoder.cs`) but is implementation and test size, not user-visible behavior. |
| Abbreviations `Fl`, `LZW`, `RL`, `AHx`, `A85` in stream dictionaries | **Change (smallest bounded removal): accept only the full filter names.** Needs owner approval. | They are inline-image abbreviations (ISO 32000-1 Table 94, 8.9.7), not valid in stream dictionaries (7.4.1); 0 abbreviated streams anywhere in the corpus; AGENTS.md's quirk list requires a real fixture and there is none. Loss is limited to nonconforming writers. Effort 0.25 to 0.5 day (inferred). |
| ASCII85 and ASCIIHex hardening (unbounded array decoders, two lenient ASCII85 inputs) | **Route to the owner as separate candidate items** (findings section 5). | Applies only if those filters are retained; if either is removed later, the item is moot. |

If the owner prefers a smaller decoder surface than the evidence alone supports, the order of least expected loss is: abbreviations; RunLength; ASCIIHex; ASCII85; LZW (highest legacy exposure, kept in ISO 32000-2). Each tier is independently removable at the four sites with `PdfUnsupportedFilterException` as the unchanged error. Tiers B to E below are drafted for that decision; none is recommended now.

## 2. Exact proposed wording (unadopted)

Common to every tier: update `ACCEPTANCE.md` and `docs/behavior.md` in the same commit as the code (D-43); the commit message lists each such edit (D-47). New tests assert `PdfUnsupportedFilterException` (message `The stream filter /X is not supported.`) at all four sites for each removed filter, plus an unaffected Flate baseline; the removed filters' own tests and fuzz seeds are deleted.

### Tier A: abbreviations (recommended Change)

AGENTS.md, replacing the sentence "Decode `LZWDecode`, `FlateDecode`, `RunLengthDecode`, `ASCIIHexDecode`, and `ASCII85Decode` when a stream's bytes are required, including the abbreviations `LZW`, `Fl`, `RL`, `AHx`, and `A85`." with:

> Decode `LZWDecode`, `FlateDecode`, `RunLengthDecode`, `ASCIIHexDecode`, and `ASCII85Decode` when a stream's bytes are required. Filter names are the full names of ISO 32000-1 Table 6; the inline-image abbreviations are not accepted in a stream dictionary.

(Only the "including the abbreviations `LZW`, `Fl`, `RL`, `AHx`, and `A85`" clause of the current sentence changes; re-read the exact AGENTS.md text at adoption.)

`ACCEPTANCE.md` line 79, replacing "The abbreviations `LZW`, `AHx`, `A85`, and `Fl` are accepted." with:

> A stream whose `/Filter` names an inline-image abbreviation (`Fl`, `LZW`, `RL`, `AHx`, `A85`) throws `PdfUnsupportedFilterException`.

`ACCEPTANCE.md` line 80, deleting "The abbreviation `RL` is accepted." `docs/behavior.md` line 43, replacing "(and the abbreviations `Fl`, `LZW`, `RL`, `AHx`, and `A85`)" with nothing, and appending: "Inline-image abbreviations are not accepted in stream dictionaries."

### Tier B: RunLengthDecode

AGENTS.md: delete `RunLengthDecode` from the decode list (and the `RL` abbreviation if Tier A is not taken). `ACCEPTANCE.md`: delete line 80; the `A85`+`LZW` chain sentence in line 79 is unchanged. `docs/behavior.md` line 43: "Filters other than Flate, LZW, ASCIIHex, and ASCII85 throw when their bytes are actually required."

### Tier C: ASCIIHexDecode and ASCII85Decode

AGENTS.md: delete both from the decode list. `ACCEPTANCE.md` line 79: delete "`/Filter [/ASCII85Decode /LZWDecode]` decodes ASCII85 first." `docs/behavior.md` line 43: "Filters other than Flate and LZW throw..." (combined with B: "Filters other than Flate and LZW").

### Tier D: LZWDecode

AGENTS.md: delete `LZWDecode` from the decode list. `ACCEPTANCE.md` line 79: delete the first sentence ("`LZWDecode` is inflated, including `EarlyChange`...") and the chain sentence if C is also taken; keep "A decoded stream above the caller cap throws `PdfObjectTooLargeException`" as a Flate statement. Line 22 stays (page content streams are never decoded). `docs/behavior.md` line 43: "Filters other than Flate throw when their bytes are actually required." Add under the attachments row: "An embedded file whose filter is not Flate is not returned; `Open` throws `PdfUnsupportedFilterException` and the library offers no way to read its encoded bytes."

All four tiers (B+C+D plus A): AGENTS.md decode bullet becomes "Decode `FlateDecode`, with its PNG predictors, when a stream's bytes are required."

## 3. Effort ranges and basis (all inferred; no spike was run)

| Tier | Source removed | Effort |
| --- | --- | --- |
| A abbreviations | `Canonical` and callers, about 25 lines | 0.25 to 0.5 day |
| B RunLength | 123 lines plus `RunLengthTests` (226 lines) | 0.5 day |
| C ASCIIHex + ASCII85 | 367 lines plus mentions in `IndirectFilterTests`, `HardeningTests` | 0.5 to 1 day |
| D LZW | 236 lines plus `LzwTests` (379 lines) | 0.5 to 1 day |
| A to D together | 726 lines plus about 40 dispatch lines | 1.5 to 2.5 days |

Basis: brace-matched line counts (`loc.json`), four call sites each with one dispatch, the unsupported-filter exception already shipped, and the replacement tests being four-site parametrized cases. Not included: owner review, a deprecation period, or any migration tooling. Overlapping savings: none with other simplification items except W-149/W-168 work-budget accounting for the ASCII85/ASCIIHex array decoders (findings section 5.2).

## 4. Costs and risks of the recommendation

- **Business use kept:** reading PDFs, of any age, whose cross-reference streams, object streams, XMP or attachments use these filters (none observed in the corpus; possible in old PDF 1.3/1.4 attachments and XMP).
- **Loss accepted (Tier A only):** nonconforming writers that put inline-image abbreviations in stream dictionaries (0 observed).
- **Recovery and credential burden:** none for the retention; for any removal, an external tool such as qpdf or pypdf to re-encode the file before opening, plus operating that tool; no credentials.
- **Responsibility deleted / retained:** retained now; Tiers delete up to 726 lines of decoder and about 600 lines of dedicated tests.
- **Residual risk of retention:** the decoder surface and its two spec leniencies (ASCII85 overflow and lone trailing character) and unbounded array decoding remain until hardened or removed.
- **Residual risk of removal:** a refusal at the cross-reference or object-stream site is total (document unreadable); at XMP or attachment sites it is local. No in-library way to obtain the encoded bytes.
- **Effort of this recommendation:** 0 for retention; Tier A 0.25 to 0.5 day if approved.
