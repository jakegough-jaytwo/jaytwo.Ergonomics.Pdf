using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Signatures;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// ISO 32000-1 7.3.10: a reference to an undefined object, to a free entry, or with a generation other than the
// cross-reference entry's is a reference to the null object. Each builder below leaves object 5 out, so the
// classic table lists it as free, and never defines object 99.
public class NullReferenceTests
{
    [Fact]
    public void undefined_free_and_stale_generation_fields_are_null()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [99 0 R 5 0 R 4 1 R 6 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [6 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Stale) /Rect [0 40 100 60] /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Tx /T (Live) /Rect [0 0 100 20] /P 3 0 R >>");
        using var document = Open(builder.Finish());

        PdfField field = Assert.Single(document.AcroForm.Fields);
        Assert.Equal("Live", field.FullName);
    }

    [Fact]
    public void stale_generation_page_reference_does_not_place_a_widget()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [6 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 2 /Kids [3 0 R 4 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [6 0 R] >>");
        builder.Indirect(4, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 100 20] /P 4 1 R >>");
        using var document = Open(builder.Finish());

        PdfField field = Assert.Single(document.AcroForm.Fields);
        Assert.Equal(1, field.PageNumber);
        Assert.Equal(new PdfRect(0, 0, 612, 792), field.MediaBox);
    }

    [Fact]
    public void compressed_object_named_at_generation_one_is_null()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.StreamObject(6, "/Type /ObjStm /N 1 /First 4", Encoding.ASCII.GetBytes("4 0 << /Title (FromStream) >>"));
        builder.Compressed(4, 6, 0);
        builder.AppendXrefStream(7, "/Info 4 1 R");
        using var document = Open(builder.Snapshot());

        Assert.Null(document.Metadata.Title);
    }

    [Theory]
    [InlineData("99 0 R")]
    [InlineData("5 0 R")]
    [InlineData("4 1 R")]
    public void dangling_info_reads_as_absent_and_an_edit_writes_a_new_dictionary(string info)
    {
        byte[] pdf = BasicWith(4, "<< /Title (Other) >>", "/Info " + info);
        using var stream = Writable(pdf);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Null(document.Metadata.Title);
            document.Metadata.Title = "Fresh";
            document.SaveIncremental(stream);
        }

