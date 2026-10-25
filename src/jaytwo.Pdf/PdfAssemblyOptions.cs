namespace jaytwo.Pdf;

/// <summary>Options for writing an unencrypted assembly of selected pages.</summary>
public sealed class PdfAssemblyOptions
{
    /// <summary>Gets or sets whether encrypted inputs may produce unencrypted output. The default is false.</summary>
    /// <remarks>Owner authentication bypasses permissions. User authentication requires copy and assembly permission on every input.</remarks>
    public bool AllowUnencryptedOutput { get; set; }

    /// <summary>Gets or sets whether supported external links survive. The default preserves them.</summary>
    public PdfExternalLinkMode ExternalLinks { get; set; } = PdfExternalLinkMode.PreserveSupported;
}
