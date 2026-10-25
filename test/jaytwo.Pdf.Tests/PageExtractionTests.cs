using System;
using System.IO;
using System.Linq;
using System.Text;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Fixtures in this file are original MIT-licensed test data, generated here or by the embedded pypdf script.
// They are focused assembly fixtures, not qualification-corpus members.
public sealed class PageExtractionTests
{
    [Fact]
    public void destination_and_lifetime_checks_run_before_output_and_forward_only_output_works()
    {
        using var input = new MemoryStream(PdfBuilder.Basic().Finish());
        using PdfDocument document = PdfDocument.Open(input);
        using PdfDocumentEditor editor = document.Edit();
        using var bytes = new MemoryStream();
        using var output = new ForwardOnlyOutput(bytes);
        Assert.Throws<ArgumentNullException>(() => editor.ExtractPages(null!, output));
        Assert.Throws<ArgumentNullException>(() => editor.ExtractPages(new[] { 1 }, null!));
        Assert.Throws<ArgumentException>(() => editor.ExtractPages(new[] { 1 }, input));
        editor.ExtractPages(new[] { 1 }, output);
        Assert.True(bytes.Length > 0);
        using (var result = new MemoryStream(bytes.ToArray()))
        using (PdfDocument extracted = PdfDocument.Open(result))
        {
            Assert.Equal(1, extracted.PageCount);
        }

        editor.Dispose();
        Assert.Throws<ObjectDisposedException>(() => editor.ExtractPages(new[] { 1 }, output));
    }

    [Fact]
    public void resource_inspection_bounds_cycles_and_depth_before_output()
    {
        var builder = PdfBuilder.Basic(pageExtra: " /Resources 4 0 R");
        for (int i = 4; i < 270; i++)
        {
            builder.Indirect(i, "<< /Next " + (i + 1) + " 0 R >>");
        }

        builder.Indirect(270, "<< /Next 4 0 R >>");
        using var input = new MemoryStream(builder.Finish());
        using PdfDocument document = PdfDocument.Open(input);
        using PdfDocumentEditor editor = document.Edit();
        using var output = new MemoryStream();
        Assert.Throws<PdfResourceLimitException>(() => editor.ExtractPages(new[] { 1 }, output));
        Assert.Equal(0, output.Length);

        builder = PdfBuilder.Basic(pageExtra: " /Resources 4 0 R");
        builder.Indirect(4, "<< /Next 4 0 R >>");
        Assert.NotEmpty(Extract(builder.Finish(), 1));
    }

