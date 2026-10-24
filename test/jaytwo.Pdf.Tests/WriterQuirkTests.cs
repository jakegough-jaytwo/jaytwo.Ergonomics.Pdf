using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using jaytwo.Pdf.Attachments;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

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
    [Trait(Lane.Trait, Lane.Corpus)]
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
    [Trait(Lane.Trait, Lane.Corpus)]
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
            using var editor = document.Edit();
            Assert.Equal(new[] { "unicode-name.txt", "plain.txt" }, document.Attachments.Select(a => a.Name).ToArray());
            using (Stream content = document.Attachments["unicode-name.txt"].Open())
            {
                Assert.Equal(Encoding.ASCII.GetBytes("first"), ReadAll(content));
            }

            // A later write keeps the original key bytes.
            editor.Attachments.Add("added.txt", new MemoryStream(new byte[] { 1, 2, 3 }), leaveOpen: false);
            editor.SaveIncremental();
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
    [Trait(Lane.Trait, Lane.Corpus)]
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

    // Real-world fixture: Acrobat's certification of a GPO enrolled bill encodes the signature /Contents in BER, with
    // indefinite lengths on the outer CMS containers, where ISO 32000-1 12.8.3.3.1 requires DER (W-72). The expected
    // values are what pdfsig 26.08.0 and pyHanko 0.38.0 report for this file.
    [Fact]
    [Trait(Lane.Trait, Lane.Corpus)]
    public void gpo_bill_signature_in_ber_is_inspected()
    {
        CorpusEntry entry = CorpusManifest.Get("gpo-bills-118hr815enr");
        byte[] pdf = File.ReadAllBytes(entry.FullPath);
        string text = TestCompat.Latin1.GetString(pdf);
        int contents = text.IndexOf("/Contents<", StringComparison.Ordinal);
        Assert.True(contents > 0);
        Assert.Equal("3080", text.Substring(contents + "/Contents<".Length, 4));
        using var document = PdfDocument.Open(new MemoryStream(pdf, writable: false), leaveOpen: true);

        PdfSignatureInfo signature = Assert.Single(document.Signatures);

        Assert.Equal("USGPOSignature", signature.FieldName);
        Assert.Equal("adbe.pkcs7.detached", signature.SubFilter);
        Assert.Equal(new PdfByteRange(0, 323818, 336016, 24060), signature.ByteRange);
        Assert.False(signature.HasLaterRevisions);
        Assert.Equal(HashAlgorithmName.SHA256, signature.DigestAlgorithm);
        Assert.Equal("1.2.840.113549.1.1.11", signature.SignatureAlgorithmOid);
        Assert.Equal(3, signature.Certificates.Count);
        Assert.Equal("a124e7f1533fb272e8744a0bedeea8c92c8170d7890f90400892e15e55e1b808", CorpusManifest.Sha256(signature.SignerCertificate!));
        Assert.Equal(new DateTimeOffset(2024, 4, 25, 23, 25, 37, TimeSpan.Zero), signature.CmsSigningTime);
        Assert.Equal(new DateTimeOffset(2024, 4, 25, 19, 25, 37, TimeSpan.FromHours(-4)), signature.SigningTime);
        Assert.Equal("Government Publishing Office", signature.Name);
        Assert.Null(signature.TimestampTime);
        Assert.Equal(1, signature.CertificationLevel);
    }

    // Real-world fixtures: pypdf 6.21.1 writes /P as the unsigned 32-bit value on every revision (D-57), here 4294966244
    // for the bits qpdf 12.3.2 reports as P = -1052: print, annotate, and fill, but not modify, copy, or assemble.
    [Theory]
    [InlineData(2, "user-secret")]
    [InlineData(2, "owner-secret")]
    [InlineData(3, "user-secret")]
    [InlineData(3, "owner-secret")]
    [InlineData(4, "user-secret")]
    [InlineData(4, "owner-secret")]
    [InlineData(5, "user-secret")]
    [InlineData(5, "owner-secret")]
    [InlineData(6, "user-secret")]
    [InlineData(6, "owner-secret")]
    public void pypdf_unsigned_permissions_read_as_qpdf_reads_them(int revision, string password)
    {
        byte[] pdf = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pypdf-unsigned-p-r" + revision.ToString(CultureInfo.InvariantCulture) + ".pdf"));
        Assert.Contains("/P 4294966244", TestCompat.Latin1.GetString(pdf), StringComparison.Ordinal);
        bool owner = password == "owner-secret";
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, password, leaveOpen: true))
        {
            Assert.Equal("print,high-quality,annotate,fill" + (revision >= 3 ? ",accessibility" : string.Empty), Describe(document.Permissions));
            using var editor = document.Edit();
            if (owner)
            {
                editor.Metadata.Title = "Owner";
            }
            else
            {
                Assert.Throws<PdfEncryptionRestrictedException>(() => editor.Metadata.Title = "Refused");
            }

            editor.AcroForm.Fill("Name", "Ada");
            editor.SaveIncremental(stream);
        }

        Assert.True(stream.ToArray().AsSpan(0, pdf.Length).SequenceEqual(pdf));
        using var reopened = PdfDocument.Open(stream, password, leaveOpen: true);
        Assert.Equal("Ada", reopened.AcroForm.GetFields("Name")[0].Value);
        Assert.Equal(owner ? "Owner" : null, reopened.Metadata.Title);
    }

    // The 32 bits drive the revision 2 to 4 file key and the revision 5 and 6 /Perms check, so a wrong reading of the
    // unsigned value fails the password. The ends of the unsigned range open as their signed spelling does.
    [Theory]
    [InlineData(2, int.MinValue)]
    [InlineData(2, -1)]
    [InlineData(3, int.MinValue)]
    [InlineData(3, -1)]
    [InlineData(4, int.MinValue)]
    [InlineData(4, -1)]
    [InlineData(5, int.MinValue)]
    [InlineData(5, -1)]
    [InlineData(6, int.MinValue)]
    [InlineData(6, -1)]
    public void unsigned_permissions_are_the_same_32_bits(int revision, int permissions)
    {
        var crypt = new StandardEncryptor(KindFor(revision), "user", "owner", permissions, encryptMetadata: true);
        string signed = crypt.Dictionary(null);
        string unsigned = signed.Replace(
            " /P " + permissions.ToString(CultureInfo.InvariantCulture) + " ",
            " /P " + unchecked((uint)permissions).ToString(CultureInfo.InvariantCulture) + " ");
        Assert.NotEqual(signed, unsigned);

        foreach (string password in new[] { "user", "owner" })
        {
            using var expected = PdfDocument.Open(new MemoryStream(EncryptedDocument(crypt, signed)), password, leaveOpen: true);
            using var actual = PdfDocument.Open(new MemoryStream(EncryptedDocument(crypt, unsigned)), password, leaveOpen: true);
            Assert.Equal("Secret", actual.Metadata.Title);
            Assert.Equal(Describe(expected.Permissions), Describe(actual.Permissions));
        }
    }

    [Theory]
    [InlineData("4294967296")]
    [InlineData("-2147483649")]
    [InlineData("4294967292.0")]
    [InlineData("(4294967292)")]
    public void permissions_outside_32_bits_are_refused(string value)
    {
        var crypt = new StandardEncryptor(StandardEncryptor.Kind.Revision6, "user", "owner", -4, encryptMetadata: true);
        string dictionary = crypt.Dictionary(null);
        Assert.Contains(" /P -4 ", dictionary, StringComparison.Ordinal);
        byte[] pdf = EncryptedDocument(crypt, dictionary.Replace(" /P -4 ", " /P " + value + " "));

        PdfEncryptedException error = Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(new MemoryStream(pdf), "user", leaveOpen: true));
        Assert.Equal("This encryption is not supported.", error.Message);
    }

    private static byte[] EncryptedDocument(StandardEncryptor crypt, string encrypt)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Indirect(20, encrypt);
        return builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private static StandardEncryptor.Kind KindFor(int revision)
    {
        return revision switch
        {
            2 => StandardEncryptor.Kind.Revision2,
            3 => StandardEncryptor.Kind.Revision3,
            4 => StandardEncryptor.Kind.Revision4Aes,
            5 => StandardEncryptor.Kind.Revision5,
            _ => StandardEncryptor.Kind.Revision6,
        };
    }

    private static string Describe(PdfPermissions permissions)
    {
        var granted = new List<string>();
        Add(permissions.CanPrint, "print");
        Add(permissions.CanPrintHighQuality, "high-quality");
        Add(permissions.CanModify, "modify");
        Add(permissions.CanCopy, "copy");
        Add(permissions.CanAnnotate, "annotate");
        Add(permissions.CanFillForms, "fill");
        Add(permissions.CanAssemble, "assemble");
        Add(permissions.CanExtractForAccessibility, "accessibility");
        return string.Join(",", granted);

        void Add(bool allowed, string name)
        {
            if (allowed)
            {
                granted.Add(name);
            }
        }
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
