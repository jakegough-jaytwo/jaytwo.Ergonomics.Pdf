#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace jaytwo.Pdf.Internal;

internal sealed class PdfParser
{
    // A name, number, or keyword token is at most this many bytes. Real tokens are a few dozen bytes.
    internal const int MaxTokenBytes = 64 * 1024;

    // An array or dictionary holds at most this many entries. Dictionary keys count even when repeated.
    internal const int MaxContainerEntries = 1024 * 1024;

    // Object numbers listed by one xref stream stay below this value, the PDF 1.7 Annex C indirect-object limit plus object 0.
    internal const int MaxXrefStreamObjects = 8388608;

    // The merged cross-reference map holds at most one entry per this many file bytes, never fewer than the floor and
    // never more than the ceiling below. A real object needs more than 8 bytes of file, so the ratio binds only on entries
    // the file cannot back. The ceiling keeps the map and its growth under about 180 MiB, inside the 256 MiB budget.
    internal const int XrefFileBytesPerEntry = 8;

    internal const int MinXrefEntryBudget = 65536;

    internal const int MaxXrefEntries = 2097152;

    // Parsing the objects of one object stream reads at most this multiple of its decoded length, plus the slack below.
    internal const int ObjectStreamReadFactor = 4;

    private const int ObjectStreamReadSlack = 64 * 1024;
    private const int ObjectStreamPresizeLimit = 1024;
    private const int IndexedDictionaryThreshold = 16;
    private const int MaxCommentDepth = 256;

    private readonly PdfCursor _cursor;
    private readonly Func<PdfObject.Ref, PdfObject>? _resolve;
    private readonly long _baseOffset;
    private readonly Stack<Token> _pushback = new Stack<Token>();
    private readonly int _stringCap;
    private readonly int _containerCap;
    private readonly List<List<(string Key, PdfObject Value)>> _dictionaryScratch = new List<List<(string Key, PdfObject Value)>>();
    private readonly List<List<PdfObject>> _arrayScratch = new List<List<PdfObject>>();
    private byte[] _token = Array.Empty<byte>();
    private char[] _chars = Array.Empty<char>();

    public PdfParser(PdfCursor cursor, Func<PdfObject.Ref, PdfObject>? resolve, long baseOffset)
        : this(cursor, resolve, baseOffset, PdfStreamDecoder.StructuralCap, MaxContainerEntries)
    {
    }

    // Tests pass smaller caps, the way the stream decoder takes a caller cap, so each branch is checked with a small input.
    internal PdfParser(PdfCursor cursor, Func<PdfObject.Ref, PdfObject>? resolve, long baseOffset, int stringCap, int containerCap)
    {
        _cursor = cursor;
        _resolve = resolve;
        _baseOffset = baseOffset;
        _stringCap = stringCap;
        _containerCap = containerCap;
    }

    public PdfObject ParseValue(int depth = 0)
    {
        if (depth > 256)
        {
            throw Error("PDF object nesting is too deep.");
        }

        Token token = NextToken();
        if (token.Kind == TokenKind.Integer)
        {
            Token second = PeekToken();
            if (second.Kind == TokenKind.Integer)
            {
                NextToken();
                Token third = PeekToken();
                if (third.Kind == TokenKind.Keyword && third.Text == "R")
                {
                    NextToken();
                    return new PdfObject.Ref(CheckedInt(token.Integer), CheckedInt(second.Integer));
                }

                _pushback.Push(second);
                return PdfObject.Num.FromLong(token.Integer);
            }

            return PdfObject.Num.FromLong(token.Integer);
        }

        if (token.Kind == TokenKind.Real)
        {
            return PdfObject.Num.FromDouble(token.Real);
        }

        if (token.Kind == TokenKind.Name)
        {
            return new PdfObject.Name(token.Text);
        }

        if (token.Kind == TokenKind.LiteralString || token.Kind == TokenKind.HexString)
        {
            return new PdfObject.Str(token.Bytes, token.Kind == TokenKind.HexString);
        }

        if (token.Kind == TokenKind.Keyword)
        {
            if (token.Text == "true")
            {
                return PdfObject.Bool.True;
            }

            if (token.Text == "false")
            {
                return PdfObject.Bool.False;
            }

            if (token.Text == "null")
            {
                return PdfObject.NullObj.Instance;
            }
        }

        if (token.Kind == TokenKind.ArrayStart)
        {
            // W-115: as with a dictionary, the entries collect in this depth's list and the array is made at its final size.
            List<PdfObject> items = Scratch(_arrayScratch, depth);
            while (true)
            {
                Token next = PeekToken();
                if (next.Kind == TokenKind.ArrayEnd)
                {
                    NextToken();
                    break;
                }

                if (next.Kind == TokenKind.End)
                {
                    throw Error("Array ended at EOF.");
                }

                if (items.Count >= _containerCap)
                {
                    throw new PdfObjectTooLargeException("A PDF array has more than " + _containerCap.ToString(CultureInfo.InvariantCulture) + " entries.");
                }

                items.Add(ParseValue(depth + 1));
            }

            var array = new PdfObject.Arr(items.Count);
            array.Items.AddRange(items);
            items.Clear();
            return array;
        }

        if (token.Kind == TokenKind.DictStart)
        {
            return ParseDictionary(depth);
        }

        throw Error("Unexpected token '" + token.Text + "'.");
    }

