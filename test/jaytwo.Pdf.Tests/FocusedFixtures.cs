using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace jaytwo.Pdf.Tests;

// D-63: focused fixture sets, outside the G2 corpus. A set is a first-level Fixtures/focused-<purpose>/ directory named
// in Sets, with a fixtures.json that records it; a fixtures.json alone does not register a directory.
internal static class FocusedFixtures
{
    public const string Prefix = "focused-";

    public static readonly IReadOnlyList<string> Sets = new[]
    {
        "focused-assembly-links",
        "focused-assembly-invoice",
    };

    public static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string PathOf(string set) => Path.Combine(Fixtures, set);

    public static IReadOnlyList<JsonElement> Entries(string set)
    {
        using JsonDocument register = JsonDocument.Parse(File.ReadAllText(Path.Combine(PathOf(set), "fixtures.json")));
        return register.RootElement.GetProperty("fixtures").EnumerateArray().Select(entry => entry.Clone()).ToList();
    }
}
