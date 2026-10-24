using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-108 and D-49: PdfCursor keeps its most recently filled 64 KiB windows within one public operation, at most
// 1 MiB, and the engine releases them when the operation returns.
public class CursorWindowTests
{
    private const int Window = PdfCursor.WindowSize;
    private const long Ceiling = 1024 * 1024;

    // Each object of the spread fixture sits in its own 64 KiB region.
    private const int SpreadPages = 24;
    private const int Padding = Window + 4096;

    [Fact]
    public void a_seek_inside_a_retained_window_reads_nothing()
    {
        byte[] data = Pattern(300 * 1024);
        var probe = new ProbeStream(new MemoryStream(data, writable: false));
        var cursor = new PdfCursor(probe);

        cursor.Seek(1000);
        Assert.Equal(data[1000], cursor.ReadByte());
        cursor.Seek(200 * 1024);
        Assert.Equal(data[200 * 1024], cursor.ReadByte());
        Assert.Equal(2, probe.ReadCalls);

        cursor.Seek(2000);
        Assert.Equal(data[2000], cursor.ReadByte());
        cursor.Seek(1000 + Window - 1);
        Assert.Equal(data[1000 + Window - 1], cursor.ReadByte());
        cursor.Seek((200 * 1024) + 10);
        Assert.Equal(data[(200 * 1024) + 10], cursor.ReadByte());
        Assert.Equal(2, probe.ReadCalls);

        // The first byte past a retained window is a fill of its own.
        cursor.Seek(1000 + Window);
        Assert.Equal(data[1000 + Window], cursor.ReadByte());
        Assert.Equal(3, probe.ReadCalls);
        Assert.Equal(3L * Window, probe.BytesRead);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Sweep)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(PdfCursor.DefaultRetainedWindows)]
    [InlineData(16)]
    public void retained_windows_read_the_same_bytes_as_a_cursor_without_windows(int windows)
    {
        byte[] data = Pattern((2 * 1024 * 1024) + 777);
        var probe = new ProbeStream(new MemoryStream(data, writable: false));
        var cursor = new PdfCursor(probe, windows);
        var reference = new PdfCursor(new MemoryStream(data, writable: false), 0);
        var random = new Random(108);
        for (int step = 0; step < 1500; step++)
        {
            // Mostly near a few hot spots, sometimes anywhere, sometimes past the end.
            int choice = random.Next(10);
            long position = choice < 6
                ? Math.Max(0, ((random.Next(4) * data.Length) / 4) + random.Next(-Window, 2 * Window))
                : choice < 9 ? random.Next(data.Length) : data.Length + random.Next(16);
            cursor.Seek(position);
            reference.Seek(position);
            int count = random.Next(1, 2 * Window);
            bool same = true;
            for (int i = 0; i < count && same; i++)
            {
                if (i % 97 == 0)
                {
                    same = reference.PeekByte() == cursor.PeekByte();
                }

                int expected = reference.ReadByte();
                same = same && expected == cursor.ReadByte() && reference.Position == cursor.Position;
                if (expected < 0)
                {
                    break;
                }
            }

            Assert.True(same, "step " + step.ToString(CultureInfo.InvariantCulture) + " at " + position.ToString(CultureInfo.InvariantCulture));
        }

        Assert.True(cursor.PeakRetainedBytes <= (long)windows * Window);
        Assert.True(cursor.PeakRetainedBytes <= Ceiling);
    }

    [Fact]
    public void a_window_that_ended_at_the_earlier_end_is_refilled_after_an_append()
    {
        var stream = new MemoryStream();
        byte[] head = Encoding.ASCII.GetBytes("0123456789");
        stream.Write(head, 0, head.Length);
        var cursor = new PdfCursor(stream);
        cursor.Seek(0);
        Assert.Equal("0123456789", ReadToEnd(cursor));

        byte[] tail = Encoding.ASCII.GetBytes("abcdef");
        stream.Seek(0, SeekOrigin.End);
        stream.Write(tail, 0, tail.Length);
        cursor.Seek(5);

        Assert.Equal("56789abcdef", ReadToEnd(cursor));
    }

    [Fact]
    public void a_same_stream_save_reads_its_own_appended_revision()
    {
        byte[] pdf = SmallForm();
        var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("Alpha", Assert.Single(document.AcroForm.Fields).Value);

        document.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["name"] = "Saved" });
        document.Metadata.Title = "Saved title";
        document.SaveIncremental();

        Assert.True(stream.Length > pdf.Length);
        Assert.Equal("Saved", Assert.Single(document.AcroForm.Fields).Value);
        Assert.Equal("Saved title", document.Metadata.Title);
    }

    // The spread fixture needs more than 16 windows, so the ceiling is reached; every result matches a cursor that
    // keeps no windows, and the retained windows never read more than it does.
    [Fact]
    [Trait(Lane.Trait, Lane.Sweep)]
    public void every_lookup_in_a_different_region_stays_under_the_ceiling_and_matches_a_cursor_without_windows()
    {
        byte[] pdf = SpreadForm();
        Assert.True(pdf.Length > 2 * SpreadPages * Window);

        var retainedProbe = new ProbeStream(new MemoryStream(pdf, writable: false));
        var plainProbe = new ProbeStream(new MemoryStream(pdf, writable: false));
        using var retained = PdfDocument.OpenWithCursorWindows(retainedProbe, leaveOpen: true, PdfCursor.DefaultRetainedWindows);
        using var plain = PdfDocument.OpenWithCursorWindows(plainProbe, leaveOpen: true, 0);

        string expected = Walk(plain);
        Assert.Equal(expected, Walk(retained));
        Assert.Contains("f23=value 23@24", expected, StringComparison.Ordinal);
        Assert.Equal(0, plain.CursorPeakRetainedBytes);
        Assert.Equal((long)PdfCursor.DefaultRetainedWindows * Window, retained.CursorPeakRetainedBytes);
        Assert.True(retained.CursorPeakRetainedBytes <= Ceiling);
        Assert.True(retainedProbe.BytesRead <= plainProbe.BytesRead);
        Assert.True(retainedProbe.ReadCalls <= plainProbe.ReadCalls);

        var values = new Dictionary<string, PdfFieldValue>();
        for (int i = 0; i < SpreadPages; i++)
        {
            values["f" + i.ToString(CultureInfo.InvariantCulture)] = "filled " + i.ToString(CultureInfo.InvariantCulture);
        }

        retained.AcroForm.Fill(values);
        plain.AcroForm.Fill(values);
        var retainedOutput = new MemoryStream();
        var plainOutput = new MemoryStream();
        retained.SaveIncremental(retainedOutput, leaveOpen: true);
        plain.SaveIncremental(plainOutput, leaveOpen: true);
        Assert.Equal(plainOutput.ToArray(), retainedOutput.ToArray());
    }

    // In each of these, the earlier step fills a window that covers the field's second-revision value, and the test
    // changes that value in the source before the next operation reads it. The next operation's own reads stop short
    // of the second revision, so a window served across the boundary shows the old value.
    [Fact]
    public void bytes_read_by_open_are_not_served_to_the_first_operation()
    {
        MemoryStream source = TwoRevisionForm("<< /Names [] >>");
        using var document = PdfDocument.Open(source, leaveOpen: true);
        Replace(source, "Alpha", "Bravo");

        Assert.Equal("Bravo", Assert.Single(document.AcroForm.Fields).Value);
    }

    [Fact]
    public void bytes_read_by_one_operation_are_not_served_to_the_next()
    {
        MemoryStream source = TwoRevisionForm("<< /Names [] >>");
        var probe = new ProbeStream(source);
        using var document = PdfDocument.Open(probe, leaveOpen: true);
        int calls = probe.ReadCalls;
        Assert.Empty(document.Attachments);
        Assert.True(probe.ReadCalls > calls);
        Replace(source, "Alpha", "Bravo");

        Assert.Equal("Bravo", Assert.Single(document.AcroForm.Fields).Value);
    }

    [Fact]
    public void bytes_read_by_a_failed_operation_are_not_served_to_the_next()
    {
        MemoryStream source = TwoRevisionForm("<< /Names [ >>");
        var probe = new ProbeStream(source);
        using var document = PdfDocument.Open(probe, leaveOpen: true);
        int calls = probe.ReadCalls;
        Assert.Throws<PdfParseException>(() => document.Attachments.Count);
        Assert.True(probe.ReadCalls > calls);
        Replace(source, "Alpha", "Bravo");

        Assert.Equal("Bravo", Assert.Single(document.AcroForm.Fields).Value);
    }

    // Parse failures keep their exception types, offsets, and messages: every fixture, whole and truncated, walks
    // the same with retained windows as with none.
    [Fact]
    [Trait(Lane.Trait, Lane.Corpus)]
    [Trait(Lane.Trait, Lane.Sweep)]
    public void fixtures_walk_the_same_with_and_without_retained_windows()
    {
        string fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        string[] files = Directory.GetFiles(fixtures, "*.pdf").Concat(Directory.GetFiles(CorpusManifest.Directory, "*.pdf")).OrderBy(file => file, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(files);
        int failures = 0;
        foreach (string file in files)
        {
            byte[] whole = File.ReadAllBytes(file);
            foreach (int length in new[] { whole.Length, whole.Length - 1, (whole.Length * 3) / 4, whole.Length / 2 })
            {
                var input = new byte[length];
                Buffer.BlockCopy(whole, 0, input, 0, length);
                string plain = Outcome(input, 0);
                string retained = Outcome(input, PdfCursor.DefaultRetainedWindows);
                Assert.True(plain == retained, Path.GetFileName(file) + " at " + length.ToString(CultureInfo.InvariantCulture) + " bytes: " + plain + " | " + retained);
                if (plain.StartsWith("!", StringComparison.Ordinal))
                {
                    failures++;
                }
            }
        }

        // The truncations reach the error paths.
        Assert.True(failures > files.Length);
    }

    private static string Outcome(byte[] input, int windows)
    {
        try
        {
            using PdfDocument document = PdfDocument.OpenWithCursorWindows(new MemoryStream(input, writable: false), leaveOpen: false, windows);
            return Walk(document);
        }
        catch (PdfParseException ex)
        {
            return "!" + ex.GetType().Name + " " + ex.Offset.ToString(CultureInfo.InvariantCulture) + " " + ex.Message;
        }
        catch (PdfException ex)
        {
            return "!" + ex.GetType().Name + " " + ex.Message;
        }
    }

    // Each read is its own public operation; a failure is part of the result.
    private static string Walk(PdfDocument document)
    {
        var result = new StringBuilder();
        Step(result, () => document.PageCount.ToString(CultureInfo.InvariantCulture));
        Step(result, () => string.Join(",", document.Pages.Select(page => page.Number.ToString(CultureInfo.InvariantCulture) + ":" + page.Rotation.ToString(CultureInfo.InvariantCulture))));
        Step(result, () => document.Metadata.Title ?? "-");
        Step(result, () => string.Join(",", document.AcroForm.Fields.Select(field => field.FullName + "=" + (field.Value ?? "-") + "@" + field.PageNumber.ToString(CultureInfo.InvariantCulture))));
        Step(result, () => string.Join(",", document.Attachments.Select(attachment => attachment.Name)));
        Step(result, () => document.Signatures.Count.ToString(CultureInfo.InvariantCulture));
        Step(result, () => document.Revisions.Count.ToString(CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static void Step(StringBuilder result, Func<string> read)
    {
        try
        {
            result.Append(read()).Append('|');
        }
        catch (PdfParseException ex)
        {
            result.Append('!').Append(ex.GetType().Name).Append(' ').Append(ex.Offset.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(ex.Message).Append('|');
        }
        catch (PdfException ex)
        {
            result.Append('!').Append(ex.GetType().Name).Append(' ').Append(ex.Message).Append('|');
        }
    }

    // A one-field form filled to "Alpha" in an appended revision. The catalog, page tree, and font sit in the first
    // 64 KiB, padding follows, and the original field and the attachment name tree end the first revision, so
    // only a window filled at the name tree or at the first cross-reference section reaches the second revision.
    private static MemoryStream TwoRevisionForm(string nameTree)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> /Names << /EmbeddedFiles 7 0 R >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>");
        builder.Indirect(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        builder.Ascii("%" + new string('x', Padding) + "\n");
        builder.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (Start) /Rect [72 720 300 744] /P 3 0 R /DA (/Helv 10 Tf 0 g) >>");
        builder.Indirect(7, nameTree);
        byte[] pdf = builder.Finish();
        var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(source, leaveOpen: true))
        {
            document.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["name"] = "Alpha" });
            document.SaveIncremental();
        }

        Assert.True(new ReadOnlySpan<byte>(source.GetBuffer(), 0, pdf.Length).IndexOf(Encoding.ASCII.GetBytes("Alpha")) < 0);
        return source;
    }

    // One text field, with its value in a small file that one window covers.
    private static byte[] SmallForm()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>");
        builder.Indirect(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        builder.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (Alpha) /Rect [72 720 300 744] /P 3 0 R /DA (/Helv 10 Tf 0 g) >>");
        builder.Indirect(6, "<< /Title (Small) >>");
        return builder.Finish("/Info 6 0 R");
    }

    // SpreadPages pages, each page and each widget padded into a 64 KiB region of its own, and the field array in
    // an order that jumps between regions.
    private static byte[] SpreadForm()
    {
        var builder = new PdfBuilder();
        string pad = "%" + new string('x', Padding) + "\n";
        var fields = new StringBuilder();
        var kids = new StringBuilder();
        for (int i = 0; i < SpreadPages; i++)
        {
            fields.Append(Id(11 + (3 * ((i * 7) % SpreadPages)))).Append(' ');
            kids.Append(Id(10 + (3 * i))).Append(' ');
        }

        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [" + fields + "] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> >>");
        builder.Ascii(pad);
        builder.Indirect(2, "<< /Type /Pages /Count " + SpreadPages.ToString(CultureInfo.InvariantCulture) + " /Kids [" + kids + "] >>");
        builder.Ascii(pad);
        builder.Indirect(3, "<< /Title (Spread) >>");
        builder.Indirect(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        for (int i = 0; i < SpreadPages; i++)
        {
            int page = 10 + (3 * i);
            string number = i.ToString(CultureInfo.InvariantCulture);
            builder.Ascii(pad);
            builder.Indirect(page, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [" + Id(page + 1) + "] >>");
            builder.Ascii(pad);
            builder.Indirect(page + 1, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (f" + number + ") /V (value " + number + ") /Rect [72 720 300 744] /P " + Id(page) + " /DA (/Helv 10 Tf 0 g) >>");
        }

        builder.Ascii(pad);
        return builder.Finish("/Info 3 0 R");
    }

    private static string Id(int id)
    {
        return id.ToString(CultureInfo.InvariantCulture) + " 0 R";
    }

    private static byte[] Pattern(int length)
    {
        var data = new byte[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = (byte)((i * 31) ^ (i >> 8));
        }

        return data;
    }

    private static string ReadToEnd(PdfCursor cursor)
    {
        var text = new StringBuilder();
        int value;
        while ((value = cursor.ReadByte()) >= 0)
        {
            text.Append((char)value);
        }

        return text.ToString();
    }

    // Replaces every occurrence in place: the field value and its appearance text.
    private static void Replace(MemoryStream stream, string from, string to)
    {
        byte[] buffer = stream.GetBuffer();
        byte[] find = Encoding.ASCII.GetBytes(from);
        int replaced = 0;
        int at;
        while ((at = new ReadOnlySpan<byte>(buffer, 0, (int)stream.Length).IndexOf(find)) >= 0)
        {
            Encoding.ASCII.GetBytes(to, 0, to.Length, buffer, at);
            replaced++;
        }

        Assert.True(replaced > 0);
    }
}
