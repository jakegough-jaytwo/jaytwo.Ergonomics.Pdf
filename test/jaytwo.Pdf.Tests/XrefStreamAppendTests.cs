using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-117, D-56: an update to a source whose section at startxref is a cross-reference stream ends in a cross-reference
// stream (ISO 32000-1 7.5.8); classic and hybrid sources keep a classic table. The appended section is read here
// without the library: the dictionary by pattern, and the rows inflated by DeflateStream past the zlib header.
public class XrefStreamAppendTests
{
    [Fact]
    public void save_after_a_cross_reference_stream_appends_one_that_lists_itself_and_chains()
    {
        byte[] pdf = StreamSource();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        byte[] first;
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.Title = "One";
            editor.SaveIncremental(stream);
            first = stream.ToArray();
            editor.Metadata.Title = "Two";
            editor.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.True(first.AsSpan(0, pdf.Length).SequenceEqual(pdf), "the original is not a prefix");
        Assert.True(saved.AsSpan(0, first.Length).SequenceEqual(first), "the first update is not a prefix");
        string appendix = TestCompat.Latin1.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.DoesNotContain("trailer", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("xref\r\n", appendix, StringComparison.Ordinal);

        XrefSection one = LastSection(first);
        Assert.Equal(LastStartXref(pdf), one.Prev);
        Assert.Equal(one.Rows.Keys.Max(), one.Id);
        Assert.Equal(one.Id + 1, one.Size);
        Assert.Equal(one.Offset, one.Rows[one.Id].Offset);
        Assert.Contains("/Root 1 0 R\n", one.Dictionary, StringComparison.Ordinal);
        Assert.Contains("/Info 4 0 R\n", one.Dictionary, StringComparison.Ordinal);
        Assert.DoesNotContain("/Encrypt", one.Dictionary, StringComparison.Ordinal);
        AssertRowsPointAtObjects(first, one);

        XrefSection two = LastSection(saved);
        Assert.Equal(one.Offset, two.Prev);
        Assert.Equal(two.Rows.Keys.Max(), two.Id);
        Assert.True(two.Id > one.Id);
        Assert.Equal(two.Id + 1, two.Size);
        AssertRowsPointAtObjects(saved, two);

        using var again = PdfDocument.Open(new MemoryStream(saved, writable: false));
        Assert.Equal("Two", again.Metadata.Title);
        Assert.Equal(3, again.Revisions.Count);
        Assert.Equal(saved.Length, again.Revisions[2].EndOffset);
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("hybrid")]
    public void classic_and_hybrid_sources_still_append_a_classic_table(string shape)
    {
        byte[] pdf = shape == "classic" ? ClassicSource() : HybridSource();
        byte[] saved = Save(pdf, null, editor => editor.Metadata.Title = "Edited");

        string appendix = TestCompat.Latin1.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("xref\r\n", appendix, StringComparison.Ordinal);
        Assert.Matches("trailer\n<<\n/Size \\d+\n/Prev " + LastStartXref(pdf).ToString(CultureInfo.InvariantCulture) + "\n", appendix);
        Assert.DoesNotContain("/Type /XRef", appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(new MemoryStream(saved, writable: false));
        Assert.Equal("Edited", again.Metadata.Title);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("owner")]
    public void encrypted_source_appends_a_plaintext_cross_reference_stream(string password)
    {
        var crypt = new StandardEncryptor(true, "user", "owner", -4, encryptMetadata: true);
        var builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Indirect(20, crypt.Dictionary(null));
        builder.AppendXrefStream(21, "/Encrypt 20 0 R /Info 4 0 R /ID [" + crypt.IdLiteral() + "]");
        byte[] pdf = builder.Snapshot();

        byte[] saved = Save(pdf, password, editor => editor.Metadata.Title = "Edited");

        XrefSection section = LastSection(saved);
        Assert.Equal(section.Rows.Keys.Max(), section.Id);
        Assert.True(section.Id >= 22);
        Assert.Contains("/Encrypt 20 0 R\n", section.Dictionary, StringComparison.Ordinal);
        Assert.Contains("/ID [<", section.Dictionary, StringComparison.Ordinal);
        AssertRowsPointAtObjects(saved, section);
        foreach (string reader in new[] { "user", "owner" })
        {
            using var again = PdfDocument.Open(new MemoryStream(saved, writable: false), reader);
            Assert.Equal("Edited", again.Metadata.Title);
        }
    }

    [Fact]
    public void signatures_after_a_cross_reference_stream_cover_the_whole_file()
    {
        byte[] pdf = StreamSource();
        using var key = RSA.Create(2048);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=W-117", key);
        var provider = new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData));
        byte[] signed = Sign(pdf, null, new PdfSignatureOptions { FieldName = "Approval" }, provider);
        byte[] countersigned = Sign(signed, null, new PdfSignatureOptions { FieldName = "Second" }, provider);

        XrefSection first = LastSection(signed);
        Assert.Equal(LastStartXref(pdf), first.Prev);
        AssertRowsPointAtObjects(signed, first);
        XrefSection second = LastSection(countersigned);
        Assert.Equal(first.Offset, second.Prev);
        Assert.True(second.Id > first.Id);
        AssertRowsPointAtObjects(countersigned, second);

        using var document = PdfDocument.Open(new MemoryStream(countersigned, writable: false));
        Assert.Equal(3, document.Revisions.Count);
        Assert.Collection(
            document.Signatures,
            one => AssertCovers(one, signed.Length, later: true),
            two => AssertCovers(two, countersigned.Length, later: false));
    }

