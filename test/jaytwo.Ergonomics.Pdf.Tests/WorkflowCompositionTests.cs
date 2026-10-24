using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Attachments;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F4 (one session, one revision) and F15 (memory) across every R2 operation together.
public class WorkflowCompositionTests
{
    private const int Mebibyte = 1024 * 1024;

    private static readonly DateTimeOffset Date = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public void every_operation_together_appends_one_revision_and_inspects_like_the_direct_sequence()
    {
        byte[] input = WorkflowPdf.Form();

        var output = new MemoryStream();
        PdfWorkflowResult result = Describe(PdfWorkflow.From(new MemoryStream(input))).Save(output);

        byte[] direct = WorkflowPdf.Direct(input, Apply);

        // The metadata edit refreshes ModDate from the clock, so the comparison is semantic, not byte for byte.
        WorkflowPdf.AssertOneRevision(input, output.ToArray());
        Assert.True(result.Modified);
        Assert.Equal(output.Length, result.OutputLength);
        using PdfDocument fromWorkflow = WorkflowPdf.Open(output.ToArray());
        using PdfDocument fromEngine = WorkflowPdf.Open(direct);
        Assert.Equal(fromEngine.Revisions.Count, fromWorkflow.Revisions.Count);
        Assert.Equal(fromEngine.Metadata.Title, fromWorkflow.Metadata.Title);
        Assert.Equal("Composed", fromWorkflow.Metadata.Title);
        Assert.Equal(fromEngine.AcroForm.Fields.Count, fromWorkflow.AcroForm.Fields.Count);
        Assert.Empty(fromWorkflow.AcroForm.Fields);
        Assert.Equal(Inventory(fromEngine), Inventory(fromWorkflow));
        Assert.Equal(new[] { "invoice.xml|<invoice/>" }, Inventory(fromWorkflow));
        Assert.Equal(fromEngine.Pages[0].Rotation, fromWorkflow.Pages[0].Rotation);
        Assert.Equal(90, fromWorkflow.Pages[0].Rotation);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void every_operation_together_reads_no_more_than_the_direct_sequence()
    {
        byte[] input = WorkflowPdf.Form(padding: 6 * Mebibyte);

        var workflowInput = new ProbeStream(new MemoryStream(input));
        Describe(PdfWorkflow.From(workflowInput)).Save(new MemoryStream());

        var directInput = new ProbeStream(new MemoryStream(input));
        using (PdfDocument document = PdfDocument.Open(directInput, leaveOpen: true))
        using (PdfDocumentEditor editor = document.Edit())
        {
            Apply(editor);
            editor.SaveIncremental(new MemoryStream(), leaveOpen: false);
        }

        Assert.True(
            workflowInput.BytesRead <= directInput.BytesRead,
            "The workflow read " + workflowInput.BytesRead + " bytes; the direct sequence read " + directInput.BytesRead + ".");
        Assert.True(workflowInput.MaxRequested <= directInput.MaxRequested);
    }

    private static PdfWorkflowBuilder Describe(PdfWorkflowBuilder workflow)
    {
        return workflow
            .RotatePageBy(1, 90)
            .Attach("invoice.xml", Content("<invoice/>"), new PdfAttachmentOptions { CreationDate = Date, ModificationDate = Date })
            .WithTitle("Composed")
            .FlattenForm()
            .FillForm(new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["City"] = "Paris" });
    }

    // The engine calls the workflow makes, in its execution order: fill, flatten, then the rest as described.
    private static void Apply(PdfDocumentEditor editor)
    {
        editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["City"] = "Paris" });
        editor.AcroForm.Flatten();
        editor.Pages[0].Rotation += 90;
        editor.Attachments.Add("invoice.xml", Content("<invoice/>"), false, new PdfAttachmentOptions { CreationDate = Date, ModificationDate = Date });
        editor.Metadata.Title = "Composed";
    }

    private static string[] Inventory(PdfDocument document)
    {
        return document.Attachments.Select(attachment =>
        {
            using Stream content = attachment.Open();
            using var reader = new StreamReader(content, Encoding.ASCII);
            return attachment.Name + "|" + reader.ReadToEnd();
        }).ToArray();
    }

    private static MemoryStream Content(string text)
    {
        return new MemoryStream(Encoding.ASCII.GetBytes(text));
    }
}
