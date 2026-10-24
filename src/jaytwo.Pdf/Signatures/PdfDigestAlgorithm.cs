namespace jaytwo.Pdf.Signatures;

/// <summary>
/// The digest a CMS signature uses for the signed byte ranges, its signed attributes, and the signature value.
/// </summary>
public enum PdfDigestAlgorithm
{
    /// <summary>
    /// SHA-256, the default.
    /// </summary>
    Sha256,

    /// <summary>
    /// SHA-384.
    /// </summary>
    Sha384,

    /// <summary>
    /// SHA-512.
    /// </summary>
    Sha512,
}
