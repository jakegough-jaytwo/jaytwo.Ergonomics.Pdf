using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

public class LzwTests
{
    private const int Cap = 1024 * 1024;

    [Theory]
    [InlineData("", "8080")]
    [InlineData("A", "20C040")]
    [InlineData("aaa", "30C0A020")]
    [InlineData("ABABABA", "2090A0504808")]
    [InlineData("Hello, PDF 1.0", "24194D86C378B04050221184031170C202")]
    public void hand_checked_vectors_decode_and_match_the_encoder(string text, string hex)
    {
        byte[] plain = Encoding.ASCII.GetBytes(text);
        byte[] encoded = Hex(hex);

        Assert.Equal(encoded, Encode(plain, earlyChange: 1));
        Assert.Equal(plain, Decode(encoded, "LZWDecode", null));
    }

    [Fact]
    public void early_change_zero_and_one_use_different_widths_and_both_decode()
    {
        byte[] plain = Pattern(600);
        byte[] early = Convert.FromBase64String("AYUEo2I5YNJ6RacVq+Z7geICCYkGpGK5oPKKTasXrOb7wAISEY0IpWM54RKaVa8Zred4ACIiGZEKpmO6ITKqXbMbruf4QEIyIZUMp2Q6YVK6Zbcdr+B4gGJCKZkOqGS6oXLKbbsfoOD4wIJSMZ0QqWU64ZLadb8BoeF5AKJiOaESqmW7IbLqfYMDouH5QMJyQaUUq2Y7YdL6BYcFo+J5gOKCSakWrGa7ofIKDYsHpOL5wQKSUa0YrWc74BIaFY8JpeN6ASKiWbEarme4IDIqHZMLpuP6QUKyYbUcr2A4YFI6JZcNp+R6gWLCabkeoGC4oHJKLZsPqOT6wYLScb0AoWE44JJaNZ8RqeV7AaLieYEComG5ILJqPaMTquX7QOE8kCQRBkIQpDEORBEkURZGEaRxHkgSJJEmShKksS5MEyTRNk4TpPE+UBQlEUZSFKUxTlQVJVFWVhWlcV5YFiWRZloWpbFuXBcl0XZeF6XxfmAYJhGGYhimMY5kGSZRlmYZpnGeaBomkaZqGqaxrmwbJtG2bhum8b5wHCcRxnIcpzHOdB0nUdZ2Hadx3ngeJ5Hmeh6nse58HyfR9n4fp/H+gCAkFQdCULQ1D0RRNFUXRlG0dR9IUjSVJ0pStLUvTFM01TdOU7T1P1BUNRVHUlS1NU9UVTVVVzKQEA==");
        byte[] late = Convert.FromBase64String("AYUEo2I5YNJ6RacVq+Z7geICCYkGpGK5oPKKTasXrOb7wAISEY0IpWM54RKaVa8Zred4ACIiGZEKpmO6ITKqXbMbruf4QEIyIZUMp2Q6YVK6Zbcdr+B4gGJCKZkOqGS6oXLKbbsfoOD4wIJSMZ0QqWU64ZLadb8BoeF5AKJiOaESqmW7IbLqfYMDouH5QMJyQaUUq2Y7YdL6BYcFo+J5gOKCSakWrGa7ofIKDYsHpOL5wQKSUa0YrWc74BIaFY8JpeN6ASKiWbEarme4IDIqHZMLpuP6QUKyYbUcr2A4YFI6JZcNp+R6gWLCabkeoGC4oHJKLZsPqOT6wYLScb0AoWE44JJaNZ8RqeV7AaLieYEComG5ILJqPaMTquX7QcJ5IEgiDIQhSGIciCJIoiyMI0jiPJAkSSJMlCVJYlyYJkmibJwnSeJ8oChKIoykKUpinKgqSqKsrCtK4rywLEsizLQtS2LcuC5Louy8L0vi/MAwTCMMxDFMYxzIMkyjLMwzTOM80DRNI0zUNU1jXNg2TaNs3DdN43zgOE4jjOQ5TmOc6DpOo6zsO07jvPA8TyPM9D1PY9z4Pk+j7Pw/T+P9AEBIKg6EoWhqHoiiaKoujKNo6j6QpGkqTpSlaWpemKZpqm6cp2nqfqCoaiqOpKlqap6oqmqqrmUgIA==");

        Assert.NotEqual(early, late);
        Assert.Equal(early, Encode(plain, earlyChange: 1));
        Assert.Equal(late, Encode(plain, earlyChange: 0));
        Assert.Equal(plain, Decode(early, "LZWDecode", Parms("EarlyChange", 1)));
        Assert.Equal(plain, Decode(late, "LZWDecode", Parms("EarlyChange", 0)));
        Assert.Throws<PdfParseException>(() => Decode(early, "LZWDecode", Parms("EarlyChange", 0)));
    }

