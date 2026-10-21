using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec 7.3 and reset plan C4. A write keeps the catalog /Perms /UR3 usage-rights signature only when its UR3 transform
// parameters (ISO 32000-1 12.8.2.3, Table 255) grant that write: /Form FillIn for a fill, /EF Create, Modify, or
// Delete for adding, replacing, or removing an attachment, and /Signature Modify for a signature into an existing empty
// signature field. Every other write, including a signature that creates a field and a document timestamp, and every
// transform that is absent, unresolvable, malformed, unknown, or insufficient, removes /UR3 from /Perms in the same
// revision, and the write proceeds. On a document that already has a signature (approval, certification, or document
// timestamp), no write changes /Perms, whatever the transform grants (W-90).
public class UsageRightsTests
{
    private const string Unsigned = "unsigned";
    private const string FillIn = "Form/FillIn";
    private const string Create = "EF/Create";
    private const string Modify = "EF/Modify";
    private const string Delete = "EF/Delete";
    private const string SignField = "Signature/Modify";
    private const string AllRights = "<< /Type /TransformParams /V /2.2 /Document [/FullSave] /Form [/FillIn /Import /Export /SubmitStandalone] /EF [/Create /Delete /Modify /Import] /Signature [/Modify] /Annots [/Create /Delete /Modify] >>";
    private const string Indirect = " /Perms 9 0 R";

    private static readonly string[] EveryRight = { FillIn, Create, Modify, Delete, SignField };

    // How the field Signed of the test document is signed. Certification also puts /DocMDP in /Perms.
    private static readonly string[] SignedKinds = { "approval", "certified P 2", "certified P 3", "document timestamp" };

