using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec 7.3 and reset plan C4. A write keeps the catalog /Perms /UR3 usage-rights signature only when its UR3 transform
// parameters (ISO 32000-1 12.8.2.3, Table 255) grant that write: /Form FillIn for a fill, and /EF Create, Modify, or
// Delete for adding, replacing, or removing an attachment. Every other write, and every transform that is absent,
// unresolvable, malformed, unknown, or insufficient, removes /UR3 from /Perms in the same revision, and the write proceeds.
public class UsageRightsTests
{
    private const string FillIn = "Form/FillIn";
    private const string Create = "EF/Create";
    private const string Modify = "EF/Modify";
    private const string Delete = "EF/Delete";
    private const string AllRights = "<< /Type /TransformParams /V /2.2 /Document [/FullSave] /Form [/FillIn /Import /Export /SubmitStandalone] /EF [/Create /Delete /Modify /Import] /Signature [/Modify] /Annots [/Create /Delete /Modify] >>";
    private const string Indirect = " /Perms 9 0 R";

    private static readonly string[] EveryRight = { FillIn, Create, Modify, Delete };

    private static readonly Dictionary<string, PolicyCase> Cases = new[]
    {
        Case("permitted", Indirect, Signature("/Reference [" + Transform("UR3", AllRights) + "]"), EveryRight),
        Case(
            "permitted, every part indirect, /V omitted",
            Indirect,
            new[]
            {
                (9, "<< /UR3 10 0 R >>"),
                (10, SignatureBody("/Reference 11 0 R")),
                (11, "[12 0 R]"),
                (12, "<< /Type /SigRef /TransformMethod 15 0 R /TransformParams 13 0 R >>"),
                (13, "<< /Type /TransformParams /Form 14 0 R /EF [16 0 R /Delete /Modify] >>"),
                (14, "[/FillIn]"),
                (15, "/UR3"),
                (16, "/Create"),
            },
            EveryRight),
        Case("permitted, /Perms and /UR3 direct in the catalog", " /Perms << /UR3 " + SignatureBody("/Reference [" + Transform("UR3", AllRights) + "]") + " >>", Array.Empty<(int, string)>(), EveryRight),
        Case("permitted, fill only", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Form [/FillIn] >>") + "]"), FillIn),
        Case("permitted, attachment create only", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /EF [/Create] >>") + "]"), Create),
        Case("permitted, attachment modify only", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /EF [/Modify] >>") + "]"), Modify),
        Case("permitted, attachment delete only", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /EF [/Delete] >>") + "]"), Delete),
        Case("absent: /Perms without /UR3", Indirect, new[] { (9, "<< >>") }, hasUsageRights: false),
        Case("absent: no /Perms", string.Empty, Array.Empty<(int, string)>(), hasUsageRights: false),
        Case("absent transform: no /Reference", Indirect, Signature(string.Empty)),
        Case("absent transform: empty /Reference", Indirect, Signature("/Reference []")),
        Case("unresolvable /UR3", Indirect, new[] { (9, "<< /UR3 99 0 R >>") }),
        Case("unresolvable /UR3 generation", Indirect, new[] { (9, "<< /UR3 10 1 R >>"), (10, SignatureBody("/Reference [" + Transform("UR3", AllRights) + "]")) }),
        Case("unresolvable /Reference", Indirect, Signature("/Reference 99 0 R")),
        Case("unresolvable /Reference entry", Indirect, Signature("/Reference [99 0 R]")),
        Case("unresolvable /TransformParams", Indirect, Signature("/Reference [" + Transform("UR3", "99 0 R") + "]")),
        Case("unresolvable /V", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V 99 0 R /Form [/FillIn] /EF [/Create /Delete /Modify] >>") + "]")),
        Case("unresolvable right", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Form [99 0 R] /EF [99 0 R] >>") + "]")),
        Case("malformed /UR3", Indirect, new[] { (9, "<< /UR3 7 >>") }),
        Case("malformed /Reference", Indirect, Signature("/Reference " + Transform("UR3", AllRights))),
        Case("malformed /Reference entry", Indirect, Signature("/Reference [/UR3]")),
        Case("malformed /TransformParams", Indirect, Signature("/Reference [" + Transform("UR3", "[/Form /FillIn]") + "]")),
        Case("malformed rights", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Form /FillIn /EF (Create Delete Modify) >>") + "]")),
        Case("malformed right entry", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Form [/FillIn 1] /EF [/Create /Delete /Modify 1] >>") + "]")),
        Case("unknown /TransformMethod", Indirect, Signature("/Reference [" + Transform("UR", AllRights) + "]")),
        Case("unknown /V", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.3 /Form [/FillIn] /EF [/Create /Delete /Modify] >>") + "]")),
        Case("unknown /V type", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V 2.2 /Form [/FillIn] /EF [/Create /Delete /Modify] >>") + "]")),
        Case("unknown second transform", Indirect, Signature("/Reference [" + Transform("UR3", AllRights) + " " + Transform("Identity", "<< >>") + "]")),
        Case("insufficient rights", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Document [/FullSave] /Form [/Import /Export /SubmitStandalone] /EF [/Import] /Signature [/Modify] /Annots [/Create] >>") + "]")),
        Case("insufficient: no rights categories", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Document [/FullSave] >>") + "]")),
    }.ToDictionary(item => item.Name);

