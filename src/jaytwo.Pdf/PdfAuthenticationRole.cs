namespace jaytwo.Pdf;

/// <summary>How the opened document authenticated, independently of its declared user permissions.</summary>
public enum PdfAuthenticationRole
{
    /// <summary>The document is unencrypted; no password authentication was needed.</summary>
    Unencrypted,

    /// <summary>The user password authenticated, including an empty user password.</summary>
    User,

    /// <summary>The owner password authenticated and bypasses user permission restrictions.</summary>
    Owner,
}
