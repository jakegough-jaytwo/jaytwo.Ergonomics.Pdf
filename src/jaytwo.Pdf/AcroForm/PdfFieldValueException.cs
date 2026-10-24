namespace jaytwo.Pdf.AcroForm;

/// <summary>
/// Raised when a field value is rejected before the document is changed.
/// </summary>
public sealed class PdfFieldValueException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFieldValueException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PdfFieldValueException(string message)
        : base(message)
    {
    }
}
