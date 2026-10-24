using System.IO;
using jaytwo.Pdf;
using jaytwo.Pdf.Metadata;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F4 (one session, one revision), F7 (metadata), and F15 (memory).
public class WorkflowMetadataTests
{
    private const int Mebibyte = 1024 * 1024;

    [Fact]
    public void a_metadata_workflow_inspects_the_same_as_the_direct_engine_edit()
    {
        byte[] input = WorkflowPdf.WithInfo();

        var output = new MemoryStream();
        PdfWorkflowResult result = PdfWorkflow.From(new MemoryStream(input))
            .WithMetadata(new PdfMetadataChanges { Title = "Executed", Author = null, Keywords = "three" })
            .Save(output);

        var direct = new MemoryStream();
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(input), leaveOpen: false))
        using (PdfDocumentEditor editor = document.Edit())
        {
            editor.Metadata.Title = "Executed";
            editor.Metadata.Author = null;
            editor.Metadata.Keywords = "three";
            editor.SaveIncremental(direct, leaveOpen: true);
        }

        // Info edits refresh ModDate from the clock, so the comparison is semantic, not byte for byte.
        byte[] workflowBytes = output.ToArray();
        WorkflowPdf.AssertPrefix(input, workflowBytes);
        Assert.True(result.Modified);
        Assert.Equal(workflowBytes.Length, result.OutputLength);
        Assert.Empty(result.FieldAppearances);

        using PdfDocument original = WorkflowPdf.Open(input);
        using PdfDocument fromWorkflow = WorkflowPdf.Open(workflowBytes);
        using PdfDocument fromEngine = WorkflowPdf.Open(direct.ToArray());
        Assert.Equal(original.Revisions.Count + 1, fromWorkflow.Revisions.Count);
        Assert.Equal(fromEngine.Revisions.Count, fromWorkflow.Revisions.Count);
        AssertSameInfo(fromEngine.Metadata, fromWorkflow.Metadata);
        Assert.Equal("Executed", fromWorkflow.Metadata.Title);
        Assert.Null(fromWorkflow.Metadata.Author);
        Assert.Equal("Orders", fromWorkflow.Metadata.Subject);
        Assert.Equal("three", fromWorkflow.Metadata.Keywords);
        Assert.Equal("Writer", fromWorkflow.Metadata.Creator);
        Assert.Equal("Press", fromWorkflow.Metadata.Producer);
    }

    [Fact]
    public void every_metadata_property_can_be_set_and_cleared()
    {
        byte[] input = WorkflowPdf.WithInfo();

        byte[] set = Run(input, new PdfMetadataChanges { Title = "T", Author = "A", Subject = "S", Keywords = "K", Creator = "C", Producer = "P" });
        byte[] cleared = Run(input, new PdfMetadataChanges { Title = null, Author = null, Subject = null, Keywords = null, Creator = null, Producer = null });

        using PdfDocument withValues = WorkflowPdf.Open(set);
        Assert.Equal(
            new[] { "T", "A", "S", "K", "C", "P" },
            new[] { withValues.Metadata.Title, withValues.Metadata.Author, withValues.Metadata.Subject, withValues.Metadata.Keywords, withValues.Metadata.Creator, withValues.Metadata.Producer });
        using PdfDocument withoutValues = WorkflowPdf.Open(cleared);
        Assert.Equal(
            new string?[] { null, null, null, null, null, null },
            new[] { withoutValues.Metadata.Title, withoutValues.Metadata.Author, withoutValues.Metadata.Subject, withoutValues.Metadata.Keywords, withoutValues.Metadata.Creator, withoutValues.Metadata.Producer });
    }

    [Fact]
    public void an_empty_metadata_change_writes_a_copy()
    {
        byte[] input = WorkflowPdf.WithInfo();

        var output = new MemoryStream();
        PdfWorkflowResult result = PdfWorkflow.From(new MemoryStream(input)).WithMetadata(new PdfMetadataChanges()).Save(output);

        Assert.False(result.Modified);
        Assert.Equal(input, output.ToArray());
    }

    [Fact]
    public void a_null_title_removes_the_title()
    {
        byte[] input = WorkflowPdf.WithInfo();

        byte[] output = Save(PdfWorkflow.From(new MemoryStream(input)).WithTitle(null));

        using PdfDocument edited = WorkflowPdf.Open(output);
        Assert.Null(edited.Metadata.Title);
        Assert.Equal("Ada", edited.Metadata.Author);
    }

    [Fact]
    public void operations_apply_in_order_in_one_revision()
    {
        byte[] input = WorkflowPdf.WithInfo();

        byte[] output = Save(PdfWorkflow.From(new MemoryStream(input))
            .WithTitle("First")
            .WithMetadata(new PdfMetadataChanges { Title = "Second", Subject = "Invoices" })
            .WithMetadata(new PdfMetadataChanges { Author = "Grace" }));

        WorkflowPdf.AssertTitled(input, output, "Second");
        using PdfDocument edited = WorkflowPdf.Open(output);
        Assert.Equal("Invoices", edited.Metadata.Subject);
        Assert.Equal("Grace", edited.Metadata.Author);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void a_metadata_workflow_reads_no_more_than_the_direct_engine_edit()
    {
        byte[] input = WorkflowPdf.Large(6 * Mebibyte);

        var workflowInput = new ProbeStream(new MemoryStream(input));
        PdfWorkflow.From(workflowInput).WithTitle("Large edit").Save(new MemoryStream());

        var directInput = new ProbeStream(new MemoryStream(input));
        using (PdfDocument document = PdfDocument.Open(directInput, leaveOpen: true))
        using (PdfDocumentEditor editor = document.Edit())
        {
            editor.Metadata.Title = "Large edit";
            editor.SaveIncremental(new MemoryStream(), leaveOpen: false);
        }

        Assert.True(
            workflowInput.BytesRead <= directInput.BytesRead,
            "The workflow read " + workflowInput.BytesRead + " bytes; the direct edit read " + directInput.BytesRead + ".");
        Assert.True(workflowInput.MaxRequested <= directInput.MaxRequested);
    }

    private static byte[] Run(byte[] input, PdfMetadataChanges changes)
    {
        return Save(PdfWorkflow.From(new MemoryStream(input)).WithMetadata(changes));
    }

    private static byte[] Save(PdfWorkflowBuilder workflow)
    {
        var output = new MemoryStream();
        workflow.Save(output);
        return output.ToArray();
    }

    private static void AssertSameInfo(PdfMetadata expected, PdfMetadata actual)
    {
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Author, actual.Author);
        Assert.Equal(expected.Subject, actual.Subject);
        Assert.Equal(expected.Keywords, actual.Keywords);
        Assert.Equal(expected.Creator, actual.Creator);
        Assert.Equal(expected.Producer, actual.Producer);
    }
}
