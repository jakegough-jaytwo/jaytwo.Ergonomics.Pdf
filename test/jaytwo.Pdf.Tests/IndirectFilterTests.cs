using System;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-40: ISO 32000-1 7.3.10 lets a stream's /Filter, /DecodeParms, their array entries, and the parameter values be
// indirect. They resolve through the document within its reference-chain, cycle, and load-depth bounds. A cross-reference
// stream must use direct values (ISO 32000-1 7.5.8.2), so a reference there is still refused.
public class IndirectFilterTests
{
    private const string Predicted = "<< /Predictor 12 /Columns 4 >>";

    private static readonly byte[] Plain = Encoding.ASCII.GetBytes("attachment-bytes");

    [Theory]
    [InlineData("/Filter 7 0 R", "/FlateDecode", null, "zlib")]
    [InlineData("/Filter 7 0 R", "[/ASCIIHexDecode /FlateDecode]", null, "hex")]
    [InlineData("/Filter [/ASCIIHexDecode 7 0 R]", "/FlateDecode", null, "hex")]
    [InlineData("/Filter 7 0 R", "[/AHx 8 0 R]", "/Fl", "hex")]
    [InlineData("/Filter 7 0 R", "8 0 R", "/FlateDecode", "zlib")]
    [InlineData("/Filter /FlateDecode /DecodeParms 7 0 R", Predicted, null, "png")]
    [InlineData("/Filter [/AHx /Fl] /DecodeParms 7 0 R", "[null " + Predicted + "]", null, "hex-png")]
    [InlineData("/Filter [/AHx /Fl] /DecodeParms [null 7 0 R]", Predicted, null, "hex-png")]
    [InlineData("/Filter [/AHx /Fl] /DecodeParms 7 0 R", "[null 8 0 R]", Predicted, "hex-png")]
    [InlineData("/Filter /FlateDecode /DecodeParms << /Predictor 7 0 R /Columns 8 0 R >>", "12", "4", "png")]
    [InlineData("/Filter 7 0 R /DecodeParms 8 0 R", "[/AHx /Fl]", "[null " + Predicted + "]", "hex-png")]
    public void indirect_filter_and_decode_parms_resolve_and_the_attachment_decodes(string entries, string seven, string? eight, string encoding)
    {
        byte[] pdf = Attachment(entries, Encode(encoding), seven, eight);

        using var document = PdfDocument.Open(new MemoryStream(pdf, writable: false), leaveOpen: true);

        Assert.Equal(Plain, ReadAll(document.Attachments["notes.bin"].Open()));
    }

    [Fact]
    public void indirect_parameter_values_resolve_without_changing_the_saved_stream_dictionary()
    {
        byte[] pdf = Attachment("/Filter /FlateDecode /DecodeParms << /Predictor 7 0 R /Columns 8 0 R >>", Encode("png"), "12", "4");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(Plain, ReadAll(document.Attachments["notes.bin"].Open()));
            document.Metadata.Title = "Revised";
            document.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        using var again = PdfDocument.Open(new MemoryStream(saved, writable: false), leaveOpen: true);
        Assert.Equal("Revised", again.Metadata.Title);
        Assert.Equal(Plain, ReadAll(again.Attachments["notes.bin"].Open()));
    }

    [Fact]
    public void reference_to_an_undefined_filter_object_is_null_so_the_stream_has_no_filter()
    {
        // ISO 32000-1 7.3.10 (D-26): a reference to an undefined object is the null object, and a null /Filter is absent.
        byte[] pdf = Attachment("/Filter 99 0 R /DecodeParms 98 0 R", Plain, "null", null);

        using var document = PdfDocument.Open(new MemoryStream(pdf, writable: false), leaveOpen: true);

        Assert.Equal(Plain, ReadAll(document.Attachments["notes.bin"].Open()));
    }

