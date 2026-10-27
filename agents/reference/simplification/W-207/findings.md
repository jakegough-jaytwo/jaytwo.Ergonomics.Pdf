# W-207 findings: unused copier policy modes

Revision: `34108778da7d6369b693ec7a255ab3f3f08eb146`. Caller inventory from `graphify query` and a repository-wide search of `*.cs` (src, test, framework-lane and fuzz assemblies), confirmed at the source lines cited below.

## Result

`Remap` and `Reject` are unused in production. `Remap` has one test-only caller; `Reject` has no caller anywhere. Removing them, with the decision fields only they read, is output-identical by construction and keeps every graph safeguard.

## Inventory

`src/jaytwo.Pdf/Internal/PdfTypedObjectCopier.cs`:

| Element | Responsibility | Production callers | Test callers | Disposition |
| --- | --- | --- | --- | --- |
| `CopyKind.Copy`, `CopyDecision.Copy(childStructure)` | policy selection | `PdfPageExtractor.Policy` L356, L363, L375 | default policy; `NewFileWriterTests` L78 | Retain |
| `CopyKind.Omit`, `CopyDecision.Omit()` | policy selection | `PdfPageExtractor.Policy` L357, L364, L374 | `NewFileWriterTests` L76 | Retain |
| `CopyKind.Remap`, `CopyDecision.Remap`, `CopyDecision.Value`, `Fill` L150-L152 | policy selection | none | `NewFileWriterTests.copier_applies_typed_omit_and_remap_rules` L77 | Remove |
| `CopyKind.Reject`, `CopyDecision.Reject`, `CopyDecision.Message`, `Fill` L145-L148 | policy selection | none | none | Remove |
| `CopyContext.Value` | policy input | never read (`Policy` reads `Structure`, `Key`, `Dictionary`) | never read | Remove |
| `ChildStructure` | structure typing | page/annotation children become `resource` (L356, L363) | none | Retain |
| `SourceObject` key (store identity, object number, generation) L54, L198 | source-qualified identity | every reference copy | `copier_registers_before_children_and_qualifies_the_map_by_source` | Retain |
| `Register` before `Fill` L60-L65 | cycle registration | every reference copy | same test; `resource_inspection_bounds_cycles_and_depth_before_output` | Retain |
| `CopyReference`/`CopyValue` recursion | reference remapping | every copy | all copier tests | Retain |
| `CopiedBody` and Crypt filter handling L117-L176 | raw stream ownership | every stream | `raw_image_stream_bytes_are_unchanged...`, `sparse_stream_is_copied_with_a_bounded_buffer` | Retain |
| `CountMappedObject`, `AddStructuralBytes`, `CheckDepth` | budgets | every copy | `every_new_file_budget_raises_the_dedicated_error` | Retain |
| optional `policy` parameter (null means copy all) | test convenience | none: production always passes `Policy` (`PdfPageExtractor` L145) | five test constructions | Retain (see below) |
| `Copy(source, reference, structure)` entry point | reference entry | none: production uses `CopyDirect` only (`PdfPageExtractor` L151, L189, L221; `PdfAssemblyForms` L124, L291, L359) | six test calls | Retain (see below) |

Measured: there is exactly one production policy, `PdfPageExtractor.Policy` (L351-L376), and it returns only `Copy` or `Omit`.

## Observations that bound the removal

