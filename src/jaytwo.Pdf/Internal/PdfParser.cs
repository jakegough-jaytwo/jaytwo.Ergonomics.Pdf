#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// Parses PDF values and indirect objects from the lexer's tokens. A stream object's data is located, not read.
// D-55: one async-shaped implementation; the synchronous members run it with useAsync false.
internal sealed class PdfParser
{
    // An array or dictionary holds at most this many entries. Dictionary keys count even when repeated.
    internal const int MaxContainerEntries = 1024 * 1024;

    private const int IndexedDictionaryThreshold = 16;

    private readonly PdfLexer _lexer;
    private readonly PdfCursor _cursor;
    private readonly PdfResolver? _resolve;
    private readonly bool _useAsync;
    private readonly CancellationToken _cancellationToken;
    private readonly long _baseOffset;
    private readonly int _containerCap;
    private readonly List<List<(string Key, PdfObject Value)>> _dictionaryScratch = new List<List<(string Key, PdfObject Value)>>();
    private readonly List<List<PdfObject>> _arrayScratch = new List<List<PdfObject>>();

    public PdfParser(PdfCursor cursor, PdfResolver? resolve, long baseOffset)
        : this(cursor, resolve, baseOffset, false, CancellationToken.None)
    {
    }

    public PdfParser(PdfCursor cursor, PdfResolver? resolve, long baseOffset, bool useAsync, CancellationToken cancellationToken)
        : this(cursor, resolve, baseOffset, PdfStreamDecoder.StructuralCap, MaxContainerEntries, useAsync, cancellationToken)
    {
    }

    // Tests pass smaller caps, the way the stream decoder takes a caller cap, so each branch is checked with a small input.
    internal PdfParser(PdfCursor cursor, PdfResolver? resolve, long baseOffset, int stringCap, int containerCap)
        : this(cursor, resolve, baseOffset, stringCap, containerCap, false, CancellationToken.None)
    {
    }

    private PdfParser(PdfCursor cursor, PdfResolver? resolve, long baseOffset, int stringCap, int containerCap, bool useAsync, CancellationToken cancellationToken)
    {
        _lexer = new PdfLexer(cursor, baseOffset, stringCap, useAsync, cancellationToken);
        _cursor = cursor;
        _resolve = resolve;
        _baseOffset = baseOffset;
        _containerCap = containerCap;
        _useAsync = useAsync;
        _cancellationToken = cancellationToken;
    }

    public PdfLexer Lexer => _lexer;

    public PdfObject ParseValue(int depth = 0)
    {
        return PdfSync.Result(ParseValueAsync(depth));
    }

    public PdfObject ParseIndirect(int expectedNumber)
    {
        return PdfSync.Result(ParseIndirectAsync(expectedNumber));
    }

    internal bool NextIsKeyword(string keyword)
    {
        return PdfSync.Result(_lexer.PeekKeywordAsync(keyword));
    }

