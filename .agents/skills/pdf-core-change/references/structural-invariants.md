# Structural invariants

A checklist for parser, decoder, encryption, and writer changes. For each item a change touches, state the rule it follows, the cited source where one applies, and the test that holds it. The limits named here are the code's constants; read the code for current values rather than copying them.

## Conformance versus repair

Tolerance that a cited PDF rule requires is conformance. Example: ISO 32000-1 7.3.10 makes a reference to an undefined object, a free entry, or a mismatched generation resolve as the null object (D-26). That does not authorize general recovery from malformed files. Name the clause, or treat the tolerance as repair and refuse it.

## Cross-reference structure

- Classic, stream, hybrid (`/XRefStm`), linearized, and incrementally updated files. A linearized file is read through its last trailer like any other; first-page hint tables are ignored.
- `/Prev` chains: newest entry wins and cycles are rejected. Within a hybrid section, at any depth of the chain, the classic table is searched first, then its `/XRefStm`, then `/Prev` (ISO 32000-1 7.5.8.4; B-41).
- Trailer inheritance: `/Root`, `/Info`, `/Encrypt`, and `/ID` come from the newest trailer that has them.
- Generations, free entries, and the generation-65535 head of the free list.
- Classic subsection bounds and row endings (CRLF, LF, SP LF, SP CR). `MaxXrefEntries`, `MaxXrefStreamObjects`, and `XrefEntryBudget` bound the allocation by what the file can actually hold. Never allocate an array of `/Size`.

## Offsets and arithmetic

- Offsets are `long`. A preamble before `%PDF-` shifts every offset (header within the first 1024 bytes).
- Every offset, length, count, and index taken from the file is checked before use. `checked` arithmetic or explicit bounds, never a silent wrap.
- Classic-xref output cannot exceed `MaxClassicOffset`; refuse with `PdfTooLargeForClassicXrefException` before writing.

## Objects and streams

- Object-stream `/N` and `/First` are bounded by the decoded length. Offsets inside it are monotonic and in range. An object in an object stream has generation 0.
- `/Length` may be direct, indirect, wrong, or stored in an object stream. A wrong length is a parse error unless a cited rule says otherwise.
- `stream` is followed by CRLF or LF, not CR alone. `endstream` may be preceded by an EOL that is not part of the data.
- Filter arrays apply in order. Predictors apply after the filter they parameterize. Decoded size is capped (`StructuralCap`, `XrefCap`) to bound decompression amplification.
- Trailing bytes after `%%EOF` and multiple `%%EOF` markers are normal for incremental files.

## Security handler

- Revisions 2 through 6, crypt filters `/StdCF` and `/Identity`, `/StmF` and `/StrF` separately.
- `/P` is a signed 32-bit integer; read it as such.
- Plaintext exceptions: signature `/Contents`, the cross-reference stream, and `/Identity` crypt filters. An encrypted xref stream is rejected.
- `/ID` may be absent, short, or a single string; derive keys from what is there and reject only what the algorithm cannot use.
- Public-key handlers are refused.

## Version

The catalog `/Version` overrides the header when it is later. Every key the writer emits must be valid for the declared version. The Arlington PDF Model can check that mechanically; confirm a finding against the declared-version rule before acting on it.

## Resource bounds

- Recursion depth (`MaxTraversalDepth`, `MaxCommentDepth`, parse depth), container size (`MaxContainerEntries`), and token length (`MaxTokenBytes`) are bounded.
- Reference cycles in `/Kids`, `/Parent`, `/Prev`, name trees, and indirect `/Length` are detected.
- Validate a count before allocating for it. Watch quadratic behavior in repeated lookups, object-stream reloading, and xref merging.

## Output

- The original bytes stay a byte-for-byte prefix of every incremental output.
- A failure before the append leaves the destination as documented; partial output is never published as success.

## Evidence

Parser, decoder, encryption, and writer changes need foreign-producer or differential evidence when a suitable independent implementation exists (qpdf, pypdf, pyHanko). Review arithmetic and complexity whenever the input controls counts, offsets, lengths, nesting, or buffer sizes.
