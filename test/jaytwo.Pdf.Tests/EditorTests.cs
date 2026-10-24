using System;
using System.IO;
using System.Linq;
using System.Reflection;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Attachments;
using jaytwo.Pdf.Metadata;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Spec R14 (W-131): the document inspects, and its editor owns every change, save, and signature.
public class EditorTests
{
    [Theory]
    [InlineData(typeof(PdfDocument))]
    [InlineData(typeof(PdfMetadata))]
    [InlineData(typeof(PdfAttachmentCollection))]
    [InlineData(typeof(PdfAcroForm))]
    [InlineData(typeof(PdfPage))]
    public void inspection_types_expose_no_setter_and_no_write_method(Type type)
    {
        string[] writes = { "SaveIncremental", "SaveIncrementalAsync", "Sign", "SignAsync", "Fill", "Flatten", "Add", "Remove", "SetXmp" };

        Assert.Empty(type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.SetMethod?.IsPublic == true));
        Assert.Empty(type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => writes.Contains(method.Name)));
    }

    [Fact]
    public void a_document_has_one_open_editor_and_a_disposed_editor_neither_saves_nor_works()
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        PdfDocumentEditor first = document.Edit();
        first.Metadata.Title = "Pending";
        first.AllowBreakingSignatures = true;

        Assert.Throws<InvalidOperationException>(() => document.Edit());
        Assert.Equal("Pending", document.Metadata.Title);

        first.Dispose();
        first.Dispose();

        Assert.Equal(pdf, stream.ToArray());
        Assert.Throws<ObjectDisposedException>(() => first.Metadata.Title = "Late");
        Assert.Throws<ObjectDisposedException>(() => first.SaveIncremental());
        Assert.Throws<ObjectDisposedException>(() => first.Pages);
        using PdfDocumentEditor second = document.Edit();
        Assert.False(second.AllowBreakingSignatures);
        second.SaveIncremental();

        using var reopened = PdfDocument.Open(new MemoryStream(stream.ToArray(), writable: false));
        Assert.Equal("Pending", reopened.Metadata.Title);
    }

    [Fact]
    public void a_disposed_document_starts_no_editor_and_its_editor_stops_working()
    {
        var document = PdfDocument.Open(new MemoryStream(PdfBuilder.Basic().Finish()));
        PdfDocumentEditor editor = document.Edit();

        document.Dispose();

        Assert.Throws<ObjectDisposedException>(() => document.Edit());
        Assert.Throws<ObjectDisposedException>(() => editor.Metadata.Title = "Late");
        editor.Dispose();
    }
}
