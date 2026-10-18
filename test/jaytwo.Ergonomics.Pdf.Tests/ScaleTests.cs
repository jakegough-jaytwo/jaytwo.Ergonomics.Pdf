using System;
using System.IO;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class ScaleTests
{
    [Fact]
    public void cross_stream_save_copies_in_one_megabyte_reads()
    {
        var builder = PdfBuilder.Basic();
        builder.StreamObject(4, "/Type /XObject", new byte[1500 * 1024]);
        builder.Indirect(5, "<< /Title (Big) >>");
        byte[] pdf = builder.Finish("/Info 5 0 R");
        var source = new ProbeStream(new MemoryStream(pdf));
        using var document = PdfDocument.Open(source, leaveOpen: true);
        document.Metadata.Title = "Copied";
        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);

        Assert.True(source.MaxRequested <= 1024 * 1024);
        Assert.True(source.BytesRead < (pdf.Length * 2L) + (1024 * 1024));
        Assert.Equal(pdf, sourceBytes(source));
        using var copy = PdfDocument.Open(destination, leaveOpen: true);
        Assert.Equal("Copied", copy.Metadata.Title);
        var occupied = new MemoryStream(new byte[] { 1 });
        Assert.Throws<PdfException>(() => document.SaveIncremental(occupied));
        Assert.Equal(1, occupied.Length);
    }

    [Fact]
    public void three_gigabyte_pdf_is_opened_edited_and_signed_without_reading_the_middle()
    {
        var builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (Huge) >>");
        builder.Ascii(new string('\n', 4096));
        long tailOffset = (3L * 1024 * 1024 * 1024) - 65536;
        (byte[] head, byte[] tail) = builder.Split(tailOffset, "/Info 4 0 R");
        var sparse = new SparsePdfStream(head, tail, tailOffset);
        var provider = new SilentProvider();
        using (var document = PdfDocument.Open(sparse, leaveOpen: true))
        {
            Assert.Equal("Huge", document.Metadata.Title);
            Assert.Equal(1, document.PageCount);
            document.Metadata.Title = "Changed";
            document.SaveIncremental(sparse);
            document.Sign(sparse, new PdfSignatureOptions { ReservedContentsBytes = 64 }, provider);
        }

        Assert.False(sparse.TouchedHole);
        Assert.True(sparse.BytesRead < 2 * 1024 * 1024);
        Assert.True(provider.Range.Length0 > 3L * 1024 * 1024 * 1024);
        Assert.True(provider.Range.Offset1 > 3L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void classic_xref_refuses_offsets_past_ten_digits_without_writing()
    {
        var builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (TooBig) >>");
        builder.Ascii(new string('\n', 4096));
        long length = 10000000000L;
        long tailOffset = length - 65536;
        (byte[] head, byte[] tail) = builder.Split(tailOffset, "/Info 4 0 R");
        var sparse = new SparsePdfStream(head, tail, tailOffset);
        using var document = PdfDocument.Open(sparse, leaveOpen: true);
        Assert.Equal("TooBig", document.Metadata.Title);
        document.Metadata.Title = "Nope";
        var destination = new MemoryStream();

        Assert.Throws<PdfTooLargeForClassicXrefException>(() => document.SaveIncremental(destination));

        Assert.Equal(0, destination.Length);
        Assert.Equal(length, sparse.Length);
        Assert.False(sparse.TouchedHole);
    }

    private static byte[] sourceBytes(ProbeStream probe)
    {
        long position = probe.Position;
        probe.Position = 0;
        using var copy = new MemoryStream();
        probe.CopyTo(copy);
        probe.Position = position;
        return copy.ToArray();
    }

    private sealed class SilentProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public PdfByteRange Range { get; private set; }

        public byte[] CreateContents(PdfSignatureContext context)
        {
            Range = context.ByteRange;
            return new byte[] { 1, 2, 3, 4 };
        }
    }
}
