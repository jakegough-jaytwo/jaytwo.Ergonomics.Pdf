using System.IO;
using System.Text;
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

    // A one-page form: text fields Name and City, check box Agree, read-only Locked, and Code with /MaxLen 3. With a
    // signature dictionary it also has a signed signature field, Sig, whose /V is object 30 (the pattern of
    // FlattenTests.BuildForm).
    public static byte[] Form(string catalogExtra = "", string? signature = null)
    {
        string sig = signature != null ? " 9 0 R" : string.Empty;
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R 7 0 R 8 0 R" + sig + "] /DA (/Helv 0 Tf 0 g) >>" + catalogExtra + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 20 0 R /Annots [4 0 R 5 0 R 6 0 R 7 0 R 8 0 R" + sig + "] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 280 724] /DA (/Helv 12 Tf 0 g) /F 4 /P 3 0 R >>");
        builder.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (City) /Rect [72 660 280 684] /DA (/Helv 12 Tf 0 g) /F 4 /P 3 0 R >>");
        builder.Indirect(6, "<< /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /Rect [72 620 90 638] /F 4 /P 3 0 R >>");
        builder.Indirect(7, "<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 1 /T (Locked) /Rect [72 580 280 604] /DA (/Helv 12 Tf 0 g) /F 4 /P 3 0 R >>");
        builder.Indirect(8, "<< /Type /Annot /Subtype /Widget /FT /Tx /MaxLen 3 /T (Code) /Rect [72 540 280 564] /DA (/Helv 12 Tf 0 g) /F 4 /P 3 0 R >>");
        if (signature != null)
        {
            builder.Indirect(9, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Sig) /V 30 0 R /Rect [300 40 500 80] /F 4 /P 3 0 R >>");
            builder.Indirect(30, signature);
        }

        builder.StreamObject(20, string.Empty, Encoding.ASCII.GetBytes("BT /F1 18 Tf 72 750 Td (Header) Tj ET"));
        return builder.Finish();
    }

    // A form with an approval signature.
    public static byte[] SignedForm()
    {
        return Form(signature: "<< /Type /Sig >>");
    }

    // A form certified with DocMDP P 2, which allows fills and refuses flatten.
    public static byte[] CertifiedForm()
    {
        return Form(" /Perms << /DocMDP 30 0 R >>", "<< /Type /Sig /Reference [<< /TransformMethod /DocMDP /TransformParams << /P 2 /V /1.2 >> >>] >>");
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
