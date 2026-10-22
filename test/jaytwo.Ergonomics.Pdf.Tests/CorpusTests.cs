using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using jaytwo.Ergonomics.Pdf.AcroForm;
using jaytwo.Ergonomics.Pdf.Attachments;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// G2 corpus checks (QUALIFICATION.md section 5): every manifest entry runs the existing operations it lists, and
// qpdf, pypdf, and pyHanko confirm the library's reads and its output. Feature-specific API tests live with their features.
public class CorpusTests
{
    private const string RevisedTitle = "Corpus revision";

    public static IEnumerable<object[]> Ids => CorpusManifest.Ids();

    [Theory]
    [MemberData(nameof(Ids))]
    public void library_reads_agree_with_the_independent_probe(string id)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        byte[] original = File.ReadAllBytes(entry.FullPath);
        if (entry.Open != "ok")
        {
            using var rejected = new MemoryStream(original, writable: false);
            Exception? error = Record.Exception(() => PdfDocument.Open(rejected, entry.Password, leaveOpen: true).Dispose());
            Assert.True(error != null, id + ": opened, but the manifest expects " + entry.Open);
            Assert.Equal(entry.Open, error!.GetType().Name);
            return;
        }

        JsonElement probe = entry.Probe;
        using var stream = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(stream, entry.Password, leaveOpen: true);
        Assert.Equal(probe.GetProperty("pages").GetInt32(), document.PageCount);

        PdfPage first = document.Pages[0];
        double[] box = probe.GetProperty("firstPage").GetProperty("mediaBox").EnumerateArray().Select(x => x.GetDouble()).ToArray();
        Assert.Equal(box, new[] { first.MediaBox.Llx, first.MediaBox.Lly, first.MediaBox.Urx, first.MediaBox.Ury }, new ToleranceComparer(0.001));
        Assert.Equal(probe.GetProperty("firstPage").GetProperty("rotation").GetInt32(), first.Rotation);

        JsonElement info = probe.GetProperty("info");
        Assert.Equal(CorpusManifest.Text(info, "title"), document.Metadata.Title);
        Assert.Equal(CorpusManifest.Text(info, "producer"), document.Metadata.Producer);
        Assert.Equal(probe.GetProperty("xmp").GetBoolean(), document.Metadata.HasXmp);

        IReadOnlyList<PdfField> fields = document.AcroForm.Fields;
        Assert.Equal(probe.GetProperty("widgets").GetInt32(), fields.Count);
        Assert.Equal(CorpusManifest.Text(probe, "fieldNamesSha256"), NamesHash(fields.Select(f => f.FullName)));
        Assert.Equal(HasXfa(probe), document.AcroForm.IsHybridXfa || document.AcroForm.IsXfaOnly);

