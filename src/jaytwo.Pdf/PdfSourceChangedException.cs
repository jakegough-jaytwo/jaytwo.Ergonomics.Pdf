namespace jaytwo.Pdf;

/// <summary>
/// Raised when the bytes behind an open document change, so reading on would mix two versions of the file.
/// </summary>
public sealed class PdfSourceChangedException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSourceChangedException"/> class.
    /// </summary>
    public PdfSourceChangedException()
        : this("The PDF source changed while the document was using it.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSourceChangedException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PdfSourceChangedException(string message)
        : base(message)
    {
    }
}
