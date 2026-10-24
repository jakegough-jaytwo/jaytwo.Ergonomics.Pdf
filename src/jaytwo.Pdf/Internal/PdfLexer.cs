#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// Reads the tokens of ISO 32000-1 7.2 from a cursor, with a pushback for the parser's lookahead. D-55: the reading
// methods are async in shape; with useAsync false every await completes inline.
internal sealed class PdfLexer
{
    // A name, number, or keyword token is at most this many bytes. Real tokens are a few dozen bytes.
    internal const int MaxTokenBytes = 64 * 1024;

    private const int MaxCommentDepth = 256;

    private readonly PdfCursor _cursor;
    private readonly long _baseOffset;
    private readonly int _stringCap;
    private readonly bool _useAsync;
    private readonly CancellationToken _cancellationToken;
    private readonly Stack<PdfToken> _pushback = new Stack<PdfToken>();
    private byte[] _token = Array.Empty<byte>();
    private char[] _chars = Array.Empty<char>();

    public PdfLexer(PdfCursor cursor, long baseOffset, int stringCap, bool useAsync, CancellationToken cancellationToken)
    {
        _cursor = cursor;
        _baseOffset = baseOffset;
        _stringCap = stringCap;
        _useAsync = useAsync;
        _cancellationToken = cancellationToken;
    }

    public PdfCursor Cursor => _cursor;

    public async ValueTask<PdfToken> PeekAsync()
    {
        if (_pushback.Count == 0)
        {
            _pushback.Push(await ReadTokenAsync().ConfigureAwait(false));
        }

        return _pushback.Peek();
    }

    public ValueTask<PdfToken> NextAsync()
    {
        if (_pushback.Count > 0)
        {
            return new ValueTask<PdfToken>(_pushback.Pop());
        }

        return ReadTokenAsync();
    }

    public void PushBack(PdfToken token)
    {
        _pushback.Push(token);
    }

    public void ClearPushback()
    {
        _pushback.Clear();
    }

    public async ValueTask<bool> PeekKeywordAsync(string keyword)
    {
        PdfToken token = await PeekAsync().ConfigureAwait(false);
        return token.Kind == PdfTokenKind.Keyword && token.Text == keyword;
    }

    public async ValueTask SkipWhitespaceAsync()
    {
        while (true)
        {
            int b = await PeekByteAsync().ConfigureAwait(false);
            if (b < 0 || !IsWhitespace(b))
            {
                return;
            }

            await ReadByteAsync().ConfigureAwait(false);
        }
    }

    // Raw bytes, for the stream keyword's line end and classic cross-reference rows.
    public ValueTask<int> ReadByteAsync()
    {
        return _cursor.ReadByteAsync(_useAsync, _cancellationToken);
    }

    public ValueTask<int> PeekByteAsync()
    {
        return _cursor.PeekByteAsync(_useAsync, _cancellationToken);
    }

    public PdfParseException Error(string message)
    {
        return new PdfParseException(_baseOffset + _cursor.Position, message);
    }

    internal static bool IsWhitespace(int b)
    {
        return b is 0 or 9 or 10 or 12 or 13 or 32;
    }

    private async ValueTask<PdfToken> ReadTokenAsync()
    {
        int commentDepth = 0;
        int b;
        while (true)
        {
            await SkipWhitespaceAsync().ConfigureAwait(false);
            b = await ReadByteAsync().ConfigureAwait(false);
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
                c = await ReadByteAsync().ConfigureAwait(false);
            }
            while (c >= 0 && c != '\n' && c != '\r');
        }

        if (b < 0)
        {
            return new PdfToken(PdfTokenKind.End, string.Empty, Array.Empty<byte>(), 0, 0);
        }

        if (b == '[')
        {
            return new PdfToken(PdfTokenKind.ArrayStart, "[", Array.Empty<byte>(), 0, 0);
        }

        if (b == ']')
        {
            return new PdfToken(PdfTokenKind.ArrayEnd, "]", Array.Empty<byte>(), 0, 0);
        }

        if (b == '<')
        {
            if (await PeekByteAsync().ConfigureAwait(false) == '<')
            {
                await ReadByteAsync().ConfigureAwait(false);
                return new PdfToken(PdfTokenKind.DictStart, "<<", Array.Empty<byte>(), 0, 0);
            }

            return new PdfToken(PdfTokenKind.HexString, string.Empty, await ReadHexStringAsync().ConfigureAwait(false), 0, 0);
        }

        if (b == '>')
        {
            if (await PeekByteAsync().ConfigureAwait(false) == '>')
            {
                await ReadByteAsync().ConfigureAwait(false);
                return new PdfToken(PdfTokenKind.DictEnd, ">>", Array.Empty<byte>(), 0, 0);
            }

            throw Error("Unexpected '>'.");
        }

        if (b == '(')
        {
            return new PdfToken(PdfTokenKind.LiteralString, string.Empty, await ReadLiteralStringAsync().ConfigureAwait(false), 0, 0);
        }

        if (b == '/')
        {
            return new PdfToken(PdfTokenKind.Name, await ReadNameAsync().ConfigureAwait(false), Array.Empty<byte>(), 0, 0);
        }

        if (b == '+' || b == '-' || b == '.' || (b >= '0' && b <= '9'))
        {
            return await ReadNumberAsync(b).ConfigureAwait(false);
        }

