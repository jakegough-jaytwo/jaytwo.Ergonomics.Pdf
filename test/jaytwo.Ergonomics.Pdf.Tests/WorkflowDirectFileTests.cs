using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// WORKFLOW-FILE-01 to 04: the ordinary Save(path) and SaveAsync(path) write the destination directly. They create it, or
// overwrite it by default, refuse an existing one only when asked, apply the edits and checks before creating or truncating
// it, and leave a failed file in place.
public class WorkflowDirectFileTests
{
    private const int Mebibyte = 1024 * 1024;

    private static readonly byte[] Previous = Encoding.ASCII.GetBytes("previous destination content");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task an_absent_destination_is_created_directly_with_no_temporary_sibling(bool useAsync)
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(input)).WithTitle("New");

        PdfWorkflowResult result = useAsync ? await workflow.SaveAsync(destination) : workflow.Save(destination);

        byte[] saved = File.ReadAllBytes(destination);
        WorkflowPdf.AssertTitled(input, saved, "New");
        Assert.Equal(saved.Length, result.OutputLength);
        Assert.Empty(directory.Temporaries());
        Assert.Single(Directory.GetFiles(directory.Path));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task an_existing_destination_is_overwritten_by_default_and_with_an_explicit_choice(bool useAsync, bool explicitChoice)
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string destination = directory.Write("out.pdf", Previous);
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(input)).WithTitle("New");
        PdfFileSaveOptions? options = explicitChoice ? new PdfFileSaveOptions { Overwrite = true } : null;

        if (useAsync)
        {
            await workflow.SaveAsync(destination, options);
        }
        else
        {
            workflow.Save(destination, options);
        }

        WorkflowPdf.AssertTitled(input, File.ReadAllBytes(destination), "New");
        Assert.Empty(directory.Temporaries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task fail_if_exists_refuses_an_existing_destination_and_consumes_the_description(bool useAsync)
    {
        using var directory = new TempDirectory();
        string destination = directory.Write("out.pdf", Previous);
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo()));
        PdfWorkflowBuilder workflow = PdfWorkflow.From(input).WithTitle("New");
        var failIfExists = new PdfFileSaveOptions { Overwrite = false };

        if (useAsync)
        {
            await Assert.ThrowsAsync<IOException>(() => workflow.SaveAsync(destination, failIfExists));
        }
        else
        {
            Assert.Throws<IOException>(() => workflow.Save(destination, failIfExists));
        }

        Assert.False(input.ReadCalled);
        Assert.Equal(Previous, File.ReadAllBytes(destination));
        Assert.Throws<InvalidOperationException>(() => workflow.Save(directory.File("again.pdf")));
        Assert.False(File.Exists(directory.File("again.pdf")));
    }

    [Fact]
    public void fail_if_exists_creates_an_absent_destination()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");

        PdfWorkflow.From(new MemoryStream(input)).WithTitle("New").Save(destination, new PdfFileSaveOptions { Overwrite = false });

        WorkflowPdf.AssertTitled(input, File.ReadAllBytes(destination), "New");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_known_refusal_happens_before_the_destination_is_created_or_truncated(bool existing)
    {
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");
        if (existing)
        {
            File.WriteAllBytes(destination, Previous);
        }

        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(new MemoryStream(WorkflowPdf.Restricted())).WithTitle("New").Save(destination));
        await Assert.ThrowsAsync<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(new MemoryStream(WorkflowPdf.Restricted())).WithTitle("New").SaveAsync(destination));

        Assert.Equal(existing, File.Exists(destination));
        if (existing)
        {
            Assert.Equal(Previous, File.ReadAllBytes(destination));
        }

        Assert.Empty(directory.Temporaries());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task the_input_file_is_refused_before_it_can_be_truncated_even_with_the_default_overwrite(bool useAsync, bool otherSpelling)
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        string destination = otherSpelling ? Path.Combine(directory.Path, "sub", "..", "in.pdf") : path;
        Directory.CreateDirectory(Path.Combine(directory.Path, "sub"));

        if (useAsync)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => PdfWorkflow.FromFile(path).WithTitle("New").SaveAsync(destination));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => PdfWorkflow.FromFile(path).WithTitle("New").Save(destination));
        }

        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.Empty(directory.Temporaries());
    }

    // WORKFLOW-ALIAS-01: matching length and last-write time do not establish that two files are the same.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_distinct_copy_with_the_input_length_and_last_write_time_is_overwritten(bool useAsync)
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        string copy = directory.Write("copy.pdf", input);
        File.SetLastWriteTimeUtc(copy, File.GetLastWriteTimeUtc(path));
        PdfWorkflowBuilder workflow = PdfWorkflow.FromFile(path).WithTitle("New");

        if (useAsync)
        {
            await workflow.SaveAsync(copy);
        }
        else
        {
            workflow.Save(copy);
        }

        WorkflowPdf.AssertTitled(input, File.ReadAllBytes(copy), "New");
        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.Empty(directory.Temporaries());
    }

    // WORKFLOW-RESTORE-01: the bytes written before the failure stay as written. The input fails on its second 1 MiB read,
    // after the first block reached the destination, so the retained file is exactly that block.
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task a_failure_after_bytes_were_written_retains_exactly_those_bytes(bool useAsync, bool existing)
    {
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");
        if (existing)
        {
            File.WriteAllBytes(destination, Previous);
        }

        byte[] data = WorkflowPdf.Large(3 * Mebibyte);
        var source = new TestPdfSource(data);
        bool failed = false;
        Action<int> failOnceTheFirstBlockIsWritten = _ =>
        {
            if (!failed && File.Exists(destination) && new FileInfo(destination).Length >= Mebibyte)
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
        byte[] retained = File.ReadAllBytes(destination);
        Assert.Equal(Mebibyte, retained.Length);
        Assert.Equal(data.AsSpan(0, Mebibyte).ToArray(), retained);
        Assert.Empty(directory.Temporaries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_failure_after_writing_starts_leaves_the_file_in_place_and_consumes_the_description(bool existing)
    {
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");
        if (existing)
        {
            File.WriteAllBytes(destination, Previous);
        }

        // The input spans several 1 MiB copy blocks. The first read after the destination was opened (and truncated) fails.
        var source = new TestPdfSource(WorkflowPdf.Large(3 * Mebibyte));
        bool failed = false;
        source.BeforeAsyncRead = _ =>
        {
            if (!failed && File.Exists(destination) && new FileInfo(destination).Length != Previous.Length)
            {
                failed = true;
                throw new IOException("The input failed after the destination was opened.");
            }
        };
        PdfWorkflowBuilder workflow = PdfWorkflow.From(source).WithTitle("New");

        await Assert.ThrowsAsync<IOException>(() => workflow.SaveAsync(destination));

        Assert.True(failed);
        Assert.True(File.Exists(destination), "The failed file was deleted.");
        if (existing)
        {
            Assert.NotEqual(Previous, File.ReadAllBytes(destination));
        }

        Assert.Empty(directory.Temporaries());
        Assert.Throws<InvalidOperationException>(() => workflow.Save(directory.File("again.pdf")));
        Assert.False(File.Exists(directory.File("again.pdf")));
    }
}
