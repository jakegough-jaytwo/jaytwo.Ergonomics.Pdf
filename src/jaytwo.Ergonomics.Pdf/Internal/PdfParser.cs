#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace jaytwo.Ergonomics.Pdf.Internal;

internal sealed class PdfParser
{
    private const int MaxCommentDepth = 256;

    private readonly PdfCursor _cursor;
    private readonly Func<int, PdfObject>? _resolve;
    private readonly long _baseOffset;
    private readonly Stack<Token> _pushback = new Stack<Token>();

    public PdfParser(PdfCursor cursor, Func<int, PdfObject>? resolve, long baseOffset)
    {
        _cursor = cursor;
        _resolve = resolve;
        _baseOffset = baseOffset;
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
            var array = new PdfObject.Arr();
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

                array.Items.Add(ParseValue(depth + 1));
            }

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

    public (Dictionary<int, XrefEntry> Entries, PdfObject.Dict Trailer) ParseXrefSection()
    {
        Token first = PeekToken();
        if (first.Kind == TokenKind.Keyword && first.Text == "xref")
        {
            return ParseClassicXref();
        }

        PdfObject obj = ParseIndirect(-1);
        if (obj is not PdfObject.Dict dict || dict.Body == null)
        {
            throw Error("Xref stream object was expected.");
        }

        return (ParseXrefStream(dict), dict);
    }

    private (Dictionary<int, XrefEntry> Entries, PdfObject.Dict Trailer) ParseClassicXref()
    {
        Token keyword = NextToken();
        if (keyword.Kind != TokenKind.Keyword || keyword.Text != "xref")
        {
            throw Error("xref keyword was expected.");
        }

        var entries = new Dictionary<int, XrefEntry>();
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

            int start = (int)NextToken().Integer;
            Token countToken = NextToken();
            if (countToken.Kind != TokenKind.Integer)
            {
                throw Error("Xref subsection count was expected.");
            }

            int count = (int)countToken.Integer;
            SkipWhitespace();
            for (int i = 0; i < count; i++)
            {
                XrefEntry entry = ReadClassicEntry();
                entries[start + i] = entry;
            }
        }

        PdfObject trailer = ParseValue();
        if (trailer is not PdfObject.Dict trailerDict)
        {
            throw Error("Trailer dictionary was expected.");
        }

