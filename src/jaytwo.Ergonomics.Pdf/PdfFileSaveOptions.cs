namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// How a workflow saves to a file path (spec F17).
/// </summary>
public sealed class PdfFileSaveOptions
{
    private bool? _overwrite;

    /// <summary>
    /// Gets a value indicating whether a destination detected as existing may be replaced. When it is not set, the ordinary
    /// <c>Save(path)</c> terminals create or overwrite (WORKFLOW-FILE-04); false asks for fail-if-exists. The signing,
    /// timestamp and Combine terminals, which still publish a staged file, treat an unset value as false.
    /// </summary>
    /// <remarks>
    /// File saves are not coordinated across independent operations or processes targeting the same output pathname.
    /// Callers requiring exclusive publication must serialize those operations. A false value does not guarantee
    /// atomic create-if-absent behavior against concurrent filesystem changes.
    /// </remarks>
    public bool Overwrite
    {
        get => _overwrite ?? true;
        init => _overwrite = value;
    }

    /// <summary>
    /// Gets a value indicating whether the output is flushed to disk before it is published. The default is true.
    /// </summary>
    /// <remarks>
    /// The destination directory is not flushed, so a power loss can still lose the published name. No power-loss durability is claimed either way.
    /// </remarks>
    public bool FlushToDisk { get; init; } = true;

    // The value a terminal that keeps its own default uses when the caller set none.
    internal bool OverwriteOr(bool defaultValue)
    {
        return _overwrite ?? defaultValue;
    }
}
