namespace jaytwo.Ergonomics.Pdf.AcroForm;

/// <summary>
/// How a field fill affected the widget appearance.
/// </summary>
public enum PdfAppearanceDisposition
{
    /// <summary>
    /// A new appearance stream was written.
    /// </summary>
    Generated,

    /// <summary>
    /// An existing on or off appearance was selected.
    /// </summary>
    ExistingAppearanceSelected,

    /// <summary>
    /// The value was stored without an appearance and /NeedAppearances was set, so a viewer must draw it. Signing refuses
    /// while /NeedAppearances is set.
    /// </summary>
    NeedsViewer,
}
