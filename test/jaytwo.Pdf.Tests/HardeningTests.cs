using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.Internal;
using jaytwo.Pdf.Signatures;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Regression tests for audit findings SA-01, SA-02, SA-03, SA-04, SA-09, and SA-11. Inputs are small, caps that need
// tens of MiB to reach are checked through the parser with a small caller cap, and every refusal is bounded, so no test
// overflows the stack or exhausts memory in the shared test process.
public class HardeningTests
{
    [Theory]
    [InlineData("number")]
    [InlineData("name")]
    [InlineData("keyword")]
    public void long_name_number_and_keyword_tokens_are_refused_within_a_small_allocation(string kind)
    {
        string token = kind switch
        {
            "number" => "1" + new string('0', 1024 * 1024),
            "name" => "/" + new string('a', 1024 * 1024),
            _ => new string('k', 1024 * 1024),
        };
        byte[] pdf = PdfBuilder.Basic().Finish("/X " + token);

        long allocated = AssertOpenRefused<PdfObjectTooLargeException>(pdf, out PdfObjectTooLargeException error);

        Assert.Contains(kind, error.Message, StringComparison.Ordinal);
        Assert.True(allocated < 2 * 1024 * 1024, "Refusal allocated " + allocated.ToString(CultureInfo.InvariantCulture) + " bytes.");
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("nested")]
    [InlineData("escaped")]
    [InlineData("octal")]
    [InlineData("carriage-return")]
    [InlineData("hex")]
    [InlineData("odd-hex")]
    public void every_string_branch_respects_the_string_cap(string branch)
    {
        const int cap = 64;

        PdfObject.Str atCap = Assert.IsType<PdfObject.Str>(ParseSmall(StringOfLength(branch, cap), cap, 16));
        Assert.Equal(cap, atCap.Raw.Length);
        Assert.Throws<PdfObjectTooLargeException>(() => ParseSmall(StringOfLength(branch, cap + 1), cap, 16));
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Clock)]
    public void dictionary_with_many_distinct_keys_is_parsed_without_a_per_key_scan()
    {
        const int keys = 150000;
        var info = new StringBuilder("<< ");
        for (int i = 0; i < keys; i++)
        {
            info.Append("/K").Append(i.ToString(CultureInfo.InvariantCulture)).Append(" (v").Append(i.ToString(CultureInfo.InvariantCulture)).Append(") ");
        }

        info.Append("/K0 (last) /Title (Wide) >>");
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, info.ToString());
        byte[] pdf = builder.Finish("/Info 4 0 R");

        var timer = Stopwatch.StartNew();
        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        Assert.Equal("Wide", document.Metadata.Title);
        timer.Stop();

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10), "Parsing took " + timer.Elapsed.ToString());
        Assert.Equal("last", document.Metadata["K0"]);
        Assert.Equal("v149999", document.Metadata["K149999"]);
    }

    [Theory]
    [InlineData("array")]
    [InlineData("dictionary")]
    [InlineData("repeated-key")]
    public void array_and_dictionary_entry_counts_are_capped(string container)
    {
        const int cap = 16;

        Assert.IsAssignableFrom<PdfObject>(ParseSmall(Container(container, cap), 64, cap));
        PdfObjectTooLargeException error = Assert.Throws<PdfObjectTooLargeException>(() => ParseSmall(Container(container, cap + 1), 64, cap));
        Assert.Contains(container == "array" ? "array" : "dictionary", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void production_array_cap_applies_when_a_document_opens()
    {
        var payload = new StringBuilder("[");
        payload.Append(Repeat("0 ", PdfParser.MaxContainerEntries + 1)).Append(']');
        byte[] pdf = PdfBuilder.Basic().Finish("/X " + payload.ToString());

        AssertOpenRefused<PdfObjectTooLargeException>(pdf, out PdfObjectTooLargeException error);

        Assert.Contains("array", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void xref_stream_entry_count_is_refused_before_entries_are_built()
    {
        int listed = PdfXrefReader.MaxStreamObjects + 1;
        string count = listed.ToString(CultureInfo.InvariantCulture);
        byte[] pdf = XrefStream("/Size " + count + " /W [0 1 0] /Index [0 " + count + "] /Filter /FlateDecode", TestCompat.Zlib(new byte[listed]));

        long allocated = AssertOpenRefused<PdfObjectTooLargeException>(pdf, out _);

        Assert.True(allocated < 96L * 1024 * 1024, "Refusal allocated " + allocated.ToString(CultureInfo.InvariantCulture) + " bytes.");
    }

    [Fact]
    public void xref_stream_subsection_past_size_is_refused()
    {
        byte[] pdf = XrefStream("/Size 1 /W [1 1 1] /Index [0 2]", new byte[] { 1, 0, 0, 1, 0, 0 });

        AssertOpenRefused<PdfParseException>(pdf, out PdfParseException error);

        Assert.Contains("/Size", error.Message, StringComparison.Ordinal);
    }

    // QA-B36-01 at a reduced count: the 8,319-byte repro listed 8,388,608 entries. If the bound regresses, this allocates
    // a few hundred MB at most, not gigabytes.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void xref_streams_listing_more_objects_than_the_file_can_back_are_refused_before_entries_are_built(int sections)
    {
        const int listed = 262144;
        byte[] pdf = ZeroXrefChain(listed, sections);
        Assert.True(pdf.Length < 4096);

        long allocated = AssertOpenRefused<PdfObjectTooLargeException>(pdf, out PdfObjectTooLargeException error);

        Assert.Contains(PdfXrefReader.MinEntryBudget.ToString(CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.True(allocated < 8L * 1024 * 1024, "Refusal allocated " + allocated.ToString(CultureInfo.InvariantCulture) + " bytes.");
    }

    [Fact]
    public void xref_entry_budget_follows_the_file_length()
    {
        int entries = PdfXrefReader.MinEntryBudget + 1;
        int padding = entries * PdfXrefReader.FileBytesPerEntry;

        AssertOpenRefused<PdfObjectTooLargeException>(XrefChainDocument(entries, 1, 0), out _);
        using var document = PdfDocument.Open(new MemoryStream(XrefChainDocument(entries, 1, padding)), leaveOpen: true);
        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public void xref_map_memory_grows_with_unique_objects_not_with_prev_sections()
    {
        const int small = 16384;
        const int large = PdfXrefReader.MinEntryBudget;
        long oneSmall = OpenAllocation(XrefChainDocument(small, 1, 0));
        long oneLarge = OpenAllocation(XrefChainDocument(large, 1, 0));
        long threeLarge = OpenAllocation(XrefChainDocument(large, 3, 0));

        double perEntry = (double)(oneLarge - oneSmall) / (large - small);
        double perSectionEntry = (double)(threeLarge - oneLarge) / (2 * large);
        string report = "Per entry " + perEntry.ToString("F1", CultureInfo.InvariantCulture) + " bytes; per extra section entry " + perSectionEntry.ToString("F1", CultureInfo.InvariantCulture) + " bytes.";

        Assert.True(perEntry < 128, report);
        Assert.True(perSectionEntry < 48, report);
    }

    // B-41: a classic subsection header is range-checked before any of its entries are read. The trailing row would
    // otherwise be read as another subsection header, so the message, not only the exception type, shows the refusal.
    [Theory]
    [InlineData("-1 1", true)]
    [InlineData("0 -1", true)]
    [InlineData("2147483647 2", true)]
    [InlineData("2147483648 1", true)]
    [InlineData("0 4294967297", true)]
    [InlineData("2147483647 1", false)]
    public void classic_xref_subsection_outside_the_object_number_range_is_refused(string header, bool refused)
    {
        byte[] pdf = ClassicSubsectionDocument(header);
        if (!refused)
        {
            using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
            Assert.Equal("ok", document.Metadata.Title);
            return;
        }

        AssertOpenRefused<PdfParseException>(pdf, out PdfParseException error);

        Assert.Contains("object number range", error.Message, StringComparison.Ordinal);
    }

    // QA-B36-02: a classic section used to be built in full beside the merged map. It is now merged as it is read, so it
    // costs what an xref stream section costs.
    [Fact]
    public void classic_xref_map_memory_grows_with_unique_objects_not_with_prev_sections()
    {
        const int small = 16384;
        const int large = PdfXrefReader.MinEntryBudget;
        long oneSmall = OpenAllocation(ClassicChainDocument(small, 1));
        long oneLarge = OpenAllocation(ClassicChainDocument(large, 1));
        long threeLarge = OpenAllocation(ClassicChainDocument(large, 3));

        double perEntry = (double)(oneLarge - oneSmall) / (large - small);
        double perSectionEntry = (double)(threeLarge - oneLarge) / (2 * large);
        string report = "Per entry " + perEntry.ToString("F1", CultureInfo.InvariantCulture) + " bytes; per extra section entry " + perSectionEntry.ToString("F1", CultureInfo.InvariantCulture) + " bytes.";

        Assert.True(perEntry < 128, report);
        Assert.True(perSectionEntry < 48, report);
    }

    [Fact]
    public void object_stream_header_is_read_pair_by_pair_without_tokenizing_the_rest()
    {
        const int filler = 4 * 1024 * 1024;
        string fillerText = Repeat(" 1", filler);
        byte[] headerFiller = ObjectStreamDocument("4 0" + fillerText + " ", "<< /Title (ok) >>");
        byte[] bodyFiller = ObjectStreamDocument("4 0 ", "<< /Title (ok) >>" + fillerText);

        long baseline = TitleAllocation(bodyFiller);
        long header = TitleAllocation(headerFiller);

        Assert.True(header < baseline + (4L * 1024 * 1024), "Header filler allocated " + header.ToString(CultureInfo.InvariantCulture) + " bytes; body filler allocated " + baseline.ToString(CultureInfo.InvariantCulture) + ".");
    }

    [Fact]
    public void object_stream_body_allocation_is_proportional_to_decoded_size()
    {
        const int count = 65536;
        byte[] warmup = ObjectStreamData(out int warmupFirst, 8);
        Assert.Equal(8, PdfObjectStreamReader.Parse(warmup, 8, warmupFirst).Count);
        byte[] data = ObjectStreamData(out int first, count);

        long before = GC.GetAllocatedBytesForCurrentThread();
        var objects = PdfObjectStreamReader.Parse(data, count, first);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double factor = (double)allocated / data.Length;

        Assert.Equal(count, objects.Count);
        Assert.True(factor < 16, "Object-stream parsing allocated " + factor.ToString("F1", CultureInfo.InvariantCulture) + " bytes per decoded byte.");
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("+42", 42L)]
    [InlineData("-42", -42L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    public void integer_token_fast_path_preserves_the_full_long_range(string text, long expected)
    {
        byte[] data = Encoding.ASCII.GetBytes(text);
        var parser = new PdfParser(PdfCursor.FromBytes(data), null, 0);

        PdfObject.Num number = Assert.IsType<PdfObject.Num>(parser.ParseValue());

        Assert.True(number.IsInteger);
        Assert.Equal(expected, number.LongValue);
    }

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    public void integer_token_fast_path_still_refuses_values_outside_the_long_range(string text)
    {
        byte[] data = Encoding.ASCII.GetBytes(text);
        var parser = new PdfParser(PdfCursor.FromBytes(data), null, 0);

        PdfParseException error = Assert.Throws<PdfParseException>(() => parser.ParseValue());

        Assert.Contains("Integer is out of range", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void object_stream_count_that_cannot_fit_before_first_is_refused()
    {
        byte[] pdf = ObjectStreamDocument("4 0 ", "<< /Title (ok) >>", count: "3");

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        PdfParseException error = Assert.Throws<PdfParseException>(() => _ = document.Metadata.Title);

        Assert.Contains("/First", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void overlapping_object_stream_objects_are_refused_within_a_work_bound()
    {
        const int depth = 8192;
        var header = new StringBuilder("4 ").Append((2 * depth).ToString(CultureInfo.InvariantCulture));
        for (int i = 0; i < depth; i++)
        {
            header.Append(' ').Append((10 + i).ToString(CultureInfo.InvariantCulture)).Append(' ').Append(i.ToString(CultureInfo.InvariantCulture));
        }

        header.Append(' ');
        byte[] pdf = ObjectStreamDocument(header.ToString(), Repeat("(", depth) + Repeat(")", depth) + "<< /Title (ok) >>", count: (depth + 1).ToString(CultureInfo.InvariantCulture));

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<PdfObjectTooLargeException>(() => _ = document.Metadata.Title);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 16L * 1024 * 1024, "Refusal allocated " + allocated.ToString(CultureInfo.InvariantCulture) + " bytes.");
    }

    [Fact]
    public void deep_page_tree_is_a_parse_error_instead_of_a_stack_overflow()
    {
        byte[] pdf = PageChain(300);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        PdfParseException pages = Assert.Throws<PdfParseException>(() => _ = document.Pages);
        Assert.Throws<PdfParseException>(() => _ = document.PageCount);

        Assert.Contains("too deep", pages.Message, StringComparison.Ordinal);
        Assert.Equal(pdf, stream.ToArray());
    }

    [Fact]
    public void page_tree_within_the_depth_limit_still_opens()
    {
        using var document = PdfDocument.Open(new MemoryStream(PageChain(200)), leaveOpen: true);

        Assert.Equal(1, document.PageCount);
        Assert.Equal(612, document.Pages[0].MediaBox.Width);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void omitted_string_or_stream_crypt_filter_defaults_to_identity(bool revision6, bool stringFilter, bool streamFilter)
    {
        var crypt = new StandardEncryptor(revision6 ? StandardEncryptor.Kind.Revision6 : StandardEncryptor.Kind.Revision4Aes, "user", "owner", -4, encryptMetadata: true);
        byte[] original = Encoding.ASCII.GetBytes("original-attachment-bytes");
        string Text(string value, int id) => stringFilter ? crypt.Text(value, id) : "(" + value + ")";
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [" + Text("notes.bin", 1) + " 6 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title " + Text("Secret", 4) + " >>");
        builder.StreamObject(5, "/Type /EmbeddedFile /Params << /Size " + original.Length.ToString(CultureInfo.InvariantCulture) + " >>", streamFilter ? crypt.Encrypt(original, 5, 0) : original);
        builder.Indirect(6, "<< /Type /Filespec /F " + Text("notes.bin", 6) + " /UF " + Text("notes.bin", 6) + " /EF << /F 5 0 R /UF 5 0 R >> >>");
        builder.Indirect(20, crypt.Dictionary(null, stringFilter, streamFilter));
        byte[] pdf = builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
        byte[] added = Encoding.ASCII.GetBytes("added-attachment-marker");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);

        using (var document = PdfDocument.Open(stream, "user", leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Equal("Secret", document.Metadata.Title);
            Assert.Equal(original, ReadAll(document.Attachments["notes.bin"].Open()));
            editor.Metadata.Title = "Updated";
            editor.Attachments.Add("added.bin", new MemoryStream(added), leaveOpen: true);
            editor.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string appendix = TestCompat.Latin1.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Equal(!streamFilter, appendix.IndexOf("added-attachment-marker", StringComparison.Ordinal) >= 0);
        if (!stringFilter)
        {
            Assert.Contains("(added.bin)", appendix, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("(added.bin)", appendix, StringComparison.Ordinal);
        }

        using var again = PdfDocument.Open(new MemoryStream(saved), "user", leaveOpen: true);
        Assert.Equal("Updated", again.Metadata.Title);
        Assert.Equal(original, ReadAll(again.Attachments["notes.bin"].Open()));
        Assert.Equal(added, ReadAll(again.Attachments["added.bin"].Open()));
    }

    [Theory]
    [InlineData("/Filter 5", "zlib")]
    [InlineData("/Filter [/FlateDecode 5]", "zlib")]
    [InlineData("/Filter /FlateDecode /DecodeParms [5]", "zlib")]
    [InlineData("/Filter /FlateDecode /DecodeParms 5", "zlib")]
    [InlineData("/Filter [/ASCIIHexDecode /FlateDecode] /DecodeParms [<< /Predictor 1 >>]", "hex")]
    [InlineData("/Filter [/ASCIIHexDecode /FlateDecode] /DecodeParms << /Predictor 1 >>", "hex")]
    public void malformed_filter_or_misaligned_decode_parms_are_refused(string filter, string encoding)
    {
        byte[] plain = Encoding.ASCII.GetBytes("attachment-bytes");
        byte[] zlib = TestCompat.Zlib(plain);
        byte[] data = encoding == "hex" ? Encoding.ASCII.GetBytes(TestCompat.ToHexString(zlib) + ">") : zlib;
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(notes.bin) 6 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(5, "/Type /EmbeddedFile " + filter + " /Params << /Size 16 >>", data);
        builder.Indirect(6, "<< /Type /Filespec /UF (notes.bin) /EF << /UF 5 0 R >> >>");
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);

        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            PdfAttachment attachment = document.Attachments["notes.bin"];
            PdfParseException error = Assert.Throws<PdfParseException>(() => ReadAll(attachment.Open()));
            Assert.Contains("Stream /", error.Message, StringComparison.Ordinal);
        }

        Assert.Equal(pdf, stream.ToArray());
    }

    [Fact]
    public void well_formed_filter_arrays_with_null_parms_still_decode()
    {
        byte[] plain = Encoding.ASCII.GetBytes("attachment-bytes");
        byte[] data = Encoding.ASCII.GetBytes(TestCompat.ToHexString(TestCompat.Zlib(plain)) + ">");
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(notes.bin) 6 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(5, "/Type /EmbeddedFile /Filter [/AHx /Fl] /DecodeParms [null null] /Params << /Size 16 >>", data);
        builder.Indirect(6, "<< /Type /Filespec /UF (notes.bin) /EF << /UF 5 0 R >> >>");

        using var document = PdfDocument.Open(new MemoryStream(builder.Finish()), leaveOpen: true);

        Assert.Equal(plain, ReadAll(document.Attachments["notes.bin"].Open()));
    }

    [Theory]
    [InlineData("((((((((((((((((((((((((((((((((")]
    [InlineData(@"(\((\((\((\((\((\((\((\((\((\((\((")]
    [InlineData("(\\\r\n\\101(\\101(\\101(\\101(\\101(\\101(\\101(\\101(\\101(\\101(")]
    public void unterminated_nested_literal_string_is_refused_at_the_cap_before_eof(string text)
    {
        // Without the per-iteration cap, this input reaches EOF and fails with a different parse error.
        var parser = new PdfParser(PdfCursor.FromBytes(Encoding.ASCII.GetBytes(text)), null, 0, 16, 16);

        Assert.Throws<PdfObjectTooLargeException>(() => parser.ParseValue());
    }

    [Theory]
    [InlineData("name")]
    [InlineData("number")]
    [InlineData("keyword")]
    public void token_of_exactly_the_documented_length_parses_and_one_more_byte_is_refused(string kind)
    {
        string Token(int length) => kind switch
        {
            "name" => "/" + new string('a', length),
            "number" => new string('1', length),
            _ => new string('k', length),
        };

        // At the cap a long integer or a bare keyword can still fail as a parse error, but not as an oversized token.
        Exception? atCap = Record.Exception(() => new PdfParser(PdfCursor.FromBytes(Encoding.ASCII.GetBytes(Token(PdfLexer.MaxTokenBytes) + " ")), null, 0).ParseValue());
        Assert.False(atCap is PdfObjectTooLargeException, atCap?.Message);
        Assert.Equal(65536, PdfLexer.MaxTokenBytes);
        Assert.Throws<PdfObjectTooLargeException>(() => new PdfParser(PdfCursor.FromBytes(Encoding.ASCII.GetBytes(Token(PdfLexer.MaxTokenBytes + 1) + " ")), null, 0).ParseValue());
    }

    [Theory]
    [InlineData("/Size 4 /W [1 1 1] /Index [2147483647 2]", typeof(PdfParseException))]
    [InlineData("/W [1 1 1] /Index [0 1 2147483647 2]", typeof(PdfParseException))]
    [InlineData("/W [1 1 1] /Index [0 2147483648]", typeof(PdfParseException))]
    [InlineData("/W [1 1 1] /Index [0 -1 0 3]", typeof(PdfParseException))]
    [InlineData("/W [1 1 1] /Index [2147483646 1]", typeof(PdfObjectTooLargeException))]
    [InlineData("/W [1 1 1] /Index [8388608 1]", typeof(PdfObjectTooLargeException))]
    public void xref_stream_index_that_overflows_or_passes_the_object_cap_is_refused(string dictionary, Type expected)
    {
        byte[] pdf = XrefStream(dictionary, new byte[] { 1, 0, 0 });
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        stream.Position = 0;

        Exception? error = Record.Exception(() => PdfDocument.Open(stream, leaveOpen: true));

        Assert.IsType(expected, error);
        Assert.Equal(pdf, stream.ToArray());
    }

    [Theory]
    [InlineData("/Index [2 1 0 1]", true)]
    [InlineData("/Index [0 2 1 1]", true)]
    [InlineData("/Index [0 2 0 1]", true)]
    [InlineData("/Index [0 1 1 1]", false)]
    [InlineData("/Index [0 1 5 0 2 1]", false)]
    public void xref_stream_index_subsections_must_ascend_without_overlap(string index, bool refused)
    {
        byte[] pdf = XrefStream("/Size 8 /W [1 1 1] " + index, new byte[] { 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0 });

        // Every case fails eventually, because the file has no /Root; only the ordering rule may name the order.
        PdfParseException error = Assert.Throws<PdfParseException>(() => PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true));

        Assert.Equal(refused, error.Message.Contains("ascending", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void deep_page_tree_with_several_kids_per_level_refuses_sign_without_growing_the_destination(int levels, bool opens)
    {
        byte[] pdf = WidePageChain(levels);
        using var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        if (opens)
        {
            Assert.Equal((2 * levels) + 1, document.PageCount);
            return;
        }

        Assert.Throws<PdfParseException>(() => _ = document.PageCount);
        using var destination = new MemoryStream();
        PdfParseException error = Assert.Throws<PdfParseException>(() => editor.Sign(destination, new PdfSignatureOptions { ReservedContentsBytes = 64 }, new FixedContents(), leaveOpen: true));

        Assert.Contains("too deep", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, source.ToArray());
    }

    private static PdfObject ParseSmall(string text, int stringCap, int containerCap)
    {
        var parser = new PdfParser(PdfCursor.FromBytes(Encoding.ASCII.GetBytes(text + " ")), null, 0, stringCap, containerCap);
        return parser.ParseValue();
    }

    private static string Container(string container, int entries)
    {
        var text = new StringBuilder(container == "array" ? "[" : "<<");
        for (int i = 0; i < entries; i++)
        {
            text.Append(container switch
            {
                "array" => "0 ",
                "dictionary" => "/K" + i.ToString(CultureInfo.InvariantCulture) + " 0 ",
                _ => "/A 0 ",
            });
        }

        return text.Append(container == "array" ? "]" : ">>").ToString();
    }

    private static string StringOfLength(string branch, int length)
    {
        return branch switch
        {
            "plain" => "(" + Repeat("a", length) + ")",
            "nested" => "(" + Repeat("(", length / 2) + Repeat(")", length / 2) + Repeat("a", length % 2) + ")",
            "escaped" => "(" + Repeat("\\(", length) + ")",
            "octal" => "(" + Repeat("\\101", length) + ")",
            "carriage-return" => "(" + Repeat("\r", length) + ")",
            "hex" => "<" + Repeat("41", length) + ">",
            _ => "<" + Repeat("41", length - 1) + "4>",
        };
    }

    private static long AssertOpenRefused<T>(byte[] pdf, out T error)
        where T : Exception
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        stream.Position = 0;

        long before = GC.GetAllocatedBytesForCurrentThread();
        error = Assert.Throws<T>(() => PdfDocument.Open(stream, leaveOpen: true));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(pdf, stream.ToArray());
        return allocated;
    }

    private static long TitleAllocation(byte[] pdf)
    {
        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal("ok", document.Metadata.Title);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static byte[] ObjectStreamDocument(string header, string body, string count = "1")
    {
        PdfBuilder builder = PdfBuilder.Basic();
        byte[] data = Encoding.ASCII.GetBytes(header + body);
        builder.StreamObject(5, "/Type /ObjStm /N " + count + " /First " + header.Length.ToString(CultureInfo.InvariantCulture) + " /Filter /FlateDecode", TestCompat.Zlib(data));
        builder.Compressed(4, 5, 0);
        builder.AppendXrefStream(7, "/Info 4 0 R");
        return builder.Snapshot();
    }

    private static byte[] ObjectStreamData(out int first, int count)
    {
        var header = new StringBuilder();
        var body = new StringBuilder(count * 2);
        for (int i = 0; i < count; i++)
        {
            header.Append((i + 1).ToString(CultureInfo.InvariantCulture));
            header.Append(' ');
            header.Append(body.Length.ToString(CultureInfo.InvariantCulture));
            header.Append(' ');
            body.Append("0 ");
        }

        first = header.Length;
        return Encoding.ASCII.GetBytes(header.ToString() + body.ToString());
    }

    private static long OpenAllocation(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        long before = GC.GetAllocatedBytesForCurrentThread();
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(1, document.PageCount);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // The QA-B36-01 shape: xref streams of one-byte entries, each section listing [0 count], chained by /Prev.
    private static byte[] ZeroXrefChain(int count, int sections)
    {
        var builder = new PdfBuilder();
        byte[] data = TestCompat.Zlib(new byte[count]);
        string size = count.ToString(CultureInfo.InvariantCulture);
        long previous = -1;
        for (int section = 0; section < sections; section++)
        {
            long offset = builder.Length;
            string prev = previous < 0 ? string.Empty : " /Prev " + previous.ToString(CultureInfo.InvariantCulture);
            builder.Ascii((section + 1).ToString(CultureInfo.InvariantCulture) + " 0 obj\n<< /Type /XRef /Size " + size + " /W [0 1 0] /Index [0 " + size + "] /Filter /FlateDecode" + prev + " /Length " + data.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
            builder.Bytes(data);
            builder.Ascii("\nendstream\nendobj\n");
            previous = offset;
        }

        builder.Ascii("startxref\n" + previous.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        return builder.Snapshot();
    }

    // A valid one-page file whose xref streams each list [0 entries]: objects 1 to 3 in use, the rest free.
    private static byte[] XrefChainDocument(int entries, int sections, int padding)
    {
        PdfBuilder builder = PdfBuilder.Basic();
        if (padding > 0)
        {
            builder.Ascii("%" + new string(' ', padding) + "\n");
        }

        var rows = new byte[entries * 7];
        for (int id = 1; id <= 3; id++)
        {
            long at = builder.OffsetOf(id);
            rows[id * 7] = 1;
            rows[(id * 7) + 1] = (byte)(at >> 24);
            rows[(id * 7) + 2] = (byte)(at >> 16);
            rows[(id * 7) + 3] = (byte)(at >> 8);
            rows[(id * 7) + 4] = (byte)at;
        }

        byte[] data = TestCompat.Zlib(rows);
        string size = entries.ToString(CultureInfo.InvariantCulture);
        long previous = -1;
        for (int section = 0; section < sections; section++)
        {
            long offset = builder.Length;
            string prev = previous < 0 ? string.Empty : " /Prev " + previous.ToString(CultureInfo.InvariantCulture);
            builder.Ascii((entries + section).ToString(CultureInfo.InvariantCulture) + " 0 obj\n<< /Type /XRef /Size " + size + " /W [1 4 2] /Index [0 " + size + "] /Root 1 0 R /Filter /FlateDecode" + prev + " /Length " + data.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
            builder.Bytes(data);
            builder.Ascii("\nendstream\nendobj\n");
            previous = offset;
        }

        builder.Ascii("startxref\n" + previous.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        return builder.Snapshot();
    }

    // A valid one-page file whose classic xref lists objects 0 to 4, then one more subsection with the given header and
    // a single free row.
    private static byte[] ClassicSubsectionDocument(string header)
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (ok) >>");
        long xref = builder.Length;
        var text = new StringBuilder("xref\n0 5\n0000000000 65535 f\r\n");
        for (int id = 1; id <= 4; id++)
        {
            text.Append(builder.OffsetOf(id).ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n\r\n");
        }

        text.Append(header).Append("\n0000000000 65535 f\r\n");
        text.Append("trailer\n<< /Size 5 /Root 1 0 R /Info 4 0 R >>\nstartxref\n").Append(xref.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
        builder.Ascii(text.ToString());
        return builder.Snapshot();
    }

    // The classic counterpart of XrefChainDocument: each section lists [0 entries], objects 1 to 3 in use, the rest free.
    private static byte[] ClassicChainDocument(int entries, int sections)
    {
        PdfBuilder builder = PdfBuilder.Basic();
        var rows = new StringBuilder(entries * 20);
        rows.Append("0000000000 65535 f\r\n");
        for (int id = 1; id < entries; id++)
        {
            rows.Append(id <= 3 ? builder.OffsetOf(id).ToString("D10", CultureInfo.InvariantCulture) + " 00000 n\r\n" : "0000000000 65535 f\r\n");
        }

        string size = entries.ToString(CultureInfo.InvariantCulture);
        string table = rows.ToString();
        long previous = -1;
        for (int section = 0; section < sections; section++)
        {
            long offset = builder.Length;
            string prev = previous < 0 ? string.Empty : " /Prev " + previous.ToString(CultureInfo.InvariantCulture);
            builder.Ascii("xref\n0 " + size + "\n" + table + "trailer\n<< /Size " + size + " /Root 1 0 R" + prev + " >>\n");
            previous = offset;
        }

        builder.Ascii("startxref\n" + previous.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        return builder.Snapshot();
    }

    private static byte[] XrefStream(string dictionary, byte[] data)
    {
        var builder = new PdfBuilder();
        long offset = builder.Length;
        builder.Ascii("1 0 obj\n<< /Type /XRef " + dictionary + " /Length " + data.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
        builder.Bytes(data);
        builder.Ascii("\nendstream\nendobj\nstartxref\n" + offset.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        return builder.Snapshot();
    }

    private static byte[] PageChain(int levels)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 10 0 R >>");
        for (int level = 0; level < levels; level++)
        {
            int id = 10 + level;
            string kid = level == levels - 1 ? "3" : (id + 1).ToString(CultureInfo.InvariantCulture);
            string parent = level == 0 ? string.Empty : " /Parent " + (id - 1).ToString(CultureInfo.InvariantCulture) + " 0 R";
            builder.Indirect(id, "<< /Type /Pages /Count 1 /Kids [" + kid + " 0 R]" + parent + (level == 0 ? " /MediaBox [0 0 612 792]" : string.Empty) + " >>");
        }

        builder.Indirect(3, "<< /Type /Page /Parent " + (10 + levels - 1).ToString(CultureInfo.InvariantCulture) + " 0 R >>");
        return builder.Finish();
    }

    private static byte[] WidePageChain(int levels)
    {
        // Each /Pages level has a leaf page on each side of the next level, so the walk sees several kids per node.
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 10 0 R >>");
        for (int level = 0; level < levels; level++)
        {
            int id = 10 + level;
            int leaf = 100000 + (2 * level);
            string next = level == levels - 1 ? "3" : (id + 1).ToString(CultureInfo.InvariantCulture);
            string parent = level == 0 ? string.Empty : " /Parent " + (id - 1).ToString(CultureInfo.InvariantCulture) + " 0 R";
            string count = ((2 * (levels - level)) + 1).ToString(CultureInfo.InvariantCulture);
            string self = id.ToString(CultureInfo.InvariantCulture);
            builder.Indirect(id, "<< /Type /Pages /Count " + count + " /Kids [" + leaf.ToString(CultureInfo.InvariantCulture) + " 0 R " + next + " 0 R " + (leaf + 1).ToString(CultureInfo.InvariantCulture) + " 0 R]" + parent + (level == 0 ? " /MediaBox [0 0 612 792]" : string.Empty) + " >>");
            builder.Indirect(leaf, "<< /Type /Page /Parent " + self + " 0 R >>");
            builder.Indirect(leaf + 1, "<< /Type /Page /Parent " + self + " 0 R >>");
        }

        builder.Indirect(3, "<< /Type /Page /Parent " + (10 + levels - 1).ToString(CultureInfo.InvariantCulture) + " 0 R >>");
        return builder.Finish();
    }

    private static string Repeat(string unit, int count)
    {
        var builder = new StringBuilder(unit.Length * count);
        for (int i = 0; i < count; i++)
        {
            builder.Append(unit);
        }

        return builder.ToString();
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

    private sealed class FixedContents : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(new byte[] { 1, 2, 3, 4 });
        }
    }
}
