using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Pdf;

namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// A workflow bound to one input: operations are described first and run together by one terminal call (spec F1 to F4).
/// </summary>
/// <remarks>
/// <para>
/// Describing an operation reads nothing. A terminal call (<c>Save</c>, <c>SaveAsync</c>, <c>AppendToSource</c>, or
/// <c>AppendToSourceAsync</c>) opens the input, applies every operation through one <see cref="PdfDocumentEditor"/>, and
/// appends at most one revision. A workflow runs once: after a terminal call has begun, whether it succeeds, fails, or is
/// canceled, every further call throws <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// The workflow never disposes a stream or <see cref="IPdfSource"/> the caller supplies. Argument and workflow-shape
/// errors are thrown before the input opens. Exceptions from <c>jaytwo.Pdf</c> propagate unchanged, and no destination is
/// written until every operation has been applied.
/// </para>
/// <para>
/// In an asynchronous run, opening the input and copying it to a destination read asynchronously. Applying operations and
/// preparing the revision still read the input synchronously, so an <see cref="IPdfSource"/> must answer synchronous reads.
/// </para>
/// <para>
/// The builder is not thread-safe, except that of concurrent terminal calls exactly one runs.
/// </para>
/// </remarks>
public sealed class PdfWorkflowBuilder
{
    private readonly object _gate = new object();
    private readonly WorkflowInput _input;
    private readonly List<Action<PdfDocumentEditor>> _operations = new List<Action<PdfDocumentEditor>>();
    private bool _started;

    internal PdfWorkflowBuilder(WorkflowInput input)
    {
        _input = input;
    }

    /// <summary>
    /// Sets the document title. <see langword="null"/> removes it.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>This workflow.</returns>
    public PdfWorkflowBuilder WithTitle(string? title)
    {
        return Add(editor => editor.Metadata.Title = title);
    }

    /// <summary>
    /// Changes document information. Only the properties the changes assign are written (spec F7).
    /// </summary>
    /// <param name="changes">The changes.</param>
    /// <returns>This workflow.</returns>
    public PdfWorkflowBuilder WithMetadata(PdfMetadataChanges changes)
    {
        if (changes == null)
        {
            throw new ArgumentNullException(nameof(changes));
        }

        return Add(editor => changes.ApplyTo(editor.Metadata));
    }

