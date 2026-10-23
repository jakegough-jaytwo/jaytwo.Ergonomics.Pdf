# Closing the performance gap with iText

Written for the owner, 2026-10-22. It covers the gaps that W-111's comparison measured (`agents/evidence/itext-comparison.md`), what causes them, and the changes that close them, in order. Each change is a work item: W-114, W-115, W-109, and W-116.

## The gap

In wall time, iText beat this library on one of twelve workloads. The gap is in allocation, and it is on the form paths.

| 500-page form workload | Warm time, this / iText | Allocated, this / iText |
|---|---|---|
| Metadata edit | 0.132 s / 0.007 s (19x) | 127 MiB / 1.5 MiB (85x) |
| Extraction | 0.075 s / 0.076 s | 68 MiB / 12 MiB (5.6x) |
| Sign | 0.099 s / 0.159 s | 105 MiB / 20 MiB (5.2x) |
| Bulk fill | 0.099 s / 0.372 s | 65 MiB / 162 MiB |

Peak heap growth stays small, 6 to 21 MiB against a 128 MiB budget. The cost is garbage: the collector absorbs it now, but it becomes time on larger forms and under load.

## Causes

A probe on the 500-page form (W-111 session, `net10.0` Release, warm):

| Steps | Time | Allocated |
|---|---|---|
| Open | 0.7 ms | 0.4 MiB |
| Open, read the title | 1.2 ms | 1.0 MiB |
| Open, set the title | 43 ms | 45 MiB |
| Open, set the title, save | 90 ms | 87 MiB |
| Open, read every field | 39 ms | 44 MiB |
| Open, read every page | 11 ms | 13 MiB |

1. **Every write walks the whole field tree to find a certification.** `EnsureDocumentChange` and `EnsureAppendAllowed` call `CertificationLevel()` (`PdfEngine.cs` around line 3484). It calls `CollectFields()` and then inspects only the signature fields' `/V`. Setting the title walks once, and the save walks again. A sign walks for the certification check and again to find or name its field.
2. **The walk builds more than certification needs.** `CollectFields()` builds the page tree and an index of every page's `/Annots`, so each field can report its page number. Certification needs neither. On this form, pages alone cost 13 MiB.
3. **Object streams are decoded repeatedly within an operation.** The engine keeps one clean decoded object stream, and the walk cycles across all of them. D-49 measured 4.7 to 7.5 decodes of each stream per operation on this form shape. W-109 is the deferred fix.
4. **Parsing allocates about 24 bytes per decoded byte.** One field walk decodes about 1.8 MB and allocates 44 MiB. Every form path pays this per walk: extraction, fill, sign, flatten, and edit.

## Changes, in order

### 1. W-114: certification and signature state without the full walk, cached per revision

- Answer `CertificationLevel` and `HasSignature` from a walk that visits only the field tree and signature fields. It builds no page or annotation index and creates no `FieldSlot` for other field types.
- Cache the answer per revision. Certification and signature presence change only when this library writes a signature revision (`Sign`, a document timestamp), and those reset the cache. A fill cannot sign a field, flatten never removes a signed field, and no other write adds a `/V` to a signature field. The work item verifies that claim, and every place that could break it, before relying on it.
- The guarantee does not change: the same signature fields are read, with the same DocMDP rules.
- Expected effect: the 500-page metadata edit drops from two full walks to one signature-only walk, roughly 87 MiB to under 30 MiB and 90 ms to under 30 ms. Signing drops one or two walks. These are estimates; W-114 measures them.
- Effort: small. It is in charter and needs no owner decision.

### 2. W-115: cut parse allocations

- Profile allocations on form extraction and the field walk (EventPipe `GCAllocationTick`, or `dotnet-trace` with the GC verbose keyword), then reduce the largest sources. Likely candidates:
  - a string per name token, where an intern table would do;
  - a dictionary per small PDF dictionary, where a small sorted array or list would do;
  - token and number boxing;
  - copies of decoded buffers.
- Each change is measured, and the outputs are byte-identical to before.
- Target: halve allocations per decoded byte. That benefits every form path.
- Effort: medium. The parser is under every fuzz target, so this needs a G8 rerun before W-81, as W-108 did.

### 3. W-109: reuse decoded object streams within an operation (needs your decision)

- Already scoped. Keep every clean object stream the operation touches, up to a hard cap, and release them when the operation returns. This removes the 4.7x to 7.5x repeat decoding.
- D-49 placed it in 1.x, after the release. Pulling it ahead of W-81 is a change to a closed decision, so it is your call. After W-114 and W-115 it may matter less; their measurements will show how much is left.

### 4. W-116: certification detection without walking the fields (needs your decision)

- With W-114 to W-109 done, one field walk per document remains on the first write, where iText does none. Skipping it would mean deciding certification from the catalog `/Perms /DocMDP` (and possibly `/SigFlags`) instead of from the signature fields themselves. That changes a documented certification rule, so it is an owner decision. The consult brief is `agents/reports/2026-10-22-certification-detection-brief.md`.

## Not part of the gap

- The large-file paths are already ahead of iText: dense 1 GiB inspect, edit, and sign are 2.3 to 2.6 times as fast, with 4 MiB or less of heap. iText's 4.4 to 4.6 GB heaps come from its whole-file buffering paths.
- Fill is already 3.8 times as fast as iText.
- The iText interoperability fix, writing a cross-reference stream when appending to a cross-reference-stream file, is W-117. It is a correctness fix, not a performance one.

## Order and gates

W-114, then W-115, then W-109 if approved. Each item reruns the G7 workloads and `make compare-itext`, and records before-and-after numbers in its Closeout and in `agents/evidence/itext-comparison.md`. W-115 and W-109 change code under every fuzz target, so they land before W-81 or repeat its G8 runs.
