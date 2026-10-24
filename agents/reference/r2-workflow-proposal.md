# R2 workflow proposal

W-141, 2026-10-24. For the owner's approval. It answers the nine questions in W-141 and proposes the requirement rows for `docs/spec/phase-1.md`. The recommended choices are recorded in D-60 (proposed).

Input: an outside expert answered the W-141 brief without access to the code. This proposal adopts most of that answer. It changes five points, and the engine's actual behavior settles three of the expert's open points. Section 1 lists both.

## 0. Status

2026-10-24, later: The owner approved F1 to F16 with corrections to F4 (semantic equivalence and an exact source prefix, not byte identity), F8 (the stream position is captured when described; content is read during the run), F11 (alias safety), F12 (publication guarantees limited to the filesystem and runtime), and F15 (overhead bounded by the description, not a constant). The owner kept F5's flatten merge: fills and flattens are the only merged operations. Section 3 is the wording in `docs/spec/phase-1.md` section 6.7.

2026-10-24: The owner approved the five choices in section 5 and the direction of section 2, with these clarifications:

- Page numbers are positive. A number above the page count is rejected before output.
- A MIME type the caller supplies always applies. Extensions match without regard to case, and no operating-system MIME database is used.
- The workflow never disposes a borrowed stream or `IPdfSource`, even after a failure or cancellation. It disposes the adapters it creates without closing the resource beneath them.
- Once a terminal call has begun, the builder rejects mutation and any further or concurrent execution, including after a failure or cancellation.
- Only the last declared value of each field reaches the bulk fill. Other operations replay in declaration order with engine semantics: no normalization, no suppressed exceptions, and no inferred final state. A conflict detectable without reading the document fails before writing.

Section 3 has been revised to match: F2, F3, F5, F6, F8, F9, and F12. The reviewer who approved did not see the row wording, so the wording of F1 to F16 still needs the owner's approval before it goes into `docs/spec/phase-1.md`. One point needs the owner's ruling: F5 merges flatten requests into one engine call, just as fills are merged. That is the only exception to replaying operations in order.

## 1. Review of the expert's answer against the engine

Settled by the engine:

- **Fill dispositions after flatten.** The expert asked how a result should report a `NeedsViewer` field that is then flattened. That case cannot complete: a fill the engine cannot draw sets `/NeedAppearances true`, and `Flatten` then throws `PdfUnsupportedFormException` (`PdfAcroFormEditor.Flatten`). The run fails before the destination is written, so the result needs no flatten state.
- **Stream origin.** The expert suggested requiring the input stream's position to be zero. The engine reads the input at absolute offsets (`PdfStreamSource`), so a stream's position does not matter and no rule is needed. Position does matter for attachment content, which the engine reads "from the current position through the end". See section 2, Q3.
- **Saves with no changes.** The expert's rule matches the engine (`PdfIncrementalWriter.SaveIncrementalAsync`). A separate destination receives a copy of the input. The same stream receives nothing.

Changed from the expert's recommendation:

1. **Caller streams are always borrowed, with no `LeaveOpen` option.** A workflow description can be dropped without ever running. If it owned a caller's stream, that stream would leak. Dropping the option also removes the `leaveOpen` parameter that sat before `CancellationToken`, which the expert flagged.
2. **The builder is mutable and runs once.** The expert proposed descriptions that are immutable by copy and also single-use. Those two conflict: every copy shares the one bound input, so "runs once" cannot be enforced per copy. The protection the expert wanted is that later changes to a caller's dictionary or options do not affect the workflow. Snapshotting arguments when they are described gives that.
3. **Operations replay in declaration order, except fills.** The expert proposed reducing attachment and rotation operations to a final state before calling the editor. Replaying each call on the editor in order gives the same results for every case in the expert's table: last-wins metadata and absolute rotation, composed relative rotation, add/remove per name. It also leaves the engine as the only authority, which is the expert's own risk 6: a missing attachment's `Remove` already throws `PdfAttachmentNotFoundException`. Fills are the exception. All fills merge, last value wins, into one bulk `Fill` before any flatten, so the whole set stays all-or-nothing.
4. **Engine exceptions propagate unchanged, with no `Exception.Data` context.** Engine messages already name the field, attachment, or rule involved. Add context only if a real failure turns out to be hard to diagnose.
5. **45 requirement rows become 16.** Several of the expert's rows restate the charter (WF-001) or engine rules that the spec already holds (WF-022, WF-024, WF-025). Section 3 keeps one testable row per workflow behavior. It uses the prefix F, which the spec does not use yet.

## 2. Answers to the nine questions

