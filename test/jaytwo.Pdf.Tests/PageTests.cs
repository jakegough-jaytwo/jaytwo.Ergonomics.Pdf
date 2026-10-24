using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Xunit;

namespace jaytwo.Pdf.Tests;

public class PageTests
{
    private const string PypdfScript =
        "import json, sys\n"
        + "from pypdf import PdfReader\n"
        + "reader = PdfReader(sys.argv[1])\n"
        + "if len(sys.argv) > 2: reader.decrypt(sys.argv[2])\n"
        + "page = reader.pages[0]\n"
        + "raw = page.indirect_reference.get_object()\n"
        + "parent = raw['/Parent'].get_object()\n"
        + "widget = raw['/Annots'][0].get_object()\n"
        + "rect = lambda value: [float(item) for item in value]\n"
        + "print(json.dumps({'direct': '/Rotate' in raw, 'rotation': int(raw['/Rotate']), 'parentRotation': int(parent['/Rotate']), 'mediaBox': rect(page.mediabox), 'cropBox': rect(page.cropbox), 'widgetRect': rect(widget['/Rect'])}))\n";

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void rotation_overrides_inheritance_and_independent_tools_see_unchanged_user_space_coordinates()
    {
        byte[] original = PageDocument(inheritedRotation: 90);
        using var stream = new MemoryStream();
        stream.Write(original, 0, original.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            PdfPage page = Assert.Single(document.Pages);
            PdfFieldSnapshot before = FieldSnapshot(document);
            Assert.Equal(90, page.Rotation);
            Assert.Equal(new PdfRect(10, 20, 210, 320), page.MediaBox);
            Assert.Equal(new PdfRect(20, 30, 180, 280), page.CropBox);

            PdfPageEditor pageEditor = Assert.Single(editor.Pages);
            Assert.Equal(1, pageEditor.Number);
            Assert.Equal(90, pageEditor.Rotation);

            pageEditor.Rotation = -180;

            Assert.Equal(180, pageEditor.Rotation);
            Assert.Equal(180, Assert.Single(document.Pages).Rotation);
            Assert.Equal(before, FieldSnapshot(document));
            Assert.Equal(180, document.AcroForm.Fields[0].PageRotation);
            editor.SaveIncremental();
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, original.Length).SequenceEqual(original));
        using (var reopened = PdfDocument.Open(new MemoryStream(saved), leaveOpen: true))
        {
            Assert.Equal(180, Assert.Single(reopened.Pages).Rotation);
            Assert.Equal(2, reopened.Revisions.Count);
            Assert.Equal(new PdfRect(30, 40, 80, 60), reopened.AcroForm.Fields[0].Rect);
            Assert.Equal(180, reopened.AcroForm.Fields[0].PageRotation);
        }

        JsonElement report = Pypdf(saved);
        Assert.True(report.GetProperty("direct").GetBoolean());
        Assert.Equal(180, report.GetProperty("rotation").GetInt32());
        Assert.Equal(90, report.GetProperty("parentRotation").GetInt32());
        Assert.Equal(new[] { 10D, 20D, 210D, 320D }, Doubles(report, "mediaBox"));
        Assert.Equal(new[] { 20D, 30D, 180D, 280D }, Doubles(report, "cropBox"));
        Assert.Equal(new[] { 30D, 40D, 80D, 60D }, Doubles(report, "widgetRect"));