    public async ValueTask<PdfObject> ParseValueAsync(int depth = 0)
    {
        if (depth > 256)
        {
            throw Error("PDF object nesting is too deep.");
        }

        PdfToken token = await _lexer.NextAsync().ConfigureAwait(false);
        if (token.Kind == PdfTokenKind.Integer)
        {
            PdfToken second = await _lexer.PeekAsync().ConfigureAwait(false);
            if (second.Kind == PdfTokenKind.Integer)
            {
                await _lexer.NextAsync().ConfigureAwait(false);
                PdfToken third = await _lexer.PeekAsync().ConfigureAwait(false);
                if (third.Kind == PdfTokenKind.Keyword && third.Text == "R")
                {
                    await _lexer.NextAsync().ConfigureAwait(false);
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
                PdfToken next = await _lexer.PeekAsync().ConfigureAwait(false);
                if (next.Kind == PdfTokenKind.ArrayEnd)
                {
                    await _lexer.NextAsync().ConfigureAwait(false);
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

                items.Add(await ParseValueAsync(depth + 1).ConfigureAwait(false));
            }

            var array = new PdfObject.Arr(items.Count);
            array.Items.AddRange(items);
            items.Clear();
            return array;
        }

        if (token.Kind == PdfTokenKind.DictStart)
        {
            return await ParseDictionaryAsync(depth).ConfigureAwait(false);
        }

        throw Error("Unexpected token '" + token.Text + "'.");
    }

    public async ValueTask<PdfObject> ParseIndirectAsync(int expectedNumber)
    {
        PdfToken number = await _lexer.NextAsync().ConfigureAwait(false);
        PdfToken generation = await _lexer.NextAsync().ConfigureAwait(false);
        PdfToken obj = await _lexer.NextAsync().ConfigureAwait(false);
        if (number.Kind != PdfTokenKind.Integer || generation.Kind != PdfTokenKind.Integer || obj.Kind != PdfTokenKind.Keyword || obj.Text != "obj")
        {
            throw Error("Indirect object header was expected.");
        }

        if (expectedNumber >= 0 && number.Integer != expectedNumber)
        {
            throw Error("Object number " + number.Integer.ToString(CultureInfo.InvariantCulture) + " does not match xref entry " + expectedNumber.ToString(CultureInfo.InvariantCulture) + ".");
        }

        PdfObject value = await ParseValueAsync().ConfigureAwait(false);
        if (value is PdfObject.Dict dict && await _lexer.PeekKeywordAsync("stream").ConfigureAwait(false))
        {
            await _lexer.NextAsync().ConfigureAwait(false);
            await ConsumeStreamEolAsync().ConfigureAwait(false);
            long length = await ResolveLengthAsync(dict).ConfigureAwait(false);
            long dataOffset = _cursor.Position;
            _cursor.Skip(length);
            await SkipOneEolAsync().ConfigureAwait(false);
            PdfToken endStream = await _lexer.NextAsync().ConfigureAwait(false);
            if (endStream.Kind != PdfTokenKind.Keyword || endStream.Text != "endstream")
            {
                throw Error("endstream was expected after the stream data.");
            }

            dict.Body = new PdfObject.FileBody(_baseOffset + dataOffset, length);
        }

        PdfToken endObj = await _lexer.NextAsync().ConfigureAwait(false);
        if (endObj.Kind != PdfTokenKind.Keyword || endObj.Text != "endobj")
        {
            throw Error("endobj was expected.");
        }

        value.ObjectNumber = (int)number.Integer;
        value.Generation = (int)generation.Integer;
        return value;
    }

    // W-115: a dictionary's entries collect in a list this parser keeps for that nesting depth, so the dictionary is
    // made once at its final size. A repeated key keeps its first position and takes the last value.
    private async ValueTask<PdfObject.Dict> ParseDictionaryAsync(int depth)
    {
        List<(string Key, PdfObject Value)> items = Scratch(_dictionaryScratch, depth);
        Dictionary<string, int>? positions = null;
        int entries = 0;
        while (true)
        {
            PdfToken token = await _lexer.NextAsync().ConfigureAwait(false);
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

            PdfObject value = await ParseValueAsync(depth + 1).ConfigureAwait(false);
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

    private async ValueTask<long> ResolveLengthAsync(PdfObject.Dict dict)
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
                length = await _resolve(reference, _useAsync, _cancellationToken).ConfigureAwait(false);
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

    private async ValueTask ConsumeStreamEolAsync()
    {
        int first = await _lexer.ReadByteAsync().ConfigureAwait(false);
        if (first == '\r')
        {
            if (await _lexer.PeekByteAsync().ConfigureAwait(false) == '\n')
            {
                await _lexer.ReadByteAsync().ConfigureAwait(false);
            }

            return;
        }

        if (first == '\n')
        {
            return;
        }

        throw Error("stream keyword must be followed by EOL.");
    }

    private async ValueTask SkipOneEolAsync()
    {
        int peek = await _lexer.PeekByteAsync().ConfigureAwait(false);
        if (peek == '\r')
        {
            await _lexer.ReadByteAsync().ConfigureAwait(false);
            if (await _lexer.PeekByteAsync().ConfigureAwait(false) == '\n')
            {
                await _lexer.ReadByteAsync().ConfigureAwait(false);
            }
        }
        else if (peek == '\n')
        {
            await _lexer.ReadByteAsync().ConfigureAwait(false);
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
