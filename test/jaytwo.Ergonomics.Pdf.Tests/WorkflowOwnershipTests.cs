using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F2: borrowed streams and sources stay open; a file the workflow opens is closed.
public class WorkflowOwnershipTests
{
    private const int Mebibyte = 1024 * 1024;

    [Fact]
    public void borrowed_streams_stay_open_after_success()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo()));
        var destination = new HookedStream();

        PdfWorkflow.From(input).WithTitle("New").Save(destination);

        Assert.False(input.Disposed);
        Assert.False(destination.Disposed);
    }

    [Fact]
    public void borrowed_streams_stay_open_after_an_engine_failure()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.Restricted()));
        var destination = new HookedStream();

        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(input).WithTitle("New").Save(destination));

        Assert.False(input.Disposed);
        Assert.False(destination.Disposed);
    }

    [Fact]
    public async Task borrowed_streams_stay_open_after_cancellation()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.Large(3 * Mebibyte)));
        using var cancel = new CancellationTokenSource();
        var destination = new HookedStream { BeforeWrite = cancel.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.From(input).WithTitle("New").SaveAsync(destination, cancel.Token));

        Assert.False(input.Disposed);
        Assert.False(destination.Disposed);
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task a_borrowed_source_stays_open_after_success_failure_and_cancellation()
    {
        var success = new TestPdfSource(WorkflowPdf.WithInfo());
        await PdfWorkflow.From(success).WithTitle("New").SaveAsync(new MemoryStream());

        var failure = new TestPdfSource(WorkflowPdf.Restricted());
        await Assert.ThrowsAsync<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(failure).WithTitle("New").SaveAsync(new MemoryStream()));

        var canceled = new TestPdfSource(WorkflowPdf.WithInfo());
        using var cancel = new CancellationTokenSource();
        canceled.BeforeAsyncRead = _ => cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.From(canceled).WithTitle("New").SaveAsync(new MemoryStream(), cancel.Token));

        Assert.False(success.Disposed);
        Assert.False(failure.Disposed);
        Assert.False(canceled.Disposed);
        Assert.True(canceled.AsyncReads > 0);
    }

    [Fact]
    public void an_input_file_is_closed_after_success()
    {
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", WorkflowPdf.WithInfo());

        PdfWorkflow.FromFile(path).WithTitle("New").Save(new MemoryStream());
        PdfWorkflow.FromFile(path).WithTitle("New").AppendToSource();

        AssertClosed(path);
    }

    [Fact]
    public void an_input_file_is_closed_after_an_engine_failure()
    {
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", WorkflowPdf.Restricted());

        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfWorkflow.FromFile(path).WithTitle("New").Save(new MemoryStream()));
        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfWorkflow.FromFile(path).WithTitle("New").AppendToSource());

        AssertClosed(path);
    }

    [Fact]
    public async Task an_input_file_is_closed_after_cancellation()
    {
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", WorkflowPdf.Large(3 * Mebibyte));
        using var cancel = new CancellationTokenSource();
        var destination = new HookedStream { BeforeWrite = cancel.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.FromFile(path).WithTitle("New").SaveAsync(destination, cancel.Token));

        AssertClosed(path);
    }

    private static void AssertClosed(string path)
    {
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }
}