| Q | Recommendation |
| --- | --- |
| 1. Entry and ownership | `PdfWorkflow.From(Stream)`, `From(IPdfSource)`, and `FromFile(string path)`, each with optional `PdfWorkflowOpenOptions { Password }`. Nothing is opened or read until a terminal call. Caller streams and sources are borrowed. Files opened from a path are owned for the run only. |
| 2. Output | `Save` and `SaveAsync` to an empty writable seekable `Stream`, or to a path with `PdfFileSaveOptions { Overwrite }`; `AppendToSource` and `AppendToSourceAsync` for an explicit same-source append, which is not available for `IPdfSource`. An output path equal to the input path is rejected; same-source writing is only the explicit append. Non-seekable destinations are rejected, with no automatic spooling. A run returns `PdfWorkflowResult { Modified, OutputLength, FieldAppearances }`. |
| 3. Operations | `Fill`, `FillForm`, `FlattenForm()` and `FlattenForm(names)`, `WithTitle`, `WithMetadata(PdfMetadataChanges)`, `Attach(name, Stream, options)`, `AttachFile(name, path, options)`, `RemoveAttachment`, `RotatePage`, `RotatePageBy`, `RotateAllPagesBy`. Dropped: raw XMP, custom information keys, explicit dates (the engine refreshes `ModDate`). |
| 4. Order and conflicts | Fills, then flatten, then the other operations in declaration order (section 1, change 3). Repeated fills: last wins. Flatten-all subsumes named flattens. Nothing is rejected for being repeated. |
| 5. Validation and errors | Arguments and workflow shape: `ArgumentException` family or `InvalidOperationException`, thrown at description time or before the input opens. Engine checks run as editor calls, before any destination is written. Engine, I/O, and cancellation exceptions propagate unchanged. |
| 6. Signed documents | `AllowBreakingExistingSignatures()` for the whole workflow sets the editor's `AllowBreakingSignatures`. It never relaxes certification, FieldMDP, or password permissions, because the engine enforces those. |
| 7. Sync and async | Both, for every terminal call. Async runs use the engine's async open and save, and document the synchronous inspection reads that W-138 tracks. No `Task.Run`. |
| 8. Read shortcuts | `ExportFormValues(path)` and `ExportFormValuesAsync(path)` only. W-145 implements them. |
| 9. Reuse | A bound workflow runs once, whether it succeeds, fails, or is canceled. Reusable recipes are out of scope until a use case needs them. |

Details the expert left open:

- **File commit.** Write a unique temporary sibling, flush it to disk, then publish. A new target is published with `File.Move`, which fails if the target appeared in the meantime. An existing target, with `Overwrite`, is published with `File.Replace`: `rename(2)` on Unix and `ReplaceFile` on Windows, both available on `netstandard2.0`. The temporary file is deleted on failure. The guarantee is that after a handled failure the target is the old file, or absent if it did not exist, and after success it is the complete new file. There is no power-loss durability claim.
- **Aliasing.** Reject equal normalized full paths before opening (case-insensitive on Windows). An alias that check misses, such as a hard or symbolic link, cannot damage the input. Publishing renames a directory entry and never writes the input's bytes. On Windows, the run's open input handle makes `ReplaceFile` of a hard-linked alias fail. W-142 tests both platforms.
- **Attachment content.** A stream's position is recorded when the attachment is described. That is not I/O, but the stream must be readable and seekable then. The run seeks back to that position. A path attachment is opened once for the run, so the engine's add-time and save-time reads see the same file.
- **`Modified`** means a revision was appended: the output is longer than the input. It is derived from public lengths, not from counting workflow operations.

## 3. Proposed requirement rows

For a new `docs/spec/phase-1.md` section 6.7, "Workflows (`jaytwo.Ergonomics.Pdf`)". Status New until the item that ships each row.

