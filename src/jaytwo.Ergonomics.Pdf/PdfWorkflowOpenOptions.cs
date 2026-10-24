namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// How a workflow opens its input.
/// </summary>
public sealed class PdfWorkflowOpenOptions
{
    /// <summary>
    /// Gets the user or owner password of a standard-security PDF. Null tries the empty user password. An unencrypted PDF ignores it.
    /// </summary>
    public string? Password { get; init; }
}
