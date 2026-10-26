using System.Text.Json;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;

// Disposable public-API research probe, not a test-suite or product change.
var inputDir = args[0];
var outputDir = args[1];
Directory.CreateDirectory(outputDir);
var results = new List<object>();
foreach (string file in Directory.GetFiles(inputDir, "*.pdf").Order())
{
    string id = Path.GetFileNameWithoutExtension(file);
    foreach (string op in new[] { "extract", "repeat" })
    {
        using var destination = new MemoryStream();
        string stage = "open";
        try
        {
            using var source = PdfDocument.Open(File.OpenRead(file));
            stage = "assemble";
            PdfAssemblyResult result;
            if (op == "extract")
            {
                using var editor = source.Edit();
                result = editor.ExtractPages(new[] { 1 }, destination, new PdfAssemblyOptions());
            }
            else
            {
                result = PdfDocumentEditor.Concatenate(new[] {
                    new PdfAssemblyInput(source, new[] { 2, 1, 1 }),
                    new PdfAssemblyInput(source, new[] { 1 }) }, destination);
            }
            string output = Path.Combine(outputDir, id + "-" + op + ".pdf");
            File.WriteAllBytes(output, destination.ToArray());
            stage = "read-output";
            using var assembled = PdfDocument.Open(File.OpenRead(output));
            var before = assembled.AcroForm.Fields.Select(f => new { name = f.FullName, value = f.Value?.ToString() }).ToArray();
            string edit = "no selected field";
            string name = id == "merged" ? "Input1.Name" : "Input1.Group.Name";
            if (before.Any(f => f.name == name))
            {
                stage = "edit";
                using var editor = assembled.Edit();
                PdfFieldValue value = id == "checkbox" ? PdfFieldValue.FromBoolean(true) : PdfFieldValue.FromText("Ada");
                edit = editor.AcroForm.Fill(name, value).ToString();
                using var edited = File.Create(Path.Combine(outputDir, id + "-" + op + "-edit.pdf"));
                editor.SaveIncremental(edited);
            }
            results.Add(new { id, op, result = "accepted", bytes = destination.Length, before, edit,
                omissions = result.Omissions.Where(o => o.Value != 0).ToDictionary(o => o.Key.ToString(), o => o.Value) });
        }
        catch (Exception e)
        {
            results.Add(new { id, op, result = "failed", stage, bytes = destination.Length, type = e.GetType().Name, message = e.Message });
        }
    }
}
// Colliding source font names and an empty occurrence retain positional namespaces.
using (var first = PdfDocument.Open(File.OpenRead(Path.Combine(inputDir, "nested.pdf"))))
using (var second = PdfDocument.Open(File.OpenRead(Path.Combine(inputDir, "courier.pdf"))))
using (var destination = File.Create(Path.Combine(outputDir, "font-collision.pdf")))
{
    PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(first, Array.Empty<int>()),
        new PdfAssemblyInput(first, new[] { 1 }), new PdfAssemblyInput(second, new[] { 1 }) }, destination);
}
using (var first = PdfDocument.Open(File.OpenRead(Path.Combine(inputDir, "nested.pdf"))))
using (var later = PdfDocument.Open(File.OpenRead(Path.Combine(inputDir, "missing-font.pdf"))))
using (var destination = new MemoryStream())
{
    string failure = "unexpected success";
    try
    {
        PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(first, new[] { 1 }),
            new PdfAssemblyInput(later, new[] { 1 }) }, destination);
    }
    catch (PdfAssemblyNotSupportedException e)
    {
        failure = e.GetType().Name;
    }
    File.WriteAllText(Path.Combine(outputDir, "late-refusal.json"), JsonSerializer.Serialize(new { failure, bytes = destination.Length }));
}
File.WriteAllText(Path.Combine(outputDir, "probe.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Recorded {results.Count} assembly/edit outcomes and font-collision output.");
