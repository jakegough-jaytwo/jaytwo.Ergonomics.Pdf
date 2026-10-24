using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.AcroForm;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Spec R12 and W4 (W-76): XFA classification, and what a fill does to a hybrid or an XFA-only form.
public class XfaTests
{
    private const string Packets = "/XFA [(template) 10 0 R (datasets) 11 0 R]";
    private const string UsageRights = "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /Reference [31 0 R] >>";
    private const string FillInTransform = "<< /Type /SigRef /TransformMethod /UR3 /TransformParams << /Type /TransformParams /V /2.2 /Form [/FillIn] >> >>";

    public static IEnumerable<object[]> Classifications => new[]
    {
        new object[] { "array", Packets, string.Empty, true, true, false },
        new object[] { "stream", "/XFA 10 0 R", string.Empty, true, true, false },
        new object[] { "indirect array", "/XFA 12 0 R", string.Empty, true, true, false },
        new object[] { "NeedsRendering false", Packets, " /NeedsRendering false", true, true, false },
        new object[] { "NeedsRendering true", Packets, " /NeedsRendering true", true, false, true },
        new object[] { "no widgets", Packets, string.Empty, false, false, true },
        new object[] { "stream, no widgets, NeedsRendering", "/XFA 10 0 R", " /NeedsRendering true", false, false, true },
        new object[] { "no XFA", string.Empty, string.Empty, true, false, false },
        new object[] { "no XFA, NeedsRendering", string.Empty, " /NeedsRendering true", false, false, false },
        new object[] { "null", "/XFA null", string.Empty, true, false, false },
        new object[] { "dangling", "/XFA 99 0 R", string.Empty, true, false, false },
        new object[] { "string", "/XFA (template)", string.Empty, true, false, false },
        new object[] { "dictionary without a stream", "/XFA 13 0 R", string.Empty, true, false, false },
    };

