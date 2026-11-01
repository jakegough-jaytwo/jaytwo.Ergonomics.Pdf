using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

    // The loader decrypts a direct trailer Info as object 0, generation 0; the writer encrypts it the same way.
    [Fact]
    public void an_encrypted_document_keeps_a_direct_info_dictionary_readable_after_an_unrelated_edit()
    {
        var crypt = new StandardEncryptor(StandardEncryptor.Kind.Revision4Aes, "user", "owner", -4, encryptMetadata: true);
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(20, crypt.Dictionary(null));
        byte[] original = builder.Finish("/Info << /Title " + crypt.Text("Old", 0) + " >> /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");

        using var output = new MemoryStream();
        using (var document = PdfDocument.Open(new MemoryStream(original), "user"))
        {
            using var editor = document.Edit();
            editor.Attachments.Add("kept.txt", new MemoryStream(Encoding.ASCII.GetBytes("kept")));
            editor.SaveIncremental(output, leaveOpen: true);
        }

        using var again = PdfDocument.Open(new MemoryStream(output.ToArray()), "user");
        Assert.Equal("Old", again.Metadata.Title);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void qpdf_reads_the_carried_direct_info_in_the_saved_trailer()
    {
        byte[] saved = SaveAfterAttachment(Pdf(direct: true));
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "saved.pdf");
            File.WriteAllBytes(path, saved);
            using Process check = ExternalTool.Start(CorpusManifest.Qpdf(), new[] { "--check", path });
            (int checkExit, string checkOut, string checkErr) = ExternalTool.Run(check);
            Assert.True(checkExit == 0, "qpdf --check: " + checkErr + checkOut);

            using Process show = ExternalTool.Start(CorpusManifest.Qpdf(), new[] { "--show-object=trailer", path });
            (int showExit, string trailer, string showErr) = ExternalTool.Run(show);
            Assert.True(showExit == 0, showErr + trailer);
            Assert.Contains("/Info", trailer, StringComparison.Ordinal);
            Assert.Contains("Old", trailer, StringComparison.Ordinal);
            Assert.Contains("Custom", trailer, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] SaveAfterAttachment(byte[] original)
    {
        using var output = new MemoryStream();
        using (var document = PdfDocument.Open(new MemoryStream(original)))
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
