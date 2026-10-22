using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace jaytwo.Ergonomics.Pdf.Internal;

internal static class PdfTextCodec
{
    // The longest escape of one literal-string byte: a backslash and three octal digits.
    public const int MaxEscapedLength = 4;

    private static readonly ushort[] PdfDoc = BuildPdfDoc();
    private static readonly ushort[] WinAnsi = BuildWinAnsi();
    private static readonly Dictionary<int, byte> PdfDocReverse = BuildPdfDocReverse();
    private static readonly Dictionary<int, byte> WinAnsiReverse = BuildWinAnsiReverse();

    public static string Decode(byte[] raw)
    {
        return TryDecode(raw, out string text, out string error) ? text : throw new PdfParseException(0, error);
    }

    // A text string (ISO 32000-1 7.9.2.2) decodes; any other byte string returns false with the reason.
    public static bool TryDecode(byte[] raw, out string text, out string error)
    {
        text = string.Empty;
        error = string.Empty;
        if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF)
        {
            if (((raw.Length - 2) & 1) != 0)
            {
                error = "UTF-16BE string has an odd length.";
                return false;
            }

            text = Encoding.BigEndianUnicode.GetString(raw, 2, raw.Length - 2);
            return true;
        }

        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
        {
            text = Encoding.UTF8.GetString(raw, 3, raw.Length - 3);
            return true;
        }

        var chars = new char[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            ushort mapped = PdfDoc[raw[i]];
            if (mapped == 0xFFFF)
            {
                error = $"Undefined PDFDocEncoding byte 0x{raw[i]:X2}.";
                return false;
            }

            chars[i] = (char)mapped;
        }