    public PdfObject ParseIndirect(int expectedNumber)
    {
        Token number = NextToken();
        Token generation = NextToken();
        Token obj = NextToken();
        if (number.Kind != TokenKind.Integer || generation.Kind != TokenKind.Integer || obj.Kind != TokenKind.Keyword || obj.Text != "obj")
        {
            throw Error("Indirect object header was expected.");
        }

        if (expectedNumber >= 0 && number.Integer != expectedNumber)
        {
            throw Error("Object number " + number.Integer.ToString(CultureInfo.InvariantCulture) + " does not match xref entry " + expectedNumber.ToString(CultureInfo.InvariantCulture) + ".");
        }

        PdfObject value = ParseValue();
        if (value is PdfObject.Dict dict && PeekKeyword("stream"))
        {
            NextToken();
            ConsumeStreamEol();
            long length = ResolveLength(dict);
            long dataOffset = _cursor.Position;
            _cursor.Skip(length);
            SkipOneEol();
            Token endStream = NextToken();
            if (endStream.Kind != TokenKind.Keyword || endStream.Text != "endstream")
            {
                throw Error("endstream was expected after the stream data.");
            }

            dict.Body = new PdfObject.FileBody(_baseOffset + dataOffset, length);
        }

        Token endObj = NextToken();
        if (endObj.Kind != TokenKind.Keyword || endObj.Text != "endobj")
        {
            throw Error("endobj was expected.");
        }

        value.ObjectNumber = (int)number.Integer;
        value.Generation = (int)generation.Integer;
        return value;
    }

    // Reads a classic xref section straight into the merged map, with no per-section copy, the way an xref stream is read.
    // Each subsection header is range-checked before its rows are read, and every new object counts against the budget.
    public PdfObject.Dict ParseClassicXref(Dictionary<int, XrefEntry> target, bool overwrite, int budget)
    {
        Token keyword = NextToken();
        if (keyword.Kind != TokenKind.Keyword || keyword.Text != "xref")
        {
            throw Error("xref keyword was expected.");
        }

        var row = new byte[18];
        while (true)
        {
            Token token = PeekToken();
            if (token.Kind == TokenKind.Keyword && token.Text == "trailer")
            {
                NextToken();
                break;
            }

            if (token.Kind != TokenKind.Integer)
            {
                throw Error("Xref subsection or trailer was expected.");
            }

            long start = NextToken().Integer;
            Token countToken = NextToken();
            if (countToken.Kind != TokenKind.Integer)
            {
                throw Error("Xref subsection count was expected.");
            }

            long count = countToken.Integer;
            if (start < 0 || count < 0 || start + count > (long)int.MaxValue + 1)
            {
                throw Error("Xref subsection exceeds the object number range.");
            }

            SkipWhitespace();
            for (int i = 0; i < count; i++)
            {
                MergeXrefEntry(target, (int)start + i, ReadClassicEntry(row), overwrite, budget);
            }
        }

        PdfObject trailer = ParseValue();
        if (trailer is not PdfObject.Dict trailerDict)
        {
            throw Error("Trailer dictionary was expected.");
        }

        return trailerDict;
    }

