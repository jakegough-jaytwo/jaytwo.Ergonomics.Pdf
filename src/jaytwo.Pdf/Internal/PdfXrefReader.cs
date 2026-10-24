#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;

namespace jaytwo.Pdf.Internal;

// Reads classic cross-reference sections and cross-reference stream entries straight into the merged map.
internal static class PdfXrefReader
{
    // Object numbers listed by one xref stream stay below this value, the PDF 1.7 Annex C indirect-object limit plus object 0.
    internal const int MaxStreamObjects = 8388608;

    // The merged cross-reference map holds at most one entry per this many file bytes, never fewer than the floor and
    // never more than the ceiling below. A real object needs more than 8 bytes of file, so the ratio binds only on entries
    // the file cannot back. The ceiling keeps the map and its growth under about 180 MiB, inside the 256 MiB budget.
    internal const int FileBytesPerEntry = 8;

    internal const int MinEntryBudget = 65536;

    internal const int MaxEntries = 2097152;

    // Reads a classic xref section straight into the merged map, with no per-section copy, the way an xref stream is read.
    // Each subsection header is range-checked before its rows are read, and every new object counts against the budget.
    public static PdfObject.Dict ParseClassic(PdfParser parser, Dictionary<int, XrefEntry> target, bool overwrite, int budget)
    {
        PdfLexer lexer = parser.Lexer;
        PdfToken keyword = lexer.Next();
        if (keyword.Kind != PdfTokenKind.Keyword || keyword.Text != "xref")
        {
            throw lexer.Error("xref keyword was expected.");
        }

        var row = new byte[18];
        while (true)
        {
            PdfToken token = lexer.Peek();
            if (token.Kind == PdfTokenKind.Keyword && token.Text == "trailer")
            {
                lexer.Next();
                break;
            }

            if (token.Kind != PdfTokenKind.Integer)
            {
                throw lexer.Error("Xref subsection or trailer was expected.");
            }

            long start = lexer.Next().Integer;
            PdfToken countToken = lexer.Next();
            if (countToken.Kind != PdfTokenKind.Integer)
            {
                throw lexer.Error("Xref subsection count was expected.");
            }

            long count = countToken.Integer;
            if (start < 0 || count < 0 || start + count > (long)int.MaxValue + 1)
            {
                throw lexer.Error("Xref subsection exceeds the object number range.");
            }

            lexer.SkipWhitespace();
            for (int i = 0; i < count; i++)
            {
                MergeEntry(target, (int)start + i, ReadClassicEntry(lexer, row), overwrite, budget);
            }
        }

        PdfObject trailer = parser.ParseValue();
        if (trailer is not PdfObject.Dict trailerDict)
        {
            throw lexer.Error("Trailer dictionary was expected.");
        }

        return trailerDict;
    }

    private static XrefEntry ReadClassicEntry(PdfLexer lexer, byte[] row)
    {
        PdfCursor cursor = lexer.Cursor;
        for (int i = 0; i < 18; i++)
        {
            int b = cursor.ReadByte();
            if (b < 0)
            {
                throw lexer.Error("Truncated xref entry.");
            }

            row[i] = (byte)b;
        }

        int eol = cursor.ReadByte();
        if (eol == ' ')
        {
            int second = cursor.ReadByte();
            if (second != '\n' && second != '\r')
            {
                throw lexer.Error("Xref entry must end in CRLF, LF, SP LF, or SP CR.");
            }
        }
        else if (eol == '\r')
        {
            int second = cursor.ReadByte();
            if (second != '\n')
            {
                throw lexer.Error("Xref entry must end in CRLF, LF, SP LF, or SP CR.");
            }
        }
        else if (eol != '\n')
        {
            throw lexer.Error("Xref entry must end in CRLF, LF, SP LF, or SP CR.");
        }

        if (row[17] != 'n' && row[17] != 'f')
        {
            throw lexer.Error("Xref entry flag was expected.");
        }

        if (!TryParseDigits(row, 0, 10, out long offset))
        {
            throw lexer.Error("Xref offset is not numeric.");
        }

        if (!TryParseDigits(row, 11, 5, out long generation))
        {
            throw lexer.Error("Xref generation is not numeric.");
        }

        byte type = row[17] == 'n' ? (byte)1 : (byte)0;
        return new XrefEntry(type, offset, (int)generation);
    }

    // Digits only, with no sign or space, as a fixed-width xref field is written. Ten digits always fit in a long.
    private static bool TryParseDigits(byte[] row, int start, int length, out long value)
    {
        value = 0;
        for (int i = start; i < start + length; i++)
        {
            int digit = row[i] - '0';
            if (digit < 0 || digit > 9)
            {
                return false;
            }

            value = (value * 10) + digit;
        }

        return true;
    }

    internal static int EntryBudget(long fileLength)
    {
        return (int)Math.Min(MaxEntries, Math.Max(MinEntryBudget, fileLength / FileBytesPerEntry));
    }

    // Adds one entry to the merged map. An overwriting section replaces what is there; otherwise the newer entry stays.
    // A new object number past the budget throws, so the map never grows beyond what the file can back.
    internal static void MergeEntry(Dictionary<int, XrefEntry> target, int id, XrefEntry entry, bool overwrite, int budget)
    {
        if (target.ContainsKey(id))
        {
            if (overwrite)
            {
                target[id] = entry;
            }

            return;
        }

        if (target.Count >= budget)
        {
            throw BudgetExceeded(budget);
        }

        target.Add(id, entry);
    }