    [Fact]
    public void clear_code_resets_the_table()
    {
        byte[] plain = Pattern(600);
        byte[] encoded = Convert.FromBase64String("gADCglGxHLBpPSLTitXzPcDxAQTEg1IxXNB5RSbVi9ZzfeABCQjGhFKxnPCJTSrXjNbzvAAREQzIhVMx3RCZVS7Zjddz/CAhGRDKhlOyHTCpXTLbjtfwPEAxIRTMh1QyXVC5ZTbdj9BwfGBBKRjOiFSynXDJbTrfgNDwvIBRMRzQiVUy3ZDZdT7BgdFw/KBhOSDSilWzHbDpfQLDgtHxPMBxQSTUi1YzXdD5BQbFg9JxfOCBSSjWjFaznfAJDQrHhNLxvQCRUSzYjVcz3BAZFQ7JhdNx/SChWTDajlewHDApHRLLhtPyPUCxYTTcj1AwXFA5JRbNh9RyfWDBaTjegFCwnHBJLRrPiNTyvYDRcTzAgVEw3JBZNR7RidVy/aBwnkgSCIMhCFIYhyIIkiiLIwjSOI8kCRJIkyUJUliXJgmSaJsnCdJ4nygKEoijKQpSmKcqCpKoqysK0rivLAsSyLMtC1LYty4Lkui7LwvS+L8wDBMIwzEMUxjHMgyTKMszDNM4zzQNE0jTNQ1TWNc2DZNo2zcN03jfOA4TiOM5DlOY5zoOk6jrOw7TuO88DxPI8z0PU9j3Pg+T6Ps/D9P4/0AQEgqDoShaGoeiKJoqi6Mo2jqPpCkaSpOlKVpal6Ypmmqbpynaep+oKhqKo6kqWpqnqiqaqquZSAg=");

        Assert.Equal(encoded, Encode(plain, earlyChange: 1, clearAtStart: true));
        Assert.Equal(plain, Decode(encoded, "LZW", null));
    }

    [Fact]
    public void full_table_emits_a_clear_code_and_round_trips()
    {
        byte[] plain = Noise(8000);
        byte[] encoded = Encode(plain, earlyChange: 1);

        Assert.Equal("81f252eb6d7a7121294e7ad19210e871c35d0ef4bb883882497e8f64fc85a1a8", Sha256(encoded));
        Assert.Equal(plain, Decode(encoded, "LZWDecode", null));
        Assert.Equal(plain, ReadOneByte(encoded, "LZWDecode", null));
    }

