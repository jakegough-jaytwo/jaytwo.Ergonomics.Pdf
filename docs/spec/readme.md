# Specification drafting notes

This file holds details to transfer from the [charter](../CHARTER.md) and [supported capabilities](../SUPPORTED.md) into the new operation specifications. It is a drafting checklist, not an adopted operation contract or a claim of implemented support.

The charter defines purpose and governing principles. SUPPORTED.md defines durable capability commitments and product boundaries. The specifications will define target inputs, outcomes, preservation rules, limitations, and failure behavior. The [behavior reference](../behavior.md) describes shipped behavior; [acceptance criteria](../../ACCEPTANCE.md) record required outcomes and verification.

The owner moved the previous specifications to `docs/spec.old/`. Consult them as historical input when drafting; do not automatically adopt their wording into the new specifications.

## Transfer process

For each item below, draft and review the relevant operation contract before removing the detailed wording from its source. Keep the capability commitment or product boundary in SUPPORTED.md. Transferring detail must not silently narrow scope or weaken shipped guarantees. Record the destination section here when the transfer is complete.

## Details to transfer from SUPPORTED.md

These are candidates for moving or shortening, not instructions to delete whole capability rows.

| Current location | Detail for the specification | Commitment to retain in SUPPORTED.md |
| --- | --- | --- |
| Intended capabilities: Document opening | Synchronous/asynchronous entry points, source stability, ownership, and authentication outcomes. | Opening existing PDFs from streams and random-access sources. |
| Intended capabilities: AcroForm appearances | Font embedding/subsetting, character and script restrictions, and outcomes for unsupported characters or shaping. | Text and choice-field appearances using caller-supplied TrueType fonts within a documented subset. |
| Intended capabilities: AcroForm flattening | Eligibility and the exact appearance, permission, and signature restrictions. | Flattening eligible editable fields into page content. |
| Intended capabilities: XFA-bearing forms | Classification, operation-specific acceptance/refusal, and explicitly documented removal consequences. | Supported hybrid AcroForm use and defined handling of XFA-bearing inputs; XFA packet editing remains excluded. |
| Intended capabilities: PDF/A inputs | Which relevant structures each operation preserves, and its limitations. | Accepting PDF/A inputs under normal operation contracts and preserving relevant structures where practical, without a conformance guarantee. |
| Intended capabilities: Page rotation | Accepted angles and their interpretation. | Page rotation. |
| Intended capabilities: Page extraction and Concatenation; Support boundaries | Exact selection semantics and structure preservation, omission, and refusal rules. | Selected-page extraction and combination, including requested order, repetition, and reordering. |
| Intended capabilities: Binary attachments | Caller limits, filename safety helpers, and extraction behavior. | Document-level attachment management and streaming reads/extraction. |
| Intended capabilities: Image placement and Image to PDF | Placement controls, page dimensions, units, defaults, and parameter behavior. | Limited image-to-PDF creation and image placement, including position, size, rotation, opacity, and foreground/background placement. |
| Intended capabilities: Signature and revision inspection | CMS inspection depth and revision/coverage reporting details. | Inspection of signatures, embedded claims, and revisions, without cryptographic validation or trust assessment. |
| Image inputs and shortcuts | JPEG/PNG variant restrictions, transparency (alpha and transparent colors), color profiles, decoding limits, and coordinates relative to page rotation/cropping. | JPEG/PNG inputs, the limited image exception, and the engine/workflow responsibility split. |
| Support boundaries: write authorization | Operation-specific password permission, DocMDP, FieldMDP, and Reader usage-rights preservation/removal rules. | Supported writes respect applicable permissions and document restrictions. |
| Support boundaries: password and permission operations | Security schemes, authentication, authorization, encryption interaction, and output strategy. | Opening/modifying password-protected PDFs and reading/changing supported security permissions; permission inspection alone does not fulfill permission-changing scope. |
| Support boundaries: foreground/background images | Painting-order semantics and visibility limitations. | Foreground/background image placement. |
| Support boundaries: timestamps | What timestamp inclusion establishes and what callers must assess separately. | Host-controlled signing and timestamp support; signature trust validation remains excluded. |
| Resource and failure contracts | Limits by operation; destination-specific outcomes before/during/after writing; unchanged-output, partial-output, restoration, publication, rollback, and retry guarantees. | Predictable resource use and explicit failure behavior, without a blanket file-size support promise. |

## Explanations to move out of the non-goals

Keep the exclusions themselves. Relocate their technical explanations alongside the relevant operation contracts:

- Signing: separate document timestamps do not establish PAdES B-LTA conformance. Keep PAdES B-LT/B-LTA construction excluded.
- Attachments and PDF/A inputs: attachment support and associated-file keys do not establish PDF/A-3 compliance. Keep PDF/A conversion, validation, and conformance guarantees excluded.
- Saving: distinguish incremental appending from rewriting an input file in place. Keep in-place rewriting excluded.

Keep the other enduring non-goals, including rendering, text extraction, general authoring/layout, redaction, signature validation, general repair, XFA packet editing, JavaScript, public-key encryption, adding encryption, general annotation editing, and page-level file attachment annotations. Their specificity defines product boundaries rather than operation mechanics.

## Charter review

No current charter passage needs removal solely to make room for the spec. Retain permissive licensing, deliberate scope, competitive reliability/performance, architectural principles, production readiness, and governance.

The specifications should make those principles concrete through operation-specific preservation, resource, and failure contracts. Benchmark methods, selected comparison tools, workloads, and numeric budgets belong in verification plans and evidence; the charter retains the quality standard and representative-workload principle.

## Companion cleanup when contracts are adopted

- Add the specification's role to SUPPORTED.md's introductory document map.
- Replace repeated instructions to write detailed contracts with concise links to the adopted contracts.
- Keep intended capabilities and non-goals independent of implementation status, delivery order, and current limitations.
- Update this checklist with completed transfers and destination sections so no detail is lost during cleanup.
