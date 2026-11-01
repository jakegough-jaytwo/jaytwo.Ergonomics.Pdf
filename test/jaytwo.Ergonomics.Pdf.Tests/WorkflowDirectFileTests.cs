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

    [Fact]
    public void the_input_file_is_refused_before_it_can_be_truncated_even_with_the_default_overwrite()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);

        Assert.Throws<ArgumentException>(() => PdfWorkflow.FromFile(path).WithTitle("New").Save(path));

        Assert.Equal(input, File.ReadAllBytes(path));
    }

    [Fact]
    public void a_same_length_and_time_copy_is_refused_as_a_possible_alias_before_it_is_truncated()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        string copy = directory.Write("copy.pdf", input);
        File.SetLastWriteTimeUtc(copy, File.GetLastWriteTimeUtc(path));

        Assert.Throws<IOException>(() => PdfWorkflow.FromFile(path).WithTitle("New").Save(copy));

        Assert.Equal(input, File.ReadAllBytes(copy));
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
