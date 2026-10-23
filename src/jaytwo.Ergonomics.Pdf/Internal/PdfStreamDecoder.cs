#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace jaytwo.Ergonomics.Pdf.Internal;

internal static class PdfStreamDecoder
{
    public const int StructuralCap = 32 * 1024 * 1024;
    public const int XrefCap = 64 * 1024 * 1024;

    // The DecodeParms keys the decoder reads. Only these are resolved when indirect.
    private static readonly string[] DecoderParameters = { "Predictor", "Colors", "BitsPerComponent", "Columns", "EarlyChange" };

    public static byte[] Decode(byte[] data, IReadOnlyList<string> filters, IReadOnlyList<PdfObject.Dict?> parms, int maxBytes)
    {
        byte[] current = data;
        for (int i = 0; i < filters.Count; i++)
        {
            PdfObject.Dict? parm = i < parms.Count ? parms[i] : null;
            current = DecodeOne(current, filters[i], parm, maxBytes);
        }

        if (current.Length > maxBytes)
        {
            throw new PdfObjectTooLargeException($"Decoded stream is {current.Length} bytes, above the {maxBytes} byte cap.");
        }

        return current;
    }

    public static Stream OpenDecoded(Stream raw, IReadOnlyList<string> filters, IReadOnlyList<PdfObject.Dict?> parms)
    {
        foreach (string filter in filters)
        {
            if (!IsSupported(filter))
            {
                throw new PdfUnsupportedFilterException(filter);
            }
        }

        Stream current = raw;
        bool predictor = false;
        PdfObject.Dict? predictorParms = null;
        for (int i = 0; i < filters.Count; i++)
        {
            PdfObject.Dict? parm = i < parms.Count ? parms[i] : null;
            if (parm != null && IntOf(parm, "Predictor") > 1)
            {
                predictor = true;
                predictorParms = parm;
            }

            current = Wrap(current, filters[i], parm);
        }

        if (!predictor)
        {
            return current;
        }

        byte[] all = ReadAll(current, StructuralCap);
        current.Dispose();
        byte[] plain = Unpredict(all, predictorParms);
        return new MemoryStream(plain, false);
    }

    // ISO 32000-1 7.3.10 lets /Filter, /DecodeParms, their array entries, and the parameter values be indirect. Pass the
    // document's resolver to follow them within its chain, cycle, and load-depth bounds. Without one, as for a
    // cross-reference stream (ISO 32000-1 7.5.8.2 requires direct values there), a reference is refused.
    public static List<string> FiltersOf(PdfObject.Dict dict, Func<PdfObject.Ref, PdfObject>? resolve = null)
    {
        var names = new List<string>();
        PdfObject? filter = Direct(dict.Get("Filter"), resolve, "/Filter");
        if (filter == null || filter is PdfObject.NullObj)
        {
            return names;
        }

        if (filter is PdfObject.Name name)
        {
            names.Add(name.Value);
            return names;
        }

        if (filter is not PdfObject.Arr array)
        {
            throw new PdfParseException(0, "Stream /Filter is not a name or an array of names.");
        }

        foreach (PdfObject item in array.Items)
        {
            if (Direct(item, resolve, "/Filter array entry") is not PdfObject.Name itemName)
            {
                throw new PdfParseException(0, "Stream /Filter array has an entry that is not a name.");
            }

            names.Add(itemName.Value);
        }

        return names;
    }

    public static List<PdfObject.Dict?> ParmsOf(PdfObject.Dict dict, int filterCount, Func<PdfObject.Ref, PdfObject>? resolve = null)
    {
        var list = new List<PdfObject.Dict?>();
        PdfObject? parms = filterCount == 0 ? null : Direct(dict.Get("DecodeParms"), resolve, "/DecodeParms");
        if (parms == null || parms is PdfObject.NullObj)
        {
            for (int i = 0; i < filterCount; i++)
            {
                list.Add(null);
            }

            return list;
        }

        if (parms is PdfObject.Dict one)
        {
            if (filterCount != 1)
            {
                throw new PdfParseException(0, "Stream /DecodeParms is one dictionary for several filters.");
            }

            list.Add(DirectParameters(one, resolve));
            return list;
        }

        if (parms is not PdfObject.Arr array)
        {
            throw new PdfParseException(0, "Stream /DecodeParms is not a dictionary or an array.");
        }

        if (array.Items.Count != filterCount)
        {
            throw new PdfParseException(0, "Stream /DecodeParms does not have one entry for each filter.");
        }

        foreach (PdfObject item in array.Items)
        {
            PdfObject? value = Direct(item, resolve, "/DecodeParms array entry");
            if (value is PdfObject.Dict entry)
            {
                list.Add(DirectParameters(entry, resolve));
            }
            else if (value is PdfObject.NullObj)
            {
                list.Add(null);
            }
            else
            {
                throw new PdfParseException(0, "Stream /DecodeParms array has an entry that is not a dictionary or null.");
            }
        }

        return list;
    }