        if (entry.Attachments == "ok")
        {
            string[] expectedNames = probe.GetProperty("attachments").EnumerateArray().Select(ExpectedName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(expectedNames, document.Attachments.Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
            foreach (JsonElement attachment in probe.GetProperty("attachments").EnumerateArray())
            {
                string? sha = CorpusManifest.Text(attachment, "sha256");
                Assert.True(sha != null, id + ": pypdf did not decode " + CorpusManifest.Text(attachment, "name"));
                using Stream content = document.Attachments[ExpectedName(attachment)].Open();
                Assert.Equal(sha, CorpusManifest.Sha256(content));
            }
        }
        else
        {
            // A recorded interoperability failure: it must stay a controlled, documented exception.
            Exception? error = Record.Exception(() =>
            {
                foreach (PdfAttachment attachment in document.Attachments)
                {
                    using Stream content = attachment.Open();
                    CorpusManifest.Sha256(content);
                }
            });
            Assert.True(error != null, id + ": attachments read, but the manifest records " + entry.Attachments + "; update the manifest");
            Assert.Equal(entry.Attachments, error!.GetType().Name);
        }

        JsonElement encryption = probe.GetProperty("encryption");
        if (encryption.ValueKind != JsonValueKind.Null && entry.Password != null)
        {
            JsonElement perms = encryption.GetProperty("permissions");
            PdfPermissions actual = document.Permissions;
            Assert.Equal(perms.GetProperty("print low resolution").GetBoolean(), actual.CanPrint);
            Assert.Equal(perms.GetProperty("print high resolution").GetBoolean(), actual.CanPrintHighQuality);
            Assert.Equal(perms.GetProperty("modify other").GetBoolean(), actual.CanModify);
            Assert.Equal(perms.GetProperty("extract for any purpose").GetBoolean(), actual.CanCopy);
            Assert.Equal(perms.GetProperty("modify annotations").GetBoolean(), actual.CanAnnotate);
            Assert.Equal(perms.GetProperty("modify forms").GetBoolean(), actual.CanFillForms);
            Assert.Equal(perms.GetProperty("modify document assembly").GetBoolean(), actual.CanAssemble);
            Assert.Equal(perms.GetProperty("extract for accessibility").GetBoolean(), actual.CanExtractForAccessibility);
        }
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void metadata_revision_is_accepted_by_independent_tools_or_refused_as_documented(string id)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        if (entry.Revise == null)
        {
            Assert.NotEqual("ok", entry.Open);
            return;
        }

        byte[] original = File.ReadAllBytes(entry.FullPath);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, entry.Password, leaveOpen: true);
        var destination = new MemoryStream();
        Exception? error = Record.Exception(() =>
        {
            document.Metadata.Title = RevisedTitle;
            document.SaveIncremental(destination, leaveOpen: true);
        });
        if (entry.Revise != "ok")
        {
            Assert.True(error != null, id + ": the revision succeeded, but the manifest expects " + entry.Revise);
            Assert.Equal(entry.Revise, error!.GetType().Name);
            Assert.Equal(0, destination.Length);
            return;
        }

        Assert.Null(error);
        byte[] revised = destination.ToArray();
        CheckOutput(entry, original, revised, null, out JsonElement after);
        Assert.Equal(RevisedTitle, CorpusManifest.Text(after.GetProperty("info"), "title"));

        // Spec 7.3: no usage-rights transform grants a metadata write, so a Reader-extended form loses /UR3, unless it
        // already has a signature, where no write changes /Perms (W-90).
        bool signed = entry.Probe.GetProperty("signatures").GetArrayLength() > 0;
        Assert.Equal(signed && entry.Probe.GetProperty("usageRights").GetBoolean(), after.GetProperty("usageRights").GetBoolean());
        using var again = new MemoryStream(revised, writable: false);
        using PdfDocument reopened = PdfDocument.Open(again, entry.Password, leaveOpen: true);
        Assert.Equal(RevisedTitle, reopened.Metadata.Title);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void text_fill_is_accepted_by_independent_tools_or_refused_as_documented(string id)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        if (entry.Fill is not JsonElement fill)
        {
            return;
        }

        string field = CorpusManifest.Text(fill, "field")!;
        string value = CorpusManifest.Text(fill, "value")!;
        string result = CorpusManifest.Text(fill, "result")!;
        byte[] original = File.ReadAllBytes(entry.FullPath);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, entry.Password, leaveOpen: true);
        var destination = new MemoryStream();
        PdfAppearanceDisposition disposition = default;
        Exception? error = Record.Exception(() =>
        {
            disposition = document.AcroForm.Fill(field, value);
            document.SaveIncremental(destination, leaveOpen: true);
        });
        if (!Enum.TryParse(result, out PdfAppearanceDisposition expected))
        {
            Assert.True(error != null, id + ": the fill succeeded, but the manifest expects " + result);
            Assert.Equal(result, error!.GetType().Name);
            Assert.Equal(0, destination.Length);
            return;
        }

        Assert.Null(error);
        Assert.Equal(expected, disposition);
        CheckOutput(entry, original, destination.ToArray(), field, out JsonElement after);
        Assert.Equal(value, CorpusManifest.Text(after.GetProperty("fieldValues"), field));

