using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using jaytwo.Ergonomics.Pdf.Internal;

namespace jaytwo.Ergonomics.Pdf.AcroForm;

internal static class AppearanceBuilder
{
    // Adobe Helvetica AFM 002.000 (1997), in 1000ths of an em. Indexed by the WinAnsi byte
    // PdfTextCodec.EncodeWinAnsi writes. Glyph names are WinAnsiEncoding, so 0x27 is quotesingle,
    // 0x60 is grave, and 0x80-0x9F are Windows-1252 punctuation rather than Latin-1 controls.
    // Bytes the AFM does not list, including .notdef and tab, use the space width 278.
    private static readonly ushort[] HelveticaWidths =
    {
        278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278,
        278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278, 278,
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584, 278,
        556, 278, 222, 556, 333, 1000, 556, 556, 333, 1000, 667, 333, 1000, 278, 611, 278,
        278, 222, 222, 333, 333, 350, 556, 1000, 333, 1000, 500, 333, 944, 278, 500, 667,
        278, 333, 556, 556, 556, 556, 260, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 556, 537, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        667, 667, 667, 667, 667, 667, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 500, 556, 556, 556, 556, 278, 278, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 584, 611, 556, 556, 556, 556, 500, 556, 500,
    };

    public static byte[] Build(
        string text,
        double width,
        double height,
        double fontSize,
        int align,
        double red,
        double green,
        double blue,
        bool multiline,
        bool fitWidth,
        double[]? background,
        double[]? border,
        double borderWidth)
    {
        if (fontSize <= 0)
        {
            fontSize = Clamp(height - 4, 6, 14);
            if (fitWidth)
            {
                string singleLine = text.Replace("\r", string.Empty).Replace("\n", string.Empty);
                double measured = Measure(singleLine, 1);
                if (measured > 0)
                {
                    fontSize = Math.Max(6, Math.Min(fontSize, Math.Max(0, width - 4) / measured));
                }
            }
        }

        List<string> lines = Wrap(text, width, fontSize, multiline);
        double leading = fontSize * 1.2;
        var builder = new StringBuilder();
        builder.Append("q\n");
        AppendBox(builder, width, height, background, border, borderWidth);
        builder.Append("BT\n/Helv ");
        builder.Append(Format(fontSize));
        builder.Append(" Tf\n");
        builder.Append(Format(red));
        builder.Append(' ');
        builder.Append(Format(green));
        builder.Append(' ');
        builder.Append(Format(blue));
        builder.Append(" rg\n");
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            double textWidth = Measure(line, fontSize);
            double x = align switch
            {
                1 => Math.Max(2, (width - textWidth) / 2),
                2 => Math.Max(2, width - textWidth - 2),
                _ => 2,
            };
            double y = height - 2 - fontSize - (i * leading);
            builder.Append("1 0 0 1 ");
            builder.Append(Format(x));
            builder.Append(' ');
            builder.Append(Format(y));
            builder.Append(" Tm\n(");
            builder.Append(PdfTextCodec.EscapeLiteral(PdfTextCodec.EncodeWinAnsi(line)));
            builder.Append(") Tj\n");
        }

        builder.Append("ET\nQ\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    public static (double Size, double Red, double Green, double Blue) ParseDefaultAppearance(string? appearance)
    {
        double size = 0;
        double red = 0;
        double green = 0;
        double blue = 0;
        if (appearance is not { Length: > 0 } text)
        {
            return (size, red, green, blue);
        }

        string[] tokens = text.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i] == "Tf" && i >= 1 && double.TryParse(tokens[i - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                size = parsed;
            }

            if (tokens[i] == "rg" && i >= 3)
            {
                red = Parse(tokens[i - 3]);
                green = Parse(tokens[i - 2]);
                blue = Parse(tokens[i - 1]);
            }
            else if (tokens[i] == "g" && i >= 1)
            {
                red = green = blue = Parse(tokens[i - 1]);
            }
        }

        return (size, red, green, blue);
    }

    public static byte[] CheckBox(bool selected, double width, double height)
    {
        return Mark(selected, width, height, false);
    }

    public static byte[] Radio(bool selected, double width, double height)
    {
        return Mark(selected, width, height, true);
    }