        return await ReadKeywordAsync(b).ConfigureAwait(false);
    }

    private async ValueTask<PdfToken> ReadNumberAsync(int first)
    {
        long start = _cursor.Position - 1;
        bool negative = first == '-';
        bool digits = first >= '0' && first <= '9';
        if (first == '.')
        {
            return await ReadRealNumberAsync(start).ConfigureAwait(false);
        }

        long limit = negative ? long.MinValue : -long.MaxValue;
        long multiplyLimit = limit / 10;
        long integer = digits ? -(first - '0') : 0;
        bool overflow = false;
        int length = 1;
        while (true)
        {
            int b = await PeekByteAsync().ConfigureAwait(false);
            if (b == '.')
            {
                return await ReadRealNumberAsync(start).ConfigureAwait(false);
            }

            if (b < '0' || b > '9')
            {
                break;
            }

            CheckTokenLength(length, "number");
            length++;
            await ReadByteAsync().ConfigureAwait(false);
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
        return new PdfToken(PdfTokenKind.Integer, string.Empty, Array.Empty<byte>(), integer, integer);
    }

    private async ValueTask<PdfToken> ReadRealNumberAsync(long start)
    {
        _cursor.Seek(start);
        int length = 0;
        int first = await ReadByteAsync().ConfigureAwait(false);
        AppendTokenByte(ref length, first);
        bool digits = first >= '0' && first <= '9';
        bool decimalPoint = first == '.';
        while (true)
        {
            int b = await PeekByteAsync().ConfigureAwait(false);
            if ((b == '.' && !decimalPoint) || (b >= '0' && b <= '9'))
            {
                CheckTokenLength(length, "number");
                decimalPoint |= b == '.';
                digits |= b != '.';
                int next = await ReadByteAsync().ConfigureAwait(false);
                AppendTokenByte(ref length, next);
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

        if (!TryParseReal(length, out double number))
        {
            throw Error("Real number is invalid.");
        }

        // W-115: a real token's text is never read, so the lexer builds none.
        return new PdfToken(PdfTokenKind.Real, string.Empty, Array.Empty<byte>(), 0, number);
    }

    private bool TryParseReal(int length, out double number)
    {
#if NETSTANDARD2_0
        return double.TryParse(new string(_chars, 0, length), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
#else
        return double.TryParse(_chars.AsSpan(0, length), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
#endif
    }

    private async ValueTask<PdfToken> ReadKeywordAsync(int first)
    {
        int length = 0;
        AppendTokenByte(ref length, first);
        while (true)
        {
            int b = await PeekByteAsync().ConfigureAwait(false);
            if (b < 0 || IsWhitespace(b) || IsDelimiter(b))
            {
                break;
            }

            CheckTokenLength(length, "keyword");
            int next = await ReadByteAsync().ConfigureAwait(false);
            AppendTokenByte(ref length, next);
        }

        return new PdfToken(PdfTokenKind.Keyword, PdfCommonTokens.Find(_token, length) ?? Latin1(_token, length), Array.Empty<byte>(), 0, 0);
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

    private async ValueTask<string> ReadNameAsync()
    {
        int length = 0;
        while (true)
        {
            int b = await PeekByteAsync().ConfigureAwait(false);
            if (b < 0 || IsWhitespace(b) || IsDelimiter(b))
            {
                break;
            }

            CheckTokenLength(length, "name");
            int next = await ReadByteAsync().ConfigureAwait(false);
            AppendTokenByte(ref length, next);
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

    // W-115: one buffer per lexer holds the bytes of the token being read, so a token allocates only its value.
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

    private async ValueTask<byte[]> ReadLiteralStringAsync()
    {
        int length = 0;
        int depth = 1;
        while (depth > 0)
        {
            if (length > _stringCap)
            {
                throw new PdfObjectTooLargeException("Literal string exceeded the structural cap.");
            }

            int b = await ReadByteAsync().ConfigureAwait(false);
            if (b < 0)
            {
                throw Error("Literal string ended at EOF.");
            }

            if (b == '\\')
            {
                int escaped = await ReadByteAsync().ConfigureAwait(false);
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
                    if (await PeekByteAsync().ConfigureAwait(false) == '\n')
                    {
                        await ReadByteAsync().ConfigureAwait(false);
                    }

                    continue;
                }

                if (escaped >= '0' && escaped <= '7')
                {
                    int value = escaped - '0';
                    for (int i = 0; i < 2; i++)
                    {
                        int peek = await PeekByteAsync().ConfigureAwait(false);
                        if (peek < '0' || peek > '7')
                        {
                            break;
                        }

                        value = (value * 8) + (await ReadByteAsync().ConfigureAwait(false) - '0');
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
                if (await PeekByteAsync().ConfigureAwait(false) == '\n')
                {
                    await ReadByteAsync().ConfigureAwait(false);
                }

                AppendTokenByte(ref length, (byte)'\n');
                continue;
            }

            AppendTokenByte(ref length, (byte)b);
        }

        return TokenBytes(length);
    }

    private async ValueTask<byte[]> ReadHexStringAsync()
    {
        int length = 0;
        int high = -1;
        while (true)
        {
            int b = await ReadByteAsync().ConfigureAwait(false);
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

    private static bool IsDelimiter(int b)
    {
        return b is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';
    }
}
