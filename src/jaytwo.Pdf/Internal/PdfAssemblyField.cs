using System.Collections.Generic;

namespace jaytwo.Pdf.Internal;

internal sealed class PdfAssemblyField
{
    internal PdfAssemblyField(PdfObject.Dict source)
    {
        Source = source;
    }

    internal PdfObject.Dict Source { get; }

    internal List<PdfAssemblyField> Children { get; } = new List<PdfAssemblyField>();

    internal List<PdfObject.Dict> Widgets { get; } = new List<PdfObject.Dict>();

    internal List<PdfObject.Ref> WidgetReferences { get; } = new List<PdfObject.Ref>();
}
