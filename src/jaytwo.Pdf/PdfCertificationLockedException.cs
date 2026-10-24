namespace jaytwo.Pdf;

/// <summary>
/// Raised when a certification signature or field lock forbids the requested change.
/// </summary>
public sealed class PdfCertificationLockedException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCertificationLockedException"/> class.
    /// </summary>
    public PdfCertificationLockedException()
        : base("A certification signature or field lock forbids this change.")
    {
    }
}
