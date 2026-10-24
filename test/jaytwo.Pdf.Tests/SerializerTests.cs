using System;
using System.IO;
using System.Linq;
using System.Text;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-50: strings and the reserved /Contents gap are encoded through a fixed buffer, so serializing a long one allocates
// a fixed amount rather than five to twelve times its length before PdfAppendixBuffer can refuse it.
public class SerializerTests
{
    private const int LongString = 9 * 1024 * 1024;
    private const long AllocationBound = 256 * 1024;

    [Theory]
    [InlineData((byte)'a', false)]
    [InlineData((byte)'(', false)]
    [InlineData((byte)'a', true)]
    [InlineData((byte)0, false)]
    public void long_string_allocates_a_fixed_amount(byte fill, bool preferHex)
    {
        var raw = Enumerable.Repeat(fill, LongString).ToArray();

        long allocated = Allocation(null, new PdfObject.Str(raw, preferHex));

        Assert.True(allocated < AllocationBound, "allocated " + allocated.ToString());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void long_encrypted_string_allocates_a_fixed_amount(int revision)
    {
        PdfSecurity security = Security(Encryptor(revision));

        long allocated = Allocation(security, new PdfObject.Str(new byte[LongString], false));

        Assert.True(allocated < AllocationBound, "allocated " + allocated.ToString());
    }

    [Fact]
    public void largest_contents_gap_allocates_a_fixed_amount()
    {
        long allocated = Allocation(null, new PdfObject.ContentsSlot(PdfSignatureOptions.MaxReservedContentsBytes));

        Assert.True(allocated < AllocationBound, "allocated " + allocated.ToString());
    }

    [Fact]
    public void literal_escapes_that_straddle_the_buffer_are_written_whole()
    {
        string text = string.Concat(Enumerable.Repeat("a(b)c\\", 2000));

        string written = Serialize(new PdfObject.Str(Encoding.ASCII.GetBytes(text), false));

        Assert.Equal("(" + text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + ")", written);
    }

    [Fact]
    public void hex_string_longer_than_the_buffer_is_upper_case_hex()
    {
        byte[] raw = Enumerable.Range(0, 10001).Select(i => (byte)(i * 7)).ToArray();

        string written = Serialize(new PdfObject.Str(raw, false));

        Assert.Equal("<" + TestCompat.ToHexString(raw) + ">", written);
    }

    [Fact]
    public void contents_gap_longer_than_the_buffer_is_zero_digits()
    {
        string written = Serialize(new PdfObject.ContentsSlot(5000));

        Assert.Equal("<" + new string('0', 10000) + ">", written);
    }

    [Fact]
    public void literal_escape_writes_each_escape_form()
    {
        byte[] raw = { (byte)'(', (byte)')', (byte)'\\', (byte)'\n', (byte)'\r', (byte)'\t', 0x00, 0x7F, 0xFF, (byte)'A' };

        Assert.Equal(@"\(\)\\\n\r\t\000\177\377A", PdfTextCodec.EscapeLiteral(raw));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void encrypted_string_longer_than_the_buffer_matches_the_reference_cipher(int revision)
    {
        StandardEncryptor crypt = Encryptor(revision);
        byte[] plain = Enumerable.Range(0, 10001).Select(i => (byte)(i * 7)).ToArray();
        var dict = new PdfObject.Dict { ObjectNumber = 5 };
        dict.Set("S", new PdfObject.Str(plain, false));
        using var output = new MemoryStream();

        new PdfSerializer(output, (_, _) => { }, Security(crypt)).WriteObject(dict);

        string written = Encoding.ASCII.GetString(output.ToArray());
        int start = written.IndexOf("/S <", StringComparison.Ordinal) + 4;
        byte[] cipher = TestCompat.FromHexString(written.Substring(start, written.IndexOf('>', start) - start));
        byte[] expected = revision == 3 ? crypt.Encrypt(plain, 5, 0) : crypt.Encrypt(plain, 5, 0, cipher.Take(16).ToArray());
        Assert.Equal(expected, cipher);
    }

    // Writes that end mid-block, and blocks that span writes, encrypt as the whole plaintext does.
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void aes_stream_body_written_in_uneven_pieces_matches_the_reference_cipher(int revision)
    {
        StandardEncryptor crypt = Encryptor(revision);
        byte[] plain = Enumerable.Range(0, 70001).Select(i => (byte)(i * 7)).ToArray();
        var dict = new PdfObject.Dict { ObjectNumber = 5, Body = new PiecewiseBody(plain, new[] { 1, 15, 16, 17, 3, 65536, 31, 100 }) };
        using var output = new MemoryStream();

        new PdfSerializer(output, (_, _) => { }, Security(crypt)).WriteObject(dict);

        byte[] written = output.ToArray();
        int start = Encoding.ASCII.GetString(written).IndexOf("stream\n", StringComparison.Ordinal) + 7;
        int length = (int)((PdfObject.Num)dict.Get("Length")!).LongValue;
        byte[] cipher = written.Skip(start).Take(length).ToArray();
        Assert.Equal(crypt.Encrypt(plain, 5, 0, cipher.Take(16).ToArray()), cipher);
    }

    // Writes value into a dictionary, so an encrypting serializer encrypts its strings, to a stream that keeps nothing.
    // A short string first initializes the cipher and codec types, so only the long write is measured.
    private static long Allocation(PdfSecurity? security, PdfObject value)
    {
        var serializer = new PdfSerializer(Stream.Null, (_, _) => { }, security);
        serializer.WriteObject(Dictionary(new PdfObject.Str(new byte[] { 0, (byte)'(' }, false)));
        serializer.WriteObject(Dictionary(new PdfObject.Str(new byte[] { (byte)'(' }, false)));
        serializer.WriteObject(Dictionary(new PdfObject.ContentsSlot(1)));
        PdfObject.Dict dict = Dictionary(value);

        long before = GC.GetAllocatedBytesForCurrentThread();
        serializer.WriteObject(dict);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static PdfObject.Dict Dictionary(PdfObject value)
    {
        var dict = new PdfObject.Dict { ObjectNumber = 5 };
        dict.Set("S", value);
        return dict;
    }

    private static string Serialize(PdfObject value)
    {
        using var output = new MemoryStream();
        new PdfSerializer(output, (_, _) => { }, null).WriteValue(value);
        return Encoding.ASCII.GetString(output.ToArray());
    }

    private static StandardEncryptor Encryptor(int revision)
    {
        StandardEncryptor.Kind kind = revision == 3 ? StandardEncryptor.Kind.Revision3 : revision == 4 ? StandardEncryptor.Kind.Revision4Aes : StandardEncryptor.Kind.Revision6;
        return new StandardEncryptor(kind, "user", "owner", -4, encryptMetadata: true);
    }

    private static PdfSecurity Security(StandardEncryptor crypt)
    {
        var parser = new PdfParser(PdfCursor.FromBytes(Encoding.ASCII.GetBytes(crypt.Dictionary(null))), null, 0);
        return PdfSecurity.Create((PdfObject.Dict)parser.ParseValue(), crypt.Id, "user", 99);
    }

    // Copies its data in the given piece sizes, repeated until the data runs out.
    private sealed class PiecewiseBody : PdfObject.StreamBody
    {
        private readonly byte[] _data;
        private readonly int[] _pieces;

        public PiecewiseBody(byte[] data, int[] pieces)
        {
            _data = data;
            _pieces = pieces;
        }

        public override long Length => _data.Length;

        public override void CopyTo(Stream destination)
        {
            for (int offset = 0, i = 0; offset < _data.Length; i++)
            {
                int count = Math.Min(_pieces[i % _pieces.Length], _data.Length - offset);
                destination.Write(_data, offset, count);
                offset += count;
            }
        }
    }
}
