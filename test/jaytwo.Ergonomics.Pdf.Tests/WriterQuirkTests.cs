using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using jaytwo.Ergonomics.Pdf.Attachments;
using jaytwo.Ergonomics.Pdf.Internal;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec section 8: each tolerated writer quirk has a real-world fixture, a test, and a README entry. Also the reads
// that the G2 corpus showed were too strict (W-66, findings D1 and Q1 in agents/evidence/qualification/g2-corpus.md).
public class WriterQuirkTests
{
    private static readonly byte[] Plain = Encoding.ASCII.GetBytes("quirk payload, long enough to deflate: " + new string('x', 200));

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public void flate_stream_whose_length_counts_the_end_of_line_decodes(string eol)
    {
        byte[] data = TestCompat.Zlib(Plain).Concat(Encoding.ASCII.GetBytes(eol)).ToArray();

        Assert.Equal(Plain, ReadAttachment(FlateAttachment(data)));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("X")]
    [InlineData("\n\n")]
    [InlineData("\n\r")]
    [InlineData("\r\r")]
    [InlineData("\r\n\n")]
    public void flate_stream_with_other_bytes_after_the_checksum_is_refused(string trailing)
    {
        byte[] data = TestCompat.Zlib(Plain).Concat(Encoding.ASCII.GetBytes(trailing)).ToArray();

        PdfParseException error = Assert.Throws<PdfParseException>(() => ReadAttachment(FlateAttachment(data)));
        Assert.Contains("Adler-32", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void flate_stream_with_a_wrong_checksum_is_refused(string eol)
    {
        byte[] zlib = TestCompat.Zlib(Plain);
        zlib[zlib.Length - 1] ^= 0x01;
        byte[] data = zlib.Concat(Encoding.ASCII.GetBytes(eol)).ToArray();

        Assert.Throws<PdfParseException>(() => ReadAttachment(FlateAttachment(data)));
    }

    [Fact]
    public void xref_stream_whose_length_counts_the_end_of_line_opens()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title (Counted) >>");
        builder.AppendXrefStream(5, "/Info 4 0 R", compressed => compressed.Concat(new byte[] { 0x0A }).ToArray());

        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot()), leaveOpen: true);

