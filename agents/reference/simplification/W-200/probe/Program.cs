using System.Text;
using System.Text.Json;
using jaytwo.Pdf;
var scratch = args[0];
var rows = new List<object>();
foreach (var path in Directory.GetFiles(Path.Combine(scratch, "inputs"), "*.pdf").OrderBy(p => p))
foreach (var role in Path.GetFileName(path).StartsWith("restricted") ? new[] { "user", "owner" } : new[] { "none" })
foreach (var op in new[] { "title", "remove-title", "author", "date", "raw", "raw-invalid", "invalid-char" })
{
    var original = File.ReadAllBytes(path);
    using var source = new MemoryStream(original, false);
    using var dest = new MemoryStream();
    string result = "accepted", error = "", pendingTitle = "", operationError = "";
    long afterOperation = 0;
    try
    {
        using var doc = PdfDocument.Open(source, role == "none" ? null : role, leaveOpen: true);
        using var editor = doc.Edit();
        // The preceding edit tests whether a later refused raw packet preserves pending state.
        if (op == "raw-invalid") editor.Metadata["Department"] = "pending";
        try
        {
            switch (op)
            {
                case "title": editor.Metadata.Title = "W200 日本語"; break;
                case "invalid-char": editor.Metadata.Title = "bad\u0001title"; break;
                case "remove-title": editor.Metadata.Title = null; break;
                case "author": editor.Metadata.Author = "Ada, Bob; Company"; break;
                case "date":
                    editor.Metadata.ModificationDate = new DateTimeOffset(2024, 2, 3, 4, 5, 6, TimeSpan.FromMinutes(330));
                    editor.Metadata.Title = "dated";
                    break;
                case "raw": editor.Metadata.SetXmp(Encoding.UTF8.GetBytes("<raw>packet</raw>")); break;
                case "raw-invalid": editor.Metadata.SetXmp(Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY e 'bad'>]><x>&e;</x>")); break;
            }
        }
        catch (Exception e) { operationError = e.GetType().Name; }
        afterOperation = dest.Length;
        pendingTitle = doc.Metadata.Title ?? "<absent>";
        editor.SaveIncremental(dest, leaveOpen: true);
        File.WriteAllBytes(Path.Combine(scratch, "outputs", Path.GetFileNameWithoutExtension(path) + "-engine-" + role + "-" + op + ".pdf"), dest.ToArray());
    }
    catch (Exception e) { result = "refused"; error = e.GetType().Name + ": " + e.Message; }
    rows.Add(new { name = Path.GetFileNameWithoutExtension(path), role, op, result, error, operationError, pendingTitle, afterOperation, destinationBytes = dest.Length, sourceUnchanged = source.ToArray().SequenceEqual(original), prefix = dest.Length >= original.Length && dest.ToArray().AsSpan(0, original.Length).SequenceEqual(original) });
}
Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
