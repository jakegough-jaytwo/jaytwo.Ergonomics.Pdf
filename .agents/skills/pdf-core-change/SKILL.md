---
name: pdf-core-change
description: Change the PDF parser, object model, stream decoding, encryption, incremental writer, pages, metadata, or attachments. Use when implementation work touches those areas.
---

# PDF core change

Follow `AGENTS.md` as the binding specification. Do not work from memory of the supported xref forms, filters, encryption revisions, or caps. Read only the applicable `ACCEPTANCE.md` and `README.md` sections and the current task record.

## Scope

This workflow covers parsing and indirect-object resolution; xref and object streams; filters and predictors; standard security; incremental serialization; page trees; metadata; attachments; and resource behavior.

Use `pdf-acroform-change` for field semantics, appearances, or widget geometry. Use `pdf-signing-change` for CMS, timestamping, signature revisions, or certification enforcement. Load those skills only when the task crosses the boundary.

For xref, object-stream, `/Length`, filter, security-handler, version, or resource-bound changes, read `references/structural-invariants.md` and work through the items the change touches.

## Working rules

- Answer three questions separately: is the file valid PDF, does it use structures this library supports, and can the requested operation be performed on it safely? A file can pass the first and fail the third.
- Keep page content streams opaque. Read only the structures the operation needs.
- Memory follows the operation's working set, not source-file size. Sequential reads, buffering, retained parsed objects, scratch buffers, caches, and dirty objects are different costs; know which one a change adds. A full-file pass is fine when the operation needs one. A full-file buffer is not.
- Treat all PDF structures as hostile input. Bound recursion, counts, offsets, decoded sizes, and allocations. Reject cycles and overflow with document exceptions.
- Prefer an explicit limit and a precise exception to unlimited recovery. Do not add repair heuristics so a malformed fixture passes. Any recovery behavior must be intentional, documented, and tested on its own.
- Preserve the original bytes as the prefix of incremental output and keep offsets as `long`.
- On an unsupported structure, leave the input untouched, publish nothing misleading, and say precisely what was unsupported. Do not publish partial output beyond the documented guarantee.
- Measure before adding a cache or changing resource behavior: name the workload and metric, measure, fix the bottleneck, measure again, and confirm nothing else regressed. Record the tradeoff. Releasing a cache can cut memory and multiply decompression.
- Keep Info and XMP consistent. Malformed XMP must not break `Open`. Stream attachments through bounded buffers. Apply the encryption and certification gates to both.

## Done

- A focused regression test covers each confirmed defect.
- Independently produced inputs cover changes to structure, encryption, or interoperability, not only files from this repository's builder.
- Resource-affecting changes have a before and after measurement.
- The original bytes are still a prefix of every incremental output.

A limit that looks wrong for the product, rather than protective, is an owner decision. Bring the measurement. Finish with `pdf-change-verification`.