    private XrefEntry ReadClassicEntry(byte[] row)
    {
        for (int i = 0; i < 18; i++)
        {
            int b = _cursor.ReadByte();
            if (b < 0)
            {
                throw Error("Truncated xref entry.");
            }

            row[i] = (byte)b;
        }

        int eol = _cursor.ReadByte();
        if (eol == ' ')
        {
            int second = _cursor.ReadByte();
            if (second != '\n' && second != '\r')
            {
                throw Error("Xref entry must end in CRLF, LF, SP LF, or SP CR.");
            }
        }
        else if (eol == '\r')
        {
            int second = _cursor.ReadByte();
            if (second != '\n')
            {
                throw Error("Xref entry must end in CRLF, LF, SP LF, or SP CR.");
            }
        }
        else if (eol != '\n')
        {
            throw Error("Xref entry must end in CRLF, LF, SP LF, or SP CR.");
        }

        if (row[17] != 'n' && row[17] != 'f')
        {
            throw Error("Xref entry flag was expected.");
        }

        if (!TryParseDigits(row, 0, 10, out long offset))
        {
            throw Error("Xref offset is not numeric.");
        }

        if (!TryParseDigits(row, 11, 5, out long generation))
        {
            throw Error("Xref generation is not numeric.");
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

    internal static int XrefEntryBudget(long fileLength)
    {
        return (int)Math.Min(MaxXrefEntries, Math.Max(MinXrefEntryBudget, fileLength / XrefFileBytesPerEntry));
    }

    // Adds one entry to the merged map. An overwriting section replaces what is there; otherwise the newer entry stays.
    // A new object number past the budget throws, so the map never grows beyond what the file can back.
    internal static void MergeXrefEntry(Dictionary<int, XrefEntry> target, int id, XrefEntry entry, bool overwrite, int budget)
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
            throw XrefBudgetExceeded(budget);
        }

        target.Add(id, entry);
    }

