using System.Collections.Generic;

namespace jaytwo.Pdf.Internal;

internal sealed class AppendixLayout
{
    public List<WrittenObject> Objects { get; } = new List<WrittenObject>();

    public long XrefOffset { get; set; }

    // The trailer's /Size, which counts a cross-reference stream's own object number.
    public int Size { get; set; }

    public PdfObject.ByteRangeSlot? ByteRange { get; set; }

    public PdfObject.ContentsSlot? Contents { get; set; }
}
