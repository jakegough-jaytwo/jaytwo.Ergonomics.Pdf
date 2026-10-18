namespace jaytwo.Ergonomics.Pdf.Attachments;

/// <summary>
/// Optional information stored with a new embedded file.
/// </summary>
public sealed class PdfAttachmentOptions
{
    /// <summary>
    /// Gets the file description stored in /Desc.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the MIME type stored as the embedded file /Subtype.
    /// </summary>
    public string? MimeType { get; init; }
}
