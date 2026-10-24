namespace jaytwo.Pdf.Signatures;

/// <summary>
/// The structural reading of a signature's <c>/ByteRange</c> against the file (ISO 32000-1 12.8.1). It says only how the
/// two ranges and the gap between them sit in the file. It does not hash the ranges or verify the signature, and a
/// well-formed byte range can still leave later bytes uncovered (see <see cref="PdfSignatureInfo.HasLaterRevisions"/>).
/// The checks run in the order of the values below, and the first that fails is reported.
/// </summary>
public enum PdfByteRangeStatus
{
    /// <summary>
    /// The field has no signature dictionary, or the signature dictionary has no <c>/ByteRange</c>.
    /// </summary>
    Missing,

    /// <summary>
    /// The first range does not start at offset 0.
    /// </summary>
    NotFromFileStart,

    /// <summary>
    /// The first range, or the second range, ends past the end of the file.
    /// </summary>
    PastEndOfFile,

    /// <summary>
    /// The second range starts before the first range ends, or leaves no room between them for the <c>&lt;&gt;</c> of a hex string.
    /// </summary>
    Overlapping,

    /// <summary>
    /// The gap between the ranges does not start with <c>&lt;</c> and end with <c>&gt;</c>, so it is not a hex string such as <c>/Contents</c>.
    /// </summary>
    GapNotHexString,

    /// <summary>
    /// The first range starts at offset 0, both ranges are inside the file in order, and the gap between them is delimited as a hex string.
    /// </summary>
    WellFormed,
}
