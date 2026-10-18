using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using jaytwo.Ergonomics.Pdf.Internal;

namespace jaytwo.Ergonomics.Pdf.Attachments;

/// <summary>
/// The document's embedded files.
/// </summary>
public sealed class PdfAttachmentCollection : IReadOnlyList<PdfAttachment>
{
    private readonly PdfEngine _engine;

    internal PdfAttachmentCollection(PdfEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Gets the number of embedded files.
    /// </summary>
    public int Count => _engine.ListAttachments().Count;

    /// <summary>
    /// Gets the attachment at the given index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The attachment.</returns>
    public PdfAttachment this[int index] => _engine.ListAttachments()[index];

    /// <summary>
    /// Gets the first attachment with the given name.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <returns>The attachment.</returns>
    public PdfAttachment this[string name]
    {
        get
        {
            if (!TryGetValue(name, out PdfAttachment? attachment))
            {
                throw new PdfAttachmentNotFoundException(name);
            }

            return attachment!;
        }
    }

    /// <summary>
    /// Looks up the first attachment with the given name.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <param name="attachment">The attachment, when found.</param>
    /// <returns><see langword="true"/> when an attachment with that name exists.</returns>
    public bool TryGetValue(string name, out PdfAttachment? attachment)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        foreach (PdfAttachment item in this)
        {
            if (item.Name == name)
            {
                attachment = item;
                return true;
            }
        }

        attachment = null;
        return false;
    }

    /// <summary>
    /// Adds an embedded file. The content stream must be seekable and stay open until the document is saved.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <param name="content">The file bytes, read from the current position through the end.</param>
    /// <param name="leaveOpen">When false, the document disposes <paramref name="content"/>.</param>
    /// <param name="options">Optional description and MIME type.</param>
    /// <returns>The attachment that was added.</returns>
    public PdfAttachment Add(string name, Stream content, bool leaveOpen = false, PdfAttachmentOptions? options = null)
    {
        return _engine.AddAttachment(name, content, leaveOpen, options);
    }

    /// <summary>
    /// Removes every embedded file with the given name.
    /// </summary>
    /// <param name="name">The file name.</param>
    public void Remove(string name)
    {
        _engine.RemoveAttachment(name);
    }

    /// <summary>
    /// Returns an enumerator over the embedded files.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<PdfAttachment> GetEnumerator()
    {
        return _engine.ListAttachments().GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