    // The provider's contents are not a timestamp token, so the revision is checked by its /ByteRange alone.
    [Fact]
    public void document_timestamp_after_a_cross_reference_stream_appends_one()
    {
        byte[] pdf = StreamSource();
        byte[] stamped = Sign(pdf, null, new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 32 }, new TimeStampProvider());

        XrefSection section = LastSection(stamped);
        Assert.Equal(LastStartXref(pdf), section.Prev);
        AssertRowsPointAtObjects(stamped, section);
        Match range = Regex.Match(TestCompat.Latin1.GetString(stamped, pdf.Length, stamped.Length - pdf.Length), @"/ByteRange \[0+ \d+ (\d+) (\d+)\]");
        Assert.True(range.Success);
        Assert.Equal(stamped.Length, long.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture) + long.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    // On qpdf's object-stream rewrite of a form, plain and AES-256: qpdf checks each update, pypdf reads the fill,
    // pdfsig and pyHanko verify the signature, and after a later fill pyHanko finds it intact with form filling only.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData(null)]
    [InlineData("user")]
    public void qpdf_pypdf_pdfsig_and_pyhanko_accept_updates_after_a_cross_reference_stream(string? password)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string form = Path.Combine(directory, "form.pdf");
            G7Fixtures.WriteForm(form, 10, "Form");
            var args = new List<string> { "--object-streams=generate" };
            if (password != null)
            {
                args.AddRange(new[] { "--encrypt", "user", "owner", "256", "--" });
            }

            byte[] source = Qpdf(directory, File.ReadAllBytes(form), args.ToArray());
            Assert.False(IText.EndsInClassicXref(source));

            byte[] filled = Save(source, password, editor => editor.AcroForm.Fill("page1.name", "Ada"));
            QpdfCheck(directory, filled, password);
            JsonElement values = Python(directory, filled, "form_values.py", password == null ? Array.Empty<string>() : new[] { password }).RootElement;
            Assert.Equal("Ada", values.GetProperty("page1.name").GetProperty("value").GetString());

