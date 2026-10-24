using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-115: the parser reads tokens through one buffer, reuses fixed strings for common names and keywords, and builds
// containers at their final size. These tests pin the parse results those changes must not alter and the allocation
// they removed.
public class ParserAllocationTests
{
    [Fact]
    public void common_token_entries_are_distinct_ascii_and_each_is_found_as_itself()
    {
        IReadOnlyList<string> entries = PdfCommonTokens.Entries;

        Assert.Equal(entries.Count, entries.Distinct(StringComparer.Ordinal).Count());
        foreach (string entry in entries)
        {
            Assert.All(entry, c => Assert.InRange(c, (char)0x20, (char)0x7E));
            byte[] bytes = Encoding.ASCII.GetBytes(entry);
            Assert.Same(entry, PdfCommonTokens.Find(bytes, bytes.Length));
        }
    }

    [Theory]
    [InlineData("Unlisted")]
    [InlineData("Typ")]
    [InlineData("TypeX")]
    [InlineData("type")]
    public void a_token_that_is_not_an_entry_is_not_found(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text + "Type");

        Assert.Null(PdfCommonTokens.Find(bytes, text.Length));
    }

    [Theory]
    [InlineData("/Type", "Type")]
    [InlineData("/Typ#65", "Type")]
    [InlineData("/#54ype", "Type")]
    [InlineData("/", "")]
    [InlineData("/A#20B", "A B")]
    [InlineData("/#", "#")]
    [InlineData("/A#4", "A#4")]
    [InlineData("/A##", "A##")]
    [InlineData("/Na#C3#A9me", "Naéme")]
    [InlineData("/Bad#FF", "Bad�")]
    public void names_decode_escapes_and_utf8_and_reuse_a_common_string(string text, string expected)
    {
        PdfObject.Name name = Assert.IsType<PdfObject.Name>(Parse(text));

        Assert.Equal(expected, name.Value);
        byte[] bytes = Encoding.UTF8.GetBytes(expected);
        string? common = PdfCommonTokens.Find(bytes, bytes.Length);
        if (common != null)
        {
            Assert.Same(common, name.Value);
        }
    }

    [Fact]
    public void a_name_with_a_bad_escape_is_refused_as_before()
    {
        PdfParseException error = Assert.Throws<PdfParseException>(() => Parse("/A#4G"));

        Assert.Equal(0, error.Offset);
        Assert.Contains("Invalid hex digit", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("truex", 5)]
    [InlineData("R", 1)]
    [InlineData("endobj", 6)]
    [InlineData("été", 3)]
    public void an_unexpected_keyword_is_named_in_the_error_at_its_end(string keyword, long offset)
    {
        byte[] data = TestCompat.Latin1.GetBytes(keyword + " ");

        PdfParseException error = Assert.Throws<PdfParseException>(() => new PdfParser(PdfCursor.FromBytes(data), null, 0).ParseValue());

        Assert.Equal(offset, error.Offset);
        Assert.Contains("Unexpected token '" + keyword + "'.", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".5", 0.5)]
    [InlineData("-.5", -0.5)]
    [InlineData("+3.25", 3.25)]
    [InlineData("1.", 1.0)]
    [InlineData("00012.500", 12.5)]
    [InlineData("-0.0", 0.0)]
    [InlineData("612.0000001", 612.0000001)]
    public void reals_parse_to_the_same_value(string text, double expected)
    {
        PdfObject.Num number = Assert.IsType<PdfObject.Num>(Parse(text));

        Assert.False(number.IsInteger);
        Assert.Equal(expected, number.DoubleValue);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("-.")]
    [InlineData("+.")]
    public void a_real_without_digits_is_refused(string text)
    {
        PdfParseException error = Assert.Throws<PdfParseException>(() => Parse(text));

        Assert.Contains("Invalid number", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(40)]
    public void a_repeated_dictionary_key_keeps_its_first_position_and_takes_the_last_value(int distinct)
    {
        var text = new StringBuilder("<<");
        for (int i = 0; i < distinct; i++)
        {
            text.Append(" /K").Append(i.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(i.ToString(CultureInfo.InvariantCulture));
        }

        text.Append(" /K0 (first again) /K").Append((distinct - 1).ToString(CultureInfo.InvariantCulture)).Append(" (last again) >>");

        PdfObject.Dict dict = Assert.IsType<PdfObject.Dict>(Parse(text.ToString()));

        Assert.Equal(distinct, dict.Items.Count);
        Assert.Equal(Enumerable.Range(0, distinct).Select(i => "K" + i.ToString(CultureInfo.InvariantCulture)), dict.Items.Select(item => item.Key));
        Assert.Equal("first again", Encoding.ASCII.GetString(Assert.IsType<PdfObject.Str>(dict.Get("K0")).Raw));
        Assert.Equal("last again", Encoding.ASCII.GetString(Assert.IsType<PdfObject.Str>(dict.Get("K" + (distinct - 1).ToString(CultureInfo.InvariantCulture))).Raw));
    }

    [Fact]
    public void nested_containers_keep_their_own_entries()
    {
        PdfObject.Dict dict = Assert.IsType<PdfObject.Dict>(Parse("<< /A [1 [2 << /B [3 4] /C << /D 5 >> >>] 6] /E << /F [7] >> /G 8 >>"));

        Assert.Equal(new[] { "A", "E", "G" }, dict.Items.Select(item => item.Key));
        PdfObject.Arr a = Assert.IsType<PdfObject.Arr>(dict.Get("A"));
        Assert.Equal(3, a.Items.Count);
        PdfObject.Arr inner = Assert.IsType<PdfObject.Arr>(a.Items[1]);
        Assert.Equal(2, inner.Items.Count);
        PdfObject.Dict innerDict = Assert.IsType<PdfObject.Dict>(inner.Items[1]);
        Assert.Equal(new[] { "B", "C" }, innerDict.Items.Select(item => item.Key));
        Assert.Equal(2, Assert.IsType<PdfObject.Arr>(innerDict.Get("B")).Items.Count);
        Assert.Equal(6L, Assert.IsType<PdfObject.Num>(a.Items[2]).LongValue);
        Assert.Single(Assert.IsType<PdfObject.Arr>(Assert.IsType<PdfObject.Dict>(dict.Get("E")).Get("F")).Items);
    }

    [Fact]
    public void literal_and_hex_strings_parse_to_the_same_bytes()
    {
        PdfObject.Arr array = Assert.IsType<PdfObject.Arr>(Parse("[(a\\(b\\)c\\\\d\\n\\101\\0\\7777\r\nx) <41 4> <> ()]"));

        Assert.Equal(new byte[] { (byte)'a', (byte)'(', (byte)'b', (byte)')', (byte)'c', (byte)'\\', (byte)'d', (byte)'\n', (byte)'A', 0, 0xFF, (byte)'7', (byte)'\n', (byte)'x' }, Assert.IsType<PdfObject.Str>(array.Items[0]).Raw);
        Assert.Equal(new byte[] { 0x41, 0x40 }, Assert.IsType<PdfObject.Str>(array.Items[1]).Raw);
        Assert.Empty(Assert.IsType<PdfObject.Str>(array.Items[2]).Raw);
        Assert.Empty(Assert.IsType<PdfObject.Str>(array.Items[3]).Raw);
    }

    [Theory]
    [InlineData("5 7 9", false)]
    [InlineData("9 7 5", false)]
    [InlineData("5 7 6", false)]
    [InlineData("5 7 5", true)]
    [InlineData("7 5 7", true)]
    [InlineData("10 20 30 15 25 10", true)]
    [InlineData("0 0", true)]
    public void an_object_stream_repeat_is_refused_whether_or_not_numbers_ascend(string ids, bool refused)
    {
        foreach (string member in new[] { "1", "null", "true" })
        {
            int[] numbers = ids.Split(' ').Select(id => int.Parse(id, CultureInfo.InvariantCulture)).ToArray();
            byte[] data = ObjectStream(out int first, numbers.Select(id => (id, member)).ToArray());

            Exception? error = Record.Exception(() => PdfParser.ParseObjectStream(data, numbers.Length, first));

            if (refused)
            {
                PdfParseException parse = Assert.IsType<PdfParseException>(error);
                Assert.Contains("repeats object number", parse.Message, StringComparison.Ordinal);
            }
            else
            {
                Assert.Null(error);
            }
        }
    }

    [Fact]
    public void a_buffer_cursor_reads_every_byte_and_seeks_without_a_window_copy()
    {
        var data = new byte[(3 * PdfCursor.WindowSize) + 17];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i * 31);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        PdfCursor cursor = PdfCursor.FromBytes(data);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024, "A buffer cursor allocated " + allocated.ToString(CultureInfo.InvariantCulture) + " bytes.");

        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(data[i], cursor.ReadByte());
        }

        Assert.Equal(-1, cursor.ReadByte());
        cursor.Seek(PdfCursor.WindowSize - 1);
        Assert.Equal(data[PdfCursor.WindowSize - 1], cursor.PeekByte());
        Assert.Equal(data[PdfCursor.WindowSize - 1], cursor.ReadByte());
        Assert.Equal(data[PdfCursor.WindowSize], cursor.ReadByte());
        cursor.Release();
        Assert.Equal(PdfCursor.WindowSize + 1, cursor.Position);
        Assert.Equal(data[PdfCursor.WindowSize + 1], cursor.ReadByte());
        cursor.Seek(data.Length + 100L);
        Assert.Equal(-1, cursor.ReadByte());
        cursor.Seek(3);
        Assert.Equal(data[3], cursor.ReadByte());
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void a_reference_chain_resolves_up_to_the_depth_limit(int references, bool resolves)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox 10 0 R >>");
        for (int i = 0; i < references - 1; i++)
        {
            builder.Indirect(10 + i, (11 + i).ToString(CultureInfo.InvariantCulture) + " 0 R");
        }

        builder.Indirect(10 + references - 1, "[0 0 612 792]");
        using var document = PdfDocument.Open(new MemoryStream(builder.Finish()), leaveOpen: true);

        if (resolves)
        {
            Assert.Equal(612, document.Pages[0].MediaBox.Width);
            return;
        }

        PdfParseException error = Assert.Throws<PdfParseException>(() => _ = document.Pages);
        Assert.Contains("chain is too deep", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("10 0 R", "10 0 R")]
    [InlineData("11 0 R", "10 0 R")]
    [InlineData("11 0 R", "12 0 R")]
    public void a_reference_cycle_is_refused(string tenRefersTo, string elevenRefersTo)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox 10 0 R >>");
        builder.Indirect(10, tenRefersTo);
        builder.Indirect(11, elevenRefersTo);
        builder.Indirect(12, "10 0 R");
        using var document = PdfDocument.Open(new MemoryStream(builder.Finish()), leaveOpen: true);

        PdfParseException error = Assert.Throws<PdfParseException>(() => _ = document.Pages);

        Assert.Contains("Cyclic indirect object reference", error.Message, StringComparison.Ordinal);
    }

    // On net10.0 this widget stream allocated 32.5 bytes per decoded byte before W-115 and 8.2 after it.
    [Fact]
    public void a_widget_object_stream_parses_within_an_allocation_bound()
    {
        const int count = 2000;
        var members = new (int, string)[count];
        for (int i = 0; i < count; i++)
        {
            string id = i.ToString(CultureInfo.InvariantCulture);
            members[i] = (100 + i, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (page" + id + ".name) /Rect [72 " + id + ".5 300 720] /P 3 0 R /F 4 /DA (/Helv 0 Tf 0 g) /MK << /BC [0 0 0] >> /AP << /N 9 0 R >> >>");
        }

        byte[] data = ObjectStream(out int first, members);
        Assert.Equal(count, PdfParser.ParseObjectStream(data, count, first).Count);

        long before = GC.GetAllocatedBytesForCurrentThread();
        List<PdfObject> objects = PdfParser.ParseObjectStream(data, count, first);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double factor = (double)allocated / data.Length;

        Assert.Equal(count, objects.Count);
        Assert.True(factor < 12, "Widget object-stream parsing allocated " + factor.ToString("F1", CultureInfo.InvariantCulture) + " bytes per decoded byte.");
    }

    private static PdfObject Parse(string text)
    {
        return new PdfParser(PdfCursor.FromBytes(Encoding.UTF8.GetBytes(text + " ")), null, 0).ParseValue();
    }

    private static byte[] ObjectStream(out int first, params (int Id, string Body)[] members)
    {
        var header = new StringBuilder();
        var body = new StringBuilder();
        foreach ((int id, string member) in members)
        {
            header.Append(id.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(body.Length.ToString(CultureInfo.InvariantCulture)).Append(' ');
            body.Append(member).Append(' ');
        }

        first = header.Length;
        return Encoding.ASCII.GetBytes(header.ToString() + body.ToString());
    }
}
