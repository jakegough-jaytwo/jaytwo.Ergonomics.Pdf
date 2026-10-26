using System.Text.Json;
using jaytwo.Ergonomics.Pdf;
using jaytwo.Pdf;
var root = args[0];
var rows = new List<object>();
var pw = new Dictionary<string,string>{["test/jaytwo.Pdf.Tests/Fixtures/corpus/libreoffice-memo-rc4.pdf"]="corpus-user",["test/jaytwo.Pdf.Tests/Fixtures/qpdf-encrypted.pdf"]="user-secret"};
foreach (var rel in File.ReadAllLines(args[1]).Skip(1).Select(l => l.Split(',')[0]))
{
    var path = Path.Combine(root, rel);
    foreach (var d in new[]{2,3,4,5,6}) pw[$"test/jaytwo.Pdf.Tests/Fixtures/defects/pypdf-unsigned-p-r{d}.pdf"]="user-secret";
    foreach (var (op, pages) in new (string, int[]?)[] { ("combine-all", null), ("combine-page1", new[] { 1 }), ("combine-all-consent", null) })
    {
        string result, detail = "", omissions = "";
        try
        {
            using var ms = new MemoryStream();
            var r = PdfWorkflow.Combine(new[] { PdfCombineInput.FromFile(path, pages, pw.TryGetValue(rel, out var pwd) ? new PdfWorkflowOpenOptions { Password = pwd } : null) }, op == "combine-all-consent" ? new PdfAssemblyOptions { AllowUnencryptedOutput = true } : null).Save(ms);
            result = "accepted";
            omissions = string.Join(";", r.Omissions.Where(o => o.Value > 0).Select(o => o.Key + "=" + o.Value));
            detail = "bytes=" + ms.Length; if (op == "combine-all-consent") { Directory.CreateDirectory(args[2]); File.WriteAllBytes(Path.Combine(args[2], Path.GetFileName(rel)), ms.ToArray()); }
        }
        catch (Exception e) { result = "refused"; detail = e.GetType().Name + ": " + e.Message; }
        rows.Add(new { path = rel, op, credential = pw.ContainsKey(rel) ? "user-password" : "none", result, detail, omissions });
    }
}
Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));




