using System;

namespace jaytwo.Pdf.Internal;

// Reads and builds the direct numbers, names, and rectangles that the engine's operations share.
internal static class PdfValues
{
    internal static PdfObject.Arr RectArray(PdfRect rect)
    {
        var array = new PdfObject.Arr();
        array.Items.Add(Number(rect.Llx));
        array.Items.Add(Number(rect.Lly));
        array.Items.Add(Number(rect.Urx));
        array.Items.Add(Number(rect.Ury));
        return array;
    }

    internal static PdfObject.Num Number(double value)
    {
        if (Math.Abs(value - Math.Truncate(value)) < 0.000000001 && value >= long.MinValue && value <= long.MaxValue)
        {
            return PdfObject.Num.FromLong((long)value);
        }

        return PdfObject.Num.FromDouble(value);
    }

    internal static string? NameOf(PdfObject? value)
    {
        return value is PdfObject.Name name ? name.Value : null;
    }

    internal static int IntOf(PdfObject? value)
    {
        if (value is PdfObject.Num number)
        {
            return number.IsInteger ? (int)number.LongValue : (int)number.DoubleValue;
        }

        return 0;
    }

    internal static long? LongOrNull(PdfObject? value)
    {
        if (value is not PdfObject.Num number)
        {
            return null;
        }

        return number.IsInteger ? number.LongValue : (long)number.DoubleValue;
    }
}
