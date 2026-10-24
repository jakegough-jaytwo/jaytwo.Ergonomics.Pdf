using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F5 (order), F6 (forms), F10 (signed documents), F12 (failure), and F13 (result) for form workflows.
public class WorkflowFormTests
{
    [Fact]
    public void a_fill_workflow_writes_the_same_bytes_as_the_direct_engine_fill()
    {
        byte[] input = WorkflowPdf.Form();

        var output = new MemoryStream();
        PdfWorkflowResult result = PdfWorkflow.From(new MemoryStream(input))
            .Fill("Name", "Ada")
            .FillForm(new Dictionary<string, PdfFieldValue> { ["City"] = "Paris", ["Agree"] = true })
            .Save(output);

        byte[] direct = Direct(input, editor => editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue>
        {
            ["Name"] = "Ada",
            ["City"] = "Paris",
            ["Agree"] = true,
        }));

        AssertOneRevision(input, output.ToArray());
        Assert.Equal(direct, output.ToArray());
        Assert.True(result.Modified);
        Assert.Equal(output.Length, result.OutputLength);
        Assert.Equal(new[] { "Agree", "City", "Name" }, result.FieldAppearances.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(result.FieldAppearances.Values, disposition => Assert.Equal(PdfAppearanceDisposition.Generated, disposition));

        using PdfDocument filled = WorkflowPdf.Open(output.ToArray());
        Assert.Equal("Ada", Value(filled, "Name"));
        Assert.Equal("Paris", Value(filled, "City"));
        Assert.True(Assert.Single(filled.AcroForm.GetFields("Agree")).IsChecked);
        Assert.Equal(5, filled.AcroForm.Fields.Count);
    }

