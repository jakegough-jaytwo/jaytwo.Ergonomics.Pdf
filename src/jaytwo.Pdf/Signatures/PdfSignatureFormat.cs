namespace jaytwo.Pdf.Signatures;

/// <summary>
/// The format of a CMS signature: its /SubFilter and the signed attributes in its CMS.
/// </summary>
public enum PdfSignatureFormat
{
    /// <summary>
    /// /adbe.pkcs7.detached, the default. The CMS signs content-type, message-digest, and signing-time.
    /// </summary>
    AdbePkcs7Detached,

    /// <summary>
    /// PAdES baseline B-B, /ETSI.CAdES.detached (ETSI EN 319 142-1). The CMS signs content-type, message-digest,
    /// and signing-certificate-v2, and has no signing-time; the signing time is in /M. With a timestamp authority,
    /// the signature is PAdES baseline B-T.
    /// </summary>
    PadesBaselineB,
}
