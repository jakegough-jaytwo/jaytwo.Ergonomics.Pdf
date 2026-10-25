using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace jaytwo.Pdf.Tests;

// W-166: generated workloads are original MIT-licensed test data, outside the G2 corpus.
// Run each memory test in its own Release process without coverage for checkpoint measurements.
[Collection("dense-memory")]
public sealed class AssemblyCheckpointTests
{
    private const long MemoryBudget = 128L * 1024 * 1024;
    private readonly ITestOutputHelper _output;

    public AssemblyCheckpointTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void factur_x_association_is_refused_before_either_operation_writes()
    {
        string path = Path.Combine(FocusedFixtures.PathOf("focused-assembly-invoice"), "factur-x.pdf");
        // Independently establish that this is a hybrid invoice, rather than a generic AF fixture.
        const string inspect = "import sys\nfrom pypdf import PdfReader\nr=PdfReader(sys.argv[1]);assert len(r.pages)==4\nassert r.trailer['/Root']['/AF'];assert 'factur-x.xml' in r.attachments\nassert b'CrossIndustryInvoice' in r.attachments['factur-x.xml'][0]\n";
        ExternalTool.Output(ExternalTool.Start(ExternalTool.Python(), "-c", inspect, path));
        using PdfDocument invoice = PdfDocument.Open(File.OpenRead(path));
        using PdfDocumentEditor editor = invoice.Edit();
        using var output = new MemoryStream();
        Assert.Throws<PdfAssemblyNotSupportedException>(() => editor.ExtractPages(new[] { 1 }, output));
        Assert.Equal(0, output.Length);
        using PdfDocument first = PdfDocument.Open(new MemoryStream(PdfBuilder.Basic().Finish()));
        var inputs = new[] { new PdfAssemblyInput(first, new[] { 1 }), new PdfAssemblyInput(invoice, new[] { 1 }) };
        Assert.Throws<PdfAssemblyNotSupportedException>(() => PdfDocumentEditor.Concatenate(inputs, output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void selection_from_one_hundred_thousand_pages_has_a_bounded_working_set()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (int count in new[] { 1000, 100000 })
            {
                string path = Path.Combine(directory, "pages.pdf");
                WritePages(path, count);
                long read = 0;
                WorkloadMeter.Sample sample = WorkloadMeter.Measure(() =>
                {
                    using var source = new ProbeStream(File.OpenRead(path));
                    using PdfDocument document = PdfDocument.Open(source);
                    using PdfDocumentEditor editor = document.Edit();
                    using var output = new MemoryStream();
                    editor.ExtractPages(new[] { count, 1, count }, output);
                    read = source.BytesRead;
                    output.Position = 0;
                    using PdfDocument result = PdfDocument.Open(output, leaveOpen: true);
                    Assert.Equal(3, result.PageCount);
                    Assert.Equal(new[] { 90, 0, 90 }, result.Pages.Select(page => page.Rotation));
                });
                _output.WriteLine(sample.Describe(count + " input pages, three selected; read " + read + " bytes", MemoryBudget));
                Assert.True(sample.PeakHeapGrowth < MemoryBudget, sample.Describe("selected-page working set", MemoryBudget));
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void multi_gibibyte_content_is_copied_with_bounded_buffers()
    {
        foreach (long length in new[] { 1024L * 1024, 3L * 1024 * 1024 * 1024 })
        {
            using var source = new AssemblyContentStream(length);
            WorkloadMeter.Sample sample = WorkloadMeter.Measure(() =>
            {
                using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);
                using PdfDocumentEditor editor = document.Edit();
                editor.ExtractPages(new[] { 1 }, Stream.Null);
            });
            _output.WriteLine(sample.Describe(length + " content bytes; read " + source.BytesRead + "; max request " + source.MaxRequested, MemoryBudget));
            Assert.True(source.BytesRead >= length);
            Assert.InRange(source.MaxRequested, 1, 1024 * 1024);
            Assert.True(sample.PeakHeapGrowth < MemoryBudget, sample.Describe("raw-stream working set", MemoryBudget));
        }
    }

    private static void WritePages(string path, int count)
    {
        using var file = File.Create(path);
        var offsets = new long[count + 3];
        void Write(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            file.Write(bytes, 0, bytes.Length);
        }

        void Object(int id, string body)
        {
            offsets[id] = file.Position;
            Write(id + " 0 obj\n" + body + "\nendobj\n");
        }

        Write("%PDF-1.7\n");
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        var kids = new StringBuilder();
        for (int index = 0; index < count; index++)
        {
            int id = index + 3;
            kids.Append(id).Append(" 0 R ");
            Object(id, "<< /Type /Page /Parent 2 0 R /Rotate " + (index == count - 1 ? 90 : 0) + " >>");
        }

        Object(2, "<< /Type /Pages /Count " + count + " /Kids [" + kids + "] /MediaBox [0 0 72 72] /Resources << >> >>");
        long xref = file.Position;
        Write("xref\n0 " + offsets.Length + "\n0000000000 65535 f \n");
        for (int id = 1; id < offsets.Length; id++)
        {
            Write(offsets[id].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        Write("trailer\n<< /Size " + offsets.Length + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
    }
}
