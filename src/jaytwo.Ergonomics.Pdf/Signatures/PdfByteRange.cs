namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// The two byte ranges covered by a PDF signature. The gap between them is the /Contents value.
/// </summary>
public readonly record struct PdfByteRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfByteRange"/> struct.
    /// </summary>
    /// <param name="offset0">Start of the first range.</param>
    /// <param name="length0">Length of the first range.</param>
    /// <param name="offset1">Start of the second range.</param>
    /// <param name="length1">Length of the second range.</param>
    public PdfByteRange(long offset0, long length0, long offset1, long length1)
    {
        Offset0 = offset0;
        Length0 = length0;
        Offset1 = offset1;
        Length1 = length1;
    }

    /// <summary>
    /// Gets the start of the first hashed range.
    /// </summary>
    public long Offset0 { get; }

    /// <summary>
    /// Gets the length of the first hashed range.
    /// </summary>
    public long Length0 { get; }

    /// <summary>
    /// Gets the start of the second hashed range.
    /// </summary>
    public long Offset1 { get; }

    /// <summary>
    /// Gets the length of the second hashed range.
    /// </summary>
    public long Length1 { get; }
}