        text = new string(chars);
        return true;
    }

    public static PdfObject.Str Encode(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (IsAscii(value))
        {
            return new PdfObject.Str(Encoding.ASCII.GetBytes(value), false);
        }

        return new PdfObject.Str(EncodeUtf16Be(value), true);
    }

    // UTF-16BE with its byte-order mark: the text-string encoding that holds any value (ISO 32000-1 7.9.2.2).
    public static byte[] EncodeUtf16Be(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        byte[] be = Encoding.BigEndianUnicode.GetBytes(value);
        var raw = new byte[be.Length + 2];
        raw[0] = 0xFE;
        raw[1] = 0xFF;
        Buffer.BlockCopy(be, 0, raw, 2, be.Length);
        return raw;
    }

    // PDFDocEncoding when every character has a code and the bytes cannot be read as a byte-order mark; otherwise UTF-16BE.
    public static byte[] EncodePdfDocOrUtf16Be(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var bytes = new byte[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            if (!PdfDocReverse.TryGetValue(value[i], out byte encoded))
            {
                return EncodeUtf16Be(value);
            }

            bytes[i] = encoded;
        }

        bool bom = (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) || (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        return bom ? EncodeUtf16Be(value) : bytes;
    }

    public static byte[] EncodePdfDoc(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var bytes = new byte[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            if (!PdfDocReverse.TryGetValue(value[i], out byte encoded))
            {
                throw new PdfEncryptedException("The password cannot be encoded for revision 4 encryption.");
            }

            bytes[i] = encoded;
        }

        return bytes;
    }

    public static bool CanEncodeWinAnsi(string value)
    {
        foreach (char ch in value)
        {
            if (ch == '\n' || ch == '\r')
            {
                continue;
            }

            if (!WinAnsiReverse.ContainsKey(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static byte[] EncodeWinAnsi(string value)
    {
        var bytes = new List<byte>(value.Length);
        foreach (char ch in value)
        {
            if (ch == '\n' || ch == '\r')
            {
                continue;
            }

            bytes.Add(WinAnsiReverse[ch]);
        }

        return bytes.ToArray();
    }

    public static string EscapeLiteral(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length + 8);
        var escaped = new byte[MaxEscapedLength];
        foreach (byte b in bytes)
        {
            int length = EscapeLiteral(b, escaped, 0);
            for (int i = 0; i < length; i++)
            {
                builder.Append((char)escaped[i]);
            }
        }

        return builder.ToString();
    }

    // Writes one byte of a literal string body, escaped, at destination[offset], and returns how many bytes it took:
    // at most MaxEscapedLength.
    public static int EscapeLiteral(byte b, byte[] destination, int offset)
    {
        if (b == (byte)'(' || b == (byte)')' || b == (byte)'\\')
        {
            destination[offset] = (byte)'\\';
            destination[offset + 1] = b;
            return 2;
        }

        if (b == (byte)'\n' || b == (byte)'\r' || b == (byte)'\t')
        {
            destination[offset] = (byte)'\\';
            destination[offset + 1] = b == (byte)'\n' ? (byte)'n' : b == (byte)'\r' ? (byte)'r' : (byte)'t';
            return 2;
        }

        if (b < 32 || b > 126)
        {
            destination[offset] = (byte)'\\';
            destination[offset + 1] = (byte)('0' + (b >> 6));
            destination[offset + 2] = (byte)('0' + ((b >> 3) & 7));
            destination[offset + 3] = (byte)('0' + (b & 7));
            return 4;
        }

        destination[offset] = b;
        return 1;
    }

    public static DateTimeOffset ParseDate(string text)
    {
        if (text.StartsWith("D:", StringComparison.Ordinal))
        {
            text = text.Substring(2);
        }

        if (text.Length < 4)
        {
            throw new PdfParseException(0, "PDF date is too short.");
        }

        int year = ReadDigits(text, 0, 4);
        int month = text.Length >= 6 ? ReadDigits(text, 4, 2) : 1;
        int day = text.Length >= 8 ? ReadDigits(text, 6, 2) : 1;
        int hour = text.Length >= 10 ? ReadDigits(text, 8, 2) : 0;
        int minute = text.Length >= 12 ? ReadDigits(text, 10, 2) : 0;
        int second = text.Length >= 14 ? ReadDigits(text, 12, 2) : 0;
        var offset = TimeSpan.Zero;
        if (text.Length > 14)
        {
            char zone = text[14];
            if (zone == 'Z')
            {
                offset = TimeSpan.Zero;
            }
            else if (zone == '+' || zone == '-')
            {
                int zh = text.Length >= 17 ? ReadDigits(text, 15, 2) : 0;
                int zm = 0;
                int mark = text.IndexOf('\'', 15);
                if (mark >= 0 && mark + 3 <= text.Length)
                {
                    zm = ReadDigits(text, mark + 1, 2);
                }

                offset = new TimeSpan(zh, zm, 0);
                if (zone == '-')
                {
                    offset = -offset;
                }
            }
        }

        try
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new PdfParseException(0, "PDF date is out of range.");
        }
    }

    public static string FormatDate(DateTimeOffset value)
    {
        string body = value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        if (value.Offset == TimeSpan.Zero)
        {
            return "D:" + body + "Z";
        }

        var absolute = value.Offset.Duration();
        string sign = value.Offset < TimeSpan.Zero ? "-" : "+";
        return "D:" + body + sign + absolute.Hours.ToString("00", CultureInfo.InvariantCulture) + "'" + absolute.Minutes.ToString("00", CultureInfo.InvariantCulture) + "'";
    }

    public static string FormatXmpDate(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    public static string ToUpperHex(byte[] data)
    {
        var chars = new char[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            byte value = data[i];
            chars[i * 2] = HexDigit(value >> 4);
            chars[(i * 2) + 1] = HexDigit(value & 0xF);
        }

        return new string(chars);
    }

    // Writes the two upper-case hex digits of value at destination[offset].
    public static void ToUpperHex(byte value, byte[] destination, int offset)
    {
        destination[offset] = (byte)HexDigit(value >> 4);
        destination[offset + 1] = (byte)HexDigit(value & 0xF);
    }

    private static bool IsAscii(string value)
    {
        foreach (char ch in value)
        {
            if (ch < 32 || ch > 126)
            {
                return false;
            }
        }

        return true;
    }

    private static int ReadDigits(string text, int index, int count)
    {
        if (index + count > text.Length)
        {
            throw new PdfParseException(0, "PDF date is truncated.");
        }

        int value = 0;
        for (int i = 0; i < count; i++)
        {
            char ch = text[index + i];
            if (ch < '0' || ch > '9')
            {
                throw new PdfParseException(0, "PDF date contains a non-digit.");
            }

            value = (value * 10) + (ch - '0');
        }

        return value;
    }

    private static ushort[] BuildPdfDoc()
    {
        var map = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            map[i] = (ushort)i;
        }

        for (int i = 0; i <= 0x17; i++)
        {
            map[i] = 0xFFFF;
        }

        map[0x09] = 0x09;
        map[0x0A] = 0x0A;
        map[0x0D] = 0x0D;
        map[0x18] = 0x02D8;
        map[0x19] = 0x02C7;
        map[0x1A] = 0x02C6;
        map[0x1B] = 0x02D9;
        map[0x1C] = 0x02DD;
        map[0x1D] = 0x02DB;
        map[0x1E] = 0x02DA;
        map[0x1F] = 0x02DC;
        map[0x7F] = 0xFFFF;
        map[0x80] = 0x2022;
        map[0x81] = 0x2020;
        map[0x82] = 0x2021;
        map[0x83] = 0x2026;
        map[0x84] = 0x2014;
        map[0x85] = 0x2013;
        map[0x86] = 0x0192;
        map[0x87] = 0x2044;
        map[0x88] = 0x2039;
        map[0x89] = 0x203A;
        map[0x8A] = 0x2212;
        map[0x8B] = 0x2030;
        map[0x8C] = 0x201E;
        map[0x8D] = 0x201C;
        map[0x8E] = 0x201D;
        map[0x8F] = 0x2018;
        map[0x90] = 0x2019;
        map[0x91] = 0x201A;
        map[0x92] = 0x2122;
        map[0x93] = 0xFB01;
        map[0x94] = 0xFB02;
        map[0x95] = 0x0141;
        map[0x96] = 0x0152;
        map[0x97] = 0x0160;
        map[0x98] = 0x0178;
        map[0x99] = 0x017D;
        map[0x9A] = 0x0131;
        map[0x9B] = 0x0142;
        map[0x9C] = 0x0153;
        map[0x9D] = 0x0161;
        map[0x9E] = 0x017E;
        map[0x9F] = 0xFFFF;
        map[0xA0] = 0x20AC;
        map[0xAD] = 0xFFFF;
        return map;
    }

    private static ushort[] BuildWinAnsi()
    {
        var map = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            map[i] = (ushort)i;
        }

        map[0x80] = 0x20AC;
        map[0x81] = 0xFFFF;
        map[0x82] = 0x201A;
        map[0x83] = 0x0192;
        map[0x84] = 0x201E;
        map[0x85] = 0x2026;
        map[0x86] = 0x2020;
        map[0x87] = 0x2021;
        map[0x88] = 0x02C6;
        map[0x89] = 0x2030;
        map[0x8A] = 0x0160;
        map[0x8B] = 0x2039;
        map[0x8C] = 0x0152;
        map[0x8D] = 0xFFFF;
        map[0x8E] = 0x017D;
        map[0x8F] = 0xFFFF;
        map[0x90] = 0xFFFF;
        map[0x91] = 0x2018;
        map[0x92] = 0x2019;
        map[0x93] = 0x201C;
        map[0x94] = 0x201D;
        map[0x95] = 0x2022;
        map[0x96] = 0x2013;
        map[0x97] = 0x2014;
        map[0x98] = 0x02DC;
        map[0x99] = 0x2122;
        map[0x9A] = 0x0161;
        map[0x9B] = 0x203A;
        map[0x9C] = 0x0153;
        map[0x9D] = 0xFFFF;
        map[0x9E] = 0x017E;
        map[0x9F] = 0x0178;
        return map;
    }

    private static Dictionary<int, byte> BuildPdfDocReverse()
    {
        var map = new Dictionary<int, byte>();
        for (int i = 0; i < 256; i++)
        {
            ushort unicode = PdfDoc[i];
            if (unicode != 0xFFFF && !map.ContainsKey(unicode))
            {
                map[unicode] = (byte)i;
            }
        }

        return map;
    }

    private static Dictionary<int, byte> BuildWinAnsiReverse()
    {
        var map = new Dictionary<int, byte>();
        for (int i = 32; i < 256; i++)
        {
            ushort unicode = WinAnsi[i];
            if (unicode != 0xFFFF && !map.ContainsKey(unicode))
            {
                map[unicode] = (byte)i;
            }
        }

        map['\t'] = (byte)'\t';
        return map;
    }

    private static char HexDigit(int value)
    {
        return (char)(value < 10 ? '0' + value : 'A' + (value - 10));
    }
}
