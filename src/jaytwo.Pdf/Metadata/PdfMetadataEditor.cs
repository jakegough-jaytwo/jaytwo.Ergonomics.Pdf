using System;

namespace jaytwo.Pdf.Metadata;

/// <summary>
/// Changes document information and XMP metadata. The changes are written by the next save or signature.
/// </summary>
public sealed class PdfMetadataEditor
{
    private readonly PdfDocumentEditor _editor;

    internal PdfMetadataEditor(PdfDocumentEditor editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// Gets or sets the document title.
    /// </summary>
    public string? Title
    {
        get => _editor.Engine.GetInfoText("Title");
        set => _editor.Engine.SetInfoText("Title", value);
    }

    /// <summary>
    /// Gets or sets the document author.
    /// </summary>
    public string? Author
    {
        get => _editor.Engine.GetInfoText("Author");
        set => _editor.Engine.SetInfoText("Author", value);
    }

    /// <summary>
    /// Gets or sets the document subject.
    /// </summary>
    public string? Subject
    {
        get => _editor.Engine.GetInfoText("Subject");
        set => _editor.Engine.SetInfoText("Subject", value);
    }

    /// <summary>
    /// Gets or sets the document keywords.
    /// </summary>
    public string? Keywords
    {
        get => _editor.Engine.GetInfoText("Keywords");
        set => _editor.Engine.SetInfoText("Keywords", value);
    }

    /// <summary>
    /// Gets or sets the creating application.
    /// </summary>
    public string? Creator
    {
        get => _editor.Engine.GetInfoText("Creator");
        set => _editor.Engine.SetInfoText("Creator", value);
    }

    /// <summary>
    /// Gets or sets the producing application.
    /// </summary>
    public string? Producer
    {
        get => _editor.Engine.GetInfoText("Producer");
        set => _editor.Engine.SetInfoText("Producer", value);
    }

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    public DateTimeOffset? CreationDate
    {
        get => _editor.Engine.GetInfoDate("CreationDate");
        set => _editor.Engine.SetInfoDate("CreationDate", value, false);
    }

    /// <summary>
    /// Gets or sets the modification date. Setting this value keeps later changes from replacing it.
    /// </summary>
    public DateTimeOffset? ModificationDate
    {
        get => _editor.Engine.GetInfoDate("ModDate");
        set => _editor.Engine.SetInfoDate("ModDate", value, true);
    }

    /// <summary>
    /// Gets or sets a document information value by its PDF key.
    /// </summary>
    /// <param name="key">The information dictionary key, such as Title or a private key.</param>
    /// <returns>The text value, or <see langword="null"/> when the key is absent.</returns>
    /// <remarks>
    /// CreationDate and ModDate are PDF date strings such as <c>D:20200101120000Z</c>. Trapped is the name True, False, or Unknown.
    /// Setting a standard text key also updates the matching XMP property when the packet is well formed.
    /// </remarks>
    public string? this[string key]
    {
        get
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return _editor.Engine.GetInfoText(key);
        }

        set => _editor.Engine.SetInfo(key, value);
    }

    /// <summary>
    /// Replaces the metadata stream with the supplied packet bytes.
    /// </summary>
    /// <param name="utf8Packet">The XMP packet.</param>
    /// <exception cref="PdfXmpException">The packet is malformed, contains a DTD, or exceeds 32 MiB.</exception>
    public void SetXmp(ReadOnlySpan<byte> utf8Packet)
    {
        _editor.Engine.SetXmp(utf8Packet);
    }
}
