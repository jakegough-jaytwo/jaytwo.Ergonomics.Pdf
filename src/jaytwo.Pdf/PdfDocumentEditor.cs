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
/// Changes a <see cref="PdfDocument"/> and writes the changes as incremental updates and signatures.
/// </summary>
/// <remarks>
/// <para>
/// Page, metadata, attachment, and form changes are pending until a save or signature writes them. One save writes every
/// pending change as one revision; a signature or document timestamp writes them with its own revision. Disposing the
/// editor does not save and does not discard: pending changes stay with the document, whose reads show them, and the
/// next editor of the document writes them.
/// </para>
/// <para>
/// A save or signature fails in one of three ways. A validation failure is found before the destination is written: an
/// argument, a destination that is neither the opened stream nor an empty writable seekable stream, a permission or
/// certification rule, an output that does not fit a classic cross-reference table, or a signature option. A processing
/// failure is found while the output is prepared: a malformed object, a source whose length changed, or a signer,
/// timestamp, size, or cancellation failure. An output failure comes from the destination itself. A signature revision is
/// prepared in memory, so its processing failures leave the destination unwritten. When a save or signature fails after
/// writing starts, the editor sets the destination back to its starting length: empty, or the opened stream's length.
/// That rollback is best effort, because a failing destination can refuse it too; no destination is written atomically.
/// </para>
/// <para>
/// The editor is not thread-safe. It cannot be used after it or its document is disposed.
/// </para>
/// </remarks>
public sealed class PdfDocumentEditor : IDisposable
{
    private readonly PdfDocument _document;
    private readonly PdfEngine _engine;
    private readonly PdfMetadataEditor _metadata;
    private readonly PdfAttachmentCollectionEditor _attachments;
    private readonly PdfAcroFormEditor _form;
    private bool _disposed;

    internal PdfDocumentEditor(PdfDocument document, PdfEngine engine)
    {
        _document = document;
        _engine = engine;
        _metadata = new PdfMetadataEditor(this);
        _attachments = new PdfAttachmentCollectionEditor(this);
        _form = new PdfAcroFormEditor(this);
    }

    /// <summary>
    /// Gets the document information and XMP changes.
    /// </summary>
    public PdfMetadataEditor Metadata => _metadata;

    /// <summary>
    /// Gets the embedded-file changes.
    /// </summary>
    public PdfAttachmentCollectionEditor Attachments => _attachments;

    /// <summary>
    /// Gets the form fill and flatten operations.
    /// </summary>
    public PdfAcroFormEditor AcroForm => _form;

    /// <summary>
    /// Gets each page's settable rotation. Page content streams are not read.
    /// </summary>
    public IReadOnlyList<PdfPageEditor> Pages
    {
        get
        {
            IReadOnlyList<PdfPage> pages = Engine.ListPages();
            var result = new List<PdfPageEditor>(pages.Count);
            foreach (PdfPage page in pages)
            {
                result.Add(new PdfPageEditor(this, page.ObjectId, page.Number, page.Rotation));
            }

            return result;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether page-content changes such as <see cref="PdfAcroFormEditor.Flatten()"/> and page rotation may change a document that has a signature.
    /// </summary>
    /// <remarks>
    /// Flattening and page rotation change page presentation, which no signature permits, so validators report every earlier
    /// approval signature or document timestamp as modified. The earlier signatures' bytes stay a prefix of the file. Without
    /// this opt-in, either operation throws <see cref="PdfSignatureConflictException"/> on such a document. A certified document
    /// stays locked against both operations either way, and signed signature fields are never flattened. Each editor starts
    /// with this value false.
    /// </remarks>
    public bool AllowBreakingSignatures
    {
        get => Engine.AllowBreakingSignatures;
        set => Engine.AllowBreakingSignatures = value;
    }

    internal PdfEngine Engine
    {
        get
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PdfDocumentEditor));
            }