    [Fact]
    public void repeated_pages_have_distinct_annotations_and_shared_fonts_and_images()
    {
        var source = new PdfBuilder();
        source.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        source.Indirect(2, "<< /Type /Pages /Count 7 /Kids [3 0 R 4 0 R 5 0 R 6 0 R 7 0 R 8 0 R 9 0 R] /MediaBox [0 0 72 144] /CropBox [1 2 70 140] /Rotate 90 /Resources 10 0 R >>");
        for (int i = 3; i <= 9; i++)
        {
            source.Indirect(i, "<< /Type /Page /Parent 2 0 R /Annots [13 0 R] /UserUnit 2 /TrimBox [2 3 60 130] /StructParents 7 >>");
        }

        source.Indirect(10, "<< /Font << /F1 11 0 R >> /XObject << /Im1 12 0 R >> >>");
        source.Indirect(11, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        source.StreamObject(12, "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", new byte[] { 128 });
        source.Indirect(13, "<< /Type /Annot /Subtype /Text /Rect [5 5 20 20] /Contents (hello) /P 5 0 R /StructParent 7 >>");
        byte[] input = source.Finish();
        byte[] output = Extract(input, 3, 3, 7);
        using var stream = new MemoryStream(output);
        using PdfDocument document = PdfDocument.Open(stream);
        Assert.Equal(3, document.PageCount);
        Assert.All(document.Pages, page => Assert.Equal(90, page.Rotation));
        Assert.Equal(3, document.Pages.Select(page => page.ObjectId).Distinct().Count());
        string text = Encoding.ASCII.GetString(output);
        Assert.Equal(1, Count(text, "/BaseFont /Helvetica"));
        Assert.Equal(1, Count(text, "/Subtype /Image"));
        Assert.Equal(3, Count(text, "/Subtype /Text"));
        foreach (PdfPage page in document.Pages)
        {
            Assert.Equal(1, Count(text, "/P " + page.ObjectId + " 0 R"));
        }

        Assert.Equal(3, Count(text, "/UserUnit 2"));
        Assert.Equal(3, Count(text, "/TrimBox"));
        Assert.DoesNotContain("/StructParent", text);
        Assert.Contains("/CropBox [1 2 70 140]", text);
    }

    [Fact]
    public void extraction_omits_document_data_and_every_signature_widget()
    {
        var builder = PdfBuilder.Basic(
            " /Outlines 5 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> /PageLabels 5 0 R /Metadata 5 0 R /Names << /EmbeddedFiles 5 0 R /JavaScript 5 0 R >> /Perms << /DocMDP 5 0 R /UR3 5 0 R >> /DSS 5 0 R /OpenAction 5 0 R /AA 5 0 R /ViewerPreferences 5 0 R /AcroForm << /Fields [4 0 R] >>",
            " /Annots [4 0 R 6 0 R 7 0 R]");
        builder.Indirect(4, "<< /FT /Sig /Kids [6 0 R] /V 5 0 R /Subtype /Widget /Rect [0 0 20 20] /AP << /N 5 0 R >> >>");
        builder.Indirect(5, "<< /PrivateValue (must-disappear) >>");
        builder.Indirect(6, "<< /Subtype /Widget /Parent 4 0 R /Rect [0 0 20 20] /AP << /N 5 0 R >> >>");
        builder.Indirect(7, "<< /Subtype /Widget /FT /Sig /Rect [0 0 20 20] >>");
        byte[] output = Extract(builder.Finish("/ID [<00000000> <00000000>] /Info 5 0 R"), 1);
        string text = Encoding.ASCII.GetString(output);
        foreach (string key in new[] { "/Sig", "/Widget", "/Outlines", "/StructTreeRoot", "/Marked", "/PageLabels", "/Metadata", "/EmbeddedFiles", "/JavaScript", "/Perms", "/DSS", "/OpenAction", "/AA", "/ViewerPreferences", "/AcroForm", "must-disappear", "<00000000>" })
        {
            Assert.DoesNotContain(key, text);
        }

        Assert.Contains("/Producer (jaytwo.Pdf)", text);
        Assert.Contains("/Info ", text);
        Assert.Contains("/ID [", text);
        Assert.NotEqual(output, Extract(builder.Snapshot(), 1));
    }

    [Theory]
    [InlineData(" /AF [4 0 R]", "")]
    [InlineData(" /AF 99 0 R", "")]
    [InlineData(" /OCProperties << /D << >> >>", "")]
    [InlineData(" /NeedsRendering true /AcroForm << /XFA 4 0 R /Fields [4 0 R] >>", "")]
    [InlineData("", " /AF [4 0 R]")]
    [InlineData("", " /Annots [<< /Subtype /Link /AF [4 0 R] >>]")]
    [InlineData("", " /Resources << /Properties << /P1 << /Type /MCAF /AF [4 0 R] >> >> >>")]
    [InlineData("", " /Resources << /XObject << /F1 4 0 R >> >>")]
    public void unsupported_associations_layers_and_dynamic_xfa_fail_before_output(string catalog, string page)
    {
        var builder = PdfBuilder.Basic(catalog, page);
        builder.StreamObject(4, "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /AF [99 0 R]", Array.Empty<byte>());
        using var input = new MemoryStream(builder.Finish());
        using PdfDocument document = PdfDocument.Open(input);
        using PdfDocumentEditor editor = document.Edit();
        using var output = new MemoryStream();
        Assert.Throws<PdfAssemblyNotSupportedException>(() => editor.ExtractPages(new[] { 1 }, output));
        Assert.Equal(0, output.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    public void invalid_page_selection_leaves_destination_unwritten(int page)
    {
        using var input = new MemoryStream(PdfBuilder.Basic().Finish());
        using PdfDocument document = PdfDocument.Open(input);
        using PdfDocumentEditor editor = document.Edit();
        using var output = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.ExtractPages(new[] { 1, page }, output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void forms_are_refused_and_unsupported_annotations_are_omitted()
    {
        byte[] input = PdfBuilder.Basic(pageExtra: " /Annots [<< /Subtype /Widget /FT /Tx /Rect [0 0 10 10] >>]").Finish();
        Assert.Throws<PdfAssemblyNotSupportedException>(() => Extract(input, 1));
        input = PdfBuilder.Basic(pageExtra: " /Annots [<< /Subtype /Link /Rect [0 0 10 10] /A << /S /Launch >> >> << /Subtype /Text /AP << /N 42 >> >>]").Finish();
        Assert.DoesNotContain("/Annots", Encoding.ASCII.GetString(Extract(input, 1)));
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void foreign_inherited_pages_keep_order_and_render_identically_in_both_renderers()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string source = Path.Combine(directory, "source.pdf");
            string extracted = Path.Combine(directory, "extracted.pdf");
            // Original test content, authored here; pypdf (BSD-3-Clause) serializes the input independently.
            const string script = "import sys\nfrom pypdf import PdfWriter\nfrom pypdf.generic import DictionaryObject, NameObject, NumberObject, ArrayObject, DecodedStreamObject\nw=PdfWriter()\nfont=w._add_object(DictionaryObject({NameObject('/Type'):NameObject('/Font'),NameObject('/Subtype'):NameObject('/Type1'),NameObject('/BaseFont'):NameObject('/Helvetica')}))\nfor n in range(1,8):\n p=w.add_blank_page(144,72)\n s=DecodedStreamObject();s.set_data(('BT /F1 18 Tf 10 30 Td (Page %d) Tj ET'%n).encode());p[NameObject('/Contents')]=w._add_object(s)\n del p['/Resources'];del p['/MediaBox']\nroot=w.get_object(w._pages)\nroot[NameObject('/Resources')]=DictionaryObject({NameObject('/Font'):DictionaryObject({NameObject('/F1'):font})})\nroot[NameObject('/MediaBox')]=ArrayObject([NumberObject(n) for n in [0,0,144,72]])\nroot[NameObject('/Rotate')]=NumberObject(90)\nw.write(sys.argv[1])\n";
            ExternalTool.Output(ExternalTool.Start(ExternalTool.Python(), "-c", script, source));
            using (var input = File.OpenRead(source))
            using (PdfDocument document = PdfDocument.Open(input))
            using (PdfDocumentEditor editor = document.Edit())
            using (var output = File.Create(extracted))
            {
                editor.ExtractPages(new[] { 3, 3, 7 }, output);
            }

            string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
            ExternalTool.Output(ExternalTool.Start(qpdf, "--check", extracted));
            string pdfinfo = ExternalTool.Locate("JAYTWO_PDFINFO", ExternalTool.Executable("pdfinfo"));
            Assert.Matches(@"Pages:\s+3", ExternalTool.Output(ExternalTool.Start(pdfinfo, extracted)));
            foreach (RenderTool renderer in RenderTool.All())
            {
                string text = renderer.Text(extracted);
                Assert.Equal(2, Count(text, "Page 3"));
                Assert.Equal(1, Count(text, "Page 7"));
                Assert.True(text.LastIndexOf("Page 3", StringComparison.Ordinal) < text.IndexOf("Page 7", StringComparison.Ordinal));
                renderer.RasterPages(directory, renderer.Name + "-source", source);
                renderer.RasterPages(directory, renderer.Name + "-output", extracted);
                int[] expected = { 3, 3, 7 };
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.Equal(renderer.ReadRaster(directory, renderer.Name + "-source", expected[i], 7), renderer.ReadRaster(directory, renderer.Name + "-output", i + 1, 3));
                }
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static byte[] Extract(byte[] input, params int[] pages)
    {
        using var stream = new MemoryStream(input);
        using PdfDocument document = PdfDocument.Open(stream);
        using PdfDocumentEditor editor = document.Edit();
        using var output = new MemoryStream();
        editor.ExtractPages(pages, output);
        Assert.True(output.CanWrite);
        Assert.Equal(input, stream.ToArray());
        return output.ToArray();
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private sealed class ForwardOnlyOutput : Stream
    {
        private readonly Stream _destination;

        internal ForwardOnlyOutput(Stream destination)
        {
            _destination = destination;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => _destination.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _destination.Write(buffer, offset, count);
    }
}
