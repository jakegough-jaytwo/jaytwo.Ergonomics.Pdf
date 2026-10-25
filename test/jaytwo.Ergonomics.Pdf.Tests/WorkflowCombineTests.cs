using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class WorkflowCombineTests
{
    [Fact]
    public void description_reads_nothing_and_snapshots_selections_and_options()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.Pages()));
        var source = new TestPdfSource(WorkflowPdf.Pages());
        int[] pages = { 3, 1, 3 };
        var occurrence = PdfCombineInput.From(input, pages);
        var options = new PdfAssemblyOptions();
        PdfCombineInput[] inputs = { occurrence };
        PdfCombineWorkflow workflow = PdfWorkflow.Combine(inputs, options);
        pages[0] = 999;
        inputs[0] = PdfCombineInput.From(source);
        options.ExternalLinks = (PdfExternalLinkMode)999;
        Assert.False(input.ReadCalled);
        Assert.Equal(0, source.SyncReads);
        Assert.Equal(0, source.AsyncReads);
        PdfCombineInput.FromFile("missing.pdf", Enumerable.Range(1, 3));

        using var output = new MemoryStream();
        workflow.Save(output);
        using PdfDocument document = WorkflowPdf.Open(output.ToArray());
        Assert.Equal(new[] { 270, 0, 270 }, document.Pages.Select(page => page.Rotation));
        Assert.True(input.CanRead);
        Assert.Equal(0, source.SyncReads);
        Assert.Throws<InvalidOperationException>(() => workflow.Save(new MemoryStream()));
    }

    [Fact]
    public void selection_and_shape_errors_do_not_read_inputs()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.Pages()));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfCombineInput.From(input, new[] { 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfCombineInput.From(input, Array.Empty<int>()));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.Combine(Array.Empty<PdfCombineInput>()));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.Combine(new PdfCombineInput[] { null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfWorkflow.Combine(new[] { PdfCombineInput.From(input) }, new PdfAssemblyOptions { ExternalLinks = (PdfExternalLinkMode)123 }));
        Assert.False(input.ReadCalled);
    }

    [Fact]
    public async Task all_terminal_forms_match_direct_assembly_and_borrow_inputs()
    {
        using var directory = new TempDirectory();
        using var input = new MemoryStream(WorkflowPdf.Pages());
        var source = new TestPdfSource(WorkflowPdf.Pages());
        string file = directory.Write("input.pdf", WorkflowPdf.Pages());
        var inputs = new[]
        {
            PdfCombineInput.From(input, new[] { 3, 1, 3 }),
            PdfCombineInput.From(source, Enumerable.Range(1, 2)),
            PdfCombineInput.FromFile(file),
        };
        using var first = new MemoryStream();
        using var second = new MemoryStream();
        PdfAssemblyResult result = PdfWorkflow.Combine(inputs).Save(first);
        await PdfWorkflow.Combine(inputs).SaveAsync(second);
        PdfWorkflow.Combine(inputs).Save(directory.File("sync.pdf"));
        await PdfWorkflow.Combine(inputs).SaveAsync(directory.File("async.pdf"));

        using PdfDocument directInput = WorkflowPdf.Open(WorkflowPdf.Pages());
        using var direct = new MemoryStream();
        PdfAssemblyResult expected = PdfDocumentEditor.Concatenate(
            new[]
            {
                new PdfAssemblyInput(directInput, new[] { 3, 1, 3 }),
                new PdfAssemblyInput(directInput, new[] { 1, 2 }),
                new PdfAssemblyInput(directInput, new[] { 1, 2, 3 }),
            },
            direct);
        Assert.Equal(expected.Omissions, result.Omissions);
        using PdfDocument reference = WorkflowPdf.Open(direct.ToArray());
        foreach (byte[] bytes in new[] { first.ToArray(), second.ToArray(), File.ReadAllBytes(directory.File("sync.pdf")), File.ReadAllBytes(directory.File("async.pdf")) })
        {
            // Assembly deliberately creates a fresh document identifier on each run.
            Assert.Equal(NormalizeIdentifier(direct.ToArray()), NormalizeIdentifier(bytes));
            using PdfDocument document = WorkflowPdf.Open(bytes);
            Assert.Equal(reference.Pages.Select(page => page.Rotation), document.Pages.Select(page => page.Rotation));
            Assert.Equal(reference.Pages.Count, document.Pages.Count);
            Assert.Equal(reference.Revisions.Count, document.Revisions.Count);
        }

        Assert.True(input.CanRead);
        Assert.False(source.Disposed);
        Assert.Empty(directory.Temporaries());
        using var exclusive = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void repeated_occurrences_keep_form_namespaces_as_the_engine_does()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /V (Ada) /Rect [0 0 20 20] /P 3 0 R >>");
        using var input = new MemoryStream(builder.Finish());
        var occurrence = PdfCombineInput.From(input);
        using var output = new MemoryStream();
        PdfWorkflow.Combine(new[] { occurrence, occurrence }).Save(output);
        using PdfDocument combined = WorkflowPdf.Open(output.ToArray());
        using PdfDocument original = WorkflowPdf.Open(input.ToArray());
        using var direct = new MemoryStream();
        PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(original, new[] { 1 }), new PdfAssemblyInput(original, new[] { 1 }) }, direct);
        using PdfDocument expected = WorkflowPdf.Open(direct.ToArray());
        Assert.Equal(expected.AcroForm.Export(), combined.AcroForm.Export());
        Assert.Contains(combined.AcroForm.Fields, field => field.FullName == "Input2.Name");
    }

    [Fact]
    public void later_invalid_page_throws_the_engine_error_before_stream_output()
    {
        using var input = new MemoryStream(WorkflowPdf.Pages());
        using var output = new MemoryStream();
        PdfCombineWorkflow workflow = PdfWorkflow.Combine(new[] { PdfCombineInput.From(input), PdfCombineInput.From(input, new[] { 4 }) });
        Assert.Throws<ArgumentOutOfRangeException>(() => workflow.Save(output));
        Assert.Equal(0, output.Length);
        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
        Assert.Throws<InvalidOperationException>(() => workflow.Save(output));
    }

    [Fact]
    public async Task cancellation_consumes_the_workflow_and_keeps_borrowed_inputs_open()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.Pages()));
        PdfCombineWorkflow workflow = PdfWorkflow.Combine(new[] { PdfCombineInput.From(input) });
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        using var output = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.SaveAsync(output, canceled.Token));
        Assert.False(input.ReadCalled);
        Assert.True(input.CanRead);
        Assert.Equal(0, output.Length);
        Assert.Throws<InvalidOperationException>(() => workflow.Save(output));
    }

    [Fact]
    public void stream_alias_and_nonempty_destination_are_rejected_before_reading()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.Pages()));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.Combine(new[] { PdfCombineInput.From(input) }).Save(input));
        using var nonempty = new MemoryStream(new byte[] { 42 });
        Assert.Throws<ArgumentException>(() => PdfWorkflow.Combine(new[] { PdfCombineInput.From(input) }).Save(nonempty));
        Assert.Equal(new byte[] { 42 }, nonempty.ToArray());
        Assert.False(input.ReadCalled);
    }

    [Fact]
    public async Task file_failure_preserves_existing_destination_and_closes_all_owned_inputs()
    {
        using var directory = new TempDirectory();
        string input = directory.Write("input.pdf", WorkflowPdf.Pages());
        byte[] previous = { 1, 2, 3 };
        string target = directory.Write("output.pdf", previous);
        var inputs = new[] { PdfCombineInput.FromFile(input), PdfCombineInput.FromFile(input, new[] { 4 }) };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PdfWorkflow.Combine(inputs).SaveAsync(target, new PdfFileSaveOptions { Overwrite = true }));
        Assert.Equal(previous, File.ReadAllBytes(target));
        Assert.Empty(directory.Temporaries());
        using var exclusive = new FileStream(input, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void file_safety_applies_to_every_input_and_requires_explicit_overwrite()
    {
        using var directory = new TempDirectory();
        string first = directory.Write("first.pdf", WorkflowPdf.WithInfo());
        string second = directory.Write("second.pdf", WorkflowPdf.Pages());
        var inputs = new[] { PdfCombineInput.FromFile(first), PdfCombineInput.FromFile(second) };
        Assert.Throws<ArgumentException>(() => PdfWorkflow.Combine(inputs).Save(second));
        string alias = directory.Write("alias.pdf", File.ReadAllBytes(second));
        File.SetLastWriteTimeUtc(alias, File.GetLastWriteTimeUtc(second));
        Assert.Throws<IOException>(() => PdfWorkflow.Combine(inputs).Save(alias, new PdfFileSaveOptions { Overwrite = true }));
        Assert.Equal(File.ReadAllBytes(second), File.ReadAllBytes(alias));
        Assert.Empty(directory.Temporaries());

        var unread = new ProbeStream(new MemoryStream(WorkflowPdf.Pages()));
        Assert.Throws<IOException>(() => PdfWorkflow.Combine(new[] { PdfCombineInput.From(unread) }).Save(alias));
        Assert.False(unread.ReadCalled);
        string target = directory.Write("output.pdf", new byte[] { 42 });
        PdfWorkflow.Combine(inputs).Save(target, new PdfFileSaveOptions { Overwrite = true, FlushToDisk = false });
        using PdfDocument result = WorkflowPdf.Open(File.ReadAllBytes(target));
        Assert.Equal(4, result.Pages.Count);
    }

    [Fact]
    public void encrypted_inputs_require_consent_and_check_each_password()
    {
        using var input = new MemoryStream(WorkflowPdf.Restricted());
        var owner = PdfCombineInput.From(input, options: new PdfWorkflowOpenOptions { Password = "owner" });
        using var output = new MemoryStream();
        Assert.Throws<PdfAssemblyNotSupportedException>(() => PdfWorkflow.Combine(new[] { owner }).Save(output));
        Assert.Equal(0, output.Length);
        var options = new PdfAssemblyOptions { AllowUnencryptedOutput = true };
        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfWorkflow.Combine(new[] { owner, PdfCombineInput.From(input) }, options).Save(output));
        Assert.Equal(0, output.Length);
        PdfWorkflow.Combine(new[] { owner }, options).Save(output);
        using PdfDocument document = WorkflowPdf.Open(output.ToArray());
        Assert.Single(document.Pages);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void link_mode_and_omission_counts_match_the_engine()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Link /Rect [0 0 20 20] /A << /S /URI /URI (https://example.com/) >> >>");
        using var input = new MemoryStream(builder.Finish());
        using var preserved = new MemoryStream();
        Assert.Empty(PdfWorkflow.Combine(new[] { PdfCombineInput.From(input) }).Save(preserved).Omissions);
        using var dropped = new MemoryStream();
        var options = new PdfAssemblyOptions { ExternalLinks = PdfExternalLinkMode.Drop };
        PdfAssemblyResult result = PdfWorkflow.Combine(new[] { PdfCombineInput.From(input) }, options).Save(dropped);
        using PdfDocument document = WorkflowPdf.Open(input.ToArray());
        using var direct = new MemoryStream();
        PdfAssemblyResult expected = PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(document, new[] { 1 }) }, direct, options);
        Assert.Equal(expected.Omissions, result.Omissions);
        Assert.Equal(1, result.Omissions[PdfOmissionReason.ExternalLinkDropped]);
    }

    [Fact]
    public async Task partial_output_failure_and_cancellation_roll_back_without_disposing_streams()
    {
        using var input = new MemoryStream(WorkflowPdf.Pages());
        using var output = new HookedStream();
        var failure = new IOException("injected write failure");
        output.BeforeWrite = () =>
        {
            if (output.Length > 0)
            {
                throw failure;
            }
        };
        Assert.Same(failure, Assert.Throws<IOException>(() => PdfWorkflow.Combine(new[] { PdfCombineInput.From(input) }).Save(output)));
        Assert.Equal(0, output.Length);
        Assert.Equal(0, output.Position);
        Assert.False(output.Disposed);
        using var canceled = new CancellationTokenSource();
        output.BeforeWrite = () => canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.Combine(new[] { PdfCombineInput.From(input) }).SaveAsync(output, canceled.Token));
        Assert.Equal(0, output.Length);
        Assert.False(output.Disposed);
        Assert.True(input.CanRead);
    }

    private static string NormalizeIdentifier(byte[] pdf)
    {
        return Regex.Replace(Encoding.ASCII.GetString(pdf), @"/ID\s*\[\s*<[0-9A-Fa-f]+>\s*<[0-9A-Fa-f]+>\s*\]", "/ID [new identifier]");
    }
}
