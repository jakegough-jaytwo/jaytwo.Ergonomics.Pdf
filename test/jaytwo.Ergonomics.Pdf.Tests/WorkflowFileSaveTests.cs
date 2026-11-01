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

// Spec F11, F12, F14, and F17 for saves to a path. The ordinary Save(path) writes directly (WORKFLOW-FILE-01 to 04, see
// WorkflowDirectFileTests); the Combine, signing and timestamp cases here keep the temporary sibling and publication.
public class WorkflowFileSaveTests
{
    private const int Mebibyte = 1024 * 1024;

    private static readonly byte[] Previous = Encoding.ASCII.GetBytes("previous destination content");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void intermediate_directory_links_are_checked_before_opening_the_input(bool combine)
    {
        using var directory = new TempDirectory();
        byte[] input = WorkflowPdf.WithInfo();
        string path = directory.Write("in.pdf", input);
        string link = directory.File("linked");
        FileLinks.CreateDirectoryLink(link, directory.Path);
        try
        {
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            Action save = () =>
            {
                if (combine)
                {
                    PdfWorkflow.Combine(new[] { PdfCombineInput.FromFile(path) }).Save(Path.Combine(link, "in.pdf"), new PdfFileSaveOptions { Overwrite = true });
                }
                else
                {
                    PdfWorkflow.FromFile(path).Save(Path.Combine(link, "in.pdf"), new PdfFileSaveOptions { Overwrite = true });
                }
            };
#if NET6_0_OR_GREATER
            Assert.Throws<ArgumentException>(save);
#else
            Assert.Contains("reparse point", Assert.Throws<IOException>(save).Message, StringComparison.Ordinal);
#endif
        }
        finally
        {
            Directory.Delete(link);
        }

        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.Empty(directory.Temporaries());
    }

    [Fact]
    public async Task a_destination_created_during_processing_is_preserved_without_overwrite()
    {
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");
        var source = new TestPdfSource(WorkflowPdf.WithInfo());
        source.BeforeAsyncRead = _ =>
        {
            File.WriteAllBytes(destination, Previous);
            source.BeforeAsyncRead = null;
        };

        await Assert.ThrowsAsync<IOException>(() => PdfWorkflow.From(source).SaveAsync(destination, new PdfFileSaveOptions { Overwrite = false }));

        Assert.Equal(Previous, File.ReadAllBytes(destination));
        Assert.Empty(directory.Temporaries());
    }

    [Fact]
    public async Task a_destination_replaced_by_an_input_hard_link_during_processing_is_refused()
    {
        using var directory = new TempDirectory();
        byte[] input = WorkflowPdf.Pages();
        string path = directory.Write("in.pdf", input);
        string destination = directory.Write("out.pdf", Previous);
        var source = new TestPdfSource(input);
        source.BeforeAsyncRead = _ =>
        {
            File.Delete(destination);
            FileLinks.CreateHardLink(destination, path);
            source.BeforeAsyncRead = null;
        };

        await Assert.ThrowsAnyAsync<IOException>(() => PdfWorkflow.Combine(new[] { PdfCombineInput.FromFile(path), PdfCombineInput.From(source) })
            .SaveAsync(destination, new PdfFileSaveOptions { Overwrite = true }));

        Assert.Equal(input, File.ReadAllBytes(path));
        Assert.Equal(input, File.ReadAllBytes(destination));
        Assert.Empty(directory.Temporaries());
    }