    // The W-88 Reader-extended forms: a UR3 transform that grants only fills, one that also grants signing an
    // existing field, and a /UR3 with no /Reference.
    private static readonly Dictionary<string, string> BuiltTransforms = new Dictionary<string, string>
    {
        ["fill"] = "/Reference [" + Transform("UR3", "<< /V /2.2 /Form [/FillIn] >>") + "]",
        ["sign"] = "/Reference [" + Transform("UR3", "<< /V /2.2 /Form [/FillIn] /Signature [/Modify] >>") + "]",
        ["noref"] = string.Empty,
    };

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
                (13, "<< /Type /TransformParams /Form 14 0 R /EF [16 0 R /Delete /Modify] /Signature 17 0 R >>"),
                (14, "[/FillIn]"),
                (15, "/UR3"),
                (16, "/Create"),
                (17, "[18 0 R]"),
                (18, "/Modify"),
            },
            EveryRight),
        Case("permitted, /Perms and /UR3 direct in the catalog", " /Perms << /UR3 " + SignatureBody("/Reference [" + Transform("UR3", AllRights) + "]") + " >>", Array.Empty<(int, string)>(), EveryRight),
        Case("permitted, fill only", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Form [/FillIn] >>") + "]"), FillIn),
        Case("permitted, attachment create only", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /EF [/Create] >>") + "]"), Create),
        Case("permitted, attachment modify only", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /EF [/Modify] >>") + "]"), Modify),
        Case("permitted, attachment delete only", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /EF [/Delete] >>") + "]"), Delete),
        Case("permitted, signature modify only", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Signature [/Modify] >>") + "]"), SignField),
        Case("absent: /Perms without /UR3", Indirect, new[] { (9, "<< >>") }, hasUsageRights: false),
        Case("absent: no /Perms", string.Empty, Array.Empty<(int, string)>(), hasUsageRights: false),
        Case("absent transform: no /Reference", Indirect, Signature(string.Empty)),
        Case("absent transform: empty /Reference", Indirect, Signature("/Reference []")),
        Case("unresolvable /UR3", Indirect, new[] { (9, "<< /UR3 99 0 R >>") }),
        Case("unresolvable /UR3 generation", Indirect, new[] { (9, "<< /UR3 10 1 R >>"), (10, SignatureBody("/Reference [" + Transform("UR3", AllRights) + "]")) }),
        Case("unresolvable /Reference", Indirect, Signature("/Reference 99 0 R")),
        Case("unresolvable /Reference entry", Indirect, Signature("/Reference [99 0 R]")),
        Case("unresolvable /TransformParams", Indirect, Signature("/Reference [" + Transform("UR3", "99 0 R") + "]")),
        Case("unresolvable /V", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V 99 0 R /Form [/FillIn] /EF [/Create /Delete /Modify] /Signature [/Modify] >>") + "]")),
        Case("unresolvable right", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Form [99 0 R] /EF [99 0 R] /Signature [99 0 R] >>") + "]")),
        Case("malformed /UR3", Indirect, new[] { (9, "<< /UR3 7 >>") }),
        Case("malformed /Reference", Indirect, Signature("/Reference " + Transform("UR3", AllRights))),
        Case("malformed /Reference entry", Indirect, Signature("/Reference [/UR3]")),
        Case("malformed /TransformParams", Indirect, Signature("/Reference [" + Transform("UR3", "[/Form /FillIn]") + "]")),
        Case("malformed rights", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Form /FillIn /EF (Create Delete Modify) /Signature /Modify >>") + "]")),
        Case("malformed right entry", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Form [/FillIn 1] /EF [/Create /Delete /Modify 1] /Signature [/Modify 1] >>") + "]")),
        Case("unknown /TransformMethod", Indirect, Signature("/Reference [" + Transform("UR", AllRights) + "]")),
        Case("unknown /V", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.3 /Form [/FillIn] /EF [/Create /Delete /Modify] /Signature [/Modify] >>") + "]")),
        Case("unknown /V type", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V 2.2 /Form [/FillIn] /EF [/Create /Delete /Modify] /Signature [/Modify] >>") + "]")),
        Case("unknown second transform", Indirect, Signature("/Reference [" + Transform("UR3", AllRights) + " " + Transform("Identity", "<< >>") + "]")),
        Case("insufficient rights", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Document [/FullSave] /Form [/Import /Export /SubmitStandalone /Add] /EF [/Import] /Signature [] /Annots [/Create /Modify] >>") + "]")),
        Case("insufficient: no rights categories", Indirect, Signature("/Reference [" + Transform("UR3", "<< /V /2.2 /Document [/FullSave] >>") + "]")),
    }.ToDictionary(item => item.Name);

    private static readonly Dictionary<string, PolicyWrite> Writes = new[]
    {
        Write("fill text", FillIn, document => document.AcroForm.Fill("Name", "Ada")),
        Write("fill check box", FillIn, document => document.AcroForm.Fill("Agree", true)),
        Write("fill radio, generated", FillIn, document => document.AcroForm.Fill("Pick", "One")),
        Write("fill radio, existing state", FillIn, document => document.AcroForm.Fill("Size", "Small")),
        Write("fill choice", FillIn, document => document.AcroForm.Fill("Color", new[] { "Red" })),
        Write("bulk fill", FillIn, document => document.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["Agree"] = true, ["Pick"] = "One", ["Size"] = "Small", ["Color"] = new[] { "Red" } })),
        Write("metadata title", null, document => document.Metadata.Title = "Usage rights"),
        Write("metadata date", null, document => document.Metadata.CreationDate = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero)),
        Write("metadata trapped", null, document => document.Metadata["Trapped"] = "True"),
        Write("metadata XMP", null, document => document.Metadata.SetXmp(Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>"))),
        Write("attachment add", Create, document => document.Attachments.Add("new.txt", new MemoryStream(Encoding.ASCII.GetBytes("new")))),
        Write("attachment replace", Modify, document => document.Attachments.Add("old.txt", new MemoryStream(Encoding.ASCII.GetBytes("replaced")))),
        Write("attachment remove", Delete, document => document.Attachments.Remove("old.txt")),
        Sign("sign, existing empty field", SignField, PdfSignatureKind.CmsDetached, "Signer"),
        Sign("sign, new field", null, PdfSignatureKind.CmsDetached, null),
        Sign("sign, new named field", null, PdfSignatureKind.CmsDetached, "Fresh"),
        Sign("document timestamp, existing empty field", null, PdfSignatureKind.DocTimeStamp, "Signer"),
        Sign("document timestamp, new field", null, PdfSignatureKind.DocTimeStamp, null),
    }.ToDictionary(item => item.Name);

    public static IEnumerable<object[]> Matrix => Writes.Keys.SelectMany(write => Cases.Keys.Select(policy => new object[] { write, policy }));

    public static IEnumerable<object[]> SignedMatrix => SignedKinds.SelectMany(signedBy => Matrix.Select(row => new object[] { signedBy, row[0], row[1] }));

    public static IEnumerable<object[]> PolicyNamesUnsignedAndSigned => new[] { Unsigned, "approval", "document timestamp" }.SelectMany(signedBy => Cases.Keys.Select(policy => new object[] { signedBy, policy }));

    public static IEnumerable<object[]> PyHankoModes => new[] { new object[] { "certify-p2" }, new object[] { "certify-p3" }, new object[] { "approve" } };

    // The Reader-extended corpus forms (G2) and whether their UR3 transform grants /EF Create, Delete, and Modify, as
    // pypdf 6.21.1 reads them. CMS-40B grants form, annotation, and signature rights but no /EF rights. All grant /Form FillIn.
    // Only cms-40b-pyhanko-approved already has a signature, so no write removes its /UR3 (W-90).
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
        byte[] original = Document(policy, Unsigned);
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

    // W-90: once the document has a signature, /Perms is the same in the new revision whatever the transform grants.
    // DocMDP P 2 and P 3 still refuse metadata and attachment writes before the policy runs.
    [Theory]
    [MemberData(nameof(SignedMatrix))]
    public void write_on_a_signed_document_leaves_perms_unchanged_whatever_the_transform_grants(string signedBy, string writeName, string policyName)
    {
        PolicyWrite write = Writes[writeName];
        PolicyCase policy = Cases[policyName];
        byte[] original = Document(policy, signedBy);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);
        Assert.Equal(policy.HasUsageRights, document.HasUsageRights);

        var destination = new MemoryStream();
        if (signedBy.StartsWith("certified", StringComparison.Ordinal) && !write.CertificationAllows)
        {
            Assert.Throws<PdfCertificationLockedException>(() => write.Run(document, destination));
            Assert.Equal(0, destination.Length);
            Assert.Equal(policy.HasUsageRights, document.HasUsageRights);
            return;
        }

        write.Run(document, destination);
        byte[] output = destination.ToArray();
        Assert.True(output.Length > original.Length);
        Assert.True(output.AsSpan(0, original.Length).SequenceEqual(original), "the original is not a prefix");
        string appendix = Encoding.ASCII.GetString(output, original.Length, output.Length - original.Length);
        Assert.Equal(1, Count(appendix, "%%EOF"));
        foreach ((int id, string _) in policy.Objects)
        {
            Assert.DoesNotContain("\n" + id.ToString(CultureInfo.InvariantCulture) + " 0 obj", appendix, StringComparison.Ordinal);
        }

        Assert.Equal(PermsValue(Encoding.ASCII.GetString(original)), PermsValue(Encoding.ASCII.GetString(output)));
        using var reopened = PdfDocument.Open(new MemoryStream(output, writable: false));
        Assert.Equal(policy.HasUsageRights, reopened.HasUsageRights);
    }

    [Theory]
    [MemberData(nameof(PolicyNamesUnsignedAndSigned))]
    public void fill_and_metadata_write_in_one_revision_remove_usage_rights_unless_signed(string signedBy, string policyName)
    {
        PolicyCase policy = Cases[policyName];
        bool signed = signedBy != Unsigned;
        byte[] original = Document(policy, signedBy);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);
        document.AcroForm.Fill("Name", "Ada");
        Assert.Equal(policy.HasUsageRights && (signed || policy.Grants.Contains(FillIn)), document.HasUsageRights);
        document.Metadata.Title = "Usage rights";
        document.AcroForm.Fill("Name", "Grace");
        Assert.Equal(policy.HasUsageRights && signed, document.HasUsageRights);

        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);
        byte[] output = destination.ToArray();
        Assert.Equal(1, Count(Encoding.ASCII.GetString(output, original.Length, output.Length - original.Length), "%%EOF"));
        using var reopened = PdfDocument.Open(new MemoryStream(output, writable: false));
        Assert.Equal(policy.HasUsageRights && signed, reopened.HasUsageRights);
        Assert.Equal("Grace", reopened.AcroForm.GetFields("Name")[0].Value);
        Assert.Equal("Usage rights", reopened.Metadata.Title);
    }

    [Theory]
    [InlineData(Unsigned)]
    [InlineData("approval")]
    public void refused_writes_keep_usage_rights_and_do_not_dirty_the_document(string signedBy)
    {
        PolicyCase policy = Cases["insufficient: no rights categories"];
        byte[] original = Document(policy, signedBy);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);

        Assert.Throws<PdfFieldNotFoundException>(() => document.AcroForm.Fill("Missing", "Ada"));
        Assert.Throws<PdfFieldValueException>(() => document.AcroForm.Fill("Name", true));
        Assert.Throws<PdfFieldValueException>(() => document.AcroForm.Fill("Size", "Large"));
        Assert.Throws<PdfFieldValueException>(() => document.AcroForm.Fill("Color", new[] { "Green" }));
        Assert.Throws<PdfFieldValueException>(() => document.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["Size"] = "Large" }));
        Assert.Throws<ArgumentException>(() => document.Metadata["Trapped"] = "Maybe");
        Assert.Throws<PdfAttachmentNotFoundException>(() => document.Attachments.Remove("missing.txt"));
        Assert.True(document.HasUsageRights);

        // A sign refused by the existing-field checks, or failed by the provider, in a new or an existing field. The
        // field Signed has a signature dictionary only on a signed document.
        var failed = new MemoryStream();
        Assert.Throws<PdfSignException>(() => document.Sign(failed, new PdfSignatureOptions { FieldName = "Name", ReservedContentsBytes = 64 }, new FixedProvider(), leaveOpen: true));
        Assert.Throws<PdfFieldReadOnlyException>(() => document.Sign(failed, new PdfSignatureOptions { FieldName = "Locked", ReservedContentsBytes = 64 }, new FixedProvider(), leaveOpen: true));
        if (signedBy != Unsigned)
        {
            Assert.Throws<PdfSignException>(() => document.Sign(failed, new PdfSignatureOptions { FieldName = "Signed", ReservedContentsBytes = 64 }, new FixedProvider(), leaveOpen: true));
        }

        Assert.True(document.HasUsageRights);
        Assert.ThrowsAny<Exception>(() => document.Sign(failed, new PdfSignatureOptions { ReservedContentsBytes = 64 }, new FixedProvider { Fail = true }, leaveOpen: true));
        Assert.True(document.HasUsageRights);
        Assert.ThrowsAny<Exception>(() => document.Sign(failed, new PdfSignatureOptions { FieldName = "Signer", ReservedContentsBytes = 64 }, new FixedProvider { Fail = true }, leaveOpen: true));
        Assert.Equal(0, failed.Length);
        Assert.True(document.HasUsageRights);

        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);
        Assert.Equal(original, destination.ToArray());
    }

    // A catalog /Perms /DocMDP alone makes the document signed, even when no field holds the certification.
    [Fact]
    public void certified_document_keeps_usage_rights_and_docmdp()
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

        // P 2 allows a fill and a signature into a new field, which no right grants; neither changes /Perms.
        document.AcroForm.Fill("Name", "Ada");
        Assert.True(document.HasUsageRights);
        var destination = new MemoryStream();
        document.Sign(destination, new PdfSignatureOptions { ReservedContentsBytes = 64 }, new FixedProvider(), leaveOpen: true);

        byte[] output = destination.ToArray();
        Assert.DoesNotContain("\n9 0 obj", Encoding.ASCII.GetString(output, original.Length, output.Length - original.Length), StringComparison.Ordinal);
        using var reopened = PdfDocument.Open(new MemoryStream(output, writable: false));
        Assert.True(reopened.HasUsageRights);
        Assert.Equal("Ada", reopened.AcroForm.GetFields("Name")[0].Value);
        Assert.Throws<PdfCertificationLockedException>(() => reopened.Metadata.Title = "Still certified");
    }

    // W-88 rebuilt (W-90): pyHanko signs a Reader-extended form (certified at P 2 or P 3, or approved) in a new field,
    // the library then makes each write W-88 checked, and pyHanko's difference analysis accepts every revision: the
    // earlier signature reports FORM_FILLING or LTA_UPDATES with docmdp_ok, and /UR3 stays. Before W-90, every write
    // that removed /UR3 made pyHanko report OTHER, ILLEGAL_MODIFICATIONS. The usage-rights signatures are synthesized.
    [Theory]
    [MemberData(nameof(PyHankoModes))]
    public void pyhanko_accepts_every_write_after_a_signature_on_a_reader_extended_form(string mode)
    {
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            var stamp = new DocTimeStampSignatureProvider(new OpenSslTimestampAuthority(openssl, directory));
            var forms = new List<string>();
            var args = new List<string> { ExternalTool.Fixture("usage_rights_samples.py"), mode };
            foreach (KeyValuePair<string, string> transform in BuiltTransforms)
            {
                foreach (bool direct in new[] { false, true })
                {
                    string name = transform.Key + (direct ? "-direct" : "-indirect");
                    File.WriteAllBytes(Path.Combine(directory, name + ".pdf"), ReaderExtendedForm(transform.Value, direct));
                    args.Add(Path.Combine(directory, name + ".pdf"));
                    args.Add(Path.Combine(directory, name + "-signed.pdf"));
                    forms.Add(name);
                }
            }

            using (Process process = ExternalTool.Start("python", args.ToArray()))
            {
                (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
                Assert.True(exitCode == 0, stderr + stdout);
            }

            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=Usage Rights", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            var cms = new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData));
            var writes = new Dictionary<string, Action<PdfDocument, Stream>>
            {
                ["fill"] = (document, destination) =>
                {
                    document.AcroForm.Fill("Name", "Ada");
                    document.SaveIncremental(destination, leaveOpen: true);
                },
                ["sign, existing field"] = (document, destination) => document.Sign(destination, new PdfSignatureOptions { FieldName = "Signer" }, cms, leaveOpen: true),
                ["sign, new field"] = (document, destination) => document.Sign(destination, new PdfSignatureOptions { FieldName = "Fresh" }, cms, leaveOpen: true),
                ["fill, then sign existing field"] = (document, destination) =>
                {
                    document.AcroForm.Fill("Name", "Ada");
                    document.Sign(destination, new PdfSignatureOptions { FieldName = "Signer" }, cms, leaveOpen: true);
                },
                ["document timestamp, existing field"] = (document, destination) => document.Sign(destination, Timestamp("Signer"), stamp, leaveOpen: true),
                ["document timestamp, new field"] = (document, destination) => document.Sign(destination, Timestamp("Stamp"), stamp, leaveOpen: true),
            };

            var outputs = new Dictionary<string, (string Label, bool AddsSignature)>();
            foreach (string form in forms)
            {
                byte[] signed = File.ReadAllBytes(Path.Combine(directory, form + "-signed.pdf"));
                foreach (KeyValuePair<string, Action<PdfDocument, Stream>> write in writes)
                {
                    using var source = new MemoryStream(signed, writable: false);
                    using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);
                    var destination = new MemoryStream();
                    write.Value(document, destination);
                    byte[] output = destination.ToArray();
                    Assert.True(output.AsSpan(0, signed.Length).SequenceEqual(signed), form + ", " + write.Key + ": the original is not a prefix");
                    string path = Path.Combine(directory, "output-" + outputs.Count.ToString(CultureInfo.InvariantCulture) + ".pdf");
                    File.WriteAllBytes(path, output);
                    outputs.Add(path, (mode + ", " + form + ", " + write.Key, write.Key != "fill"));
                }
            }

            string earlier = mode == "approve" ? "Approve" : "Certify";
            JsonElement probe = CorpusManifest.Probe(outputs.Keys.ToArray());
            foreach (KeyValuePair<string, (string Label, bool AddsSignature)> output in outputs)
            {
                JsonElement after = probe.GetProperty(output.Key);
                string label = output.Value.Label;
                Assert.Null(CorpusManifest.Text(after, "openError"));
                Assert.Equal(0, after.GetProperty("qpdfCheck").GetProperty("exit").GetInt32());
                List<JsonElement> signatures = after.GetProperty("signatures").EnumerateArray().ToList();
                Assert.Equal(output.Value.AddsSignature ? 2 : 1, signatures.Count);
                JsonElement first = signatures.Single(s => CorpusManifest.Text(s, "field") == earlier);
                Assert.True(first.GetProperty("intact").GetBoolean() && first.GetProperty("valid").GetBoolean(), label + ": the earlier signature is not intact and valid");
                Assert.True(first.GetProperty("docmdpOk").GetBoolean(), label + ": " + CorpusManifest.Text(first, "suspicious"));
                Assert.Contains(CorpusManifest.Text(first, "modification"), new[] { "FORM_FILLING", "LTA_UPDATES" });
                Assert.True(after.GetProperty("usageRights").GetBoolean(), label + ": /UR3 was removed");
                if (!output.Value.AddsSignature)
                {
                    continue;
                }

                JsonElement added = signatures.Single(s => CorpusManifest.Text(s, "field") != earlier);
                Assert.Equal("ENTIRE_FILE", CorpusManifest.Text(added, "coverage"));
                Assert.True(added.GetProperty("intact").GetBoolean() && added.GetProperty("valid").GetBoolean(), label + ": the new signature is not intact and valid");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
        // W-97: the signature's /Name is encrypted like any string; only /Contents is plaintext.
        builder.Indirect(10, SignatureBody("/Reference [" + Transform("UR3", "<< /V /2.2 /Form [/FillIn] >>") + "]").Replace("/Name (ARE Test)", "/Name " + crypt.Text("ARE Test", 10)));
        builder.Indirect(20, crypt.Dictionary(null));
        byte[] original = builder.Finish("/Info 5 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");

        // The user password may fill but not modify: the refused metadata write keeps /UR3, and the granted fill keeps it.
        var filled = new MemoryStream();
        using (var user = PdfDocument.Open(new MemoryStream(original, writable: false), "user"))
        {
            Assert.Throws<PdfEncryptionRestrictedException>(() => user.Metadata.Title = "Nope");
            Assert.True(user.HasUsageRights);
            user.AcroForm.Fill("Name", "Ada");
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
    public void reader_extended_form_keeps_usage_rights_on_attachment_add_only_when_granted_or_signed(string id, bool grantsAttachmentEdits)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        bool kept = grantsAttachmentEdits || IsSigned(entry);
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
        Assert.Equal(kept, after.GetProperty("usageRights").GetBoolean());
        using var reopened = PdfDocument.Open(new MemoryStream(destination.ToArray(), writable: false), entry.Password);
        Assert.Equal(kept, reopened.HasUsageRights);
        Assert.Contains(reopened.Attachments, attachment => attachment.Name == "usage-rights.txt");
    }

    // Every Reader-extended corpus form grants /Signature Modify, but no right grants a signature that creates a field,
    // so it removes /UR3 unless the form already has a signature.
    [Theory]
    [MemberData(nameof(ReaderExtendedIds))]
    public void reader_extended_form_loses_usage_rights_when_signed_into_a_new_field_unless_already_signed(string id)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        SignCorpusForm(entry, null, keepsUsageRights: IsSigned(entry));
    }

    // The Reader-extended corpus forms with an empty signature field, as pypdf 6.21.1 reads them. Each grants
    // /Signature Modify, so signing that field keeps /UR3.
    [Theory]
    [InlineData("cms-40b", "Signature-1")]
    [InlineData("cms-40b", "Signature-2")]
    [InlineData("cms-40b-pyhanko-approved", "Signature-2")]
    [InlineData("va-21-4138", "form1[0].#subform[1].Digital_Signature[0]")]
    public void reader_extended_form_keeps_usage_rights_when_its_empty_signature_field_is_signed(string id, string field)
    {
        SignCorpusForm(CorpusManifest.Get(id), field, keepsUsageRights: true);
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

    // W-90 on the corpus form with an approval signature, Signature-1 from pyHanko: no write changes /Perms, so pyHanko's
    // difference analysis of Signature-1 never reports a /Perms change. Before W-90, the metadata write, both signatures
    // and timestamps that create a field, and the timestamp into Signature-2 removed /UR3, and pyHanko reported OTHER
    // because /Perms differed. pyHanko 0.38.0 still reports OTHER for reasons unrelated to /Perms: CMS-40B is a tagged
    // form, and a revision that changes a widget the structure tree references is unexplained (W-88), and an attachment
    // changes the catalog /Names and /AF (W-92). The writes that touch neither are accepted.
    [Fact]
    public void signed_reader_extended_form_keeps_usage_rights_and_pyhanko_finds_no_perms_change()
    {
        CorpusEntry entry = CorpusManifest.Get("cms-40b-pyhanko-approved");
        byte[] original = File.ReadAllBytes(entry.FullPath);
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            var stamp = new DocTimeStampSignatureProvider(new OpenSslTimestampAuthority(openssl, directory));
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=Usage Rights", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            var cms = new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData));
            var writes = new Dictionary<string, Action<PdfDocument, Stream>>
            {
                ["fill"] = (document, destination) =>
                {
                    document.AcroForm.Fill("1-2", "Ada");
                    document.SaveIncremental(destination, leaveOpen: true);
                },
                ["metadata"] = (document, destination) =>
                {
                    document.Metadata.Title = "Usage rights";
                    document.SaveIncremental(destination, leaveOpen: true);
                },
                ["attachment add"] = (document, destination) =>
                {
                    document.Attachments.Add("usage-rights.txt", new MemoryStream(Encoding.ASCII.GetBytes("usage rights")));
                    document.SaveIncremental(destination, leaveOpen: true);
                },
                ["sign, existing field"] = (document, destination) => document.Sign(destination, new PdfSignatureOptions { FieldName = "Signature-2" }, cms, leaveOpen: true),
                ["sign, new field"] = (document, destination) => document.Sign(destination, new PdfSignatureOptions { FieldName = "Fresh" }, cms, leaveOpen: true),
                ["document timestamp, existing field"] = (document, destination) => document.Sign(destination, Timestamp("Signature-2"), stamp, leaveOpen: true),
                ["document timestamp, new field"] = (document, destination) => document.Sign(destination, Timestamp("Stamp"), stamp, leaveOpen: true),
            };
            var accepted = new HashSet<string> { "metadata", "sign, new field", "document timestamp, new field" };

            var outputs = new Dictionary<string, string>();
            foreach (KeyValuePair<string, Action<PdfDocument, Stream>> write in writes)
            {
                using var source = new MemoryStream(original, writable: false);
                using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);
                var destination = new MemoryStream();
                write.Value(document, destination);
                byte[] output = destination.ToArray();
                Assert.True(output.AsSpan(0, original.Length).SequenceEqual(original), write.Key + ": the original is not a prefix");
                string path = Path.Combine(directory, "output-" + outputs.Count.ToString(CultureInfo.InvariantCulture) + ".pdf");
                File.WriteAllBytes(path, output);
                outputs.Add(path, write.Key);
            }

            JsonElement probe = CorpusManifest.Probe(outputs.Keys.ToArray());
            foreach (KeyValuePair<string, string> output in outputs)
            {
                JsonElement after = probe.GetProperty(output.Key);
                Assert.Null(CorpusManifest.Text(after, "openError"));
                Assert.Equal(0, after.GetProperty("qpdfCheck").GetProperty("exit").GetInt32());
                JsonElement first = after.GetProperty("signatures").EnumerateArray().Single(s => CorpusManifest.Text(s, "field") == "Signature-1");
                Assert.True(first.GetProperty("intact").GetBoolean() && first.GetProperty("valid").GetBoolean(), output.Value + ": Signature-1 is not intact and valid");
                string? suspicious = CorpusManifest.Text(first, "suspicious");
                Assert.True(suspicious == null || !(suspicious.Contains("key /Perms") || suspicious.Contains(".Root.Perms")), output.Value + ": " + suspicious);
                if (accepted.Contains(output.Value))
                {
                    Assert.True(first.GetProperty("docmdpOk").GetBoolean(), output.Value + ": " + suspicious);
                    Assert.NotEqual("OTHER", CorpusManifest.Text(first, "modification"));
                }

                Assert.True(after.GetProperty("usageRights").GetBoolean(), output.Value + ": /UR3 was removed");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // W-92 and W-93: on a built form without usage rights, with and without an existing attachment listed in /AF, pyHanko
    // approves or the library time-stamps, and the library then adds an attachment. The earlier signature stays intact
    // and valid, and pyHanko 0.38.0 reports the revision OTHER, because its policy allows no catalog /Names or /AF change
    // after a signature, whoever writes it. A metadata write on the same bases is the control it accepts. The reason is
    // not pinned: pyHanko reports only its first objection.
    [Fact]
    public void attachment_added_after_a_signature_leaves_it_intact_and_pyhanko_reports_other()
    {
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            var stamp = new DocTimeStampSignatureProvider(new OpenSslTimestampAuthority(openssl, directory));
            var args = new List<string> { ExternalTool.Fixture("usage_rights_samples.py"), "approve" };
            var bases = new Dictionary<string, string>();
            foreach (bool existing in new[] { false, true })
            {
                string name = existing ? "attachment" : "plain";
                byte[] form = SignableForm(existing);
                File.WriteAllBytes(Path.Combine(directory, name + ".pdf"), form);
                args.Add(Path.Combine(directory, name + ".pdf"));
                args.Add(Path.Combine(directory, name + "-approved.pdf"));
                bases.Add(name + "-approved", "Approve");

                using PdfDocument document = PdfDocument.Open(new MemoryStream(form, writable: false));
                var stamped = new MemoryStream();
                document.Sign(stamped, Timestamp("Stamp"), stamp, leaveOpen: true);
                File.WriteAllBytes(Path.Combine(directory, name + "-stamped.pdf"), stamped.ToArray());
                bases.Add(name + "-stamped", "Stamp");
            }

            using (Process process = ExternalTool.Start("python", args.ToArray()))
            {
                (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
                Assert.True(exitCode == 0, stderr + stdout);
            }

            var writes = new Dictionary<string, Action<PdfDocument>>
            {
                ["attachment add"] = document => document.Attachments.Add("invoice.xml", new MemoryStream(Encoding.ASCII.GetBytes("<Invoice/>"))),
                ["metadata"] = document => document.Metadata.Title = "Signed",
            };

            var outputs = new Dictionary<string, (string Label, string Earlier, bool Attachment)>();
            foreach (KeyValuePair<string, string> signedBase in bases)
            {
                byte[] signed = File.ReadAllBytes(Path.Combine(directory, signedBase.Key + ".pdf"));
                foreach (KeyValuePair<string, Action<PdfDocument>> write in writes)
                {
                    using var source = new MemoryStream(signed, writable: false);
                    using PdfDocument document = PdfDocument.Open(source, leaveOpen: true);
                    write.Value(document);
                    var destination = new MemoryStream();
                    document.SaveIncremental(destination, leaveOpen: true);
                    byte[] output = destination.ToArray();
                    string label = signedBase.Key + ", " + write.Key;
                    Assert.True(output.AsSpan(0, signed.Length).SequenceEqual(signed), label + ": the signed file is not a prefix");
                    string path = Path.Combine(directory, "output-" + outputs.Count.ToString(CultureInfo.InvariantCulture) + ".pdf");
                    File.WriteAllBytes(path, output);
                    outputs.Add(path, (label, signedBase.Value, write.Key == "attachment add"));
                }
            }

            JsonElement probe = CorpusManifest.Probe(outputs.Keys.ToArray());
            foreach (KeyValuePair<string, (string Label, string Earlier, bool Attachment)> output in outputs)
            {
                JsonElement after = probe.GetProperty(output.Key);
                string label = output.Value.Label;
                Assert.Null(CorpusManifest.Text(after, "openError"));
                Assert.Equal(0, after.GetProperty("qpdfCheck").GetProperty("exit").GetInt32());
                JsonElement earlier = Assert.Single(after.GetProperty("signatures").EnumerateArray().ToList());
                Assert.Equal(output.Value.Earlier, CorpusManifest.Text(earlier, "field"));
                Assert.True(earlier.GetProperty("intact").GetBoolean() && earlier.GetProperty("valid").GetBoolean(), label + ": the earlier signature is not intact and valid");
                string? modification = CorpusManifest.Text(earlier, "modification");
                if (output.Value.Attachment)
                {
                    Assert.True(modification == "OTHER", label + ": " + modification);
                    Assert.NotNull(CorpusManifest.Text(earlier, "suspicious"));
                    Assert.Contains(after.GetProperty("attachments").EnumerateArray(), attachment => CorpusManifest.Text(attachment, "name") == "invoice.xml");
                }
                else
                {
                    Assert.True(modification != "OTHER", label + ": " + CorpusManifest.Text(earlier, "suspicious"));
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Signs the form with a throwaway RSA key, into `field` or a new field. pypdf (through the corpus probe) reads whether
    // /UR3 survived, and pyHanko finds the new signature intact and valid over the entire file in that field.
    private static void SignCorpusForm(CorpusEntry entry, string? field, bool keepsUsageRights)
    {
        byte[] original = File.ReadAllBytes(entry.FullPath);
        using var source = new MemoryStream(original, writable: false);
        using PdfDocument document = PdfDocument.Open(source, entry.Password, leaveOpen: true);
        Assert.True(document.HasUsageRights);
        if (field != null)
        {
            Assert.Equal(PdfFieldType.Signature, Assert.Single(document.AcroForm.GetFields(field)).FieldType);
        }

        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Usage Rights", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var destination = new MemoryStream();
        document.Sign(destination, new PdfSignatureOptions { FieldName = field }, new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData)), leaveOpen: true);

        JsonElement after = CheckOutput(entry, original, destination.ToArray());
        Assert.Equal(keepsUsageRights, after.GetProperty("usageRights").GetBoolean());
        List<JsonElement> signatures = after.GetProperty("signatures").EnumerateArray().ToList();
        Assert.Equal(entry.Probe.GetProperty("signatures").GetArrayLength() + 1, signatures.Count);
        JsonElement signed = signatures.Single(s => CorpusManifest.Text(s, "coverage") == "ENTIRE_FILE");
        if (field != null)
        {
            Assert.Equal(field, CorpusManifest.Text(signed, "field"));
        }

        Assert.True(signed.GetProperty("intact").GetBoolean());
        Assert.True(signed.GetProperty("valid").GetBoolean());
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

    private static bool IsSigned(CorpusEntry entry)
    {
        return entry.Probe.GetProperty("signatures").GetArrayLength() > 0;
    }

    private static PdfSignatureOptions Timestamp(string fieldName)
    {
        return new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, FieldName = fieldName, ReservedContentsBytes = 16384 };
    }

    // The catalog's /Perms value in the last revision that writes the catalog (object 1), with whitespace around
    // delimiters removed, or null when there is none.
    private static string? PermsValue(string pdf)
    {
        MatchCollection catalogs = Regex.Matches(pdf, @"(?m)^1 0 obj");
        int start = catalogs[catalogs.Count - 1].Index;
        int end = pdf.IndexOf("endobj", start, StringComparison.Ordinal);
        int key = pdf.IndexOf("/Perms", start, end - start, StringComparison.Ordinal);
        if (key < 0)
        {
            return null;
        }

        int i = key + "/Perms".Length;
        while (char.IsWhiteSpace(pdf[i]))
        {
            i++;
        }

        int valueStart = i;
        if (string.CompareOrdinal(pdf, i, "<<", 0, 2) != 0)
        {
            return Regex.Match(pdf.Substring(i, end - i), @"^\d+\s+\d+\s+R").Value;
        }

        int depth = 0;
        do
        {
            if (string.CompareOrdinal(pdf, i, "<<", 0, 2) == 0)
            {
                depth++;
                i += 2;
            }
            else if (string.CompareOrdinal(pdf, i, ">>", 0, 2) == 0)
            {
                depth--;
                i += 2;
            }
            else if (pdf[i] == '<' || pdf[i] == '(')
            {
                i = pdf.IndexOf(pdf[i] == '<' ? '>' : ')', i) + 1;
            }
            else
            {
                i++;
            }
        }
        while (depth > 0);

        return Regex.Replace(pdf.Substring(valueStart, i - valueStart), @"\s*([<>\[\]()/])\s*", "$1");
    }

    // A W-88 form: a text field Name, an empty signature field Signer, and /UR3 with the given /Reference, in an
    // indirect /Perms or a direct one.
    private static byte[] ReaderExtendedForm(string reference, bool directPerms)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 6 0 R >> >> >>" + (directPerms ? " /Perms << /UR3 10 0 R >>" : Indirect) + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [4 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 300 724] /DA (/Helv 12 Tf 0 g) /F 4 /P 3 0 R >>");
        builder.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signer) /Rect [300 600 500 640] /F 4 /P 3 0 R >>");
        builder.Indirect(6, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        if (!directPerms)
        {
            builder.Indirect(9, "<< /UR3 10 0 R >>");
        }

        builder.Indirect(10, SignatureBody(reference));
        return builder.Finish("/ID [<0123456789abcdef0123456789abcdef> <0123456789abcdef0123456789abcdef>]");
    }

    // A W-92 form without usage rights: a text field Name and an empty signature field Signer, and optionally an
    // attachment existing.txt in an indirect /Names /EmbeddedFiles tree and an indirect catalog /AF, as an e-invoice
    // that already carries its XML would have.
    private static byte[] SignableForm(bool existingAttachment)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 6 0 R >> >> >>" + (existingAttachment ? " /Names 20 0 R /AF 24 0 R" : string.Empty) + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [4 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 300 724] /DA (/Helv 12 Tf 0 g) /F 4 /P 3 0 R >>");
        builder.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signer) /Rect [300 600 500 640] /F 4 /P 3 0 R >>");
        builder.Indirect(6, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        if (existingAttachment)
        {
            builder.Indirect(20, "<< /EmbeddedFiles 21 0 R >>");
            builder.Indirect(21, "<< /Names [(existing.txt) 22 0 R] >>");
            builder.Indirect(22, "<< /Type /Filespec /F (existing.txt) /UF (existing.txt) /EF << /F 23 0 R >> >>");
            builder.StreamObject(23, "/Type /EmbeddedFile", Encoding.ASCII.GetBytes("existing"));
            builder.Indirect(24, "[22 0 R]");
        }

        return builder.Finish("/ID [<0123456789abcdef0123456789abcdef> <0123456789abcdef0123456789abcdef>]");
    }

    // The policy document, unsigned or signed in the field Signed. A certification also puts /DocMDP in /Perms.
    private static byte[] Document(PolicyCase policy, string signedBy)
    {
        bool certified = signedBy.StartsWith("certified", StringComparison.Ordinal);
        string perms = policy.Perms;
        if (certified && perms.Length == 0)
        {
            perms = " /Perms << /DocMDP 25 0 R >>";
        }
        else if (certified && perms != Indirect)
        {
            perms = perms.Replace(" /Perms <<", " /Perms << /DocMDP 25 0 R");
        }

        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R 7 0 R 8 0 R 22 0 R 23 0 R 24 0 R] >> /Names << /EmbeddedFiles << /Names [(old.txt) 20 0 R] >> >>" + perms + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R 6 0 R 7 0 R 8 0 R 22 0 R 23 0 R 24 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 240 720] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Btn /T (Agree) /Rect [72 660 90 678] /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Btn /Ff 49152 /T (Pick) /Rect [72 620 90 638] /P 3 0 R >>");
        builder.Indirect(7, "<< /Subtype /Widget /FT /Ch /T (Color) /Opt [(Red) (Blue)] /Rect [72 580 240 600] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");
        builder.Indirect(8, "<< /Subtype /Widget /FT /Btn /Ff 49152 /T (Size) /AP << /N << /Small null /Off null >> >> /Rect [72 540 90 558] /P 3 0 R >>");
        foreach ((int id, string body) in policy.Objects)
        {
            Assert.True(id != 9 || body.StartsWith("<<", StringComparison.Ordinal));
            builder.Indirect(id, certified && id == 9 ? "<< /DocMDP 25 0 R" + body.Substring(2) : body);
        }

        builder.Indirect(20, "<< /Type /Filespec /F (old.txt) /UF (old.txt) /EF << /F 21 0 R >> >>");
        builder.StreamObject(21, "/Type /EmbeddedFile", Encoding.ASCII.GetBytes("old"));

        // An empty signature field, a read-only one, and one that has a signature dictionary only when the document is
        // signed: an approval signature, a DocMDP certification, or a document timestamp.
        builder.Indirect(22, "<< /Subtype /Widget /FT /Sig /T (Signer) /Rect [300 700 500 740] /F 4 /P 3 0 R >>");
        builder.Indirect(23, "<< /Subtype /Widget /FT /Sig /Ff 1 /T (Locked) /Rect [300 640 500 680] /F 4 /P 3 0 R >>");
        if (signedBy == Unsigned)
        {
            builder.Indirect(24, "<< /Subtype /Widget /FT /Sig /T (Signed) /Rect [300 580 500 620] /F 4 /P 3 0 R >>");
            return builder.Finish();
        }

        builder.Indirect(24, "<< /Subtype /Widget /FT /Sig /T (Signed) /V 25 0 R /Rect [300 580 500 620] /F 4 /P 3 0 R >>");
        string signature = signedBy switch
        {
            "approval" => "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 0 0 0] /Contents <00> >>",
            "certified P 2" => "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 0 0 0] /Contents <00> /Reference [" + Transform("DocMDP", "<< /Type /TransformParams /P 2 /V /1.2 >>") + "] >>",
            "certified P 3" => "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 0 0 0] /Contents <00> /Reference [" + Transform("DocMDP", "<< /Type /TransformParams /P 3 /V /1.2 >>") + "] >>",
            "document timestamp" => "<< /Type /DocTimeStamp /Filter /Adobe.PPKLite /SubFilter /ETSI.RFC3161 /ByteRange [0 0 0 0] /Contents <00> >>",
            _ => throw new ArgumentOutOfRangeException(nameof(signedBy)),
        };
        builder.Indirect(25, signature);
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

    // DocMDP P 2 and P 3 allow fills and signing, and refuse metadata and attachment writes.
    private static PolicyWrite Write(string name, string? right, Action<PdfDocument> edit)
    {
        return new PolicyWrite(name, right, right == FillIn, (document, destination) =>
        {
            edit(document);
            document.SaveIncremental(destination, leaveOpen: true);
        });
    }

    private static PolicyWrite Sign(string name, string? right, PdfSignatureKind kind, string? fieldName)
    {
        return new PolicyWrite(name, right, true, (document, destination) =>
        {
            var options = new PdfSignatureOptions { Kind = kind, FieldName = fieldName, ReservedContentsBytes = 64 };
            document.Sign(destination, options, new FixedProvider { Kind = kind }, leaveOpen: true);
            using var reopened = PdfDocument.Open(new MemoryStream(((MemoryStream)destination).ToArray(), writable: false));
            Assert.Equal(fieldName == "Signer" ? 3 : 4, reopened.AcroForm.Fields.Count(field => field.FieldType == PdfFieldType.Signature));
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
        public PolicyWrite(string name, string? right, bool certificationAllows, Action<PdfDocument, Stream> run)
        {
            Name = name;
            Right = right;
            CertificationAllows = certificationAllows;
            Run = run;
        }

        public string Name { get; }

        public string? Right { get; }

        public bool CertificationAllows { get; }

        public Action<PdfDocument, Stream> Run { get; }
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public bool Fail { get; set; }

        public PdfSignatureKind Kind { get; set; } = PdfSignatureKind.CmsDetached;

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
