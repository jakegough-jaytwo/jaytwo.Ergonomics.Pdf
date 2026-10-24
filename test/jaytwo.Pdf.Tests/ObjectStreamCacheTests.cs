using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace jaytwo.Pdf.Tests;

public class ObjectStreamCacheTests
{
    [Fact]
    public void object_stream_xref_index_must_match_the_header_position()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [10 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [10 0 R] >>");
        byte[] data = ObjectStream(
            out int first,
            (10, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 72 16] /P 3 0 R >>"),
            (11, "<< /Pad true >>"));
        builder.StreamObject(20, "/Type /ObjStm /N 2 /First " + first.ToString(CultureInfo.InvariantCulture), data);
        builder.Compressed(10, 20, 1);
        builder.Compressed(11, 20, 0);
        builder.AppendXrefStream(30, string.Empty);
        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot(), writable: false), leaveOpen: true);

        Assert.Throws<PdfParseException>(() => document.AcroForm.Fields);
    }

    [Fact]
    public void duplicate_object_numbers_in_an_object_stream_are_rejected()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [10 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [10 0 R] >>");
        byte[] data = ObjectStream(
            out int first,
            (10, "<< /Pad true >>"),
            (10, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 72 16] /P 3 0 R >>"));
        builder.StreamObject(20, "/Type /ObjStm /N 2 /First " + first.ToString(CultureInfo.InvariantCulture), data);
        builder.Compressed(10, 20, 0);
        builder.AppendXrefStream(30, string.Empty);
        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot(), writable: false), leaveOpen: true);

        Assert.Throws<PdfParseException>(() => document.AcroForm.Fields);
    }

    [Fact]
    public void field_walk_releases_object_streams_and_rereads_them()
    {
        string[] names = { "A", "B", "C", "D" };
        string[] values = { "Alpha", "Beta", "Gamma", "Delta" };
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [10 0 R 12 0 R 14 0 R 16 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [10 0 R 12 0 R 14 0 R 16 0 R] >>");
        for (int i = 0; i < names.Length; i++)
        {
            int fieldId = 10 + (i * 2);
            int padId = fieldId + 1;
            int streamId = 20 + i;
            byte[] data = ObjectStream(
                out int first,
                (fieldId, "<< /Subtype /Widget /FT /Tx /T (" + names[i] + ") /V (" + values[i] + ") /Rect [0 0 72 16] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>"),
                (padId, "<< /Pad (" + new string('x', 24 * 1024) + ") >>"));
            builder.StreamObject(streamId, "/Type /ObjStm /N 2 /First " + first.ToString(CultureInfo.InvariantCulture), data);
            builder.Compressed(fieldId, streamId, 0);
            builder.Compressed(padId, streamId, 1);
        }

        builder.AppendXrefStream(30, string.Empty);
        byte[] pdf = builder.Snapshot();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            IReadOnlyList<PdfField> fields = document.AcroForm.Fields;
            Assert.Equal(4, fields.Count);
            Assert.True(document.RetainedObjectStreamCount <= 1);
            Assert.Equal("Alpha", Assert.Single(fields, field => field.FullName == "A").Value);
            Assert.Equal("Beta", Assert.Single(fields, field => field.FullName == "B").Value);
            Assert.Equal("Gamma", Assert.Single(fields, field => field.FullName == "C").Value);
            Assert.Equal("Delta", Assert.Single(fields, field => field.FullName == "D").Value);

            IReadOnlyList<PdfField> again = document.AcroForm.Fields;
            Assert.True(document.RetainedObjectStreamCount <= 1);
            Assert.Equal("Alpha", Assert.Single(again, field => field.FullName == "A").Value);
            Assert.Equal("Beta", Assert.Single(again, field => field.FullName == "B").Value);
            Assert.Equal("Gamma", Assert.Single(again, field => field.FullName == "C").Value);
            Assert.Equal("Delta", Assert.Single(again, field => field.FullName == "D").Value);

            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("A", "Edited"));
            Assert.Equal("Edited", Value(document, "A"));
            Assert.Equal("Beta", Value(document, "B"));
            Assert.Equal("Gamma", Value(document, "C"));
            Assert.Equal("Delta", Value(document, "D"));
            Assert.True(document.RetainedObjectStreamCount <= 1);

            editor.SaveIncremental(stream);
            Assert.Equal("Edited", Value(document, "A"));
            Assert.True(document.RetainedObjectStreamCount <= 1);
        }

        using var reopened = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("Edited", Value(reopened, "A"));
        Assert.Equal("Beta", Value(reopened, "B"));
        Assert.Equal("Gamma", Value(reopened, "C"));
        Assert.Equal("Delta", Value(reopened, "D"));
    }

    private static string? Value(PdfDocument document, string name)
    {
        return Assert.Single(document.AcroForm.Fields, field => field.FullName == name).Value;
    }

    private static byte[] ObjectStream(out int first, params (int Id, string Body)[] objects)
    {
        var header = new StringBuilder();
        var body = new StringBuilder();
        foreach ((int id, string item) in objects)
        {
            header.Append(id.ToString(CultureInfo.InvariantCulture));
            header.Append(' ');
            header.Append(body.Length.ToString(CultureInfo.InvariantCulture));
            header.Append(' ');
            body.Append(item);
        }

        first = header.Length;
        return Encoding.ASCII.GetBytes(header.ToString() + body.ToString());
    }
}
