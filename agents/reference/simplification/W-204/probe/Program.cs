using System.Text;
using System.Text.Json;
using jaytwo.Ergonomics.Pdf;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;

var root = args[0];
var evidence = args[1];
var rows = new List<object>();
var input = File.ReadAllBytes(Path.Combine(root, "form.pdf"));
KeyValuePair<string, PdfFieldValue> Pair(string key, string value) => new(key, value);

void Workflow(string name, Action<PdfWorkflowBuilder> describe, bool append = false)
{
    using var source = new CountingStream(input);
    using var target = new MemoryStream();
    using var attachment = new CountingStream(Encoding.ASCII.GetBytes("invoice"));
    var workflow = PdfWorkflow.From(source);
    string stage = "describe", error = "", consumed = "";
    long descriptionReads = -1;
    Dictionary<string, string>? appearances = null;
    try
    {
        if (name == "attach-remove") workflow.Attach("invoice.txt", attachment);
        describe(workflow);
        descriptionReads = source.BytesRead + attachment.BytesRead;
        stage = "terminal";
        var result = append ? workflow.AppendToSource() : workflow.Save(target);
        appearances = result.FieldAppearances.ToDictionary(p => p.Key, p => p.Value.ToString());
        stage = "accepted";
    }
    catch (Exception e) { error = e.GetType().Name; }
    try { workflow.WithTitle("retry"); } catch (Exception e) { consumed = e.GetType().Name; }
    var bytes = append ? source.ToArray() : target.ToArray();
    if (stage == "accepted") File.WriteAllBytes(Path.Combine(root, name + ".pdf"), bytes);
    rows.Add(new { name, api = "workflow", stage, error, descriptionReads, sourceReadBytes = source.BytesRead,
        destinationBytes = bytes.Length, prefix = stage == "accepted" && bytes.AsSpan(0, input.Length).SequenceEqual(input),
        sourceUnchanged = source.ToArray().SequenceEqual(input), sourceOpen = source.CanRead, destinationOpen = target.CanWrite,
        attachmentOpen = attachment.CanRead, attachmentReadBytes = attachment.BytesRead, consumed, appearances });
}