    [PlatformFact("LINUX")]
    public async Task missing_input_path_metadata_prevents_overwrite()
    {
        using var directory = new TempDirectory();
        byte[] input = WorkflowPdf.Pages();
        string path = directory.Write("in.pdf", input);
        string moved = directory.File("moved.pdf");
        string destination = directory.Write("out.pdf", Previous);
        var source = new TestPdfSource(input);
        source.BeforeAsyncRead = _ =>
        {
            File.Move(path, moved);
            source.BeforeAsyncRead = null;
        };

        await Assert.ThrowsAnyAsync<IOException>(() => PdfWorkflow.Combine(new[] { PdfCombineInput.FromFile(path), PdfCombineInput.From(source) })
            .SaveAsync(destination, new PdfFileSaveOptions { Overwrite = true }));

        Assert.Equal(input, File.ReadAllBytes(moved));
        Assert.Equal(Previous, File.ReadAllBytes(destination));
        Assert.Empty(directory.Temporaries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task concurrent_publications_leave_one_complete_output(bool overwrite)
    {
        using var directory = new TempDirectory();
        using var ready = new Barrier(2);
        byte[] input = WorkflowPdf.WithInfo();
        string destination = directory.File("out.pdf");
        Task<Exception?> Run(string title) => Task.Run(async () =>
        {
            var source = new TestPdfSource(input);
            source.BeforeAsyncRead = _ =>
            {
                source.BeforeAsyncRead = null;
                Assert.True(ready.SignalAndWait(TimeSpan.FromSeconds(30)));
            };
            return (Exception?)await Record.ExceptionAsync(() => PdfWorkflow.From(source).WithTitle(title).SaveAsync(destination, new PdfFileSaveOptions { Overwrite = overwrite }));
        });

        Exception?[] results = await Task.WhenAll(Run("First"), Run("Second"));

        Assert.Contains(results, result => result == null);
        foreach (Exception? result in results)
        {
            Assert.True(result == null || result is IOException, result?.ToString());
        }

        // D-65: saves do not coordinate publication. On Unix both no-overwrite moves may succeed;
        // either outcome must leave a complete PDF, and the test must not require the race to reproduce.

        byte[] output = File.ReadAllBytes(destination);
        using PdfDocument document = WorkflowPdf.Open(output);
        Assert.Contains(document.Metadata.Title, new[] { "First", "Second" });
        WorkflowPdf.AssertTitled(input, output, document.Metadata.Title!);
        Assert.Empty(directory.Temporaries());
    }

    [Fact]
    public async Task failed_publication_removes_the_temporary_and_preserves_the_destination_directory()
    {
        using var directory = new TempDirectory();
        string destination = directory.File("out.pdf");
        var source = new TestPdfSource(WorkflowPdf.WithInfo());
        source.BeforeAsyncRead = _ =>
        {
            Directory.CreateDirectory(destination);
            source.BeforeAsyncRead = null;
        };

        Exception? failure = await Record.ExceptionAsync(() => PdfWorkflow.From(source).SaveAsync(destination, new PdfFileSaveOptions { Overwrite = true }));

        Assert.True(failure is IOException || failure is UnauthorizedAccessException, failure?.ToString());
        Assert.True(Directory.Exists(destination));
        Assert.Empty(Directory.GetFileSystemEntries(destination));
        Assert.Empty(directory.Temporaries());
    }

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

        Assert.Throws<IOException>(() => PdfWorkflow.From(input).WithTitle("New").Save(destination, new PdfFileSaveOptions { Overwrite = false }));
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
        Assert.True(new PdfFileSaveOptions().Overwrite);
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
    public async Task cancellation_during_an_async_path_save_leaves_the_failed_file_in_place(bool existing)
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
        bool opened = false;
        source.BeforeAsyncRead = _ =>
        {
            if (File.Exists(destination) && new FileInfo(destination).Length != Previous.Length)
            {
                opened = true;
                cancel.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PdfWorkflow.From(source).WithTitle("New").SaveAsync(destination, new PdfFileSaveOptions { Overwrite = true }, cancel.Token));

        Assert.True(opened);
        Assert.Empty(directory.Temporaries());
        Assert.True(File.Exists(destination));
        if (existing)
        {
            Assert.NotEqual(Previous, File.ReadAllBytes(destination));
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

    // Windows refuses by the sharing violation on the open input, before the direct write truncates it. No identity check
    // covers a hard link (WORKFLOW-ALIAS-01), so other platforms are not promised the same refusal.
    [PlatformFact("WINDOWS")]
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
