namespace jaytwo.Pdf;

/// <summary>
/// One logical PDF revision. A linearized file's main and first-page cross-reference sections are one revision.
/// </summary>
public sealed class PdfRevision
{
    internal PdfRevision(int number, long endOffset, bool endsAtSignatureByteRange)
    {
        Number = number;
        EndOffset = endOffset;
        EndsAtSignatureByteRange = endsAtSignatureByteRange;
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
    /// Gets a value indicating whether a signature field's <c>/ByteRange</c> ends at exactly this revision's end offset.
    /// It is an offset match only: the byte range's structure, the signature, and the covered bytes are not checked.
    /// </summary>
    public bool EndsAtSignatureByteRange { get; }
}