    /// <summary>
    /// Runs the workflow and writes the input and its new revision to an empty writable seekable stream. With no changes it
    /// writes a copy of the input.
    /// </summary>
    /// <param name="destination">The destination. It is not disposed. After a failure it is set back to its starting length, as far as it allows.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">The destination is not empty, writable, and seekable, or it is the input stream.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public PdfWorkflowResult Save(Stream destination)
    {
        return SaveStreamAsync(destination, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the workflow and writes the input and its new revision to an empty writable seekable stream. With no changes it
    /// writes a copy of the input.
    /// </summary>
    /// <param name="destination">The destination. It is not disposed. After a failure or cancellation it is set back to its starting length, as far as it allows.</param>
    /// <param name="cancellationToken">Observed while the input opens and the destination is written.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">The destination is not empty, writable, and seekable, or it is the input stream.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public Task<PdfWorkflowResult> SaveAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        return SaveStreamAsync(destination, true, cancellationToken);
    }

    /// <summary>
    /// Runs the workflow and writes the input and its new revision to a file (spec F17).
    /// </summary>
    /// <remarks>
    /// The output is written to a temporary sibling, <c>.&lt;name&gt;.&lt;random&gt;.tmp</c>, and moved to the path only after
    /// it is complete; after a failure the temporary file is removed and the path is left as it was. An existing file is
    /// replaced only with <see cref="PdfFileSaveOptions.Overwrite"/>. A destination that may be the input is refused: the
    /// input's own path, and, before replacing an existing file, any file with the input's length and last-write time,
    /// which also refuses a copy whose timestamps were copied. These checks are best-effort safeguards, not proof of file
    /// identity. On Unix, a destination that is a symbolic link is replaced as a link, and its target is not changed; other
    /// hard links to a replaced file keep its old content. No power-loss durability is claimed.
    /// </remarks>
    /// <param name="path">The destination path. Its directory must exist.</param>
    /// <param name="options">Overwrite and flush options.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">The path is the input's path.</exception>
    /// <exception cref="DirectoryNotFoundException">The destination directory does not exist.</exception>
    /// <exception cref="IOException">The destination exists without <see cref="PdfFileSaveOptions.Overwrite"/>, or it may be the input.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public PdfWorkflowResult Save(string path, PdfFileSaveOptions? options = null)
    {
        return SaveFileAsync(path, options, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the workflow and writes the input and its new revision to a file (spec F17).
    /// </summary>
    /// <remarks>
    /// The output is written to a temporary sibling, <c>.&lt;name&gt;.&lt;random&gt;.tmp</c>, and moved to the path only after
    /// it is complete; after a failure or cancellation the temporary file is removed and the path is left as it was.
    /// Cancellation is observed until the output is published. An existing file is replaced only with
    /// <see cref="PdfFileSaveOptions.Overwrite"/>. A destination that may be the input is refused: the input's own path, and,
    /// before replacing an existing file, any file with the input's length and last-write time, which also refuses a copy
    /// whose timestamps were copied. These checks are best-effort safeguards, not proof of file identity. On Unix, a
    /// destination that is a symbolic link is replaced as a link, and its target is not changed; other hard links to a
    /// replaced file keep its old content. No power-loss durability is claimed.
    /// </remarks>
    /// <param name="path">The destination path. Its directory must exist.</param>
    /// <param name="options">Overwrite and flush options.</param>
    /// <param name="cancellationToken">Observed until the output is published.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">The path is the input's path.</exception>
    /// <exception cref="DirectoryNotFoundException">The destination directory does not exist.</exception>
    /// <exception cref="IOException">The destination exists without <see cref="PdfFileSaveOptions.Overwrite"/>, or it may be the input.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public Task<PdfWorkflowResult> SaveAsync(string path, PdfFileSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        return SaveFileAsync(path, options, true, cancellationToken);
    }

    /// <summary>
    /// Runs the workflow and appends its revision to the input stream or file itself. With no changes nothing is written.
    /// </summary>
    /// <returns>The run's result.</returns>
    /// <exception cref="InvalidOperationException">The workflow has already run, its input is an <see cref="IPdfSource"/>, or its input stream is not writable.</exception>
    public PdfWorkflowResult AppendToSource()
    {
        return AppendAsync(false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the workflow and appends its revision to the input stream or file itself. With no changes nothing is written.
    /// </summary>
    /// <param name="cancellationToken">Observed while the input opens and before the revision is written.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="InvalidOperationException">The workflow has already run, its input is an <see cref="IPdfSource"/>, or its input stream is not writable.</exception>
    public Task<PdfWorkflowResult> AppendToSourceAsync(CancellationToken cancellationToken = default)
    {
        return AppendAsync(true, cancellationToken);
    }

    private PdfWorkflowBuilder Add(Action<PdfDocumentEditor> operation)
    {
        lock (_gate)
        {
            CheckNotStarted();
            _operations.Add(operation);
        }

        return this;
    }

    private void Begin()
    {
        lock (_gate)
        {
            CheckNotStarted();
            _started = true;
        }
    }

    private void CheckNotStarted()
    {
        if (_started)
        {
            throw new InvalidOperationException("This workflow has already run. Build another workflow to run again.");
        }
    }

    private void Apply(PdfDocumentEditor editor)
    {
        foreach (Action<PdfDocumentEditor> operation in _operations)
        {
            operation(editor);
        }
    }

    private async Task<PdfWorkflowResult> SaveStreamAsync(Stream destination, bool useAsync, CancellationToken cancellationToken)
    {
        Begin();
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (_input.IsStream(destination))
        {
            throw new ArgumentException("The destination is the input stream. Use AppendToSource to add a revision to the input.", nameof(destination));
        }

        if (!destination.CanWrite || !destination.CanSeek || destination.Length != 0)
        {
            throw new ArgumentException("The destination must be an empty writable seekable stream.", nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using WorkflowSession session = await _input.OpenAsync(false, useAsync, cancellationToken).ConfigureAwait(false);
        using (PdfDocumentEditor editor = session.Document.Edit())
        {
            Apply(editor);
            if (useAsync)
            {
                await editor.SaveIncrementalAsync(destination, leaveOpen: true, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                editor.SaveIncremental(destination, leaveOpen: true);
            }
        }

        return new PdfWorkflowResult(destination.Length > session.InputLength, destination.Length);
    }

    private async Task<PdfWorkflowResult> SaveFileAsync(string path, PdfFileSaveOptions? options, bool useAsync, CancellationToken cancellationToken)
    {
        Begin();
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (path.Length == 0)
        {
            throw new ArgumentException("The destination path is empty.", nameof(path));
        }

        options ??= new PdfFileSaveOptions();
        string target = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(target) ?? throw new ArgumentException("The destination path names no file.", nameof(path));
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("The destination directory '" + directory + "' does not exist.");
        }

        string? inputPath = _input.FilePath;
        if (inputPath != null && WorkflowFiles.SamePath(inputPath, target))
        {
            throw new ArgumentException("The destination is the input file. Use AppendToSource to add a revision to the input.", nameof(path));
        }

        if (!options.Overwrite && File.Exists(target))
        {
            throw WorkflowFiles.Exists(target);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using WorkflowSession session = await _input.OpenAsync(false, useAsync, cancellationToken).ConfigureAwait(false);
        using PdfDocumentEditor editor = session.Document.Edit();
        Apply(editor);
        string temporary = WorkflowFiles.TemporarySibling(target);
        bool published = false;
        try
        {
            long outputLength;
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, useAsync))
            {
                if (useAsync)
                {
                    await editor.SaveIncrementalAsync(output, leaveOpen: true, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    editor.SaveIncremental(output, leaveOpen: true);
                }

                if (options.FlushToDisk)
                {
                    output.Flush(flushToDisk: true);
                }

                outputLength = output.Length;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // The input stays open while the output is published, so the alias check sees it as the run read it.
            WorkflowFiles.Publish(temporary, target, options.Overwrite, inputPath);
            published = true;
            return new PdfWorkflowResult(outputLength > session.InputLength, outputLength);
        }
        finally
        {
            if (!published)
            {
                WorkflowFiles.TryDelete(temporary);
            }
        }
    }

    private async Task<PdfWorkflowResult> AppendAsync(bool useAsync, CancellationToken cancellationToken)
    {
        Begin();
        if (_input.IsSource)
        {
            throw new InvalidOperationException("AppendToSource needs a stream or file input. An IPdfSource is read-only.");
        }

        if (!_input.IsWritableStream)
        {
            throw new InvalidOperationException("AppendToSource needs a writable input stream.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using WorkflowSession session = await _input.OpenAsync(true, useAsync, cancellationToken).ConfigureAwait(false);
        using (PdfDocumentEditor editor = session.Document.Edit())
        {
            Apply(editor);
            if (useAsync)
            {
                await editor.SaveIncrementalAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                editor.SaveIncremental();
            }
        }

        long length = session.Stream!.Length;
        return new PdfWorkflowResult(length > session.InputLength, length);
    }
}