    [Theory]
    [MemberData(nameof(Classifications))]
    public void form_is_hybrid_or_xfa_only_by_its_xfa_needs_rendering_and_widgets(string name, string xfa, string catalogExtra, bool widgets, bool hybrid, bool xfaOnly)
    {
        byte[] pdf = Form(xfa, catalogExtra, widgets);

        using var document = PdfDocument.Open(new MemoryStream(pdf, writable: false), leaveOpen: true);

        Assert.True(hybrid == document.AcroForm.IsHybridXfa, name + ": IsHybridXfa");
        Assert.True(xfaOnly == document.AcroForm.IsXfaOnly, name + ": IsXfaOnly");
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Corpus)]
    public void corpus_hybrid_and_xfa_only_forms_are_classified()
    {
        foreach (CorpusEntry entry in CorpusManifest.Entries.Where(e => e.Open == "ok"))
        {
            using var stream = new MemoryStream(File.ReadAllBytes(entry.FullPath), writable: false);
            using PdfDocument document = PdfDocument.Open(stream, entry.Password, leaveOpen: true);
            bool hybrid = entry.Id == "irs-fw9-2024-03" || entry.Id == "va-21-4138";
            bool xfaOnly = entry.Id == "grants-sf424-v4";
            Assert.True(hybrid == document.AcroForm.IsHybridXfa, entry.Id + ": IsHybridXfa");
            Assert.True(xfaOnly == document.AcroForm.IsXfaOnly, entry.Id + ": IsXfaOnly");
        }
    }

    [Theory]
    [InlineData(Packets)]
    [InlineData("/XFA 10 0 R")]
    public void first_fill_removes_xfa_in_the_same_revision(string xfa)
    {
        byte[] pdf = Form(xfa, string.Empty, widgets: true);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.True(document.AcroForm.IsHybridXfa);
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Name", "Ada"));
            Assert.False(document.AcroForm.IsHybridXfa);
            Assert.False(document.AcroForm.IsXfaOnly);
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Name", "Grace"));
            editor.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        Assert.Equal(1, Occurrences(saved.AsSpan(pdf.Length).ToArray(), "startxref"));
        using var reopened = PdfDocument.Open(new MemoryStream(saved, writable: false), leaveOpen: true);
        Assert.False(reopened.AcroForm.IsHybridXfa);
        Assert.False(reopened.AcroForm.IsXfaOnly);
        Assert.Equal("Grace", reopened.AcroForm.GetFields("Name")[0].Value);
        Assert.Equal(2, reopened.Revisions.Count);
    }

    [Fact]
    public void bulk_fill_removes_xfa_and_writes_the_same_revision_as_single_fills()
    {
        byte[] pdf = Form(Packets, string.Empty, widgets: true);
        byte[] single = Save(pdf, null, (document, editor) =>
        {
            editor.AcroForm.Fill("Name", "Ada");
            editor.AcroForm.Fill("Other", "Grace");
        });
        byte[] bulk = Save(pdf, null, (_, editor) =>
            editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["Other"] = "Grace" }));

        Assert.Equal(single, bulk);
        using var reopened = PdfDocument.Open(new MemoryStream(bulk, writable: false), leaveOpen: true);
        Assert.False(reopened.AcroForm.IsHybridXfa);
        Assert.Equal("Ada", reopened.AcroForm.GetFields("Name")[0].Value);
        Assert.Equal("Grace", reopened.AcroForm.GetFields("Other")[0].Value);
    }

    [Fact]
    public void refused_and_empty_fills_keep_xfa_and_do_not_dirty_the_document()
    {
        byte[] pdf = Form(Packets, string.Empty, widgets: true);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfFieldNotFoundException>(() => editor.AcroForm.Fill("Missing", "Ada"));
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Name", true));
        Assert.Throws<PdfFieldReadOnlyException>(() => editor.AcroForm.Fill("Locked", "Ada"));
        Assert.Throws<PdfFieldNotFoundException>(() => editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["Missing"] = "Ada" }));
        Assert.Empty(editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue>()));
        Assert.True(document.AcroForm.IsHybridXfa);
        editor.SaveIncremental(stream);

        Assert.Equal(pdf.Length, stream.Length);
    }

    [Fact]
    public void certification_and_fieldmdp_locks_refuse_a_hybrid_fill_first()
    {
        // DocMDP P 1 refuses with the certification lock, ahead of the signed-hybrid refusal (D-48), and keeps /XFA.
        byte[] locked = Form(Packets, " /Perms << /DocMDP 20 0 R >>", widgets: true, signatureField: false, (20, "<< /Type /Sig /Reference [21 0 R] >>"), (21, "<< /TransformMethod /DocMDP /TransformParams << /P 1 /V /1.2 >> >>"));
        using (var document = PdfDocument.Open(new MemoryStream(locked, writable: false), leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Throws<PdfCertificationLockedException>(() => editor.AcroForm.Fill("Name", "Ada"));
            Assert.True(document.AcroForm.IsHybridXfa);
        }

        // A FieldMDP lock on the field refuses and keeps /XFA.
        byte[] fieldLocked = Form(Packets, string.Empty, widgets: true, signatureField: true, (20, "<< /Type /Sig /Reference [21 0 R] >>"), (21, "<< /TransformMethod /FieldMDP /TransformParams << /Action /All /V /1.2 >> >>"));
        using (var document = PdfDocument.Open(new MemoryStream(fieldLocked, writable: false), leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Throws<PdfCertificationLockedException>(() => editor.AcroForm.Fill("Name", "Ada"));
            Assert.True(document.AcroForm.IsHybridXfa);
        }
    }

    // D-48: a hybrid form with a signature refuses a fill. A document has a signature when /Perms has /DocMDP or a
    // signature field's /V resolves to a dictionary (spec 7.3), so P 2 and P 3 certifications, approval signatures, and
    // document timestamps all count.
    [Theory]
    [InlineData("certified P 2", " /Perms << /DocMDP 20 0 R /UR3 30 0 R >>", false, "<< /Type /Sig /Reference [21 0 R] >>", "/P 2")]
    [InlineData("certified P 3", " /Perms << /DocMDP 20 0 R /UR3 30 0 R >>", false, "<< /Type /Sig /Reference [21 0 R] >>", "/P 3")]
    [InlineData("approval", " /Perms << /UR3 30 0 R >>", true, "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached >>", "/P 2")]
    [InlineData("document timestamp", " /Perms << /UR3 30 0 R >>", true, "<< /Type /DocTimeStamp /Filter /Adobe.PPKLite /SubFilter /ETSI.RFC3161 >>", "/P 2")]
    public void signed_hybrid_form_refuses_a_fill_and_keeps_xfa_and_usage_rights(string name, string perms, bool signatureField, string signature, string docMdp)
    {
        byte[] pdf = Form(Packets, perms, widgets: true, signatureField, (20, signature), (21, "<< /TransformMethod /DocMDP /TransformParams << " + docMdp + " /V /1.2 >> >>"), (30, UsageRights), (31, FillInTransform));
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.True(document.AcroForm.IsHybridXfa, name);
        Assert.Throws<PdfSignatureConflictException>(() => editor.AcroForm.Fill("Name", "Ada"));
        Assert.Throws<PdfSignatureConflictException>(() => editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["Other"] = "Grace" }));
        Assert.Empty(editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue>()));
        Assert.True(document.AcroForm.IsHybridXfa);
        Assert.True(document.HasUsageRights);
        Assert.Null(document.AcroForm.GetFields("Name")[0].Value);
        editor.SaveIncremental(stream);

        Assert.Equal(pdf.Length, stream.Length);
    }

    // D-48: a signature field whose /V does not resolve is unsigned, and the /UR3 signature is not a signature, so the
    // fill proceeds; it removes /XFA and, since FillIn does not grant deleting the XFA definition, /UR3 too.
    [Theory]
    [InlineData("usage rights only", false)]
    [InlineData("signature field with a dangling /V", true)]
    public void unsigned_hybrid_fill_removes_xfa_and_usage_rights(string name, bool danglingSignature)
    {
        byte[] pdf = Form(Packets, " /Perms << /UR3 30 0 R >>", widgets: true, signatureField: danglingSignature, (30, UsageRights), (31, FillInTransform));
        byte[] filled = Save(pdf, null, (document, editor) =>
        {
            Assert.True(document.HasUsageRights, name);
            editor.AcroForm.Fill("Name", "Ada");
            Assert.False(document.AcroForm.IsHybridXfa);
            Assert.False(document.HasUsageRights);
        });

        using var reopened = PdfDocument.Open(new MemoryStream(filled, writable: false), leaveOpen: true);
        Assert.False(reopened.AcroForm.IsHybridXfa);
        Assert.False(reopened.HasUsageRights);
        Assert.Equal("Ada", reopened.AcroForm.GetFields("Name")[0].Value);
    }

    [Fact]
    public void fill_of_a_form_without_xfa_keeps_usage_rights_that_grant_fill_in()
    {
        byte[] pdf = Form(string.Empty, " /Perms << /UR3 30 0 R >>", widgets: true, signatureField: false, (30, UsageRights), (31, FillInTransform));
        byte[] filled = Save(pdf, null, (_, editor) => editor.AcroForm.Fill("Name", "Ada"));

        using var reopened = PdfDocument.Open(new MemoryStream(filled, writable: false), leaveOpen: true);
        Assert.True(reopened.HasUsageRights);
        Assert.Equal("Ada", reopened.AcroForm.GetFields("Name")[0].Value);
    }

    // The signed check runs before Sign adds its own signature, so a fill and a signature of an unsigned hybrid form in
    // one session both apply: the fill removes /XFA and /UR3 and the signature follows in the same save.
    [Fact]
    public void fill_then_sign_of_an_unsigned_hybrid_form_removes_xfa_and_usage_rights()
    {
        byte[] pdf = Form(Packets, " /Perms << /UR3 30 0 R >>", widgets: true, signatureField: false, (30, UsageRights), (31, FillInTransform));
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        var destination = new MemoryStream();

        editor.AcroForm.Fill("Name", "Ada");
        editor.Sign(destination, new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider(), leaveOpen: true);

        byte[] signed = destination.ToArray();
        Assert.True(signed.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        using var reopened = PdfDocument.Open(new MemoryStream(signed, writable: false), leaveOpen: true);
        Assert.False(reopened.AcroForm.IsHybridXfa);
        Assert.False(reopened.HasUsageRights);
        Assert.Equal("Ada", reopened.AcroForm.GetFields("Name")[0].Value);
        Assert.Single(reopened.AcroForm.Fields, field => field.FieldType == PdfFieldType.Signature);
    }

    [Theory]
    [InlineData(Packets, " /NeedsRendering true", true)]
    [InlineData(Packets, "", false)]
    public void xfa_only_form_refuses_fills_and_still_reads(string xfa, string catalogExtra, bool widgets)
    {
        byte[] pdf = Form(xfa, catalogExtra, widgets);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.True(document.AcroForm.IsXfaOnly);
        Assert.Throws<PdfUnsupportedFormException>(() => editor.AcroForm.Fill("Name", "Ada"));
        Assert.Throws<PdfUnsupportedFormException>(() => editor.AcroForm.Fill("Missing", "Ada"));
        Assert.Throws<PdfUnsupportedFormException>(() => editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada" }));
        Assert.Empty(editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue>()));
        Assert.True(document.AcroForm.IsXfaOnly);
        Assert.Equal(1, document.PageCount);
        Assert.Equal(widgets ? 3 : 0, document.AcroForm.Fields.Count);
        Assert.Equal(widgets ? 3 : 0, document.AcroForm.Export().Count);
        Assert.Equal("XFA", document.Metadata.Title);
        editor.SaveIncremental(stream);

        Assert.Equal(pdf.Length, stream.Length);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Corpus)]
    public void corpus_xfa_only_form_refuses_a_fill_and_still_reads()
    {
        CorpusEntry entry = CorpusManifest.Get("grants-sf424-v4");
        byte[] original = File.ReadAllBytes(entry.FullPath);
        using var stream = new MemoryStream();
        stream.Write(original, 0, original.Length);
        using var document = PdfDocument.Open(stream, entry.Password, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfUnsupportedFormException>(() => editor.AcroForm.Fill("form1[0].Name[0]", "Ada"));
        Assert.Throws<PdfUnsupportedFormException>(() => editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["form1[0].Name[0]"] = "Ada" }));
        Assert.Empty(document.AcroForm.Fields);
        Assert.Empty(document.AcroForm.Export());
        Assert.Equal(entry.Probe.GetProperty("pages").GetInt32(), document.PageCount);
        Assert.True(document.HasUsageRights);
        editor.SaveIncremental(stream);

        Assert.Equal(original.Length, stream.Length);
    }

    // Plan C4: the fill re-encrypts the AcroForm dictionary without /XFA, and the catalog without /UR3 (D-48), under each
    // password and keeps /Encrypt and /ID; a user password without the fill bit refuses and keeps /XFA and /UR3. qpdf and pypdf read the output with the password.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("aes-256", "user")]
    [InlineData("aes-256", "owner")]
    [InlineData("rc4-128", "user")]
    [InlineData("rc4-128", "owner")]
    public void encrypted_hybrid_fill_removes_xfa_under_each_password(string cipher, string password)
    {
        StandardEncryptor.Kind kind = cipher == "aes-256" ? StandardEncryptor.Kind.Revision6 : StandardEncryptor.Kind.Revision3;
        byte[] pdf = EncryptedForm(new StandardEncryptor(kind, "user", "owner", StandardEncryptor.FillOnly(), encryptMetadata: true));
        byte[] filled = Save(pdf, password, (document, editor) =>
        {
            Assert.True(document.AcroForm.IsHybridXfa);
            Assert.True(document.HasUsageRights);
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Name", "Ada"));
        });

        Assert.True(filled.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string update = Encoding.ASCII.GetString(filled, pdf.Length, filled.Length - pdf.Length);
        Assert.Contains("/Encrypt 20 0 R", update, StringComparison.Ordinal);
        Assert.Contains("/ID [", update, StringComparison.Ordinal);
        Assert.DoesNotContain("/XFA", update, StringComparison.Ordinal);
        Assert.DoesNotContain("Helv 12 Tf", update, StringComparison.Ordinal);
        using (var reopened = PdfDocument.Open(new MemoryStream(filled, writable: false), "user", leaveOpen: true))
        {
            Assert.False(reopened.AcroForm.IsHybridXfa);
            Assert.False(reopened.HasUsageRights);
            Assert.Equal("Ada", reopened.AcroForm.GetFields("Name")[0].Value);
        }

        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "filled.pdf");
            File.WriteAllBytes(path, filled);
            AssertQpdfCheck(path, password);
            string spec = path + "::" + password + "::FIELD=Name";
            JsonElement probe = CorpusManifest.Probe(spec).GetProperty(spec);
            Assert.Null(CorpusManifest.Text(probe, "openError"));
            Assert.DoesNotContain("xfa", probe.GetProperty("features").EnumerateArray().Select(f => f.GetString()));
            Assert.Equal("Ada", CorpusManifest.Text(probe.GetProperty("fieldValues"), "Name"));
            Assert.False(probe.GetProperty("usageRights").GetBoolean());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void user_password_without_the_fill_bit_refuses_and_keeps_xfa()
    {
        int noFill = -4 & ~(1 << 8) & ~(1 << 5);
        byte[] pdf = EncryptedForm(new StandardEncryptor(StandardEncryptor.Kind.Revision6, "user", "owner", noFill, encryptMetadata: true));
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, "user", leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfEncryptionRestrictedException>(() => editor.AcroForm.Fill("Name", "Ada"));
        Assert.True(document.AcroForm.IsHybridXfa);
        Assert.True(document.HasUsageRights);
        editor.SaveIncremental(stream);

        Assert.Equal(pdf.Length, stream.Length);
    }

    // D-48 on a real form: pyHanko approves or certifies (P 2) the W-9 hybrid form. A fill would remove /XFA, which
    // pyHanko 0.38.0's difference analysis does not permit after a signature (W-76 measured modification OTHER), so the
    // fill throws, the source is unchanged, and pyHanko still finds the signature intact and valid.
    [Theory]
    [Trait(Lane.Trait, Lane.External)]
    [Trait(Lane.Trait, Lane.Corpus)]
    [InlineData("approve")]
    [InlineData("certify-p2")]
    public void pyhanko_signed_hybrid_corpus_form_refuses_a_fill(string mode)
    {
        CorpusEntry entry = CorpusManifest.Get("irs-fw9-2024-03");
        string directory = ExternalTool.TempDirectory();
        try
        {
            string unsigned = Path.Combine(directory, "unsigned.pdf");
            string signedPath = Path.Combine(directory, "signed.pdf");
            File.Copy(entry.FullPath, unsigned);
            using (Process process = ExternalTool.Start(ExternalTool.Python(), ExternalTool.Fixture("usage_rights_samples.py"), mode, unsigned, signedPath))
            {
                (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
                Assert.True(exitCode == 0, stderr + stdout);
            }

            byte[] signed = File.ReadAllBytes(signedPath);
            using (var stream = new MemoryStream())
            {
                stream.Write(signed, 0, signed.Length);
                using var document = PdfDocument.Open(stream, leaveOpen: true);
                using var editor = document.Edit();
                Assert.True(document.AcroForm.IsHybridXfa);
                Assert.Throws<PdfSignatureConflictException>(() => editor.AcroForm.Fill("topmostSubform[0].Page1[0].f1_01[0]", "Ada"));
                Assert.True(document.HasUsageRights);
                editor.SaveIncremental(stream);
                Assert.True(stream.ToArray().AsSpan().SequenceEqual(signed));
            }

            JsonElement probe = CorpusManifest.Probe(signedPath).GetProperty(signedPath);
            Assert.Contains("xfa", probe.GetProperty("features").EnumerateArray().Select(f => f.GetString()));
            string earlier = mode == "approve" ? "Approve" : "Certify";
            JsonElement signature = probe.GetProperty("signatures").EnumerateArray().Single(s => CorpusManifest.Text(s, "field") == earlier);
            Assert.True(signature.GetProperty("intact").GetBoolean() && signature.GetProperty("valid").GetBoolean());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // A one-page form: Name and Other are text fields, Locked is read-only. `xfa` is the AcroForm /XFA entry; objects 10
    // and 11 are packet streams, 12 an indirect packet array, 13 a dictionary that is not a stream. With `signatureField`,
    // Sig is a signature field whose /V is object 20, which the caller passes in `extra`.
    private static byte[] Form(string xfa, string catalogExtra, bool widgets, bool signatureField = false, params (int Id, string Body)[] extra)
    {
        string fields = (widgets ? "4 0 R 5 0 R 6 0 R" : string.Empty) + (signatureField ? " 7 0 R" : string.Empty);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [" + fields + "] " + xfa + " >>" + catalogExtra + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [" + fields + "] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 240 720] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T (Other) /Rect [72 660 240 680] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Tx /T (Locked) /Ff 1 /Rect [72 620 240 640] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>");
        builder.Indirect(7, "<< /Subtype /Widget /FT /Sig /T (Sig) /V 20 0 R /Rect [0 0 0 0] /P 3 0 R >>");
        builder.StreamObject(10, string.Empty, Encoding.ASCII.GetBytes("<template xmlns=\"http://www.xfa.org/schema/xfa-template/2.5/\"/>"));
        builder.StreamObject(11, string.Empty, Encoding.ASCII.GetBytes("<xfa:datasets xmlns:xfa=\"http://www.xfa.org/schema/xfa-data/1.0/\"/>"));
        builder.Indirect(12, "[(template) 10 0 R (datasets) 11 0 R]");
        builder.Indirect(13, "<< /Type /NotAStream >>");
        builder.Indirect(14, "<< /Title (XFA) >>");
        foreach ((int id, string body) in extra)
        {
            builder.Indirect(id, body);
        }

        return builder.Finish("/Info 14 0 R");
    }

    private static byte[] EncryptedForm(StandardEncryptor crypt)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] /XFA [" + crypt.Text("template", 1) + " 10 0 R] /DA " + crypt.Text("/Helv 0 Tf 0 g", 1) + " >> /Perms << /UR3 30 0 R >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [5 0 R] >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T " + crypt.Text("Name", 5) + " /Rect [72 700 240 720] /DA " + crypt.Text("/Helv 12 Tf 0 g", 5) + " /P 3 0 R >>");
        builder.StreamObject(10, string.Empty, crypt.Encrypt(Encoding.ASCII.GetBytes("<template xmlns=\"http://www.xfa.org/schema/xfa-template/2.5/\"/>"), 10, 0));
        builder.Indirect(20, crypt.Dictionary(null));
        builder.Indirect(30, UsageRights);
        builder.Indirect(31, FillInTransform);
        return builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private static byte[] Save(byte[] pdf, string? password, Action<PdfDocument, PdfDocumentEditor> write)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, password, leaveOpen: true))
        {
            using var editor = document.Edit();
            write(document, editor);
            editor.SaveIncremental(stream);
        }

        return stream.ToArray();
    }

    private static int Occurrences(byte[] data, string text)
    {
        string ascii = Encoding.ASCII.GetString(data);
        int count = 0;
        for (int index = ascii.IndexOf(text, StringComparison.Ordinal); index >= 0; index = ascii.IndexOf(text, index + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
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
        Assert.True(exitCode == 0 && !(stdout + stderr).Contains("WARNING"), "qpdf --check exited " + exitCode.ToString(CultureInfo.InvariantCulture) + Environment.NewLine + stderr + stdout);
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(new byte[] { 1, 2, 3, 4 });
        }
    }
}
