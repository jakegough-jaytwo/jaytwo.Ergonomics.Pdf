using System;
using System.Collections.Generic;
using System.IO;
using jaytwo.Pdf.Internal;

namespace jaytwo.Pdf.Metadata;

/// <summary>
/// Document information and XMP metadata. <see cref="PdfMetadataEditor"/> changes them.
/// </summary>
public sealed class PdfMetadata
{
    private readonly PdfEngine _engine;

    internal PdfMetadata(PdfEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Gets the document title.
    /// </summary>
    public string? Title => _engine.GetInfoText("Title");

    /// <summary>
    /// Gets the document author.
    /// </summary>
    public string? Author => _engine.GetInfoText("Author");

    /// <summary>
    /// Gets the document subject.
    /// </summary>
    public string? Subject => _engine.GetInfoText("Subject");

    /// <summary>
    /// Gets the document keywords.
    /// </summary>
    public string? Keywords => _engine.GetInfoText("Keywords");

    /// <summary>
    /// Gets the creating application.
    /// </summary>
    public string? Creator => _engine.GetInfoText("Creator");

    /// <summary>
    /// Gets the producing application.
    /// </summary>
    public string? Producer => _engine.GetInfoText("Producer");

    /// <summary>
    /// Gets the creation date.
    /// </summary>
    public DateTimeOffset? CreationDate => _engine.GetInfoDate("CreationDate");

    /// <summary>
    /// Gets the modification date.
    /// </summary>
    public DateTimeOffset? ModificationDate => _engine.GetInfoDate("ModDate");

    /// <summary>
    /// Gets the keys in the document information dictionary.
    /// </summary>
    public IReadOnlyList<string> Keys => _engine.InfoKeys();

    /// <summary>
    /// Gets a value indicating whether the catalog has a metadata stream.
    /// </summary>
    public bool HasXmp => _engine.HasXmp;

    /// <summary>
    /// Gets a document information value by its PDF key.
    /// </summary>
    /// <param name="key">The information dictionary key, such as Title or a private key.</param>
    /// <returns>The text value, or <see langword="null"/> when the key is absent.</returns>
    /// <remarks>
    /// CreationDate and ModDate are PDF date strings such as <c>D:20200101120000Z</c>. Trapped is the name True, False, or Unknown.
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
    }

    /// <summary>
    /// Opens the decoded XMP packet.
    /// </summary>
    /// <returns>A readable stream of the packet. The caller disposes it.</returns>
    public Stream OpenXmp()
    {
        return _engine.OpenXmp();
    }
}
