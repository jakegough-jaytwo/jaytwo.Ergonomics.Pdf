using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// WORKFLOW-BEGIN-01: a terminal call whose source-independent arguments are rejected leaves the description usable;
// once the arguments are accepted, a refusal, a failure and a pre-cancelled token each consume it exactly once.
public class WorkflowBeginTests
{
    // Each case names an output stream a terminal must reject before it executes.
    public static IEnumerable<object[]> IneligibleStreams()
    {
        yield return new object[] { "null" };
        yield return new object[] { "read-only" };
        yield return new object[] { "non-seekable" };
        yield return new object[] { "non-empty" };
        yield return new object[] { "position-not-zero" };
    }

    // Combine output need not seek (WORKFLOW-STREAM-01), so a non-seekable stream is eligible there.
    public static IEnumerable<object[]> CombineIneligibleStreams()
    {
        yield return new object[] { "null" };
        yield return new object[] { "read-only" };
        yield return new object[] { "non-empty" };
        yield return new object[] { "position-not-zero" };
    }

    public static IEnumerable<object[]> InvalidPaths()
    {
        yield return new object[] { "null" };
        yield return new object[] { "empty" };
        yield return new object[] { "illegal-character" };
    }

    [Theory]
    [MemberData(nameof(IneligibleStreams))]
    public async Task an_ineligible_save_stream_does_not_consume_the_description(string kind)
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");

        WorkflowTerminalArguments.AssertRejected(() => workflow.Save(WorkflowTerminalArguments.Ineligible(kind)!));
        await WorkflowTerminalArguments.AssertRejectedAsync(() => workflow.SaveAsync(WorkflowTerminalArguments.Ineligible(kind)!));

