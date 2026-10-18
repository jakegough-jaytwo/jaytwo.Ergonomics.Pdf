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
    /// The value was stored and /NeedAppearances was set because no appearance was generated.
    /// </summary>
    NeedAppearances,
}