Workflow("same-value", w => w.Fill("Name", "Ada").Fill("Name", "Ada"));
Workflow("last-value", w => w.Fill("Name", "Default").Fill("Name", "Ada"));
Workflow("within-batch", w => w.FillForm(new[] { Pair("Name", "Default"), Pair("Name", "Ada") }));
Workflow("across-batches", w => w.FillForm(new[] { Pair("Name", "Default"), Pair("City", "Paris") }).FillForm(new[] { Pair("Name", "Ada"), Pair("City", "Rome") }));
Workflow("invalid-kind-overridden", w => w.Fill("Name", true).Fill("Name", "Ada"));
Workflow("invalid-length-overridden", w => w.Fill("Code", "TOOLONG").Fill("Code", "OK"));
Workflow("invalid-final", w => w.Fill("Code", "OK").Fill("Code", "TOOLONG"));
Workflow("undrawable-overridden", w => w.Fill("Name", "\u65e5\u672c").FlattenForm("Name").Fill("Name", "Ada"));
Workflow("undrawable-final", w => w.Fill("Name", "Ada").Fill("Name", "\u65e5\u672c").FlattenForm("Name"));
Workflow("flatten-first", w => w.FlattenForm("Name").Fill("Name", "Ada"));
Workflow("union-flatten", w => w.FlattenForm("Name").FlattenForm("City", "Name").Fill("Name", "Ada").Fill("City", "Rome"));
Workflow("all-flatten", w => w.FlattenForm("Missing").FlattenForm().Fill("Name", "Ada"));
Workflow("interleaved", w => w.WithTitle("Draft").RotatePageBy(1, 90).FlattenForm("Name").WithTitle("Final").Fill("Name", "Ada").RotatePage(1, 180).RotatePageBy(1, 90));
Workflow("relative-order", w => w.RotatePage(1, 180).RotatePageBy(1, 90).RotatePage(1, 90));
Workflow("cumulative-rotation", w => w.RotatePageBy(1, 90).RotatePageBy(1, 90));
Workflow("attach-remove", w => w.RemoveAttachment("invoice.txt").WithTitle("Final"));
Workflow("remove-before-attach", w => w.RemoveAttachment("invoice.txt").Attach("invoice.txt", new MemoryStream(new byte[] { 1 })));
Workflow("form-error-first", w => w.RemoveAttachment("missing").Fill("Code", "TOOLONG"));
Workflow("atomic-append", w => w.Fill("Name", "Ada").Fill("Code", "TOOLONG"), append: true);
Workflow("snapshot", w => { var values = new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada" }; w.FillForm(values); values["Name"] = "Changed"; });
Workflow("ordinal-names", w => w.Fill("Name", "Ada").Fill("name", "Other"));
Workflow("rotation-before-flatten", w => w.RotatePage(1, 90).Fill("Name", "Ada").FlattenForm("Name"));

void Direct(string name, Action<PdfDocumentEditor> edit, bool saveAfterError = true)
{
    using var source = new MemoryStream(input);
    using var target = new MemoryStream();
    using var doc = PdfDocument.Open(source, leaveOpen: true);
    string error = "";
    using (var editor = doc.Edit())
    {
        try { edit(editor); } catch (Exception e) { error = e.GetType().Name; }
    }
    // Deliberately use a new editor: disposing an editor does not discard pending document edits.
    if (saveAfterError || error.Length == 0)
    {
        using var next = doc.Edit();
        next.SaveIncremental(target, leaveOpen: true);
        File.WriteAllBytes(Path.Combine(root, name + ".pdf"), target.ToArray());
    }
    rows.Add(new { name, api = "engine", error, savedWithNewEditor = target.Length > 0,
        destinationBytes = target.Length, prefix = target.Length >= input.Length && target.ToArray().AsSpan(0, input.Length).SequenceEqual(input),
        sourceUnchanged = source.ToArray().SequenceEqual(input) });
}
Direct("engine-bulk-duplicate", e => e.AcroForm.Fill(new[] { Pair("Name", "Default"), Pair("Name", "Ada") }));
Direct("engine-bulk-identical", e => e.AcroForm.Fill(new[] { Pair("Name", "Ada"), Pair("Name", "Ada") }));
Direct("engine-sequential", e => { e.AcroForm.Fill("Name", "Default"); e.AcroForm.Fill("Name", "Ada"); });
Direct("engine-staged-equivalent", e => { e.AcroForm.Fill("Name", "Ada"); e.AcroForm.Flatten(new[] { "Name" }); });
Direct("engine-flatten-first", e => { e.AcroForm.Flatten(new[] { "Name" }); e.AcroForm.Fill("Name", "Ada"); });
Direct("engine-invalid-first", e => { e.AcroForm.Fill("Code", "TOOLONG"); e.AcroForm.Fill("Code", "OK"); });
Direct("engine-atomic", e => { e.Metadata.Title = "Pending"; e.AcroForm.Fill(new[] { Pair("Name", "Ada"), Pair("Code", "TOOLONG") }); });
Direct("engine-sequential-partial", e => { e.Metadata.Title = "Pending"; e.AcroForm.Fill("Name", "Ada"); e.AcroForm.Fill("Code", "TOOLONG"); });
Direct("engine-undrawable-first", e => { e.AcroForm.Fill("Name", "\u65e5\u672c"); e.AcroForm.Fill("Name", "Ada"); e.AcroForm.Flatten(new[] { "Name" }); });
Direct("engine-rotation-before-flatten", e => { e.Pages[0].Rotation = 90; e.AcroForm.Fill("Name", "Ada"); e.AcroForm.Flatten(new[] { "Name" }); });
Direct("engine-interleaved-equivalent", e => { e.AcroForm.Fill("Name", "Ada"); e.AcroForm.Flatten(new[] { "Name" }); e.Metadata.Title = "Draft"; e.Pages[0].Rotation += 90; e.Metadata.Title = "Final"; e.Pages[0].Rotation = 180; e.Pages[0].Rotation += 90; });
Direct("engine-error-order", e => { e.Attachments.Remove("missing"); e.AcroForm.Fill("Code", "TOOLONG"); });
File.WriteAllText(Path.Combine(evidence, "observations.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Recorded {rows.Count} scenarios.");