        Assert.DoesNotContain("\n4 0 obj", Appendix(stream, pdf), StringComparison.Ordinal);
        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("Fresh", again.Metadata.Title);
    }

    [Theory]
    [InlineData("99 0 R")]
    [InlineData("5 0 R")]
    [InlineData("4 1 R")]
    public void dangling_acroform_is_replaced_when_signing(string acroForm)
    {
        byte[] pdf = BasicWith(4, "<< /Fields [] /Marker true >>", string.Empty, " /AcroForm " + acroForm);
        using var source = new MemoryStream(pdf, writable: false);
        using var destination = new MemoryStream();
        using (var document = PdfDocument.Open(source, leaveOpen: true))
        {
            Assert.Empty(document.AcroForm.Fields);
            document.Sign(destination, new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider(), leaveOpen: true);
        }

        string appendix = Appendix(destination, pdf);
        Assert.DoesNotContain("\n4 0 obj", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("/AcroForm " + acroForm, appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(destination, leaveOpen: true);
        Assert.Equal(PdfFieldType.Signature, Assert.Single(again.AcroForm.Fields).FieldType);
    }

    [Fact]
    public void dangling_embedded_file_is_listed_and_does_not_open()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(a.txt) 4 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Type /Filespec /F (a.txt) /UF (a.txt) /EF << /F 99 0 R >> >>");
        using var document = Open(builder.Finish());

        Assert.Single(document.Attachments);
        Assert.Throws<PdfParseException>(() => document.Attachments["a.txt"].Open());
    }

    [Fact]
    public void stale_generation_stream_length_is_not_a_length()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(a.txt) 4 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Type /Filespec /F (a.txt) /UF (a.txt) /EF << /F 6 0 R >> >>");
        builder.Indirect(6, "<< /Type /EmbeddedFile /Length 7 1 R >>\nstream\nhello\nendstream");
        builder.Indirect(7, "5");
        using var document = Open(builder.Finish());

        Assert.Throws<PdfParseException>(() => document.Attachments["a.txt"].Open());
    }

    [Theory]
    [InlineData(" /Perms 99 0 R", "<< /Type /Sig /Reference [10 0 R] >>", "<< /TransformMethod /DocMDP /TransformParams << /P 2 /V /1.2 >> >>")]
    [InlineData(" /Perms << /DocMDP 9 1 R >>", "<< /Type /Sig /Reference [10 0 R] >>", "<< /TransformMethod /DocMDP /TransformParams << /P 2 /V /1.2 >> >>")]
    [InlineData(" /Perms << /DocMDP 9 0 R >>", "<< /Type /Sig /Reference 99 0 R >>", "<< /TransformMethod /DocMDP /TransformParams << /P 2 /V /1.2 >> >>")]
    [InlineData(" /Perms << /DocMDP 9 0 R >>", "<< /Type /Sig /Reference [99 0 R 10 0 R] >>", "<< /TransformMethod /DocMDP /TransformParams << /P 2 /V /1.2 >> >>")]
    [InlineData(" /Perms << /DocMDP 9 0 R >>", "<< /Type /Sig /Reference [10 0 R] >>", "<< /TransformMethod /DocMDP /TransformParams 99 0 R >>")]
    [InlineData(" /Perms << /DocMDP 9 0 R >>", "<< /Type /Sig /Reference [10 0 R] >>", "<< /TransformMethod /DocMDP /TransformParams null >>")]
    public void certification_that_resolves_to_null_still_blocks_fills_and_signing(string perms, string signature, string transform)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R" + perms + " /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 100 20] /P 3 0 R >>");
        builder.Indirect(9, signature);
        builder.Indirect(10, transform);
        using var document = Open(builder.Finish());

        Assert.Equal("Name", Assert.Single(document.AcroForm.Fields).FullName);
        Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Fill("Name", "Ada"));
        Assert.Throws<PdfCertificationLockedException>(() => document.Sign(new MemoryStream(), new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider()));
    }

    [Theory]
    [InlineData("<< /Type /Sig /Reference 99 0 R >>", "<< /TransformMethod /FieldMDP /TransformParams << /Action /Include /Fields [(Other)] /V /1.2 >> >>")]
    [InlineData("<< /Type /Sig /Reference [99 0 R 10 0 R] >>", "<< /TransformMethod /FieldMDP /TransformParams << /Action /Include /Fields [(Other)] /V /1.2 >> >>")]
    [InlineData("<< /Type /Sig /Reference [10 0 R] >>", "<< /TransformMethod /FieldMDP /TransformParams 99 0 R >>")]
    [InlineData("<< /Type /Sig /Reference [10 0 R] >>", "<< /TransformMethod /FieldMDP /TransformParams << /Action /Include /Fields 99 0 R /V /1.2 >> >>")]
    [InlineData("<< /Type /Sig /Reference [10 0 R] >>", "<< /TransformMethod /FieldMDP /TransformParams << /Action /Include /Fields [99 0 R (Other)] /V /1.2 >> >>")]
    [InlineData("<< /Type /Sig /Reference [10 0 R] >>", "<< /TransformMethod /FieldMDP /TransformParams << /Action /Include /Fields null /V /1.2 >> >>")]
    [InlineData("<< /Type /Sig /Reference [10 0 R] >>", "<< /TransformMethod /FieldMDP /TransformParams << /Action /Include /Fields [null (Other)] /V /1.2 >> >>")]
    public void field_lock_that_resolves_to_null_locks_the_field(string signature, string transform)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 6 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 6 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 100 20] /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Sig /T (Sig) /V 9 0 R /Rect [0 30 10 40] /P 3 0 R >>");
        builder.Indirect(9, signature);
        builder.Indirect(10, transform);
        using var document = Open(builder.Finish());

        Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Fill("Name", "Ada"));
        Assert.Null(document.AcroForm.GetFields("Name")[0].Value);
    }

    [Fact]
    public void stale_generation_encryption_dictionary_is_rejected()
    {
        var crypt = new StandardEncryptor(true, "user", "owner", -4, encryptMetadata: true);
        byte[] pdf = BasicWith(20, crypt.Dictionary(string.Empty), "/Encrypt 20 1 R /ID [" + crypt.IdLiteral() + "]");

        Assert.Throws<PdfParseException>(() => PdfDocument.Open(new MemoryStream(pdf), "user", leaveOpen: true));
    }

    private static byte[] BasicWith(int id, string body, string trailer, string catalogExtra = "")
    {
        PdfBuilder builder = PdfBuilder.Basic(catalogExtra);
        builder.Indirect(id, body);
        builder.Indirect(6, "<< /Filler true >>");
        return builder.Finish(trailer);
    }

    private static PdfDocument Open(byte[] pdf)
    {
        return PdfDocument.Open(new MemoryStream(pdf, writable: false), leaveOpen: true);
    }

    private static MemoryStream Writable(byte[] pdf)
    {
        var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        return stream;
    }

    private static string Appendix(MemoryStream stream, byte[] pdf)
    {
        byte[] saved = stream.ToArray();
        Assert.True(saved.Length > pdf.Length);
        for (int i = 0; i < pdf.Length; i++)
        {
            Assert.Equal(pdf[i], saved[i]);
        }

        return Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(new byte[] { 1, 2, 3, 4 });
        }
    }
}
