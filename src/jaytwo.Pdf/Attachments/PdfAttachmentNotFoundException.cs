namespace jaytwo.Pdf.Attachments;

/// <summary>
/// Raised when an embedded file name is not in the document.
/// </summary>
public sealed class PdfAttachmentNotFoundException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAttachmentNotFoundException"/> class.
    /// </summary>
    /// <param name="name">The attachment name that was requested.</param>
    public PdfAttachmentNotFoundException(string name)
        : base($"Attachment '{name}' was not found.")
    {
        Name = name;
    }

    /// <summary>
    /// Gets the attachment name that was requested.
    /// </summary>
    public string Name { get; }
}
