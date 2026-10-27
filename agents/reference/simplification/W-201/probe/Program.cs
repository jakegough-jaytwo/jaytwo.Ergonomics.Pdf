using System.Text.Json;
using System.Text;
using jaytwo.Pdf;
var scratch = args[0];
var scenarios = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(args[1]))!;
var rows = new List<object>();
foreach (var (name, inputs) in scenarios)
{
    var bytes = inputs.Select(n => File.ReadAllBytes(Path.Combine(scratch, "inputs", n + ".pdf"))).ToArray();
    var streams = bytes.Select(b => new MemoryStream(b, false)).ToArray();
    var docs = new List<PdfDocument>();
    using var dest = new MemoryStream();
    string result = "accepted", error = "";
    try
    {
        docs.AddRange(streams.Select(s => PdfDocument.Open(s, leaveOpen: true)));
        if (inputs.Length == 1)
        {
            using var edit = docs[0].Edit();
            edit.ExtractPages(new[] { 1 }, dest);
        }
        else PdfDocumentEditor.Concatenate(docs.Select(d => new PdfAssemblyInput(d, new[] { 1 })), dest);
        File.WriteAllBytes(Path.Combine(scratch, "outputs", name + "-engine.pdf"), dest.ToArray());
    }
    catch (Exception e) { result = "refused"; error = e.GetType().Name + ": " + e.Message; }
    finally { foreach (var doc in docs) doc.Dispose(); }
    rows.Add(new { name, inputs, result, error, destinationBytes = dest.Length, sourceUnchanged = streams.Select((s, i) => s.ToArray().SequenceEqual(bytes[i])).All(x => x) });
    foreach (var stream in streams) stream.Dispose();
}
Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
var recoveryRows = new List<object>();
foreach (var kind in new[] { "info", "custom" })
{
    var original = File.ReadAllBytes(Path.Combine(scratch, "outputs", "a-b-engine.pdf"));
    using var source = new MemoryStream(original, false);
    using var doc = PdfDocument.Open(source, leaveOpen: true);
    using var editor = doc.Edit();
    using var dest = new MemoryStream();
    editor.Metadata.Title = "Combined dossier";
    editor.Metadata.Author = "Accounts Team";
    if (kind == "custom") editor.Metadata.SetXmp(Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:biz=\"urn:w201:business\"><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Combined dossier</rdf:li></rdf:Alt></dc:title><dc:creator><rdf:Seq><rdf:li>Accounts Team</rdf:li></rdf:Seq></dc:creator><biz:Record biz:code=\"42\">source-only</biz:Record></rdf:Description></rdf:RDF></x:xmpmeta>"));
    long beforeSaveBytes = dest.Length;
    editor.SaveIncremental(dest, leaveOpen: true);
    File.WriteAllBytes(Path.Combine(scratch, "outputs", "recovery-" + kind + ".pdf"), dest.ToArray());
    recoveryRows.Add(new { kind, setterCalls = kind == "info" ? 2 : 3, beforeSaveBytes, prefix = dest.ToArray().AsSpan(0, original.Length).SequenceEqual(original) });
}
File.WriteAllText(args[2], JsonSerializer.Serialize(recoveryRows, new JsonSerializerOptions { WriteIndented = true }));
