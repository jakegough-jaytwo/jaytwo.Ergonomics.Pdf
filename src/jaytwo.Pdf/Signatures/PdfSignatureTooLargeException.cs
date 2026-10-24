namespace jaytwo.Pdf.Signatures;

/// <summary>
/// Raised when signature contents do not fit in the reserved /Contents hex gap.
/// </summary>
public sealed class PdfSignatureTooLargeException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSignatureTooLargeException"/> class.
    /// </summary>
    /// <param name="actualBytes">The contents length that was returned.</param>
    /// <param name="reservedBytes">The reserved contents length.</param>
    public PdfSignatureTooLargeException(int actualBytes, int reservedBytes)
        : base($"Signature contents are {actualBytes} bytes and the reserved gap is {reservedBytes} bytes. The file was not modified.")
    {
        ActualBytes = actualBytes;
        ReservedBytes = reservedBytes;
    }

    /// <summary>
    /// Gets the contents length that was returned.
    /// </summary>
    public int ActualBytes { get; }

    /// <summary>
    /// Gets the reserved contents length.
    /// </summary>
    public int ReservedBytes { get; }
}
