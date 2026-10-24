using System;
using System.Collections.Generic;
using System.IO;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Attachments;
using jaytwo.Pdf.Internal;
using jaytwo.Pdf.Metadata;
using jaytwo.Pdf.Signatures;

namespace jaytwo.Pdf;

/// <summary>
/// A seekable PDF opened for inspection. Pages, fields, attachments, signatures, and revisions are read when asked for.
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
    public int PageCount => _engine.PageCount;

    /// <summary>
    /// Gets each page's media box, crop box, and rotation. Page content streams are not read.
    /// </summary>
    public IReadOnlyList<PdfPage> Pages => _engine.ListPages();

    /// <summary>
    /// Gets what the user password may do. An unencrypted document allows every operation.
    /// The owner password is not limited by these bits.
    /// </summary>
    public PdfPermissions Permissions => _engine.Permissions;

    /// <summary>
    /// Gets every signature field, signed or not, with what its signature dictionary and CMS value say (spec R9).
    /// Nothing is validated, and the signed byte range is not read.
    /// </summary>
    /// <exception cref="PdfParseException">A signature's <c>/ByteRange</c>, <c>/M</c>, or CMS <c>/Contents</c> does not parse.</exception>
    public IReadOnlyList<PdfSignatureInfo> Signatures => _engine.ListSignatures();

    /// <summary>
    /// Gets the logical revisions in oldest-to-newest order (spec R10). A linearized file's main and first-page
    /// cross-reference sections are one revision. Signature coverage is reported from <c>/ByteRange</c>, not validated,
    /// and the signed byte ranges are not read.
    /// </summary>
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
    /// <param name="leaveOpen">When false, disposing the document disposes <paramref name="stream"/>.</param>
    /// <returns>The open document.</returns>
    public static PdfDocument Open(Stream stream, bool leaveOpen = false)
    {
        return Open(stream, null, leaveOpen);
    }

    /// <summary>
    /// Opens a PDF from a readable seekable stream.
    /// </summary>
    /// <param name="stream">The PDF. Offsets are file offsets, including any preamble before %PDF-.</param>
    /// <param name="password">The user or owner password of a standard-security PDF. Null tries the empty user password. An unencrypted PDF ignores it.</param>
    /// <param name="leaveOpen">When false, disposing the document disposes <paramref name="stream"/>.</param>
    /// <returns>The open document.</returns>
    public static PdfDocument Open(Stream stream, string? password, bool leaveOpen = false)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("PDF input must be a readable seekable stream.", nameof(stream));
        }

        return new PdfDocument(PdfEngine.Open(stream, leaveOpen, password));
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
        return new PdfDocument(PdfEngine.Open(stream, leaveOpen, null, cursorWindows));
    }

    internal void EndEdit(PdfDocumentEditor editor)
    {
        if (ReferenceEquals(_editor, editor))
        {
            _editor = null;
        }
    }
}
