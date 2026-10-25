namespace jaytwo.Pdf;

/// <summary>
/// Raised before output when a source structure cannot safely be assembled into a new PDF.
/// </summary>
public sealed class PdfAssemblyNotSupportedException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAssemblyNotSupportedException"/> class.
    /// </summary>
    /// <param name="message">The unsupported structure.</param>
    public PdfAssemblyNotSupportedException(string message)
        : base(message)
    {
    }
}
