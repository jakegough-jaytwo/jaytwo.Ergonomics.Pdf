using System;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Regression tests for B-18 fuzz smoke findings FZ-1 to FZ-7. A malformed file throws a document exception
// (README limits row), never a framework exception from the decoder or the source stream.
public class FuzzRegressionTests
{
    // FZ-1: corrupt Flate data in an object stream escaped as System.IO.InvalidDataException.
    [Theory]
    [InlineData("header")]
    [InlineData("deflate")]
    [InlineData("checksum")]
    [InlineData("truncated")]
    public void corrupt_flate_object_stream_throws_parse_exception(string kind)
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.StreamObject(5, "/Type /ObjStm /N 1 /First 4 /Filter /FlateDecode", Corrupt(kind, Encoding.ASCII.GetBytes("4 0 << /Title (FromStream) >>")));
        builder.Compressed(4, 5, 0);
        builder.AppendXrefStream(6, "/Info 4 0 R");

        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot()));

        Assert.Throws<PdfParseException>(() => document.Metadata.Title);
    }

    // FZ-1: the streaming decoder used by attachments had the same escape.
    [Theory]
    [InlineData("header")]
    [InlineData("deflate")]
    [InlineData("checksum")]
    [InlineData("truncated")]
    public void corrupt_flate_attachment_throws_parse_exception(string kind)
    {
        PdfBuilder builder = PdfBuilder.Basic(" /Names << /EmbeddedFiles << /Names [(a.txt) 4 0 R] >> >>");
        builder.Indirect(4, "<< /Type /Filespec /F (a.txt) /EF << /F 5 0 R >> >>");
        builder.StreamObject(5, "/Type /EmbeddedFile /Filter /FlateDecode", Corrupt(kind, Encoding.ASCII.GetBytes(new string('x', 4096))));

        using var document = PdfDocument.Open(new MemoryStream(builder.Finish()));

        Assert.Throws<PdfParseException>(() =>
        {
            using Stream content = document.Attachments["a.txt"].Open();
            content.CopyTo(Stream.Null);
        });
    }

    // FZ-2: a stream /Length that ran past 2 GiB made PdfCursor seek a MemoryStream out of range, which threw
    // ArgumentOutOfRangeException from Open.
    [Fact]
    public void xref_stream_length_past_the_end_throws_parse_exception()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        long xrefAt = builder.Length;
        builder.Ascii("4 0 obj\n<< /Type /XRef /Size 5 /W [1 4 2] /Root 1 0 R /Length 4294967296 >>\nstream\n\0\0\0\0\0\0\0\nendstream\nendobj\n"
            + "startxref\n" + xrefAt.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");

        Assert.Throws<PdfParseException>(() => PdfDocument.Open(new MemoryStream(builder.Snapshot())));
    }

    // FZ-3: XML that parses but has no rdf:RDF made an info edit throw PdfXmpException after the info dictionary had
    // already changed. It is malformed XMP, so the edit succeeds and the packet stays byte for byte.
    [Fact]
    public void xmp_without_rdf_stays_byte_for_byte_while_info_updates()
    {
        const string Packet = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><x:other/></x:xmpmeta>";
        PdfBuilder builder = PdfBuilder.Basic(" /Metadata 5 0 R");
        builder.Indirect(4, "<< /Title (Old) >>");
        builder.StreamObject(5, "/Type /Metadata /Subtype /XML", Encoding.ASCII.GetBytes(Packet));
        byte[] pdf = builder.Finish("/Info 4 0 R");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.Title = "New";
            editor.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.Equal(1, Count(saved, Encoding.ASCII.GetBytes(Packet)));
        using var again = PdfDocument.Open(new MemoryStream(saved));
        Assert.Equal("New", again.Metadata.Title);
    }

    // FZ-4: a CreationDate field outside its range (here hour 11, minute 99, second 99) made ParseDate throw
    // ArgumentOutOfRangeException. An unparseable date already throws PdfParseException, so an out-of-range one does too.
    [Theory]
    [InlineData("D:20260101119999+01'00'")]
    [InlineData("D:20261301000000Z")]
    [InlineData("D:20260100000000Z")]
    [InlineData("D:20260231000000Z")]
    [InlineData("D:20260101250000Z")]
    public void out_of_range_creation_date_throws_parse_exception(string date)
    {
        using var document = PdfDocument.Open(new MemoryStream(InfoPdf("/CreationDate (" + date + ")")));

        Assert.Throws<PdfParseException>(() => document.Metadata.CreationDate);
    }

    // FZ-5: the same defect through ModificationDate (month 79, day 03 in the artifact).
    [Theory]
    [InlineData("D:20261003790381Z")]
    [InlineData("D:20267903000000Z")]
    [InlineData("D:20260101000060Z")]
    public void out_of_range_modification_date_throws_parse_exception(string date)
    {
        using var document = PdfDocument.Open(new MemoryStream(InfoPdf("/ModDate (" + date + ")")));

        Assert.Throws<PdfParseException>(() => document.Metadata.ModificationDate);
    }

    // FZ-6: a revision 6 /Perms value that is not exactly 16 bytes reached AesEcb and threw CryptographicException. It is a
    // malformed encryption dictionary, so Open throws PdfEncryptedException.
    [Theory]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(33)]
    public void aes256_perms_of_the_wrong_length_throws_encrypted_exception(int length)
    {
        var crypt = new StandardEncryptor(true, string.Empty, "owner", -4, encryptMetadata: true);
        string dictionary = System.Text.RegularExpressions.Regex.Replace(
            crypt.Dictionary(null),
            "/Perms <[0-9A-Fa-f]+>",
            "/Perms <" + new string('3', length * 2) + ">");
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(20, dictionary);
        byte[] pdf = builder.Finish("/Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");

        Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(new MemoryStream(pdf)));
    }

    // FZ-7: /Info that resolves to a name read as absent but made an info edit throw InvalidCastException. The edit
    // replaces the unusable /Info with a new dictionary.
    [Fact]
    public void non_dictionary_info_reads_absent_and_an_edit_replaces_it()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "/NotADictionary");
        byte[] pdf = builder.Finish("/Info 4 0 R");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Null(document.Metadata.Title);
            editor.Metadata.Title = "New";
            editor.SaveIncremental(stream);
        }

        using var again = PdfDocument.Open(new MemoryStream(stream.ToArray()));
        Assert.Equal("New", again.Metadata.Title);
    }

    // FZ-7, second defect in the same artifact: a catalog /Names that resolves to a name read as no name tree, but an
    // attachment add cast it to a dictionary. The add replaces the unusable /Names.
    [Fact]
    public void non_dictionary_names_reads_absent_and_an_attachment_add_replaces_it()
    {
        PdfBuilder builder = PdfBuilder.Basic(" /Names 4 0 R");
        builder.Indirect(4, "/NotADictionary");
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Empty(document.Attachments);
            editor.Attachments.Add("a.txt", new MemoryStream(Encoding.ASCII.GetBytes("hi")));
            editor.SaveIncremental(stream);
        }

        using var again = PdfDocument.Open(new MemoryStream(stream.ToArray()));
        Assert.Single(again.Attachments);
    }

    private static byte[] InfoPdf(string infoEntries)
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< " + infoEntries + " >>");
        return builder.Finish("/Info 4 0 R");
    }

    private static byte[] Corrupt(string kind, byte[] plain)
    {
        byte[] zlib = TestCompat.Zlib(plain);
        switch (kind)
        {
            case "header":
                zlib[0] = 0x00;
                return zlib;
            case "deflate":
                // A block type of 3 is reserved, so DeflateStream rejects the data.
                return new byte[] { 0x78, 0x9C, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 };
            case "checksum":
                zlib[zlib.Length - 1] ^= 0xFF;
                return zlib;
            default:
                Array.Resize(ref zlib, zlib.Length / 2);
                return zlib;
        }
    }

    private static int Count(byte[] haystack, byte[] needle)
    {
        int count = 0;
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j])
            {
                j++;
            }

            if (j == needle.Length)
            {
                count++;
            }
        }

        return count;
    }
}