    [Fact]
    public void tiff_predictor_runs_after_lzw()
    {
        byte[] deltas = new byte[] { 1, 1, 1, 1, 5, 252, 0, 0 };
        byte[] encoded = Encode(deltas, earlyChange: 1);
        var parms = new PdfObject.Dict();
        parms.Set("Predictor", PdfObject.Num.FromLong(2));
        parms.Set("Columns", PdfObject.Num.FromLong(4));

        byte[] plain = Decode(encoded, "LZWDecode", parms);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 1, 1, 1 }, plain);
    }

    [Fact]
    public void ascii85_then_lzw_follows_filter_array_order()
    {
        byte[] plain = Encoding.ASCII.GetBytes("filters run first to last");
        byte[] lzw = Encode(plain, earlyChange: 1);
        byte[] wrapped = Ascii85(lzw);

        byte[] decoded = PdfStreamDecoder.Decode(
            wrapped,
            new[] { "A85", "LZW" },
            new PdfObject.Dict?[] { null, null },
            Cap);

        Assert.Equal(plain, decoded);
        using var stream = PdfStreamDecoder.OpenDecoded(
            new MemoryStream(wrapped),
            new[] { "ASCII85Decode", "LZWDecode" },
            new PdfObject.Dict?[] { null, null });
        Assert.Equal(plain, ReadAll(stream));
    }

    [Fact]
    public void abbreviations_decode_hex_base85_and_flate()
    {
        Assert.Equal(new byte[] { 0x48, 0x69 }, Decode(Encoding.ASCII.GetBytes("4869>"), "AHx", null));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, Decode(Encoding.ASCII.GetBytes("z~>"), "A85", null));
        byte[] plain = Encoding.ASCII.GetBytes("flate");
        Assert.Equal(plain, Decode(Zlib(plain), "Fl", null));
    }

    [Fact]
    public void truncated_stream_and_unknown_code_are_parse_errors()
    {
        byte[] encoded = Hex("20C040");
        Assert.Throws<PdfParseException>(() => Decode(encoded.AsSpan(0, 2).ToArray(), "LZWDecode", null));
        Assert.Throws<PdfParseException>(() => Decode(new byte[] { 0xFF, 0x80 }, "LZWDecode", null));
    }

    [Fact]
    public void decoded_output_above_the_cap_is_refused()
    {
        byte[] encoded = Encode(new byte[1000], earlyChange: 1);

        var error = Assert.Throws<PdfObjectTooLargeException>(() => Decode(encoded, "LZWDecode", null, 100));

        Assert.Contains("100", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void lzw_attachment_and_xmp_round_trip_through_the_document()
    {
        byte[] notes = new byte[] { 0x00, 0x7F, 0xFF, 0x41 };
        byte[] packet = Encoding.ASCII.GetBytes("<?xml version=\"1.0\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>");
        var builder = new PdfBuilder(version: "1.0");
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Metadata 7 0 R /Names << /EmbeddedFiles << /Names [(notes.bin) 6 0 R] >> >> /AF [6 0 R] >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(5, "/Type /EmbeddedFile /Filter /LZWDecode /Params << /Size " + notes.Length.ToString() + " >>", Encode(notes, 1));
        builder.Indirect(6, "<< /Type /Filespec /F (notes.bin) /EF << /F 5 0 R >> >>");
        builder.StreamObject(7, "/Type /Metadata /Subtype /XML /Filter [/ASCII85Decode /LZWDecode]", Ascii85(Encode(packet, 1)));
        byte[] pdf = builder.Finish();

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        Assert.Equal(1, document.PageCount);
        using (var opened = document.Attachments[0].Open())
        {
            Assert.Equal(notes, ReadAll(opened));
        }

        using var xmp = document.Metadata.OpenXmp();
        Assert.Equal(packet, ReadAll(xmp));
    }

    private static byte[] Decode(byte[] data, string filter, PdfObject.Dict? parms, int cap = Cap)
    {
        return PdfStreamDecoder.Decode(data, new[] { filter }, new PdfObject.Dict?[] { parms }, cap);
    }

    private static byte[] ReadOneByte(byte[] data, string filter, PdfObject.Dict? parms)
    {
        using Stream stream = PdfStreamDecoder.OpenDecoded(new MemoryStream(data), new[] { filter }, new PdfObject.Dict?[] { parms });
        using var output = new MemoryStream();
        var one = new byte[1];
        int read;
        while ((read = stream.Read(one, 0, 1)) > 0)
        {
            output.Write(one, 0, read);
        }

        return output.ToArray();
    }

    private static byte[] Encode(byte[] data, int earlyChange, bool clearAtStart = false)
    {
        var table = new Dictionary<string, int>(4096);
        Reset(table, out int nextCode);
        var codes = new List<(int Code, int Width)>();
        if (clearAtStart)
        {
            codes.Add((256, 9));
        }

        string omega = string.Empty;
        foreach (byte value in data)
        {
            string candidate = omega + (char)value;
            if (table.ContainsKey(candidate))
            {
                omega = candidate;
                continue;
            }

            int width = CodeWidth(nextCode - 1, earlyChange);
            codes.Add((table[omega], width));
            table[candidate] = nextCode;
            nextCode++;
            if (nextCode == 4096)
            {
                codes.Add((256, width));
                Reset(table, out nextCode);
            }

            omega = ((char)value).ToString();
        }

        if (omega.Length > 0)
        {
            codes.Add((table[omega], CodeWidth(nextCode - 1, earlyChange)));
        }

        codes.Add((257, CodeWidth(nextCode, earlyChange)));
        return Pack(codes);
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

    private static void Reset(Dictionary<string, int> table, out int nextCode)
    {
        table.Clear();
        for (int i = 0; i < 256; i++)
        {
            table[((char)i).ToString()] = i;
        }

        nextCode = 258;
    }

    private static byte[] Pack(List<(int Code, int Width)> codes)
    {
        ulong accumulator = 0;
        int bits = 0;
        var output = new List<byte>();
        foreach ((int code, int width) in codes)
        {
            accumulator = (accumulator << width) | (uint)code;
            bits += width;
            while (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((accumulator >> bits) & 0xFF));
            }

            accumulator = bits == 0 ? 0 : accumulator & ((1ul << bits) - 1);
        }

        if (bits > 0)
        {
            output.Add((byte)((accumulator << (8 - bits)) & 0xFF));
        }

        return output.ToArray();
    }

    private static PdfObject.Dict Parms(string key, int value)
    {
        var dict = new PdfObject.Dict();
        dict.Set(key, PdfObject.Num.FromLong(value));
        return dict;
    }

    private static byte[] Pattern(int count)
    {
        var data = new byte[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = (byte)(((i * 17) + 3) & 0xFF);
        }

        return data;
    }

    private static byte[] Noise(int count)
    {
        uint state = 0x12345678;
        var data = new byte[count];
        for (int i = 0; i < count; i++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            data[i] = (byte)state;
        }

        return data;
    }

    private static byte[] Ascii85(byte[] data)
    {
        var output = new StringBuilder();
        int index = 0;
        while (index + 4 <= data.Length)
        {
            uint value = ((uint)data[index] << 24) | ((uint)data[index + 1] << 16) | ((uint)data[index + 2] << 8) | data[index + 3];
            index += 4;
            if (value == 0)
            {
                output.Append('z');
                continue;
            }

            output.Append(Base85(value, 5));
        }

        int remain = data.Length - index;
        if (remain > 0)
        {
            uint value = 0;
            for (int i = 0; i < remain; i++)
            {
                value = (value << 8) | data[index + i];
            }

            value <<= (4 - remain) * 8;
            output.Append(Base85(value, remain + 1));
        }

        output.Append("~>");
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static string Base85(uint value, int count)
    {
        var chars = new char[5];
        for (int i = 4; i >= 0; i--)
        {
            chars[i] = (char)('!' + (value % 85));
            value /= 85;
        }

        return new string(chars, 0, count);
    }

    private static byte[] Zlib(byte[] data)
    {
        return TestCompat.Zlib(data);
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

    private static string Sha256(byte[] data)
    {
        return TestCompat.ToHexString(TestCompat.Sha256(data)).ToLowerInvariant();
    }
}
