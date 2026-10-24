using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace jaytwo.Pdf.Tests;

// D-59: every defect fixture is registered, names the tests that use it, and records its validators as pass, fail,
// not-run, or unsupported. The tests rerun the validators; this checks the register, so it runs in the fast lane.
public class DefectFixtureTests
{
    private static readonly string[] Results = { "pass", "fail", "not-run", "unsupported" };

    [Fact]
    public void every_defect_fixture_is_registered_with_its_hash()
    {
        IReadOnlyList<JsonElement> entries = DefectFixtures.Entries();
        var registered = entries.Select(entry => entry.GetProperty("file").GetString()!).ToList();
        var problems = new List<string>();
        foreach (string path in Directory.GetFiles(DefectFixtures.Directory, "*.pdf"))
        {
            if (!registered.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            {
                problems.Add(Path.GetFileName(path) + ": not registered");
            }
        }

        foreach (JsonElement entry in entries)
        {
            string file = entry.GetProperty("file").GetString()!;
            string path = Path.Combine(DefectFixtures.Directory, file);
            if (!File.Exists(path))
            {
                problems.Add(file + ": registered, but missing");
            }
            else if (CorpusManifest.Sha256(File.ReadAllBytes(path)) != entry.GetProperty("sha256").GetString())
            {
                problems.Add(file + ": SHA-256 differs");
            }
        }

        Assert.True(registered.Distinct(StringComparer.Ordinal).Count() == registered.Count, "a fixture is registered twice");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void every_defect_fixture_names_its_tests_and_validator_results()
    {
        var problems = new List<string>();
        foreach (JsonElement entry in DefectFixtures.Entries())
        {
            string file = entry.GetProperty("file").GetString()!;
            foreach (string name in new[] { "workItem", "command", "exercises" })
            {
                if (string.IsNullOrWhiteSpace(CorpusManifest.Text(entry, name)))
                {
                    problems.Add(file + ": missing " + name);
                }
            }

            if (entry.GetProperty("tests").GetArrayLength() == 0)
            {
                problems.Add(file + ": names no test");
            }

            foreach (JsonElement test in entry.GetProperty("tests").EnumerateArray())
            {
                if (!IsTest(test.GetString()!))
                {
                    problems.Add(file + ": " + test.GetString() + " is not a test in this assembly");
                }
            }

            foreach (JsonElement validator in entry.GetProperty("validators").EnumerateArray())
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

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static bool IsTest(string name)
    {
        int dot = name.LastIndexOf('.');
        Type? type = dot < 0 ? null : typeof(DefectFixtureTests).Assembly.GetType(typeof(DefectFixtureTests).Namespace + "." + name.Substring(0, dot));
        MethodInfo? method = type?.GetMethod(name.Substring(dot + 1), BindingFlags.Public | BindingFlags.Instance);
        return method?.GetCustomAttribute<FactAttribute>() != null;
    }
}
