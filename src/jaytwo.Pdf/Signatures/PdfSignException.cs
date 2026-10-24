namespace jaytwo.Pdf.Signatures;

/// <summary>
/// Raised when a digital signature cannot be appended.
/// </summary>
public sealed class PdfSignException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSignException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PdfSignException(string message)
        : base(message)
    {
    }
}
