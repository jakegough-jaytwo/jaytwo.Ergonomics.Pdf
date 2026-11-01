using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.Metadata;
using Xunit;

namespace jaytwo.Pdf.Tests;

// META-A9 (METADATA-SYNC-01, METADATA-FAIL-01): an ordinary metadata edit reads the XMP packet from the source after it
// has provisionally changed Info. A source that fails at the nth read after the edit starts reaches each of those points.
// After the failure, both stores, the date-override state and earlier pending edits must match their pre-call state.
// The metadata members take no cancellation token and read synchronously, so no observed-cancellation point exists to reach.
[Trait(Lane.Trait, Lane.Sweep)]
public class MetadataFailureTests
{
    private const string OldModDate = "D:20200101120000Z";

    [Fact]
    public void io_failure_during_a_mapped_assignment_restores_both_stores_and_keeps_earlier_edits()
    {
        FaultPoints points = Sweep(
            setup: AddAttachment,
            edit: editor => editor.Metadata.Title = "New",
            afterFailure: (document, editor) => AssertUnchanged(document, editor),
            afterSave: AssertSavedUnchanged);
        Assert.True(points.Count >= 2, "No read after the first provisional change was reached: " + points.Count);
    }

    [Fact]
    public void io_failure_during_a_mapped_clear_restores_both_stores_and_keeps_earlier_edits()
    {
        FaultPoints points = Sweep(
            setup: AddAttachment,
            edit: editor => editor.Metadata.Title = null,
            afterFailure: (document, editor) => AssertUnchanged(document, editor),
            afterSave: AssertSavedUnchanged);
        Assert.True(points.Count >= 2, "Reached " + points.Count);
    }

    [Fact]
    public void io_failure_during_a_custom_key_edit_restores_info_and_the_date()
    {
        FaultPoints points = Sweep(
            setup: AddAttachment,
            edit: editor => editor.Metadata["Custom"] = "value",
            afterFailure: (document, editor) =>
            {
                AssertUnchanged(document, editor);
                Assert.Null(editor.Metadata["Custom"]);
            },
            afterSave: AssertSavedUnchanged);
        Assert.True(points.Count >= 1, "Reached " + points.Count);
    }

    [Fact]
    public void io_failure_during_a_trapped_edit_restores_info_and_the_date()
    {
        FaultPoints points = Sweep(
            setup: AddAttachment,
            edit: editor => editor.Metadata["Trapped"] = "True",
            afterFailure: (document, editor) =>
            {
                AssertUnchanged(document, editor);
                Assert.Null(editor.Metadata["Trapped"]);
            },
            afterSave: AssertSavedUnchanged);
        Assert.True(points.Count >= 1, "Reached " + points.Count);
    }

    // A failed call neither sets nor changes the override: after a failed explicit assignment, the next ordinary edit still
    // refreshes ModDate, and the failed value never appears.
    [Fact]
    public void io_failure_during_an_explicit_moddate_assignment_does_not_set_the_override()
    {
        var explicitDate = new DateTimeOffset(2021, 6, 7, 8, 9, 10, TimeSpan.Zero);
        FaultPoints points = Sweep(
            setup: AddAttachment,
            edit: editor => editor.Metadata.ModificationDate = explicitDate,
            afterFailure: (document, editor) =>
            {
                Assert.Equal(OldModDate, editor.Metadata["ModDate"]);
                editor.Metadata.Keywords = "after";
                Assert.NotEqual(OldModDate, editor.Metadata["ModDate"]);
                Assert.NotEqual(explicitDate, editor.Metadata.ModificationDate);
            },
            afterSave: (again, text) => Assert.DoesNotContain("2021-06-07", text, StringComparison.Ordinal));
        Assert.True(points.Count >= 1, "Reached " + points.Count);
    }

    // The trailer's direct Info dictionary moves into an object on the first edit; a restore must put it back. Only the open
    // document is checked: an incremental save of an unedited direct Info does not carry it forward (not part of this item).
    [Fact]
    public void io_failure_restores_a_direct_info_dictionary()
    {
        FaultPoints points = Sweep(
            setup: AddAttachment,
            edit: editor => editor.Metadata.Title = "New",
            afterFailure: (document, editor) => AssertUnchanged(document, editor),
            afterSave: (again, text) => { },
            inlineInfo: true);
        Assert.True(points.Count >= 2, "Reached " + points.Count);
    }

    // A failed explicit clear does not leave ModDate absent under later edits either.
    [Fact]
    public void io_failure_during_an_explicit_moddate_clear_does_not_set_the_override()
    {
        FaultPoints points = Sweep(
            setup: AddAttachment,
            edit: editor => editor.Metadata.ModificationDate = null,
            afterFailure: (document, editor) =>
            {
                Assert.Equal(OldModDate, editor.Metadata["ModDate"]);
                editor.Metadata.Keywords = "after";
                Assert.NotNull(editor.Metadata["ModDate"]);
                Assert.NotEqual(OldModDate, editor.Metadata["ModDate"]);
            },
            afterSave: (again, text) => Assert.NotNull(again.Metadata["ModDate"]));
        Assert.True(points.Count >= 1, "Reached " + points.Count);
    }

