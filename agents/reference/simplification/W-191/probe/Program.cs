using System.Text.Json;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;

string inputs = args[0], outputs = args[1];
Directory.CreateDirectory(outputs);
var results = new List<object>();
foreach (string file in Directory.GetFiles(inputs, "*.pdf").OrderBy(f => f))
{
    foreach (string operation in new[] { "concat", "subset", "reorder", "repeat" })
    {
        using var source = PdfDocument.Open(File.OpenRead(file));
        using var destination = new MemoryStream();
        string id = Path.GetFileNameWithoutExtension(file);
        try
        {
            PdfAssemblyResult result;
            if (operation == "subset" || operation == "reorder")
            {
                using var editor = source.Edit();
                result = editor.ExtractPages(operation == "subset" ? new[] { 1 } : new[] { 2, 1 }, destination, new PdfAssemblyOptions());
            }
            else
            {
                result = PdfDocumentEditor.Concatenate(new[] {
                    new PdfAssemblyInput(source, operation == "repeat" ? new[] { 1, 1, 2 } : new[] { 1, 2 }),
                    new PdfAssemblyInput(source, new[] { 1 }) }, destination);
            }

            string output = Path.Combine(outputs, id + "-" + operation + ".pdf");
            File.WriteAllBytes(output, destination.ToArray());
            string? edit = null;
            if (id == "tagged-form")
            {
                using var assembled = PdfDocument.Open(File.OpenRead(output));
                using var editor = assembled.Edit();
                edit = editor.AcroForm.Fill("Input1.Name", PdfFieldValue.FromText("Ada")).ToString();
                using var edited = File.Create(Path.Combine(outputs, id + "-" + operation + "-edit.pdf"));
                editor.SaveIncremental(edited);
            }
            results.Add(new { id, operation, outcome = "accepted", bytes = destination.Length, edit,
                omissions = result.Omissions.Where(x => x.Value != 0).ToDictionary(x => x.Key.ToString(), x => x.Value) });
        }
        catch (Exception error)
        {
            results.Add(new { id, operation, outcome = "failed", bytes = destination.Length,
                type = error.GetType().Name, message = error.Message });
        }
    }
}
using (var first = PdfDocument.Open(File.OpenRead(Path.Combine(inputs, "mappings.pdf"))))
using (var second = PdfDocument.Open(File.OpenRead(Path.Combine(inputs, "conflicting-mappings.pdf"))))
using (var destination = File.Create(Path.Combine(outputs, "map-collision.pdf")))
{
    PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(first, new[] { 1 }), new PdfAssemblyInput(second, new[] { 1 }) }, destination);
}
File.WriteAllText(Path.Combine(outputs, "probe.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Recorded {results.Count} public-API assembly outcomes plus map-collision output.");
