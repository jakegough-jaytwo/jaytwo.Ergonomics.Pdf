#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace jaytwo.Ergonomics.Pdf.Internal;

internal sealed class PdfSerializer
{
    private readonly Stream _output;
    private readonly Action<PdfObject.FileBody, Stream> _copyFileBody;
    private readonly PdfSecurity? _security;

    // Strings and the /Contents gap are encoded through this buffer rather than whole.
    private readonly byte[] _chunk = new byte[4096];

    public PdfSerializer(Stream output, Action<PdfObject.FileBody, Stream> copyFileBody, PdfSecurity? security)
    {
        _output = output;
        _copyFileBody = copyFileBody;
        _security = security;
    }

    public PdfObject.ByteRangeSlot? ByteRange { get; private set; }

    public PdfObject.ContentsSlot? Contents { get; private set; }

    public long Position => _output.Position;

    public void WriteObject(PdfObject value)
    {
        bool encryptStrings = _security != null && _security.EncryptsStringsOf(value);
        if (value is PdfObject.Dict dict && dict.Body != null)
        {
            long length = dict.Body.Length;
            if (dict.Body is not PdfObject.FileBody && _security != null && _security.EncryptsStream(dict))
            {
                length = _security.CipherLength(dict, length);
            }

            dict.Set("Length", PdfObject.Num.FromLong(length));
        }

        WriteAscii(value.ObjectNumber.ToString(CultureInfo.InvariantCulture));
        WriteByte(' ');
        WriteAscii(value.Generation.ToString(CultureInfo.InvariantCulture));
        WriteAscii(" obj\n");
        WriteValue(value, value.ObjectNumber, value.Generation, encryptStrings);
        if (value is PdfObject.Dict streamDict && streamDict.Body != null)
        {
            WriteAscii("\nstream\n");
            WriteBody(streamDict);
            WriteAscii("\nendstream");
        }

        WriteAscii("\nendobj\n");
    }

    public void WriteValue(PdfObject value)
    {
        WriteValue(value, 0, 0, false);
    }

    private void WriteValue(PdfObject value, int objectNumber, int generation, bool encryptStrings)
    {
        if (value is PdfObject.Dict dict)
        {
            WriteAscii("<<");
            foreach (var item in dict.Items)
            {
                WriteByte('\n');
                WriteName(item.Key);
                WriteByte(' ');
                WriteValue(item.Value, objectNumber, generation, encryptStrings && !PdfSecurity.IsSignatureContents(dict, item.Key));
            }

            WriteAscii("\n>>");
            return;
        }

        if (value is PdfObject.Arr array)
        {
            WriteByte('[');
            for (int i = 0; i < array.Items.Count; i++)
            {
                if (i > 0)
                {
                    WriteByte(' ');
                }

                WriteValue(array.Items[i], objectNumber, generation, encryptStrings);
            }

            WriteByte(']');
            return;
        }

        if (value is PdfObject.Name name)
        {
            WriteName(name.Value);
            return;
        }

        if (value is PdfObject.Str str)
        {
            if (encryptStrings && _security != null)
            {
                WriteByte('<');
                using (var hex = new HexStream(this))
                {
                    _security.Encrypt(str.Raw, objectNumber, generation, hex);
                }

                WriteByte('>');
            }
            else
            {
                WriteString(str);
            }

            return;
        }

        if (value is PdfObject.Num number)
        {
            WriteAscii(number.Format());
            return;
        }

        if (value is PdfObject.Ref reference)
        {
            WriteAscii(reference.ObjectId.ToString(CultureInfo.InvariantCulture));
            WriteByte(' ');
            WriteAscii(reference.GenerationNumber.ToString(CultureInfo.InvariantCulture));
            WriteAscii(" R");
            return;
        }

        if (value is PdfObject.Bool boolean)
        {
            WriteAscii(boolean.Value ? "true" : "false");
            return;
        }

        if (value is PdfObject.NullObj)
        {
            WriteAscii("null");
            return;
        }

        if (value is PdfObject.ByteRangeSlot range)
        {
            ByteRange = range;
            WriteByte('[');
            range.DigitPosition = _output.Position;
            WriteAscii("0000000000 0000000000 0000000000 0000000000");
            WriteByte(']');
            return;
        }

        if (value is PdfObject.ContentsSlot contents)
        {
            Contents = contents;
            contents.LessThanPosition = _output.Position;
            WriteByte('<');
            contents.HexPosition = _output.Position;
            for (int i = 0; i < _chunk.Length; i++)
            {
                _chunk[i] = (byte)'0';
            }

            for (long remaining = checked(contents.ReservedBytes * 2L); remaining > 0; remaining -= _chunk.Length)
            {
                _output.Write(_chunk, 0, (int)Math.Min(_chunk.Length, remaining));
            }

            WriteByte('>');
            return;
        }

        throw new PdfException("Cannot serialize a PDF value of type " + value.GetType().Name + ".");
    }

    private void WriteBody(PdfObject.Dict dict)
    {
        PdfObject.StreamBody? body = dict.Body;
        if (body == null)
        {
            return;
        }

        if (body is PdfObject.FileBody fileBody)
        {
            _copyFileBody(fileBody, _output);
            return;
        }

        if (_security != null && _security.EncryptsStream(dict))
        {
            _security.Encrypt(dict, body, _output);
            return;
        }

        body.CopyTo(_output);
    }

    private void WriteString(PdfObject.Str str)
    {
        bool hex = str.PreferHex;
        if (!hex)
        {
            foreach (byte b in str.Raw)
            {
                if (b < 32 || b > 126)
                {
                    hex = true;
                    break;
                }
            }
        }

        if (hex)
        {
            WriteByte('<');
            WriteHex(str.Raw, 0, str.Raw.Length);
            WriteByte('>');
            return;
        }

        WriteByte('(');
        int used = 0;
        foreach (byte b in str.Raw)
        {
            if (used > _chunk.Length - PdfTextCodec.MaxEscapedLength)
            {
                _output.Write(_chunk, 0, used);
                used = 0;
            }

            used += PdfTextCodec.EscapeLiteral(b, _chunk, used);
        }

        _output.Write(_chunk, 0, used);
        WriteByte(')');
    }

    // Encodes in chunks, so the working set does not grow with the string.
    private void WriteHex(byte[] data, int offset, int count)
    {
        while (count > 0)
        {
            int taken = Math.Min(count, _chunk.Length / 2);
            for (int i = 0; i < taken; i++)
            {
                PdfTextCodec.ToUpperHex(data[offset + i], _chunk, i * 2);
            }

            _output.Write(_chunk, 0, taken * 2);
            offset += taken;
            count -= taken;
        }
    }

    private void WriteName(string value)
    {
        WriteByte('/');
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        foreach (byte b in bytes)
        {
            if (b < 33 || b > 126 || b == '#' || IsDelimiter(b))
            {
                WriteByte('#');
                WriteAscii(b.ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                WriteByte(b);
            }
        }
    }

    private void WriteAscii(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        _output.Write(bytes, 0, bytes.Length);
    }

    private void WriteByte(char value)
    {
        _output.WriteByte((byte)value);
    }

    private void WriteByte(byte value)
    {
        _output.WriteByte(value);
    }

    private static bool IsDelimiter(byte b)
    {
        return b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';
    }

    // Writes what the cipher produces to the serializer's output as hex digits.
    private sealed class HexStream : Stream
    {
        private readonly PdfSerializer _serializer;

        public HexStream(PdfSerializer serializer)
        {
            _serializer = serializer;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _serializer.WriteHex(buffer, offset, count);
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