    [Fact]
    public void object_stream_with_an_indirect_filter_array_decodes()
    {
        byte[] payload = Encoding.ASCII.GetBytes(TestCompat.ToHexString(TestCompat.Zlib(Encoding.ASCII.GetBytes("4 0 << /Title (FromStream) >>"))) + ">");
        var builder = PdfBuilder.Basic();
        builder.Indirect(8, "[/ASCIIHexDecode 9 0 R]");
        builder.Indirect(9, "/FlateDecode");
        builder.StreamObject(6, "/Type /ObjStm /N 1 /First 4 /Filter 8 0 R", payload);
        builder.Compressed(4, 6, 0);
        builder.AppendXrefStream(7, "/Info 4 0 R");

        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot(), writable: false), leaveOpen: true);

        Assert.Equal("FromStream", document.Metadata.Title);
    }

    [Fact]
    public void object_stream_whose_filter_is_stored_inside_itself_is_refused()
    {
        byte[] payload = Encoding.ASCII.GetBytes("4 0 8 26 << /Title (FromStream) >> /FlateDecode");
        var builder = PdfBuilder.Basic();
        builder.StreamObject(6, "/Type /ObjStm /N 2 /First 9 /Filter 8 0 R", payload);
        builder.Compressed(4, 6, 0);
        builder.Compressed(8, 6, 1);
        builder.AppendXrefStream(7, "/Info 4 0 R");
        byte[] pdf = builder.Snapshot();

        PdfParseException error = Assert.Throws<PdfParseException>(() =>
        {
            using var document = PdfDocument.Open(new MemoryStream(pdf, writable: false), leaveOpen: true);
            _ = document.Metadata.Title;
        });
        Assert.Contains("Cyclic", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Filter 7 0 R", "8 0 R", "7 0 R", "Cyclic")]
    [InlineData("/Filter 5 0 R", "null", null, "Stream /Filter is not a name")]
    [InlineData("/Filter 7 0 R", "(FlateDecode)", null, "Stream /Filter is not a name")]
    [InlineData("/Filter [7 0 R]", "[/FlateDecode]", null, "Stream /Filter array has an entry that is not a name")]
    [InlineData("/Filter /FlateDecode /DecodeParms 7 0 R", "5", null, "Stream /DecodeParms is not a dictionary or an array")]
    [InlineData("/Filter /FlateDecode /DecodeParms 7 0 R", "[null null]", null, "Stream /DecodeParms does not have one entry for each filter")]
    [InlineData("/Filter [/AHx /Fl] /DecodeParms [null 7 0 R]", "/FlateDecode", null, "Stream /DecodeParms array has an entry")]
    public void indirect_filter_that_resolves_to_a_malformed_value_or_a_cycle_is_refused(string entries, string seven, string? eight, string message)
    {
        byte[] pdf = Attachment(entries, TestCompat.Zlib(Plain), seven, eight);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);

        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            PdfAttachment attachment = document.Attachments["notes.bin"];
            PdfParseException error = Assert.Throws<PdfParseException>(() => ReadAll(attachment.Open()));
            Assert.Contains(message, error.Message, StringComparison.Ordinal);
        }

        Assert.Equal(pdf, stream.ToArray());
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void indirect_filter_chain_decodes_up_to_the_reference_chain_bound(int references, bool decodes)
    {
        var builder = AttachmentBuilder("/Filter 10 0 R", TestCompat.Zlib(Plain));
        for (int i = 0; i < references - 1; i++)
        {
            builder.Indirect(10 + i, (11 + i).ToString(CultureInfo.InvariantCulture) + " 0 R");
        }

        builder.Indirect(10 + references - 1, "/FlateDecode");
        using var document = PdfDocument.Open(new MemoryStream(builder.Finish(), writable: false), leaveOpen: true);
        PdfAttachment attachment = document.Attachments["notes.bin"];

        if (decodes)
        {
            Assert.Equal(Plain, ReadAll(attachment.Open()));
        }
        else
        {
            PdfParseException error = Assert.Throws<PdfParseException>(() => ReadAll(attachment.Open()));
            Assert.Contains("too deep", error.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("/Filter /FlateDecode /DecodeParms", "/Filter 8 0 R /DecodeParms", "/FlateDecode")]
    [InlineData("/DecodeParms << /Predictor 12 ", "/DecodeParms << /Predictor 8 0 R ", "12")]
    public void xref_stream_with_an_indirect_filter_or_parameter_is_refused(string direct, string indirect, string target)
    {
        var builder = PdfBuilder.Basic();
        builder.Indirect(8, target);
        builder.AppendXrefStream(7, string.Empty);
        string text = TestCompat.Latin1.GetString(builder.Snapshot());
        int at = text.LastIndexOf(direct, StringComparison.Ordinal);
        Assert.True(at > 0);
        byte[] pdf = TestCompat.Latin1.GetBytes(text.Substring(0, at) + indirect + text.Substring(at + direct.Length));

        PdfParseException error = Assert.Throws<PdfParseException>(() => PdfDocument.Open(new MemoryStream(pdf, writable: false), leaveOpen: true));

        Assert.Contains("indirect reference where a direct object is required", error.Message, StringComparison.Ordinal);
    }

    private static byte[] Encode(string encoding)
    {
        return encoding switch
        {
            "zlib" => TestCompat.Zlib(Plain),
            "hex" => Hex(TestCompat.Zlib(Plain)),
            "png" => TestCompat.Zlib(PngNone(Plain, 4)),
            "hex-png" => Hex(TestCompat.Zlib(PngNone(Plain, 4))),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        };
    }

    private static byte[] Hex(byte[] data)
    {
        return Encoding.ASCII.GetBytes(TestCompat.ToHexString(data) + ">");
    }

    // PNG predictor rows with filter type 0 (None): a decoder that ignores /Predictor returns the tag bytes too.
    private static byte[] PngNone(byte[] data, int columns)
    {
        using var output = new MemoryStream();
        for (int at = 0; at < data.Length; at += columns)
        {
            output.WriteByte(0);
            output.Write(data, at, columns);
        }

        return output.ToArray();
    }

    private static byte[] Attachment(string entries, byte[] data, string seven, string? eight)
    {
        PdfBuilder builder = AttachmentBuilder(entries, data);
        builder.Indirect(7, seven);
        if (eight != null)
        {
            builder.Indirect(8, eight);
        }

        return builder.Finish();
    }

    private static PdfBuilder AttachmentBuilder(string entries, byte[] data)
    {
        var builder = PdfBuilder.Basic(" /Names << /EmbeddedFiles << /Names [(notes.bin) 6 0 R] >> >>");
        builder.StreamObject(5, "/Type /EmbeddedFile " + entries + " /Params << /Size 16 >>", data);
        builder.Indirect(6, "<< /Type /Filespec /UF (notes.bin) /EF << /UF 5 0 R >> >>");
        return builder;
    }

    private static byte[] ReadAll(Stream stream)
    {
        using (stream)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
    }
}