| ID | Requirement | Item |
| --- | --- | --- |
| F1 | **Deferred description.** `PdfWorkflow.From` (a `Stream` or an `IPdfSource`) and `FromFile` (a path) bind one input, with an optional password. Neither they nor any operation method reads the input, attachment content, or a destination. Arguments are copied when described, so later changes to a caller's collection or options do not affect the workflow. | W-142 |
| F2 | **Ownership.** The workflow never disposes a caller-supplied stream or `IPdfSource` (the input, attachment content, or a destination), including after a failure or cancellation. It disposes the engine objects and adapters it creates without closing the borrowed resource beneath them. A file it opens from a path is opened during the run and closed after it, on success, failure, and cancellation. | W-142 |
| F3 | **One run.** A workflow runs once. Once a terminal call has begun, another terminal call or operation method throws `InvalidOperationException`, whether the run succeeded, failed, or was canceled, and including a call made concurrently with the run. | W-142 |
| F4 | **One session, one revision.** A run opens the input once, applies every operation through one `PdfDocumentEditor`, and saves once. Non-signing operations add at most one revision. The output shall preserve the original source bytes as an exact prefix and produce results semantically equivalent to the corresponding direct-engine operations. | W-142 |
| F5 | **Order.** Form operations run first. All fills reach the engine as one all-or-nothing bulk fill, which holds only the last declared value of each field. All flatten requests then reach the engine as one flatten: every field when any request names none, otherwise the union of the named fields. Every other operation is then replayed on the editor in declaration order, with the engine's semantics for each call. The workflow does not combine, drop, or reorder those operations, infer a final state, or suppress an engine exception. | W-143 |
| F6 | **Forms.** Fill one field or many. Flatten every field or named fields. | W-143 |
| F7 | **Metadata.** Set or clear the title, author, subject, keywords, creator, and producer. A property that is not assigned is unchanged; one assigned `null` is removed. | W-142 |
| F8 | **Attachments.** Add from a readable seekable stream, capturing its current position when described and reading its content from that position during execution, or from a file path, and remove by name. A MIME type the caller supplies always applies. Otherwise a path attachment's extension, matched without regard to case, sets it from this table: `.xml` `text/xml`, `.json` `application/json`, `.pdf` `application/pdf`, `.csv` `text/csv`, `.txt` `text/plain`. Any other extension leaves it unspecified. No operating-system MIME database is used. | W-144 |
| F9 | **Rotation.** Set a page's rotation, rotate a page by an angle, or rotate every page by an angle, in multiples of 90. Pages are numbered from 1, as `PdfPage.Number` is. A page number below 1 or an angle that is not a multiple of 90 is rejected when described. A page number above the page count is rejected before output. Relative rotations accumulate in declaration order. | W-144 |
| F10 | **Signed documents.** Flatten and rotation on a document with an approval signature or a document timestamp require `AllowBreakingExistingSignatures()`. It does not relax certification, FieldMDP, or password permissions. | W-143, W-144 |
| F11 | **Outputs.** `Save` to a stream writes the input bytes and the revision to an empty writable seekable stream; with no changes it writes a copy. `Save` to a path stages a temporary sibling and publishes it only after success. An existing file is replaced only with `Overwrite`, and a path equal to the input path is rejected before the input opens. `AppendToSource` appends to the bound stream or file and writes nothing when nothing changed; it is not offered for an `IPdfSource`. Non-seekable destinations are rejected. A separate-output save shall reject a destination known to alias its input, including through equivalent paths or detected filesystem identity. When safe separation cannot be established, the implementation shall not overwrite its open input. | W-142 |
| F12 | **Failure.** Argument errors, workflow-shape errors, and conflicts detectable without reading the document are thrown before the input opens. Engine exceptions propagate unchanged. No destination is written until every operation has been applied to the editor. After a later failure, a stream destination is restored to its starting length on a best-effort basis, as the engine does. Path output is written to a temporary sibling and published only after successful processing. Failure before publication leaves the existing destination unchanged; publication and replacement guarantees are limited to those supported by the filesystem and runtime. | W-142 |
| F13 | **Result.** A run returns whether a revision was appended, the output length, and the appearance disposition of each filled field's final value. | W-142, W-143 |
| F14 | **Sync and async.** Every terminal call has a synchronous and an asynchronous form; the asynchronous form takes a `CancellationToken`. Synchronous source reads during an asynchronous run are documented. The engine still reads synchronously while inspecting and editing; a later engine change addresses that. | W-142 |
| F15 | **Memory.** A run never buffers a whole PDF or attachment. Workflow overhead is bounded by the described operations, their arguments, and result state, not by total PDF or attachment size. Document processing retains the engine's bounded-memory guarantees. | W-142, W-146 |
| F16 | **Form export.** `ExportFormValues` and `ExportFormValuesAsync` return a file's form values without the caller managing a document. | W-145 |

## 4. Public API sketch

