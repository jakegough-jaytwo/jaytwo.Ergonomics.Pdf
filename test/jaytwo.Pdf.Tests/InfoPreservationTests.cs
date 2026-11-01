using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-226: an incremental save after an edit that does not touch metadata carries the existing Info dictionary forward,
// whether the source's trailer holds it as an indirect object or as a direct dictionary. The original bytes stay a prefix.
public class InfoPreservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void an_unrelated_edit_keeps_the_info_dictionary_through_save_and_reopen(bool direct)
    {
        byte[] original = Pdf(direct);
        byte[] saved = SaveAfterAttachment(original);

        Assert.True(saved.Length > original.Length);
        Assert.True(saved.Take(original.Length).SequenceEqual(original), "The original bytes are not a prefix.");
        AssertInfo(saved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void a_save_with_no_edit_keeps_the_info_dictionary(bool direct)
    {
        byte[] original = Pdf(direct);
        using var output = new MemoryStream();
        using (var document = PdfDocument.Open(new MemoryStream(original)))
        {
            using var editor = document.Edit();
            editor.SaveIncremental(output, leaveOpen: true);
        }

        Assert.Equal(original, output.ToArray());
        AssertInfo(output.ToArray());
    }

    // The loader decrypts a direct trailer Info as object 0, generation 0, so a source in that shape is built the same way.
    [Fact]
    public void an_encrypted_document_keeps_a_direct_info_dictionary_readable_after_an_unrelated_edit()
    {
        byte[] original = EncryptedDirectInfo();
        byte[] saved = SaveAfterAttachment(original, "user");

        Assert.True(saved.Take(original.Length).SequenceEqual(original), "The original bytes are not a prefix.");
        using var again = PdfDocument.Open(new MemoryStream(saved), "user");
        Assert.Equal("Old", again.Metadata.Title);
    }

    // ISO 32000-1 Table 15: the trailer's /Info is an indirect reference. The saved revision carries the values in a new
    // indirect object, which qpdf reads (and, when encrypted, decrypts under that object's own identity).
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "user")]
    public void qpdf_reads_the_carried_info_through_an_indirect_trailer_reference(bool direct, string? password)
    {
        byte[] original = password == null ? Pdf(direct) : EncryptedDirectInfo();
        byte[] saved = SaveAfterAttachment(original, password);
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "saved.pdf");
            File.WriteAllBytes(path, saved);
            string[] auth = password == null ? new string[0] : new[] { "--password=" + password };

            Assert.Equal(0, Qpdf(auth.Concat(new[] { "--check", path }), out string check));

            Assert.Equal(0, Qpdf(auth.Concat(new[] { "--show-object=trailer", path }), out string trailer));
            Match reference = Regex.Match(trailer, @"/Info (\d+) 0 R");
            Assert.True(reference.Success, "The trailer's /Info is not an indirect reference: " + trailer);

            Assert.Equal(0, Qpdf(auth.Concat(new[] { "--show-object=" + reference.Groups[1].Value, path }), out string info));
            Assert.Contains("Old", info, StringComparison.Ordinal);
            if (password == null)
            {
                Assert.Contains("Custom", info, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static int Qpdf(System.Collections.Generic.IEnumerable<string> args, out string output)
    {
        using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), args.ToArray());
        (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
        output = stdout + stderr;
        return exitCode;
    }

    private static byte[] EncryptedDirectInfo()
    {
        var crypt = new StandardEncryptor(StandardEncryptor.Kind.Revision4Aes, "user", "owner", -4, encryptMetadata: true);
        PdfBuilder builder = PdfBuilder.Basic(pageExtra: " /Resources << >>");
        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Info << /Title " + crypt.Text("Old", 0) + " >> /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private static byte[] SaveAfterAttachment(byte[] original, string? password = null)
    {
        using var output = new MemoryStream();
        using (var document = password == null ? PdfDocument.Open(new MemoryStream(original)) : PdfDocument.Open(new MemoryStream(original), password))
        {
            using var editor = document.Edit();
            editor.Attachments.Add("kept.txt", new MemoryStream(Encoding.ASCII.GetBytes("kept")));
            editor.SaveIncremental(output, leaveOpen: true);
        }

        return output.ToArray();
    }

    private static void AssertInfo(byte[] saved)
    {
        using var again = PdfDocument.Open(new MemoryStream(saved));
        Assert.Equal("Old", again.Metadata.Title);
        Assert.Equal("Someone", again.Metadata.Author);
        Assert.Equal("value", again.Metadata["Custom"]);
    }

    private static byte[] Pdf(bool direct)
    {
        PdfBuilder builder = PdfBuilder.Basic(pageExtra: " /Resources << >>");
        const string Info = "<< /Title (Old) /Author (Someone) /Custom (value) >>";
        if (direct)
        {
            return builder.Finish("/Info " + Info);
        }

        builder.Indirect(4, Info);
        return builder.Finish("/Info 4 0 R");
    }
}
