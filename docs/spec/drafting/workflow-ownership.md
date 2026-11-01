# Workflow ownership examples and research

Drafting aid for Q-44/Q-45, prepared 2026-10-27. The owner selected deferred reads and description-value capture, requested examples/research for resource ownership, and asked which session option was recommended. After reviewing those explanations, the owner selected borrowing supplied resources and one ordinary document/editor session with one save. WORKFLOW-OWN-01 and WORKFLOW-SESSION-01 in [workflows.md](../workflows.md) record those choices. The examples below retain their research/rationale role; unasked resource/output details are not inferred.

## Selected workflow ownership policy and rationale

Borrow streams and random-access sources supplied by the caller, including source PDFs, attachment payloads and output destinations. Keep them open on success, failure and cancellation. Own/dispose the handles, engine objects, adapters and temporary resources the workflow creates, including files it opens from paths, without closing borrowed resources underneath them.

| Caller situation | Selected ownership result | Reason |
| --- | --- | --- |
| Caller creates an output stream, runs the workflow, then rewinds/uploads it. | Workflow leaves it open; caller closes it after upload. | The caller needs to use the result after the PDF execution finishes. |
| Caller opens a source PDF stream or supplies a custom random-access source. | Workflow borrows it; caller retains disposal responsibility. | A workflow run should not unexpectedly terminate a resource lifecycle the host controls. |
| Caller supplies an attachment stream, then needs it for another purpose. | Workflow leaves it open, including after rejection/cancellation. | The stream may have a wider application lifetime than the PDF run. |
| Caller supplies only an input/output pathname. | Workflow opens/owns the necessary handles and closes what it created when the run finishes or fails. | The caller has not supplied an existing resource to keep alive. Exact publication/temporary-file cleanup rules remain separate. |

Leaving a stream open does not mean leaving its position or bytes unchanged. It does not guarantee rewind, retry safety, output rollback, thread safety or permission to mutate the source concurrently. Those need explicit contracts; failure/cancellation may affect destination contents even if ownership remains with the caller. Library-created resources still require cleanup after failure.

The benefit of this policy is predictable composition with uploads, application-owned storage and custom sources. The cost is that callers must dispose the resources they supplied. A transfer/leave-open option is another viable API choice, but creates defaults and overload behavior that need deliberate design. The proposed simple workflow policy does not dictate the independently useful engine's ownership defaults.

## Repository evidence

[D-60](../../../agents/decisions/D-60.md), decision 2, recorded this borrowing/owned-path distinction for the earlier workflow design, including failure/cancellation. It is historical owner intent, not automatic adoption of the rebuilt contract.

[W-204 findings](../../../agents/reference/simplification/W-204/findings.md) report 22 workflow cases: 16 succeeded and six refused; borrowed resources stayed open, descriptions read zero source/attachment bytes, and changed caller dictionaries did not alter the captured fill. These observations concern the pinned synthetic probe. They do not prove every resource/failure path: the study explicitly did not test destination failures or filesystem publication races.

[W-185](../../../agents/work-items/W-185.md) records shared file-save lifecycle work and preservation of borrowed ownership; [D-64](../../../agents/decisions/D-64.md) describes private signing staging and one-session behavior. These supply candidate implementation/coverage context, not a complete new ownership or signing contract.

## External precedent and limits

Microsoft's [StreamWriter constructors](https://learn.microsoft.com/en-us/dotnet/api/system.io.streamwriter.-ctor?view=net-10.0#system-io-streamwriter-ctor(system-io-stream-system-text-encoding-system-int32-system-boolean)) expose leaveOpen; ordinary disposal otherwise closes the underlying stream. This is evidence that closure is an explicit API choice, not a universal .NET rule that a supplied stream is always borrowed.

