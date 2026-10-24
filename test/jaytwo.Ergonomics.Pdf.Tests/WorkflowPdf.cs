using System.IO;
using jaytwo.Pdf;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Small PDFs for the workflow tests, built with the engine tests' PdfBuilder.
internal static class WorkflowPdf
{
    public static byte[] WithInfo()
    {
        var builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (Original) /Author (Ada) /Subject (Orders) /Keywords (one two) /Creator (Writer) /Producer (Press) >>");
        return builder.Finish("/Info 4 0 R");
    }

    // A document padded with an unreferenced stream, so copying it takes several of the engine's 1 MiB blocks.
    public static byte[] Large(int padding)
    {
        var builder = PdfBuilder.Basic();
        var data = new byte[padding];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)('a' + (i % 26));
        }

        builder.StreamObject(4, string.Empty, data);
        builder.Indirect(5, "<< /Title (Large) >>");
        return builder.Finish("/Info 5 0 R");
    }

    // An RC4 file that opens with the empty user password and allows form filling only, so a metadata edit is refused
    // when it is applied (the pattern of EncryptionTests.Document).
    public static byte[] Restricted()
    {
        var crypt = new StandardEncryptor(false, string.Empty, "owner", StandardEncryptor.FillOnly(), encryptMetadata: true);
        var builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    public static PdfDocument Open(byte[] pdf)
    {
        return PdfDocument.Open(new MemoryStream(pdf), leaveOpen: false);
    }

    public static void AssertPrefix(byte[] input, byte[] output)
    {
        Assert.True(output.Length >= input.Length, "The output is shorter than the input.");
        int differs = -1;
        for (int i = 0; i < input.Length && differs < 0; i++)
        {
            differs = input[i] == output[i] ? -1 : i;
        }

        Assert.True(differs < 0, "The output differs from the input at offset " + differs + ".");
    }

    // Asserts the output appends one revision with the given title and keeps the input as an exact prefix.
    public static void AssertTitled(byte[] input, byte[] output, string title)
    {
        AssertPrefix(input, output);
        Assert.True(output.Length > input.Length, "No revision was appended.");
        using PdfDocument original = Open(input);
        using PdfDocument edited = Open(output);
        Assert.Equal(original.Revisions.Count + 1, edited.Revisions.Count);
        Assert.Equal(title, edited.Metadata.Title);
    }
}
