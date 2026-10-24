namespace jaytwo.Pdf.AcroForm;

/// <summary>
/// Raised when an AcroForm field full name is not in the document.
/// </summary>
public sealed class PdfFieldNotFoundException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFieldNotFoundException"/> class.
    /// </summary>
    /// <param name="fullName">The field full name that was requested.</param>
    public PdfFieldNotFoundException(string fullName)
        : base($"AcroForm field '{fullName}' was not found.")
    {
        FullName = fullName;
    }

    /// <summary>
    /// Gets the field full name that was requested.
    /// </summary>
    public string FullName { get; }
}