        return (entries, trailerDict);
    }

    private XrefEntry ReadClassicEntry()
    {
        var chars = new char[18];
        for (int i = 0; i < 18; i++)
        {
            int b = _cursor.ReadByte();
            if (b < 0)
            {
                throw Error("Truncated xref entry.");
            }

            chars[i] = (char)b;
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

        if (chars[17] != 'n' && chars[17] != 'f')
        {
            throw Error("Xref entry flag was expected.");
        }

        if (!long.TryParse(new string(chars, 0, 10), NumberStyles.None, CultureInfo.InvariantCulture, out long offset))
        {
            throw Error("Xref offset is not numeric.");
        }

        if (!int.TryParse(new string(chars, 11, 5), NumberStyles.None, CultureInfo.InvariantCulture, out int generation))
        {
            throw Error("Xref generation is not numeric.");
        }

        byte type = chars[17] == 'n' ? (byte)1 : (byte)0;
        return new XrefEntry(type, offset, generation);
    }

    private Dictionary<int, XrefEntry> ParseXrefStream(PdfObject.Dict dict)
    {
        if (dict.Body is not PdfObject.FileBody body)
        {
            throw Error("Xref stream has no data.");
        }

        byte[] raw = ReadFile(body.Offset, body.Length);
        var filters = PdfStreamDecoder.FiltersOf(dict);
        var parms = PdfStreamDecoder.ParmsOf(dict, filters.Count);
        EnsureXrefColumns(dict, parms);
        byte[] decoded = PdfStreamDecoder.Decode(raw, filters, parms, PdfStreamDecoder.XrefCap);
        return ParseXrefEntries(dict, decoded);
    }

    internal Dictionary<int, XrefEntry> ParseXrefEntries(PdfObject.Dict dict, byte[] decoded)
    {
        int[] widths = ReadWidths(dict);
        var index = ReadIndex(dict);
        if ((index.Count & 1) != 0)
        {
            throw new PdfParseException(0, "Xref stream /Index must contain pairs of integers.");
        }

        int entryWidth = widths[0] + widths[1] + widths[2];
        var entries = new Dictionary<int, XrefEntry>();
        int cursor = 0;
        for (int pair = 0; pair < index.Count; pair += 2)
        {
            int start = index[pair];
            int count = index[pair + 1];
            if ((long)start + count > (long)int.MaxValue + 1)
            {
                throw new PdfParseException(0, "Xref stream /Index subsection exceeds the object number range.");
            }

            if (count > 0 && (entryWidth == 0 || count > (decoded.Length - cursor) / entryWidth))
            {
                throw new PdfParseException(0, "Xref stream /Index count exceeds the decoded entries.");
            }

            for (int i = 0; i < count; i++)
            {
                long typeRaw = ReadField(decoded, ref cursor, widths[0]);
                int type = widths[0] == 0 ? 1 : (int)typeRaw;
                long field1 = ReadField(decoded, ref cursor, widths[1]);
                int field2 = (int)ReadField(decoded, ref cursor, widths[2]);
                entries[start + i] = new XrefEntry((byte)type, field1, field2);
            }
        }

        return entries;
    }

    internal static void EnsureXrefColumns(PdfObject.Dict dict, List<PdfObject.Dict?> parms)
    {
        int width = 0;
        if (dict.Get("W") is PdfObject.Arr w)
        {
            foreach (PdfObject item in w.Items)
            {
                if (item is PdfObject.Num number)
                {
                    width += number.IsInteger ? (int)number.LongValue : (int)number.DoubleValue;
                }
            }
        }

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

    private byte[] ReadFile(long offset, long length)
    {
        if (length > int.MaxValue)
        {
            throw new PdfObjectTooLargeException("Stream is too large to decode.");
        }

        if (_cursor.Length < offset + length)
        {
            throw Error("Stream extends past EOF.");
        }

        long saved = _cursor.Position;
        _cursor.Seek(offset - _baseOffset);
        var data = new byte[(int)length];
        for (int i = 0; i < data.Length; i++)
        {
            int b = _cursor.ReadByte();
            if (b < 0)
            {
                throw Error("Stream ended early.");
            }

            data[i] = (byte)b;
        }

        _cursor.Seek(saved);
        return data;
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

    private PdfObject.Dict ParseDictionary(int depth)
    {
        var dict = new PdfObject.Dict();
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

            dict.Set(token.Text, ParseValue(depth + 1));
        }

        return dict;
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
                length = _resolve(reference.ObjectId);
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
        var text = new StringBuilder();
        text.Append((char)first);
        bool real = first == '.';
        bool digits = first >= '0' && first <= '9';
        while (true)
        {
            int b = _cursor.PeekByte();
            if (b == '.' && !real)
            {
                real = true;
                text.Append((char)_cursor.ReadByte());
                continue;
            }

            if (b >= '0' && b <= '9')
            {
                digits = true;
                text.Append((char)_cursor.ReadByte());
                continue;
            }

            break;
        }

        if (!digits)
        {
            throw Error("Invalid number.");
        }

        string value = text.ToString();
        if (!real)
        {
            if (!long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
            {
                throw Error("Integer is out of range.");
            }

            return new Token(TokenKind.Integer, value, Array.Empty<byte>(), integer, integer);
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            throw Error("Real number is invalid.");
        }

        return new Token(TokenKind.Real, value, Array.Empty<byte>(), 0, number);
    }

    private Token ReadKeyword(int first)
    {
        var text = new StringBuilder();
        text.Append((char)first);
        while (true)
        {
            int b = _cursor.PeekByte();
            if (b < 0 || IsWhitespace(b) || IsDelimiter(b))
            {
                break;
            }

            text.Append((char)_cursor.ReadByte());
        }

        return new Token(TokenKind.Keyword, text.ToString(), Array.Empty<byte>(), 0, 0);
    }

    private string ReadName()
    {
        var raw = new List<byte>();
        while (true)
        {
            int b = _cursor.PeekByte();
            if (b < 0 || IsWhitespace(b) || IsDelimiter(b))
            {
                break;
            }

            raw.Add((byte)_cursor.ReadByte());
        }

        var decoded = new List<byte>();
        for (int i = 0; i < raw.Count; i++)
        {
            if (raw[i] == (byte)'#' && i + 2 < raw.Count)
            {
                decoded.Add((byte)((Hex(raw[i + 1]) << 4) | Hex(raw[i + 2])));
                i += 2;
            }
            else
            {
                decoded.Add(raw[i]);
            }
        }

        return Encoding.UTF8.GetString(decoded.ToArray());
    }

    private byte[] ReadLiteralString()
    {
        var output = new List<byte>();
        int depth = 1;
        while (depth > 0)
        {
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

                    output.Add((byte)value);
                    continue;
                }

                output.Add(escaped switch
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
                output.Add((byte)b);
                continue;
            }

            if (b == ')')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }

                output.Add((byte)b);
                continue;
            }

            if (b == '\r')
            {
                if (_cursor.PeekByte() == '\n')
                {
                    _cursor.ReadByte();
                }

                output.Add((byte)'\n');
                continue;
            }

            output.Add((byte)b);
            if (output.Count > PdfStreamDecoder.StructuralCap)
            {
                throw new PdfObjectTooLargeException("Literal string exceeded the structural cap.");
            }
        }

        return output.ToArray();
    }

    private byte[] ReadHexString()
    {
        var output = new List<byte>();
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
                output.Add((byte)((high << 4) | nibble));
                high = -1;
            }
        }

        if (high >= 0)
        {
            output.Add((byte)(high << 4));
        }

        return output.ToArray();
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
