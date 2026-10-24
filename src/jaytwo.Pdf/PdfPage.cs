namespace jaytwo.Pdf;

/// <summary>
/// A page's boxes and rotation. Content streams are not read.
/// </summary>
public sealed class PdfPage
{
    internal PdfPage(int objectId, int number, PdfRect mediaBox, PdfRect cropBox, int rotation)
    {
        ObjectId = objectId;
        Number = number;
        MediaBox = mediaBox;
        CropBox = cropBox;
        Rotation = rotation;
    }

    /// <summary>
    /// Gets the 1-based page number.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Gets the media box. The origin is the bottom-left of the page.
    /// </summary>
    public PdfRect MediaBox { get; }

    /// <summary>
    /// Gets the crop box. When the page omits /CropBox, this is the inherited crop box, or the media box when no ancestor specifies one.
    /// </summary>
    public PdfRect CropBox { get; }

    /// <summary>
    /// Gets the page rotation in degrees: 0, 90, 180, or 270. <see cref="PdfPageEditor.Rotation"/> changes it.
    /// </summary>
    public int Rotation { get; }

    internal int ObjectId { get; }
}
