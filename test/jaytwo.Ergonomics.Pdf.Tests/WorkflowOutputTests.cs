using System;
using System.IO;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F11 (outputs), F12 (failure), and F14 (sync and async) for stream saves and AppendToSource.
public class WorkflowOutputTests
{
    [Theory]
    [InlineData("stream", "Save(stream)")]
    [InlineData("stream", "SaveAsync(stream)")]
    [InlineData("stream", "Save(path)")]
    [InlineData("stream", "SaveAsync(path)")]
    [InlineData("stream", "AppendToSource")]
    [InlineData("stream", "AppendToSourceAsync")]
    [InlineData("file", "Save(stream)")]
    [InlineData("file", "SaveAsync(stream)")]
    [InlineData("file", "Save(path)")]
    [InlineData("file", "SaveAsync(path)")]
    [InlineData("file", "AppendToSource")]
    [InlineData("file", "AppendToSourceAsync")]
    [InlineData("source", "Save(stream)")]
    [InlineData("source", "SaveAsync(stream)")]
    [InlineData("source", "Save(path)")]
    [InlineData("source", "SaveAsync(path)")]
    public async Task every_terminal_form_appends_one_revision(string inputKind, string terminal)
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string inputPath = directory.Write("in.pdf", input);
        var inputStream = new MemoryStream();
        inputStream.Write(input, 0, input.Length);
        PdfWorkflowBuilder workflow = inputKind switch
        {
            "stream" => PdfWorkflow.From(inputStream),
            "file" => PdfWorkflow.FromFile(inputPath),
            _ => PdfWorkflow.From(new TestPdfSource(input)),
        };
        workflow.WithTitle("New");

        var destination = new MemoryStream();
        string destinationPath = directory.File("out.pdf");
        PdfWorkflowResult result = terminal switch
        {
            "Save(stream)" => workflow.Save(destination),
            "SaveAsync(stream)" => await workflow.SaveAsync(destination),
            "Save(path)" => workflow.Save(destinationPath),
            "SaveAsync(path)" => await workflow.SaveAsync(destinationPath),
            "AppendToSource" => workflow.AppendToSource(),
            _ => await workflow.AppendToSourceAsync(),
        };

        byte[] output = terminal.StartsWith("Append", StringComparison.Ordinal)
            ? (inputKind == "file" ? File.ReadAllBytes(inputPath) : inputStream.ToArray())
            : (terminal.EndsWith("(path)", StringComparison.Ordinal) ? File.ReadAllBytes(destinationPath) : destination.ToArray());
        WorkflowPdf.AssertTitled(input, output, "New");
        Assert.True(result.Modified);
        Assert.Equal(output.Length, result.OutputLength);
        Assert.Empty(directory.Temporaries());
    }

    [Fact]
    public async Task a_stream_save_with_no_operations_writes_a_copy()
    {
        byte[] input = WorkflowPdf.WithInfo();

        var output = new MemoryStream();
        PdfWorkflowResult result = PdfWorkflow.From(new MemoryStream(input)).Save(output);
        var asyncOutput = new MemoryStream();
        PdfWorkflowResult asyncResult = await PdfWorkflow.From(new MemoryStream(input)).SaveAsync(asyncOutput);

        Assert.Equal(input, output.ToArray());
        Assert.Equal(input, asyncOutput.ToArray());
        Assert.False(result.Modified);
        Assert.False(asyncResult.Modified);
        Assert.Equal(input.Length, result.OutputLength);
    }

    [Fact]
    public async Task append_to_source_with_no_operations_writes_nothing()
    {
        byte[] input = WorkflowPdf.WithInfo();
        var stream = new HookedStream(input);
        stream.BeforeWrite = () => throw new InvalidOperationException("The input was written.");
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        DateTime written = File.GetLastWriteTimeUtc(path);

        PdfWorkflowResult result = PdfWorkflow.From(stream).AppendToSource();
        PdfWorkflowResult asyncResult = await PdfWorkflow.From(stream).AppendToSourceAsync();
        PdfWorkflowResult fileResult = PdfWorkflow.FromFile(path).AppendToSource();

        Assert.False(result.Modified);
        Assert.False(asyncResult.Modified);
        Assert.False(fileResult.Modified);
        Assert.Equal(input.Length, result.OutputLength);
        Assert.Equal(input, stream.ToArray());
        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
    }

    [Theory]
    [InlineData("non-empty")]
    [InlineData("read-only")]
    [InlineData("non-seekable")]
    [InlineData("input")]
    public async Task an_unusable_destination_stream_is_rejected_before_the_input_opens(string kind)
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo()));
        Stream Destination()
        {
            switch (kind)
            {
                case "non-empty":
                    var used = new MemoryStream();
                    used.WriteByte(1);
                    return used;
                case "read-only":
                    return new MemoryStream(new byte[0], writable: false);
                case "non-seekable":
                    return new ProbeStream(new MemoryStream(), allowRead: false);
                default:
                    return input;
            }
        }

        Assert.Throws<ArgumentException>(() => PdfWorkflow.From(input).WithTitle("New").Save(Destination()));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfWorkflow.From(input).WithTitle("New").SaveAsync(Destination()));
        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.From(input).WithTitle("New").Save((Stream)null!));

        Assert.False(input.ReadCalled);
    }

    [Fact]
    public async Task append_to_source_is_rejected_for_a_source_or_a_read_only_stream()
    {
        var source = new TestPdfSource(WorkflowPdf.WithInfo());
        var readOnly = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo(), writable: false));

        Assert.Throws<InvalidOperationException>(() => PdfWorkflow.From(source).WithTitle("New").AppendToSource());
        await Assert.ThrowsAsync<InvalidOperationException>(() => PdfWorkflow.From(source).WithTitle("New").AppendToSourceAsync());
        Assert.Throws<InvalidOperationException>(() => PdfWorkflow.From(readOnly).WithTitle("New").AppendToSource());
        await Assert.ThrowsAsync<InvalidOperationException>(() => PdfWorkflow.From(readOnly).WithTitle("New").AppendToSourceAsync());

        Assert.Equal(0, source.SyncReads + source.AsyncReads);
        Assert.False(readOnly.ReadCalled);
    }

    [Fact]
    public void an_unusable_input_stream_is_rejected_when_bound()
    {
        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.From((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.From((IPdfSource)null!));
        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.FromFile(null!));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.FromFile(string.Empty));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.From(new ProbeStream(new MemoryStream(), allowRead: false)));
    }

    [Fact]
    public async Task an_engine_failure_propagates_and_leaves_the_destination_stream_empty()
    {
        byte[] input = WorkflowPdf.Restricted();

        var destination = new MemoryStream();
        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(new MemoryStream(input)).WithTitle("New").Save(destination));
        var asyncDestination = new MemoryStream();
        await Assert.ThrowsAsync<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(new MemoryStream(input)).WithTitle("New").SaveAsync(asyncDestination));

        Assert.Equal(0, destination.Length);
        Assert.Equal(0, asyncDestination.Length);
    }

    [Fact]
    public async Task an_engine_failure_leaves_an_appended_source_unchanged()
    {
        byte[] input = WorkflowPdf.Restricted();
        var stream = new MemoryStream();
        stream.Write(input, 0, input.Length);
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);

        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(stream).WithTitle("New").AppendToSource());
        await Assert.ThrowsAsync<PdfEncryptionRestrictedException>(() => PdfWorkflow.FromFile(path).WithTitle("New").AppendToSourceAsync());

        Assert.Equal(input, stream.ToArray());
        Assert.Equal(input, File.ReadAllBytes(path));
    }
}
