using System;
using System.IO;
using System.Linq;
using jaytwo.Pdf;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F4 (one revision), F5 (order), F9 (rotation), and F10 (signed documents) for rotation workflows.
public class WorkflowRotationTests
{
    [Fact]
    public void a_rotation_workflow_writes_the_same_bytes_as_the_direct_engine_rotations()
    {
        byte[] input = WorkflowPdf.Pages();

        var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(input))
            .RotatePageBy(1, 90)
            .RotatePageBy(1, 90)
            .RotatePageBy(2, 90)
            .RotatePage(2, 0)
            .RotatePageBy(3, -450)
            .RotateAllPagesBy(90)
            .Save(output);

        byte[] direct = WorkflowPdf.Direct(input, editor =>
        {
            editor.Pages[0].Rotation += 90;
            editor.Pages[0].Rotation += 90;
            editor.Pages[1].Rotation += 90;
            editor.Pages[1].Rotation = 0;
            editor.Pages[2].Rotation += -450;
            foreach (PdfPageEditor page in editor.Pages)
            {
                page.Rotation += 90;
            }
        });

        WorkflowPdf.AssertOneRevision(input, output.ToArray());
        Assert.Equal(direct, output.ToArray());

        // Page 1: 0 + 90 + 90 + 90. Page 2: set to 0 after its relative turn, then + 90. Page 3: 270 - 450 + 90.
        Assert.Equal(new[] { 270, 90, 270 }, Rotations(output.ToArray()));
    }

    [Fact]
    public void rotating_every_page_adds_to_each_pages_own_rotation()
    {
        var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(WorkflowPdf.Pages())).RotateAllPagesBy(90).Save(output);

        Assert.Equal(new[] { 90, 180, 0 }, Rotations(output.ToArray()));
    }

    [Fact]
    public void a_page_above_the_page_count_is_refused_before_output()
    {
        var output = new MemoryStream();
        PdfWorkflowBuilder absolute = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Pages())).WithTitle("New").RotatePage(4, 90);
        PdfWorkflowBuilder relative = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Pages())).RotatePageBy(1, 90).RotatePageBy(4, 90);

        Assert.Throws<ArgumentOutOfRangeException>(() => absolute.Save(output));
        Assert.Throws<ArgumentOutOfRangeException>(() => relative.Save(output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void rotation_arguments_are_checked_when_described()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Pages()));

        Assert.Throws<ArgumentOutOfRangeException>(() => workflow.RotatePage(0, 90));
        Assert.Throws<ArgumentOutOfRangeException>(() => workflow.RotatePageBy(-1, 90));
        Assert.Throws<ArgumentOutOfRangeException>(() => workflow.RotatePage(1, 45));
        Assert.Throws<ArgumentOutOfRangeException>(() => workflow.RotatePageBy(1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => workflow.RotateAllPagesBy(-30));

        // The refused descriptions recorded nothing.
        Assert.False(workflow.Save(new MemoryStream()).Modified);
    }

    [Fact]
    public void a_rotation_on_a_signed_document_needs_the_opt_in()
    {
        byte[] input = WorkflowPdf.SignedForm();

        var refused = new MemoryStream();
        Assert.Throws<PdfSignatureConflictException>(() => PdfWorkflow.From(new MemoryStream(input)).RotateAllPagesBy(90).Save(refused));
        Assert.Equal(0, refused.Length);

        var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(input)).AllowBreakingExistingSignatures().RotatePage(1, 90).Save(output);

        WorkflowPdf.AssertOneRevision(input, output.ToArray());
        Assert.Equal(new[] { 90 }, Rotations(output.ToArray()));
    }

    [Fact]
    public void the_opt_in_does_not_relax_certification_for_rotation()
    {
        var output = new MemoryStream();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.CertifiedForm())).AllowBreakingExistingSignatures().RotatePage(1, 90);

        Assert.Throws<PdfCertificationLockedException>(() => workflow.Save(output));
        Assert.Equal(0, output.Length);
    }

    private static int[] Rotations(byte[] pdf)
    {
        using PdfDocument document = WorkflowPdf.Open(pdf);
        return document.Pages.Select(page => page.Rotation).ToArray();
    }
}
