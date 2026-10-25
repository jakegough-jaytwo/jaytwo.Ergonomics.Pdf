namespace jaytwo.Pdf.Attachments;

/// <summary>
/// Raised when decoded attachment content exceeds a caller-selected byte limit.
/// </summary>
public sealed class PdfAttachmentTooLargeException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAttachmentTooLargeException"/> class.
    /// </summary>
    /// <param name="name">The untrusted attachment name.</param>
    /// <param name="limit">The decoded-byte limit.</param>
    public PdfAttachmentTooLargeException(string name, long limit)
        : base($"Attachment '{name}' exceeds the decoded-byte limit of {limit} bytes.")
    {
        Name = name;
        Limit = limit;
    }

    /// <summary>
    /// Gets the untrusted attachment name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the decoded-byte limit.
    /// </summary>
    public long Limit { get; }
}
