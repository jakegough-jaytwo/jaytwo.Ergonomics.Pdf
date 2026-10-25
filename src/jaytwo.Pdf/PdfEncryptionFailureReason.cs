namespace jaytwo.Pdf;

/// <summary>The reason an encrypted PDF could not be opened.</summary>
public enum PdfEncryptionFailureReason
{
    /// <summary>No reason was supplied by the exception's caller.</summary>
    Unknown,

    /// <summary>No password was supplied and the empty password did not authenticate.</summary>
    PasswordRequired,

    /// <summary>The supplied password did not authenticate or could not be encoded.</summary>
    PasswordRejected,

    /// <summary>The encryption data is missing, malformed, or inconsistent after authentication.</summary>
    MalformedEncryption,

    /// <summary>The security handler, revision, or encryption configuration is not supported.</summary>
    UnsupportedEncryption,
}