        // Spec 7.3: every Reader-extended corpus form grants /Form FillIn, so a fill keeps /UR3 (UsageRightsTests),
        // except on a hybrid form, where the fill also removes /XFA, which FillIn does not grant (spec W4, D-48).
        Assert.Equal(KeepsUsageRightsAfterFill(entry), after.GetProperty("usageRights").GetBoolean());
        Assert.False(HasXfa(after), id + ": /XFA is still in the AcroForm");
    }

    // Spec R6 and W2: the export agrees with pypdf's reading of every terminal field, and one bulk fill that changes
    // every field a caller can fill is accepted by qpdf, pypdf, and pyHanko, in the same shape as the export.
    [Theory]
    [MemberData(nameof(Ids))]
    public void form_export_agrees_with_pypdf_and_a_bulk_fill_is_accepted_by_independent_tools(string id)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        if (entry.Open != "ok" || entry.Probe.GetProperty("widgets").GetInt32() == 0)
        {
            return;
        }

        byte[] original = File.ReadAllBytes(entry.FullPath);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, entry.Password, leaveOpen: true);
        IReadOnlyDictionary<string, PdfFieldValue> before = document.AcroForm.Export();
        FormValues.AssertAgree(before, FormValues.Pypdf(original, entry.Password), id + " before");
        Dictionary<string, PdfFieldValue> values = ChangedValues(document.AcroForm.Fields, before);
        if (values.Count == 0)
        {
            return;
        }

        var destination = new MemoryStream();
        document.AcroForm.Fill(values);
        document.SaveIncremental(destination, leaveOpen: true);

        byte[] revised = destination.ToArray();
        CheckOutput(entry, original, revised, null, out JsonElement after);
        Assert.Equal(KeepsUsageRightsAfterFill(entry), after.GetProperty("usageRights").GetBoolean());
        Assert.False(HasXfa(after), id + ": /XFA is still in the AcroForm");
        var expected = before.ToDictionary(item => item.Key, item => values.TryGetValue(item.Key, out PdfFieldValue value) ? value : item.Value);
        using var again = new MemoryStream(revised, writable: false);
        using PdfDocument reopened = PdfDocument.Open(again, entry.Password, leaveOpen: true);
        Assert.Equal(expected, reopened.AcroForm.Export());
        FormValues.AssertAgree(reopened.AcroForm.Export(), FormValues.Pypdf(revised, entry.Password), id + " after");
    }

    // The checks every modified corpus file gets: the original is a prefix, qpdf --check passes with no warnings,
    // and the probe sees the same pages, fields, and attachments, with every earlier signature still intact and valid.
    private static void CheckOutput(CorpusEntry entry, byte[] original, byte[] output, string? field, out JsonElement after)
    {
        Assert.True(output.Length > original.Length);
        Assert.True(output.AsSpan(0, original.Length).SequenceEqual(original), entry.Id + ": the original is not a prefix");
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "output.pdf");
            File.WriteAllBytes(path, output);
            var args = new List<string>();
            if (entry.Password != null)
            {
                args.Add("--password=" + entry.Password);
            }

            args.Add("--check");
            args.Add(path);
            using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), args.ToArray());
            (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
            Assert.True(exitCode == 0 && !(stdout + stderr).Contains("WARNING"), entry.Id + ": qpdf --check exited " + exitCode.ToString() + Environment.NewLine + stderr + stdout);

            string spec = entry.ProbeSpec(path, field);
            after = CorpusManifest.Probe(spec).GetProperty(spec);
            JsonElement before = entry.Probe;
            Assert.Null(CorpusManifest.Text(after, "openError"));
            Assert.Equal(before.GetProperty("pages").GetInt32(), after.GetProperty("pages").GetInt32());
            Assert.Equal(before.GetProperty("widgets").GetInt32(), after.GetProperty("widgets").GetInt32());
            Assert.Equal(CorpusManifest.Text(before, "fieldNamesSha256"), CorpusManifest.Text(after, "fieldNamesSha256"));
            Assert.Equal(CorpusManifest.Canonical(before.GetProperty("attachments")), CorpusManifest.Canonical(after.GetProperty("attachments")));
            Assert.Equal(before.GetProperty("revisions").GetInt32() + 1, after.GetProperty("revisions").GetInt32());
            List<JsonElement> signatures = after.GetProperty("signatures").EnumerateArray().ToList();
            foreach (JsonElement signature in before.GetProperty("signatures").EnumerateArray())
            {
                string name = CorpusManifest.Text(signature, "field")!;
                JsonElement match = signatures.Single(s => CorpusManifest.Text(s, "field") == name);
                Assert.True(match.GetProperty("intact").GetBoolean(), entry.Id + ": signature " + name + " is not intact after the write");
                Assert.True(match.GetProperty("valid").GetBoolean(), entry.Id + ": signature " + name + " is not valid after the write");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // An attachment is named by its name-tree key when the key is a text string, and otherwise by its file
    // specification (G2 finding Q1). qpdf renders a key that is not text with its undefined bytes, such as NUL.
    private static string ExpectedName(JsonElement attachment)
    {
        string key = CorpusManifest.Text(attachment, "name")!;
        return key.IndexOf('\0') >= 0 ? CorpusManifest.Text(attachment, "fileName")! : key;
    }

    // A different value for every field that can be filled: read-only fields, and fields the export leaves out, are skipped.
    private static Dictionary<string, PdfFieldValue> ChangedValues(IReadOnlyList<PdfField> fields, IReadOnlyDictionary<string, PdfFieldValue> export)
    {
        var values = new Dictionary<string, PdfFieldValue>(StringComparer.Ordinal);
        foreach (IGrouping<string, PdfField> group in fields.GroupBy(field => field.FullName, StringComparer.Ordinal))
        {
            PdfField first = group.First();
            if (first.ReadOnly || !export.TryGetValue(group.Key, out PdfFieldValue current))
            {
                continue;
            }

            if (first.FieldType == PdfFieldType.Text)
            {
                string text = current.Text == "W68" ? "W69" : "W68";
                values[group.Key] = first.MaxLength is int max ? text.Substring(0, Math.Min(max, text.Length)) : text;
            }
            else if (first.ButtonKind == PdfButtonKind.CheckBox)
            {
                values[group.Key] = current.Boolean != true;
            }
            else if (first.ButtonKind == PdfButtonKind.Radio)
            {
                string? state = group.Select(field => field.ExportValue).FirstOrDefault(name => name != null && name != current.Text);
                if (state != null)
                {
                    values[group.Key] = state;
                }
            }
            else if (first.Options.Count > 0)
            {
                string option = first.Options.Select(item => item.Value).LastOrDefault(item => item != current.Text) ?? first.Options[0].Value;
                values[group.Key] = first.MultiSelect ? new[] { option } : option;
            }
            else if (first.Editable)
            {
                values[group.Key] = current.Text == "W68" ? "W69" : "W68";
            }
        }

        return values;
    }

    // corpus_probe.py lists the "xfa" feature when pypdf finds /XFA in the AcroForm dictionary.
    private static bool HasXfa(JsonElement probe)
    {
        return probe.GetProperty("features").EnumerateArray().Any(feature => feature.GetString() == "xfa");
    }

    // Every corpus form that can be filled is unsigned or has no XFA, so a hybrid fill removes /UR3 (D-48).
    private static bool KeepsUsageRightsAfterFill(CorpusEntry entry)
    {
        return entry.Probe.GetProperty("usageRights").GetBoolean() && !HasXfa(entry.Probe);
    }

    private static string NamesHash(IEnumerable<string> names)
    {
        string joined = string.Join("\n", names.OrderBy(n => n, StringComparer.Ordinal));
        return CorpusManifest.Sha256(Encoding.UTF8.GetBytes(joined));
    }

    private sealed class ToleranceComparer : IEqualityComparer<double>
    {
        private readonly double _tolerance;

        public ToleranceComparer(double tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(double x, double y)
        {
            return Math.Abs(x - y) <= _tolerance;
        }

        public int GetHashCode(double obj)
        {
            return 0;
        }
    }
}
