# W-196 Gate 2 proposal — not adopted

Owner approval is required for the following wording and implementation insertion. This document changes no binding contract. Recommendation: adopt P. N is an explicit alternative, not an additional configurable mode.

## Proposed exact wording

R196-P1: “Extraction and concatenation preserve the page label of each selected source page in output order. A repeated page or repeated input occurrence retains the same label; labels need not be unique. If a source has no PageLabels entry, its source page numbers, starting at 1, are its labels. Input page selections continue to use physical 1-based page numbers, independently of labels.”

R196-P2: “The output rebuilds PageLabels ranges for the selected pages. It preserves standard D, R, r, A and a styles, text prefixes, prefix-only and empty labels, and the source numeric value at each selected page. Gaps, reordering and repetitions start new ranges wherever needed. Adjacent compatible ranges may be coalesced only if every page label is unchanged. No printed page content is read or changed to reconcile numbering.”

R196-P3: “Before output writing, the engine validates each participating input's complete PageLabels number tree within its structural budgets. Cycles, unresolved nodes, invalid keys or ordering, inconsistent Limits, missing index zero, out-of-document range keys, invalid label dictionary types, unsupported styles and nonpositive or overflowing numeric values fail closed. Unsupported structures throw PdfAssemblyNotSupportedException; exhausted structural budgets throw PdfResourceLimitException. No silent fallback to output numbering is permitted. No PageLabels entry means implicit source decimal numbering; a present label dictionary without S means prefix-only, including an empty label.”

R196-P4: “Page labels do not change outline, destination, tag, authentication, encryption-consent or permission rules. Labels are not identifiers for unique pages and no collision reconciliation is performed. The output is a new document and its physical page numbers run from 1 in output order.”

Alternative R196-N (replace P1-P3 entirely if selected): “Extraction and concatenation deliberately omit source PageLabels. Output numbering is decimal 1 through the output page count, independent of each source's labels, prefixes, restarts and physical page numbers. Printed folios within page content remain unchanged. Preserving references to source numbering or supplying a source-to-output mapping is the caller's responsibility. No uniqueness reconciliation or label preservation option is provided.”

## Proposed implementation item after approval

One engine-owned feature item: bounded page-label preservation during N1/N2. Proposed paths: engine label-tree reader/projector and assembly/writer integration under `src/jaytwo.Pdf/Internal/`; affected engine tests and small original fixtures under `test/jaytwo.Pdf.Tests/`; public assembly XML documentation where needed; `ACCEPTANCE.md` and `docs/behavior.md`. Approve precise paths at activation. The primary domain skill would be pdf-core-change. No workflow-layer PDF manipulation or runtime package is needed. No item ID is allocated and no queue insertion is made here.

Acceptance: all final W-196 label sequences via both public terminals; flat and indirect nested trees, gaps/repeats/reordering/occurrence boundaries, unlabeled input semantics, Roman/decimal/prefix-only/empty/Unicode and alphabetic blocks. Add focused malformed/cyclic/overflow/budget tests proving pre-write refusal and unchanged destination bytes. Charge every inspected node/prefix and emitted range to existing structural budgets, with a bounded many-range workload. Verify displayed labels using two independent readers including one other than pypdf for nested trees. Confirm existing page order/content-copy and permission behavior with focused existing tests. Normal incremental operations must not be altered. Add no public label editing API.

If approved, the owner-approved change must replace the dropped-label wording in phase-2 section 3, answer 5.7 and requirement A4, along with corresponding shipped-contract updates at implementation. Exact protected-file wording must be approved; this research does not edit those files or claim an automatic New-to-Exists exception for replacing an existing contract. Estimated effort 3-6 engineer-days; all label support is added work. Release qualification remains a separate activity.
