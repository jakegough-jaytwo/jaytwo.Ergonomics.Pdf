using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Independent reads of AcroForm values: Fixtures/form_values.py walks the field tree with pypdf, and qpdf --check
// accepts or rejects the file. These compare the library's Export with what pypdf reads.
internal static class FormValues
{
    private static readonly HashSet<string> ExportedKinds = new HashSet<string>(StringComparer.Ordinal) { "text", "checkbox", "radio", "choice", "multichoice" };

    // Every terminal field pypdf finds, keyed by full name.
    public static Dictionary<string, JsonElement> Pypdf(byte[] pdf, string? password = null)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "form.pdf");
            File.WriteAllBytes(path, pdf);
            var args = new List<string> { ExternalTool.Fixture("form_values.py"), path };
            if (password != null)
            {
                args.Add(password);
            }

            using Process process = ExternalTool.Start(ExternalTool.Python(), args.ToArray());
            using JsonDocument document = JsonDocument.Parse(ExternalTool.Output(process));
            return document.RootElement.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // The value pypdf reads, in the shape R6 gives it, or null for a field R6 leaves out.
    public static PdfFieldValue? Expected(JsonElement field)
    {
        string kind = field.GetProperty("kind").GetString()!;
        if (!ExportedKinds.Contains(kind))
        {
            return null;
        }

        JsonElement value = field.GetProperty("value");
        return value.ValueKind switch
        {
            JsonValueKind.Null => PdfFieldValue.None,
            JsonValueKind.True => PdfFieldValue.FromBoolean(true),
            JsonValueKind.False => PdfFieldValue.FromBoolean(false),
            JsonValueKind.Array => PdfFieldValue.FromList(value.EnumerateArray().Select(item => item.GetString()!)),
            _ => PdfFieldValue.FromText(value.GetString()),
        };
    }

    // The library's export holds exactly the fields pypdf reports as form data, with the same values.
    public static void AssertAgree(IReadOnlyDictionary<string, PdfFieldValue> export, Dictionary<string, JsonElement> pypdf, string context)
    {
        var expected = new Dictionary<string, PdfFieldValue>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonElement> field in pypdf)
        {
            if (Expected(field.Value) is PdfFieldValue value)
            {
                expected[field.Key] = value;
            }
        }

        string[] missing = expected.Keys.Except(export.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        string[] extra = export.Keys.Except(expected.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0 && extra.Length == 0, context + ": missing [" + string.Join(", ", missing) + "], extra [" + string.Join(", ", extra) + "]");
        foreach (KeyValuePair<string, PdfFieldValue> item in expected)
        {
            Assert.True(item.Value == export[item.Key], context + ": " + item.Key + " is " + export[item.Key].ToString() + " in the export and " + item.Value.ToString() + " in pypdf");
        }
    }

    public static void AssertQpdfCheck(byte[] pdf, string? password = null)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "form.pdf");
            File.WriteAllBytes(path, pdf);
            var args = new List<string>();
            if (password != null)
            {
                args.Add("--password=" + password);
            }

            args.Add("--check");
            args.Add(path);
            using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), args.ToArray());
            (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
            Assert.True(exitCode == 0 && !(stdout + stderr).Contains("WARNING"), "qpdf --check exited " + exitCode.ToString() + Environment.NewLine + stderr + stdout);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
