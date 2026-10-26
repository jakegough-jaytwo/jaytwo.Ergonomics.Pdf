using System;
using System.IO;
using System.Text;
using jaytwo.Pdf;

// Disposable public-API research probe, not an assertion-based regression suite.
internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("# engine=" + ((System.Runtime.Versioning.TargetFrameworkAttribute)Attribute.GetCustomAttribute(typeof(PdfDocument).Assembly, typeof(System.Runtime.Versioning.TargetFrameworkAttribute))!).FrameworkName);
        foreach (string line in File.ReadAllLines(Path.Combine(args[0], "cases.tsv")))
        {
            string[] row = line.Split('\t');
            string name = row[0];
            string password = args.Length > 1 ? args[1] : Encoding.UTF8.GetString(Convert.FromBase64String(row[1]));
            string stage = "open";
            try
            {
                using var source = File.OpenRead(Path.Combine(args[0], "fixtures", row[2]));
                using var document = PdfDocument.Open(source, password);
                stage = "title";
                string title = document.Metadata.Title ?? "<null>";
                stage = "attachment";
                using var attachment = document.Attachments["probe.bin"].Open();
                using var content = new MemoryStream();
                attachment.CopyTo(content);
                string payload = Convert.ToBase64String(content.ToArray());
                Console.WriteLine(name + "\tOK\t" + Convert.ToBase64String(Encoding.UTF8.GetBytes(title)) + "\t" + payload + "\t" + document.AuthenticationRole);
                if (row.Length > 3 && row[3] == "save")
                {
                    stage = "save";
                    using var editor = document.Edit();
                    editor.Metadata.Title = "saved";
                    editor.Attachments.Add("empty.bin", new MemoryStream());
                    using var output = new MemoryStream();
                    editor.SaveIncremental(output, leaveOpen: true);
                    byte[] saved = output.ToArray();
                    Directory.CreateDirectory(Path.Combine(args[0], "saved"));
                    File.WriteAllBytes(Path.Combine(args[0], "saved", name + ".pdf"), saved);
                    byte[] original = File.ReadAllBytes(Path.Combine(args[0], "fixtures", row[2]));
                    bool prefix = saved.Length >= original.Length;
                    for (int i = 0; prefix && i < original.Length; i++) prefix &= original[i] == saved[i];
                    Console.WriteLine(name + "\tSAVED\tprefix=" + prefix);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(name + "\tFAIL\t" + stage + "\t" + error.GetType().Name + "\t" + (error is PdfEncryptedException encrypted ? encrypted.Reason.ToString() : error.Message.Replace('\n', ' ').Replace('\r', ' ')));
            }
        }
    }
}
