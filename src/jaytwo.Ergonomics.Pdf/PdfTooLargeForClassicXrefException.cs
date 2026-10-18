namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// Raised when an incremental save would need a classic xref offset wider than 10 digits.
/// </summary>
public sealed class PdfTooLargeForClassicXrefException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTooLargeForClassicXrefException"/> class.
    /// </summary>
    public PdfTooLargeForClassicXrefException()
        : base("A classic xref offset cannot exceed 9,999,999,999. The file was not modified.")
    {
    }
}
