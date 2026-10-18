using System;
using System.IO;
using System.IO.Compression;
using System.Text;
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

    private static byte[] Zlib(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }
}
