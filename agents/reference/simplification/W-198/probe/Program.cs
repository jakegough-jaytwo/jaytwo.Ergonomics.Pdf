using System.Text.Json;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;
var scratch = args[0];
var rows = new List<object>();
foreach (var path in Directory.GetFiles(Path.Combine(scratch,"inputs"),"*.pdf").OrderBy(p=>p))
{
    string name = Path.GetFileNameWithoutExtension(path);
    foreach (var credential in name.StartsWith("encrypted") ? new[]{"user","owner"} : new[]{"none"})
    foreach (var op in new[]{"extract","pending-extract","pending-concat","refused-fill-save","pending-save"})
    {
        using var source = new MemoryStream(); source.Write(File.ReadAllBytes(path)); source.Position=0;
        using var doc = PdfDocument.Open(source,credential=="none"?null:credential,leaveOpen:true);
        using var editor = doc.Edit();
        using var dest = new MemoryStream();
        bool before = doc.AcroForm.IsHybridXfa;
        string result="accepted", error="", fillError="";
        try
        {
            if(op.StartsWith("pending")) { editor.AcroForm.Fill("Name","pending"); editor.AcroForm.Fill("Name","pending"); editor.Pages[0].Rotation=90; }
            if(op=="refused-fill-save")
            {
                // Keep an unrelated pending edit across a refused fill.
                editor.Pages[0].Rotation=90;
                try { editor.AcroForm.Fill("does-not-exist","bad"); } catch(Exception e) { fillError=e.GetType().Name; }
            }
            if(op.EndsWith("save")) editor.SaveIncremental(dest,leaveOpen:true);
            else if(op=="pending-concat") PdfDocumentEditor.Concatenate(new[]{new PdfAssemblyInput(doc,new[]{1})},dest,new PdfAssemblyOptions{AllowUnencryptedOutput=true});
            else editor.ExtractPages(new[]{1},dest,new PdfAssemblyOptions{AllowUnencryptedOutput=true});
            File.WriteAllBytes(Path.Combine(scratch,"outputs",name+(credential=="none"?"":"-"+credential)+"-"+op+".pdf"),dest.ToArray());
        }
        catch(Exception e) { result="refused"; error=e.GetType().Name+": "+e.Message; }
        rows.Add(new{ name,credential,op,result,error,fillError,hybridBefore=before,hybridAfter=doc.AcroForm.IsHybridXfa,xfaOnly=doc.AcroForm.IsXfaOnly,destinationBytes=dest.Length,sourceUnchanged=source.ToArray().SequenceEqual(File.ReadAllBytes(path)) });
    }
}
Console.WriteLine(JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
