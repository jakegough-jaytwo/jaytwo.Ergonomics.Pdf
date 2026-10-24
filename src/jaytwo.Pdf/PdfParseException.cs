namespace jaytwo.Pdf;

/// <summary>
/// Raised when PDF syntax or cross-reference data cannot be read.
/// </summary>
public sealed class PdfParseException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfParseException"/> class.
    /// </summary>
    /// <param name="offset">The file offset where reading failed.</param>
    /// <param name="message">The error message.</param>
    public PdfParseException(long offset, string message)
        : base($"Parse error at offset {offset}: {message}")
    {
        Offset = offset;
    }

    /// <summary>
    /// Gets the file offset where reading failed.
    /// </summary>
    public long Offset { get; }
}
