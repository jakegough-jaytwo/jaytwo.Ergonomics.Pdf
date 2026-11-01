using System;
using System.IO;
using System.Linq;
using System.Text;
using jaytwo.Pdf.Metadata;
using Xunit;

namespace jaytwo.Pdf.Tests;

public class MetadataTests
{
    [Fact]
    public void info_round_trips_unicode_and_keeps_custom_keys()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (Old) /Author (Ada) /Custom (kept) /Creator (Original) /Producer (Original) >>");
        byte[] pdf = builder.Finish("/Info 4 0 R");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.Title = "héllo 日本語";
            editor.Metadata.Subject = "Subject";
            editor.Metadata.Keywords = "alpha beta";
            editor.Metadata["Department"] = "Forms";
            editor.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("/Custom", appendix, StringComparison.Ordinal);
        Assert.Contains("FEFF", appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(new MemoryStream(saved), leaveOpen: true);
        Assert.Equal("héllo 日本語", again.Metadata.Title);
        Assert.Equal("Ada", again.Metadata.Author);
        Assert.Equal("Subject", again.Metadata.Subject);
        Assert.Equal("alpha beta", again.Metadata.Keywords);
        Assert.Equal("Original", again.Metadata.Creator);
        Assert.Equal("Original", again.Metadata.Producer);
        Assert.Equal("kept", again.Metadata["Custom"]);
        Assert.Equal("Forms", again.Metadata["Department"]);
        Assert.Null(again.Metadata["Missing"]);
        Assert.Contains("Custom", again.Metadata.Keys);
        Assert.Contains("Department", again.Metadata.Keys);
    }

