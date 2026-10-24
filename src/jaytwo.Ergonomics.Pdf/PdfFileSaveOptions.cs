namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// How a workflow saves to a file path (spec F17).
/// </summary>
public sealed class PdfFileSaveOptions
{
    /// <summary>
    /// Gets a value indicating whether an existing destination file is replaced. Without it, an existing destination is refused before the input opens.
    /// </summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Gets a value indicating whether the output is flushed to disk before it is published. The default is true.
    /// </summary>
    /// <remarks>
    /// The destination directory is not flushed, so a power loss can still lose the published name. No power-loss durability is claimed either way.
    /// </remarks>
    public bool FlushToDisk { get; init; } = true;
}