    private static PdfObject? Direct(PdfObject? value, Func<PdfObject.Ref, PdfObject>? resolve, string what)
    {
        if (value is not PdfObject.Ref reference)
        {
            return value;
        }

        if (resolve == null)
        {
            throw new PdfParseException(0, "Stream " + what + " is an indirect reference where a direct object is required.");
        }

        return resolve(reference);
    }

    // Returns the dictionary itself when the parameters the decoder reads are direct. Otherwise returns a copy with those
    // values resolved, so the document's cached object keeps its references.
    private static PdfObject.Dict DirectParameters(PdfObject.Dict parms, Func<PdfObject.Ref, PdfObject>? resolve)
    {
        bool indirect = false;
        foreach (string key in DecoderParameters)
        {
            if (parms.Get(key) is PdfObject.Ref)
            {
                indirect = true;
                break;
            }
        }

        if (!indirect)
        {
            return parms;
        }

        var copy = new PdfObject.Dict();
        foreach ((string key, PdfObject value) in parms.Items)
        {
            bool read = Array.IndexOf(DecoderParameters, key) >= 0;
            copy.Append(key, read ? Direct(value, resolve, "/DecodeParms /" + key)! : value);
        }

        return copy;
    }

    private static bool IsSupported(string filter)
    {
        string canonical = Canonical(filter);
        return canonical == "FlateDecode"
            || canonical == "ASCIIHexDecode"
            || canonical == "ASCII85Decode"
            || canonical == "LZWDecode"
            || canonical == "RunLengthDecode";
    }

    private static string Canonical(string filter)
    {
        if (filter == "LZW")
        {
            return "LZWDecode";
        }

        if (filter == "AHx")
        {
            return "ASCIIHexDecode";
        }

        if (filter == "A85")
        {
            return "ASCII85Decode";
        }

        if (filter == "Fl")
        {
            return "FlateDecode";
        }

        if (filter == "RL")
        {
            return "RunLengthDecode";
        }

        return filter;
    }

    private static Stream Wrap(Stream source, string filter, PdfObject.Dict? parms)
    {
        string canonical = Canonical(filter);
        if (canonical == "FlateDecode")
        {
            return OpenInflate(source, leaveOpen: false);
        }

        if (canonical == "LZWDecode")
        {
            return new LzwDecodeStream(source, EarlyChangeOf(parms));
        }

        if (canonical == "ASCIIHexDecode")
        {
            return new AsciiHexDecodeStream(source);
        }

        if (canonical == "ASCII85Decode")
        {
            return new Ascii85DecodeStream(source);
        }

        if (canonical == "RunLengthDecode")
        {
            return new RunLengthDecodeStream(source);
        }

        throw new PdfUnsupportedFilterException(filter);
    }

    private static byte[] DecodeOne(byte[] data, string filter, PdfObject.Dict? parms, int maxBytes)
    {
        string canonical = Canonical(filter);
        byte[] decoded;
        if (canonical == "FlateDecode")
        {
            decoded = Inflate(data, maxBytes);
        }
        else if (canonical == "LZWDecode")
        {
            decoded = Lzw(data, EarlyChangeOf(parms), maxBytes);
        }
        else if (canonical == "ASCIIHexDecode")
        {
            decoded = AsciiHex(data);
        }
        else if (canonical == "ASCII85Decode")
        {
            decoded = Ascii85(data);
        }
        else if (canonical == "RunLengthDecode")
        {
            decoded = RunLength(data, maxBytes);
        }
        else
        {
            throw new PdfUnsupportedFilterException(filter);
        }

        if (parms != null && IntOf(parms, "Predictor") > 1)
        {
            decoded = Unpredict(decoded, parms);
        }

        return decoded;
    }

