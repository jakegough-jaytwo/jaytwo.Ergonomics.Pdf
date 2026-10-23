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
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// W-73, spec R10. pyHanko supplies the independent xref-section and signature-field view.
public class RevisionTests
{
    public static IEnumerable<object[]> Ids => CorpusManifest.Ids();

    [Theory]
    [MemberData(nameof(Ids))]
    public void corpus_revisions_agree_with_pyhanko(string id)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        if (entry.Open != "ok")
        {
            return;
        }

        AssertReadersAgree(entry.FullPath, entry.Password);
    }

    [Theory]
    [InlineData("frb-beige-book-2024-01", 1)]
    [InlineData("irs-fw9-2024-03", 2)]
    public void linearized_sections_are_one_logical_revision(string id, int revisions)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        using var stream = File.OpenRead(entry.FullPath);
        using var document = PdfDocument.Open(stream, entry.Password, leaveOpen: true);

        Assert.Equal(revisions, document.Revisions.Count);
        Assert.Equal(stream.Length, document.Revisions[revisions - 1].EndOffset);
    }

    [Fact]
    public void signature_and_later_update_report_logical_revision_numbers()
    {
        byte[] original = File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf"));
        using var key = RSA.Create(2048);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Revision Test", key);
        byte[] signed = Sign(original, new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)));
        byte[] revised;
        using (var stream = new MemoryStream(signed, writable: false))
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        using (var destination = new MemoryStream())
        {
            document.Metadata.Title = "After signature";
            document.SaveIncremental(destination, leaveOpen: true);
            revised = destination.ToArray();
        }

        using var source = new MemoryStream(revised, writable: false);
        using var opened = PdfDocument.Open(source, leaveOpen: true);
        Assert.Collection(
            opened.Revisions,
            revision => AssertRevision(revision, 1, original.Length, false),
            revision => AssertRevision(revision, 2, signed.Length, true),
            revision => AssertRevision(revision, 3, revised.Length, false));
        PdfSignatureInfo signature = Assert.Single(opened.Signatures);
        Assert.Equal(2, signature.SignedRevisionNumber);
        Assert.Equal(signed.Length, signature.SignedRevisionEnd);

        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "revised.pdf");
            File.WriteAllBytes(path, revised);
            AssertReadersAgree(path, null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void hybrid_xref_stream_is_part_of_its_classic_revision()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (Base) >>");
        long baseXref = builder.Length;
        builder.Finish("/Info 4 0 R");
        long baseEnd = builder.Length;
        builder.Indirect(4, "<< /Title (Stream) >>");
        long streamAt = builder.AppendXrefStream(5, "/Info 4 0 R");
        builder.Indirect(4, "<< /Title (Classic) >>");
        long hybridXref = builder.Length;
        builder.Ascii("xref\n4 1\n" + builder.OffsetOf(4).ToString("D10", CultureInfo.InvariantCulture) + " 00000 n\r\n"
            + "trailer\n<< /Size 6 /Root 1 0 R /Info 4 0 R /Prev " + baseXref.ToString(CultureInfo.InvariantCulture)
            + " /XRefStm " + streamAt.ToString(CultureInfo.InvariantCulture) + " >>\nstartxref\n" + hybridXref.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");

        using var stream = new MemoryStream(builder.Snapshot(), writable: false);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        Assert.Collection(
            document.Revisions,
            revision => AssertRevision(revision, 1, baseEnd, false),
            revision => AssertRevision(revision, 2, stream.Length, false));
    }

    [Fact]
    public void malformed_historical_footer_only_blocks_revision_listing()
    {
        PdfBuilder builder = PdfBuilder.Basic();
        long baseXref = builder.Length;
        byte[] original = builder.Finish();
        int eof = LastIndexOf(original, Encoding.ASCII.GetBytes("%%EOF"));
        original[eof + 4] = (byte)'X';
        using var output = new MemoryStream();
        output.Write(original, 0, original.Length);
        long objectOffset = output.Length;
        WriteAscii(output, "4 0 obj\n<< >>\nendobj\n");
        long xref = output.Length;
        WriteAscii(output, "xref\n4 1\n" + objectOffset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n\r\n"
            + "trailer\n<< /Size 5 /Root 1 0 R /Prev " + baseXref.ToString(CultureInfo.InvariantCulture) + " >>\nstartxref\n"
            + xref.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");

        using var source = new MemoryStream(output.ToArray(), writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);

        Assert.Throws<PdfParseException>(() => document.Revisions);
        Assert.Equal(1, document.PageCount);
    }

    private static void AssertReadersAgree(string path, string? password)
    {
        IReadOnlyList<PdfRevision> revisions;
        IReadOnlyList<PdfSignatureInfo> signatures;
        using (var stream = File.OpenRead(path))
        using (var document = PdfDocument.Open(stream, password, leaveOpen: true))
        {
            revisions = document.Revisions;
            signatures = document.Signatures;
        }

        using JsonDocument report = Inspect(path, password);
        JsonElement expected = report.RootElement.GetProperty("revisions");
        Assert.Equal(expected.GetArrayLength(), revisions.Count);
        for (int i = 0; i < revisions.Count; i++)
        {
            JsonElement revision = expected[i];
            AssertRevision(
                revisions[i],
                revision.GetProperty("number").GetInt32(),
                revision.GetProperty("endOffset").GetInt64(),
                revision.GetProperty("covered").GetBoolean());
        }

        JsonElement signatureRevisions = report.RootElement.GetProperty("signatureRevisions");
        foreach (PdfSignatureInfo signature in signatures.Where(item => item.IsSigned))
        {
            JsonElement number = signatureRevisions.GetProperty(signature.FieldName);
            Assert.Equal(number.ValueKind == JsonValueKind.Null ? null : number.GetInt32(), signature.SignedRevisionNumber);
        }
    }

    private static JsonDocument Inspect(string path, string? password)
    {
        var args = new List<string> { ExternalTool.Fixture("inspect_revisions.py"), path };
        if (password != null)
        {
            args.Add(password);
        }

        using Process process = ExternalTool.Start(ExternalTool.Python(), args.ToArray());
        return JsonDocument.Parse(ExternalTool.Output(process));
    }

    private static byte[] Sign(byte[] pdf, IPdfSignatureProvider provider)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var destination = new MemoryStream();
        document.Sign(destination, new PdfSignatureOptions(), provider, leaveOpen: true);
        return destination.ToArray();
    }

    private static void AssertRevision(PdfRevision revision, int number, long endOffset, bool covered)
    {
        Assert.Equal(number, revision.Number);
        Assert.Equal(endOffset, revision.EndOffset);
        Assert.Equal(covered, revision.IsCoveredBySignature);
    }

    private static int LastIndexOf(byte[] data, byte[] value)
    {
        for (int i = data.Length - value.Length; i >= 0; i--)
        {
            if (data.Skip(i).Take(value.Length).SequenceEqual(value))
            {
                return i;
            }
        }

        throw new InvalidOperationException("Value not found.");
    }

    private static void WriteAscii(Stream stream, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
