namespace jaytwo.Pdf;

/// <summary>
/// One logical PDF revision. A linearized file's main and first-page cross-reference sections are one revision.
/// </summary>
public sealed class PdfRevision
{
    internal PdfRevision(int number, long endOffset, bool isCoveredBySignature)
    {
        Number = number;
        EndOffset = endOffset;
        IsCoveredBySignature = isCoveredBySignature;
    }

    /// <summary>
    /// Gets the 1-based revision number, oldest first.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Gets the exclusive file offset where this revision ends.
    /// </summary>
    public long EndOffset { get; }

    /// <summary>
    /// Gets a value indicating whether a signature field's <c>/ByteRange</c> ends at this revision's end offset.
    /// The signature and its byte range are not validated.
    /// </summary>
    public bool IsCoveredBySignature { get; }
}
