namespace jaytwo.Pdf;

/// <summary>
/// Raised when a stream uses a filter this revision cannot decode.
/// </summary>
public sealed class PdfUnsupportedFilterException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfUnsupportedFilterException"/> class.
    /// </summary>
    /// <param name="filter">The PDF filter name, without the leading slash.</param>
    public PdfUnsupportedFilterException(string filter)
        : base($"The stream filter /{filter} is not supported.")
    {
        Filter = filter;
    }

    /// <summary>
    /// Gets the PDF filter name, without the leading slash.
    /// </summary>
    public string Filter { get; }
}
