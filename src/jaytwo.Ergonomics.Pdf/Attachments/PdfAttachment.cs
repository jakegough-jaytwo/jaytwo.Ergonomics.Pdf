using System.IO;
using jaytwo.Ergonomics.Pdf.Internal;

namespace jaytwo.Ergonomics.Pdf.Attachments;

/// <summary>
/// An embedded file in the document name tree.
/// </summary>
public sealed class PdfAttachment
{
    private readonly PdfEngine _engine;

    internal PdfAttachment(PdfEngine engine, string name, string? description, string? mimeType, long? size)
    {
        _engine = engine;
        Name = name;
        Description = description;
        MimeType = mimeType;
        Size = size;
    }

    /// <summary>
    /// Gets the attachment name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description from /Desc.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the MIME type from the embedded file /Subtype.
    /// </summary>
    public string? MimeType { get; }

    /// <summary>
    /// Gets /Params /Size, or <see langword="null"/> when the parameter is absent.
    /// </summary>
    public long? Size { get; }

    /// <summary>
    /// Opens the decoded file bytes. The caller disposes the stream.
    /// </summary>
    /// <returns>A readable stream of the attachment.</returns>
    public Stream Open()
    {
        return _engine.OpenAttachment(Name);
    }
}
