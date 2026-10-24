using System;
using System.Collections.Generic;
using System.Globalization;

namespace jaytwo.Pdf.Internal;

// Parses the objects of a decoded object stream (ISO 32000-1 7.5.7) from its header of object numbers and offsets.
internal static class PdfObjectStreamReader
{
    // Parsing the objects of one object stream reads at most this multiple of its decoded length, plus the slack below.
    internal const int ReadFactor = 4;

    private const int ReadSlack = 64 * 1024;
    private const int PresizeLimit = 1024;

    internal static List<PdfObject> Parse(byte[] data, int count, int first)
    {
        // W-115: /N sizes the list, up to a bound so a header that overstates it cannot reserve more. Writers list object
        // numbers in ascending order, so the set that finds a repeat is made only when a number is not above the last.
        var objects = new List<PdfObject>(Math.Min(count, PresizeLimit));
        HashSet<int>? ids = null;
        int lastId = -1;
        var cursor = PdfCursor.FromBytes(data);
        var parser = new PdfParser(cursor, null, 0);
        long budget = ((long)data.Length * ReadFactor) + ReadSlack;
        long consumed = 0;
        int header = 0;
        for (int i = 0; i < count; i++)
        {
            int id = HeaderInteger(data, first, ref header);
            int offset = HeaderInteger(data, first, ref header);
            if (ids == null && id > lastId)
            {
                lastId = id;
            }
            else
            {
                // The earlier pairs are read again from the header, which they have already passed.
                if (ids == null)
                {
                    ids = new HashSet<int>();
                    int reread = 0;
                    for (int earlier = 0; earlier < i; earlier++)
                    {
                        ids.Add(HeaderInteger(data, first, ref reread));
                        HeaderInteger(data, first, ref reread);
                    }
                }

                if (!ids.Add(id))
                {
                    throw new PdfParseException(0, "Object stream header repeats object number " + id.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }

            if (offset > data.Length - first)
            {
                throw new PdfParseException(0, "Object stream header contains an invalid object number or offset.");
            }

            int start = first + offset;
            cursor.Seek(start);
            parser.Lexer.ClearPushback();
            PdfObject value = parser.ParseValue();
            if (parser.NextIsKeyword("stream"))
            {
                throw new PdfParseException(0, "Object streams cannot contain stream objects.");
            }

            consumed += cursor.Position - start;
            if (consumed > budget)
            {
                throw new PdfObjectTooLargeException("Object stream objects overlap: parsing them read more than " + ReadFactor.ToString(CultureInfo.InvariantCulture) + " times the decoded stream.");
            }

            value.ObjectNumber = id;
            value.Generation = 0;
            objects.Add(value);
        }

        return objects;
    }

    private static int HeaderInteger(byte[] data, int end, ref int position)
    {
        while (position < end && IsHeaderSeparator(data[position]))
        {
            position++;
        }

        if (position >= end)
        {
            throw new PdfParseException(0, "Object stream header is truncated.");
        }

        long value = 0;
        while (position < end && !IsHeaderSeparator(data[position]))
        {
            byte digit = data[position++];
            if (digit < (byte)'0' || digit > (byte)'9')
            {
                throw new PdfParseException(0, "Object stream header contains an invalid object number or offset.");
            }

            value = (value * 10) + (digit - (byte)'0');
            if (value > int.MaxValue)
            {
                throw new PdfParseException(0, "Object stream header contains an invalid object number or offset.");
            }
        }

        return (int)value;
    }

    private static bool IsHeaderSeparator(byte b)
    {
        return b == 11 || PdfLexer.IsWhitespace(b);
    }
}
