using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace jaytwo.Pdf.Tests;

// D-59: defect and writer-quirk fixtures, outside the G2 corpus, and Fixtures/defects/fixtures.json, which records them.
internal static class DefectFixtures
{
    public const string DirectoryName = "defects";

    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Fixtures", DirectoryName);

    public static IReadOnlyList<JsonElement> Entries()
    {
        using JsonDocument register = JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory, "fixtures.json")));
        return register.RootElement.GetProperty("fixtures").EnumerateArray().Select(entry => entry.Clone()).ToList();
    }
}
