namespace jaytwo.Ergonomics.Pdf.AcroForm;

/// <summary>
/// Raised when a form write targets a form the library cannot write, such as an XFA-only form.
/// </summary>
public sealed class PdfUnsupportedFormException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfUnsupportedFormException"/> class.
    /// </summary>
    /// <param name="message">The reason the form cannot be written.</param>
    public PdfUnsupportedFormException(string message)
        : base(message)
    {
    }
}