```csharp
namespace jaytwo.Ergonomics.Pdf;

public static class PdfWorkflow
{
    public static PdfWorkflowBuilder From(Stream input, PdfWorkflowOpenOptions? options = null);
    public static PdfWorkflowBuilder From(IPdfSource input, PdfWorkflowOpenOptions? options = null);
    public static PdfWorkflowBuilder FromFile(string path, PdfWorkflowOpenOptions? options = null);
    public static IReadOnlyDictionary<string, PdfFieldValue> ExportFormValues(string path, PdfWorkflowOpenOptions? options = null);
    public static Task<IReadOnlyDictionary<string, PdfFieldValue>> ExportFormValuesAsync(string path, PdfWorkflowOpenOptions? options = null, CancellationToken cancellationToken = default);
}

public sealed class PdfWorkflowBuilder
{
    public PdfWorkflowBuilder Fill(string fullName, PdfFieldValue value);
    public PdfWorkflowBuilder FillForm(IEnumerable<KeyValuePair<string, PdfFieldValue>> values);
    public PdfWorkflowBuilder FlattenForm();
    public PdfWorkflowBuilder FlattenForm(params string[] fullNames);
    public PdfWorkflowBuilder WithTitle(string? title);
    public PdfWorkflowBuilder WithMetadata(PdfMetadataChanges changes);
    public PdfWorkflowBuilder Attach(string name, Stream content, PdfAttachmentOptions? options = null);
    public PdfWorkflowBuilder AttachFile(string name, string path, PdfAttachmentOptions? options = null);
    public PdfWorkflowBuilder RemoveAttachment(string name);
    public PdfWorkflowBuilder RotatePage(int pageNumber, int degrees);
    public PdfWorkflowBuilder RotatePageBy(int pageNumber, int degrees);
    public PdfWorkflowBuilder RotateAllPagesBy(int degrees);
    public PdfWorkflowBuilder AllowBreakingExistingSignatures();

    public PdfWorkflowResult Save(Stream destination);
    public Task<PdfWorkflowResult> SaveAsync(Stream destination, CancellationToken cancellationToken = default);
    public PdfWorkflowResult Save(string path, PdfFileSaveOptions? options = null);
    public Task<PdfWorkflowResult> SaveAsync(string path, PdfFileSaveOptions? options = null, CancellationToken cancellationToken = default);
    public PdfWorkflowResult AppendToSource();
    public Task<PdfWorkflowResult> AppendToSourceAsync(CancellationToken cancellationToken = default);
}

public sealed class PdfWorkflowOpenOptions { public string? Password { get; init; } }
public sealed class PdfFileSaveOptions { public bool Overwrite { get; init; } }

// Each init setter records that the property was assigned, so `Author = null` clears and an omitted property is unchanged.
public sealed class PdfMetadataChanges { public string? Title { get; init; } /* Author, Subject, Keywords, Creator, Producer */ }

public sealed class PdfWorkflowResult
{
    public bool Modified { get; }
    public long OutputLength { get; }
    public IReadOnlyDictionary<string, PdfAppearanceDisposition> FieldAppearances { get; }
}
```

## 5. Choices for the owner

The owner approved each recommendation on 2026-10-24.

1. **Page numbering.** Recommended: 1-based `pageNumber`, matching `PdfPage.Number` and `PdfPageEditor.Number` and how callers name pages. Alternative: the expert's 0-based `pageIndex`, matching `editor.Pages[i]`.
2. **The MIME table for `AttachFile`.** Recommended: a closed table of `.xml` `text/xml` (the type Factur-X and ZUGFeRD specify for their XML), `.json` `application/json`, `.pdf` `application/pdf`, `.csv` `text/csv`, and `.txt` `text/plain`. Any other extension gets no MIME type. Alternative: no inference.
3. **Ownership** (section 1, change 1). Recommended: always borrow. Alternative: the expert's `LeaveOpen` option, defaulting to borrow.
4. **Builder shape** (section 1, change 2). Recommended: a mutable builder that runs once and snapshots its arguments. Alternative: the expert's immutable descriptions, with the run-once state shared by every copy of one binding.
5. **Ordering** (section 1, change 3). Recommended: fills merged before flatten, everything else replayed in order. Alternative: the expert's per-name and per-page final-state reduction.

## 6. Caller code, engine versus workflow

The expert's side-by-side holds, and it shows a modest gain for single operations. One metadata edit saves only the document and editor lifetimes. The R2 exit criterion "shorter, with real defaults or safeguards" is met by the combined, file, and every-page cases:

```csharp
// Engine
using var input = File.OpenRead("order.pdf");
using var output = File.Create("order-signed-off.pdf");
using var document = PdfDocument.Open(input, leaveOpen: true);
using var attachment = File.OpenRead("order.json");
using var editor = document.Edit();
editor.AcroForm.Fill(values);
editor.AcroForm.Flatten();
editor.Metadata.Title = "Executed Order";
editor.Attachments.Add("order.json", attachment, leaveOpen: true, new PdfAttachmentOptions { MimeType = "application/json" });
for (int i = 0; i < editor.Pages.Count; i++)
{
    editor.Pages[i].Rotation += 90;
}

editor.SaveIncremental(output, leaveOpen: true);
// A failure leaves an empty order-signed-off.pdf behind, and an existing one is overwritten without asking.

// Workflow
var result = await PdfWorkflow.FromFile("order.pdf")
    .FillForm(values)
    .FlattenForm()
    .WithTitle("Executed Order")
    .AttachFile("order.json", "order.json")
    .RotateAllPagesBy(90)
    .SaveAsync("order-signed-off.pdf", cancellationToken: ct);
```

W-146 checks the `README.md` examples against this criterion.