        Assert.Equal("Counted", document.Metadata.Title);
    }

    // Real-world fixture: Antenna House PDF Output Library 6.6 counts the LF before endstream in /Length.
    [Fact]
    public void irs_1040es_embedded_file_decodes_as_pypdf_decodes_it()
    {
        CorpusEntry entry = CorpusManifest.Get("irs-f1040es-2026");
        JsonElement expected = entry.Probe.GetProperty("attachments")[0];
        using var document = PdfDocument.Open(new MemoryStream(File.ReadAllBytes(entry.FullPath), writable: false), leaveOpen: true);

        PdfAttachment attachment = Assert.Single(document.Attachments);
        Assert.Equal("Press Quality(1).joboptions", attachment.Name);
        using Stream content = attachment.Open();
        byte[] bytes = ReadAll(content);
        Assert.Equal(15003, bytes.Length);
        Assert.Equal(CorpusManifest.Text(expected, "sha256"), CorpusManifest.Sha256(bytes));
    }

    // Real-world fixture: Acrobat Distiller 6.0.1 does the same in the font programs of the IRS 2005 Form 5500. The
    // library never decodes a font, so the decoder is called directly on object 39's stream data.
    [Fact]
    public void irs_5500_font_program_whose_length_counts_the_lf_decodes()
    {
        byte[] file = File.ReadAllBytes(CorpusManifest.Get("irs-f5500-2005").FullPath);
        byte[] header = Encoding.ASCII.GetBytes("39 0 obj\r<</Subtype/Type1C/Length 3989/Filter/FlateDecode>>stream\r\n");
        int start = IndexOf(file, header) + header.Length;
        byte[] data = new byte[3989];
        Array.Copy(file, start, data, 0, data.Length);
        Assert.Equal((byte)'\n', data[data.Length - 1]);

        byte[] decoded = PdfStreamDecoder.Decode(data, new[] { "FlateDecode" }, new PdfObject.Dict?[] { null }, PdfStreamDecoder.StructuralCap);

        byte[] strict = PdfStreamDecoder.Decode(data.Take(data.Length - 1).ToArray(), new[] { "FlateDecode" }, new PdfObject.Dict?[] { null }, PdfStreamDecoder.StructuralCap);
        Assert.Equal(strict, decoded);

        // Python's strict zlib.decompress of the first 3,988 bytes gives 4,667 bytes of CFF (header 01 00 04).
        Assert.Equal(4667, decoded.Length);
        Assert.Equal(new byte[] { 1, 0, 4 }, decoded.Take(3).ToArray());
    }

    [Fact]
    public void name_tree_key_that_is_not_a_text_string_takes_the_file_specification_name()
    {
        // "a.txt" in UTF-16LE with no byte-order mark: a valid string, but not a text string (0x00 is undefined in
        // PDFDocEncoding). Keys that are text keep naming their attachments.
        byte[] pdf = NameTreeDocument("<61002E00740078007400> 6 0 R (plain.txt) 8 0 R", "/UF (unicode-name.txt) /F (ascii-name.txt)", "/F (second.txt)");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(new[] { "unicode-name.txt", "plain.txt" }, document.Attachments.Select(a => a.Name).ToArray());
            using (Stream content = document.Attachments["unicode-name.txt"].Open())
            {
                Assert.Equal(Encoding.ASCII.GetBytes("first"), ReadAll(content));
            }

            // A later write keeps the original key bytes.
            document.Attachments.Add("added.txt", new MemoryStream(new byte[] { 1, 2, 3 }), leaveOpen: false);
            document.SaveIncremental();
        }

        string appended = Encoding.ASCII.GetString(stream.ToArray(), pdf.Length, (int)stream.Length - pdf.Length);
        Assert.Contains("<61002e00740078007400>", appended, StringComparison.OrdinalIgnoreCase);
        using var reopened = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal(new[] { "unicode-name.txt", "added.txt", "plain.txt" }, reopened.Attachments.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void name_tree_key_that_is_not_a_text_string_falls_back_to_f()
    {
        byte[] pdf = NameTreeDocument("<61002E00740078007400> 6 0 R", "/F (only-f.txt)", null);
        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);

        Assert.Equal("only-f.txt", Assert.Single(document.Attachments).Name);
    }

    [Fact]
    public void invalid_file_specification_text_is_still_refused()
    {
        byte[] pdf = NameTreeDocument("<61002E00740078007400> 6 0 R", "/UF <610000>", null);
        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);

        Assert.Throws<PdfParseException>(() => document.Attachments.Count);
    }

    // Real-world fixture: Acrobat PDFMaker 15 for Word keys its accessibility report in UTF-16LE with no byte-order mark.
    [Fact]
    public void faa_8710_1_attachment_is_named_by_its_file_specification()
    {
        CorpusEntry entry = CorpusManifest.Get("faa-8710-1");
        using var document = PdfDocument.Open(new MemoryStream(File.ReadAllBytes(entry.FullPath), writable: false), leaveOpen: true);

        PdfAttachment attachment = Assert.Single(document.Attachments);
        Assert.Equal("FAA_Form_8710-1_.pdf.accreport.html", attachment.Name);
        Assert.Equal("text/html", attachment.MimeType);
        using Stream content = attachment.Open();
        byte[] bytes = ReadAll(content);

        // The producer's own /Params /CheckSum (MD5), as qpdf reports it.
        using MD5 md5 = MD5.Create();
        Assert.Equal("783658f5ffb42a35b8c49d28cf0c7bcd", string.Concat(md5.ComputeHash(bytes).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))));
    }

    private static byte[] FlateAttachment(byte[] data)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(q.bin) 6 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(5, "/Type /EmbeddedFile /Filter /FlateDecode", data);
        builder.Indirect(6, "<< /Type /Filespec /F (q.bin) /EF << /F 5 0 R >> >>");
        return builder.Finish();
    }

    private static byte[] NameTreeDocument(string names, string firstSpec, string? secondSpec)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [" + names + "] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(5, "/Type /EmbeddedFile", Encoding.ASCII.GetBytes("first"));
        builder.Indirect(6, "<< /Type /Filespec " + firstSpec + " /EF << /F 5 0 R >> >>");
        if (secondSpec != null)
        {
            builder.StreamObject(7, "/Type /EmbeddedFile", Encoding.ASCII.GetBytes("second"));
            builder.Indirect(8, "<< /Type /Filespec " + secondSpec + " /EF << /F 7 0 R >> >>");
        }

        return builder.Finish();
    }

    private static byte[] ReadAttachment(byte[] pdf)
    {
        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        using Stream content = document.Attachments[0].Open();
        return ReadAll(content);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        throw new InvalidOperationException("The fixture does not contain the expected object.");
    }

    private static byte[] ReadAll(Stream content)
    {
        using var copy = new MemoryStream();
        content.CopyTo(copy);
        return copy.ToArray();
    }
}
