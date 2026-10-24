using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F16: form export from a path, with the file closed afterwards.
public class WorkflowExportTests
{
    private const int Mebibyte = 1024 * 1024;

    [Fact]
    public async Task export_returns_the_engine_export_of_the_file()
    {
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", Filled(WorkflowPdf.Form()));

        IReadOnlyDictionary<string, PdfFieldValue> expected;
        using (PdfDocument document = PdfDocument.Open(File.OpenRead(path)))
        {
            expected = document.AcroForm.Export();
        }

        Assert.Equal("Ada", expected["Name"].Text);
        Assert.Equal(Sorted(expected), Sorted(PdfWorkflow.ExportFormValues(path)));
        Assert.Equal(Sorted(expected), Sorted(await PdfWorkflow.ExportFormValuesAsync(path)));
    }

    [Fact]
    public void a_file_without_a_form_exports_no_values()
    {
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", WorkflowPdf.WithInfo());

        Assert.Empty(PdfWorkflow.ExportFormValues(path));
    }

    [Fact]
    public async Task the_password_option_opens_an_encrypted_file()
    {
        using var directory = new TempDirectory();
        string path = directory.Write("in.pdf", WorkflowPdf.EncryptedForm());
        var options = new PdfWorkflowOpenOptions { Password = "user" };

        Assert.Equal("Ada", PdfWorkflow.ExportFormValues(path, options)["Name"].Text);
        Assert.Equal("Ada", (await PdfWorkflow.ExportFormValuesAsync(path, options))["Name"].Text);
        Assert.Throws<PdfEncryptedException>(() => PdfWorkflow.ExportFormValues(path));
    }

    [Fact]
    public async Task the_file_is_closed_after_success_failure_and_cancellation()
    {
        using var directory = new TempDirectory();
        string form = directory.Write("form.pdf", Filled(WorkflowPdf.Form()));
        string encrypted = directory.Write("encrypted.pdf", WorkflowPdf.EncryptedForm());
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        PdfWorkflow.ExportFormValues(form);
        await PdfWorkflow.ExportFormValuesAsync(form);
        Assert.Throws<PdfEncryptedException>(() => PdfWorkflow.ExportFormValues(encrypted));
        await Assert.ThrowsAsync<PdfEncryptedException>(() => PdfWorkflow.ExportFormValuesAsync(encrypted));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.ExportFormValuesAsync(form, cancellationToken: cancel.Token));

        AssertClosed(form);
        AssertClosed(encrypted);
    }

    // A byte-range lock makes another handle's read of the range fail (Windows only), so an export that succeeds under a
    // lock on the middle of an unreferenced stream read none of it, as the engine reads none of it.
    [PlatformFact("WINDOWS")]
    public async Task export_reads_no_bytes_that_the_engine_does_not()
    {
        using var directory = new TempDirectory();
        byte[] input = Filled(WorkflowPdf.Form(padding: 3 * Mebibyte));
        string path = directory.Write("in.pdf", input);
        long padding = IndexOf(input, "21 0 obj");

        using var locker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Lock(locker, padding + Mebibyte, Mebibyte);
        Assert.Throws<IOException>(() => File.ReadAllBytes(path));

        IReadOnlyDictionary<string, PdfFieldValue> expected;
        using (PdfDocument document = PdfDocument.Open(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
        {
            expected = document.AcroForm.Export();
        }

        Assert.Equal(Sorted(expected), Sorted(PdfWorkflow.ExportFormValues(path)));
        Assert.Equal(Sorted(expected), Sorted(await PdfWorkflow.ExportFormValuesAsync(path)));
    }

    [Fact]
    public async Task a_missing_or_empty_path_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.ExportFormValues(null!));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.ExportFormValues(string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => PdfWorkflow.ExportFormValuesAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => PdfWorkflow.ExportFormValuesAsync(string.Empty));
    }

    private static byte[] Filled(byte[] form)
    {
        return WorkflowPdf.Direct(form, editor => editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue>
        {
            ["Name"] = "Ada",
            ["City"] = "Paris",
            ["Agree"] = true,
        }));
    }

    private static KeyValuePair<string, PdfFieldValue>[] Sorted(IReadOnlyDictionary<string, PdfFieldValue> values)
    {
        return values.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
    }

    private static long IndexOf(byte[] data, string marker)
    {
        return Encoding.ASCII.GetString(data).IndexOf(marker, StringComparison.Ordinal);
    }

    // Disposing the stream releases the lock.
    private static void Lock(FileStream file, long position, long length)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("A byte-range lock blocks other handles' reads only on Windows.");
        }

        file.Lock(position, length);
    }

    private static void AssertClosed(string path)
    {
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }
}
