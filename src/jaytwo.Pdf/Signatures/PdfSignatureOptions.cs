namespace jaytwo.Pdf.Signatures;

/// <summary>
/// Options for an incremental PDF signature.
/// </summary>
public sealed class PdfSignatureOptions
{
    /// <summary>
    /// The largest <see cref="ReservedContentsBytes"/> accepted.
    /// </summary>
    internal const int MaxReservedContentsBytes = 1024 * 1024;

    /// <summary>
    /// Gets the signature field name. When null, the next SignatureN name is used.
    /// An existing empty signature field with this name is reused, and its rectangle is kept.
    /// </summary>
    public string? FieldName { get; init; }

    /// <summary>
    /// Gets the 1-based page that receives a new signature widget. Ignored when an existing field is reused.
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Gets the widget rectangle for a new signature field. The default is an invisible zero-area box.
    /// An existing signature field keeps its rectangle.
    /// </summary>
    public PdfRect Rect { get; init; }

    /// <summary>
    /// Gets the signature dictionary kind.
    /// </summary>
    public PdfSignatureKind Kind { get; init; } = PdfSignatureKind.CmsDetached;

    /// <summary>
    /// Gets the format of a CMS signature. The default is <see cref="PdfSignatureFormat.AdbePkcs7Detached"/>.
    /// A document timestamp has no format; any other value with <see cref="PdfSignatureKind.DocTimeStamp"/> throws <see cref="PdfSignException"/>.
    /// </summary>
    public PdfSignatureFormat Format { get; init; } = PdfSignatureFormat.AdbePkcs7Detached;

    /// <summary>
    /// Gets the digest of a CMS signature. The default is SHA-256. A document timestamp uses SHA-256 only;
    /// any other value with <see cref="PdfSignatureKind.DocTimeStamp"/> throws <see cref="PdfSignException"/>.
    /// </summary>
    public PdfDigestAlgorithm DigestAlgorithm { get; init; } = PdfDigestAlgorithm.Sha256;

    /// <summary>
    /// Gets the number of bytes reserved for the hexadecimal /Contents value, from 1 to 1,048,576.
    /// </summary>
    public int ReservedContentsBytes { get; init; } = 16384;

    /// <summary>
    /// Gets the signature reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the signature location.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Gets the signer contact information.
    /// </summary>
    public string? ContactInfo { get; init; }

    /// <summary>
    /// Gets the signer name stored in the signature dictionary.
    /// </summary>
    public string? SignerName { get; init; }

    /// <summary>
    /// Gets the signing time stored in /M and, for <see cref="PdfSignatureFormat.AdbePkcs7Detached"/>, in the CMS signing-time attribute.
    /// When null, UTC now is used for CMS signatures.
    /// </summary>
    public System.DateTimeOffset? SigningTime { get; init; }
}
