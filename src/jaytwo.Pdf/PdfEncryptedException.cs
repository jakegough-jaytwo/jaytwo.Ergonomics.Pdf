namespace jaytwo.Pdf;

/// <summary>
/// Raised when a PDF is encrypted and cannot be opened with the supplied password.
/// </summary>
public sealed class PdfEncryptedException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfEncryptedException"/> class.
    /// </summary>
    public PdfEncryptedException()
        : this("The PDF is encrypted.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfEncryptedException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PdfEncryptedException(string message)
        : base(message)
    {
    }
}
