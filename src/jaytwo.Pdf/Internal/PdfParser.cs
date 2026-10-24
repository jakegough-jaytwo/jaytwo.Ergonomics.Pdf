#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;

namespace jaytwo.Pdf.Internal;

// Parses PDF values and indirect objects from the lexer's tokens. A stream object's data is located, not read.
internal sealed class PdfParser
{
    // An array or dictionary holds at most this many entries. Dictionary keys count even when repeated.
    internal const int MaxContainerEntries = 1024 * 1024;

    private const int IndexedDictionaryThreshold = 16;

    private readonly PdfLexer _lexer;
    private readonly PdfCursor _cursor;
    private readonly Func<PdfObject.Ref, PdfObject>? _resolve;
    private readonly long _baseOffset;
    private readonly int _containerCap;
    private readonly List<List<(string Key, PdfObject Value)>> _dictionaryScratch = new List<List<(string Key, PdfObject Value)>>();
    private readonly List<List<PdfObject>> _arrayScratch = new List<List<PdfObject>>();

    public PdfParser(PdfCursor cursor, Func<PdfObject.Ref, PdfObject>? resolve, long baseOffset)
        : this(cursor, resolve, baseOffset, PdfStreamDecoder.StructuralCap, MaxContainerEntries)
    {
    }

    // Tests pass smaller caps, the way the stream decoder takes a caller cap, so each branch is checked with a small input.
    internal PdfParser(PdfCursor cursor, Func<PdfObject.Ref, PdfObject>? resolve, long baseOffset, int stringCap, int containerCap)
    {
        _lexer = new PdfLexer(cursor, baseOffset, stringCap);
        _cursor = cursor;
        _resolve = resolve;
        _baseOffset = baseOffset;
        _containerCap = containerCap;
    }

    public PdfLexer Lexer => _lexer;

    public PdfObject ParseValue(int depth = 0)
    {
        if (depth > 256)
        {
            throw Error("PDF object nesting is too deep.");
        }

        PdfToken token = _lexer.Next();
        if (token.Kind == PdfTokenKind.Integer)
        {
            PdfToken second = _lexer.Peek();
            if (second.Kind == PdfTokenKind.Integer)
            {
                _lexer.Next();
                PdfToken third = _lexer.Peek();
                if (third.Kind == PdfTokenKind.Keyword && third.Text == "R")
                {
                    _lexer.Next();
                    return new PdfObject.Ref(CheckedInt(token.Integer), CheckedInt(second.Integer));
                }

                _lexer.PushBack(second);
                return PdfObject.Num.FromLong(token.Integer);
            }

            return PdfObject.Num.FromLong(token.Integer);
        }

        if (token.Kind == PdfTokenKind.Real)
        {
            return PdfObject.Num.FromDouble(token.Real);
        }

        if (token.Kind == PdfTokenKind.Name)
        {
            return new PdfObject.Name(token.Text);
        }

        if (token.Kind == PdfTokenKind.LiteralString || token.Kind == PdfTokenKind.HexString)
        {
            return new PdfObject.Str(token.Bytes, token.Kind == PdfTokenKind.HexString);
        }

        if (token.Kind == PdfTokenKind.Keyword)
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

        if (token.Kind == PdfTokenKind.ArrayStart)
        {
            // W-115: as with a dictionary, the entries collect in this depth's list and the array is made at its final size.
            List<PdfObject> items = Scratch(_arrayScratch, depth);
            while (true)
            {
                PdfToken next = _lexer.Peek();
                if (next.Kind == PdfTokenKind.ArrayEnd)
                {
                    _lexer.Next();
                    break;
                }

                if (next.Kind == PdfTokenKind.End)
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

        if (token.Kind == PdfTokenKind.DictStart)
        {
            return ParseDictionary(depth);
        }

        throw Error("Unexpected token '" + token.Text + "'.");
    }

    public PdfObject ParseIndirect(int expectedNumber)
    {
        PdfToken number = _lexer.Next();
        PdfToken generation = _lexer.Next();
        PdfToken obj = _lexer.Next();
        if (number.Kind != PdfTokenKind.Integer || generation.Kind != PdfTokenKind.Integer || obj.Kind != PdfTokenKind.Keyword || obj.Text != "obj")
        {
            throw Error("Indirect object header was expected.");
        }

        if (expectedNumber >= 0 && number.Integer != expectedNumber)
        {
            throw Error("Object number " + number.Integer.ToString(CultureInfo.InvariantCulture) + " does not match xref entry " + expectedNumber.ToString(CultureInfo.InvariantCulture) + ".");
        }

        PdfObject value = ParseValue();
        if (value is PdfObject.Dict dict && _lexer.PeekKeyword("stream"))
        {
            _lexer.Next();
            ConsumeStreamEol();
            long length = ResolveLength(dict);
            long dataOffset = _cursor.Position;
            _cursor.Skip(length);
            SkipOneEol();
            PdfToken endStream = _lexer.Next();
            if (endStream.Kind != PdfTokenKind.Keyword || endStream.Text != "endstream")
            {
                throw Error("endstream was expected after the stream data.");
            }

            dict.Body = new PdfObject.FileBody(_baseOffset + dataOffset, length);
        }

        PdfToken endObj = _lexer.Next();
        if (endObj.Kind != PdfTokenKind.Keyword || endObj.Text != "endobj")
        {
            throw Error("endobj was expected.");
        }

        value.ObjectNumber = (int)number.Integer;
        value.Generation = (int)generation.Integer;
        return value;
    }

    internal bool NextIsKeyword(string keyword)
    {
        return _lexer.PeekKeyword(keyword);
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
            PdfToken token = _lexer.Next();
            if (token.Kind == PdfTokenKind.DictEnd)
            {
                break;
            }

            if (token.Kind != PdfTokenKind.Name)
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

    private static int CheckedInt(long value)
    {
        if (value < 0 || value > int.MaxValue)
        {
            throw new PdfParseException(0, "Object number does not fit in a 32-bit integer.");
        }

        return (int)value;
    }

    private PdfParseException Error(string message)
    {
        return _lexer.Error(message);
    }
}
