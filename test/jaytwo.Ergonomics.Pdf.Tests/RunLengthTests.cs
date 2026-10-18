using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using jaytwo.Ergonomics.Pdf.Internal;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class RunLengthTests
{
    private const int Cap = 1024 * 1024;

    [Theory]
    [InlineData("004180", "A")]
    [InlineData("01486980", "Hi")]
    [InlineData("FF4180", "AA")]
    [InlineData("004180FF", "A")]
    [InlineData("0041", "A")]
    [InlineData("80", "")]
    [InlineData("008080", "\u0080")]
    public void hand_checked_runs_decode(string hex, string text)
    {
        byte[] plain = text == "\u0080" ? new byte[] { 0x80 } : Encoding.ASCII.GetBytes(text);

        Assert.Equal(plain, Decode(Hex(hex), "RunLengthDecode"));
    }

    [Fact]
    public void repeat_of_128_and_literal_of_128_decode()
    {
        var repeated = new byte[128];
        for (int i = 0; i < repeated.Length; i++)
        {
            repeated[i] = 0x42;
        }

        Assert.Equal(repeated, Decode(new byte[] { 129, 0x42, 128 }, "RL"));

        var literal = new byte[128];
        for (int i = 0; i < literal.Length; i++)
        {
            literal[i] = (byte)i;
        }

        var encoded = new byte[130];
        encoded[0] = 127;
        Buffer.BlockCopy(literal, 0, encoded, 1, 128);
        encoded[129] = 128;
        Assert.Equal(literal, Decode(encoded, "RunLengthDecode"));
    }

    [Fact]
    public void mixed_runs_round_trip_one_byte_at_a_time()
    {
        var plain = new byte[300];
        for (int i = 0; i < plain.Length; i++)
        {
            plain[i] = (byte)(i < 40 ? 0x7F : i);
        }

        byte[] encoded = Encode(plain);

        Assert.Equal(plain, Decode(encoded, "RunLengthDecode"));
        Assert.Equal(plain, ReadOneByte(encoded));
    }

    [Fact]
    public void literal_keeps_zero_and_high_bytes()
    {
        byte[] plain = new byte[] { 0x00, 0x7F, 0xFF };
        byte[] encoded = new byte[] { 2, 0x00, 0x7F, 0xFF, 128 };

        Assert.Equal(plain, Decode(encoded, "RL"));
    }

    [Fact]
    public void truncated_literal_and_repeat_are_parse_errors()
    {
        Assert.Throws<PdfParseException>(() => Decode(new byte[] { 2, 0x41 }, "RunLengthDecode"));
        Assert.Throws<PdfParseException>(() => Decode(new byte[] { 255 }, "RunLengthDecode"));
    }

    [Fact]
    public void expanded_run_above_the_cap_is_refused()
    {
        var error = Assert.Throws<PdfObjectTooLargeException>(() => Decode(new byte[] { 129, 0x42 }, "RL", 10));

        Assert.Contains("10", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ascii85_then_run_length_follows_filter_array_order()
    {
        byte[] plain = Encoding.ASCII.GetBytes("RL");
        byte[] wrapped = Ascii85(new byte[] { 1, (byte)'R', (byte)'L', 128 });

        byte[] decoded = PdfStreamDecoder.Decode(
            wrapped,
            new[] { "ASCII85Decode", "RunLengthDecode" },
            new PdfObject.Dict?[] { null, null },
            Cap);

        Assert.Equal(plain, decoded);
    }

    [Fact]
    public void run_length_attachment_opens()
    {
        byte[] notes = new byte[] { 0x00, 0x00, 0x00, 0x41, 0x42 };
        var builder = new PdfBuilder(version: "1.0");
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(notes.bin) 5 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(4, "/Type /EmbeddedFile /Filter /RL /Params << /Size " + notes.Length.ToString() + " >>", Encode(notes));
        builder.Indirect(5, "<< /Type /Filespec /F (notes.bin) /EF << /F 4 0 R >> >>");
        byte[] pdf = builder.Finish();

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        using var opened = document.Attachments[0].Open();
        Assert.Equal(notes, ReadAll(opened));
    }

    private static byte[] Decode(byte[] data, string filter, int cap = Cap)
    {
        return PdfStreamDecoder.Decode(data, new[] { filter }, new PdfObject.Dict?[] { null }, cap);
    }

    private static byte[] ReadOneByte(byte[] data)
    {
        using Stream stream = PdfStreamDecoder.OpenDecoded(new MemoryStream(data), new[] { "RunLengthDecode" }, new PdfObject.Dict?[] { null });
        using var output = new MemoryStream();
        var one = new byte[1];
        int read;
        while ((read = stream.Read(one, 0, 1)) > 0)
        {
            output.Write(one, 0, read);
        }

        return output.ToArray();
    }

    private static byte[] Encode(byte[] data)
    {
        var output = new List<byte>();
        int index = 0;
        while (index < data.Length)
        {
            int repeat = 1;
            while (index + repeat < data.Length && data[index + repeat] == data[index] && repeat < 128)
            {
                repeat++;
            }

            if (repeat >= 2)
            {
                output.Add((byte)(257 - repeat));
                output.Add(data[index]);
                index += repeat;
                continue;
            }

            int start = index;
            index++;
            while (index < data.Length && index - start < 128)
            {
                if (index + 1 < data.Length && data[index + 1] == data[index])
                {
                    break;
                }

                index++;
            }

            int count = index - start;
            output.Add((byte)(count - 1));
            for (int i = 0; i < count; i++)
            {
                output.Add(data[start + i]);
            }
        }

        output.Add(128);
        return output.ToArray();
    }

    private static byte[] Ascii85(byte[] data)
    {
        var output = new StringBuilder();
        uint value = 0;
        for (int i = 0; i < 4; i++)
        {
            value = (value << 8) | (i < data.Length ? data[i] : (byte)0);
        }

        var chars = new char[5];
        for (int i = 4; i >= 0; i--)
        {
            chars[i] = (char)('!' + (value % 85));
            value /= 85;
        }

        output.Append(chars, 0, data.Length + 1);
        output.Append("~>");
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Hex(string hex)
    {
        var data = new byte[hex.Length / 2];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return data;
    }
}
