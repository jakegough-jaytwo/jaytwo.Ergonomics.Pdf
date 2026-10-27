using System.Text.Json;
using jaytwo.Pdf;
var scratch = args[0];
var rows = new List<object>();
foreach (var path in Directory.GetFiles(Path.Combine(scratch,"inputs"),"*.pdf").OrderBy(p=>p))
foreach (var credential in Path.GetFileName(path).StartsWith("encrypted-") ? new[]{"user","owner"} : new[]{"none"})
foreach (var op in new[]{"save", "fill", "metadata", "refused-metadata-fill"})
{
    var original = File.ReadAllBytes(path);
    using var source = new MemoryStream(original, writable:false);
    using var dest = new MemoryStream();
    string result="accepted", error="", precedingError="";
    bool? hybridAfter=null;
    try
    {
        using var doc = PdfDocument.Open(source,credential=="none"?null:credential,leaveOpen:true);
        using var editor = doc.Edit();
        if(op=="metadata") editor.Metadata.Title="W199";
        if(op=="refused-metadata-fill")
        {
            try { editor.Metadata.Title="earlier"; } catch(Exception e) { precedingError=e.GetType().Name; }
        }
        if(op is "fill" or "refused-metadata-fill") editor.AcroForm.Fill("Name","Ada");
        hybridAfter=doc.AcroForm.IsHybridXfa;
        editor.SaveIncremental(dest,leaveOpen:true);
        File.WriteAllBytes(Path.Combine(scratch,"outputs",Path.GetFileNameWithoutExtension(path)+"-"+credential+"-"+op+".pdf"),dest.ToArray());
    }
    catch(Exception e) { result="refused"; error=e.GetType().Name+": "+e.Message; }
    rows.Add(new { name=Path.GetFileNameWithoutExtension(path),credential,op,result,error,precedingError,hybridAfter,destinationBytes=dest.Length,sourceUnchanged=source.ToArray().SequenceEqual(original),prefix=dest.Length>=original.Length && dest.ToArray().AsSpan(0,original.Length).SequenceEqual(original) });
}
Console.WriteLine(JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
