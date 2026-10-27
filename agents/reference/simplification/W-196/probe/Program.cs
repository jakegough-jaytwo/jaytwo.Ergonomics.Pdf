using System.Text.Json;
using jaytwo.Pdf;

// args: inputsDir outputsDir. Reads cases.json from inputsDir. Public engine API only.
var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(args[0], "cases.json"))).RootElement;
Directory.CreateDirectory(args[1]);
var rows = new List<object>();
foreach (var c in cases.EnumerateArray())
{
    var name = c.GetProperty("name").GetString()!;
    var documents = new Dictionary<string, PdfDocument>();
    using var destination = new MemoryStream();
    string? error = null;
    Dictionary<string, long>? omissions = null;
    try
    {
        var inputs = new List<PdfAssemblyInput>();
        foreach (var input in c.GetProperty("inputs").EnumerateArray())
        {
            var file = input.GetProperty("file").GetString()!;
            if (!documents.TryGetValue(file, out var document))
                documents[file] = document = PdfDocument.Open(File.OpenRead(Path.Combine(args[0], file + ".pdf")), leaveOpen: false);
            inputs.Add(new PdfAssemblyInput(document, input.GetProperty("pages").EnumerateArray().Select(p => p.GetInt32()).ToArray()));
        }
        PdfAssemblyResult result;
        if (c.GetProperty("mode").GetString() == "extract")
        {
            using var editor = inputs[0].Document.Edit();
            result = editor.ExtractPages(inputs[0].PageNumbers, destination, PdfExternalLinkMode.PreserveSupported);
        }
        else
        {
            result = PdfDocumentEditor.Concatenate(inputs, destination);
        }
        omissions = result.Omissions.ToDictionary(p => p.Key.ToString(), p => p.Value);
        File.WriteAllBytes(Path.Combine(args[1], name + ".pdf"), destination.ToArray());
    }
    catch (Exception exception) { error = exception.GetType().Name; }
    foreach (var d in documents.Values) d.Dispose();
    rows.Add(new { name, error, bytes = destination.Length, omissions });
}
Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