The official [iText .NET 9.4 PdfWriter API](https://api.itextpdf.com/iText/dotnet/9.4.0/classi_text_1_1_kernel_1_1_pdf_1_1_pdf_writer.html) exposes inherited SetCloseStream control. It likewise demonstrates explicit control rather than establishing jaytwo workflow defaults. These are primary documentation checks, not competitor execution or evidence about all historical versions/defaults.

Recommendation: the workflow convenience layer should use the simple borrowing rule above; callers dispose what they supply. If ownership transfer is needed, design it explicitly rather than having termination close caller resources incidentally.

## What the one-session recommendation means

The recommended answer to Q-45 is the first option: one document/editor session and one save for ordinary edits over an existing PDF.

Example: fill CustomerName, update Title and rotate a page. Open the input once; apply those ordinary edits through one editor using the selected workflow ordering; save the accepted ordinary edits once. The alternative would allow opening/saving/reopening between stages, which needs a different pending-state and intermediate-output contract.

This recommendation is supported by the earlier D-60 design and W-204's measured ordinary workflows, and was selected by the owner after clarification. It does not settle signing/timestamp staging, assembly, image creation, no-op revision counts, resource ownership or destination failure/publication guarantees. It is not a promise that every possible workflow terminal writes exactly one revision.

## Later output-strategy decision

After these ownership explanations, the owner selected direct destination-file output under WORKFLOW-FILE-01, not the earlier temporary-sibling publication model. D-60/D-65/W-185 staging examples remain historical context; they do not establish unchanged-destination or complete-publication guarantees for the rebuilt direct-write target. Borrowed resources still remain open and internally owned handles still require cleanup. Exact overwrite/timing/failed-file behavior is being decided separately.

## Overwrite conventions: Q-49 follow-up

Primary documentation checked 2026-10-27 after the owner requested Linux and .NET expectations. This is documentation research, not execution, a caller survey or an adopted default.

| Context | Documented behavior |
| --- | --- |
| GNU cp | Normally replaces existing destination files; interactive/no-clobber/update controls can change it. [GNU manual](https://www.gnu.org/software/coreutils/manual/coreutils.html#cp-invocation) |
| Bash output redirection | The ordinary > redirect creates or truncates; noclobber can prevent overwrites. [Bash manual](https://www.gnu.org/s/bash/manual/html_node/Redirections.html) |
| .NET File.WriteAllBytes | Creates a file or truncates/overwrites an existing destination. [Microsoft documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.writeallbytes?view=net-10.0) |
| .NET File.Copy with two arguments | Refuses an existing destination; an overload takes an overwrite choice. [Microsoft documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.copy?view=net-10.0) |

Inference/revised recommendation: for a direct Save(path) operation, create-or-overwrite with an explicit fail-if-exists option is a reasonable convention-based default. The earlier opt-in-overwrite recommendation prioritized deliberate replacement; it was not a universal Linux/.NET expectation. After the comparison, on 2026-10-27, the owner selected create-or-overwrite by default with an explicit fail-if-exists option, recorded as WORKFLOW-FILE-04. The earlier research request alone was not acceptance. A failing-if-exists contract is also defensible, but must be named/documented explicitly.

Alongside the selected default, the owner selected ordinary edits/available validation before destination creation/truncation and retaining failed direct output without restoration or automatic deletion. The original source is not implicitly eligible for overwrite; input alias safeguards and the separate explicit-append capability remain their own contracts.

## Input/output aliases: Q-52 follow-up

The owner asked whether to let an alias fail naturally instead of refusing eagerly. The subsequent owner answer selected the narrower boundary below as WORKFLOW-ALIAS-01. Documentation reviewed on 2026-10-27 gives different precedents; no competitor probes were run.

| Tool | Documented precedent | Limit of the comparison |
| --- | --- | --- |
| qpdf 12.4.2 | Explicit `--replace-input` writes a temporary file then replaces the input, leaving input untouched on errors. [CLI documentation](https://qpdf.readthedocs.io/en/stable/cli.html#option-replace-input) | This is deliberate staged replacement, not relying on an ordinary direct write to fail. It does not establish a universal alias-detection guarantee. |
| PDFBox 3 | Warns that using the input as output corrupts the file and throws when saving reads previously unread parts. [Migration guide](https://pdfbox.apache.org/3.0/migration.html#dont-use-the-source-as-output) | A natural exception can occur after source damage. The warning does not promise an early guard. |
| Aspose.PDF for .NET | Parameterless `Save()` explicitly performs incremental update on a writable source stream. [API reference](https://reference.aspose.com/pdf/net/aspose.pdf/document/save/) | An intentional source-update API does not prove arbitrary same-path `Save(path)` safety or alias detection. |
| iText | The official manipulation example constructs separate source-reader and destination-writer roles. [Tutorial](https://kb.itextpdf.com/itext/chapter-3-using-ileafelement-implementations) | This example does not settle same-file rejection timing or identity detection; do not claim it guarantees a guard. |

Owner-selected policy after this comparison: refuse before destructive opening when library abstractions establish that source and destination are the same file. Do not refuse caller streams for possible shared resources or reject an independent file solely for equal size/timestamp. Document caller responsibility for aliases the supported checks cannot establish. The exact portable checks, path/link/stream coverage and races need design and direct-write revalidation. The excluded in-place source rewrite remains excluded; supported explicit incremental append is separate.

W-206's size/timestamp heuristic had independent-copy false positives, and its replacement experiments concerned staged publication. Neither its safety results nor its link results establish direct-truncation safety. The reason to retain a narrow positive-identity guard is source preservation, not a claim that all established tools refuse early. Relying on an exception alone cannot establish that the original input survives.
