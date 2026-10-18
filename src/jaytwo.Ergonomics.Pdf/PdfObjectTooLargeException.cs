namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// Raised when a decoded structural object exceeds the in-memory cap.
/// </summary>
public sealed class PdfObjectTooLargeException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfObjectTooLargeException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PdfObjectTooLargeException(string message)
        : base(message)
    {
    }
}
