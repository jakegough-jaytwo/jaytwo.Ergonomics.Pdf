namespace jaytwo.Pdf;

/// <summary>
/// Raised when assembling a new PDF would exceed a bounded structural resource.
/// </summary>
public sealed class PdfResourceLimitException : PdfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfResourceLimitException"/> class.
    /// </summary>
    /// <param name="resource">The bounded resource that was exceeded.</param>
    public PdfResourceLimitException(string resource)
        : base("The PDF assembly " + resource + " limit was exceeded.")
    {
        Resource = resource;
    }

    /// <summary>
    /// Gets the bounded resource that was exceeded.
    /// </summary>
    public string Resource { get; }
}
