namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// The kind of incremental signature to append.
/// </summary>
public enum PdfSignatureKind
{
    /// <summary>
    /// A detached CMS signature, /adbe.pkcs7.detached.
    /// </summary>
    CmsDetached,

    /// <summary>
    /// A document timestamp, /ETSI.RFC3161.
    /// </summary>
    DocTimeStamp,
}
