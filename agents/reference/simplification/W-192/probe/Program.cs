using System.Text.Json;
using jaytwo.Pdf;

var rows = new List<object>();
Directory.CreateDirectory(args[1]);
foreach (var path in Directory.GetFiles(args[0], "*.pdf").Order())
{
    var name = Path.GetFileNameWithoutExtension(path);
    var operations = name == "navigation" ? new[] { "repeat", "exclude", "twice" } : new[] { "single" };
    foreach (var operation in operations)
    {
        using var document = PdfDocument.Open(File.OpenRead(path), leaveOpen: false);
        using var destination = new MemoryStream();
        var pages = operation == "exclude" ? new[] { 1, 1 } : operation == "single" ? new[] { 1 } : new[] { 1, 1, 2, 2 };
        string? error = null;
        Dictionary<string, long>? omissions = null;
        try
        {
            var inputs = operation == "twice"
                ? new[] { new PdfAssemblyInput(document, pages), new PdfAssemblyInput(document, pages) }
                : new[] { new PdfAssemblyInput(document, pages) };
            var result = PdfDocumentEditor.Concatenate(inputs, destination);
            omissions = result.Omissions.ToDictionary(p => p.Key.ToString(), p => p.Value);
            File.WriteAllBytes(Path.Combine(args[1], name + "-" + operation + ".pdf"), destination.ToArray());
        }
        catch (Exception exception)
        {
            error = exception.GetType().Name;
        }
        rows.Add(new { name, operation, pages, error, bytes = destination.Length, omissions });
    }
}
Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