            using var key = RSA.Create(2048);
            using X509Certificate2 certificate = TestKeys.SelfSigned("CN=W-117", key);
            byte[] signed = Sign(filled, password, new PdfSignatureOptions { FieldName = "Approval" }, new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)));
            QpdfCheck(directory, signed, password);
            AssertPdfsig(signed, certificate, password, Pdfsig.WholeFile);
            string report = PyHanko(directory, signed, password, "entire", null);
            Assert.Contains("intact=true", report, StringComparison.Ordinal);

            byte[] revised = Save(signed, password, editor => editor.AcroForm.Fill("page2.name", "Grace"));
            QpdfCheck(directory, revised, password);
            AssertPdfsig(revised, certificate, password, Pdfsig.NotWholeFile);
            report = PyHanko(directory, revised, password, "revision", "docmdp-ok,modification=FORM_FILLING");
            Assert.Contains("intact=true", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertPdfsig(byte[] signed, X509Certificate2 certificate, string? password, string coverage)
    {
        (int exitCode, string report) = Pdfsig.Verify(signed, new[] { certificate.RawData }, password);
        List<string> signature = Assert.Single(Pdfsig.Signatures(report));
        Assert.True(exitCode == 0, report);
        Assert.Contains(Pdfsig.Valid, signature);
        Assert.Contains(coverage, signature);
    }

    private static byte[] Qpdf(string directory, byte[] pdf, params string[] options)
    {
        string input = Path.Combine(directory, "qpdf-input.pdf");
        string output = Path.Combine(directory, "qpdf-output.pdf");
        File.WriteAllBytes(input, pdf);
        var args = new List<string>(options) { input, output };
        using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), args.ToArray());
        (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
        Assert.True(exitCode == 0, stderr + stdout);
        return File.ReadAllBytes(output);
    }

    private static void QpdfCheck(string directory, byte[] pdf, string? password)
    {
        string path = Path.Combine(directory, "check.pdf");
        File.WriteAllBytes(path, pdf);
        var args = new List<string> { "--check" };
        if (password != null)
        {
            args.Add("--password=" + password);
        }

        args.Add(path);
        using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), args.ToArray());
        (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
        Assert.True(exitCode == 0, "qpdf --check: " + stderr + stdout);
    }

    private static JsonDocument Python(string directory, byte[] pdf, string script, params string[] extra)
    {
        string path = Path.Combine(directory, "read.pdf");
        File.WriteAllBytes(path, pdf);
        var args = new List<string> { ExternalTool.Fixture(script), path };
        args.AddRange(extra);
        using Process process = ExternalTool.Start(ExternalTool.Python(), args.ToArray());
        return JsonDocument.Parse(ExternalTool.Output(process));
    }

    private static string PyHanko(string directory, byte[] pdf, string? password, string coverage, string? checks)
    {
        string path = Path.Combine(directory, "signed.pdf");
        File.WriteAllBytes(path, pdf);
        var args = new List<string> { ExternalTool.Fixture("validate_signature.py"), path, "-", password ?? "-", coverage };
        if (checks != null)
        {
            args.Add(checks);
        }

        using Process process = ExternalTool.Start(ExternalTool.Python(), args.ToArray());
        return ExternalTool.Output(process);
    }

    private static void AssertCovers(PdfSignatureInfo signature, long end, bool later)
    {
        PdfByteRange range = Assert.IsType<PdfByteRange>(signature.ByteRange);
        Assert.Equal(0, range.Offset0);
        Assert.Equal(end, range.Offset1 + range.Length1);
        Assert.Equal(later, signature.HasLaterRevisions);
    }

    // Every row is a type 1 entry whose offset holds that object: "N G obj".
    private static void AssertRowsPointAtObjects(byte[] pdf, XrefSection section)
    {
        foreach (KeyValuePair<int, (long Offset, int Generation)> row in section.Rows)
        {
            string header = row.Key.ToString(CultureInfo.InvariantCulture) + " " + row.Value.Generation.ToString(CultureInfo.InvariantCulture) + " obj";
            Assert.Equal(header, TestCompat.Latin1.GetString(pdf, (int)row.Value.Offset, header.Length));
        }
    }

    private static XrefSection LastSection(byte[] pdf)
    {
        long offset = LastStartXref(pdf);
        string text = TestCompat.Latin1.GetString(pdf, (int)offset, pdf.Length - (int)offset);
        Match match = Regex.Match(text, @"^(\d+) 0 obj\n<<\n/Type /XRef\n(.*?)>>\nstream\n", RegexOptions.Singleline);
        Assert.True(match.Success, "the section at startxref is not a cross-reference stream: " + text.Substring(0, Math.Min(200, text.Length)));
        string dictionary = match.Groups[2].Value;
        int[] widths = Numbers(dictionary, @"/W \[([^\]]*)\]");
        int[] index = Numbers(dictionary, @"/Index \[([^\]]*)\]");
        Assert.Contains("/Filter /FlateDecode\n", dictionary, StringComparison.Ordinal);
        Assert.Equal(1, widths[0]);
        int length = Numbers(dictionary, @"/Length (\d+)")[0];
        byte[] rows = Inflate(pdf, (int)offset + match.Length, length);
        int rowWidth = widths.Sum();
        var entries = new Dictionary<int, (long, int)>();
        int position = 0;
        for (int i = 0; i < index.Length; i += 2)
        {
            for (int id = index[i]; id < index[i] + index[i + 1]; id++)
            {
                Assert.Equal(1, rows[position]);
                entries.Add(id, (BigEndian(rows, position + 1, widths[1]), (int)BigEndian(rows, position + 1 + widths[1], widths[2])));
                position += rowWidth;
            }
        }

        Assert.Equal(rows.Length, position);
        return new XrefSection(
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            offset,
            dictionary,
            Numbers(dictionary, @"/Size (\d+)")[0],
            long.Parse(Regex.Match(dictionary, @"/Prev (\d+)").Groups[1].Value, CultureInfo.InvariantCulture),
            entries);
    }

    private static int[] Numbers(string dictionary, string pattern)
    {
        Match match = Regex.Match(dictionary, pattern);
        Assert.True(match.Success, pattern + " is missing from " + dictionary);
        return match.Groups[1].Value.Split(' ').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
    }

    private static byte[] Inflate(byte[] pdf, int start, int length)
    {
        using var deflated = new MemoryStream(pdf, start + 2, length - 6, writable: false);
        using var inflate = new DeflateStream(deflated, CompressionMode.Decompress);
        using var rows = new MemoryStream();
        inflate.CopyTo(rows);
        return rows.ToArray();
    }

    private static long BigEndian(byte[] data, int offset, int width)
    {
        long value = 0;
        for (int i = 0; i < width; i++)
        {
            value = (value << 8) | data[offset + i];
        }

        return value;
    }

    private static long LastStartXref(byte[] pdf)
    {
        MatchCollection matches = Regex.Matches(TestCompat.Latin1.GetString(pdf), @"startxref\s+(\d+)\s+%%EOF");
        return long.Parse(matches[matches.Count - 1].Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static byte[] Save(byte[] pdf, string? password, Action<PdfDocumentEditor> edit)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        using var editor = document.Edit();
        edit(editor);
        using var destination = new MemoryStream();
        editor.SaveIncremental(destination, leaveOpen: true);
        byte[] saved = destination.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf), "the original is not a prefix");
        return saved;
    }

    private static byte[] Sign(byte[] pdf, string? password, PdfSignatureOptions options, IPdfSignatureProvider provider)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        using var editor = document.Edit();
        using var destination = new MemoryStream();
        editor.Sign(destination, options, provider, leaveOpen: true);
        byte[] signed = destination.ToArray();
        Assert.True(signed.AsSpan(0, pdf.Length).SequenceEqual(pdf), "the original is not a prefix");
        return signed;
    }

    private static byte[] StreamSource()
    {
        PdfBuilder builder = PdfBuilder.Basic(pageExtra: " /Resources << >>");
        builder.Indirect(4, "<< /Title (Base) >>");
        builder.AppendXrefStream(5, "/Info 4 0 R");
        return builder.Snapshot();
    }

    private static byte[] ClassicSource()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (Base) >>");
        builder.Indirect(5, "<< >>");
        return builder.Finish("/Info 4 0 R");
    }

    // A classic table at startxref whose /XRefStm names a cross-reference stream (ISO 32000-1 7.5.8.4).
    private static byte[] HybridSource()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (Base) >>");
        long baseXref = builder.Length;
        builder.Finish("/Info 4 0 R");
        builder.Indirect(4, "<< /Title (Stream) >>");
        long streamAt = builder.AppendXrefStream(5, "/Info 4 0 R");
        builder.Indirect(4, "<< /Title (Classic) >>");
        long hybridXref = builder.Length;
        builder.Ascii("xref\n4 1\n" + builder.OffsetOf(4).ToString("D10", CultureInfo.InvariantCulture) + " 00000 n\r\n"
            + "trailer\n<< /Size 6 /Root 1 0 R /Info 4 0 R /Prev " + baseXref.ToString(CultureInfo.InvariantCulture)
            + " /XRefStm " + streamAt.ToString(CultureInfo.InvariantCulture) + " >>\nstartxref\n" + hybridXref.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        return builder.Snapshot();
    }

    private sealed class XrefSection
    {
        public XrefSection(int id, long offset, string dictionary, int size, long prev, Dictionary<int, (long Offset, int Generation)> rows)
        {
            Id = id;
            Offset = offset;
            Dictionary = dictionary;
            Size = size;
            Prev = prev;
            Rows = rows;
        }

        public int Id { get; }

        public long Offset { get; }

        public string Dictionary { get; }

        public int Size { get; }

        public long Prev { get; }

        public Dictionary<int, (long Offset, int Generation)> Rows { get; }
    }

    private sealed class TimeStampProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.DocTimeStamp;

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(new byte[] { 1, 2, 3, 4 });
        }
    }
}