    // The earlier pending edit is an attachment, which does not read or write XMP, so the packet is still read from the
    // source by the metadata edit that follows. An earlier metadata edit would move the packet into memory.
    private static void AddAttachment(PdfDocumentEditor editor)
    {
        editor.Attachments.Add("kept.txt", new MemoryStream(Encoding.ASCII.GetBytes("kept")));
    }

    private static void AssertUnchanged(PdfDocument document, PdfDocumentEditor editor)
    {
        Assert.Equal("Old", editor.Metadata.Title);
        Assert.Null(editor.Metadata.Subject);
        Assert.Equal(OldModDate, editor.Metadata["ModDate"]);
        Assert.Single(document.Attachments);
    }

    private static void AssertSavedUnchanged(PdfDocument again, string xmpText)
    {
        Assert.Equal("Old", again.Metadata.Title);
        Assert.Null(again.Metadata.Subject);
        Assert.Equal(OldModDate, again.Metadata["ModDate"]);
        Assert.Single(again.Attachments);
        Assert.Contains(">Old<", xmpText, StringComparison.Ordinal);
        Assert.Contains("2020-01-01T12:00:00Z", xmpText, StringComparison.Ordinal);
        Assert.DoesNotContain(">New<", xmpText, StringComparison.Ordinal);
        Assert.DoesNotContain("Keywords", xmpText, StringComparison.Ordinal);
    }

    // Runs the edit with the source failing at its nth read after the edit starts, for n = 1, 2, ... until the edit
    // finishes without reaching the fault. Every n that fails with an IOException is a point reached and is checked.
    private static FaultPoints Sweep(
        Action<PdfDocumentEditor> setup,
        Action<PdfDocumentEditor> edit,
        Action<PdfDocument, PdfDocumentEditor> afterFailure,
        Action<PdfDocument, string> afterSave,
        bool inlineInfo = false)
    {
        var points = new FaultPoints();
        for (int n = 1; n < 40; n++)
        {
            var source = new FaultSource(Pdf(inlineInfo));
            using var document = PdfDocument.Open(source, leaveOpen: true);
            using var editor = document.Edit();
            setup(editor);
            source.FailAtRead = n;
            try
            {
                edit(editor);
            }
            catch (IOException)
            {
                points.Count++;
                source.FailAtRead = 0;
                afterFailure(document, editor);
                using var saved = new MemoryStream();
                editor.SaveIncremental(saved, leaveOpen: true);
                saved.Position = 0;
                using var again = PdfDocument.Open(saved, leaveOpen: true);
                using var xmp = again.Metadata.OpenXmp();
                afterSave(again, new StreamReader(xmp).ReadToEnd());
                continue;
            }

            return points;
        }

        throw new InvalidOperationException("The edit never finished.");
    }

    private static byte[] Pdf(bool inlineInfo = false)
    {
        const string Packet = "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>"
            + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">"
            + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Old</rdf:li></rdf:Alt></dc:title>"
            + "<xmp:ModifyDate>2020-01-01T12:00:00Z</xmp:ModifyDate>"
            + "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
        PdfBuilder builder = PdfBuilder.Basic(" /Metadata 5 0 R");
        string info = "<< /Title (Old) /ModDate (" + OldModDate + ") >>";
        builder.StreamObject(5, "/Type /Metadata /Subtype /XML", Encoding.UTF8.GetBytes(Packet));
        if (inlineInfo)
        {
            return builder.Finish("/Info " + info);
        }

        builder.Indirect(4, info);
        return builder.Finish("/Info 4 0 R");
    }

    private sealed class FaultPoints
    {
        public int Count { get; set; }
    }

    // A source that throws an IOException at the nth read once armed, then reads normally again.
    private sealed class FaultSource : IPdfSource
    {
        private readonly byte[] _data;
        private int _reads;

        public FaultSource(byte[] data)
        {
            _data = data;
        }

        public long Length => _data.Length;

        public int FailAtRead
        {
            get => _reads;
            set => _reads = value;
        }

        public int Read(long position, Span<byte> buffer)
        {
            if (_reads > 0 && --_reads == 0)
            {
                throw new IOException("Injected read failure.");
            }

            if (position >= _data.Length)
            {
                return 0;
            }

            int count = (int)Math.Min(buffer.Length, _data.Length - position);
            _data.AsSpan((int)position, count).CopyTo(buffer);
            return count;
        }

        public ValueTask<int> ReadAsync(long position, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return new ValueTask<int>(Read(position, buffer.Span));
        }

        public void Dispose()
        {
        }
    }
}
