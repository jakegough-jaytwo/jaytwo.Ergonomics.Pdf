namespace jaytwo.Ergonomics.Pdf;

/// <summary>
/// A rectangle in PDF user space. The origin is the bottom-left of the page.
/// </summary>
public readonly record struct PdfRect
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRect"/> struct.
    /// </summary>
    /// <param name="llx">Lower-left x.</param>
    /// <param name="lly">Lower-left y.</param>
    /// <param name="urx">Upper-right x.</param>
    /// <param name="ury">Upper-right y.</param>
    public PdfRect(double llx, double lly, double urx, double ury)
    {
        Llx = llx;
        Lly = lly;
        Urx = urx;
        Ury = ury;
    }

    /// <summary>
    /// Gets the lower-left x coordinate.
    /// </summary>
    public double Llx { get; }

    /// <summary>
    /// Gets the lower-left y coordinate.
    /// </summary>
    public double Lly { get; }

    /// <summary>
    /// Gets the upper-right x coordinate.
    /// </summary>
    public double Urx { get; }

    /// <summary>
    /// Gets the upper-right y coordinate.
    /// </summary>
    public double Ury { get; }

    /// <summary>
    /// Gets the width, <see cref="Urx"/> minus <see cref="Llx"/>.
    /// </summary>
    public double Width => Urx - Llx;

    /// <summary>
    /// Gets the height, <see cref="Ury"/> minus <see cref="Lly"/>.
    /// </summary>
    public double Height => Ury - Lly;
}
