using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Loads the G2 corpus manifest and runs the independent probe (qpdf, pypdf, pyHanko) over corpus files.
internal static class CorpusManifest
{
    private static readonly Lazy<IReadOnlyList<CorpusEntry>> _entries = new Lazy<IReadOnlyList<CorpusEntry>>(Load);

    public static IReadOnlyList<CorpusEntry> Entries => _entries.Value;

    public static string Directory
    {
        get
        {
            Lane.Refuse("The G2 corpus", "[Trait(Lane.Trait, Lane.Corpus)]");
            return ManifestDirectory;
        }
    }

    // Test discovery reads the manifest for theory data in every lane.
    private static string ManifestDirectory => Path.Combine(AppContext.BaseDirectory, "Fixtures", "corpus");

    public static IEnumerable<object[]> Ids()
    {
        return Entries.Select(entry => new object[] { entry.Id });
    }

    public static CorpusEntry Get(string id)
    {
        return Entries.Single(entry => entry.Id == id);
    }

    public static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    public static string Qpdf()
    {
        return ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
    }

    // Runs corpus_probe.py once for every spec and returns the result keyed by spec.
    public static JsonElement Probe(params string[] specs)
    {
        var args = new List<string> { Path.Combine(Directory, "corpus_probe.py"), Qpdf() };
        args.AddRange(specs);
        using Process process = ExternalTool.Start(ExternalTool.Python(), args.ToArray());
        string output = ExternalTool.Output(process);
        using JsonDocument document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }

    // The probe facts the manifest records: drops what the manifest keeps elsewhere and the qpdf warning text,
    // which differs between qpdf releases.
    public static string Recorded(JsonElement probe)
    {
        var builder = new StringBuilder();
        WriteCanonical(builder, probe, 0, new HashSet<string> { "sha256", "size", "fieldValues" });
        return builder.ToString();
    }

    public static string Canonical(JsonElement element)
    {
        var builder = new StringBuilder();
        WriteCanonical(builder, element, 0, new HashSet<string>());
        return builder.ToString();
    }

    public static string Sha256(byte[] data)
    {
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
    }

    public static string Sha256(Stream stream)
    {
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static IReadOnlyList<CorpusEntry> Load()
    {
        string path = Path.Combine(ManifestDirectory, "manifest.json");
        Assert.True(File.Exists(path), path);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement.Clone();
        return root.GetProperty("entries").EnumerateArray().Select(element => new CorpusEntry(element)).ToList();
    }

    private static void WriteCanonical(StringBuilder builder, JsonElement element, int depth, HashSet<string> skipAtTop)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                bool first = true;
                foreach (JsonProperty property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (depth == 0 && skipAtTop.Contains(property.Name))
                    {
                        continue;
                    }

                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    builder.Append(JsonSerializer.Serialize(property.Name)).Append(':');
                    if (depth == 0 && property.Name == "qpdfCheck")
                    {
                        builder.Append("{\"exit\":").Append(property.Value.GetProperty("exit").GetRawText()).Append('}');
                    }
                    else
                    {
                        WriteCanonical(builder, property.Value, depth + 1, skipAtTop);
                    }
                }

                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (index++ > 0)
                    {
                        builder.Append(',');
                    }

                    WriteCanonical(builder, item, depth + 1, skipAtTop);
                }

                builder.Append(']');
                break;
            case JsonValueKind.String:
                builder.Append(JsonSerializer.Serialize(element.GetString()));
                break;
            case JsonValueKind.Number:
                builder.Append(element.GetDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                break;
            default:
                builder.Append(element.GetRawText());
                break;
        }
    }
}