    // Reads the entries of one xref stream straight into the merged map, with no per-section copy.
    internal void ParseXrefEntries(PdfObject.Dict dict, byte[] decoded, Dictionary<int, XrefEntry> target, bool overwrite, int budget)
    {
        int[] widths = ReadWidths(dict);
        var index = ReadIndex(dict);
        if ((index.Count & 1) != 0)
        {
            throw new PdfParseException(0, "Xref stream /Index must contain pairs of integers.");
        }

        int entryWidth = widths[0] + widths[1] + widths[2];
        long available = entryWidth == 0 ? 0 : decoded.Length / entryWidth;
        long? size = dict.Get("Size") is PdfObject.Num sizeNumber ? XrefIndexValue(sizeNumber, "/Size") : null;
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

            if (index[pair + 1] > 0 && end > MaxXrefStreamObjects)
            {
                throw new PdfObjectTooLargeException("Xref stream lists object numbers at or above " + MaxXrefStreamObjects.ToString(CultureInfo.InvariantCulture) + ".");
            }

            if (listed > budget)
            {
                throw XrefBudgetExceeded(budget);
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
                MergeXrefEntry(target, start + i, new XrefEntry((byte)type, field1, field2), overwrite, budget);
            }
        }
    }

    private static PdfObjectTooLargeException XrefBudgetExceeded(int budget)
    {
        return new PdfObjectTooLargeException("The cross-reference sections list more than " + budget.ToString(CultureInfo.InvariantCulture) + " distinct objects, the limit for a file of this length.");
    }

    internal static void EnsureXrefColumns(PdfObject.Dict dict, List<PdfObject.Dict?> parms)
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

                index.Add(XrefIndexValue(number, "/Index"));
            }

            return index;
        }

        int size = 0;
        if (dict.Get("Size") is PdfObject.Num sizeNumber)
        {
            size = XrefIndexValue(sizeNumber, "/Size");
        }

        index.Add(0);
        index.Add(size);
        return index;
    }

    private static int XrefIndexValue(PdfObject.Num number, string key)
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

    internal static List<PdfObject> ParseObjectStream(byte[] data, int count, int first)
    {
        // W-115: /N sizes the list, up to a bound so a header that overstates it cannot reserve more. Writers list object
        // numbers in ascending order, so the set that finds a repeat is made only when a number is not above the last.
        var objects = new List<PdfObject>(Math.Min(count, ObjectStreamPresizeLimit));
        HashSet<int>? ids = null;
        int lastId = -1;
        var cursor = PdfCursor.FromBytes(data);
        var parser = new PdfParser(cursor, null, 0);
        long budget = ((long)data.Length * ObjectStreamReadFactor) + ObjectStreamReadSlack;
        long consumed = 0;
        int header = 0;
        for (int i = 0; i < count; i++)
        {
            int id = ObjectStreamHeaderInteger(data, first, ref header);
            int offset = ObjectStreamHeaderInteger(data, first, ref header);
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
                        ids.Add(ObjectStreamHeaderInteger(data, first, ref reread));
                        ObjectStreamHeaderInteger(data, first, ref reread);
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
            parser._pushback.Clear();
            PdfObject value = parser.ParseValue();
            if (parser.NextIsKeyword("stream"))
            {
                throw new PdfParseException(0, "Object streams cannot contain stream objects.");
            }

            consumed += cursor.Position - start;
            if (consumed > budget)
            {
                throw new PdfObjectTooLargeException("Object stream objects overlap: parsing them read more than " + ObjectStreamReadFactor.ToString(CultureInfo.InvariantCulture) + " times the decoded stream.");
            }

            value.ObjectNumber = id;
            value.Generation = 0;
            objects.Add(value);
        }

        return objects;
    }

    private static int ObjectStreamHeaderInteger(byte[] data, int end, ref int position)
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
        return b == 11 || IsWhitespace(b);
    }

    // W-115: a dictionary's entries collect in a list this parser keeps for that nesting depth, so the dictionary is
    // made once at its final size. A repeated key keeps its first position and takes the last value.
    private PdfObject.Dict ParseDictionary(int depth)
    {
        List<(string Key, PdfObject Value)> items = Scratch(_dictionaryScratch, depth);
        Dictionary<string, int>? positions = null;
        int entries = 0;
        while (true)
        {
            Token token = NextToken();
            if (token.Kind == TokenKind.DictEnd)
            {
                break;
            }

            if (token.Kind != TokenKind.Name)
            {
                throw Error("Dictionary key was expected.");
            }

            if (++entries > _containerCap)
            {
                throw new PdfObjectTooLargeException("A PDF dictionary has more than " + _containerCap.ToString(CultureInfo.InvariantCulture) + " entries.");
            }

            PdfObject value = ParseValue(depth + 1);
            if (positions == null && items.Count < IndexedDictionaryThreshold)
            {
                int existing = items.Count - 1;
                while (existing >= 0 && items[existing].Key != token.Text)
                {
                    existing--;
                }

                if (existing >= 0)
                {
                    items[existing] = (token.Text, value);
                }
                else
                {
                    items.Add((token.Text, value));
                }

                continue;
            }

            if (positions == null)
            {
                positions = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < items.Count; i++)
                {
                    positions[items[i].Key] = i;
                }
            }

            if (positions.TryGetValue(token.Text, out int at))
            {
                items[at] = (items[at].Key, value);
            }
            else
            {
                positions.Add(token.Text, items.Count);
                items.Add((token.Text, value));
            }
        }

        var dict = new PdfObject.Dict(items.Count);
        foreach ((string key, PdfObject value) in items)
        {
            dict.Append(key, value);
        }

        items.Clear();
        return dict;
    }

    private static List<T> Scratch<T>(List<List<T>> byDepth, int depth)
    {
        while (byDepth.Count <= depth)
        {
            byDepth.Add(new List<T>());
        }

        List<T> scratch = byDepth[depth];
        scratch.Clear();
        return scratch;
    }

    private long ResolveLength(PdfObject.Dict dict)
    {
        PdfObject? length = dict.Get("Length") ?? throw Error("Stream is missing /Length.");
        if (length is PdfObject.Ref reference)
        {
            if (_resolve == null)
            {
                throw Error("Stream /Length is indirect and cannot be resolved.");
            }

            long saved = _cursor.Position;
            try
            {
                length = _resolve(reference);
            }
            finally
            {
                _cursor.Seek(saved);
            }
        }

        if (length is PdfObject.Num number)
        {
            return number.IsInteger ? number.LongValue : (long)number.DoubleValue;
        }

        throw Error("Stream /Length is not a number.");
    }

    private void ConsumeStreamEol()
    {
        int first = _cursor.ReadByte();
        if (first == '\r')
        {
            if (_cursor.PeekByte() == '\n')
            {
                _cursor.ReadByte();
            }

            return;
        }

        if (first == '\n')
        {
            return;
        }

        throw Error("stream keyword must be followed by EOL.");
    }

    private void SkipOneEol()
    {
        int peek = _cursor.PeekByte();
        if (peek == '\r')
        {
            _cursor.ReadByte();
            if (_cursor.PeekByte() == '\n')
            {
                _cursor.ReadByte();
            }
        }
        else if (peek == '\n')
        {
            _cursor.ReadByte();
        }
    }

    internal bool NextIsKeyword(string keyword)
    {
        return PeekKeyword(keyword);
    }

    private bool PeekKeyword(string keyword)
    {
        Token token = PeekToken();
        return token.Kind == TokenKind.Keyword && token.Text == keyword;
    }

    private Token PeekToken()
    {
        if (_pushback.Count == 0)
        {
            _pushback.Push(ReadToken());
        }

        return _pushback.Peek();
    }

    private Token NextToken()
    {
        if (_pushback.Count > 0)
        {
            return _pushback.Pop();
        }

        return ReadToken();
    }

    private static int CheckedInt(long value)
    {
        if (value < 0 || value > int.MaxValue)
        {
            throw new PdfParseException(0, "Object number does not fit in a 32-bit integer.");
        }

        return (int)value;
    }

    private Token ReadToken()
    {
        int commentDepth = 0;
        int b;
        while (true)
        {
            SkipWhitespace();
            b = _cursor.ReadByte();
            if (b != '%')
            {
                break;
            }

            commentDepth++;
            if (commentDepth > MaxCommentDepth)
            {
                throw Error("Too many consecutive PDF comments.");
            }

            int c;
            do
            {
                c = _cursor.ReadByte();
            }
            while (c >= 0 && c != '\n' && c != '\r');
        }

        if (b < 0)
        {
            return new Token(TokenKind.End, string.Empty, Array.Empty<byte>(), 0, 0);
        }

        if (b == '[')
        {
            return new Token(TokenKind.ArrayStart, "[", Array.Empty<byte>(), 0, 0);
        }

        if (b == ']')
        {
            return new Token(TokenKind.ArrayEnd, "]", Array.Empty<byte>(), 0, 0);
        }

        if (b == '<')
        {
            if (_cursor.PeekByte() == '<')
            {
                _cursor.ReadByte();
                return new Token(TokenKind.DictStart, "<<", Array.Empty<byte>(), 0, 0);
            }

            return new Token(TokenKind.HexString, string.Empty, ReadHexString(), 0, 0);
        }

        if (b == '>')
        {
            if (_cursor.PeekByte() == '>')
            {
                _cursor.ReadByte();
                return new Token(TokenKind.DictEnd, ">>", Array.Empty<byte>(), 0, 0);
            }

            throw Error("Unexpected '>'.");
        }

        if (b == '(')
        {
            return new Token(TokenKind.LiteralString, string.Empty, ReadLiteralString(), 0, 0);
        }

        if (b == '/')
        {
            return new Token(TokenKind.Name, ReadName(), Array.Empty<byte>(), 0, 0);
        }

        if (b == '+' || b == '-' || b == '.' || (b >= '0' && b <= '9'))
        {
            return ReadNumber(b);
        }

        return ReadKeyword(b);
    }

    private Token ReadNumber(int first)
    {
        long start = _cursor.Position - 1;
        bool negative = first == '-';
        bool digits = first >= '0' && first <= '9';
        if (first == '.')
        {
            return ReadRealNumber(start);
        }

        long limit = negative ? long.MinValue : -long.MaxValue;
        long multiplyLimit = limit / 10;
        long integer = digits ? -(first - '0') : 0;
        bool overflow = false;
        int length = 1;
        while (true)
        {
            int b = _cursor.PeekByte();
            if (b == '.')
            {
                return ReadRealNumber(start);
            }

            if (b < '0' || b > '9')
            {
                break;
            }

            CheckTokenLength(length, "number");
            length++;
            _cursor.ReadByte();
            digits = true;
            if (!overflow)
            {
                int digit = b - '0';
                if (integer < multiplyLimit)
                {
                    overflow = true;
                }
                else
                {
                    integer *= 10;
                    if (integer < limit + digit)
                    {
                        overflow = true;
                    }
                    else
                    {
                        integer -= digit;
                    }
                }
            }
        }

        if (!digits)
        {
            throw Error("Invalid number.");
        }

        if (overflow)
        {
            throw Error("Integer is out of range.");
        }

        integer = negative ? integer : -integer;
        return new Token(TokenKind.Integer, string.Empty, Array.Empty<byte>(), integer, integer);
    }

    private Token ReadRealNumber(long start)
    {
        _cursor.Seek(start);
        int length = 0;
        int first = _cursor.ReadByte();
        AppendTokenByte(ref length, first);
        bool digits = first >= '0' && first <= '9';
        bool decimalPoint = first == '.';
        while (true)
        {
            int b = _cursor.PeekByte();
            if ((b == '.' && !decimalPoint) || (b >= '0' && b <= '9'))
            {
                CheckTokenLength(length, "number");
                decimalPoint |= b == '.';
                digits |= b != '.';
                AppendTokenByte(ref length, _cursor.ReadByte());
                continue;
            }

            break;
        }

        if (!digits)
        {
            throw Error("Invalid number.");
        }

        if (_chars.Length < length)
        {
            _chars = new char[Math.Max(32, length)];
        }

        for (int i = 0; i < length; i++)
        {
            _chars[i] = (char)_token[i];
        }

#if NETSTANDARD2_0
        bool parsed = double.TryParse(new string(_chars, 0, length), NumberStyles.Float, CultureInfo.InvariantCulture, out double number);
#else
        bool parsed = double.TryParse(_chars.AsSpan(0, length), NumberStyles.Float, CultureInfo.InvariantCulture, out double number);
#endif
        if (!parsed)
        {
            throw Error("Real number is invalid.");
        }

        // W-115: a real token's text is never read, so the parser builds none.
        return new Token(TokenKind.Real, string.Empty, Array.Empty<byte>(), 0, number);
    }

    private Token ReadKeyword(int first)
    {
        int length = 0;
        AppendTokenByte(ref length, first);
        while (true)
        {
            int b = _cursor.PeekByte();
            if (b < 0 || IsWhitespace(b) || IsDelimiter(b))
            {
                break;
            }

            CheckTokenLength(length, "keyword");
            AppendTokenByte(ref length, _cursor.ReadByte());
        }

        return new Token(TokenKind.Keyword, PdfCommonTokens.Find(_token, length) ?? Latin1(_token, length), Array.Empty<byte>(), 0, 0);
    }

    // Each byte becomes the character with that code, as a keyword always has.
    private static string Latin1(byte[] bytes, int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)bytes[i];
        }

        return new string(chars);
    }

    private string ReadName()
    {
        int length = 0;
        while (true)
        {
            int b = _cursor.PeekByte();
            if (b < 0 || IsWhitespace(b) || IsDelimiter(b))
            {
                break;
            }

            CheckTokenLength(length, "name");
            AppendTokenByte(ref length, _cursor.ReadByte());
        }

        // Escapes decode in place: each one shortens the name, so the write index never passes the read index.
        int decoded = 0;
        for (int i = 0; i < length; i++)
        {
            if (_token[i] == (byte)'#' && i + 2 < length)
            {
                _token[decoded++] = (byte)((Hex(_token[i + 1]) << 4) | Hex(_token[i + 2]));
                i += 2;
            }
            else
            {
                _token[decoded++] = _token[i];
            }
        }

        return PdfCommonTokens.Find(_token, decoded) ?? Encoding.UTF8.GetString(_token, 0, decoded);
    }

    // W-115: one buffer per parser holds the bytes of the token being read, so a token allocates only its value.
    private void AppendTokenByte(ref int length, int value)
    {
        if (length == _token.Length)
        {
            Array.Resize(ref _token, Math.Max(32, _token.Length * 2));
        }

        _token[length++] = (byte)value;
    }

    private byte[] TokenBytes(int length)
    {
        var bytes = new byte[length];
        Buffer.BlockCopy(_token, 0, bytes, 0, length);
        return bytes;
    }

    private byte[] ReadLiteralString()
    {
        int length = 0;
        int depth = 1;
        while (depth > 0)
        {
            if (length > _stringCap)
            {
                throw new PdfObjectTooLargeException("Literal string exceeded the structural cap.");
            }

            int b = _cursor.ReadByte();
            if (b < 0)
            {
                throw Error("Literal string ended at EOF.");
            }

            if (b == '\\')
            {
                int escaped = _cursor.ReadByte();
                if (escaped < 0)
                {
                    throw Error("Literal string escape ended at EOF.");
                }

                if (escaped == '\n')
                {
                    continue;
                }

                if (escaped == '\r')
                {
                    if (_cursor.PeekByte() == '\n')
                    {
                        _cursor.ReadByte();
                    }

                    continue;
                }

                if (escaped >= '0' && escaped <= '7')
                {
                    int value = escaped - '0';
                    for (int i = 0; i < 2; i++)
                    {
                        int peek = _cursor.PeekByte();
                        if (peek < '0' || peek > '7')
                        {
                            break;
                        }

                        value = (value * 8) + (_cursor.ReadByte() - '0');
                    }

                    AppendTokenByte(ref length, (byte)value);
                    continue;
                }

                AppendTokenByte(ref length, escaped switch
                {
                    'n' => (byte)'\n',
                    'r' => (byte)'\r',
                    't' => (byte)'\t',
                    'b' => (byte)'\b',
                    'f' => (byte)'\f',
                    _ => (byte)escaped,
                });
                continue;
            }

            if (b == '(')
            {
                depth++;
                AppendTokenByte(ref length, (byte)b);
                continue;
            }

            if (b == ')')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }

                AppendTokenByte(ref length, (byte)b);
                continue;
            }

            if (b == '\r')
            {
                if (_cursor.PeekByte() == '\n')
                {
                    _cursor.ReadByte();
                }

                AppendTokenByte(ref length, (byte)'\n');
                continue;
            }

            AppendTokenByte(ref length, (byte)b);
        }

        return TokenBytes(length);
    }

    private byte[] ReadHexString()
    {
        int length = 0;
        int high = -1;
        while (true)
        {
            int b = _cursor.ReadByte();
            if (b < 0)
            {
                throw Error("Hex string ended at EOF.");
            }

            if (b == '>')
            {
                break;
            }

            if (IsWhitespace(b))
            {
                continue;
            }

            int nibble = Hex((byte)b);
            if (high < 0)
            {
                high = nibble;
            }
            else
            {
                if (length >= _stringCap)
                {
                    throw new PdfObjectTooLargeException("Hex string exceeded the structural cap.");
                }

                AppendTokenByte(ref length, (byte)((high << 4) | nibble));
                high = -1;
            }
        }

        if (high >= 0)
        {
            if (length >= _stringCap)
            {
                throw new PdfObjectTooLargeException("Hex string exceeded the structural cap.");
            }

            AppendTokenByte(ref length, (byte)(high << 4));
        }

        return TokenBytes(length);
    }

    private static void CheckTokenLength(int length, string kind)
    {
        if (length >= MaxTokenBytes)
        {
            throw new PdfObjectTooLargeException("A PDF " + kind + " is longer than " + MaxTokenBytes.ToString(CultureInfo.InvariantCulture) + " bytes.");
        }
    }

    private void SkipWhitespace()
    {
        while (true)
        {
            int b = _cursor.PeekByte();
            if (b < 0 || !IsWhitespace(b))
            {
                return;
            }

            _cursor.ReadByte();
        }
    }

    private PdfParseException Error(string message)
    {
        return new PdfParseException(_baseOffset + _cursor.Position, message);
    }

    private static int Hex(byte b)
    {
        if (b >= (byte)'0' && b <= (byte)'9')
        {
            return b - (byte)'0';
        }

        if (b >= (byte)'A' && b <= (byte)'F')
        {
            return b - (byte)'A' + 10;
        }

        if (b >= (byte)'a' && b <= (byte)'f')
        {
            return b - (byte)'a' + 10;
        }

        throw new PdfParseException(0, "Invalid hex digit in name or string.");
    }

    private static bool IsWhitespace(int b)
    {
        return b is 0 or 9 or 10 or 12 or 13 or 32;
    }

    private static bool IsDelimiter(int b)
    {
        return b is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';
    }

    private enum TokenKind
    {
        End,
        Integer,
        Real,
        Name,
        LiteralString,
        HexString,
        Keyword,
        ArrayStart,
        ArrayEnd,
        DictStart,
        DictEnd,
    }

    private readonly struct Token
    {
        public Token(TokenKind kind, string text, byte[] bytes, long integer, double real)
        {
            Kind = kind;
            Text = text;
            Bytes = bytes;
            Integer = integer;
            Real = real;
        }

        public TokenKind Kind { get; }

        public string Text { get; }

        public byte[] Bytes { get; }

        public long Integer { get; }

        public double Real { get; }
    }
}
