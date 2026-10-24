using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Attachments;
using jaytwo.Pdf.Internal;
using jaytwo.Pdf.Metadata;
using jaytwo.Pdf.Signatures;

namespace jaytwo.Pdf;

/// <summary>
/// A PDF opened for inspection from a seekable stream or an <see cref="IPdfSource"/>. Pages, fields, attachments, signatures, and revisions are read when asked for.
/// </summary>
/// <remarks>
/// The document does not change itself. <see cref="Edit"/> returns the <see cref="PdfDocumentEditor"/> that makes page,
/// metadata, attachment, and form changes and writes them as incremental updates and signatures.
/// </remarks>
public sealed class PdfDocument : IDisposable
{
    private readonly PdfEngine _engine;
    private readonly PdfMetadata _metadata;
    private readonly PdfAttachmentCollection _attachments;
    private readonly PdfAcroForm _form;
    private PdfDocumentEditor? _editor;
    private bool _disposed;

    private PdfDocument(PdfEngine engine)
    {
        _engine = engine;
        _metadata = new PdfMetadata(engine);
        _attachments = new PdfAttachmentCollection(engine);
        _form = new PdfAcroForm(engine);
    }

    /// <summary>
    /// Gets the document information dictionary and XMP metadata.
    /// </summary>
    public PdfMetadata Metadata => _metadata;

    /// <summary>
    /// Gets the embedded files.
    /// </summary>
    public PdfAttachmentCollection Attachments => _attachments;

    /// <summary>
    /// Gets the AcroForm.
    /// </summary>
    public PdfAcroForm AcroForm => _form;

    /// <summary>
    /// Gets the number of pages.
    /// </summary>
    /// <remarks>Each access walks the page tree again.</remarks>
    public int PageCount => _engine.PageCount;

    /// <summary>
    /// Gets each page's media box, crop box, and rotation. Page content streams are not read.
    /// </summary>
    /// <remarks>Each access reads the document again, pending edits included, and returns a new list; keep the list rather than reading the property in a loop.</remarks>
    public IReadOnlyList<PdfPage> Pages => _engine.ListPages();

    /// <summary>
    /// Gets what the user password may do. An unencrypted document allows every operation.
    /// The owner password is not limited by these bits.
    /// </summary>
    /// <remarks>
    /// These are the standard security handler's permission bits only. A certification signature's DocMDP level is
    /// reported separately by <see cref="PdfSignatureInfo.CertificationLevel"/>, and the editor enforces both.
    /// </remarks>
    public PdfPermissions Permissions => _engine.Permissions;

    /// <summary>
    /// Gets every signature field, signed or not, with what its signature dictionary and CMS value say (spec R9).
    /// Nothing is validated, and the signed byte range is not read.
    /// </summary>
    /// <remarks>Each access reads the document again, pending edits included, and returns a new list; keep the list rather than reading the property in a loop.</remarks>
    /// <exception cref="PdfParseException">A signature's <c>/ByteRange</c>, <c>/M</c>, or CMS <c>/Contents</c> does not parse.</exception>
    public IReadOnlyList<PdfSignatureInfo> Signatures => _engine.ListSignatures();

    /// <summary>
    /// Gets the logical revisions in oldest-to-newest order (spec R10). A linearized file's main and first-page
    /// cross-reference sections are one revision. Signature coverage is reported from <c>/ByteRange</c>, not validated,
    /// and the signed byte ranges are not read.
    /// </summary>
    /// <remarks>Each access reads the document again, pending edits included, and returns a new list; keep the list rather than reading the property in a loop.</remarks>
    /// <exception cref="PdfParseException">A revision footer or a signature <c>/ByteRange</c> does not parse.</exception>
    public IReadOnlyList<PdfRevision> Revisions => _engine.ListRevisions();

    internal int RetainedObjectStreamCount => _engine.RetainedObjectStreamCount;

    internal long DecodedByteCount => _engine.DecodedByteCount;

    internal long CursorPeakRetainedBytes => _engine.CursorPeakRetainedBytes;

