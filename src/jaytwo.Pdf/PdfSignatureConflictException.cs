namespace jaytwo.Pdf;

/// <summary>
/// Raised when a change would make validators report an existing signature as modified beyond what it permits.
/// </summary>
public sealed class PdfSignatureConflictException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSignatureConflictException"/> class.
    /// </summary>
    /// <param name="message">The change and the reason it conflicts with the existing signatures.</param>
    public PdfSignatureConflictException(string message)
        : base(message)
    {
    }
}
