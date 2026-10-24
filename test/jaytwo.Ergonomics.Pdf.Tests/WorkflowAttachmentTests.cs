using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.Attachments;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F2 (ownership), F4 (one revision), F5 (order), and F8 (attachments) for attachment workflows.
public class WorkflowAttachmentTests
{
    private const int Mebibyte = 1024 * 1024;

    private static readonly DateTimeOffset Date = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void an_attachment_workflow_writes_the_same_bytes_as_the_direct_engine_adds()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string order = directory.Write("order.JSON", Encoding.ASCII.GetBytes("{\"id\":1}"));
        string notes = directory.Write("notes.bin", Encoding.ASCII.GetBytes("notes"));
        var invoice = new MemoryStream(Encoding.ASCII.GetBytes("skip<invoice/>")) { Position = 4 };

        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(input))
            .Attach("invoice.xml", invoice, Dated("text/xml"))
            .AttachFile("order.json", order, Dated(null, "The order"))
            .AttachFile("notes.bin", notes, Dated(null));

        // The run seeks back to the position recorded when the attachment was described.
        invoice.Position = invoice.Length;
        var output = new MemoryStream();
        PdfWorkflowResult result = workflow.Save(output);

        byte[] direct = WorkflowPdf.Direct(input, editor =>
        {
            editor.Attachments.Add("invoice.xml", Content("<invoice/>"), false, Dated("text/xml"));
            editor.Attachments.Add("order.json", Content("{\"id\":1}"), false, Dated("application/json", "The order"));
            editor.Attachments.Add("notes.bin", Content("notes"), false, Dated(null));
        });

        WorkflowPdf.AssertOneRevision(input, output.ToArray());
        Assert.Equal(direct, output.ToArray());
        Assert.True(result.Modified);
        Assert.Equal(
            new[] { "invoice.xml|text/xml||<invoice/>", "notes.bin|||notes", "order.json|application/json|The order|{\"id\":1}" },
            Inventory(output.ToArray()).OrderBy(entry => entry, StringComparer.Ordinal));
        Assert.True(invoice.CanRead);
    }

    [Fact]
    public void add_then_remove_gives_the_result_of_the_same_engine_calls()
    {
        AssertSameAsDirect(
            WorkflowPdf.WithInfo(),
            workflow => workflow.Attach("a.txt", Content("one")).RemoveAttachment("a.txt"),
            editor =>
            {
                editor.Attachments.Add("a.txt", Content("one"));
                editor.Attachments.Remove("a.txt");
            });
    }

    [Fact]
    public void remove_then_add_gives_the_result_of_the_same_engine_calls()
    {
        byte[] input = WorkflowPdf.Direct(WorkflowPdf.WithInfo(), editor => editor.Attachments.Add("a.txt", Content("old"), false, Dated(null)));

        string[] inventory = AssertSameAsDirect(
            input,
            workflow => workflow.RemoveAttachment("a.txt").Attach("a.txt", Content("new")),
            editor =>
            {
                editor.Attachments.Remove("a.txt");
                editor.Attachments.Add("a.txt", Content("new"));
            });

        Assert.Equal(new[] { "a.txt|||new" }, inventory);
    }

    [Fact]
    public void adding_twice_under_one_name_gives_the_result_of_the_same_engine_calls()
    {
        string[] inventory = AssertSameAsDirect(
            WorkflowPdf.WithInfo(),
            workflow => workflow.Attach("a.txt", Content("one")).Attach("a.txt", Content("two")),
            editor =>
            {
                editor.Attachments.Add("a.txt", Content("one"));
                editor.Attachments.Add("a.txt", Content("two"));
            });

        Assert.Equal(new[] { "a.txt|||two" }, inventory);
    }

    [Fact]
    public void removing_a_missing_attachment_throws_before_output()
    {
        var output = new MemoryStream();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).WithTitle("New").RemoveAttachment("missing");

        Assert.Throws<PdfAttachmentNotFoundException>(() => workflow.Save(output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void a_stream_shorter_than_its_recorded_position_is_refused_before_output()
    {
        var content = new MemoryStream();
        content.Write(new byte[10], 0, 10);
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).Attach("a.txt", content);
        content.SetLength(4);

        var output = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => workflow.Save(output));
        Assert.Equal(0, output.Length);
        Assert.True(content.CanRead);
    }

    [Fact]
    public void a_missing_attachment_file_is_refused_before_output()
    {
        using var directory = new TempDirectory();
        var output = new MemoryStream();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).AttachFile("a.txt", directory.File("missing.txt"));

        Assert.Throws<FileNotFoundException>(() => workflow.Save(output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void an_attachment_file_is_closed_after_success_and_failure()
    {
        using var directory = new TempDirectory();
        string path = directory.Write("a.txt", Encoding.ASCII.GetBytes("content"));

        PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).AttachFile("a.txt", path).Save(new MemoryStream());
        AssertClosed(path);

        // A failure after the engine added the file, and one inside the engine's add.
        Assert.Throws<PdfAttachmentNotFoundException>(() => PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).AttachFile("a.txt", path).RemoveAttachment("missing").Save(new MemoryStream()));
        AssertClosed(path);
        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfWorkflow.From(new MemoryStream(WorkflowPdf.Restricted())).AttachFile("a.txt", path).Save(new MemoryStream()));
        AssertClosed(path);
    }

    [Fact]
    public async Task an_attachment_file_is_closed_after_cancellation()
    {
        using var directory = new TempDirectory();
        string path = directory.Write("a.txt", Encoding.ASCII.GetBytes("content"));
        using var cancel = new CancellationTokenSource();
        var destination = new HookedStream { BeforeWrite = cancel.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.From(new MemoryStream(WorkflowPdf.Large(3 * Mebibyte))).AttachFile("a.txt", path).SaveAsync(destination, cancel.Token));

        AssertClosed(path);
    }

    [Theory]
    [InlineData("a.XML", null, "text/xml")]
    [InlineData("a.Json", null, "application/json")]
    [InlineData("a.pdf", null, "application/pdf")]
    [InlineData("a.csv", null, "text/csv")]
    [InlineData("a.TXT", null, "text/plain")]
    [InlineData("a.bin", null, null)]
    [InlineData("a", null, null)]
    [InlineData("a.xml", "application/xml", "application/xml")]
    public void a_file_attachment_takes_its_mime_type_from_the_caller_or_the_table(string file, string? supplied, string? expected)
    {
        using var directory = new TempDirectory();
        string path = directory.Write(file, Encoding.ASCII.GetBytes("content"));
        PdfAttachmentOptions? options = supplied == null ? null : new PdfAttachmentOptions { MimeType = supplied };

        var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).AttachFile("attached", path, options).Save(output);

        using PdfDocument edited = WorkflowPdf.Open(output.ToArray());
        Assert.Equal(expected, edited.Attachments["attached"].MimeType);
    }

    [Fact]
    public void attachment_arguments_are_checked_when_described()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo()));

        Assert.Throws<ArgumentNullException>(() => workflow.Attach(null!, Content("x")));
        Assert.Throws<ArgumentNullException>(() => workflow.Attach("a.txt", null!));
        Assert.Throws<ArgumentException>(() => workflow.Attach("a.txt", new ProbeStream(new MemoryStream(), allowRead: false)));
        var closed = new MemoryStream();
        closed.Dispose();
        Assert.Throws<ArgumentException>(() => workflow.Attach("a.txt", closed));
        Assert.Throws<ArgumentNullException>(() => workflow.AttachFile(null!, "a.txt"));
        Assert.Throws<ArgumentNullException>(() => workflow.AttachFile("a.txt", null!));
        Assert.Throws<ArgumentException>(() => workflow.AttachFile("a.txt", string.Empty));
        Assert.Throws<ArgumentNullException>(() => workflow.RemoveAttachment(null!));

        // The refused descriptions recorded nothing.
        Assert.False(workflow.Save(new MemoryStream()).Modified);
    }

    private static string[] AssertSameAsDirect(byte[] input, Func<PdfWorkflowBuilder, PdfWorkflowBuilder> describe, Action<PdfDocumentEditor> direct)
    {
        var output = new MemoryStream();
        describe(PdfWorkflow.From(new MemoryStream(input))).Save(output);

        WorkflowPdf.AssertOneRevision(input, output.ToArray());
        string[] inventory = Inventory(output.ToArray());
        Assert.Equal(Inventory(WorkflowPdf.Direct(input, direct)), inventory);
        return inventory;
    }

    // Each attachment as name|MIME type|description|content.
    private static string[] Inventory(byte[] pdf)
    {
        using PdfDocument document = WorkflowPdf.Open(pdf);
        return document.Attachments.Select(attachment =>
        {
            using Stream content = attachment.Open();
            using var reader = new StreamReader(content, Encoding.ASCII);
            return attachment.Name + "|" + attachment.MimeType + "|" + attachment.Description + "|" + reader.ReadToEnd();
        }).ToArray();
    }

    private static PdfAttachmentOptions Dated(string? mimeType, string? description = null)
    {
        return new PdfAttachmentOptions { MimeType = mimeType, Description = description, CreationDate = Date, ModificationDate = Date };
    }

    private static MemoryStream Content(string text)
    {
        return new MemoryStream(Encoding.ASCII.GetBytes(text));
    }

    private static void AssertClosed(string path)
    {
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }
}
