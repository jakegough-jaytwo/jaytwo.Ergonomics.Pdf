using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace jaytwo.Pdf.Tests;

// D-63: every focused set is registered and flat, every fixture in it is registered with its hash, and every entry
// records its provenance, license, and evidence. These list and hash files and open none, so they run in the fast lane.
public class FocusedFixtureTests
{
    private static readonly string[] Results = { "pass", "fail", "not-run", "unsupported" };

    private static readonly string[] RequiredText =
    {
        "sha256", "workItem", "purpose", "producer", "source", "acquired", "transformations", "license", "redistribution", "expected",
    };

    [Fact]
    public void every_focused_set_is_registered_and_flat()
    {
        var problems = new List<string>();
        foreach (string path in Directory.GetDirectories(FocusedFixtures.Fixtures))
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith(FocusedFixtures.Prefix, StringComparison.Ordinal) && !FocusedFixtures.Sets.Contains(name, StringComparer.Ordinal))
            {
                problems.Add(name + ": not a registered focused set");
            }
        }

        foreach (string set in FocusedFixtures.Sets)
        {
            string path = FocusedFixtures.PathOf(set);
            if (!set.StartsWith(FocusedFixtures.Prefix, StringComparison.Ordinal))
            {
                problems.Add(set + ": a focused set is named " + FocusedFixtures.Prefix + "<purpose>");
            }

            if (!Directory.Exists(path))
            {
                problems.Add(set + ": registered, but missing");
                continue;
            }

            if (!File.Exists(Path.Combine(path, "fixtures.json")))
            {
                problems.Add(set + ": no fixtures.json");
            }

            foreach (string directory in Directory.GetDirectories(path))
            {
                problems.Add(set + "/" + Path.GetFileName(directory) + ": a focused set has no subdirectories");
            }

            foreach (string file in Directory.GetFiles(path))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                {
                    problems.Add(set + "/" + Path.GetFileName(file) + ": a focused set has no symbolic links");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void every_focused_fixture_is_registered_with_its_hash()
    {
        var problems = new List<string>();
        foreach (string set in FocusedFixtures.Sets)
        {
            string path = FocusedFixtures.PathOf(set);
            var registered = FocusedFixtures.Entries(set).Select(entry => entry.GetProperty("file").GetString()!).ToList();
            foreach (string file in Directory.GetFiles(path, "*.pdf"))
            {
                if (!registered.Contains(Path.GetFileName(file), StringComparer.Ordinal))
                {
                    problems.Add(set + "/" + Path.GetFileName(file) + ": not registered");
                }
            }

            foreach (string file in registered.GroupBy(file => file, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
            {
                problems.Add(set + "/" + file + ": registered twice");
            }

            foreach (JsonElement entry in FocusedFixtures.Entries(set))
            {
                string file = entry.GetProperty("file").GetString()!;
                string fixture = Path.Combine(path, file);
                if (!File.Exists(fixture))
                {
                    problems.Add(set + "/" + file + ": registered, but missing");
                }
                else if (CorpusManifest.Sha256(File.ReadAllBytes(fixture)) != CorpusManifest.Text(entry, "sha256"))
                {
                    problems.Add(set + "/" + file + ": SHA-256 differs");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void every_focused_fixture_records_provenance_license_and_evidence()
    {
        var problems = new List<string>();
        foreach (string set in FocusedFixtures.Sets)
        {
            foreach (JsonElement entry in FocusedFixtures.Entries(set))
            {
                string file = set + "/" + entry.GetProperty("file").GetString();
                foreach (string name in RequiredText)
                {
                    if (string.IsNullOrWhiteSpace(CorpusManifest.Text(entry, name)))
                    {
                        problems.Add(file + ": missing " + name);
                    }
                }

                foreach (JsonElement attribution in Array(entry, "attribution"))
                {
                    if (!File.Exists(Path.Combine(FocusedFixtures.PathOf(set), attribution.GetString()!)))
                    {
                        problems.Add(file + ": attribution file " + attribution.GetString() + " is not in the set");
                    }
                }

                if (Array(entry, "tests").Count == 0)
                {
                    problems.Add(file + ": names no test");
                }

                foreach (JsonElement test in Array(entry, "tests"))
                {
                    if (!DefectFixtureTests.IsTest(test.GetString()!))
                    {
                        problems.Add(file + ": " + test.GetString() + " is not a test in this assembly");
                    }
                }

                if (Array(entry, "validators").Count == 0)
                {
                    problems.Add(file + ": names no validator");
                }

                foreach (JsonElement validator in Array(entry, "validators"))
                {
                    string tool = CorpusManifest.Text(validator, "tool") ?? "(no tool)";
                    string? result = CorpusManifest.Text(validator, "result");
                    if (!Results.Contains(result, StringComparer.Ordinal))
                    {
                        problems.Add(file + ": " + tool + " has result " + (result ?? "(none)"));
                    }

                    if (string.IsNullOrWhiteSpace(CorpusManifest.Text(validator, "property")))
                    {
                        problems.Add(file + ": " + tool + " names no property");
                    }

                    if (result == "unsupported" && (CorpusManifest.Text(validator, "version") == null || CorpusManifest.Text(validator, "reason") == null))
                    {
                        problems.Add(file + ": an unsupported " + tool + " result names the tool version and the reason");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static IReadOnlyList<JsonElement> Array(JsonElement entry, string name)
    {
        return entry.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().ToList()
            : new List<JsonElement>();
    }
}