    private static void AppendBox(StringBuilder builder, double width, double height, double[]? background, double[]? border, double borderWidth)
    {
        if (background != null)
        {
            AppendColor(builder, background, stroking: false);
            builder.Append("0 0 ").Append(Format(width)).Append(' ').Append(Format(height)).Append(" re f\n");
        }

        if (border == null || borderWidth <= 0)
        {
            return;
        }

        double effectiveWidth = Math.Min(borderWidth, Math.Min(width, height));
        double inset = effectiveWidth / 2;
        AppendColor(builder, border, stroking: true);
        builder.Append(Format(effectiveWidth)).Append(" w\n");
        builder.Append(Format(inset)).Append(' ').Append(Format(inset)).Append(' ');
        builder.Append(Format(Math.Max(0, width - effectiveWidth))).Append(' ');
        builder.Append(Format(Math.Max(0, height - effectiveWidth))).Append(" re S\n");
    }

    private static void AppendColor(StringBuilder builder, double[] color, bool stroking)
    {
        for (int i = 0; i < color.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(Format(color[i]));
        }

        builder.Append(stroking
            ? color.Length == 1 ? " G\n" : color.Length == 3 ? " RG\n" : " K\n"
            : color.Length == 1 ? " g\n" : color.Length == 3 ? " rg\n" : " k\n");
    }

    private static List<string> Wrap(string text, double width, double fontSize, bool multiline)
    {
        var lines = new List<string>();
        if (!multiline)
        {
            lines.Add(text.Replace("\r", string.Empty).Replace("\n", string.Empty));
            return lines;
        }

        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        double limit = width - 4;
        string[] paragraphs = normalized.Split('\n');
        foreach (string paragraph in paragraphs)
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            // Newlines are already split out, so EncodeWinAnsi writes one byte per character.
            byte[] encoded = PdfTextCodec.EncodeWinAnsi(paragraph);
            int start = 0;
            double used = 0;
            for (int index = 0; index < encoded.Length; index++)
            {
                double advance = HelveticaWidths[encoded[index]] / 1000d * fontSize;
                if (index > start && used + advance > limit)
                {
                    lines.Add(paragraph.Substring(start, index - start));
                    start = index;
                    used = 0;
                }

                used += advance;
            }

            lines.Add(paragraph.Substring(start));
        }

        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }

        return lines;
    }

    private static double Measure(string text, double fontSize)
    {
        byte[] encoded = PdfTextCodec.EncodeWinAnsi(text);
        int units = 0;
        for (int i = 0; i < encoded.Length; i++)
        {
            units += HelveticaWidths[encoded[i]];
        }

        return units / 1000d * fontSize;
    }

    private static double Parse(string token)
    {
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return value;
        }

        return 0;
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static byte[] Mark(bool selected, double width, double height, bool radio)
    {
        var builder = new StringBuilder();
        builder.Append("q\n1 w\n0 0 0 RG\n");
        if (radio)
        {
            double cx = width / 2;
            double cy = height / 2;
            double radius = Math.Max(1, (Math.Min(width, height) / 2) - 1);
            AppendCircle(builder, cx, cy, radius, "S");
            if (selected)
            {
                builder.Append("0 0 0 rg\n");
                AppendCircle(builder, cx, cy, Math.Max(0.5, radius * 0.45), "f");
            }
        }
        else
        {
            builder.Append("1 1 ").Append(Format(Math.Max(0, width - 2))).Append(' ').Append(Format(Math.Max(0, height - 2))).Append(" re S\n");
            if (selected)
            {
                builder.Append("2 w\n");
                builder.Append(Format(width * 0.2)).Append(' ').Append(Format(height * 0.5)).Append(" m\n");
                builder.Append(Format(width * 0.4)).Append(' ').Append(Format(height * 0.2)).Append(" l\n");
                builder.Append(Format(width * 0.8)).Append(' ').Append(Format(height * 0.8)).Append(" l\nS\n");
            }
        }

        builder.Append("Q\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static void AppendCircle(StringBuilder builder, double cx, double cy, double radius, string operation)
    {
        double control = 0.5522847498d * radius;
        builder.Append(Format(cx + radius)).Append(' ').Append(Format(cy)).Append(" m\n");
        AppendCurve(builder, cx + radius, cy + control, cx + control, cy + radius, cx, cy + radius);
        AppendCurve(builder, cx - control, cy + radius, cx - radius, cy + control, cx - radius, cy);
        AppendCurve(builder, cx - radius, cy - control, cx - control, cy - radius, cx, cy - radius);
        AppendCurve(builder, cx + control, cy - radius, cx + radius, cy - control, cx + radius, cy);
        builder.Append(operation).Append('\n');
    }

    private static void AppendCurve(StringBuilder builder, double x1, double y1, double x2, double y2, double x3, double y3)
    {
        builder.Append(Format(x1)).Append(' ').Append(Format(y1)).Append(' ');
        builder.Append(Format(x2)).Append(' ').Append(Format(y2)).Append(' ');
        builder.Append(Format(x3)).Append(' ').Append(Format(y3)).Append(" c\n");
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
