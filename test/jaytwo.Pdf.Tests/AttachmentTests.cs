using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace jaytwo.Pdf.Tests;

public class AttachmentTests
{
    // qpdf --check warns on a page without /Resources, which is required (ISO 32000-1 Table 30).
    private const string Resources = " /Resources << >>";

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
            using var editor = document.Edit();
            editor.Attachments.Add("b.bin", probe, leaveOpen: true, new PdfAttachmentOptions { Description = "beta", MimeType = "application/octet-stream" });
            editor.Attachments.Add("a.bin", new MemoryStream(second), leaveOpen: true);
            editor.SaveIncremental(stream);
        }

        Assert.True(probe.MaxRequested <= 65536);
        using var again = PdfDocument.Open(stream, leaveOpen: true);
        using var againEditor = again.Edit();
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

        againEditor.Attachments.Remove("a.bin");
        againEditor.SaveIncremental(stream);
        using var removed = PdfDocument.Open(stream, leaveOpen: true);
        using var removedEditor = removed.Edit();
        Assert.Single(removed.Attachments);
        Assert.Equal("b.bin", removed.Attachments[0].Name);
        Assert.Throws<PdfAttachmentNotFoundException>(() => removedEditor.Attachments.Remove("missing.bin"));
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
        using var editor = document.Edit();
        var probe = new ProbeStream(new MemoryStream(new byte[] { 1 }), allowRead: false);

        Assert.Throws<ArgumentException>(() => editor.Attachments.Add("a.bin", probe));
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
            using var editor = document.Edit();
            editor.Attachments.Add("usage.txt", new MemoryStream(Encoding.ASCII.GetBytes("added")));
            editor.SaveIncremental();
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
            using var editor = document.Edit();
            editor.Attachments.Add("z.txt", new MemoryStream(new byte[] { 1 }));
            editor.SaveIncremental();
        }

        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Attachments.Add("Bericht Ä.txt", new MemoryStream(new byte[] { 2 }));
            editor.SaveIncremental();
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
            using var editor = document.Edit();
            editor.Attachments.Add("Résumé.txt", new MemoryStream(Encoding.ASCII.GetBytes("added")));
            editor.Attachments.Add("a.txt", new MemoryStream(Encoding.ASCII.GetBytes("replaced")));
            editor.SaveIncremental();
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
            using var editor = document.Edit();
            editor.Attachments.Add("b.txt", new MemoryStream(new byte[] { 1 }));
            editor.SaveIncremental();
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
            using var editor = document.Edit();
            editor.Attachments.Add("A日本.txt", new MemoryStream(new byte[] { 1 }));
            editor.SaveIncremental();
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
    [Trait(Lane.Trait, Lane.Interop)]
    [Trait(Lane.Trait, Lane.Corpus)]
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
            using var editor = document.Edit();
            editor.Attachments.Add(added, new MemoryStream(content));
            editor.SaveIncremental(destination, leaveOpen: true);
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

    // Spec W9 and W10 (W-75). pypdf 6.21.1 is the independent reader of /AFRelationship, the /Params dates, and
    // /CheckSum, which must be the MD5 of the uncompressed bytes (ISO 32000-1 Table 46). One attachment per
    // relationship, compressed and not, with a date in a zone other than UTC.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void relationship_dates_and_checksum_are_written_and_read_by_pypdf()
    {
        PdfAttachmentRelationship[] relationships = (PdfAttachmentRelationship[])Enum.GetValues(typeof(PdfAttachmentRelationship));
        Assert.Equal(8, relationships.Length);
        var created = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(-5) - TimeSpan.FromMinutes(30));
        var modified = new DateTimeOffset(2026, 10, 21, 12, 34, 56, TimeSpan.Zero);
        byte[] pdf = PdfBuilder.Basic(pageExtra: Resources).Finish();
        var contents = new Dictionary<string, byte[]>();
        var destination = new MemoryStream();
        DateTimeOffset before = Seconds(DateTimeOffset.UtcNow);
        using (var document = PdfDocument.Open(new MemoryStream(pdf, writable: false)))
        {
            using var editor = document.Edit();
            for (int i = 0; i < relationships.Length; i++)
            {
                string name = relationships[i] + ".bin";
                byte[] content = Enumerable.Range(0, 1000 + i).Select(value => (byte)(value * (i + 3))).ToArray();
                contents[name] = content;
                PdfAttachment added = editor.Attachments.Add(name, new MemoryStream(content), options: new PdfAttachmentOptions
                {
                    Relationship = relationships[i],
                    CreationDate = created,
                    ModificationDate = modified,
                    Compress = i % 2 == 1,
                });
                Assert.Equal(relationships[i], added.Relationship);
                Assert.Equal(created, added.CreationDate);
            }

            contents["default.txt"] = Encoding.ASCII.GetBytes("no options");
            editor.Attachments.Add("default.txt", new MemoryStream(contents["default.txt"]));
            editor.SaveIncremental(destination, leaveOpen: true);
        }

        DateTimeOffset after = DateTimeOffset.UtcNow;
        byte[] output = destination.ToArray();
        Assert.True(output.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        using (var reopened = PdfDocument.Open(new MemoryStream(output, writable: false)))
        {
            for (int i = 0; i < relationships.Length; i++)
            {
                PdfAttachment attachment = reopened.Attachments[relationships[i] + ".bin"];
                Assert.Equal(relationships[i], attachment.Relationship);
                Assert.Equal(created, attachment.CreationDate);
                Assert.Equal(created.Offset, attachment.CreationDate!.Value.Offset);
                Assert.Equal(modified, attachment.ModificationDate);
                Assert.Equal(contents[attachment.Name], ReadAll(attachment));
            }

            PdfAttachment unspecified = reopened.Attachments["default.txt"];
            Assert.Equal(PdfAttachmentRelationship.Unspecified, unspecified.Relationship);
            Assert.InRange(unspecified.CreationDate!.Value, before, after);
            Assert.Equal(unspecified.CreationDate, unspecified.ModificationDate);
        }

        Assert.Contains("/AFRelationship /EncryptedPayload", Encoding.ASCII.GetString(output, pdf.Length, output.Length - pdf.Length), StringComparison.Ordinal);
        WithFile(output, path =>
        {
            AssertQpdfChecks(path);
            JsonElement report = InspectAttachments(path, null);
            Assert.Equal(JsonValueKind.Null, report.GetProperty("pageMode").ValueKind);
            Dictionary<string, JsonElement> found = report.GetProperty("attachments").EnumerateArray().ToDictionary(item => item.GetProperty("name").GetString()!);
            Assert.Equal(contents.Keys.OrderBy(name => name, StringComparer.Ordinal), found.Keys.OrderBy(name => name, StringComparer.Ordinal));
            foreach (KeyValuePair<string, byte[]> pair in contents)
            {
                JsonElement item = found[pair.Key];
                string md5 = TestCompat.ToHexString(Md5(pair.Value));
                Assert.Equal(md5, item.GetProperty("checksum").GetString(), StringComparer.OrdinalIgnoreCase);
                Assert.Equal(md5, item.GetProperty("md5").GetString(), StringComparer.OrdinalIgnoreCase);
                Assert.Equal(pair.Value.Length, item.GetProperty("size").GetInt64());
                Assert.Equal(pair.Value.Length, item.GetProperty("length").GetInt64());
                if (pair.Key == "default.txt")
                {
                    Assert.Equal("/Unspecified", item.GetProperty("relationship").GetString());
                    DateTimeOffset stamped = DateTimeOffset.Parse(item.GetProperty("creationDate").GetString()!, CultureInfo.InvariantCulture);
                    Assert.InRange(stamped, before, after);
                    continue;
                }

                Assert.Equal("/" + pair.Key.Substring(0, pair.Key.Length - 4), item.GetProperty("relationship").GetString());
                Assert.Equal("2026-03-04T10:36:07Z", item.GetProperty("creationDate").GetString());
                Assert.Equal("2026-10-21T12:34:56Z", item.GetProperty("modDate").GetString());
            }
        });
    }

    // Spec W11 (W-75): Flate-encoded while streaming. 40 MiB is above the 32 MiB structural cap, and the content is
    // read twice, 64 KiB at a time: once at Add to measure /CheckSum and the compressed /Length, once by the save.
    // The library, qpdf 12.4.2, and pypdf 6.21.1 return the bytes exactly.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void compressed_attachment_round_trips_byte_for_byte_above_the_structural_cap()
    {
        const int size = 40 * 1024 * 1024;
        var content = new byte[size];
        var random = new Random(75);
        for (int offset = 0; offset < size; offset += 4096)
        {
            // Compressible runs between random blocks, so the deflater emits stored, fixed, and dynamic blocks.
            if ((offset / 4096) % 3 == 0)
            {
                var block = new byte[4096];
                random.NextBytes(block);
                Buffer.BlockCopy(block, 0, content, offset, block.Length);
            }
            else
            {
                content[offset] = (byte)(offset >> 12);
            }
        }

        byte[] pdf = PdfBuilder.Basic(pageExtra: Resources).Finish();
        var probe = new ProbeStream(new MemoryStream(content, writable: false));
        var destination = new MemoryStream();
        using (var document = PdfDocument.Open(new MemoryStream(pdf, writable: false)))
        {
            using var editor = document.Edit();
            editor.Attachments.Add("large.bin", probe, leaveOpen: true, new PdfAttachmentOptions { Compress = true });
            Assert.Equal(size, probe.BytesRead);
            editor.SaveIncremental(destination, leaveOpen: true);
        }

        Assert.Equal(2L * size, probe.BytesRead);
        Assert.True(probe.MaxRequested <= 65536, "requested " + probe.MaxRequested.ToString());
        byte[] output = destination.ToArray();
        Assert.True(output.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        Assert.True(output.Length < pdf.Length + (size / 2), "the attachment was not compressed: " + output.Length.ToString());
        string sha256 = CorpusManifest.Sha256(content);
        using (var reopened = PdfDocument.Open(new MemoryStream(output, writable: false)))
        {
            Assert.Equal(size, reopened.Attachments["large.bin"].Size);
            Assert.Equal(sha256, CorpusManifest.Sha256(ReadAll(reopened.Attachments["large.bin"])));
        }

        WithFile(output, path =>
        {
            AssertQpdfChecks(path);
            Assert.Equal(sha256, CorpusManifest.Sha256(QpdfAttachment(path, "large.bin")));
            JsonElement item = InspectAttachments(path, null).GetProperty("attachments")[0];
            Assert.Equal(sha256, item.GetProperty("sha256").GetString());
            Assert.Equal(TestCompat.ToHexString(Md5(content)), item.GetProperty("checksum").GetString(), StringComparer.OrdinalIgnoreCase);
        });
    }

    // Every byte value, and an empty file, through the zlib wrapper; compression stays off by default.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void compressed_small_and_empty_attachments_round_trip_and_compression_is_off_by_default()
    {
        byte[] every = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        byte[] pdf = PdfBuilder.Basic(pageExtra: Resources).Finish();
        var destination = new MemoryStream();
        using (var document = PdfDocument.Open(new MemoryStream(pdf, writable: false)))
        {
            using var editor = document.Edit();
            editor.Attachments.Add("every.bin", new MemoryStream(every), options: new PdfAttachmentOptions { Compress = true });
            editor.Attachments.Add("empty.bin", new MemoryStream(), options: new PdfAttachmentOptions { Compress = true });
            editor.Attachments.Add("plain.bin", new MemoryStream(every));
            editor.SaveIncremental(destination, leaveOpen: true);
        }

        byte[] output = destination.ToArray();
        string appendix = Encoding.ASCII.GetString(output, pdf.Length, output.Length - pdf.Length);
        Assert.Equal(2, Regex.Matches(appendix, "/Filter /FlateDecode").Count);
        using (var reopened = PdfDocument.Open(new MemoryStream(output, writable: false)))
        {
            Assert.Equal(every, ReadAll(reopened.Attachments["every.bin"]));
            Assert.Empty(ReadAll(reopened.Attachments["empty.bin"]));
            Assert.Equal(every, ReadAll(reopened.Attachments["plain.bin"]));
        }

        WithFile(output, path =>
        {
            AssertQpdfChecks(path);
            Assert.Equal(every, QpdfAttachment(path, "every.bin"));
            Assert.Empty(QpdfAttachment(path, "empty.bin"));
            Assert.Equal(every, QpdfAttachment(path, "plain.bin"));
        });
    }

    // Spec W12 (W-75): the option sets /PageMode /UseAttachments; without it an existing /PageMode is kept.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void open_attachments_panel_sets_the_page_mode_that_pypdf_reads()
    {
        byte[] pdf = PdfBuilder.Basic(" /PageMode /UseOutlines", Resources).Finish();
        byte[] kept = Save(pdf, editor => editor.Attachments.Add("a.txt", new MemoryStream(new byte[] { 1 })));
        byte[] opened = Save(pdf, editor => editor.Attachments.Add("a.txt", new MemoryStream(new byte[] { 1 }), options: new PdfAttachmentOptions { OpenAttachmentsPanel = true }));

        Assert.Contains("/PageMode /UseOutlines", Encoding.ASCII.GetString(kept, pdf.Length, kept.Length - pdf.Length), StringComparison.Ordinal);
        Assert.DoesNotContain("/UseAttachments", Encoding.ASCII.GetString(kept, pdf.Length, kept.Length - pdf.Length), StringComparison.Ordinal);
        Assert.Contains("/PageMode /UseAttachments", Encoding.ASCII.GetString(opened, pdf.Length, opened.Length - pdf.Length), StringComparison.Ordinal);
        Assert.DoesNotContain("/UseOutlines", Encoding.ASCII.GetString(opened, pdf.Length, opened.Length - pdf.Length), StringComparison.Ordinal);
        WithFile(kept, path => Assert.Equal("/UseOutlines", InspectAttachments(path, null).GetProperty("pageMode").GetString()));
        WithFile(opened, path =>
        {
            AssertQpdfChecks(path);
            Assert.Equal("/UseAttachments", InspectAttachments(path, null).GetProperty("pageMode").GetString());
        });
    }

    // W-75 failure guarantee: content that changes between Add and the save fails the save, and neither the source
    // nor an empty destination is changed, so no wrong /CheckSum or /Length is published.
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public void content_changed_after_add_fails_the_save_and_leaves_the_destination_unchanged(bool compress, bool shorten, bool sameStream)
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        var content = new MemoryStream();
        content.Write(Enumerable.Range(0, 5000).Select(value => (byte)value).ToArray(), 0, 5000);
        content.Position = 0;
        var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        var destination = new MemoryStream();
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        editor.Attachments.Add("changed.bin", content, leaveOpen: true, new PdfAttachmentOptions { Compress = compress });
        if (shorten)
        {
            content.SetLength(4000);
        }
        else
        {
            content.Position = 2500;
            content.WriteByte(0xFF);
        }

        if (sameStream)
        {
            Assert.Throws<InvalidOperationException>(() => editor.SaveIncremental());
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => editor.SaveIncremental(destination, leaveOpen: true));
        }

        Assert.Equal(pdf, source.ToArray());
        Assert.Equal(0, destination.Length);
    }

    // Spec R8: an /AFRelationship that is absent or not a Table 43 name reads as Unspecified, and a /Params date that
    // is not a PDF date throws only when that date is read, so the rest of the listing stays usable.
    [Fact]
    public void foreign_relationship_and_dates_are_read_and_a_malformed_date_throws_only_when_read()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(a.xml) 6 0 R (b.txt) 8 0 R (c.txt) 10 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(5, "/Type /EmbeddedFile /Params << /Size 3 /CreationDate (D:20240102030405+01'00') /ModDate 11 0 R >>", Encoding.ASCII.GetBytes("abc"));
        builder.Indirect(6, "<< /Type /Filespec /UF (a.xml) /EF << /F 5 0 R >> /AFRelationship 12 0 R >>");
        builder.StreamObject(7, "/Type /EmbeddedFile /Params << /CreationDate (yesterday) >>", Encoding.ASCII.GetBytes("b"));
        builder.Indirect(8, "<< /Type /Filespec /UF (b.txt) /EF << /F 7 0 R >> /AFRelationship /Custom >>");
        builder.StreamObject(9, "/Type /EmbeddedFile", Encoding.ASCII.GetBytes("c"));
        builder.Indirect(10, "<< /Type /Filespec /UF (c.txt) /EF << /F 9 0 R >> >>");
        builder.Indirect(11, "(D:20250607)");
        builder.Indirect(12, "/Data");
        byte[] pdf = builder.Finish();

        using var document = PdfDocument.Open(new MemoryStream(pdf, writable: false));
        Assert.Equal(new[] { "a.xml", "b.txt", "c.txt" }, document.Attachments.Select(a => a.Name).ToArray());
        PdfAttachment data = document.Attachments["a.xml"];
        Assert.Equal(PdfAttachmentRelationship.Data, data.Relationship);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(1)), data.CreationDate);
        Assert.Equal(new DateTimeOffset(2025, 6, 7, 0, 0, 0, TimeSpan.Zero), data.ModificationDate);
        PdfAttachment custom = document.Attachments["b.txt"];
        Assert.Equal(PdfAttachmentRelationship.Unspecified, custom.Relationship);
        Assert.Throws<PdfParseException>(() => custom.CreationDate);
        Assert.Null(custom.ModificationDate);
        Assert.Null(custom.Size);
        Assert.Equal(Encoding.ASCII.GetBytes("b"), ReadAll(custom));
        PdfAttachment bare = document.Attachments["c.txt"];
        Assert.Equal(PdfAttachmentRelationship.Unspecified, bare.Relationship);
        Assert.Null(bare.CreationDate);
    }

    [Fact]
    public void undefined_relationship_is_rejected_before_the_document_changes()
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        var content = new ProbeStream(new MemoryStream(new byte[] { 1 }));

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Attachments.Add("a.bin", content, leaveOpen: true, new PdfAttachmentOptions { Relationship = (PdfAttachmentRelationship)99 }));
        Assert.False(content.ReadCalled);
        Assert.Empty(document.Attachments);
        editor.SaveIncremental();
        Assert.Equal(pdf, source.ToArray());
    }

    // Fixtures/inspect_attachments.py: the catalog /PageMode and each attachment as pypdf 6.21.1 reads it.
    internal static JsonElement InspectAttachments(string path, string? password)
    {
        var args = new List<string> { ExternalTool.Fixture("inspect_attachments.py"), path };
        if (password != null)
        {
            args.Add(password);
        }

        using Process process = ExternalTool.Start(ExternalTool.Python(), args.ToArray());
        using JsonDocument report = JsonDocument.Parse(ExternalTool.Output(process));
        return report.RootElement.Clone();
    }

    private static byte[] Save(byte[] pdf, Action<PdfDocumentEditor> write)
    {
        var destination = new MemoryStream();
        using (var document = PdfDocument.Open(new MemoryStream(pdf, writable: false)))
        {
            using var editor = document.Edit();
            write(editor);
            editor.SaveIncremental(destination, leaveOpen: true);
        }

        byte[] output = destination.ToArray();
        Assert.True(output.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        return output;
    }

    private static void WithFile(byte[] pdf, Action<string> check)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "output.pdf");
            File.WriteAllBytes(path, pdf);
            check(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertQpdfChecks(string path)
    {
        (int exitCode, string stdout, string stderr) = Qpdf("--check", path);
        Assert.True(exitCode == 0 && !(stdout + stderr).Contains("WARNING"), "qpdf --check exited " + exitCode.ToString() + Environment.NewLine + stderr + stdout);
    }

    // qpdf writes the decoded attachment to stdout, which is read as bytes, not text.
    private static byte[] QpdfAttachment(string path, string name)
    {
        using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), "--show-attachment=" + name, path);
        var bytes = new MemoryStream();
        System.Threading.Tasks.Task copy = process.StandardOutput.BaseStream.CopyToAsync(bytes);
        System.Threading.Tasks.Task<string> stderr = process.StandardError.ReadToEndAsync();
        copy.GetAwaiter().GetResult();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, "qpdf --show-attachment=" + name + " exited " + process.ExitCode.ToString() + Environment.NewLine + stderr.GetAwaiter().GetResult());
        return bytes.ToArray();
    }

    private static byte[] Md5(byte[] data)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return md5.ComputeHash(data);
    }

    private static DateTimeOffset Seconds(DateTimeOffset value)
    {
        return new DateTimeOffset(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Offset);
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
