using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Ergonomics.Pdf.AcroForm;
using jaytwo.Ergonomics.Pdf.Attachments;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Ergonomics.Pdf.Metadata;
using jaytwo.Ergonomics.Pdf.Signatures;

namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// A seekable PDF opened for incremental metadata, attachment, form, and signature updates.
/// </summary>
public sealed class PdfDocument : IDisposable
{
    private readonly PdfEngine _engine;
    private readonly PdfMetadata _metadata;
    private readonly PdfAttachmentCollection _attachments;
    private readonly PdfAcroForm _form;

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

    internal int RetainedObjectStreamCount => _engine.RetainedObjectStreamCount;

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
    /// Appends one incremental update to the stream passed to <see cref="Open(Stream, bool)"/>.
    /// </summary>
    public void SaveIncremental()
    {
        _engine.SaveIncremental();
    }

    /// <summary>
    /// Appends one incremental update containing the pending edits.
    /// </summary>
    /// <param name="destination">The same stream, or an empty writable stream that receives a copy plus the update.</param>
    /// <param name="leaveOpen">When <paramref name="destination"/> is a different stream, controls whether the document owns it.</param>
    public void SaveIncremental(Stream destination, bool leaveOpen = false)
    {
        _engine.SaveIncremental(destination, leaveOpen);
    }

    /// <summary>
    /// Appends one incremental update to the stream passed to <see cref="Open(Stream, bool)"/>.
    /// It produces the same bytes as <see cref="SaveIncremental()"/>.
    /// </summary>
    /// <param name="cancellationToken">Observed before the update is written.</param>
    /// <returns>A task that completes when the update is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled. The stream is unchanged, and the pending edits remain.</exception>
    public Task SaveIncrementalAsync(CancellationToken cancellationToken = default)
    {
        return _engine.SaveIncrementalAsync(cancellationToken);
    }

    /// <summary>
    /// Appends one incremental update containing the pending edits. It produces the same bytes as <see cref="SaveIncremental(Stream, bool)"/>.
    /// A different destination receives the copy with asynchronous reads and writes; the update itself is serialized synchronously.
    /// </summary>
    /// <param name="destination">The same stream, or an empty writable stream that receives a copy plus the update.</param>
    /// <param name="leaveOpen">When <paramref name="destination"/> is a different stream, controls whether the document owns it.</param>
    /// <param name="cancellationToken">Observed before the write and between the 1 MiB blocks of the copy.</param>
    /// <returns>A task that completes when the update is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled. The destination is truncated to its starting length, and the pending edits remain.</exception>
    public Task SaveIncrementalAsync(Stream destination, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        return _engine.SaveIncrementalAsync(destination, leaveOpen, cancellationToken);
    }

    /// <summary>
    /// Appends one incremental signature revision to the stream passed to <see cref="Open(Stream, bool)"/>, including any pending edits.
    /// The provider is called with <see cref="CancellationToken.None"/>, and this method waits for it.
    /// </summary>
    /// <param name="options">Signature field, page, and reserved contents size.</param>
    /// <param name="provider">Builds the /Contents bytes. <see cref="IPdfSignatureProvider.Kind"/> must match <see cref="PdfSignatureOptions.Kind"/>.</param>
    public void Sign(PdfSignatureOptions options, IPdfSignatureProvider provider)
    {
        _engine.Sign(options, provider);
    }

    /// <summary>
    /// Appends one incremental signature revision, including any pending edits.
    /// The provider is called with <see cref="CancellationToken.None"/>, and this method waits for it.
    /// </summary>
    /// <param name="destination">The same stream, or an empty writable stream.</param>
    /// <param name="options">Signature field, page, and reserved contents size.</param>
    /// <param name="provider">Builds the /Contents bytes. <see cref="IPdfSignatureProvider.Kind"/> must match <see cref="PdfSignatureOptions.Kind"/>.</param>
    /// <param name="leaveOpen">When <paramref name="destination"/> is a different stream, controls whether the document owns it.</param>
    public void Sign(Stream destination, PdfSignatureOptions options, IPdfSignatureProvider provider, bool leaveOpen = false)
    {
        _engine.Sign(destination, options, provider, leaveOpen);
    }

    /// <summary>
    /// Appends one incremental signature revision to the stream passed to <see cref="Open(Stream, bool)"/>, including any pending edits.
    /// It produces the same bytes as <see cref="Sign(PdfSignatureOptions, IPdfSignatureProvider)"/> for the same provider output.
    /// </summary>
    /// <param name="options">Signature field, page, and reserved contents size.</param>
    /// <param name="provider">Builds the /Contents bytes. <see cref="IPdfSignatureProvider.Kind"/> must match <see cref="PdfSignatureOptions.Kind"/>.</param>
    /// <param name="cancellationToken">Passed to the provider, which passes it to the signer and the timestamp authority, and observed before the revision is written.</param>
    /// <returns>A task that completes when the revision is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled. The stream is unchanged, and the document is as it was before the call.</exception>
    public Task SignAsync(PdfSignatureOptions options, IPdfSignatureProvider provider, CancellationToken cancellationToken = default)
    {
        return _engine.SignAsync(options, provider, cancellationToken);
    }

    /// <summary>
    /// Appends one incremental signature revision, including any pending edits.
    /// It produces the same bytes as <see cref="Sign(Stream, PdfSignatureOptions, IPdfSignatureProvider, bool)"/> for the same provider output.
    /// A different destination receives the copy and the revision with asynchronous reads and writes.
    /// </summary>
    /// <param name="destination">The same stream, or an empty writable stream.</param>
    /// <param name="options">Signature field, page, and reserved contents size.</param>
    /// <param name="provider">Builds the /Contents bytes. <see cref="IPdfSignatureProvider.Kind"/> must match <see cref="PdfSignatureOptions.Kind"/>.</param>
    /// <param name="leaveOpen">When <paramref name="destination"/> is a different stream, controls whether the document owns it.</param>
    /// <param name="cancellationToken">Passed to the provider, and observed before the write and between the 1 MiB blocks of the copy.</param>
    /// <returns>A task that completes when the revision is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled. The destination is truncated to its starting length, and the document is as it was before the call.</exception>
    public Task SignAsync(Stream destination, PdfSignatureOptions options, IPdfSignatureProvider provider, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        return _engine.SignAsync(destination, options, provider, leaveOpen, cancellationToken);
    }

    /// <summary>
    /// Releases the document. Pending edits are not saved.
    /// </summary>
    public void Dispose()
    {
        _engine.Dispose();
    }
}