        await AssertCorrectedStreamRun(workflow, "New");
    }

    [Fact]
    public async Task the_input_stream_as_a_save_destination_does_not_consume_the_description()
    {
        var input = new MemoryStream(WorkflowPdf.WithInfo());
        PdfWorkflowBuilder workflow = PdfWorkflow.From(input).WithTitle("New");

        Assert.Throws<ArgumentException>(() => workflow.Save(input));

        await AssertCorrectedStreamRun(workflow, "New");
    }

    [Theory]
    [MemberData(nameof(InvalidPaths))]
    public async Task an_invalid_save_path_does_not_consume_the_description(string kind)
    {
        using var directory = new TempDirectory();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");

        WorkflowTerminalArguments.AssertRejected(() => workflow.Save(WorkflowTerminalArguments.BadPath(kind)!));
        await WorkflowTerminalArguments.AssertRejectedAsync(() => workflow.SaveAsync(WorkflowTerminalArguments.BadPath(kind)!));

        string path = directory.File("out.pdf");
        workflow.Save(path);
        using PdfDocument saved = WorkflowPdf.Open(File.ReadAllBytes(path));
        Assert.Equal("New", saved.Metadata.Title);
    }

    [Fact]
    public async Task the_input_file_as_a_save_destination_does_not_consume_the_description()
    {
        using var directory = new TempDirectory();
        string input = directory.Write("in.pdf", WorkflowPdf.WithInfo());
        PdfWorkflowBuilder workflow = PdfWorkflow.FromFile(input).WithTitle("New");

        Assert.Throws<ArgumentException>(() => workflow.Save(input));
        await Assert.ThrowsAsync<ArgumentException>(() => workflow.SaveAsync(input));

        workflow.Save(directory.File("out.pdf"));
        using PdfDocument saved = WorkflowPdf.Open(File.ReadAllBytes(directory.File("out.pdf")));
        Assert.Equal("New", saved.Metadata.Title);
    }

    [Fact]
    public async Task a_refusal_after_execution_begins_consumes_the_description_once()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Restricted())).WithTitle("New");

        Assert.Throws<PdfEncryptionRestrictedException>(() => workflow.Save(new MemoryStream()));

        await AssertConsumed(workflow);
    }

    [Fact]
    public async Task a_pre_cancelled_token_with_accepted_arguments_consumes_the_description_once()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.SaveAsync(new MemoryStream(), canceled.Token));

        await AssertConsumed(workflow);
    }

    [Fact]
    public async Task a_pre_cancelled_token_with_rejected_arguments_still_leaves_the_description_usable()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAsync<ArgumentException>(() => workflow.SaveAsync(new MemoryStream(new byte[] { 1 }), canceled.Token));

        await AssertCorrectedStreamRun(workflow, "New");
    }

    [Fact]
    public async Task a_missing_destination_directory_consumes_the_description_once()
    {
        using var directory = new TempDirectory();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");

        Assert.Throws<DirectoryNotFoundException>(() => workflow.Save(directory.File(Path.Combine("missing", "out.pdf"))));

        await AssertConsumed(workflow);
    }

    [Fact]
    public async Task an_existing_destination_file_refusal_consumes_the_description_once()
    {
        using var directory = new TempDirectory();
        string existing = directory.Write("out.pdf", new byte[] { 1 });
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");

        Assert.Throws<IOException>(() => workflow.Save(existing));

        await AssertConsumed(workflow);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(existing));
    }

    [Fact]
    public async Task append_on_a_read_only_source_is_a_refusal_that_consumes_the_description_once()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new TestPdfSource(WorkflowPdf.WithInfo())).WithTitle("New");

        Assert.Throws<InvalidOperationException>(() => workflow.AppendToSource());

        await AssertConsumed(workflow);
    }

    [Theory]
    [MemberData(nameof(CombineIneligibleStreams))]
    public void an_ineligible_combine_stream_does_not_consume_the_description(string kind)
    {
        PdfCombineWorkflow workflow = PdfWorkflow.Combine(new[] { PdfCombineInput.From(new MemoryStream(WorkflowPdf.WithInfo())) });

        WorkflowTerminalArguments.AssertRejected(() => workflow.Save(WorkflowTerminalArguments.Ineligible(kind)!));

        var output = new MemoryStream();
        workflow.Save(output);
        Assert.True(output.Length > 0);
        Assert.Throws<InvalidOperationException>(() => workflow.Save(new MemoryStream()));
    }

    [Fact]
    public async Task a_non_seekable_combine_stream_is_accepted_and_the_description_runs_once()
    {
        byte[] pdf = WorkflowPdf.WithInfo();
        PdfCombineWorkflow sync = PdfWorkflow.Combine(new[] { PdfCombineInput.From(new MemoryStream(pdf)) });
        var syncOutput = new WorkflowTerminalArguments.ForwardOnlyStream();

        PdfAssemblyResult syncResult = sync.Save(syncOutput);

        Assert.NotNull(syncResult);
        using (PdfDocument saved = WorkflowPdf.Open(syncOutput.Written))
        {
            Assert.Equal(1, saved.Pages.Count);
        }

        Assert.Throws<InvalidOperationException>(() => sync.Save(new MemoryStream()));

        PdfCombineWorkflow async = PdfWorkflow.Combine(new[] { PdfCombineInput.From(new MemoryStream(pdf)) });
        var asyncOutput = new WorkflowTerminalArguments.ForwardOnlyStream();

        await async.SaveAsync(asyncOutput);

        using (PdfDocument saved = WorkflowPdf.Open(asyncOutput.Written))
        {
            Assert.Equal(1, saved.Pages.Count);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => async.SaveAsync(new MemoryStream()));
    }

    [Fact]
    public void a_non_seekable_stream_does_not_relax_the_builder_save_rule()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New");

        WorkflowTerminalArguments.AssertRejected(() => workflow.Save(new WorkflowTerminalArguments.ForwardOnlyStream()));

        workflow.Save(new MemoryStream());
    }

    [Theory]
    [MemberData(nameof(InvalidPaths))]
    public void an_invalid_combine_path_does_not_consume_the_description(string kind)
    {
        using var directory = new TempDirectory();
        PdfCombineWorkflow workflow = PdfWorkflow.Combine(new[] { PdfCombineInput.From(new MemoryStream(WorkflowPdf.WithInfo())) });

        WorkflowTerminalArguments.AssertRejected(() => workflow.Save(WorkflowTerminalArguments.BadPath(kind)!));

        workflow.Save(directory.File("out.pdf"));
        Assert.True(File.Exists(directory.File("out.pdf")));
        Assert.Throws<InvalidOperationException>(() => workflow.Save(directory.File("again.pdf")));
    }

    [Fact]
    public async Task a_combine_destination_that_is_an_input_does_not_consume_the_description()
    {
        var input = new MemoryStream(WorkflowPdf.WithInfo());
        PdfCombineWorkflow workflow = PdfWorkflow.Combine(new[] { PdfCombineInput.From(input) });

        Assert.Throws<ArgumentException>(() => workflow.Save(input));
        await Assert.ThrowsAsync<ArgumentException>(() => workflow.SaveAsync(input));

        workflow.Save(new MemoryStream());
    }

    [Fact]
    public async Task a_pre_cancelled_combine_with_accepted_arguments_consumes_the_description_once()
    {
        PdfCombineWorkflow workflow = PdfWorkflow.Combine(new[] { PdfCombineInput.From(new MemoryStream(WorkflowPdf.WithInfo())) });
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.SaveAsync(new MemoryStream(), canceled.Token));

        var output = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => workflow.Save(output));
        Assert.Equal(0, output.Length);
    }

    // Ineligible arguments racing valid calls never take the claim; exactly one valid call runs.
    [Fact]
    public async Task invalid_and_valid_concurrent_calls_run_one_valid_call_exactly_once()
    {
        byte[] pdf = WorkflowPdf.WithInfo();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(pdf)).WithTitle("New");
            using var barrier = new Barrier(3);
            Task<Exception?> Run(Func<Stream> destination) => Task.Run<Exception?>(() =>
            {
                barrier.SignalAndWait();
                return Record.Exception(() => workflow.Save(destination()));
            });

            Exception?[] outcomes = await Task.WhenAll(
                Run(() => new MemoryStream()),
                Run(() => new MemoryStream(new byte[] { 1 })),
                Run(() => new MemoryStream()));

            Assert.Single(outcomes, outcome => outcome == null);
            Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
            Assert.Single(outcomes, outcome => outcome is ArgumentException);
        }
    }

    private static async Task AssertCorrectedStreamRun(PdfWorkflowBuilder workflow, string title)
    {
        var output = new MemoryStream();
        await workflow.SaveAsync(output);
        using PdfDocument saved = WorkflowPdf.Open(output.ToArray());
        Assert.Equal(title, saved.Metadata.Title);
        Assert.Throws<InvalidOperationException>(() => workflow.Save(new MemoryStream()));
    }

    // The consumed description fails every later terminal without running it: the output stays empty.
    private static async Task AssertConsumed(PdfWorkflowBuilder workflow)
    {
        var output = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => workflow.Save(output));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.SaveAsync(output));
        Assert.Equal(0, output.Length);
    }
}
