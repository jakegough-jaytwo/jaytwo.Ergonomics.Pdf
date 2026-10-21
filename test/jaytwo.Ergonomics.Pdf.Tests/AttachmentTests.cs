using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class AttachmentTests
{
    [Fact]
    public void binary_attachment_round_trips_size_and_sorted_names()
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        var first = new byte[200 * 1024];
        for (int i = 0; i < first.Length; i++)
        {
            first[i] = (byte)(i & 0xFF);
        }

        var second = new byte[] { 0x00, 0x7F, 0xFF };
        var probe = new ProbeStream(new MemoryStream(first));
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Attachments.Add("b.bin", probe, leaveOpen: true, new PdfAttachmentOptions { Description = "beta", MimeType = "application/octet-stream" });
            document.Attachments.Add("a.bin", new MemoryStream(second), leaveOpen: true);
            document.SaveIncremental(stream);
        }

        Assert.True(probe.MaxRequested <= 65536);
        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal(2, again.Attachments.Count);
        Assert.Equal("a.bin", again.Attachments[0].Name);
        Assert.Equal("b.bin", again.Attachments[1].Name);
        Assert.Equal(second.Length, again.Attachments[0].Size);
        Assert.Equal(first.Length, again.Attachments[1].Size);
        Assert.Equal("beta", again.Attachments[1].Description);
        Assert.Equal("application/octet-stream", again.Attachments[1].MimeType);
        Assert.Equal("beta", again.Attachments["b.bin"].Description);
        Assert.True(again.Attachments.TryGetValue("a.bin", out PdfAttachment? named));
        Assert.NotNull(named);
        Assert.Equal(second.Length, named!.Size);
        Assert.Throws<PdfAttachmentNotFoundException>(() => again.Attachments["missing.bin"].Open());
        using (var opened = again.Attachments[0].Open())
        {
            using var copy = new MemoryStream();
            opened.CopyTo(copy);
            Assert.Equal(second, copy.ToArray());
        }

        again.Attachments.Remove("a.bin");
        again.SaveIncremental(stream);
        using var removed = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Single(removed.Attachments);
        Assert.Equal("b.bin", removed.Attachments[0].Name);
        Assert.Throws<PdfAttachmentNotFoundException>(() => removed.Attachments.Remove("missing.bin"));
    }

    [Fact]
    public void flate_attachment_is_inflated_and_dct_is_refused()
    {
        byte[] plain = Encoding.ASCII.GetBytes("flate-bytes");
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(notes.bin) 6 0 R] >> >> /AF [6 0 R] >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(5, "/Type /EmbeddedFile /Filter /FlateDecode /Params << /Size " + plain.Length.ToString() + " >>", Zlib(plain));
        builder.Indirect(6, "<< /Type /Filespec /F (notes.bin) /UF (notes.bin) /EF << /F 5 0 R /UF 5 0 R >> >>");
        byte[] pdf = builder.Finish();

        using (var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true))
        {
            Assert.Equal(plain.Length, document.Attachments[0].Size);
            using var opened = document.Attachments[0].Open();
            using var copy = new MemoryStream();
            opened.CopyTo(copy);
            Assert.Equal(plain, copy.ToArray());
        }

        var dct = new PdfBuilder();
        dct.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(pic.jpg) 6 0 R] >> >> >>");
        dct.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        dct.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        dct.StreamObject(5, "/Type /EmbeddedFile /Filter /DCTDecode /Params << /Size 4 >>", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
        dct.Indirect(6, "<< /Type /Filespec /UF (pic.jpg) /EF << /UF 5 0 R >> >>");
        byte[] jpeg = dct.Finish();
        using var jpegDocument = PdfDocument.Open(new MemoryStream(jpeg), leaveOpen: true);
        Assert.Equal(4, jpegDocument.Attachments[0].Size);
        Assert.Throws<PdfUnsupportedFilterException>(() => jpegDocument.Attachments[0].Open());
    }

    [Fact]
    public void non_seekable_attachment_is_rejected()
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        var probe = new ProbeStream(new MemoryStream(new byte[] { 1 }), allowRead: false);

        Assert.Throws<ArgumentException>(() => document.Attachments.Add("a.bin", probe));
    }

    // ISO 32000-1 7.9.6 compares name-tree keys byte by byte and requires one self-consistent key encoding (W-86).
    // "usage.txt" sorts after "Press.txt" as text, but before its UTF-16BE key as bytes when written in PDFDocEncoding.
    [Fact]
    public void attachment_added_beside_a_utf16be_key_is_keyed_in_utf16be()
    {
        byte[] pdf = NameTreeDocument("<FEFF00500072006500730073002E007400780074> 6 0 R");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Attachments.Add("usage.txt", new MemoryStream(Encoding.ASCII.GetBytes("added")));
            document.SaveIncremental();
        }

        string keys = WrittenKeys(stream, pdf.Length);
        Assert.Contains("<FEFF00500072006500730073002E007400780074>", keys, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<FEFF00750073006100670065002E007400780074>", keys, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("(usage.txt)", keys, StringComparison.Ordinal);
        using var reopened = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal(new[] { "Press.txt", "usage.txt" }, reopened.Attachments.Select(a => a.Name).ToArray());
        Assert.Equal(Encoding.ASCII.GetBytes("first"), ReadAll(reopened.Attachments["Press.txt"]));
        Assert.Equal(Encoding.ASCII.GetBytes("added"), ReadAll(reopened.Attachments["usage.txt"]));
    }

    // A new tree is keyed in UTF-16BE, which holds any name, so a later name cannot mix encodings in it.
    [Fact]
    public void new_name_tree_is_keyed_in_utf16be()
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Attachments.Add("z.txt", new MemoryStream(new byte[] { 1 }));
            document.SaveIncremental();
        }

        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Attachments.Add("Bericht Ä.txt", new MemoryStream(new byte[] { 2 }));
            document.SaveIncremental();
        }

        string keys = WrittenKeys(stream, pdf.Length);
        Assert.Contains("<FEFF007A002E007400780074>", keys, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("(z.txt)", keys, StringComparison.Ordinal);
        using var reopened = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal(new[] { "Bericht Ä.txt", "z.txt" }, reopened.Attachments.Select(a => a.Name).ToArray());
    }

    // A tree keyed in PDFDocEncoding takes new keys in PDFDocEncoding when the name has a code in it, and a
    // replaced attachment keeps the key the tree already held.
    [Fact]
    public void attachment_added_to_a_pdfdoc_name_tree_is_keyed_in_pdfdoc()
    {
        byte[] pdf = NameTreeDocument("(a.txt) 6 0 R (z.txt) 8 0 R");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Attachments.Add("Résumé.txt", new MemoryStream(Encoding.ASCII.GetBytes("added")));
            document.Attachments.Add("a.txt", new MemoryStream(Encoding.ASCII.GetBytes("replaced")));
            document.SaveIncremental();
        }

        string keys = WrittenKeys(stream, pdf.Length);
        Assert.Contains("<52E973756DE92E747874>", keys, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(a.txt)", keys, StringComparison.Ordinal);
        Assert.Contains("(z.txt)", keys, StringComparison.Ordinal);
        Assert.DoesNotContain("<FEFF", keys, StringComparison.OrdinalIgnoreCase);
        using var reopened = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal(new[] { "Résumé.txt", "a.txt", "z.txt" }, reopened.Attachments.Select(a => a.Name).ToArray());
        Assert.Equal(Encoding.ASCII.GetBytes("replaced"), ReadAll(reopened.Attachments["a.txt"]));
    }

    // A file listed only in /AF enters the name tree on the next attachment write, keyed like any key the library adds.
    [Fact]
    public void file_listed_only_in_af_is_keyed_in_the_tree_encoding()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AF [6 0 R] >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(5, "/Type /EmbeddedFile", Encoding.ASCII.GetBytes("first"));
        builder.Indirect(6, "<< /Type /Filespec /F (only-af.txt) /UF (only-af.txt) /EF << /F 5 0 R >> >>");
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Attachments.Add("b.txt", new MemoryStream(new byte[] { 1 }));
            document.SaveIncremental();
        }

        string keys = WrittenKeys(stream, pdf.Length);
        Assert.Contains("<FEFF006F006E006C0079002D00610066002E007400780074>", keys, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<FEFF0062002E007400780074>", keys, StringComparison.OrdinalIgnoreCase);
        using var reopened = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal(new[] { "b.txt", "only-af.txt" }, reopened.Attachments.Select(a => a.Name).ToArray());
    }

    // Documented limitation: a name with no PDFDocEncoding code is UTF-16BE even in a PDFDocEncoded tree, and the keys
    // stay in byte order, so it follows every PDFDocEncoded key. The library does not re-encode keys it did not add.
    [Fact]
    public void name_outside_pdfdoc_in_a_pdfdoc_tree_is_utf16be_and_sorts_by_bytes()
    {
        byte[] pdf = NameTreeDocument("(z.txt) 6 0 R");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Attachments.Add("A日本.txt", new MemoryStream(new byte[] { 1 }));
            document.SaveIncremental();
        }

        string keys = WrittenKeys(stream, pdf.Length);
        Assert.Contains("(z.txt)", keys, StringComparison.Ordinal);
        Assert.Contains("<FEFF004165E5672C002E007400780074>", keys, StringComparison.OrdinalIgnoreCase);
        using var reopened = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal(new[] { "z.txt", "A日本.txt" }, reopened.Attachments.Select(a => a.Name).ToArray());
    }

    // Real-world fixture (W-86): the IRS 1040-ES keys "Press Quality(1).joboptions" in UTF-16BE. qpdf 12.4.2 compares
    // keys as text, validates the order in --check, and finds a key by binary search; pypdf reads the tree in order.
    [Fact]
    public void irs_1040es_attachment_added_after_its_utf16be_key_is_found_by_qpdf_and_pypdf()
    {
        CorpusEntry entry = CorpusManifest.Get("irs-f1040es-2026");
        const string existing = "Press Quality(1).joboptions";
        const string added = "usage-rights.txt";
        byte[] content = Encoding.ASCII.GetBytes("added beside a UTF-16BE key");
        byte[] original = File.ReadAllBytes(entry.FullPath);
        var destination = new MemoryStream();
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(original, writable: false), entry.Password))
        {
            document.Attachments.Add(added, new MemoryStream(content));
            document.SaveIncremental(destination, leaveOpen: true);
        }

        byte[] output = destination.ToArray();
        Assert.True(output.AsSpan(0, original.Length).SequenceEqual(original));
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "output.pdf");
            File.WriteAllBytes(path, output);
            (int exitCode, string stdout, string stderr) = Qpdf("--check", path);
            Assert.True(exitCode == 0 && !(stdout + stderr).Contains("WARNING"), "qpdf --check exited " + exitCode.ToString() + Environment.NewLine + stderr + stdout);
            (exitCode, stdout, stderr) = Qpdf("--show-attachment=" + added, path);
            Assert.True(exitCode == 0, "qpdf did not find " + added + Environment.NewLine + stderr);
            Assert.Equal(Encoding.ASCII.GetString(content), stdout);
            (exitCode, _, stderr) = Qpdf("--show-attachment=" + existing, path);
            Assert.True(exitCode == 0, "qpdf did not find " + existing + Environment.NewLine + stderr);

            string spec = entry.ProbeSpec(path);
            JsonElement after = CorpusManifest.Probe(spec).GetProperty(spec);
            Dictionary<string, string?> found = after.GetProperty("attachments").EnumerateArray()
                .ToDictionary(attachment => CorpusManifest.Text(attachment, "name")!, attachment => CorpusManifest.Text(attachment, "sha256"));
            Assert.Equal(new[] { existing, added }, found.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray());
            Assert.Equal(CorpusManifest.Sha256(content), found[added]);
            Assert.Equal(CorpusManifest.Text(entry.Probe.GetProperty("attachments")[0], "sha256"), found[existing]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        using var reopened = PdfDocument.Open(new MemoryStream(output, writable: false), entry.Password);
        Assert.Equal(new[] { existing, added }, reopened.Attachments.Select(a => a.Name).ToArray());
        Assert.Equal(content, ReadAll(reopened.Attachments[added]));
    }

    private static (int ExitCode, string Stdout, string Stderr) Qpdf(params string[] args)
    {
        using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), args);
        return ExternalTool.Run(process);
    }

    private static byte[] NameTreeDocument(string names)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [" + names + "] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(5, "/Type /EmbeddedFile", Encoding.ASCII.GetBytes("first"));
        builder.Indirect(6, "<< /Type /Filespec /UF (first.txt) /EF << /F 5 0 R >> >>");
        builder.StreamObject(7, "/Type /EmbeddedFile", Encoding.ASCII.GetBytes("second"));
        builder.Indirect(8, "<< /Type /Filespec /UF (second.txt) /EF << /F 7 0 R >> >>");
        return builder.Finish();
    }

    // The keys of the last /EmbeddedFiles /Names array written after the original bytes.
    private static string WrittenKeys(MemoryStream stream, int prefix)
    {
        string appended = Encoding.ASCII.GetString(stream.ToArray(), prefix, (int)stream.Length - prefix);
        int tree = appended.LastIndexOf("/EmbeddedFiles", StringComparison.Ordinal);
        Assert.True(tree >= 0, "no /EmbeddedFiles was written");
        int start = appended.IndexOf('[', tree);
        return appended.Substring(start, appended.IndexOf(']', start) - start + 1);
    }

    private static byte[] ReadAll(PdfAttachment attachment)
    {
        using Stream content = attachment.Open();
        using var copy = new MemoryStream();
        content.CopyTo(copy);
        return copy.ToArray();
    }

    private static byte[] Zlib(byte[] data)
    {
        return TestCompat.Zlib(data);
    }
}