    [Fact]
    public void pdfdoc_bytes_decode_bullet_and_euro()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (\\200\\240) >>");
        byte[] pdf = builder.Finish("/Info 4 0 R");

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);

        Assert.Equal("\u2022\u20AC", document.Metadata.Title);
    }

    [Fact]
    public void dates_accept_zulu_and_offset_forms()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /CreationDate (D:20200101120000Z) /ModDate (D:20200101120000-05'00') >>");
        byte[] pdf = builder.Finish("/Info 4 0 R");

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);

        Assert.Equal(new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.Zero), document.Metadata.CreationDate);
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.FromHours(-5)), document.Metadata.ModificationDate);
    }

    [Fact]
    public void explicit_modification_date_is_not_replaced()
    {
        byte[] pdf = Titled("Old");
        var stamp = new DateTimeOffset(1999, 5, 4, 3, 2, 1, TimeSpan.Zero);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.ModificationDate = stamp;
            editor.Metadata.Title = "New";
            editor.SaveIncremental(stream);
        }

        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("New", again.Metadata.Title);
        Assert.Equal(stamp, again.Metadata.ModificationDate);
    }

    [Fact]
    public void absent_xmp_is_created_and_well_formed_xmp_is_updated()
    {
        byte[] created = Titled("Old");
        using (var stream = new MemoryStream())
        {
            stream.Write(created, 0, created.Length);
            using (var document = PdfDocument.Open(stream, leaveOpen: true))
            {
                using var editor = document.Edit();
                Assert.False(document.Metadata.HasXmp);
                editor.Metadata.Title = "Created";
                editor.SaveIncremental(stream);
            }

            using var again = PdfDocument.Open(stream, leaveOpen: true);
            Assert.True(again.Metadata.HasXmp);
            using var xmp = again.Metadata.OpenXmp();
            string text = new StreamReader(xmp).ReadToEnd();
            Assert.Contains("Created", text, StringComparison.Ordinal);
            Assert.Contains("ModifyDate", text, StringComparison.Ordinal);
        }

        string packet = "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>"
            + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">"
            + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Old</rdf:li></rdf:Alt></dc:title>"
            + "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Metadata 5 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title (Old) >>");
        builder.StreamObject(5, "/Type /Metadata /Subtype /XML", Encoding.UTF8.GetBytes(packet));
        byte[] pdf = builder.Finish("/Info 4 0 R");
        using var xmpStream = new MemoryStream();
        xmpStream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(xmpStream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.Title = "Updated";
            editor.SaveIncremental(xmpStream);
        }

        using var updated = PdfDocument.Open(xmpStream, leaveOpen: true);
        using var updatedXmp = updated.Metadata.OpenXmp();
        string updatedText = new StreamReader(updatedXmp).ReadToEnd();
        Assert.Contains("Updated", updatedText, StringComparison.Ordinal);
        Assert.Contains("ModifyDate", updatedText, StringComparison.Ordinal);
        Assert.Equal("Updated", updated.Metadata.Title);
    }

    // Opening and reading keep their own contract: malformed XMP opens and reports as an error on read, and the refused
    // ordinary edit leaves the packet byte for byte (METADATA-READ-01, METADATA-VALIDATE-01).
    [Fact]
    public void malformed_xmp_opens_and_reads_as_an_error_and_refuses_an_edit_with_no_change()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Metadata 5 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title (Old) >>");
        builder.StreamObject(5, "/Type /Metadata /Subtype /XML", Encoding.ASCII.GetBytes("not xml"));
        byte[] pdf = builder.Finish("/Info 4 0 R");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.True(document.Metadata.HasXmp);
            Assert.Throws<PdfXmpException>(() => document.Metadata.OpenXmp());
            Assert.Throws<PdfXmpException>(() => editor.Metadata.Title = "New");
            editor.SaveIncremental(stream);
        }

        Assert.Equal(pdf, stream.ToArray());
        using var again = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        Assert.Equal("Old", again.Metadata.Title);
        Assert.Throws<PdfXmpException>(() => again.Metadata.OpenXmp());
    }

    [Fact]
    public void set_xmp_stores_the_exact_bytes()
    {
        byte[] packet = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><a/>");
        byte[] pdf = Titled("Old");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.SetXmp(packet);
            editor.SaveIncremental(stream);
        }

        using var again = PdfDocument.Open(stream, leaveOpen: true);
        using var xmp = again.Metadata.OpenXmp();
        using var copy = new MemoryStream();
        xmp.CopyTo(copy);
        Assert.Equal(packet, copy.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void utf8_and_utf16_xmp_packets_read_and_edit(bool utf16)
    {
        string packet = "<?xml version=\"1.0\" encoding=\"" + (utf16 ? "utf-16" : "utf-8") + "\"?>"
            + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>Old</dc:title>"
            + "</rdf:Description></rdf:RDF></x:xmpmeta>";
        byte[] bytes = utf16 ? Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(packet)).ToArray() : Encoding.UTF8.GetBytes(packet);
        using var stream = XmpDocument(bytes);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var opened = document.Metadata.OpenXmp();
            Assert.Equal(bytes, ReadAll(opened));
            using var editor = document.Edit();
            editor.Metadata.Title = "Updated";
            editor.SaveIncremental(stream);
        }

        using var again = PdfDocument.Open(stream, leaveOpen: true);
        using var updated = again.Metadata.OpenXmp();
        Assert.Contains("Updated", Encoding.UTF8.GetString(ReadAll(updated)), StringComparison.Ordinal);
    }

    [Fact]
    public void dtd_is_rejected_without_resolving_an_external_resource()
    {
        byte[] packet = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><!DOCTYPE xmp SYSTEM \"file:///this-resource-must-not-be-opened\"><xmp/>");
        using var stream = XmpDocument(packet);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        PdfXmpException error = Assert.Throws<PdfXmpException>(() => document.Metadata.OpenXmp());

        Assert.Equal("XMP is not well-formed XML.", error.Message);
    }

    [Fact]
    public void refused_xmp_packet_does_not_dirty_the_document()
    {
        byte[] pdf = Titled("Old");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();
        byte[] oversized = new byte[XmpEditor.MaxDocumentBytes + 1];

        PdfXmpException error = Assert.Throws<PdfXmpException>(() => editor.Metadata.SetXmp(oversized));
        editor.SaveIncremental(stream);

        Assert.Equal("XMP exceeds the 32 MiB document limit.", error.Message);
        Assert.Equal(pdf.Length, stream.Length);
        Assert.False(document.Metadata.HasXmp);
    }

    [Fact]
    public void trapped_is_a_name_and_info_dates_round_trip_as_text()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Trapped /False /ModDate (D:20200101120000Z) >>");
        byte[] pdf = builder.Finish("/Info 4 0 R");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Equal("False", document.Metadata["Trapped"]);
            Assert.Equal("D:20200101120000Z", document.Metadata["ModDate"]);
            Assert.Throws<ArgumentException>(() => editor.Metadata["Trapped"] = "Maybe");
            editor.Metadata["Trapped"] = "True";
            editor.Metadata["ModDate"] = "D:20200102030405-05'00'";
            editor.Metadata["Title"] = "Later";
            editor.SaveIncremental();
        }

        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("True", again.Metadata["Trapped"]);
        Assert.Equal(new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5)), again.Metadata.ModificationDate);
        Assert.Equal("Later", again.Metadata.Title);
        Assert.Equal("D:20200102030405-05'00'", again.Metadata["ModDate"]);
    }

    [Fact]
    public void info_edit_refreshes_moddate_in_utc()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (Old) /ModDate (D:20200101120000-05'00') >>");
        byte[] pdf = builder.Finish("/Info 4 0 R");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.Title = "New";
            editor.SaveIncremental();
        }

        DateTimeOffset after = DateTimeOffset.UtcNow.AddSeconds(1);
        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("New", again.Metadata.Title);
        DateTimeOffset? read = again.Metadata.ModificationDate;
        Assert.True(read.HasValue);
        DateTimeOffset modified = read.GetValueOrDefault();
        Assert.Equal(TimeSpan.Zero, modified.Offset);
        Assert.InRange(modified, before, after);
        string text = again.Metadata["ModDate"] ?? string.Empty;
        Assert.StartsWith("D:", text, StringComparison.Ordinal);
        Assert.EndsWith("Z", text, StringComparison.Ordinal);
    }

    // METADATA-VALIDATE-01: a touched property must be defined in exactly one place; anything else refuses with no change.
    [Theory]
    [InlineData("<rdf:Description rdf:about=\"\"><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">A</rdf:li></rdf:Alt></dc:title></rdf:Description><rdf:Description rdf:about=\"x\"><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">B</rdf:li></rdf:Alt></dc:title></rdf:Description>")]
    [InlineData("<rdf:Description rdf:about=\"\" dc:title=\"A\"><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">B</rdf:li></rdf:Alt></dc:title></rdf:Description>")]
    [InlineData("<rdf:Description rdf:about=\"\"><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">A</rdf:li><rdf:li xml:lang=\"X-DEFAULT\">B</rdf:li></rdf:Alt></dc:title></rdf:Description>")]
    public void duplicate_touched_definition_refuses_with_no_change(string descriptions)
    {
        byte[] packet = DuplicateXmpPacket(descriptions);
        using var stream = DuplicateXmpDocument(packet);
        byte[] original = stream.ToArray();
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Throws<PdfXmpException>(() => editor.Metadata.Title = "New");
            Assert.Throws<PdfXmpException>(() => editor.Metadata.Title = null);
            Assert.Equal("Old", editor.Metadata.Title);
            Assert.Equal("D:20200101120000Z", editor.Metadata["ModDate"]);
            editor.SaveIncremental(stream);
        }

        Assert.Equal(original, stream.ToArray());
    }

    // A refresh of ModDate touches xmp:ModifyDate, so a duplicate ModifyDate refuses an edit of an unrelated property.
    [Fact]
    public void duplicate_modify_date_refuses_an_edit_that_refreshes_moddate()
    {
        byte[] packet = DuplicateXmpPacket(
            "<rdf:Description rdf:about=\"\" xmp:ModifyDate=\"2020-01-01T12:00:00Z\"><xmp:ModifyDate>2020-01-01T12:00:00Z</xmp:ModifyDate></rdf:Description>");
        using var stream = DuplicateXmpDocument(packet);
        byte[] original = stream.ToArray();
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Throws<PdfXmpException>(() => editor.Metadata.Keywords = "alpha");
            Assert.Throws<PdfXmpException>(() => editor.Metadata["Custom"] = "value");
            Assert.Throws<PdfXmpException>(() => editor.Metadata["Trapped"] = "True");
            Assert.Null(editor.Metadata.Keywords);
            Assert.Null(editor.Metadata["Custom"]);
            editor.SaveIncremental(stream);
        }

        Assert.Equal(original, stream.ToArray());
    }

    // The same duplicate in a property the edit does not touch never refuses, and the duplicate survives.
    [Fact]
    public void unrelated_duplicate_does_not_refuse_and_survives()
    {
        byte[] packet = DuplicateXmpPacket(
            "<rdf:Description rdf:about=\"\"><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Old</rdf:li></rdf:Alt></dc:title><pdf:Keywords>one</pdf:Keywords></rdf:Description>"
            + "<rdf:Description rdf:about=\"x\"><pdf:Keywords>two</pdf:Keywords></rdf:Description>");
        using var stream = DuplicateXmpDocument(packet);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.Title = "New";
            editor.SaveIncremental(stream);
        }

        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("New", again.Metadata.Title);
        using var xmp = again.Metadata.OpenXmp();
        string text = new StreamReader(xmp).ReadToEnd();
        Assert.Contains(">New<", text, StringComparison.Ordinal);
        Assert.Contains(">one<", text, StringComparison.Ordinal);
        Assert.Contains(">two<", text, StringComparison.Ordinal);
    }

    // A property defined once, in a description other than the first, is edited there; no second definition is added.
    [Fact]
    public void property_defined_in_a_later_description_is_edited_in_place()
    {
        byte[] packet = DuplicateXmpPacket(
            "<rdf:Description rdf:about=\"\"><pdf:Keywords>kept</pdf:Keywords></rdf:Description>"
            + "<rdf:Description rdf:about=\"x\"><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Old</rdf:li></rdf:Alt></dc:title></rdf:Description>");
        using var stream = DuplicateXmpDocument(packet);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.Title = "New";
            editor.SaveIncremental(stream);
        }

        using var again = PdfDocument.Open(stream, leaveOpen: true);
        using var xmp = again.Metadata.OpenXmp();
        string text = new StreamReader(xmp).ReadToEnd();
        Assert.Equal(1, text.Split(new[] { "dc:title>" }, StringSplitOptions.None).Length / 2);
        Assert.Contains(">New<", text, StringComparison.Ordinal);
        Assert.DoesNotContain(">Old<", text, StringComparison.Ordinal);
        Assert.Contains(">kept<", text, StringComparison.Ordinal);
    }

    // METADATA-VALIDATE-01: an ordinary edit that must touch an existing packet it cannot edit safely is refused with no
    // change and no Info-only fallback. Malformed XML and well-formed but unsupported shapes are distinct causes.
    [Theory]
    [InlineData("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF")]
    [InlineData("not xml at all")]
    [InlineData("<?xpacket begin=\"\"?><a><b></a>")]
    public void malformed_packet_refuses_every_touching_edit_with_no_change(string packetText)
    {
        AssertEditsRefusedWithNoChange(Encoding.UTF8.GetBytes(packetText));
    }

    [Theory]
    [InlineData("<foo/>")]
    [InlineData("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><x:other/></x:xmpmeta>")]
    [InlineData("<foo xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:RDF/></foo>")]
    public void well_formed_packet_with_another_root_or_no_rdf_refuses_every_touching_edit_with_no_change(string packetText)
    {
        AssertEditsRefusedWithNoChange(Encoding.UTF8.GetBytes(packetText));
    }

    // A raw replacement is its own contract: it installs the unsupported packet, a later ordinary edit refuses, and the
    // earlier pending replacement stays.
    [Fact]
    public void refused_edit_keeps_an_earlier_pending_raw_replacement()
    {
        byte[] raw = Encoding.UTF8.GetBytes("<foo/>");
        using var stream = DuplicateXmpDocument(DuplicateXmpPacket(string.Empty));
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.SetXmp(raw);
            Assert.Throws<PdfXmpException>(() => editor.Metadata.Title = "New");
            editor.SaveIncremental(stream);
        }

        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("Old", again.Metadata.Title);
        using var xmp = again.Metadata.OpenXmp();
        Assert.Equal(raw, ReadAll(xmp));
    }

    // Opening and reading stay separate: a document with such a packet opens and its Info reads normally.
    [Fact]
    public void unsupported_packet_still_opens_and_reads_info()
    {
        using var stream = DuplicateXmpDocument(Encoding.UTF8.GetBytes("<foo/>"));
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("Old", document.Metadata.Title);
        Assert.True(document.Metadata.HasXmp);
    }

    private static void AssertEditsRefusedWithNoChange(byte[] packet)
    {
        using var stream = DuplicateXmpDocument(packet);
        byte[] original = stream.ToArray();
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Throws<PdfXmpException>(() => editor.Metadata.Title = "New");
            Assert.Throws<PdfXmpException>(() => editor.Metadata.Title = null);
            Assert.Throws<PdfXmpException>(() => editor.Metadata.Keywords = "alpha");
            Assert.Throws<PdfXmpException>(() => editor.Metadata.CreationDate = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
            Assert.Throws<PdfXmpException>(() => editor.Metadata.ModificationDate = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
            Assert.Throws<PdfXmpException>(() => editor.Metadata["Custom"] = "value");
            Assert.Throws<PdfXmpException>(() => editor.Metadata["Trapped"] = "True");
            Assert.Equal("Old", editor.Metadata.Title);
            Assert.Equal("D:20200101120000Z", editor.Metadata["ModDate"]);
            Assert.Null(editor.Metadata["Custom"]);
            editor.SaveIncremental(stream);
        }

        Assert.Equal(original, stream.ToArray());
    }

    private static byte[] DuplicateXmpPacket(string descriptions)
    {
        return Encoding.UTF8.GetBytes(
            "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>"
            + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\" "
            + "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">"
            + descriptions
            + "</rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>");
    }

    private static MemoryStream DuplicateXmpDocument(byte[] packet)
    {
        PdfBuilder builder = PdfBuilder.Basic(" /Metadata 5 0 R");
        builder.Indirect(4, "<< /Title (Old) /ModDate (D:20200101120000Z) >>");
        builder.StreamObject(5, "/Type /Metadata /Subtype /XML", packet);
        byte[] pdf = builder.Finish("/Info 4 0 R");
        var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        return stream;
    }

    private static byte[] Titled(string title)
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (" + title + ") >>");
        return builder.Finish("/Info 4 0 R");
    }

    private static MemoryStream XmpDocument(byte[] packet)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(4, "/Type /Metadata /Subtype /XML", packet);
        byte[] pdf = builder.Finish();
        var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        return stream;
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static int Count(byte[] haystack, byte[] needle)
    {
        int count = 0;
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                count++;
            }
        }

        return count;
    }
}