- `Policy` refuses catalog, page-tree, page and signature dictionaries by throwing `PdfAssemblyNotSupportedException` directly (L370), not through `Reject`. Rewriting it to use `Reject` would change the exception type from `PdfAssemblyNotSupportedException` to `PdfException`, so `Reject` is not a latent consumer. The same refusal runs earlier in `PdfAssemblyInspection` L84, before output, so the policy throw is a late second guard. Whether it is still reachable after inspection is inferred, not proven here; it is outside this removal and stays.
- Occurrence locality does not come from the source-qualified map. `PdfPageExtractor.Concatenate` creates a new extractor per input (L89), and `Import` creates a new copier per call (L145), so a document given twice gets independent copies even though both occurrences share one `PdfObjectStore`. Within one copier, production copies only from one store, so store qualification is a safeguard, not current behavior. A future shared copier across occurrences would break occurrence-local fields (W-164); that is a reason to keep both the per-occurrence copier and the qualified key.
- `Remap` bypassed copying: its value went into the output unchanged. A remap value containing a source `Ref` would have leaked a source object number into the new file. Removing it removes that hazard rather than adding one.
- The default null policy and the `Copy(Ref)` entry point are test-only, but the tests that use them are the direct evidence for cycle registration, source qualification, raw stream copying and the budgets. Removing them would force those tests through the extractor's policy, adding fixture construction for no production saving. Not recommended.

## Proposed removal scope (follow-up implementation, needs Gate 2 approval)

Exact diff: [`proposed-removal.diff`](proposed-removal.diff). It:

1. removes `CopyKind.Remap` and `CopyKind.Reject`;
2. replaces the Reject and Remap branches in `Fill` with the single `Append(key, CopyValue(...))` call;
3. removes `CopyDecision.Value`, `CopyDecision.Message`, `CopyDecision.Remap` and `CopyDecision.Reject`, narrowing the private constructor to `(kind, childStructure)`;
4. removes `CopyContext.Value` and its constructor parameter;
5. renames `copier_applies_typed_omit_and_remap_rules` to `copier_applies_typed_omit_rules` and drops its `Change` entry, keeping the Keep/Drop assertions.

Responsibility deleted: two decision kinds, two factories, three properties, two branches and one `InvalidOperationException` path. 7 insertions, 39 deletions across two files. Retained: everything else in the inventory. Added: nothing. Business use lost: none; no public API, behavior, refusal or output change. Overlapping savings: none claimed with other simplification items. Effort: under an hour, basis the measured probe below. It is a behavior-preserving consolidation, so the implementing item loads `csharp-refactoring` before editing.

Not recommended: collapsing `CopyDecision` to a nullable child-structure string. It saves a few lines and makes Omit an implicit null.

## Probe

Disposable worktree at the pinned revision with `proposed-removal.diff` applied (since removed):

- `dotnet build src/jaytwo.Pdf` (all targets, StyleCop warnings as errors): 0 warnings, 0 errors.
- `dotnet test test/jaytwo.Pdf.Tests -f net10.0` filtered to `NewFileWriterTests`, `PageExtractionTests`, `PageConcatenationTests`, `AssemblyLinksTests` and `AssemblyCheckpointTests`: 75 passed, 0 failed.

## Focused regression selection for the follow-up

- `NewFileWriterTests` (whole class): Omit rule, source-qualified map, cycle registration, raw stream bytes, bounded buffer, budgets.
- `PageExtractionTests.repeated_pages_have_distinct_annotations_and_shared_fonts_and_images`: repeated pages within one occurrence.
- `PageConcatenationTests.repeated_occurrences_keep_fields_values_and_links_separate_and_widgets_page_owned` and `nested_fields_prune_unselected_widgets_keep_inheritance_and_repeated_widget_copies`: repeated occurrences of one store.
- `PageExtractionTests.resource_inspection_bounds_cycles_and_depth_before_output`: malformed cyclic and deep graphs.
- `PageExtractionTests.extraction_omits_document_data_and_every_signature_widget`: Omit-driven document-data removal.

No new test is needed: the removed branches have no production path to regress.

## Measured versus inferred; gaps

Measured: the caller inventory, the build and the 75 focused tests on the patched tree. Inferred: output identity, from the absence of any production construction of `Remap` or `Reject`. Baseline test counts on the unpatched tree were not rerun. The fast lane, other frameworks, the Docker gate and independent review were not run and are not required for this research item. Rescope the follow-up if any new `Remap` or `Reject` caller appears before implementation.