    private static readonly Dictionary<string, PolicyWrite> Writes = new[]
    {
        Write("fill text", FillIn, document => document.AcroForm.SetText("Name", "Ada")),
        Write("fill check box", FillIn, document => document.AcroForm.SetCheckBox("Agree", true)),
        Write("fill radio, generated", FillIn, document => document.AcroForm.SetRadio("Pick", "One")),
        Write("fill radio, existing state", FillIn, document => document.AcroForm.SetRadio("Size", "Small")),
        Write("fill choice", FillIn, document => document.AcroForm.SetChoice("Color", new[] { "Red" })),
        Write("metadata title", null, document => document.Metadata.Title = "Usage rights"),
        Write("metadata date", null, document => document.Metadata.CreationDate = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero)),
        Write("metadata trapped", null, document => document.Metadata["Trapped"] = "True"),
        Write("metadata XMP", null, document => document.Metadata.SetXmp(Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>"))),
        Write("attachment add", Create, document => document.Attachments.Add("new.txt", new MemoryStream(Encoding.ASCII.GetBytes("new")))),
        Write("attachment replace", Modify, document => document.Attachments.Add("old.txt", new MemoryStream(Encoding.ASCII.GetBytes("replaced")))),
        Write("attachment remove", Delete, document => document.Attachments.Remove("old.txt")),
        new PolicyWrite("sign", null, (document, destination) => document.Sign(destination, new PdfSignatureOptions { ReservedContentsBytes = 64 }, new FixedProvider(), leaveOpen: true)),
    }.ToDictionary(item => item.Name);

    public static IEnumerable<object[]> Matrix => Writes.Keys.SelectMany(write => Cases.Keys.Select(policy => new object[] { write, policy }));

    public static IEnumerable<object[]> PolicyNames => Cases.Keys.Select(policy => new object[] { policy });

    // The Reader-extended corpus forms (G2) and whether their UR3 transform grants /EF Create, Delete, and Modify, as
    // pypdf 6.21.1 reads them. CMS-40B grants form, annotation, and signature rights but no /EF rights. All grant /Form FillIn.
    public static IEnumerable<object[]> ReaderExtended => new[]
    {
        new object[] { "cms-40b", false },
        new object[] { "cms-40b-pyhanko-approved", false },
        new object[] { "grants-sf424-v4", true },
        new object[] { "irs-f1040es-2026", true },
        new object[] { "irs-fw9-2024-03", true },
        new object[] { "va-21-4138", true },
    };

    public static IEnumerable<object[]> ReaderExtendedIds => ReaderExtended.Select(row => new[] { row[0] });

    [Theory]
    [MemberData(nameof(Matrix))]
    public void write_keeps_usage_rights_only_when_the_ur3_transform_grants_it(string writeName, string policyName)
    {
        PolicyWrite write = Writes[writeName];
        PolicyCase policy = Cases[policyName];
        byte[] original = Document(policy);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);
        Assert.Equal(policy.HasUsageRights, document.HasUsageRights);

        var destination = new MemoryStream();
        write.Run(document, destination);

        bool kept = policy.HasUsageRights && write.Right != null && policy.Grants.Contains(write.Right);
        byte[] output = destination.ToArray();
        Assert.True(output.Length > original.Length);
        Assert.True(output.AsSpan(0, original.Length).SequenceEqual(original), "the original is not a prefix");
        string appendix = Encoding.ASCII.GetString(output, original.Length, output.Length - original.Length);
        Assert.Equal(1, Count(appendix, "%%EOF"));
        if (!policy.HasUsageRights || (kept && policy.Perms == Indirect))
        {
            Assert.DoesNotContain("\n9 0 obj", appendix, StringComparison.Ordinal);
        }

        using var reopened = PdfDocument.Open(new MemoryStream(output, writable: false));
        Assert.Equal(kept, reopened.HasUsageRights);
    }

