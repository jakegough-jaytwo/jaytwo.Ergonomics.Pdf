namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Raised when a timestamp authority rejects a request or returns an unusable token.
/// </summary>
public sealed class PdfTimestampException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTimestampException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PdfTimestampException(string message)
        : base(message)
    {
    }
}
