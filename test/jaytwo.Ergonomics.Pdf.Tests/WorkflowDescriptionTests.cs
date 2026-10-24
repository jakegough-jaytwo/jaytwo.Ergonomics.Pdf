using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F1 (deferred description) and F3 (one run).
public class WorkflowDescriptionTests
{
    [Fact]
    public void describing_a_stream_workflow_reads_nothing()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo()));

        PdfWorkflow.From(input).WithTitle("New").WithMetadata(new PdfMetadataChanges { Author = "Grace" })
            .Fill("Name", "Ada").FillForm(new Dictionary<string, PdfFieldValue> { ["City"] = "Paris" }).FlattenForm("Name").FlattenForm().AllowBreakingExistingSignatures();

        Assert.False(input.ReadCalled);
    }

    [Fact]
    public void describing_a_source_workflow_reads_nothing()
    {
        var source = new TestPdfSource(WorkflowPdf.WithInfo());

        PdfWorkflow.From(source).WithTitle("New").WithMetadata(new PdfMetadataChanges { Author = "Grace" })
            .Fill("Name", "Ada").FillForm(new Dictionary<string, PdfFieldValue> { ["City"] = "Paris" }).FlattenForm("Name").FlattenForm().AllowBreakingExistingSignatures();

        Assert.Equal(0, source.SyncReads);
        Assert.Equal(0, source.AsyncReads);
    }

    [Fact]
    public void describing_a_file_workflow_opens_nothing()
    {
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", WorkflowPdf.WithInfo());

        // The file is held exclusively, so opening it would throw.
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            PdfWorkflow.FromFile(path).WithTitle("New").WithMetadata(new PdfMetadataChanges { Author = "Grace" })
            .Fill("Name", "Ada").FillForm(new Dictionary<string, PdfFieldValue> { ["City"] = "Paris" }).FlattenForm("Name").FlattenForm().AllowBreakingExistingSignatures();
        }

        PdfWorkflow.FromFile(directory.File("missing.pdf")).WithTitle("New");
    }

    [Fact]
    public void a_null_metadata_change_is_refused_when_described()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo()));

        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.From(input).WithMetadata(null!));
        Assert.False(input.ReadCalled);
    }

    [Fact]
    public async Task a_workflow_runs_once_after_success()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");

        workflow.Save(new MemoryStream());

        await AssertConsumed(workflow);
    }

    [Fact]
    public async Task a_workflow_runs_once_after_an_engine_failure()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Restricted())).WithTitle("New");

        Assert.Throws<PdfEncryptionRestrictedException>(() => workflow.Save(new MemoryStream()));

        await AssertConsumed(workflow);
    }

    [Fact]
    public async Task a_workflow_runs_once_after_cancellation()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.SaveAsync(new MemoryStream(), canceled.Token));

        await AssertConsumed(workflow);
    }

    [Fact]
    public async Task a_rejected_terminal_call_also_consumes_the_workflow()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");

        Assert.Throws<ArgumentNullException>(() => workflow.Save((Stream)null!));

        await AssertConsumed(workflow);
    }

    [Fact]
    public async Task an_operation_described_while_the_run_is_underway_is_refused()
    {
        var source = new TestPdfSource(WorkflowPdf.WithInfo());
        PdfWorkflowBuilder workflow = PdfWorkflow.From(source).WithTitle("New");
        Exception? late = null;
        source.BeforeAsyncRead = reads =>
        {
            if (reads == 0)
            {
                late = Record.Exception(() => workflow.WithTitle("Late"));
            }
        };

        var output = new MemoryStream();
        await workflow.SaveAsync(output);

        Assert.IsType<InvalidOperationException>(late);
        using PdfDocument edited = WorkflowPdf.Open(output.ToArray());
        Assert.Equal("New", edited.Metadata.Title);
    }

    [Fact]
    public async Task of_two_concurrent_terminal_calls_exactly_one_runs()
    {
        byte[] pdf = WorkflowPdf.WithInfo();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(pdf)).WithTitle("New");
            using var barrier = new Barrier(2);
            Task<Exception?> Run() => Task.Run<Exception?>(() =>
            {
                barrier.SignalAndWait();
                return Record.Exception(() => workflow.Save(new MemoryStream()));
            });

            Exception?[] outcomes = await Task.WhenAll(Run(), Run());

            Assert.Single(outcomes, outcome => outcome == null);
            Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
        }
    }

    private static async Task AssertConsumed(PdfWorkflowBuilder workflow)
    {
        Assert.Throws<InvalidOperationException>(() => workflow.Save(new MemoryStream()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.SaveAsync(new MemoryStream()));
        Assert.Throws<InvalidOperationException>(() => workflow.Save(Path.Combine(Path.GetTempPath(), "never.pdf")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.SaveAsync(Path.Combine(Path.GetTempPath(), "never.pdf")));
        Assert.Throws<InvalidOperationException>(() => workflow.AppendToSource());
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.AppendToSourceAsync());
        Assert.Throws<InvalidOperationException>(() => workflow.WithTitle("Again"));
        Assert.Throws<InvalidOperationException>(() => workflow.WithMetadata(new PdfMetadataChanges { Title = "Again" }));
        Assert.Throws<InvalidOperationException>(() => workflow.Fill("Name", "Again"));
        Assert.Throws<InvalidOperationException>(() => workflow.FillForm(new Dictionary<string, PdfFieldValue> { ["Name"] = "Again" }));
        Assert.Throws<InvalidOperationException>(() => workflow.FlattenForm());
        Assert.Throws<InvalidOperationException>(() => workflow.FlattenForm("Name"));
        Assert.Throws<InvalidOperationException>(() => workflow.AllowBreakingExistingSignatures());
    }
}
