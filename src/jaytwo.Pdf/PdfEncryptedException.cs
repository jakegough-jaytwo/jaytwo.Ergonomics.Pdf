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
        : this(PdfEncryptionFailureReason.Unknown, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfEncryptedException"/> class with a typed reason.
    /// </summary>
    /// <param name="reason">Why encrypted open failed.</param>
    /// <param name="message">The error message.</param>
    public PdfEncryptedException(PdfEncryptionFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>Gets why encrypted open failed, without parsing the message.</summary>
    public PdfEncryptionFailureReason Reason { get; }
}
