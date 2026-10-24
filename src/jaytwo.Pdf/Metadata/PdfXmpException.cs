namespace jaytwo.Pdf.Metadata;

/// <summary>
/// Raised when the document metadata stream is not well-formed XMP.
/// </summary>
public sealed class PdfXmpException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfXmpException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The XML parser failure.</param>
    public PdfXmpException(string message, System.Exception? innerException = null)
        : base(message, innerException!)
    {
    }
}