    private static byte[] Inflate(byte[] data, int maxBytes)
    {
        using var input = new MemoryStream(data, false);
        using Stream zlib = OpenInflate(input, leaveOpen: true);
        return ReadAll(zlib, maxBytes);
    }

    private static byte[] Lzw(byte[] data, int earlyChange, int maxBytes)
    {
        using var input = new MemoryStream(data, false);
        using Stream lzw = new LzwDecodeStream(input, earlyChange);
        return ReadAll(lzw, maxBytes);
    }

    private static byte[] RunLength(byte[] data, int maxBytes)
    {
        using var input = new MemoryStream(data, false);
        using Stream run = new RunLengthDecodeStream(input);
        return ReadAll(run, maxBytes);
    }

    private static int EarlyChangeOf(PdfObject.Dict? parms)
    {
        if (parms?.Get("EarlyChange") is not PdfObject.Num number)
        {
            return 1;
        }

        int value = number.IsInteger ? (int)number.LongValue : (int)number.DoubleValue;
        return value == 0 ? 0 : 1;
    }

    private static Stream OpenInflate(Stream source, bool leaveOpen)
    {
        return new ZlibInflateStream(source, leaveOpen);
    }

    private static byte[] ReadAll(Stream stream, int maxBytes)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        while (true)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new PdfObjectTooLargeException($"Decoded stream exceeded {maxBytes} bytes.");
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    public static byte[] Unpredict(byte[] data, PdfObject.Dict? parms)
    {
        int predictor = IntOf(parms, "Predictor");
        if (predictor <= 1)
        {
            return data;
        }

        int columns = IntOf(parms, "Columns");
        if (columns <= 0)
        {
            columns = 1;
        }

        int colors = IntOf(parms, "Colors");
        if (colors <= 0)
        {
            colors = 1;
        }

        int bpc = IntOf(parms, "BitsPerComponent");
        if (bpc <= 0)
        {
            bpc = 8;
        }

        long samples = (long)columns * colors;
        long rowBytesLong = samples > int.MaxValue ? long.MaxValue : ((samples * bpc) + 7) / 8;
        if (rowBytesLong >= int.MaxValue)
        {
            throw new PdfParseException(0, "Predictor row width is too large.");
        }

        int rowBytes = (int)rowBytesLong;
        if (predictor == 2)
        {
            return UnpredictTiff(data, rowBytes);
        }

        if (predictor >= 10)
        {
            return UnpredictPng(data, rowBytes);
        }

        throw new PdfUnsupportedFilterException("Predictor" + predictor.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static byte[] UnpredictTiff(byte[] data, int rowBytes)
    {
        if (rowBytes <= 0)
        {
            throw new PdfParseException(0, "Predictor row width is zero.");
        }

        var output = new byte[data.Length];
        Buffer.BlockCopy(data, 0, output, 0, data.Length);
        for (int row = 0; row < output.Length; row += rowBytes)
        {
            int end = Math.Min(output.Length, row + rowBytes);
            for (int i = row + 1; i < end; i++)
            {
                output[i] = (byte)(output[i] + output[i - 1]);
            }
        }

        return output;
    }

    private static byte[] UnpredictPng(byte[] data, int rowBytes)
    {
        if (rowBytes <= 0)
        {
            throw new PdfParseException(0, "Predictor row width is zero.");
        }

        int stride = rowBytes + 1;
        if (data.Length % stride != 0)
        {
            throw new PdfParseException(0, "Predictor rows are not aligned.");
        }

        int rows = data.Length / stride;
        if (rows == 0)
        {
            return Array.Empty<byte>();
        }

        var output = new byte[rows * rowBytes];
        var previous = new byte[rowBytes];
        for (int row = 0; row < rows; row++)
        {
            int source = row * stride;
            byte filter = data[source];
            int dest = row * rowBytes;
            for (int i = 0; i < rowBytes; i++)
            {
                byte raw = data[source + 1 + i];
                byte left = i > 0 ? output[dest + i - 1] : (byte)0;
                byte up = previous[i];
                byte upLeft = i > 0 ? previous[i - 1] : (byte)0;
                output[dest + i] = (byte)(raw + Predict(filter, left, up, upLeft));
            }

            Buffer.BlockCopy(output, dest, previous, 0, rowBytes);
        }

        return output;
    }

    private static int Predict(byte filter, byte left, byte up, byte upLeft)
    {
        switch (filter)
        {
            case 0:
                return 0;
            case 1:
                return left;
            case 2:
                return up;
            case 3:
                return (left + up) / 2;
            case 4:
                return Paeth(left, up, upLeft);
            default:
                throw new PdfParseException(0, "Unknown PNG predictor " + filter.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        }
    }

    private static int Paeth(byte left, byte up, byte upLeft)
    {
        int p = left + up - upLeft;
        int pa = Math.Abs(p - left);
        int pb = Math.Abs(p - up);
        int pc = Math.Abs(p - upLeft);
        if (pa <= pb && pa <= pc)
        {
            return left;
        }

        if (pb <= pc)
        {
            return up;
        }

        return upLeft;
    }

    private static int IntOf(PdfObject.Dict? dict, string key)
    {
        if (dict?.Get(key) is PdfObject.Num number)
        {
            return number.IsInteger ? (int)number.LongValue : (int)number.DoubleValue;
        }

        return 0;
    }

    private static byte[] AsciiHex(byte[] data)
    {
        using var output = new MemoryStream();
        int high = -1;
        foreach (byte b in data)
        {
            if (b == (byte)'>')
            {
                break;
            }

            if (IsWhite(b))
            {
                continue;
            }

            int nibble = HexNibble(b);
            if (high < 0)
            {
                high = nibble;
            }
            else
            {
                output.WriteByte((byte)((high << 4) | nibble));
                high = -1;
            }
        }

        if (high >= 0)
        {
            output.WriteByte((byte)(high << 4));
        }

        return output.ToArray();
    }

    private static byte[] Ascii85(byte[] data)
    {
        using var output = new MemoryStream();
        uint value = 0;
        int count = 0;
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            if (IsWhite(b))
            {
                continue;
            }

            if (b == (byte)'~')
            {
                break;
            }

            if (b == (byte)'z' && count == 0)
            {
                output.Write(new byte[4], 0, 4);
                continue;
            }

            if (b < (byte)'!' || b > (byte)'u')
            {
                throw new PdfParseException(0, "Invalid ASCII85 character.");
            }

            value = (value * 85) + (uint)(b - (byte)'!');
            count++;
            if (count == 5)
            {
                WriteGroup(output, value, 4);
                value = 0;
                count = 0;
            }
        }

        if (count > 1)
        {
            for (int i = count; i < 5; i++)
            {
                value = (value * 85) + 84;
            }

            WriteGroup(output, value, count - 1);
        }

        return output.ToArray();
    }

    private static void WriteGroup(Stream output, uint value, int bytes)
    {
        var group = new byte[4];
        group[0] = (byte)(value >> 24);
        group[1] = (byte)(value >> 16);
        group[2] = (byte)(value >> 8);
        group[3] = (byte)value;
        output.Write(group, 0, bytes);
    }

    private static bool IsWhite(byte b)
    {
        return b == 0 || b == 9 || b == 10 || b == 12 || b == 13 || b == 32;
    }

    private static int HexNibble(byte b)
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

        throw new PdfParseException(0, "Invalid ASCII hex character.");
    }

    private sealed class ZlibInflateStream : Stream
    {
        private readonly Stream _source;
        private readonly bool _leaveOpen;
        private ZlibPayloadStream? _payload;
        private DeflateStream? _deflate;
        private uint _sum1 = 1;
        private uint _sum2;
        private bool _verified;

        public ZlibInflateStream(Stream source, bool leaveOpen)
        {
            _source = source;
            _leaveOpen = leaveOpen;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            EnsureStarted();
            int read;
            try
            {
                read = _deflate!.Read(buffer, offset, count);
            }
            catch (InvalidDataException ex)
            {
                throw new PdfParseException(0, "The Flate data is invalid: " + ex.Message);
            }

            for (int i = 0; i < read; i++)
            {
                Add(buffer[offset + i]);
            }

            if (read == 0 && !_verified)
            {
                _payload!.Drain();
                _verified = true;
                if (!_payload.ChecksumMatches((_sum2 << 16) | _sum1))
                {
                    throw new PdfParseException(0, "The zlib Adler-32 checksum at the end of the stream data does not match the decoded bytes.");
                }
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _deflate?.Dispose();
                if (!_leaveOpen)
                {
                    _source.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void EnsureStarted()
        {
            if (_deflate != null)
            {
                return;
            }

            ReadHeader(_source);
            _payload = new ZlibPayloadStream(_source);
            _deflate = new DeflateStream(_payload, CompressionMode.Decompress, leaveOpen: true);
        }

        private void Add(byte value)
        {
            _sum1 = (_sum1 + value) % 65521;
            _sum2 = (_sum2 + _sum1) % 65521;
        }

        private static void ReadHeader(Stream source)
        {
            int cmf = source.ReadByte();
            int flg = source.ReadByte();
            if (cmf < 0 || flg < 0)
            {
                throw new PdfParseException(0, "The zlib header is truncated.");
            }

            int header = (cmf * 256) + flg;
            if ((cmf & 0x0F) != 8 || (cmf >> 4) > 7 || header % 31 != 0)
            {
                throw new PdfParseException(0, "The zlib header is invalid.");
            }

            if ((flg & 0x20) != 0)
            {
                throw new PdfParseException(0, "A zlib preset dictionary is not supported.");
            }
        }
    }

    // Passes the deflate data on and holds back the last four bytes of the stream data, which RFC 1950 makes the
    // Adler-32. It also keeps the last six bytes, so that the checksum can be found in front of an end-of-line
    // marker that the writer counted in /Length (a tolerated writer quirk; ISO 32000-1 7.3.8.1 excludes that
    // marker from /Length). Bytes past the end of the deflate data are ignored by the inflater.
    private sealed class ZlibPayloadStream : Stream
    {
        private const int TailLength = 6;
        private readonly Stream _source;
        private readonly byte[] _hold = new byte[4];
        private readonly byte[] _swap = new byte[4];
        private readonly byte[] _incoming;
        private readonly byte[] _tail = new byte[TailLength];
        private int _tailCount;
        private bool _primed;
        private bool _exhausted;

        // W-115: a seekable source with fewer than 8 KiB left gets a buffer of what is left, and at least 64 bytes. A read
        // asks for no more than the source still holds, so each read returns the bytes it would with 8 KiB.
        public ZlibPayloadStream(Stream source)
        {
            _source = source;
            _incoming = new byte[source.CanSeek ? (int)Math.Max(64, Math.Min(8192, source.Length - source.Position)) : 8192];
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void Drain()
        {
            if (_exhausted)
            {
                return;
            }

            var discard = new byte[256];
            while (Read(discard, 0, discard.Length) > 0)
            {
            }
        }

        // Call after Drain. The stored checksum is the last four bytes of the stream data, or the four bytes in
        // front of a final CR, LF, or CR LF.
        public bool ChecksumMatches(uint actual)
        {
            if (StoredChecksum(0) == actual)
            {
                return true;
            }

            byte last = _tail[TailLength - 1];
            if (_tailCount >= 5 && (last == (byte)'\n' || last == (byte)'\r') && StoredChecksum(1) == actual)
            {
                return true;
            }

            return _tailCount >= 6 && _tail[TailLength - 2] == (byte)'\r' && last == (byte)'\n' && StoredChecksum(2) == actual;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0 || _exhausted)
            {
                return 0;
            }

            if (!_primed)
            {
                int filled = 0;
                while (filled < 4)
                {
                    int header = _source.Read(_hold, filled, 4 - filled);
                    if (header == 0)
                    {
                        throw new PdfParseException(0, "The zlib stream is truncated.");
                    }

                    filled += header;
                }

                Remember(_hold, 4);
                _primed = true;
            }

            int incoming = _source.Read(_incoming, 0, Math.Min(count, _incoming.Length));
            if (incoming == 0)
            {
                _exhausted = true;
                return 0;
            }

            Remember(_incoming, incoming);
            if (incoming < 4)
            {
                Buffer.BlockCopy(_hold, 0, buffer, offset, incoming);
                Buffer.BlockCopy(_hold, incoming, _swap, 0, 4 - incoming);
                Buffer.BlockCopy(_incoming, 0, _swap, 4 - incoming, incoming);
                Buffer.BlockCopy(_swap, 0, _hold, 0, 4);
                return incoming;
            }

            Buffer.BlockCopy(_hold, 0, buffer, offset, 4);
            Buffer.BlockCopy(_incoming, 0, buffer, offset + 4, incoming - 4);
            Buffer.BlockCopy(_incoming, incoming - 4, _hold, 0, 4);
            return incoming;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private void Remember(byte[] data, int count)
        {
            if (count >= TailLength)
            {
                Buffer.BlockCopy(data, count - TailLength, _tail, 0, TailLength);
            }
            else
            {
                Buffer.BlockCopy(_tail, count, _tail, 0, TailLength - count);
                Buffer.BlockCopy(data, 0, _tail, TailLength - count, count);
            }

            _tailCount = Math.Min(TailLength, _tailCount + count);
        }

        // The big-endian checksum that ends `trailing` bytes before the end of the stream data.
        private uint StoredChecksum(int trailing)
        {
            int start = TailLength - trailing - 4;
            return ((uint)_tail[start] << 24) | ((uint)_tail[start + 1] << 16) | ((uint)_tail[start + 2] << 8) | _tail[start + 3];
        }
    }

    private sealed class AsciiHexDecodeStream : Stream
    {
        private readonly Stream _source;
        private int _high = -2;

        public AsciiHexDecodeStream(Stream source)
        {
            _source = source;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int written = 0;
            while (written < count)
            {
                int b = NextByte();
                if (b < 0)
                {
                    break;
                }

                buffer[offset + written] = (byte)b;
                written++;
            }

            return written;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _source.Dispose();
            }

            base.Dispose(disposing);
        }

        private int NextByte()
        {
            if (_high == -1)
            {
                return -1;
            }

            while (true)
            {
                int b = _source.ReadByte();
                if (b < 0 || b == '>')
                {
                    if (_high >= 0)
                    {
                        int value = _high << 4;
                        _high = -1;
                        return value;
                    }

                    _high = -1;
                    return -1;
                }

                if (IsWhite((byte)b))
                {
                    continue;
                }

                int nibble = HexNibble((byte)b);
                if (_high < 0)
                {
                    _high = nibble;
                    continue;
                }

                int combined = (_high << 4) | nibble;
                _high = -2;
                return combined;
            }
        }
    }

    private sealed class Ascii85DecodeStream : Stream
    {
        private readonly Stream _source;
        private readonly byte[] _pending = new byte[4];
        private int _pendingIndex;
        private int _pendingCount;
        private bool _done;

        public Ascii85DecodeStream(Stream source)
        {
            _source = source;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int written = 0;
            while (written < count)
            {
                if (_pendingIndex >= _pendingCount)
                {
                    if (!Fill())
                    {
                        break;
                    }
                }

                buffer[offset + written] = _pending[_pendingIndex++];
                written++;
            }

            return written;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _source.Dispose();
            }

            base.Dispose(disposing);
        }

        private bool Fill()
        {
            if (_done)
            {
                return false;
            }

            uint value = 0;
            int count = 0;
            while (true)
            {
                int b = _source.ReadByte();
                if (b < 0 || b == '~')
                {
                    _done = true;
                    if (count > 1)
                    {
                        for (int i = count; i < 5; i++)
                        {
                            value = (value * 85) + 84;
                        }

                        Store(value, count - 1);
                        return true;
                    }

                    return false;
                }

                if (IsWhite((byte)b))
                {
                    continue;
                }

                if (b == 'z' && count == 0)
                {
                    Store(0, 4);
                    return true;
                }

                if (b < '!' || b > 'u')
                {
                    throw new PdfParseException(0, "Invalid ASCII85 character.");
                }

                value = (value * 85) + (uint)(b - '!');
                count++;
                if (count == 5)
                {
                    Store(value, 4);
                    return true;
                }
            }
        }

        private void Store(uint value, int bytes)
        {
            _pending[0] = (byte)(value >> 24);
            _pending[1] = (byte)(value >> 16);
            _pending[2] = (byte)(value >> 8);
            _pending[3] = (byte)value;
            _pendingIndex = 0;
            _pendingCount = bytes;
        }
    }

    private sealed class LzwDecodeStream : Stream
    {
        private const int ClearCode = 256;
        private const int EodCode = 257;
        private const int TableSize = 4096;

        private readonly Stream _source;
        private readonly int _earlyChange;
        private readonly ushort[] _prefix = new ushort[TableSize];
        private readonly byte[] _suffix = new byte[TableSize];
        private readonly byte[] _stack = new byte[TableSize];
        private uint _accumulator;
        private int _bitCount;
        private int _codeLength = 9;
        private int _nextCode = 258;
        private int _previous = -1;
        private int _cursor;
        private bool _ended;

        public LzwDecodeStream(Stream source, int earlyChange)
        {
            _source = source;
            _earlyChange = earlyChange == 0 ? 0 : 1;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int written = 0;
            while (written < count)
            {
                if (_cursor <= 0)
                {
                    if (_ended || !ReadNextCode())
                    {
                        break;
                    }
                }

                buffer[offset + written] = _stack[--_cursor];
                written++;
            }

            return written;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _source.Dispose();
            }

            base.Dispose(disposing);
        }

        private bool ReadNextCode()
        {
            while (true)
            {
                int code = ReadCode();
                if (code < 0 || code == EodCode)
                {
                    _ended = true;
                    return false;
                }

                if (code == ClearCode)
                {
                    _codeLength = 9;
                    _nextCode = 258;
                    _previous = -1;
                    continue;
                }

                if (code > _nextCode || code >= TableSize)
                {
                    throw new PdfParseException(0, "LZW code is outside the table.");
                }

                int length;
                byte first;
                if (code == _nextCode)
                {
                    if (_previous < 0)
                    {
                        throw new PdfParseException(0, "LZW code is outside the table.");
                    }

                    length = PushChain(_previous);
                    first = _stack[length - 1];
                    Array.Copy(_stack, 0, _stack, 1, length);
                    _stack[0] = first;
                    length++;
                }
                else
                {
                    length = PushChain(code);
                    first = _stack[length - 1];
                }

                if (_previous >= 0 && _nextCode < TableSize)
                {
                    _prefix[_nextCode] = (ushort)_previous;
                    _suffix[_nextCode] = first;
                    _nextCode++;
                }

                _previous = code;
                _cursor = length;
                _codeLength = CodeWidth(_nextCode, _earlyChange);
                return true;
            }
        }

        private int PushChain(int code)
        {
            int count = 0;
            int current = code;
            while (current >= 256)
            {
                if (count >= TableSize || current >= TableSize)
                {
                    throw new PdfParseException(0, "LZW code is outside the table.");
                }

                _stack[count++] = _suffix[current];
                current = _prefix[current];
            }

            _stack[count++] = (byte)current;
            return count;
        }

        private int ReadCode()
        {
            while (_bitCount < _codeLength)
            {
                int value = _source.ReadByte();
                if (value < 0)
                {
                    if (_bitCount == 0)
                    {
                        return -1;
                    }

                    throw new PdfParseException(0, "LZW stream is truncated.");
                }

                _accumulator = (_accumulator << 8) | (uint)value;
                _bitCount += 8;
            }

            _bitCount -= _codeLength;
            int code = (int)((_accumulator >> _bitCount) & ((1u << _codeLength) - 1));
            if (_bitCount == 0)
            {
                _accumulator = 0;
            }
            else
            {
                _accumulator &= (1u << _bitCount) - 1;
            }

            return code;
        }

        private static int CodeWidth(int tableSize, int earlyChange)
        {
            int marker = tableSize + earlyChange;
            if (marker >= 2048)
            {
                return 12;
            }

            if (marker >= 1024)
            {
                return 11;
            }

            if (marker >= 512)
            {
                return 10;
            }

            return 9;
        }
    }

    private sealed class RunLengthDecodeStream : Stream
    {
        private readonly Stream _source;
        private int _remaining;
        private int _repeat = -1;
        private bool _ended;

        public RunLengthDecodeStream(Stream source)
        {
            _source = source;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int written = 0;
            while (written < count)
            {
                if (_remaining <= 0)
                {
                    if (_ended || !StartRun())
                    {
                        break;
                    }
                }

                if (_repeat < 0)
                {
                    int value = _source.ReadByte();
                    if (value < 0)
                    {
                        throw new PdfParseException(0, "RunLength stream is truncated.");
                    }

                    buffer[offset + written] = (byte)value;
                }
                else
                {
                    buffer[offset + written] = (byte)_repeat;
                }

                _remaining--;
                written++;
            }

            return written;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _source.Dispose();
            }

            base.Dispose(disposing);
        }

        private bool StartRun()
        {
            int length = _source.ReadByte();
            if (length < 0 || length == 128)
            {
                _ended = true;
                return false;
            }

            if (length < 128)
            {
                _repeat = -1;
                _remaining = length + 1;
                return true;
            }

            int value = _source.ReadByte();
            if (value < 0)
            {
                throw new PdfParseException(0, "RunLength stream is truncated.");
            }

            _repeat = value;
            _remaining = 257 - length;
            return true;
        }
    }
}
