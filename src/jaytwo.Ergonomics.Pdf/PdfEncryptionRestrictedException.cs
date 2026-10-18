namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// Raised when the password that opened an encrypted PDF does not allow the requested change.
/// </summary>
public sealed class PdfEncryptionRestrictedException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfEncryptionRestrictedException"/> class.
    /// </summary>
    public PdfEncryptionRestrictedException()
        : base("The PDF password does not allow this change.")
    {
    }
}
