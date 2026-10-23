using System;
using System.IO;
using System.Text.Json;

namespace jaytwo.Ergonomics.Pdf.Tests;

// One entry of Fixtures/corpus/manifest.json, the G2 corpus manifest (QUALIFICATION.md section 5).
internal sealed class CorpusEntry
{
    public CorpusEntry(JsonElement element)
    {
        Element = element;
    }

    public JsonElement Element { get; }

    public string Id => CorpusManifest.Text(Element, "id") ?? string.Empty;

    public string File => CorpusManifest.Text(Element, "file") ?? string.Empty;

    public string Category => CorpusManifest.Text(Element, "category") ?? string.Empty;

    public string Authoring => CorpusManifest.Text(Element, "authoring") ?? string.Empty;

    public string Classification => CorpusManifest.Text(Element, "classification") ?? string.Empty;

    public string? Password => CorpusManifest.Text(Element, "password");

    public string Open => CorpusManifest.Text(Element.GetProperty("operations"), "open") ?? string.Empty;

    // Listing every attachment and reading its content: "ok" or the expected exception type name.
    public string? Attachments => CorpusManifest.Text(Element.GetProperty("operations"), "attachments");

    // Null when the operation does not apply; "ok" or the expected exception type name otherwise.
    public string? Revise => CorpusManifest.Text(Element.GetProperty("operations"), "revise");

    public JsonElement? Fill
    {
        get
        {
            JsonElement fill = Element.GetProperty("operations").GetProperty("fill");
            return fill.ValueKind == JsonValueKind.Null ? null : fill;
        }
    }

    public JsonElement Probe => Element.GetProperty("probe");

    public string FullPath
    {
        get
        {
            Lane.Refuse("The G2 corpus", "[Trait(Lane.Trait, Lane.Corpus)]");
            return Path.Combine(AppContext.BaseDirectory, "Fixtures", File.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    // The probe argument for this file: PATH[::PASSWORD][::FIELD=NAME].
    public string ProbeSpec(string path, string? field = null)
    {
        string spec = path;
        if (Password != null)
        {
            spec += "::" + Password;
        }

        if (field != null)
        {
            spec += "::FIELD=" + field;
        }

        return spec;
    }

    public override string ToString()
    {
        return Id;
    }
}