    [Fact]
    public void fills_described_after_a_flatten_apply_before_it()
    {
        byte[] input = WorkflowPdf.Form();

        var output = new MemoryStream();
        PdfWorkflowResult result = PdfWorkflow.From(new MemoryStream(input))
            .FlattenForm()
            .Fill("Name", "Ada")
            .Fill("Agree", true)
            .Save(output);

        byte[] direct = Direct(input, editor =>
        {
            editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["Agree"] = true });
            editor.AcroForm.Flatten();
        });

        AssertOneRevision(input, output.ToArray());
        Assert.Equal(direct, output.ToArray());
        Assert.Equal(2, result.FieldAppearances.Count);
        using PdfDocument flattened = WorkflowPdf.Open(output.ToArray());
        Assert.Empty(flattened.AcroForm.Fields);
    }

    [Fact]
    public void the_last_fill_of_a_field_is_the_only_one_sent()
    {
        byte[] input = WorkflowPdf.Form();

        // The first value cannot be drawn, so had it reached the engine the result would say NeedsViewer, and two
        // entries for one name would make the bulk fill throw.
        var output = new MemoryStream();
        PdfWorkflowResult result = PdfWorkflow.From(new MemoryStream(input))
            .Fill("Name", "日本")
            .FillForm(new Dictionary<string, PdfFieldValue> { ["City"] = "Paris" })
            .Fill("Name", "Ada")
            .Fill("City", "Rome")
            .Save(output);

        Assert.Equal(2, result.FieldAppearances.Count);
        Assert.Equal(PdfAppearanceDisposition.Generated, result.FieldAppearances["Name"]);
        Assert.Equal(PdfAppearanceDisposition.Generated, result.FieldAppearances["City"]);
        using PdfDocument filled = WorkflowPdf.Open(output.ToArray());
        Assert.Equal("Ada", Value(filled, "Name"));
        Assert.Equal("Rome", Value(filled, "City"));
    }

    [Fact]
    public void a_value_the_engine_cannot_draw_reports_needs_viewer()
    {
        var output = new MemoryStream();
        PdfWorkflowResult result = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Form())).Fill("Name", "日本").Save(output);

        Assert.Equal(PdfAppearanceDisposition.NeedsViewer, Assert.Single(result.FieldAppearances).Value);
    }

    [Fact]
    public void named_flattens_reach_the_engine_as_one_flatten_of_their_union()
    {
        byte[] input = WorkflowPdf.Form();

        // Flattening Name twice through the engine would throw for the repeated name.
        var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(input))
            .Fill("Name", "Ada")
            .FlattenForm("Name")
            .FlattenForm("City", "Name")
            .Save(output);

        byte[] direct = Direct(input, editor =>
        {
            editor.AcroForm.Fill("Name", "Ada");
            editor.AcroForm.Flatten(new[] { "Name", "City" });
        });

        AssertOneRevision(input, output.ToArray());
        Assert.Equal(direct, output.ToArray());
        using PdfDocument flattened = WorkflowPdf.Open(output.ToArray());
        Assert.Equal(new[] { "Agree", "Locked", "Code" }, flattened.AcroForm.Fields.Select(field => field.FullName));
    }

    [Fact]
    public void a_flatten_of_every_field_covers_a_named_flatten()
    {
        byte[] input = WorkflowPdf.Form();

        var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(input)).FlattenForm("Name").FlattenForm().Save(output);

        byte[] direct = Direct(input, editor => editor.AcroForm.Flatten());

        AssertOneRevision(input, output.ToArray());
        Assert.Equal(direct, output.ToArray());
        using PdfDocument flattened = WorkflowPdf.Open(output.ToArray());
        Assert.Empty(flattened.AcroForm.Fields);
    }

    [Fact]
    public void form_and_metadata_operations_share_one_revision()
    {
        byte[] input = WorkflowPdf.Form();

        var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(input)).WithTitle("Filled").Fill("Name", "Ada").Save(output);

        AssertOneRevision(input, output.ToArray());
        using PdfDocument edited = WorkflowPdf.Open(output.ToArray());
        Assert.Equal("Filled", edited.Metadata.Title);
        Assert.Equal("Ada", Value(edited, "Name"));
    }

    [Theory]
    [InlineData("Missing", "Ada", typeof(PdfFieldNotFoundException))]
    [InlineData("Locked", "Ada", typeof(PdfFieldReadOnlyException))]
    [InlineData("Code", "TOO LONG", typeof(PdfFieldValueException))]
    public void a_refused_fill_throws_its_engine_exception_and_writes_nothing(string name, string value, Type expected)
    {
        var output = new MemoryStream();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Form())).Fill(name, value);

        Exception thrown = Assert.ThrowsAny<Exception>(() => workflow.Save(output));

        Assert.IsType(expected, thrown);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void fills_described_in_separate_calls_are_all_or_nothing()
    {
        byte[] input = WorkflowPdf.Form();
        var output = new MemoryStream();
        output.Write(input, 0, input.Length);
        output.Position = 0;

        PdfWorkflowBuilder workflow = PdfWorkflow.From(output).Fill("Name", "Ada").Fill("Agree", true).Fill("Code", "TOO LONG");

        Assert.Throws<PdfFieldValueException>(() => workflow.AppendToSource());
        Assert.Equal(input, output.ToArray());
    }

    [Fact]
    public void an_undrawable_fill_followed_by_a_flatten_is_refused()
    {
        var output = new MemoryStream();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Form())).Fill("Name", "日本").FlattenForm();

        Assert.Throws<PdfUnsupportedFormException>(() => workflow.Save(output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void a_flatten_on_a_signed_document_needs_the_opt_in()
    {
        byte[] input = WorkflowPdf.SignedForm();

        var refused = new MemoryStream();
        Assert.Throws<PdfSignatureConflictException>(() => PdfWorkflow.From(new MemoryStream(input)).FlattenForm("Name").Save(refused));
        Assert.Equal(0, refused.Length);

        var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(input)).AllowBreakingExistingSignatures().FlattenForm().Save(output);

        AssertOneRevision(input, output.ToArray());
        using PdfDocument flattened = WorkflowPdf.Open(output.ToArray());
        Assert.Equal("Sig", Assert.Single(flattened.AcroForm.Fields).FullName);
    }

    [Fact]
    public void the_opt_in_does_not_relax_certification()
    {
        var output = new MemoryStream();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.CertifiedForm())).AllowBreakingExistingSignatures().FlattenForm();

        Assert.Throws<PdfCertificationLockedException>(() => workflow.Save(output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void form_arguments_are_checked_and_copied_when_described()
    {
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Form()));
        Assert.Throws<ArgumentNullException>(() => workflow.Fill(null!, "Ada"));
        Assert.Throws<ArgumentNullException>(() => workflow.FillForm(null!));
        Assert.Throws<ArgumentException>(() => workflow.FillForm(new[] { new KeyValuePair<string, PdfFieldValue>(null!, "Ada") }));
        Assert.Throws<ArgumentNullException>(() => workflow.FlattenForm(null!));
        Assert.Throws<ArgumentException>(() => workflow.FlattenForm("Name", null!));

        var values = new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada" };
        string[] names = { "City" };
        workflow.FillForm(values).FlattenForm(names);
        values["Name"] = "Grace";
        values["Missing"] = "x";
        names[0] = "Missing";

        var output = new MemoryStream();
        workflow.Save(output);

        using PdfDocument edited = WorkflowPdf.Open(output.ToArray());
        Assert.Equal("Ada", Value(edited, "Name"));
        Assert.DoesNotContain(edited.AcroForm.Fields, field => field.FullName == "City");
    }

    private static byte[] Direct(byte[] input, Action<PdfDocumentEditor> edit)
    {
        var output = new MemoryStream();
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(input), leaveOpen: false))
        using (PdfDocumentEditor editor = document.Edit())
        {
            edit(editor);
            editor.SaveIncremental(output, leaveOpen: true);
        }

        return output.ToArray();
    }

    private static void AssertOneRevision(byte[] input, byte[] output)
    {
        WorkflowPdf.AssertPrefix(input, output);
        using PdfDocument original = WorkflowPdf.Open(input);
        using PdfDocument edited = WorkflowPdf.Open(output);
        Assert.Equal(original.Revisions.Count + 1, edited.Revisions.Count);
    }

    private static string? Value(PdfDocument document, string name)
    {
        return Assert.Single(document.AcroForm.GetFields(name)).Value;
    }
}
