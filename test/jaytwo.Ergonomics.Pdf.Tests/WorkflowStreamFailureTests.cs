using System;
using System.IO;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// WORKFLOW-STREAM-02 and WORKFLOW-RESTORE-01: an ordinary Save(Stream) or SaveAsync(Stream) that fails after writing
// started leaves the bytes it wrote in the caller's stream, which stays open; a refusal before any write leaves it empty.
public class WorkflowStreamFailureTests
{
    private const int Mebibyte = 1024 * 1024;

    // The input fails on its second 1 MiB read, after the first block reached the destination.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_failure_after_bytes_were_written_leaves_exactly_those_bytes_in_the_open_stream(bool useAsync)
    {
        byte[] data = WorkflowPdf.Large(3 * Mebibyte);
        var memory = new MemoryStream();
        var destination = new ProbeStream(memory);
        var source = new TestPdfSource(data);
        bool failed = false;
        Action<int> failOnceTheFirstBlockIsWritten = _ =>
        {
            if (!failed && memory.Length >= Mebibyte)
            {
                failed = true;
                throw new IOException("The input failed after the first block was written.");
            }
        };
        source.BeforeAsyncRead = failOnceTheFirstBlockIsWritten;
        source.BeforeSyncRead = failOnceTheFirstBlockIsWritten;
        PdfWorkflowBuilder workflow = PdfWorkflow.From(source).WithTitle("New");

        if (useAsync)
        {
            await Assert.ThrowsAsync<IOException>(() => workflow.SaveAsync(destination));
        }
        else
        {
            Assert.Throws<IOException>(() => workflow.Save(destination));
        }

        Assert.True(failed);
        Assert.Equal(Mebibyte, memory.Length);
        Assert.Equal(data.AsSpan(0, Mebibyte).ToArray(), memory.ToArray());
        Assert.False(destination.Disposed);
        Assert.True(destination.CanWrite);
        var again = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => workflow.Save(again));
        Assert.Equal(0, again.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_refusal_before_any_write_leaves_the_open_stream_empty_and_consumes_the_description(bool useAsync)
    {
        var memory = new MemoryStream();
        var destination = new ProbeStream(memory);
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Restricted())).WithTitle("New");

        if (useAsync)
        {
            await Assert.ThrowsAsync<PdfEncryptionRestrictedException>(() => workflow.SaveAsync(destination));
        }
        else
        {
            Assert.Throws<PdfEncryptionRestrictedException>(() => workflow.Save(destination));
        }

        Assert.Equal(0, memory.Length);
        Assert.False(destination.Disposed);
        Assert.Throws<InvalidOperationException>(() => workflow.Save(new MemoryStream()));
    }
}
