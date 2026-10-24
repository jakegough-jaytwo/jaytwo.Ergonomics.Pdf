using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F11, F12, F14, and F17 for saves to a path: the temporary sibling, publication, overwrite, and alias safety.
public class WorkflowFileSaveTests
{
    private const int Mebibyte = 1024 * 1024;

    private static readonly byte[] Previous = Encoding.ASCII.GetBytes("previous destination content");

    [Fact]
    public void a_path_save_publishes_a_new_file()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");

        PdfWorkflowResult result = PdfWorkflow.From(new MemoryStream(input)).WithTitle("New").Save(destination);

        byte[] output = File.ReadAllBytes(destination);
        WorkflowPdf.AssertTitled(input, output, "New");
        Assert.Equal(output.Length, result.OutputLength);
        Assert.Empty(directory.Temporaries());
    }

    [Fact]
    public void a_path_save_with_no_operations_writes_a_copy()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");

        PdfWorkflowResult result = PdfWorkflow.From(new MemoryStream(input)).Save(destination);

        Assert.False(result.Modified);
        Assert.Equal(input, File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task an_existing_file_is_refused_without_overwrite_before_the_input_opens()
    {
        using var directory = new TempDirectory();
        string destination = directory.Write("out.pdf", Previous);
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo()));

        Assert.Throws<IOException>(() => PdfWorkflow.From(input).WithTitle("New").Save(destination));
        await Assert.ThrowsAsync<IOException>(() => PdfWorkflow.From(input).WithTitle("New").SaveAsync(destination, new PdfFileSaveOptions { Overwrite = false }));

        Assert.False(input.ReadCalled);
        Assert.Equal(Previous, File.ReadAllBytes(destination));
        Assert.Empty(directory.Temporaries());
    }

    [Fact]
    public void an_existing_file_is_replaced_with_overwrite()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string destination = directory.Write("out.pdf", Previous);

        PdfWorkflow.From(new MemoryStream(input)).WithTitle("New").Save(destination, new PdfFileSaveOptions { Overwrite = true });

        WorkflowPdf.AssertTitled(input, File.ReadAllBytes(destination), "New");
        Assert.Empty(directory.Temporaries());
    }

    [Fact]
    public async Task a_missing_destination_directory_is_refused_before_the_input_opens()
    {
        using var directory = new TempDirectory();
        string destination = Path.Combine(directory.Path, "missing", "out.pdf");
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo()));

        Assert.Throws<DirectoryNotFoundException>(() => PdfWorkflow.From(input).WithTitle("New").Save(destination));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => PdfWorkflow.From(input).WithTitle("New").SaveAsync(destination));
        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.From(input).WithTitle("New").Save((string)null!));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.From(input).WithTitle("New").Save(string.Empty));

        Assert.False(input.ReadCalled);
    }

    [Fact]
    public void flushing_defaults_on_and_a_save_without_it_still_publishes_a_complete_file()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");

        PdfWorkflow.From(new MemoryStream(input)).WithTitle("New").Save(destination, new PdfFileSaveOptions { FlushToDisk = false });

        Assert.True(new PdfFileSaveOptions().FlushToDisk);
        Assert.False(new PdfFileSaveOptions().Overwrite);
        WorkflowPdf.AssertTitled(input, File.ReadAllBytes(destination), "New");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task an_engine_failure_leaves_the_destination_as_it_was(bool existing)
    {
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");
        if (existing)
        {
            File.WriteAllBytes(destination, Previous);
        }

        var overwrite = new PdfFileSaveOptions { Overwrite = true };
        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(new MemoryStream(WorkflowPdf.Restricted())).WithTitle("New").Save(destination, overwrite));
        await Assert.ThrowsAsync<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(new MemoryStream(WorkflowPdf.Restricted())).WithTitle("New").SaveAsync(destination, overwrite));

        if (existing)
        {
            Assert.Equal(Previous, File.ReadAllBytes(destination));
        }
        else
        {
            Assert.False(File.Exists(destination));
        }

        Assert.Empty(directory.Temporaries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task cancellation_during_an_async_path_save_leaves_no_file(bool existing)
    {
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");
        if (existing)
        {
            File.WriteAllBytes(destination, Previous);
        }

        // The engine checks cancellation between 1 MiB copy blocks, so the input spans several.
        var source = new TestPdfSource(WorkflowPdf.Large(3 * Mebibyte));
        using var cancel = new CancellationTokenSource();
        string? temporary = null;
        source.BeforeAsyncRead = _ =>
        {
            string[] temporaries = directory.Temporaries();
            if (temporaries.Length > 0)
            {
                temporary ??= temporaries[0];
                cancel.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PdfWorkflow.From(source).WithTitle("New").SaveAsync(destination, new PdfFileSaveOptions { Overwrite = true }, cancel.Token));

        Assert.NotNull(temporary);
        Assert.Matches(new Regex("^\\.out\\.pdf\\.[0-9a-f]{8}\\.tmp$"), temporary);
        Assert.Empty(directory.Temporaries());
        if (existing)
        {
            Assert.Equal(Previous, File.ReadAllBytes(destination));
        }
        else
        {
            Assert.False(File.Exists(destination));
        }
    }

    [Theory]
    [InlineData("in.pdf")]
    [InlineData("./in.pdf")]
    [InlineData("sub/../in.pdf")]
    public async Task the_input_path_is_refused_before_the_input_opens(string relative)
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        Directory.CreateDirectory(directory.File("sub"));
        string destination = Path.Combine(directory.Path, relative);

        // The input is held exclusively, so opening it would throw IOException instead.
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<ArgumentException>(() => PdfWorkflow.FromFile(path).WithTitle("New").Save(destination, new PdfFileSaveOptions { Overwrite = true }));
            await Assert.ThrowsAsync<ArgumentException>(() => PdfWorkflow.FromFile(path).WithTitle("New").SaveAsync(destination));
        }

        Assert.Equal(input, File.ReadAllBytes(path));
    }

    [Fact]
    public void the_path_of_an_input_file_stream_is_refused()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);

        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<ArgumentException>(() => PdfWorkflow.From(stream).WithTitle("New").Save(path, new PdfFileSaveOptions { Overwrite = true }));
            Assert.Equal(0, stream.Position);
        }

        Assert.Equal(input, File.ReadAllBytes(path));
    }

    [PlatformFact("WINDOWS", "OSX")]
    public void a_destination_that_differs_from_the_input_only_by_case_is_refused()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);

        Assert.Throws<ArgumentException>(() => PdfWorkflow.FromFile(path).WithTitle("New").Save(directory.File("IN.PDF"), new PdfFileSaveOptions { Overwrite = true }));

        Assert.Equal(input, File.ReadAllBytes(path));
    }

    [Fact]
    public void a_copy_with_the_input_length_and_last_write_time_is_refused_as_a_possible_alias()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        string destination = directory.Write("copy.pdf", input);
        File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(path));

        IOException refused = Assert.Throws<IOException>(() => PdfWorkflow.FromFile(path).WithTitle("New").Save(destination, new PdfFileSaveOptions { Overwrite = true }));

        Assert.Contains("may be the input", refused.Message, StringComparison.Ordinal);
        Assert.Equal(input, File.ReadAllBytes(destination));
        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.Empty(directory.Temporaries());
    }

    [Fact]
    public void a_same_length_copy_with_a_different_last_write_time_is_replaced()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        string destination = directory.Write("copy.pdf", input);
        File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(path).AddHours(-1));

        PdfWorkflow.FromFile(path).WithTitle("New").Save(destination, new PdfFileSaveOptions { Overwrite = true });

        WorkflowPdf.AssertTitled(input, File.ReadAllBytes(destination), "New");
        Assert.Equal(input, File.ReadAllBytes(path));
    }

    // Windows may refuse by the alias check or, if the timestamp read through the link is stale, by the sharing
    // violation on the open input. Unix refuses by the alias check: both names share one inode.
    [Fact]
    public void replacing_a_hard_link_to_the_open_input_fails_and_leaves_the_input_unchanged()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        string link = directory.File("link.pdf");
        FileLinks.CreateHardLink(link, path);

        Assert.ThrowsAny<IOException>(() => PdfWorkflow.FromFile(path).WithTitle("New").Save(link, new PdfFileSaveOptions { Overwrite = true }));

        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.Equal(input, File.ReadAllBytes(link));
        Assert.Empty(directory.Temporaries());
    }

#if NET6_0_OR_GREATER
    [PlatformFact("LINUX")]
    public void a_symbolic_link_destination_is_replaced_as_a_link_and_its_target_is_unchanged()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        string target = directory.Write("target.pdf", Previous);
        string link = directory.File("link.pdf");
        File.CreateSymbolicLink(link, target);

        PdfWorkflow.FromFile(path).WithTitle("New").Save(link, new PdfFileSaveOptions { Overwrite = true });

        Assert.Equal(Previous, File.ReadAllBytes(target));
        Assert.Null(new FileInfo(link).LinkTarget);
        WorkflowPdf.AssertTitled(input, File.ReadAllBytes(link), "New");
    }

    [PlatformFact("LINUX")]
    public void a_symbolic_link_to_the_input_is_refused_before_the_input_opens()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", input);
        string link = directory.File("link.pdf");
        File.CreateSymbolicLink(link, path);

        Assert.Throws<ArgumentException>(() => PdfWorkflow.FromFile(path).WithTitle("New").Save(link, new PdfFileSaveOptions { Overwrite = true }));

        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.NotNull(new FileInfo(link).LinkTarget);
    }
#endif
}
