namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// How a workflow saves to a file path (spec F17).
/// </summary>
public sealed class PdfFileSaveOptions
{
    /// <summary>
    /// Gets a value indicating whether a destination detected as existing may be replaced. Without it, a destination detected as existing before opening the input is refused then.
    /// </summary>
    /// <remarks>
    /// File saves are not coordinated across independent operations or processes targeting the same output pathname.
    /// Callers requiring exclusive publication must serialize those operations. A false value does not guarantee
    /// atomic create-if-absent behavior against concurrent filesystem changes.
    /// </remarks>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Gets a value indicating whether the output is flushed to disk before it is published. The default is true.
    /// </summary>
    /// <remarks>
    /// The destination directory is not flushed, so a power loss can still lose the published name. No power-loss durability is claimed either way.
    /// </remarks>
    public bool FlushToDisk { get; init; } = true;
}