    internal bool HasUsageRights => _engine.HasUsageRights;

    /// <summary>
    /// Opens a PDF from a readable seekable stream. An encrypted file whose user password is empty opens with the permission bits.
    /// </summary>
    /// <param name="stream">The PDF. Offsets are file offsets, including any preamble before %PDF-.</param>
    /// <param name="leaveOpen">When false, disposing the document, or a failed open, disposes <paramref name="stream"/>.</param>
    /// <returns>The open document.</returns>
    /// <exception cref="PdfSourceChangedException">The stream's length changes while the document uses it.</exception>
    public static PdfDocument Open(Stream stream, bool leaveOpen = false)
    {
        return Open(stream, null, leaveOpen);
    }

    /// <summary>
    /// Opens a PDF from a readable seekable stream.
    /// </summary>
    /// <param name="stream">The PDF. Offsets are file offsets, including any preamble before %PDF-.</param>
    /// <param name="password">The user or owner password of a standard-security PDF. Null tries the empty user password. An unencrypted PDF ignores it.</param>
    /// <param name="leaveOpen">When false, disposing the document, or a failed open, disposes <paramref name="stream"/>.</param>
    /// <returns>The open document.</returns>
    /// <exception cref="PdfSourceChangedException">The stream's length changes while the document uses it.</exception>
    public static PdfDocument Open(Stream stream, string? password, bool leaveOpen = false)
    {
        return new PdfDocument(PdfEngine.Open(StreamSource(stream, leaveOpen), true, password, PdfCursor.DefaultRetainedWindows));
    }

    /// <summary>
    /// Opens a PDF from a random-access source (spec R13). An encrypted file whose user password is empty opens with the permission bits.
    /// </summary>
    /// <param name="source">The PDF snapshot. It must not change or be disposed while the document uses it.</param>
    /// <param name="leaveOpen">When false, the document owns <paramref name="source"/>: disposing the document, or a failed open, disposes it.</param>
    /// <returns>The open document.</returns>
    public static PdfDocument Open(IPdfSource source, bool leaveOpen)
    {
        return Open(source, null, leaveOpen);
    }

    /// <summary>
    /// Opens a PDF from a random-access source (spec R13).
    /// </summary>
    /// <param name="source">The PDF snapshot. It must not change or be disposed while the document uses it.</param>
    /// <param name="password">The user or owner password of a standard-security PDF. Null tries the empty user password. An unencrypted PDF ignores it.</param>
    /// <param name="leaveOpen">When false, the document owns <paramref name="source"/>: disposing the document, or a failed open, disposes it.</param>
    /// <returns>The open document.</returns>
    public static PdfDocument Open(IPdfSource source, string? password, bool leaveOpen)
    {
        return new PdfDocument(PdfEngine.Open(CheckSource(source), !leaveOpen, password, PdfCursor.DefaultRetainedWindows));
    }

