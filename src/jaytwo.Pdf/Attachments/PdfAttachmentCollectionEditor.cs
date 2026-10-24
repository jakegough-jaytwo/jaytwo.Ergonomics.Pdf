using System;
using System.IO;

namespace jaytwo.Pdf.Attachments;

/// <summary>
/// Adds and removes the document's embedded files. The changes are written by the next save or signature.
/// </summary>
public sealed class PdfAttachmentCollectionEditor
{
    private readonly PdfDocumentEditor _editor;

    internal PdfAttachmentCollectionEditor(PdfDocumentEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// Adds an embedded file. The content stream must be seekable and stay open, unchanged, until the change is
    /// saved. <c>Add</c> reads the content once to compute /Params /CheckSum, the MD5 of the bytes, and the
    /// compressed length when <see cref="PdfAttachmentOptions.Compress"/> is set. The save reads it again and throws
    /// <see cref="InvalidOperationException"/>, leaving the destination unchanged, when the bytes no longer match.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <param name="content">The file bytes, read from the current position through the end.</param>
    /// <param name="leaveOpen">When false, the document disposes <paramref name="content"/>.</param>
    /// <param name="options">Optional description, MIME type, relationship, dates, compression, and page mode.</param>
    /// <returns>The attachment that was added.</returns>
    public PdfAttachment Add(string name, Stream content, bool leaveOpen = false, PdfAttachmentOptions? options = null)
    {
        return _editor.Engine.AddAttachment(name, content, leaveOpen, options);
    }

    /// <summary>
    /// Removes every embedded file with the given name from the /EmbeddedFiles name tree and the catalog /AF. An
    /// incremental save keeps the earlier revisions, so the file's bytes stay in the output and can be recovered from
    /// it until the document is rewritten by compaction (Phase 2).
    /// </summary>
    /// <param name="name">The file name.</param>
    public void Remove(string name)
    {
        _editor.Engine.RemoveAttachment(name);
    }
}