    [Theory]
    [MemberData(nameof(PolicyNames))]
    public void fill_and_metadata_write_in_one_revision_remove_usage_rights(string policyName)
    {
        PolicyCase policy = Cases[policyName];
        byte[] original = Document(policy);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);
        document.AcroForm.SetText("Name", "Ada");
        Assert.Equal(policy.HasUsageRights && policy.Grants.Contains(FillIn), document.HasUsageRights);
        document.Metadata.Title = "Usage rights";
        document.AcroForm.SetText("Name", "Grace");
        Assert.False(document.HasUsageRights);

        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);
        byte[] output = destination.ToArray();
        Assert.Equal(1, Count(Encoding.ASCII.GetString(output, original.Length, output.Length - original.Length), "%%EOF"));
        using var reopened = PdfDocument.Open(new MemoryStream(output, writable: false));
        Assert.False(reopened.HasUsageRights);
        Assert.Equal("Grace", reopened.AcroForm.GetFields("Name")[0].Value);
        Assert.Equal("Usage rights", reopened.Metadata.Title);
    }

    [Fact]
    public void refused_writes_keep_usage_rights_and_do_not_dirty_the_document()
    {
        PolicyCase policy = Cases["insufficient: no rights categories"];
        byte[] original = Document(policy);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);

        Assert.Throws<PdfFieldNotFoundException>(() => document.AcroForm.SetText("Missing", "Ada"));
        Assert.Throws<PdfFieldValueException>(() => document.AcroForm.SetCheckBox("Name", true));
        Assert.Throws<PdfFieldValueException>(() => document.AcroForm.SetRadio("Size", "Large"));
        Assert.Throws<PdfFieldValueException>(() => document.AcroForm.SetChoice("Color", new[] { "Green" }));
        Assert.Throws<ArgumentException>(() => document.Metadata["Trapped"] = "Maybe");
        Assert.Throws<PdfAttachmentNotFoundException>(() => document.Attachments.Remove("missing.txt"));
        Assert.True(document.HasUsageRights);

        var failed = new MemoryStream();
        Assert.ThrowsAny<Exception>(() => document.Sign(failed, new PdfSignatureOptions { ReservedContentsBytes = 64 }, new FixedProvider { Fail = true }, leaveOpen: true));
        Assert.Equal(0, failed.Length);
        Assert.True(document.HasUsageRights);

        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);
        Assert.Equal(original, destination.ToArray());
    }

    [Fact]
    public void certified_document_applies_the_policy_after_certification_and_keeps_docmdp()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >>" + Indirect + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 240 720] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");
        builder.Indirect(9, "<< /DocMDP 11 0 R /UR3 10 0 R >>");
        builder.Indirect(10, SignatureBody("/Reference [" + Transform("UR3", "<< /V /2.2 /Form [/FillIn] /EF [/Create] >>") + "]"));
        builder.Indirect(11, "<< /Type /Sig /Reference [<< /TransformMethod /DocMDP /TransformParams << /P 2 /V /1.2 >> >>] >>");
        byte[] original = builder.Finish();

        // DocMDP P 2 refuses metadata and attachment writes before the policy runs, so /UR3 stays.
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);
        Assert.Throws<PdfCertificationLockedException>(() => document.Metadata.Title = "Nope");
        Assert.Throws<PdfCertificationLockedException>(() => document.Attachments.Add("new.txt", new MemoryStream(new byte[] { 1 })));
        Assert.True(document.HasUsageRights);

        // P 2 allows a fill, which /Form FillIn keeps, and a signature, which removes /UR3 and keeps /DocMDP.
        document.AcroForm.SetText("Name", "Ada");
        Assert.True(document.HasUsageRights);
        var destination = new MemoryStream();
        document.Sign(destination, new PdfSignatureOptions { ReservedContentsBytes = 64 }, new FixedProvider(), leaveOpen: true);

        using var reopened = PdfDocument.Open(new MemoryStream(destination.ToArray(), writable: false));
        Assert.False(reopened.HasUsageRights);
        Assert.Equal("Ada", reopened.AcroForm.GetFields("Name")[0].Value);
        Assert.Throws<PdfCertificationLockedException>(() => reopened.Metadata.Title = "Still certified");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void encrypted_document_applies_the_policy_after_the_permission_bits(bool revision6)
    {
        var crypt = new StandardEncryptor(revision6, "user", "owner", StandardEncryptor.FillOnly(), encryptMetadata: true);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> /Perms 9 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T " + crypt.Text("Name", 4) + " /Rect [72 700 240 720] /DA " + crypt.Text("/Helv 0 Tf 0 g", 4) + " /P 3 0 R >>");
        builder.Indirect(5, "<< /Title " + crypt.Text("Secret", 5) + " >>");
        builder.Indirect(9, "<< /UR3 10 0 R >>");
        builder.Indirect(10, SignatureBody("/Reference [" + Transform("UR3", "<< /V /2.2 /Form [/FillIn] >>") + "]"));
        builder.Indirect(20, crypt.Dictionary(null));
        byte[] original = builder.Finish("/Info 5 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");

        // The user password may fill but not modify: the refused metadata write keeps /UR3, and the granted fill keeps it.
        var filled = new MemoryStream();
        using (var user = PdfDocument.Open(new MemoryStream(original, writable: false), "user"))
        {
            Assert.Throws<PdfEncryptionRestrictedException>(() => user.Metadata.Title = "Nope");
            Assert.True(user.HasUsageRights);
            user.AcroForm.SetText("Name", "Ada");
            user.SaveIncremental(filled, leaveOpen: true);
        }

        // The owner password may modify: the metadata write removes /UR3 in the same revision.
        var revised = new MemoryStream();
        using (var owner = PdfDocument.Open(new MemoryStream(original, writable: false), "owner"))
        {
            owner.Metadata.Title = "Usage rights";
            owner.SaveIncremental(revised, leaveOpen: true);
        }

        using (var reopened = PdfDocument.Open(new MemoryStream(filled.ToArray(), writable: false), "user"))
        {
            Assert.True(reopened.HasUsageRights);
            Assert.Equal("Ada", reopened.AcroForm.GetFields("Name")[0].Value);
        }

        using (var reopened = PdfDocument.Open(new MemoryStream(revised.ToArray(), writable: false), "user"))
        {
            Assert.False(reopened.HasUsageRights);
            Assert.Equal("Usage rights", reopened.Metadata.Title);
        }

        // qpdf and pypdf, through the corpus probe, read both outputs with the password.
        string directory = ExternalTool.TempDirectory();
        try
        {
            string fillPath = Path.Combine(directory, "filled.pdf");
            string revisedPath = Path.Combine(directory, "revised.pdf");
            File.WriteAllBytes(fillPath, filled.ToArray());
            File.WriteAllBytes(revisedPath, revised.ToArray());
            AssertQpdfCheck(fillPath, "user");
            AssertQpdfCheck(revisedPath, "user");
            JsonElement probe = CorpusManifest.Probe(fillPath + "::user", revisedPath + "::user");
            Assert.True(probe.GetProperty(fillPath + "::user").GetProperty("usageRights").GetBoolean());
            JsonElement after = probe.GetProperty(revisedPath + "::user");
            Assert.False(after.GetProperty("usageRights").GetBoolean());
            Assert.Equal("Usage rights", CorpusManifest.Text(after.GetProperty("info"), "title"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(ReaderExtended))]
    public void reader_extended_form_keeps_usage_rights_on_attachment_add_only_when_granted(string id, bool grantsAttachmentEdits)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        Assert.True(entry.Probe.GetProperty("usageRights").GetBoolean());
        byte[] original = File.ReadAllBytes(entry.FullPath);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, entry.Password, leaveOpen: true);
        // The IRS 1040-ES keys its attachment "Press Quality(1).joboptions" in UTF-16BE. This name sorts after it as text,
        // so qpdf --check passes only when the new key is UTF-16BE too (W-86).
        document.Attachments.Add("usage-rights.txt", new MemoryStream(Encoding.ASCII.GetBytes("usage rights")));
        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);

        JsonElement after = CheckOutput(entry, original, destination.ToArray());
        Assert.Equal(grantsAttachmentEdits, after.GetProperty("usageRights").GetBoolean());
        using var reopened = PdfDocument.Open(new MemoryStream(destination.ToArray(), writable: false), entry.Password);
        Assert.Equal(grantsAttachmentEdits, reopened.HasUsageRights);
        Assert.Contains(reopened.Attachments, attachment => attachment.Name == "usage-rights.txt");
    }

    [Theory]
    [MemberData(nameof(ReaderExtendedIds))]
    public void reader_extended_form_loses_usage_rights_when_signed(string id)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        byte[] original = File.ReadAllBytes(entry.FullPath);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, entry.Password, leaveOpen: true);
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Usage Rights", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var destination = new MemoryStream();
        document.Sign(destination, new PdfSignatureOptions(), new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData)), leaveOpen: true);

        JsonElement after = CheckOutput(entry, original, destination.ToArray());
        Assert.False(after.GetProperty("usageRights").GetBoolean());
        List<JsonElement> signatures = after.GetProperty("signatures").EnumerateArray().ToList();
        Assert.Equal(entry.Probe.GetProperty("signatures").GetArrayLength() + 1, signatures.Count);
        JsonElement signed = signatures.Single(s => CorpusManifest.Text(s, "coverage") == "ENTIRE_FILE");
        Assert.True(signed.GetProperty("intact").GetBoolean());
        Assert.True(signed.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public void irs_1040es_keeps_usage_rights_when_its_attachment_is_replaced_or_removed()
    {
        CorpusEntry entry = CorpusManifest.Get("irs-f1040es-2026");
        const string name = "Press Quality(1).joboptions";
        byte[] original = File.ReadAllBytes(entry.FullPath);

        var replaced = new MemoryStream();
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(original, writable: false), entry.Password))
        {
            document.Attachments.Add(name, new MemoryStream(Encoding.ASCII.GetBytes("replaced")));
            Assert.True(document.HasUsageRights);
            document.SaveIncremental(replaced, leaveOpen: true);
        }

        var removed = new MemoryStream();
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(original, writable: false), entry.Password))
        {
            document.Attachments.Remove(name);
            Assert.True(document.HasUsageRights);
            document.SaveIncremental(removed, leaveOpen: true);
        }

        Assert.True(CheckOutput(entry, original, replaced.ToArray()).GetProperty("usageRights").GetBoolean());
        JsonElement afterRemove = CheckOutput(entry, original, removed.ToArray());
        Assert.True(afterRemove.GetProperty("usageRights").GetBoolean());
        Assert.Equal(0, afterRemove.GetProperty("attachments").GetArrayLength());
    }

    // The original is a prefix, qpdf --check passes with no warnings, and pypdf and pyHanko (through the corpus probe)
    // read one more revision with every earlier signature still intact and valid.
    private static JsonElement CheckOutput(CorpusEntry entry, byte[] original, byte[] output)
    {
        Assert.True(output.Length > original.Length);
        Assert.True(output.AsSpan(0, original.Length).SequenceEqual(original), entry.Id + ": the original is not a prefix");
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "output.pdf");
            File.WriteAllBytes(path, output);
            AssertQpdfCheck(path, entry.Password);
            string spec = entry.ProbeSpec(path);
            JsonElement after = CorpusManifest.Probe(spec).GetProperty(spec);
            Assert.Null(CorpusManifest.Text(after, "openError"));
            Assert.Equal(entry.Probe.GetProperty("revisions").GetInt32() + 1, after.GetProperty("revisions").GetInt32());
            List<JsonElement> signatures = after.GetProperty("signatures").EnumerateArray().ToList();
            foreach (JsonElement signature in entry.Probe.GetProperty("signatures").EnumerateArray())
            {
                string field = CorpusManifest.Text(signature, "field")!;
                JsonElement match = signatures.Single(s => CorpusManifest.Text(s, "field") == field);
                Assert.True(match.GetProperty("intact").GetBoolean(), entry.Id + ": signature " + field + " is not intact after the write");
                Assert.True(match.GetProperty("valid").GetBoolean(), entry.Id + ": signature " + field + " is not valid after the write");
            }

            return after;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertQpdfCheck(string path, string? password)
    {
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

    private static byte[] Document(PolicyCase policy)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R 7 0 R 8 0 R] >> /Names << /EmbeddedFiles << /Names [(old.txt) 20 0 R] >> >>" + policy.Perms + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R 6 0 R 7 0 R 8 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 240 720] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Btn /T (Agree) /Rect [72 660 90 678] /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Btn /Ff 49152 /T (Pick) /Rect [72 620 90 638] /P 3 0 R >>");
        builder.Indirect(7, "<< /Subtype /Widget /FT /Ch /T (Color) /Opt [(Red) (Blue)] /Rect [72 580 240 600] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");
        builder.Indirect(8, "<< /Subtype /Widget /FT /Btn /Ff 49152 /T (Size) /AP << /N << /Small null /Off null >> >> /Rect [72 540 90 558] /P 3 0 R >>");
        foreach ((int id, string body) in policy.Objects)
        {
            builder.Indirect(id, body);
        }

        builder.Indirect(20, "<< /Type /Filespec /F (old.txt) /UF (old.txt) /EF << /F 21 0 R >> >>");
        builder.StreamObject(21, "/Type /EmbeddedFile", Encoding.ASCII.GetBytes("old"));
        return builder.Finish();
    }

    private static PolicyCase Case(string name, string perms, (int Id, string Body)[] objects, params string[] grants)
    {
        return new PolicyCase(name, perms, objects, grants, hasUsageRights: true);
    }

    private static PolicyCase Case(string name, string perms, (int Id, string Body)[] objects, bool hasUsageRights)
    {
        return new PolicyCase(name, perms, objects, Array.Empty<string>(), hasUsageRights);
    }

    private static PolicyWrite Write(string name, string? right, Action<PdfDocument> edit)
    {
        return new PolicyWrite(name, right, (document, destination) =>
        {
            edit(document);
            document.SaveIncremental(destination, leaveOpen: true);
        });
    }

    private static (int Id, string Body)[] Signature(string reference)
    {
        return new[] { (9, "<< /UR3 10 0 R >>"), (10, SignatureBody(reference)) };
    }

    // A usage-rights signature as Adobe's Reader Extensions write it. The library does not validate it (spec 7.3).
    private static string SignatureBody(string reference)
    {
        return "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /Name (ARE Test) /ByteRange [0 0 0 0] /Contents <00> " + reference + " >>";
    }

    private static string Transform(string method, string parameters)
    {
        return "<< /Type /SigRef /TransformMethod /" + method + " /TransformParams " + parameters + " >>";
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private sealed class PolicyCase
    {
        public PolicyCase(string name, string perms, (int Id, string Body)[] objects, string[] grants, bool hasUsageRights)
        {
            Name = name;
            Perms = perms;
            Objects = objects;
            Grants = grants;
            HasUsageRights = hasUsageRights;
        }

        public string Name { get; }

        public string Perms { get; }

        public (int Id, string Body)[] Objects { get; }

        public string[] Grants { get; }

        public bool HasUsageRights { get; }
    }

    private sealed class PolicyWrite
    {
        public PolicyWrite(string name, string? right, Action<PdfDocument, Stream> run)
        {
            Name = name;
            Right = right;
            Run = run;
        }

        public string Name { get; }

        public string? Right { get; }

        public Action<PdfDocument, Stream> Run { get; }
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public bool Fail { get; set; }

        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public byte[] CreateContents(PdfSignatureContext context)
        {
            if (Fail)
            {
                throw new InvalidOperationException("The test provider failed.");
            }

            return new byte[] { 1, 2, 3, 4 };
        }
    }

    private sealed class RsaSigner : IPdfExternalSigner
    {
        private readonly RSA _rsa;

        public RsaSigner(RSA rsa, byte[] certificate)
        {
            _rsa = rsa;
            CertificateDer = certificate;
            AdditionalCertificates = Array.Empty<byte[]>();
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates { get; }

        public byte[] SignHash(ReadOnlySpan<byte> hash)
        {
            return _rsa.SignHash(hash.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }
}
