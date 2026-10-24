using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-132 (spec R13, D-55): a document opens from an IPdfSource snapshot or a seekable stream through its adapter. An
// asynchronous open awaits every read, reads may be partial, ownership follows leaveOpen, cancellation returns no
// document, and a stream whose length changes under the document fails instead of mixing versions.
public class SourceTests
{
    public static TheoryData<string> Fixtures => new TheoryData<string> { "classic", "chain", "rc4", "padded" };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task an_async_open_awaits_every_read_and_never_reads_synchronously(string fixture)
    {
        var source = new TestPdfSource(Fixture(fixture)) { AllowSync = false, Yield = true };

        using PdfDocument document = await PdfDocument.OpenAsync(source, Password(fixture), leaveOpen: true);

        Assert.True(source.AsyncReads > 0);
        Assert.Equal(0, source.SyncReads);
        source.AllowSync = true;
        Assert.Equal(1, document.PageCount);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task sync_and_async_opens_read_the_same_document(string fixture)
    {
        byte[] pdf = Fixture(fixture);
        using PdfDocument fromStream = PdfDocument.Open(new MemoryStream(pdf, writable: false), Password(fixture));
        using PdfDocument fromSource = PdfDocument.Open(new TestPdfSource(pdf), Password(fixture), leaveOpen: false);
        using PdfDocument fromAsync = await PdfDocument.OpenAsync(new TestPdfSource(pdf), Password(fixture), leaveOpen: false);
        using PdfDocument fromAsyncStream = await PdfDocument.OpenAsync(new MemoryStream(pdf, writable: false), Password(fixture));

        foreach (PdfDocument document in new[] { fromSource, fromAsync, fromAsyncStream })
        {
            Assert.Equal(Describe(fromStream), Describe(document));
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(7, false)]
    [InlineData(7, true)]
    [InlineData(65535, true)]
    public async Task partial_reads_open_and_save_the_same_bytes_as_a_whole_stream(int maxRead, bool useAsync)
    {
        byte[] pdf = Fixture("rc4");
        byte[] expected = FillAndSave(PdfDocument.Open(new MemoryStream(pdf, writable: false), "user"));
        var source = new TestPdfSource(pdf) { MaxRead = maxRead };

        PdfDocument document = useAsync
            ? await PdfDocument.OpenAsync(source, "user", leaveOpen: false)
            : PdfDocument.Open(source, "user", leaveOpen: false);

        Assert.Equal(expected, FillAndSave(document));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_zero_byte_read_before_the_claimed_length_reports_truncation(bool useAsync)
    {
        byte[] pdf = Fixture("classic");
        var source = new TestPdfSource(pdf, claimedLength: pdf.Length + 100_000);

        if (useAsync)
        {
            await Assert.ThrowsAsync<PdfParseException>(() => PdfDocument.OpenAsync(source, leaveOpen: true));
        }
        else
        {
            Assert.Throws<PdfParseException>(() => PdfDocument.Open(source, leaveOpen: true));
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task a_canceled_open_returns_no_document_and_disposes_only_an_owned_source(int readsBeforeCancel, bool leaveOpen)
    {
        byte[] pdf = Fixture("chain");
        using var cancel = new CancellationTokenSource();
        var source = new TestPdfSource(pdf) { AllowSync = false, Yield = true };
        source.BeforeAsyncRead = reads =>
        {
            if (reads == readsBeforeCancel)
            {
                cancel.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfDocument.OpenAsync(source, leaveOpen, cancel.Token));

        Assert.Equal(!leaveOpen, source.Disposed);
        if (leaveOpen)
        {
            source.BeforeAsyncRead = null;
            source.AllowSync = true;
            using PdfDocument reopened = await PdfDocument.OpenAsync(source, leaveOpen: true);
            Assert.Equal(1, reopened.PageCount);
        }
    }

    [Fact]
    public async Task an_open_canceled_before_it_starts_reads_nothing()
    {
        var source = new TestPdfSource(Fixture("classic"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfDocument.OpenAsync(source, leaveOpen: true, new CancellationToken(canceled: true)));

        Assert.Equal(0, source.SyncReads);
        Assert.False(source.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void disposing_the_document_disposes_only_an_owned_source(bool leaveOpen)
    {
        var source = new TestPdfSource(Fixture("classic"));

        using (PdfDocument document = PdfDocument.Open(source, leaveOpen))
        {
            Assert.Equal(1, document.PageCount);
            Assert.False(source.Disposed);
        }

        Assert.Equal(!leaveOpen, source.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void a_failed_open_disposes_only_an_owned_source(bool leaveOpen)
    {
        var source = new TestPdfSource(Encoding.ASCII.GetBytes("%PDF-1.7\nnot a pdf at all\n"));

        Assert.Throws<PdfParseException>(() => PdfDocument.Open(source, leaveOpen));

        Assert.Equal(!leaveOpen, source.Disposed);
    }

    [Fact]
    public async Task a_null_source_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => PdfDocument.Open((IPdfSource)null!, leaveOpen: true));
        await Assert.ThrowsAsync<ArgumentNullException>(() => PdfDocument.OpenAsync((IPdfSource)null!, leaveOpen: true));
    }

    [Fact]
    public void a_stream_that_grows_under_the_document_fails_the_next_read()
    {
        var stream = new MemoryStream();
        byte[] pdf = Fixture("classic");
        stream.Write(pdf, 0, pdf.Length);
        using PdfDocument document = PdfDocument.Open(stream, leaveOpen: true);

        stream.Seek(0, SeekOrigin.End);
        stream.Write(new byte[] { (byte)'\n' }, 0, 1);

        Assert.Throws<PdfSourceChangedException>(() => document.PageCount);
        using var editor = document.Edit();
        Assert.Throws<PdfSourceChangedException>(() => editor.Pages[0].Rotation = 90);
        Assert.Throws<PdfSourceChangedException>(() => editor.SaveIncremental());
        Assert.Equal(pdf.Length + 1, stream.Length);
    }

    [Fact]
    public void a_document_saves_its_own_appends_and_reads_them_back()
    {
        var stream = new MemoryStream();
        byte[] pdf = Fixture("classic");
        stream.Write(pdf, 0, pdf.Length);
        using PdfDocument document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        editor.Pages[0].Rotation = 90;
        editor.SaveIncremental();
        editor.Pages[0].Rotation = 180;
        editor.SaveIncremental();

        Assert.Equal(180, document.Pages[0].Rotation);
        using PdfDocument reopened = PdfDocument.Open(new MemoryStream(stream.ToArray(), writable: false));
        Assert.Equal(180, reopened.Pages[0].Rotation);
        Assert.Equal(3, reopened.Revisions.Count);
    }

    [Fact]
    public void a_document_opened_from_a_source_has_no_stream_to_append_to()
    {
        var source = new TestPdfSource(Fixture("classic"));
        using PdfDocument document = PdfDocument.Open(source, leaveOpen: false);
        using var editor = document.Edit();
        editor.Pages[0].Rotation = 90;

        Assert.Throws<InvalidOperationException>(() => editor.SaveIncremental());

        var destination = new MemoryStream();
        editor.SaveIncremental(destination, leaveOpen: true);
        Assert.Equal(90, document.Pages[0].Rotation);
        Assert.True(source.Disposed);
    }

    private static string Describe(PdfDocument document)
    {
        var text = new StringBuilder();
        text.Append(document.PageCount.ToString(CultureInfo.InvariantCulture)).Append(';');
        foreach (PdfPage page in document.Pages)
        {
            text.Append(page.Rotation.ToString(CultureInfo.InvariantCulture)).Append(',');
        }

        text.Append(';').Append(document.Revisions.Count.ToString(CultureInfo.InvariantCulture));
        text.Append(';').Append(string.Join(",", document.AcroForm.Fields.Select(field => field.FullName + "=" + field.Value)));
        return text.ToString();
    }

    private static byte[] FillAndSave(PdfDocument document)
    {
        using (document)
        {
            using var editor = document.Edit();
            editor.AcroForm.Fill("Name", "Ada");
            var destination = new MemoryStream();
            editor.SaveIncremental(destination, leaveOpen: true);
            return destination.ToArray();
        }
    }

    private static string? Password(string fixture)
    {
        return fixture == "rc4" ? "user" : null;
    }

    private static byte[] Fixture(string name)
    {
        return name switch
        {
            "classic" => Form(new PdfBuilder()),
            "padded" => Padded(),
            "rc4" => Rc4Form(),
            "chain" => IndirectLengthChain(),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
    }

    private static byte[] Form(PdfBuilder builder)
    {
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /V (Old) /Rect [72 700 240 720] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");
        return builder.Finish("/ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>]");
    }

    // The form's objects sit on both sides of 300 KiB of stream data, so opening and reading fill several windows.
    private static byte[] Padded()
    {
        var builder = new PdfBuilder();
        builder.StreamObject(5, "/Type /XObject", new byte[300 * 1024]);
        return Form(builder);
    }

    private static byte[] Rc4Form()
    {
        var crypt = new StandardEncryptor(StandardEncryptor.Kind.Revision3, "user", "owner", -4, encryptMetadata: true, 128);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T " + crypt.Text("Name", 4) + " /Rect [72 700 240 720] /DA " + crypt.Text("/Helv 0 Tf 0 g", 4) + " /P 3 0 R >>");
        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    // The older section is a cross-reference stream whose /Length is object 10, which the newer section places in object
    // stream 11. Opening it loads an object-stream member while the cross-reference chain is still being read.
    private static byte[] IndirectLengthChain()
    {
        var file = new MemoryStream();
        var offsets = new Dictionary<int, long>();
        void Ascii(string text) => file.Write(Encoding.ASCII.GetBytes(text), 0, Encoding.ASCII.GetByteCount(text));
        void Object(int id, string body)
        {
            offsets[id] = file.Length;
            Ascii(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n" + body + "\nendobj\n");
        }

        Ascii("%PDF-1.7\n");
        Object(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [3 0 R] >> >>");
        Object(2, "<< /Type /Pages /Count 1 /Kids [5 0 R] >>");
        Object(3, "<< /FT /Tx /T (Name) /V (Old) /Kids [6 0 R] >>");
        Object(5, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [6 0 R] >>");
        Object(6, "<< /Subtype /Widget /Parent 3 0 R /Rect [72 700 240 720] /DA (/Helv 0 Tf 0 g) /P 5 0 R >>");

        // Seven rows of /W [1 4 2] for objects 0 to 6; object 4 is the older stream itself.
        const int olderRows = 7;
        string member = (olderRows * 7).ToString(CultureInfo.InvariantCulture);
        string header = "10 0 ";
        offsets[11] = file.Length;
        Ascii("11 0 obj\n<< /Type /ObjStm /N 1 /First " + header.Length.ToString(CultureInfo.InvariantCulture)
            + " /Length " + (header.Length + member.Length).ToString(CultureInfo.InvariantCulture) + " >>\nstream\n" + header + member + "\nendstream\nendobj\n");

        long older = file.Length;
        offsets[4] = older;
        var rows = new List<byte[]> { Row(0, 0, 65535) };
        for (int id = 1; id < olderRows; id++)
        {
            rows.Add(offsets.TryGetValue(id, out long offset) ? Row(1, offset, 0) : Row(0, 0, 65535));
        }

        Ascii("4 0 obj\n<< /Type /XRef /Size 7 /W [1 4 2] /Index [0 7] /Root 1 0 R /Length 10 0 R >>\nstream\n");
        foreach (byte[] row in rows)
        {
            file.Write(row, 0, row.Length);
        }

        Ascii("\nendstream\nendobj\n");
        long newer = file.Length;
        byte[] newerRows = Row(2, 11, 0).Concat(Row(1, offsets[11], 0)).Concat(Row(1, newer, 0)).ToArray();
        Ascii("12 0 obj\n<< /Type /XRef /Size 13 /W [1 4 2] /Index [10 3] /Root 1 0 R /Prev " + older.ToString(CultureInfo.InvariantCulture)
            + " /ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>] /Length "
            + newerRows.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
        file.Write(newerRows, 0, newerRows.Length);
        Ascii("\nendstream\nendobj\nstartxref\n" + newer.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        return file.ToArray();
    }

    private static byte[] Row(int type, long field1, int field2)
    {
        return new[] { (byte)type, (byte)(field1 >> 24), (byte)(field1 >> 16), (byte)(field1 >> 8), (byte)field1, (byte)(field2 >> 8), (byte)field2 };
    }
}
