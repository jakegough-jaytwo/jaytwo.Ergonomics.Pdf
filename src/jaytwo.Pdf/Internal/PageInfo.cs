namespace jaytwo.Pdf.Internal;

internal readonly struct PageInfo
{
    public PageInfo(int objectId, PdfRect mediaBox, PdfRect cropBox, int rotate)
    {
        ObjectId = objectId;
        MediaBox = mediaBox;
        CropBox = cropBox;
        Rotate = rotate;
        Number = 0;
    }

    public int ObjectId { get; }

    public PdfRect MediaBox { get; }

    public PdfRect CropBox { get; }

    public int Rotate { get; }

    public int Number { get; }
}
