using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Attachments;

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
    // Spec F8: the only MIME types a path attachment's extension sets.
    private static readonly Dictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".xml"] = "text/xml",
        [".json"] = "application/json",
        [".pdf"] = "application/pdf",
        [".csv"] = "text/csv",
        [".txt"] = "text/plain",
    };

    private readonly object _gate = new object();
    private readonly WorkflowInput _input;
    private readonly List<Action<WorkflowSession, PdfDocumentEditor>> _operations = new List<Action<WorkflowSession, PdfDocumentEditor>>();
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
    /// Allows a flatten or a page rotation to change a document that has an approval signature or a document timestamp
    /// (spec F10).
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
    /// Adds an embedded file whose content is the rest of a stream (spec F8).
    /// </summary>
    /// <remarks>
    /// The stream's position is recorded now, and nothing is read. When the workflow runs, it seeks back to that position
    /// and calls <see cref="PdfAttachmentCollectionEditor.Add"/>, which reads the content through the end; the save reads it
    /// again. The content must not change before or during the run. A change before the run cannot be detected. A change
    /// during it makes the run throw <see cref="InvalidOperationException"/> without writing the destination, and so does a
    /// stream that is shorter than the recorded position when the run reaches it. An attachment with the name of an
    /// existing one replaces it. The stream is not disposed.
    /// </remarks>
    /// <param name="name">The file name.</param>
    /// <param name="content">A readable seekable stream positioned at the start of the content.</param>
    /// <param name="options">Optional description, MIME type, relationship, dates, compression, and page mode.</param>
    /// <returns>This workflow.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="content"/> is not readable and seekable.</exception>
    public PdfWorkflowBuilder Attach(string name, Stream content, PdfAttachmentOptions? options = null)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (!content.CanRead || !content.CanSeek)
        {
            throw new ArgumentException("Attachment content must be a readable seekable stream.", nameof(content));
        }

        long position = content.Position;
        return Add(editor =>
        {
            if (content.Length < position)
            {
                throw new InvalidOperationException("The content of attachment '" + name + "' is shorter than its position when the attachment was described.");
            }

            content.Position = position;
            editor.Attachments.Add(name, content, leaveOpen: true, options);
        });
    }

    /// <summary>
    /// Adds an embedded file whose content is a file (spec F8).
    /// </summary>
    /// <remarks>
    /// The file is opened when the workflow runs, once for both of the engine's reads, and closed when the run ends. A MIME
    /// type in <paramref name="options"/> applies. Otherwise the extension, matched without regard to case, sets it:
    /// <c>.xml</c> <c>text/xml</c>, <c>.json</c> <c>application/json</c>, <c>.pdf</c> <c>application/pdf</c>, <c>.csv</c>
    /// <c>text/csv</c>, and <c>.txt</c> <c>text/plain</c>. Any other extension leaves it unspecified. An attachment with the
    /// name of an existing one replaces it.
    /// </remarks>
    /// <param name="name">The file name stored in the document.</param>
    /// <param name="path">The file to attach.</param>
    /// <param name="options">Optional description, MIME type, relationship, dates, compression, and page mode.</param>
    /// <returns>This workflow.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public PdfWorkflowBuilder AttachFile(string name, string path, PdfAttachmentOptions? options = null)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (path.Length == 0)
        {
            throw new ArgumentException("The attachment path is empty.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        PdfAttachmentOptions? resolved = WithMimeType(options, fullPath);
        return Add((session, editor) => editor.Attachments.Add(name, session.OpenAttachment(fullPath), leaveOpen: true, resolved));
    }

    /// <summary>
    /// Removes every embedded file with the given name (spec F8).
    /// </summary>
    /// <remarks>
    /// The removal applies in the order described, so a file added before it is removed and one added after it is kept.
    /// </remarks>
    /// <param name="name">The file name.</param>
    /// <returns>This workflow.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfAttachmentNotFoundException">When the workflow runs, no embedded file has the name.</exception>
    public PdfWorkflowBuilder RemoveAttachment(string name)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        return Add(editor => editor.Attachments.Remove(name));
    }

    /// <summary>
    /// Sets a page's rotation (spec F9), through <see cref="PdfPageEditor.Rotation"/>.
    /// </summary>
    /// <remarks>
    /// On a document with an approval signature or a document timestamp, rotation needs
    /// <see cref="AllowBreakingExistingSignatures"/>.
    /// </remarks>
    /// <param name="pageNumber">The 1-based page number, as <see cref="PdfPage.Number"/> is.</param>
    /// <param name="degrees">A multiple of 90, positive or negative.</param>
    /// <returns>This workflow.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageNumber"/> is below 1, or <paramref name="degrees"/> is not a multiple of 90. When the workflow runs, <paramref name="pageNumber"/> is above the page count.</exception>
    public PdfWorkflowBuilder RotatePage(int pageNumber, int degrees)
    {
        CheckPage(pageNumber);
        CheckAngle(degrees);
        return Add(editor => Page(editor, pageNumber).Rotation = degrees);
    }

    /// <summary>
    /// Rotates a page by an angle, added to its rotation when the operation applies (spec F9).
    /// </summary>
    /// <remarks>
    /// Rotations apply in the order described, so two of them accumulate and a later <see cref="RotatePage"/> replaces
    /// them. On a document with an approval signature or a document timestamp, rotation needs
    /// <see cref="AllowBreakingExistingSignatures"/>.
    /// </remarks>
    /// <param name="pageNumber">The 1-based page number, as <see cref="PdfPage.Number"/> is.</param>
    /// <param name="degrees">A multiple of 90, positive for clockwise.</param>
    /// <returns>This workflow.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageNumber"/> is below 1, or <paramref name="degrees"/> is not a multiple of 90. When the workflow runs, <paramref name="pageNumber"/> is above the page count.</exception>
    public PdfWorkflowBuilder RotatePageBy(int pageNumber, int degrees)
    {
        CheckPage(pageNumber);
        CheckAngle(degrees);
        return Add(editor => Page(editor, pageNumber).Rotation += degrees % 360);
    }

    /// <summary>
    /// Rotates every page by an angle, added to each page's own rotation (spec F9).
    /// </summary>
    /// <remarks>
    /// On a document with an approval signature or a document timestamp, rotation needs
    /// <see cref="AllowBreakingExistingSignatures"/>.
    /// </remarks>
    /// <param name="degrees">A multiple of 90, positive for clockwise.</param>
    /// <returns>This workflow.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="degrees"/> is not a multiple of 90.</exception>
    public PdfWorkflowBuilder RotateAllPagesBy(int degrees)
    {
        CheckAngle(degrees);
        return Add(editor =>
        {
            foreach (PdfPageEditor page in editor.Pages)
            {
                page.Rotation += degrees % 360;
            }
        });
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

    private static void CheckPage(int pageNumber)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Pages are numbered from 1.");
        }
    }

    private static void CheckAngle(int degrees)
    {
        if (degrees % 90 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degrees), "Page rotation must be a multiple of 90 degrees.");
        }
    }

    private static PdfPageEditor Page(PdfDocumentEditor editor, int pageNumber)
    {
        IReadOnlyList<PdfPageEditor> pages = editor.Pages;
        if (pageNumber > pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "The document has no page " + pageNumber + "; it has " + pages.Count + ".");
        }

        return pages[pageNumber - 1];
    }

    private static PdfAttachmentOptions? WithMimeType(PdfAttachmentOptions? options, string path)
    {
        if (options?.MimeType != null || !MimeTypes.TryGetValue(Path.GetExtension(path), out string? mimeType))
        {
            return options;
        }

        return new PdfAttachmentOptions
        {
            Description = options?.Description,
            MimeType = mimeType,
            Relationship = options?.Relationship ?? PdfAttachmentRelationship.Unspecified,
            CreationDate = options?.CreationDate,
            ModificationDate = options?.ModificationDate,
            Compress = options?.Compress ?? false,
            OpenAttachmentsPanel = options?.OpenAttachmentsPanel ?? false,
        };
    }

    private PdfWorkflowBuilder Add(Action<PdfDocumentEditor> operation)
    {
        return Add((session, editor) => operation(editor));
    }

    private PdfWorkflowBuilder Add(Action<WorkflowSession, PdfDocumentEditor> operation)
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
    private IReadOnlyDictionary<string, PdfAppearanceDisposition> Apply(WorkflowSession session, PdfDocumentEditor editor)
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

        foreach (Action<WorkflowSession, PdfDocumentEditor> operation in _operations)
        {
            operation(session, editor);
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
            appearances = Apply(session, editor);
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
        IReadOnlyDictionary<string, PdfAppearanceDisposition> appearances = Apply(session, editor);
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
            appearances = Apply(session, editor);
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