            return _engine;
        }
    }

    /// <summary>
    /// Writes an ordered selection of this document's pages to a new, unencrypted PDF.
    /// </summary>
    /// <param name="pageNumbers">One input occurrence: positive, 1-based page numbers, with repetitions allowed.</param>
    /// <param name="destination">An empty writable destination. It stays open and need not seek.</param>
    /// <remarks>
    /// The source remains unchanged. Supported links and appearances are preserved; signature widgets are omitted. Other form widgets and
    /// encrypted inputs are currently refused. Document metadata, attachments, outlines, labels, and tags are omitted.
    /// Unsupported associated files, layers, and dynamic XFA are refused before output. A failure after writing begins
    /// can leave an incomplete file, which the caller must discard. Do not use the source stream as the destination.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A page number is outside the source's page range.</exception>
    /// <exception cref="PdfAssemblyNotSupportedException">The input contains an unsupported assembly structure.</exception>
    /// <exception cref="PdfResourceLimitException">A structural assembly budget is exceeded.</exception>
    public void ExtractPages(IEnumerable<int> pageNumbers, Stream destination)
    {
        Engine.ExtractPages(pageNumbers, destination, PdfExternalLinkMode.PreserveSupported);
    }

    /// <summary>Writes selected pages to a new PDF and reports aggregate annotation omissions.</summary>
    /// <param name="pageNumbers">Positive, 1-based source page numbers, in output order.</param>
    /// <param name="destination">An empty writable stream, left open; it need not seek.</param>
    /// <param name="externalLinks">Whether supported external links survive.</param>
    /// <returns>Counts by omission reason, with no annotation history.</returns>
    /// <remarks>Uses the same failure and input rules as <see cref="ExtractPages(IEnumerable{int}, Stream)"/>.</remarks>
    public PdfAssemblyResult ExtractPages(IEnumerable<int> pageNumbers, Stream destination, PdfExternalLinkMode externalLinks)
    {
        return Engine.ExtractPages(pageNumbers, destination, externalLinks);
    }

    /// <summary>
    /// Appends one incremental update containing the pending changes to the stream passed to <see cref="PdfDocument.Open(Stream, bool)"/>.
    /// </summary>
    /// <remarks>
    /// With no pending change nothing is written. A failure follows the classes described on <see cref="PdfDocumentEditor"/>,
    /// and the pending changes remain.
    /// </remarks>
    public void SaveIncremental()
    {
        Engine.SaveIncremental();
    }

    /// <summary>
    /// Appends one incremental update containing the pending changes.
    /// </summary>
    /// <remarks>
    /// A different destination receives a copy of the opened bytes and then the update, and becomes the stream the document
    /// reads. A failure follows the classes described on <see cref="PdfDocumentEditor"/>, and the pending changes remain.
    /// </remarks>
    /// <param name="destination">The same stream, or an empty writable seekable stream that receives a copy plus the update.</param>
    /// <param name="leaveOpen">When <paramref name="destination"/> is a different stream, controls whether the document owns it.</param>
    public void SaveIncremental(Stream destination, bool leaveOpen = false)
    {
        Engine.SaveIncremental(destination, leaveOpen);
    }

    /// <summary>
    /// Appends one incremental update containing the pending changes to the stream passed to <see cref="PdfDocument.Open(Stream, bool)"/>.
    /// It produces the same bytes as <see cref="SaveIncremental()"/>.
    /// </summary>
    /// <remarks>
    /// A failure follows the classes described on <see cref="PdfDocumentEditor"/>, and the pending changes remain.
    /// </remarks>
    /// <param name="cancellationToken">Observed before the update is written.</param>
    /// <returns>A task that completes when the update is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled. The stream is unchanged, and the pending changes remain.</exception>
    public Task SaveIncrementalAsync(CancellationToken cancellationToken = default)
    {
        return Engine.SaveIncrementalAsync(cancellationToken);
    }

    /// <summary>
    /// Appends one incremental update containing the pending changes. It produces the same bytes as <see cref="SaveIncremental(Stream, bool)"/>.
    /// A different destination receives the copy with asynchronous reads and writes; the update itself is serialized synchronously.
    /// </summary>
    /// <remarks>
    /// A failure follows the classes described on <see cref="PdfDocumentEditor"/>, and the pending changes remain.
    /// </remarks>
    /// <param name="destination">The same stream, or an empty writable seekable stream that receives a copy plus the update.</param>
    /// <param name="leaveOpen">When <paramref name="destination"/> is a different stream, controls whether the document owns it.</param>
    /// <param name="cancellationToken">Observed before the write and between the 1 MiB blocks of the copy.</param>
    /// <returns>A task that completes when the update is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled. The destination is truncated to its starting length, and the pending changes remain.</exception>
    public Task SaveIncrementalAsync(Stream destination, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        return Engine.SaveIncrementalAsync(destination, leaveOpen, cancellationToken);
    }

    /// <summary>
    /// Appends one incremental signature revision to the stream passed to <see cref="PdfDocument.Open(Stream, bool)"/>, including any pending changes.
    /// The provider is called with <see cref="CancellationToken.None"/>, and this method waits for it.
    /// </summary>
    /// <remarks>
    /// A failure follows the classes described on <see cref="PdfDocumentEditor"/>, and the document is as it was before the call.
    /// </remarks>
    /// <param name="options">Signature field, page, and reserved contents size.</param>
    /// <param name="provider">Builds the /Contents bytes. <see cref="IPdfSignatureProvider.Kind"/> must match <see cref="PdfSignatureOptions.Kind"/>.</param>
    public void Sign(PdfSignatureOptions options, IPdfSignatureProvider provider)
    {
        Engine.Sign(options, provider);
    }

    /// <summary>
    /// Appends one incremental signature revision, including any pending changes.
    /// The provider is called with <see cref="CancellationToken.None"/>, and this method waits for it.
    /// </summary>
    /// <remarks>
    /// A failure follows the classes described on <see cref="PdfDocumentEditor"/>, and the document is as it was before the call.
    /// </remarks>
    /// <param name="destination">The same stream, or an empty writable seekable stream.</param>
    /// <param name="options">Signature field, page, and reserved contents size.</param>
    /// <param name="provider">Builds the /Contents bytes. <see cref="IPdfSignatureProvider.Kind"/> must match <see cref="PdfSignatureOptions.Kind"/>.</param>
    /// <param name="leaveOpen">When <paramref name="destination"/> is a different stream, controls whether the document owns it.</param>
    public void Sign(Stream destination, PdfSignatureOptions options, IPdfSignatureProvider provider, bool leaveOpen = false)
    {
        Engine.Sign(destination, options, provider, leaveOpen);
    }

    /// <summary>
    /// Appends one incremental signature revision to the stream passed to <see cref="PdfDocument.Open(Stream, bool)"/>, including any pending changes.
    /// It produces the same bytes as <see cref="Sign(PdfSignatureOptions, IPdfSignatureProvider)"/> for the same provider output.
    /// </summary>
    /// <remarks>
    /// A failure follows the classes described on <see cref="PdfDocumentEditor"/>, and the document is as it was before the call.
    /// </remarks>
    /// <param name="options">Signature field, page, and reserved contents size.</param>
    /// <param name="provider">Builds the /Contents bytes. <see cref="IPdfSignatureProvider.Kind"/> must match <see cref="PdfSignatureOptions.Kind"/>.</param>
    /// <param name="cancellationToken">Passed to the provider, which passes it to the signer and the timestamp authority, and observed before the revision is written.</param>
    /// <returns>A task that completes when the revision is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled. The stream is unchanged, and the document is as it was before the call.</exception>
    public Task SignAsync(PdfSignatureOptions options, IPdfSignatureProvider provider, CancellationToken cancellationToken = default)
    {
        return Engine.SignAsync(options, provider, cancellationToken);
    }

    /// <summary>
    /// Appends one incremental signature revision, including any pending changes.
    /// It produces the same bytes as <see cref="Sign(Stream, PdfSignatureOptions, IPdfSignatureProvider, bool)"/> for the same provider output.
    /// A different destination receives the copy and the revision with asynchronous reads and writes.
    /// </summary>
    /// <remarks>
    /// A failure follows the classes described on <see cref="PdfDocumentEditor"/>, and the document is as it was before the call.
    /// </remarks>
    /// <param name="destination">The same stream, or an empty writable seekable stream.</param>
    /// <param name="options">Signature field, page, and reserved contents size.</param>
    /// <param name="provider">Builds the /Contents bytes. <see cref="IPdfSignatureProvider.Kind"/> must match <see cref="PdfSignatureOptions.Kind"/>.</param>
    /// <param name="leaveOpen">When <paramref name="destination"/> is a different stream, controls whether the document owns it.</param>
    /// <param name="cancellationToken">Passed to the provider, and observed before the write and between the 1 MiB blocks of the copy.</param>
    /// <returns>A task that completes when the revision is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled. The destination is truncated to its starting length, and the document is as it was before the call.</exception>
    public Task SignAsync(Stream destination, PdfSignatureOptions options, IPdfSignatureProvider provider, bool leaveOpen = false, CancellationToken cancellationToken = default)
    {
        return Engine.SignAsync(destination, options, provider, leaveOpen, cancellationToken);
    }

    /// <summary>
    /// Ends the editor. Pending changes are not saved; they stay with the document for its next editor.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _document.EndEdit(this);
    }
}
