namespace jaytwo.Ergonomics.Pdf.AcroForm;

/// <summary>
/// Raised when a fill targets a read-only AcroForm field.
/// </summary>
public sealed class PdfFieldReadOnlyException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFieldReadOnlyException"/> class.
    /// </summary>
    /// <param name="fullName">The read-only field full name.</param>
    public PdfFieldReadOnlyException(string fullName)
        : base($"AcroForm field '{fullName}' is read-only.")
    {
        FullName = fullName;
    }

    /// <summary>
    /// Gets the read-only field full name.
    /// </summary>
    public string FullName { get; }
}