    /// <summary>
    /// Opens a PDF from a readable seekable stream, reading it asynchronously. An encrypted file whose user password is empty opens with the permission bits.
    /// </summary>
    /// <param name="stream">The PDF. Offsets are file offsets, including any preamble before %PDF-.</param>
    /// <param name="leaveOpen">When false, disposing the document, or a failed or canceled open, disposes <paramref name="stream"/>.</param>
    /// <param name="cancellationToken">Cancels the open. A canceled open returns no document.</param>
    /// <returns>The open document.</returns>
    public static Task<PdfDocument> OpenAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        return OpenAsync(stream, null, leaveOpen, cancellationToken);
    }

    /// <summary>
    /// Opens a PDF from a readable seekable stream, reading it asynchronously.
    /// </summary>
    /// <param name="stream">The PDF. Offsets are file offsets, including any preamble before %PDF-.</param>
    /// <param name="password">The user or owner password of a standard-security PDF. Null tries the empty user password. An unencrypted PDF ignores it.</param>
    /// <param name="leaveOpen">When false, disposing the document, or a failed or canceled open, disposes <paramref name="stream"/>.</param>
    /// <param name="cancellationToken">Cancels the open. A canceled open returns no document.</param>
    /// <returns>The open document.</returns>
    public static Task<PdfDocument> OpenAsync(Stream stream, string? password, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        return OpenSourceAsync(StreamSource(stream, leaveOpen), true, password, cancellationToken);
    }

    /// <summary>
    /// Opens a PDF from a random-access source, awaiting every read (spec R13). An encrypted file whose user password is empty opens with the permission bits.
    /// </summary>
    /// <param name="source">The PDF snapshot. It must not change or be disposed while the document uses it.</param>
    /// <param name="leaveOpen">When false, the document owns <paramref name="source"/>: disposing the document, or a failed or canceled open, disposes it.</param>
    /// <param name="cancellationToken">Cancels the open. A canceled open returns no document.</param>
    /// <returns>The open document.</returns>
    public static Task<PdfDocument> OpenAsync(IPdfSource source, bool leaveOpen, CancellationToken cancellationToken = default)
    {
        return OpenAsync(source, null, leaveOpen, cancellationToken);
    }

    /// <summary>
    /// Opens a PDF from a random-access source, awaiting every read (spec R13).
    /// </summary>
    /// <param name="source">The PDF snapshot. It must not change or be disposed while the document uses it.</param>
    /// <param name="password">The user or owner password of a standard-security PDF. Null tries the empty user password. An unencrypted PDF ignores it.</param>
    /// <param name="leaveOpen">When false, the document owns <paramref name="source"/>: disposing the document, or a failed or canceled open, disposes it.</param>
    /// <param name="cancellationToken">Cancels the open. A canceled open returns no document.</param>
    /// <returns>The open document.</returns>
    public static Task<PdfDocument> OpenAsync(IPdfSource source, string? password, bool leaveOpen, CancellationToken cancellationToken = default)
    {
        return OpenSourceAsync(CheckSource(source), !leaveOpen, password, cancellationToken);
    }

    /// <summary>
    /// Starts an editor for this document. Every change, save, and signature goes through it.
    /// </summary>
    /// <remarks>
    /// A document has at most one open editor. Disposing the editor ends it without saving, and another can then start.
    /// The document's reads show the editor's changes, saved or pending.
    /// </remarks>
    /// <returns>The editor.</returns>
    /// <exception cref="InvalidOperationException">An editor of this document is still open.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    public PdfDocumentEditor Edit()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfDocument));
        }

        if (_editor != null)
        {
            throw new InvalidOperationException("An editor of this document is still open. Dispose it before starting another.");
        }

        _engine.AllowBreakingSignatures = false;
        _editor = new PdfDocumentEditor(this, _engine);
        return _editor;
    }

    /// <summary>
    /// Releases the document. Pending changes are not saved, and an open editor can no longer be used.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _engine.Dispose();
    }

    // W-108: opens with the given number of retained cursor windows; zero keeps none.
    internal static PdfDocument OpenWithCursorWindows(Stream stream, bool leaveOpen, int cursorWindows)
    {
        return new PdfDocument(PdfEngine.Open(StreamSource(stream, leaveOpen), true, null, cursorWindows));
    }

    internal void EndEdit(PdfDocumentEditor editor)
    {
        if (ReferenceEquals(_editor, editor))
        {
            _editor = null;
        }
    }

    private static IPdfSource StreamSource(Stream stream, bool leaveOpen)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("PDF input must be a readable seekable stream.", nameof(stream));
        }

        return new PdfStreamSource(stream, leaveOpen);
    }

    private static IPdfSource CheckSource(IPdfSource source)
    {
        return source ?? throw new ArgumentNullException(nameof(source));
    }

    private static async Task<PdfDocument> OpenSourceAsync(IPdfSource source, bool ownsSource, string? password, CancellationToken cancellationToken)
    {
        return new PdfDocument(await PdfEngine.OpenAsync(source, ownsSource, password, PdfCursor.DefaultRetainedWindows, cancellationToken).ConfigureAwait(false));
    }
}
