using System;
using System.Collections.Generic;
using System.IO;
using jaytwo.Ergonomics.Pdf.Internal;

namespace jaytwo.Ergonomics.Pdf.Metadata;

/// <summary>
/// Document information and XMP metadata.
/// </summary>
public sealed class PdfMetadata
{
    private readonly PdfEngine _engine;

    internal PdfMetadata(PdfEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Gets or sets the document title.
    /// </summary>
    public string? Title
    {
        get => _engine.GetInfoText("Title");
        set => _engine.SetInfoText("Title", value);
    }

    /// <summary>
    /// Gets or sets the document author.
    /// </summary>
    public string? Author
    {
        get => _engine.GetInfoText("Author");
        set => _engine.SetInfoText("Author", value);
    }

    /// <summary>
    /// Gets or sets the document subject.
    /// </summary>
    public string? Subject
    {
        get => _engine.GetInfoText("Subject");
        set => _engine.SetInfoText("Subject", value);
    }

    /// <summary>
    /// Gets or sets the document keywords.
    /// </summary>
    public string? Keywords
    {
        get => _engine.GetInfoText("Keywords");
        set => _engine.SetInfoText("Keywords", value);
    }

    /// <summary>
    /// Gets or sets the creating application.
    /// </summary>
    public string? Creator
    {
        get => _engine.GetInfoText("Creator");
        set => _engine.SetInfoText("Creator", value);
    }

    /// <summary>
    /// Gets or sets the producing application.
    /// </summary>
    public string? Producer
    {
        get => _engine.GetInfoText("Producer");
        set => _engine.SetInfoText("Producer", value);
    }

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    public DateTimeOffset? CreationDate
    {
        get => _engine.GetInfoDate("CreationDate");
        set => _engine.SetInfoDate("CreationDate", value, false);
    }

    /// <summary>
    /// Gets or sets the modification date. Setting this value keeps later edits from replacing it.
    /// </summary>
    public DateTimeOffset? ModificationDate
    {
        get => _engine.GetInfoDate("ModDate");
        set => _engine.SetInfoDate("ModDate", value, true);
    }

    /// <summary>
    /// Gets the keys in the document information dictionary.
    /// </summary>
    public IReadOnlyList<string> Keys => _engine.InfoKeys();

    /// <summary>
    /// Gets a value indicating whether the catalog has a metadata stream.
    /// </summary>
    public bool HasXmp => _engine.HasXmp;

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

            return _engine.GetInfoText(key);
        }

        set => _engine.SetInfo(key, value);
    }

    /// <summary>
    /// Opens the decoded XMP packet.
    /// </summary>
    /// <returns>A readable stream of the packet. The caller disposes it.</returns>
    public Stream OpenXmp()
    {
        return _engine.OpenXmp();
    }

    /// <summary>
    /// Replaces the metadata stream with the supplied packet bytes.
    /// </summary>
    /// <param name="utf8Packet">The XMP packet.</param>
    public void SetXmp(ReadOnlySpan<byte> utf8Packet)
    {
        _engine.SetXmp(utf8Packet);
    }
}
