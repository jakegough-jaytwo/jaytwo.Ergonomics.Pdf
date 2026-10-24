using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;

namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// A workflow bound to one input: operations are described first and run together by one terminal call (spec F1 to F4).
/// </summary>
/// <remarks>
/// <para>
/// Describing an operation reads nothing. A terminal call (<c>Save</c>, <c>SaveAsync</c>, <c>AppendToSource</c>, or
/// <c>AppendToSourceAsync</c>) opens the input, applies every operation through one <see cref="PdfDocumentEditor"/>, and
/// appends at most one revision. Form operations apply first: every fill, as one all-or-nothing bulk fill that holds the
/// last value described for each field, then one flatten. The other operations then apply in the order described
/// (spec F5). A workflow runs once: after a terminal call has begun, whether it succeeds, fails, or is
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
    private readonly List<KeyValuePair<string, PdfFieldValue>> _fills = new List<KeyValuePair<string, PdfFieldValue>>();
    private readonly Dictionary<string, int> _fillIndex = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<string> _flattenNames = new List<string>();
    private readonly HashSet<string> _flattenNamed = new HashSet<string>(StringComparer.Ordinal);
    private bool _flattenAll;
    private bool _allowBreakingSignatures;
    private bool _started;

    internal PdfWorkflowBuilder(WorkflowInput input)
    {
        _input = input;
    }

    /// <summary>
    /// Fills one field by its dotted full name (spec F6).
    /// </summary>
    /// <remarks>
    /// Every fill in the workflow reaches <see cref="PdfAcroFormEditor.Fill(IEnumerable{KeyValuePair{string, PdfFieldValue}})"/>
    /// as one all-or-nothing bulk fill, before any flatten, whatever order they were described in. A field filled more than
    /// once takes the last value. The engine checks the name and the value when the workflow runs.
    /// </remarks>
    /// <param name="fullName">The dotted field name.</param>
    /// <param name="value">The new value.</param>
    /// <returns>This workflow.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fullName"/> is <see langword="null"/>.</exception>
    public PdfWorkflowBuilder Fill(string fullName, PdfFieldValue value)
    {
        if (fullName == null)
        {
            throw new ArgumentNullException(nameof(fullName));
        }

        return Record(() => SetFill(fullName, value));
    }

    /// <summary>
    /// Fills several fields (spec F6). The values are copied when described.
    /// </summary>
    /// <remarks>
    /// The values join the workflow's one bulk fill, as <see cref="Fill(string, PdfFieldValue)"/> describes. A field filled
    /// more than once, here or in another call, takes the last value.
    /// </remarks>
    /// <param name="values">Full names and values, for example a <see cref="Dictionary{TKey, TValue}"/> or the result of <see cref="PdfAcroForm.Export"/>.</param>
    /// <returns>This workflow.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A name is <see langword="null"/>.</exception>
    public PdfWorkflowBuilder FillForm(IEnumerable<KeyValuePair<string, PdfFieldValue>> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var copy = new List<KeyValuePair<string, PdfFieldValue>>(values);
        foreach (KeyValuePair<string, PdfFieldValue> fill in copy)
        {
            if (fill.Key == null)
            {
                throw new ArgumentException("A field name cannot be null.", nameof(values));
            }
        }

        return Record(() =>
        {
            foreach (KeyValuePair<string, PdfFieldValue> fill in copy)
            {
                SetFill(fill.Key, fill.Value);
            }
        });
    }

    /// <summary>
    /// Flattens every field except signature fields (spec F6), after every fill.
    /// </summary>
    /// <remarks>
    /// The workflow flattens once, through <see cref="PdfAcroFormEditor.Flatten()"/>, which also covers any named flatten.
    /// On a document with an approval signature or a document timestamp, flatten needs
    /// <see cref="AllowBreakingExistingSignatures"/>.
    /// </remarks>
    /// <returns>This workflow.</returns>
    public PdfWorkflowBuilder FlattenForm()
    {
        return Record(() => _flattenAll = true);
    }

    /// <summary>
    /// Flattens the named fields (spec F6), after every fill.
    /// </summary>
    /// <remarks>
    /// The workflow flattens once: every field when <see cref="FlattenForm()"/> is also described, otherwise every field
    /// named in any call, through <see cref="PdfAcroFormEditor.Flatten(IEnumerable{string})"/>. A name may be given more than
    /// once. The engine checks the names when the workflow runs. On a document with an approval signature or a document
    /// timestamp, flatten needs <see cref="AllowBreakingExistingSignatures"/>.
    /// </remarks>
    /// <param name="fullNames">The dotted full names.</param>
    /// <returns>This workflow.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fullNames"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A name is <see langword="null"/>.</exception>
    public PdfWorkflowBuilder FlattenForm(params string[] fullNames)
    {
        if (fullNames == null)
        {
            throw new ArgumentNullException(nameof(fullNames));
        }

        string[] copy = (string[])fullNames.Clone();
        if (Array.IndexOf(copy, null) >= 0)
        {
            throw new ArgumentException("A field name cannot be null.", nameof(fullNames));
        }

        return Record(() =>
        {
            foreach (string name in copy)
            {
                if (_flattenNamed.Add(name))
                {
                    _flattenNames.Add(name);
                }
            }
        });
    }

    /// <summary>
    /// Allows a flatten to change a document that has an approval signature or a document timestamp (spec F10).
    /// </summary>
    /// <remarks>
    /// This sets <see cref="PdfDocumentEditor.AllowBreakingSignatures"/> for the run. Viewers will report the existing
    /// signatures as modified. It does not relax certification, FieldMDP locks, or password permissions, and a signed
    /// signature field is never flattened.
    /// </remarks>
    /// <returns>This workflow.</returns>
    public PdfWorkflowBuilder AllowBreakingExistingSignatures()
    {
        return Record(() => _allowBreakingSignatures = true);
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
        return Record(() => _operations.Add(operation));
    }

    private PdfWorkflowBuilder Record(Action describe)
    {
        lock (_gate)
        {
            CheckNotStarted();
            describe();
        }

        return this;
    }

    private void SetFill(string fullName, PdfFieldValue value)
    {
        var fill = new KeyValuePair<string, PdfFieldValue>(fullName, value);
        if (_fillIndex.TryGetValue(fullName, out int index))
        {
            _fills[index] = fill;
        }
        else
        {
            _fillIndex.Add(fullName, _fills.Count);
            _fills.Add(fill);
        }
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

    // Spec F5: one bulk fill, then one flatten, then the other operations in the order described.
    private IReadOnlyDictionary<string, PdfAppearanceDisposition> Apply(PdfDocumentEditor editor)
    {
        if (_allowBreakingSignatures)
        {
            editor.AllowBreakingSignatures = true;
        }

        IReadOnlyDictionary<string, PdfAppearanceDisposition> appearances = PdfWorkflowResult.NoFields;
        if (_fills.Count > 0)
        {
            appearances = editor.AcroForm.Fill(_fills);
        }

        if (_flattenAll)
        {
            editor.AcroForm.Flatten();
        }
        else if (_flattenNames.Count > 0)
        {
            editor.AcroForm.Flatten(_flattenNames);
        }

        foreach (Action<PdfDocumentEditor> operation in _operations)
        {
            operation(editor);
        }

        return appearances;
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
        IReadOnlyDictionary<string, PdfAppearanceDisposition> appearances;
        using (PdfDocumentEditor editor = session.Document.Edit())
        {
            appearances = Apply(editor);
            if (useAsync)
            {
                await editor.SaveIncrementalAsync(destination, leaveOpen: true, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                editor.SaveIncremental(destination, leaveOpen: true);
            }
        }

        return new PdfWorkflowResult(destination.Length > session.InputLength, destination.Length, appearances);
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
        IReadOnlyDictionary<string, PdfAppearanceDisposition> appearances = Apply(editor);
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
            return new PdfWorkflowResult(outputLength > session.InputLength, outputLength, appearances);
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
        IReadOnlyDictionary<string, PdfAppearanceDisposition> appearances;
        using (PdfDocumentEditor editor = session.Document.Edit())
        {
            appearances = Apply(editor);
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
        return new PdfWorkflowResult(length > session.InputLength, length, appearances);
    }
}