        Assert.Equal((250, 160), RenderedSize(original));
        Assert.Equal((160, 250), RenderedSize(saved));
    }

    [Theory]
    [InlineData(-720, 0)]
    [InlineData(-450, 270)]
    [InlineData(0, 0)]
    [InlineData(360, 0)]
    [InlineData(450, 90)]
    [InlineData(540, 180)]
    [InlineData(630, 270)]
    public void rotation_normalizes_every_multiple_of_ninety(int value, int expected)
    {
        using var document = PdfDocument.Open(new MemoryStream(PageDocument(inheritedRotation: 90)), leaveOpen: true);
        using var editor = document.Edit();

        editor.Pages[0].Rotation = value;

        Assert.Equal(expected, document.Pages[0].Rotation);
    }

    [Fact]
    public void invalid_rotation_and_an_equal_direct_rotation_leave_the_document_clean()
    {
        byte[] pdf = PageDocument(inheritedRotation: 0, directRotation: 90);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Pages[0].Rotation = 45);
        editor.Pages[0].Rotation = 450;
        editor.SaveIncremental();

        Assert.Equal(pdf.Length, stream.Length);
        Assert.Equal(90, document.Pages[0].Rotation);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void docmdp_blocks_rotation_at_every_level_even_with_the_opt_in(int permission)
    {
        byte[] pdf = PageDocument(
            inheritedRotation: 0,
            catalogExtra: " /Perms << /DocMDP 7 0 R >>",
            extras: new[]
            {
                (7, "<< /Type /Sig /Reference [8 0 R] >>"),
                (8, "<< /TransformMethod /DocMDP /TransformParams << /P " + permission.ToString() + " /V /1.2 >> >>"),
            });
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();
        editor.AllowBreakingSignatures = true;

        Assert.Throws<PdfCertificationLockedException>(() => editor.Pages[0].Rotation = 90);
        AssertUnchanged(editor, stream, pdf);
    }

    [Theory]
    [InlineData("approval")]
    [InlineData("document timestamp")]
    public void a_signature_requires_the_opt_in_and_keeps_usage_rights(string kind)
    {
        byte[] pdf = SignedPageDocument(kind);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.True(document.HasUsageRights);
        Assert.Throws<PdfSignatureConflictException>(() => editor.Pages[0].Rotation = 90);
        AssertUnchanged(editor, stream, pdf);

        editor.AllowBreakingSignatures = true;
        editor.Pages[0].Rotation = 90;
        Assert.True(document.HasUsageRights);
        editor.SaveIncremental();

        Assert.True(stream.ToArray().AsSpan(0, pdf.Length).SequenceEqual(pdf));
        Assert.Equal(90, document.Pages[0].Rotation);
    }

    [Fact]
    public void rotation_removes_usage_rights_on_an_unsigned_document()
    {
        byte[] pdf = PageDocument(
            inheritedRotation: 0,
            catalogExtra: " /Perms << /UR3 8 0 R >>",
            extras: new[]
            {
                (8, "<< /Type /Sig /Reference [9 0 R] >>"),
                (9, "<< /TransformMethod /UR3 /TransformParams << /V /2.2 /Document [/FullSave] >> >>"),
            });
        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        using var editor = document.Edit();

        Assert.True(document.HasUsageRights);
        editor.Pages[0].Rotation = 90;

        Assert.False(document.HasUsageRights);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("256", "owner")]
    [InlineData("256", "user")]
    [InlineData("128-rc4", "owner")]
    [InlineData("128-rc4", "user")]
    public void encrypted_rotation_keeps_encrypt_and_id_and_reads_in_qpdf_and_pypdf(string cipher, string password)
    {
        byte[] pdf = QpdfEncrypt(PageDocument(inheritedRotation: 0), cipher);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, password, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Pages[0].Rotation = 270;
            editor.SaveIncremental();
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string appendix = System.Text.Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("/Encrypt ", appendix, StringComparison.Ordinal);
        Assert.Contains("/ID ", appendix, StringComparison.Ordinal);
        AssertQpdfCheck(saved, password);
        Assert.Equal(270, Pypdf(saved, password).GetProperty("rotation").GetInt32());
        using var reopened = PdfDocument.Open(new MemoryStream(saved), password, leaveOpen: true);
        Assert.Equal(270, reopened.Pages[0].Rotation);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.External)]
    public void user_password_without_modify_refuses_and_the_owner_can_rotate()
    {
        byte[] pdf = QpdfEncrypt(PageDocument(inheritedRotation: 0), "256", "--modify-other=n");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, "user", leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Throws<PdfEncryptionRestrictedException>(() => editor.Pages[0].Rotation = 90);
            AssertUnchanged(editor, stream, pdf);
        }

        using (var document = PdfDocument.Open(stream, "owner", leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Pages[0].Rotation = 90;
            editor.SaveIncremental();
        }

        Assert.True(stream.Length > pdf.Length);
    }

    private static PdfFieldSnapshot FieldSnapshot(PdfDocument document)
    {
        var field = Assert.Single(document.AcroForm.Fields);
        return new PdfFieldSnapshot(field.Rect, field.MediaBox);
    }

    private static byte[] PageDocument(
        int inheritedRotation,
        int? directRotation = null,
        string catalogExtra = "",
        string additionalFields = "",
        (int Id, string Body)[]? extras = null)
    {
        string rotate = directRotation.HasValue ? " /Rotate " + directRotation.Value.ToString() : string.Empty;
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R" + additionalFields + "] >>" + catalogExtra + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] /Resources << >> /MediaBox [10 20 210 320] /CropBox [20 30 180 280] /Rotate " + inheritedRotation.ToString() + " >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /Contents 5 0 R /Annots [4 0 R" + additionalFields + "]" + rotate + " >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [30 40 80 60] /P 3 0 R >>");
        builder.StreamObject(5, string.Empty, Array.Empty<byte>());
        if (extras != null)
        {
            foreach ((int id, string body) in extras)
            {
                builder.Indirect(id, body);
            }
        }

        return builder.Finish();
    }

    private static byte[] SignedPageDocument(string kind)
    {
        string signature = kind == "approval"
            ? "<< /Type /Sig /Reference [10 0 R] >>"
            : "<< /Type /DocTimeStamp /SubFilter /ETSI.RFC3161 >>";
        return PageDocument(
            inheritedRotation: 0,
            catalogExtra: " /Perms << /UR3 8 0 R >>",
            additionalFields: " 6 0 R",
            extras: new[]
            {
                (6, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signed) /V 7 0 R /Rect [100 40 150 60] /P 3 0 R >>"),
                (7, signature),
                (8, "<< /Type /Sig /Reference [9 0 R] >>"),
                (9, "<< /TransformMethod /UR3 /TransformParams << /V /2.2 /Document [/FullSave] >> >>"),
                (10, "<< /TransformMethod /FieldMDP /TransformParams << /Action /All /V /1.2 >> >>"),
            });
    }

    private static void AssertUnchanged(PdfDocumentEditor editor, MemoryStream stream, byte[] pdf)
    {
        editor.SaveIncremental();
        Assert.Equal(pdf.Length, stream.Length);
    }

    private static JsonElement Pypdf(byte[] pdf, string? password = null)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "rotated.pdf");
            File.WriteAllBytes(path, pdf);
            string python = ExternalTool.Python();
            using Process process = password == null
                ? ExternalTool.Start(python, "-c", PypdfScript, path)
                : ExternalTool.Start(python, "-c", PypdfScript, path, password);
            using JsonDocument report = JsonDocument.Parse(ExternalTool.Output(process));
            return report.RootElement.Clone();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static double[] Doubles(JsonElement report, string property)
    {
        var values = new List<double>();
        foreach (JsonElement value in report.GetProperty(property).EnumerateArray())
        {
            values.Add(value.GetDouble());
        }

        return values.ToArray();
    }

    private static (int Width, int Height) RenderedSize(byte[] pdf)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "page.pdf");
            string prefix = Path.Combine(directory, "page");
            File.WriteAllBytes(path, pdf);
            string pdftoppm = LocatePoppler("JAYTWO_PDFTOPPM", "pdftoppm");
            using Process process = ExternalTool.Start(pdftoppm, "-cropbox", "-r", "72", "-f", "1", "-l", "1", "-singlefile", path, prefix);
            ExternalTool.Output(process);
            byte[] ppm = File.ReadAllBytes(prefix + ".ppm");
            int index = 2;
            int width = ReadPpmNumber(ppm, ref index);
            int height = ReadPpmNumber(ppm, ref index);
            return (width, height);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string LocatePoppler(string variable, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("JAYTWO_POPPLER");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, ExternalTool.Executable(name));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return ExternalTool.Locate(variable, ExternalTool.Executable(name));
    }

    private static int ReadPpmNumber(byte[] ppm, ref int index)
    {
        while (index < ppm.Length && (ppm[index] == ' ' || ppm[index] == '\n' || ppm[index] == '\r' || ppm[index] == '\t'))
        {
            index++;
        }

        int value = 0;
        while (index < ppm.Length && ppm[index] >= '0' && ppm[index] <= '9')
        {
            value = (value * 10) + (ppm[index] - '0');
            index++;
        }

        return value;
    }

    private static byte[] QpdfEncrypt(byte[] pdf, string cipher, string? restriction = null)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string input = Path.Combine(directory, "plain.pdf");
            string output = Path.Combine(directory, "encrypted.pdf");
            File.WriteAllBytes(input, pdf);
            var args = new List<string> { "--allow-weak-crypto", "--encrypt", "user", "owner" };
            args.AddRange(cipher == "128-rc4" ? new[] { "128", "--use-aes=n" } : new[] { "256" });
            if (restriction != null)
            {
                args.Add(restriction);
            }

            args.AddRange(new[] { "--", input, output });
            using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), args.ToArray());
            ExternalTool.Output(process);
            return File.ReadAllBytes(output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertQpdfCheck(byte[] pdf, string password)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "rotated.pdf");
            File.WriteAllBytes(path, pdf);
            using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), "--password=" + password, "--check", path);
            ExternalTool.Output(process);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private readonly record struct PdfFieldSnapshot(PdfRect Rect, PdfRect MediaBox);
}
