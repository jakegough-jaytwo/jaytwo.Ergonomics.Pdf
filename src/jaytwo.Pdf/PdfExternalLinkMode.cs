namespace jaytwo.Pdf;

/// <summary>Controls external links in a new assembled file.</summary>
public enum PdfExternalLinkMode
{
    /// <summary>Preserve supported absolute HTTP, HTTPS, and mailto links.</summary>
    PreserveSupported,

    /// <summary>Omit every external link.</summary>
    Drop,
}
