using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// The G2 corpus manifest validator (QUALIFICATION.md section 5). The thresholds are the gate's, not the manifest's,
// so editing the manifest cannot weaken them. Run it whenever a corpus fixture is added or changed.
public class CorpusManifestTests
{
    private static readonly string[] Categories = { "forms", "office-browser", "scans-linearized", "reports" };

    private static readonly string[] TestGenerators = { "pypdf", "qpdf", "pyHanko" };

    // QUALIFICATION.md section 5: coverage the supported corpus must include, as corpus_probe.py names it.
    private static readonly string[] RequiredFeatures =
    {
        "classic-xref", "xref-stream", "object-streams", "hybrid-xref", "multiple-revisions", "inherited-page-attributes",
        "encryption", "acroform", "signature-field", "xmp", "embedded-files",
    };

    // The examples QUALIFICATION.md section 5 gives for each category.
    private static readonly (string Category, string Feature)[] CategoryFeatures =
    {
        ("forms", "xfa"), ("forms", "usage-rights"), ("forms", "signed"), ("scans-linearized", "linearized"),
        ("reports", "thousand-pages"),
    };

    [Fact]
    public void manifest_entries_are_complete()
    {
        var problems = new List<string>();
        foreach (CorpusEntry entry in CorpusManifest.Entries)
        {
            JsonElement e = entry.Element;
            foreach (string name in new[] { "id", "file", "sha256", "category", "authoring", "classification", "justification", "exercises" })
            {
                if (string.IsNullOrWhiteSpace(CorpusManifest.Text(e, name)))
                {
                    problems.Add(entry.Id + ": missing " + name);
                }
            }

            if (!e.TryGetProperty("size", out JsonElement size) || size.ValueKind != JsonValueKind.Number)
            {
                problems.Add(entry.Id + ": missing size");
            }

            JsonElement provenance = e.GetProperty("provenance");
            foreach (string name in new[] { "method", "source", "acquired", "command" })
            {
                if (string.IsNullOrWhiteSpace(CorpusManifest.Text(provenance, name)))
                {
                    problems.Add(entry.Id + ": missing provenance." + name);
                }
            }

            if (!DateTime.TryParseExact(CorpusManifest.Text(provenance, "acquired"), "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
            {
                problems.Add(entry.Id + ": provenance.acquired is not yyyy-MM-dd");
            }

            JsonElement license = e.GetProperty("license");
            if (string.IsNullOrWhiteSpace(CorpusManifest.Text(license, "terms")))
            {
                problems.Add(entry.Id + ": missing license.terms");
            }

            if (license.GetProperty("redistributable").ValueKind != JsonValueKind.True)
            {
                // A file that cannot be committed needs a fetch script with a pinned hash and checks that report not run.
                // No corpus file needs that path yet, so one cannot be added without building it.
                problems.Add(entry.Id + ": not redistributable; the corpus has no fetch path for it");
            }

            if (string.IsNullOrWhiteSpace(CorpusManifest.Text(e.GetProperty("producer"), "name")))
            {
                problems.Add(entry.Id + ": missing producer.name");
            }

            if (!Categories.Contains(entry.Category) && entry.Category != "synthetic")
            {
                problems.Add(entry.Id + ": unknown category " + entry.Category);
            }

            if (entry.Classification != "supported" && entry.Classification != "unsupported")
            {
                problems.Add(entry.Id + ": unknown classification " + entry.Classification);
            }

            if (entry.Classification == "unsupported" && entry.Open == "ok" && (entry.Revise == null || entry.Revise == "ok"))
            {
                problems.Add(entry.Id + ": an unsupported file must name the exception it fails with");
            }

            if (string.IsNullOrWhiteSpace(entry.Open))
            {
                problems.Add(entry.Id + ": missing operations.open");
            }

            if (entry.Open == "ok" && string.IsNullOrWhiteSpace(entry.Attachments))
            {
                problems.Add(entry.Id + ": missing operations.attachments");
            }

            if (entry.Open != "ok" && (entry.Attachments != null || entry.Revise != null || entry.Fill != null))
            {
                problems.Add(entry.Id + ": a file that does not open lists no other operations");
            }

            if (entry.Classification == "supported" && entry.Open != "ok")
            {
                problems.Add(entry.Id + ": a supported file opens");
            }

            if (entry.Fill is JsonElement fill)
            {
                foreach (string name in new[] { "field", "value", "result" })
                {
                    if (string.IsNullOrWhiteSpace(CorpusManifest.Text(fill, name)))
                    {
                        problems.Add(entry.Id + ": missing operations.fill." + name);
                    }
                }
            }

            JsonElement validators = e.GetProperty("validators");
            if (validators.GetArrayLength() == 0)
            {
                problems.Add(entry.Id + ": no validators");
            }

            foreach (JsonElement validator in validators.EnumerateArray())
            {
                foreach (string name in new[] { "tool", "version", "property" })
                {
                    if (string.IsNullOrWhiteSpace(CorpusManifest.Text(validator, name)))
                    {
                        problems.Add(entry.Id + ": validator missing " + name);
                    }
                }
            }

            int transformations = e.GetProperty("transformations").GetArrayLength();
            if ((entry.Authoring == "transformed") != (transformations > 0))
            {
                problems.Add(entry.Id + ": transformations must be listed exactly when authoring is transformed");
            }

            if (entry.Authoring != "external" && entry.Authoring != "transformed" && entry.Authoring != "test-generator")
            {
                problems.Add(entry.Id + ": unknown authoring " + entry.Authoring);
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void manifest_entries_are_distinct_and_match_their_files()
    {
        IReadOnlyList<CorpusEntry> entries = CorpusManifest.Entries;
        Assert.Equal(entries.Count, entries.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(entries.Count, entries.Select(e => e.File).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(entries.Count, entries.Select(e => CorpusManifest.Text(e.Element, "sha256")).Distinct(StringComparer.Ordinal).Count());
        foreach (CorpusEntry entry in entries)
        {
            Assert.True(File.Exists(entry.FullPath), entry.Id + ": " + entry.FullPath);
            byte[] data = File.ReadAllBytes(entry.FullPath);
            Assert.True(CorpusManifest.Text(entry.Element, "sha256") == CorpusManifest.Sha256(data), entry.Id + ": SHA-256 differs");
            Assert.True(entry.Element.GetProperty("size").GetInt64() == data.LongLength, entry.Id + ": size differs");
        }
    }

    [Fact]
    public void every_fixture_pdf_has_a_manifest_entry()
    {
        string fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var listed = new HashSet<string>(CorpusManifest.Entries.Select(e => e.File), StringComparer.OrdinalIgnoreCase);
        var files = Directory.GetFiles(fixtures, "*.pdf").Select(Path.GetFileName)
            .Concat(Directory.GetFiles(CorpusManifest.Directory, "*.pdf").Select(path => "corpus/" + Path.GetFileName(path)))
            .ToList();
        string[] orphans = files.Where(file => !listed.Contains(file!)).Select(file => file!).ToArray();
        Assert.True(orphans.Length == 0, "No manifest entry: " + string.Join(", ", orphans));
    }

    [Fact]
    public void corpus_meets_the_g2_thresholds()
    {
        IReadOnlyList<CorpusEntry> entries = CorpusManifest.Entries;
        var problems = new List<string>();
        List<CorpusEntry> realistic = entries.Where(e => e.Category != "synthetic").ToList();
        if (realistic.Count < 30)
        {
            problems.Add("fewer than 30 files outside the synthetic category: " + realistic.Count.ToString());
        }

        foreach (string category in Categories)
        {
            int count = entries.Count(e => e.Category == category);
            if (count < 5)
            {
                problems.Add(category + " has " + count.ToString() + " files; at least 5 are required");
            }
        }

        int external = entries.Count(e => e.Authoring == "external");
        if (external * 3 < entries.Count * 2)
        {
            problems.Add(external.ToString() + " of " + entries.Count.ToString() + " files are from tools other than the test generators; two thirds are required");
        }

        foreach (CorpusEntry entry in entries)
        {
            string producer = CorpusManifest.Text(entry.Element.GetProperty("producer"), "name") ?? string.Empty;
            if (entry.Category == "synthetic" && entry.Authoring != "test-generator")
            {
                problems.Add(entry.Id + ": only test-generator files are synthetic");
            }

            if (entry.Authoring == "external" && TestGenerators.Any(tool => producer.StartsWith(tool, StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add(entry.Id + ": a test generator's output is not external");
            }

            if (entry.Authoring == "external" && entry.Element.GetProperty("transformations").GetArrayLength() > 0)
            {
                problems.Add(entry.Id + ": a transformed file is not external");
            }

            if (entry.Authoring == "transformed")
            {
                foreach (JsonElement transformation in entry.Element.GetProperty("transformations").EnumerateArray())
                {
                    string tool = CorpusManifest.Text(transformation, "tool") ?? string.Empty;
                    if (!TestGenerators.Contains(tool))
                    {
                        problems.Add(entry.Id + ": transformation tool " + tool + " is not a test generator; record the file as its own producer");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void supported_corpus_covers_the_required_structures()
    {
        List<CorpusEntry> supported = CorpusManifest.Entries.Where(e => e.Classification == "supported").ToList();
        var problems = new List<string>();
        foreach (string feature in RequiredFeatures)
        {
            if (!supported.Any(e => Features(e).Contains(feature)))
            {
                problems.Add("no supported file has " + feature);
            }
        }

        foreach ((string category, string feature) in CategoryFeatures)
        {
            if (!supported.Any(e => e.Category == category && Features(e).Contains(feature)))
            {
                problems.Add("no supported " + category + " file has " + feature);
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void independent_probe_agrees_with_every_manifest_entry()
    {
        // qpdf, pypdf, and pyHanko re-derive every recorded fact, so the manifest cannot drift from its files.
        IReadOnlyList<CorpusEntry> entries = CorpusManifest.Entries;
        JsonElement probes = CorpusManifest.Probe(entries.Select(e => e.ProbeSpec(e.FullPath)).ToArray());
        var problems = new List<string>();
        foreach (CorpusEntry entry in entries)
        {
            JsonElement actual = probes.GetProperty(entry.ProbeSpec(entry.FullPath));
            string expected = CorpusManifest.Recorded(entry.Probe);
            string observed = CorpusManifest.Recorded(actual);
            if (expected != observed)
            {
                problems.Add(entry.Id + Environment.NewLine + "  manifest: " + expected + Environment.NewLine + "  probe:    " + observed);
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static HashSet<string> Features(CorpusEntry entry)
    {
        return new HashSet<string>(entry.Probe.GetProperty("features").EnumerateArray().Select(f => f.GetString()!), StringComparer.Ordinal);
    }
}