    // Reads the entries of one xref stream straight into the merged map, with no per-section copy.
    internal static void ParseStreamEntries(PdfObject.Dict dict, byte[] decoded, Dictionary<int, XrefEntry> target, bool overwrite, int budget)
    {
        int[] widths = ReadWidths(dict);
        var index = ReadIndex(dict);
        if ((index.Count & 1) != 0)
        {
            throw new PdfParseException(0, "Xref stream /Index must contain pairs of integers.");
        }

        int entryWidth = widths[0] + widths[1] + widths[2];
        long available = entryWidth == 0 ? 0 : decoded.Length / entryWidth;
        long? size = dict.Get("Size") is PdfObject.Num sizeNumber ? IndexValue(sizeNumber, "/Size") : null;
        long listed = 0;
        long previousEnd = 0;
        for (int pair = 0; pair < index.Count; pair += 2)
        {
            long end = (long)index[pair] + index[pair + 1];
            if (end > (long)int.MaxValue + 1)
            {
                throw new PdfParseException(0, "Xref stream /Index subsection exceeds the object number range.");
            }

            if (index[pair + 1] > 0 && index[pair] < previousEnd)
            {
                throw new PdfParseException(0, "Xref stream /Index subsections overlap or are not in ascending order.");
            }

            listed += index[pair + 1];
            if (index[pair + 1] > 0 && listed > available)
            {
                throw new PdfParseException(0, "Xref stream /Index count exceeds the decoded entries.");
            }

            if (index[pair + 1] > 0 && size != null && end > size.Value)
            {
                throw new PdfParseException(0, "Xref stream /Index subsection exceeds /Size.");
            }

            if (index[pair + 1] > 0 && end > MaxStreamObjects)
            {
                throw new PdfObjectTooLargeException("Xref stream lists object numbers at or above " + MaxStreamObjects.ToString(CultureInfo.InvariantCulture) + ".");
            }

            if (listed > budget)
            {
                throw BudgetExceeded(budget);
            }

            if (index[pair + 1] > 0)
            {
                previousEnd = end;
            }
        }

        int cursor = 0;
        for (int pair = 0; pair < index.Count; pair += 2)
        {
            int start = index[pair];
            int count = index[pair + 1];
            for (int i = 0; i < count; i++)
            {
                long typeRaw = ReadField(decoded, ref cursor, widths[0]);
                int type = widths[0] == 0 ? 1 : (int)typeRaw;
                long field1 = ReadField(decoded, ref cursor, widths[1]);
                int field2 = (int)ReadField(decoded, ref cursor, widths[2]);
                MergeEntry(target, start + i, new XrefEntry((byte)type, field1, field2), overwrite, budget);
            }
        }
    }

    private static PdfObjectTooLargeException BudgetExceeded(int budget)
    {
        return new PdfObjectTooLargeException("The cross-reference sections list more than " + budget.ToString(CultureInfo.InvariantCulture) + " distinct objects, the limit for a file of this length.");
    }

    internal static void EnsureColumns(PdfObject.Dict dict, List<PdfObject.Dict?> parms)
    {
        int[] widths = ReadWidths(dict);
        int width = widths[0] + widths[1] + widths[2];
        if (width <= 0)
        {
            return;
        }

        for (int i = 0; i < parms.Count; i++)
        {
            PdfObject.Dict? parm = parms[i];
            if (parm == null)
            {
                continue;
            }

            if (parm.Get("Predictor") is PdfObject.Num predictor && predictor.DoubleValue >= 10 && parm.Get("Columns") == null)
            {
                parm.Set("Columns", PdfObject.Num.FromLong(width));
            }
        }
    }

    private static int[] ReadWidths(PdfObject.Dict dict)
    {
        if (dict.Get("W") is not PdfObject.Arr array || array.Items.Count != 3)
        {
            throw new PdfParseException(0, "Xref stream /W must contain three integers.");
        }

        var widths = new int[3];
        for (int i = 0; i < 3; i++)
        {
            if (array.Items[i] is not PdfObject.Num number)
            {
                throw new PdfParseException(0, "Xref stream /W entry is not a number.");
            }

            long width = number.IsInteger ? number.LongValue : (long)number.DoubleValue;
            if (width < 0 || width > sizeof(long))
            {
                throw new PdfParseException(0, "Xref stream /W entry is outside the supported range.");
            }

            widths[i] = (int)width;
        }

        return widths;
    }

    private static List<int> ReadIndex(PdfObject.Dict dict)
    {
        var index = new List<int>();
        if (dict.Get("Index") is PdfObject.Arr array)
        {
            foreach (PdfObject item in array.Items)
            {
                if (item is not PdfObject.Num number)
                {
                    throw new PdfParseException(0, "Xref stream /Index entry is not a number.");
                }

                index.Add(IndexValue(number, "/Index"));
            }

            return index;
        }

        int size = 0;
        if (dict.Get("Size") is PdfObject.Num sizeNumber)
        {
            size = IndexValue(sizeNumber, "/Size");
        }

        index.Add(0);
        index.Add(size);
        return index;
    }

    private static int IndexValue(PdfObject.Num number, string key)
    {
        if (!number.IsInteger || number.LongValue < 0 || number.LongValue > int.MaxValue)
        {
            throw new PdfParseException(0, "Xref stream " + key + " entry is not a non-negative 32-bit integer.");
        }

        return (int)number.LongValue;
    }

    private static long ReadField(byte[] data, ref int cursor, int width)
    {
        if (width == 0)
        {
            return 0;
        }

        if (width < 0 || cursor < 0 || cursor > data.Length || width > data.Length - cursor)
        {
            throw new PdfParseException(0, "Xref stream entry is truncated.");
        }

        long value = 0;
        for (int i = 0; i < width; i++)
        {
            value = (value << 8) | data[cursor + i];
        }

        cursor += width;
        return value;
    }
}
