# Feedback on `agent-role-assignments-review.md`

Date: 2026-10-18
From: author of `role-assignments-review.md`

## Summary

This is the most useful of the three reviews on *how the team runs*. It is the only one that answers who creates a task, who picks the implementer, and who closes the task. Keep the linear operating contract (Finding 3), the pre-edit concurrency record (Finding 7), and the standard report format (Finding 8). They are the parts most worth carrying into the revised roles.

It is the weakest of the three on *evidence*. Most findings are stated in general terms. The reader is asked to trust that conflicts exist without being shown many of them. Below are suggested changes, ordered by impact.

## What to keep

- **The operating contract (lines 95-105).** It is clear, linear, and says that routine work needs no second PM approval once a task is defined. Neither other review gets this right.
- **The timestamp line in the Signing notes (line 209).** "Timestamp-response checks performed while creating a signature are allowed, while a public signature-validation subsystem is not." That is exactly where the charter boundary sits.
- **The dependency finding (lines 53-67).** The citations are accurate (`AGENTS.md:9` and `:21`, and the role line ranges). The framing is also correct: an exception to the rule is a charter change for the owner, not a dependency review.
- **Prompt shape and revision order (lines 219-244).** Good, and directly usable.

## What to change

### 1. Parts of the review are now out of date

The repository moved after this review was written:

- `AGENTS.md` now requires Poppler for `AppearanceRenderTests` and SoftHSM 2 for `SigningInteropTests`, in addition to qpdf and pyHanko/pypdf.
- `ACCEPTANCE.md` "Foreign files" now covers several gaps the roles treated as open:
  - chain trust from the root alone;
  - a live `HttpTimestampAuthority` round trip that pyHanko trusts;
  - SoftHSM signing through `IPdfExternalSigner`;
  - DocMDP `P` 1 and `P` 2 results checked by pyHanko;
  - Poppler text extraction and raster checks;
  - a dense file over 32 MiB with peak managed heap under 8 MiB.
- The directory is now a git repository.

These lines no longer hold:

- **QA notes, line 217:** "required baseline tooling—currently qpdf and Python with pyHanko". The required set is now qpdf, Python with pyHanko and pypdf, Poppler, and SoftHSM 2. That changes the recommendation itself. Poppler and SoftHSM are no longer optional, and pdfsig, PDFBox, MuPDF, and Acrobat remain optional.
- **Finding 7 and the concurrency protocol** assume branches and worktrees are available "if used". They now are. You can make worktrees the default rather than a maybe.

### 2. The seventh role costs more than it saves

The "Document Features Engineer" (lines 80 and 188-189) would own `Metadata/PdfMetadata.cs`, `Metadata/XmpEditor.cs`, and `Attachments/`. That is a small surface. Almost every change there also runs through `PdfEngine.cs`, the encryption gates, and DocMDP, which belong to Core. A separate seat adds a handoff on every task without removing any shared-file contention.

Make the "explicitly assign to Core" fallback the primary recommendation. `role-assignments.md` reaches the same conclusion. With two reviews disagreeing, the owner has to pick one.

### 3. The ownership matrix reproduces the problem from Finding 6

Finding 6 says "coordinate" is not ownership. The matrix then splits DocMDP/FieldMDP across three roles (line 187): Signing owns the policy, Core supplies the parsing, and AcroForm enforces fill decisions. That is the same diffuse ownership in table form. Name one implementation owner. Signing is the natural one, with the others consulted.

The matrix also lists the Technical Lead as "Accountable" on every engineering row. The TL notes (line 201) call that seat overloaded, and the matrix leaves the overload in place. Consider making specialists accountable for their own areas and leaving the TL accountable for integration and cross-cutting rows only.

### 4. Name the stale and charter-reopening items specifically

Finding 4 says historical baselines should be removed, but never says which items are already closed. An agent revising the roles from this review would not know what to delete. The other two reviews list them. Add:

- **Core role, lines 244-262.** These lines invite an xref-stream writer and removal of the 8 MiB appendix. Both conflict with `AGENTS.md:11` and `:14`.
- **Signing role, lines 300-304.** "Historically rejected all modifications when `/DocMDP` was present." That is already implemented and tested.
- **AcroForm role, lines 332-336.** "Approximate character widths." Adobe Helvetica advances are already specified and tested.
- **Tech lead, line 200.** "`IPdfExternalSigner` or its successor abstraction."
- **QA role, lines 427, 429, 542, 549.** Nonce mismatch, untrusted TSA, cancellation, and non-seekable outputs. The library cannot produce these scenarios, and the untrusted-TSA case requires the trust validation the charter excludes.

### 5. Missing findings the other reviews caught

- **House style.** No role mentions StyleCop-as-errors, XML docs, one type per file, file-scoped namespaces, the `_` prefix, or the scoped SA1201/1202/1204 pragma policy. An agent could "fix" member order in the engine files where the order is intentional.
- **Naming drift.** `project-manager.md` defines a *Product* Manager. The signing and QA seats are called different names in different files.
- **Target frameworks.** The library also ships `netstandard2.0` and `netstandard2.1`, which the tests never exercise.
- **Doc ownership.** No role owns updates to `README.md`, `ACCEPTANCE.md`, and `AGENTS.md` when behavior changes.
- **A real code gap the roles point at.** Neither `HttpTimestampAuthority` nor `CmsDetachedSignatureProvider` compares the returned token's message imprint with the request. The signing role's "do not accept an arbitrary ASN.1 response" is a valid task. Calling it out would show the boundary at line 209 in practice.

### 6. Minor

- "3,280 lines" counts editor lines. `wc -l` reports 3,274 because the files lack trailing newlines. Either figure is fine; it only matters if someone checks.
- The executive assessment and the final verdict make the same point twice. One of them can go.
