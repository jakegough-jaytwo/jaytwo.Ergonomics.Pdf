# R3 assembly proposal

2026-10-24. For the owner's approval of wording. It answers the ten interview questions in `docs/spec/phase-2.md` section 5 for N1 (extract pages) and N2 (concatenate), and proposes the replacement text for sections 2, 3, and 5 of that spec. The owner approved it with the corrections in section 3, and D-61 records the decision.

Input: two independent outside experts, with no access to the code, answered the interview in three rounds. Both agreed on every item in section 1. They disagreed on seven link and associated-file points in round two and four more in round three; section 2 records how each settled. The owner approved adopting the converged answers as the direction on 2026-10-24. The wording is in the spec (section 3).

N3 to N5 stay in R5 (D-54). Nothing here changes them.

## 1. Answers to the ten questions

| # | Question | Answer |
| --- | --- | --- |
| 1 | Bookmarks | Drop `/Outlines`. Rebasing is a later feature. |
| 2 | Links | Keep a whitelist of internal and external links (section 3, A6 to A9). Drop the rest. |
| 3 | Field name collisions | Add one synthetic top-level parent field per input occurrence. No automatic flatten. Refuse forms whose scripts, calculations, or XFA cannot be preserved. |
| 4 | Accessibility tags | Drop `/StructTreeRoot`, clear `/MarkInfo /Marked`, remove `/StructParents` and `/StructParent`. |
| 5 | Document-level data | No first-input-wins rule. New minimal `/Info`, new `/ID`. Omit XMP, document attachments, viewer preferences, `/OpenAction`, catalog `/AA`, the JavaScript name tree, and `/Perms`. A nonempty `/AF` is refused. |
| 6 | Output encryption | The output is always unencrypted. An encrypted input is refused unless the caller sets an explicit option. The option applies when the caller authenticated as owner, or as user with the permissions in Q9. Re-encrypting the output is deferred. |
| 7 | Page labels | Drop `/PageLabels`. Preserving them ranks above outlines in the backlog. |
| 8 | Output format | Header at least `%PDF-1.5`, chosen before the first byte. Classic xref table when every offset fits ten digits; otherwise a cross-reference stream. Never an object stream. Offsets are `long`. |
| 9 | Permissions | Owner authentication bypasses `/P`. User authentication: R2 needs bits 4 and 5; R3 and later need bits 5 and 11. Checked for every input. Bit 3 (print) and bit 10 (accessibility) are not part of the rule. |
| 10 | Memory | The old-to-new map is acceptable with explicit budgets and a dedicated resource-limit exception. No spill to disk. |

The permission rule is a conservative product policy. ISO 32000 defines no bit for building a new file from selected pages.

## 2. Points that took a second or third round

Settled by both experts:

- **Link whitelist.** A link is rebuilt from approved entries, never cloned.
- **Duplicated pages.** A link to a different page targets that page's first occurrence in the same input occurrence. A link to its own page targets its own copy. A target missing from the occurrence drops the link. Concatenating one file twice makes two occurrences. Not configurable.
- **`/Next`, `/IsMap true`, `/AA`.** The whole link is dropped.
- **`/AP`.** A valid appearance is copied with its resources under budgets.
- **Link option.** Two states, `PreserveSupported` (default) and `Drop`.
- **Result.** A small object with reason-coded counts.
- **Layers and XFA.** A nonempty `/OCProperties` and dynamic XFA are refused. Dropping layer configuration could expose hidden content.

Settled in round three (both experts chose these):

- **Relative URIs** resolve with RFC 3986 against a valid absolute `http` or `https` catalog `/URI /Base`, with no user-info. Otherwise the link is dropped.
- **An unsupported or over-budget `/AP`** omits that annotation. The decision comes before any output for it. A failure after output has begun aborts the operation.
- **Non-catalog `/AF`** is refused when found in a bounded walk of the selected pages.

One point is my call, not the experts': the `/AF` walk skips the structure tree, because this release drops it. That leaves tagged associations on structure elements undetected, and the spec says so.

## 3. The wording

The owner approved the direction on 2026-10-24 and forwarded a review that required corrections to A1, A2, A3, A5, A7, A8, A9, A12, A13, and A15. The corrected wording is in `docs/spec/phase-2.md` sections 2, 3, 5, and 6, applied by W-160. The corrections are:

- **A1, A2.** An input occurrence is defined once: one source document and an ordered selection of pages, which may repeat a page. `3, 3, 7` is one occurrence, not three.
- **A3.** Every signature field and widget is omitted, signed or not, which replaces "empty or removed".
- **A5.** The `/AF` guarantee is limited to the inspected scope and says the structure tree is not traversed.
- **A7, A8.** A link with any `/AA` is omitted whole. An absolute `http` or `https` URI with userinfo is unsupported, as well as a relative one.
- **A9.** A local `/AP` limit omits the annotation. A global limit, an integrity failure, or a failure after the annotation began aborts.
- **A12.** The header is no lower than the effective version of any retained feature, as well as no lower than 1.5.
- **A13.** The memory model names the page-instance mappings, cross-reference state, and buffers, not just the map and one object.
- **A15.** Fixtures need documented provenance and redistribution rights, and a focused regression fixture is not a corpus member.

## 4. Decision

D-61 records the answers in section 1 and the settlements in section 2, and adopts the N1 and N2 sections of `phase-2.md`, as the charter requires before any Phase 2 work starts.

## 5. Not decided

- Re-encrypting the output.
- Preserving outlines, page labels, tagged structure, or layers.
- Merging document attachments.
- Per-page bookmark rebasing.
- A compose builder beyond `PdfWorkflow.Combine` (R3 scope says "if justified by use cases").

## 6. Next steps

W-160 applied the wording and recorded D-61. W-161 to W-166 implement it, ordered after the hardening backlog in `agents/state/current.md`.
