using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-112: iText Core 9.8.0 reads back every write path's output. W-117: on classic sources, and on the same sources
// rewritten by qpdf with object streams, whose last section is a cross-reference stream and so is each update (D-56).
public class ITextInteropTests
{
    private const string XmpPacket =
        "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>"
        + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
        + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
        + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">XmpEdited</rdf:li></rdf:Alt></dc:title>"
        + "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    private const int FormPages = 10;

    public static IEnumerable<object[]> Shapes()
    {
        yield return new object[] { "classic" };
        yield return new object[] { "xref-stream" };
    }

    public static IEnumerable<object[]> SigningCases()
    {
        foreach (string shape in new[] { "classic", "xref-stream" })
        {
            foreach (string key in new[] { TestKeys.Rsa, TestKeys.P256, TestKeys.P384 })
            {
                foreach (PdfSignatureFormat format in new[] { PdfSignatureFormat.AdbePkcs7Detached, PdfSignatureFormat.PadesBaselineB })
                {
                    yield return new object[] { shape, key, format };
                }
            }
        }
    }

    public static IEnumerable<object[]> EncryptedCases()
    {
        foreach (string shape in new[] { "classic", "xref-stream" })
        {
            foreach (string cipher in new[] { "256-aes", "128-rc4" })
            {
                foreach (string password in new[] { "user", "owner" })
                {
                    yield return new object[] { shape, cipher, password };
                }
            }
        }
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [MemberData(nameof(Shapes))]
    public void itext_reads_info_xmp_attachment_and_rotation_edits(string shape)
    {
        byte[] attachment = { 0x00, 0x01, 0x02, 0x7F, 0x80, 0xFE, 0xFF, 0x42, 0x2D, 0x32, 0x33 };
        byte[] source = Shape(PdfBuilder.Basic(pageExtra: " /Resources << >>").Finish(), shape);
        using var content = new MemoryStream(attachment, writable: false);
        byte[] output = Edit(source, null, editor =>
        {
            editor.Metadata.Title = "InfoEdited";
            editor.Metadata.SetXmp(Encoding.UTF8.GetBytes(XmpPacket));
            editor.Attachments.Add("notes.bin", content, leaveOpen: true);
            editor.Pages[0].Rotation = 90;
        });

        ITextReading reading = IText.Read(output);
        Assert.Equal("InfoEdited", reading.Title);
        Assert.Contains("XmpEdited", reading.Xmp, StringComparison.Ordinal);
        Assert.Equal(attachment, reading.Attachments["notes.bin"]);
        Assert.Equal(new[] { 90 }, reading.Rotations);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [MemberData(nameof(Shapes))]
    public void itext_reads_a_single_fill_a_bulk_fill_and_a_flatten(string shape)
    {
        byte[] source = Shape(Form(), shape);
        byte[] single = Edit(source, null, editor => editor.AcroForm.Fill("page1.name", "Ada"));
        ITextReading singleReading = IText.Read(single);
        Assert.Equal("Ada", singleReading.Fields["page1.name"]);
        Assert.Equal("Name 2", singleReading.Fields["page2.name"]);

        byte[] bulk = Edit(source, null, editor => editor.AcroForm.Fill(G7Fixtures.TextValues(FormPages)));
        ITextReading bulkReading = IText.Read(bulk);
        Assert.Equal(FormPages * 2, bulkReading.Fields.Count);
        for (int page = 1; page <= FormPages; page++)
        {
            Assert.Equal("Filled " + page.ToString(System.Globalization.CultureInfo.InvariantCulture), bulkReading.Fields["page" + page.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".name"]);
        }

        byte[] flattened = Edit(bulk, null, editor => editor.AcroForm.Flatten());
        ITextReading flatReading = IText.Read(flattened);
        Assert.Equal(FormPages, flatReading.Pages);
        Assert.Empty(flatReading.Fields);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [MemberData(nameof(SigningCases))]
    public void itext_verifies_signatures_and_the_first_signature_after_a_later_revision(string shape, string key, PdfSignatureFormat format)
    {
        using AsymmetricAlgorithm signingKey = TestKeys.Create(key);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=iText interop", signingKey);
        byte[] signed = Sign(Shape(PdfBuilder.Basic(pageExtra: " /Resources << >>").Finish(), shape), null, signingKey, certificate, format);
        ITextSignature signature = Assert.Single(IText.Read(signed).Signatures).Value;
        Assert.True(signature.Intact, shape + " " + key + " " + format + ": iText " + IText.Version + " does not verify the signature");
        Assert.True(signature.CoversWholeDocument);
        Assert.Equal("W-112", signature.Reason);

        byte[] revised = Edit(signed, null, editor => editor.Metadata.Title = "After signing");
        ITextReading reading = IText.Read(revised);
        Assert.Equal("After signing", reading.Title);
        ITextSignature earlier = Assert.Single(reading.Signatures).Value;
        Assert.True(earlier.Intact, shape + " " + key + " " + format + ": the signature is not intact after a later revision");
        Assert.False(earlier.CoversWholeDocument);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [MemberData(nameof(EncryptedCases))]
    public void itext_reads_fills_and_verifies_signatures_on_encrypted_files(string shape, string cipher, string password)
    {
        byte[] source = QpdfEncrypt(Form(), shape, cipher);
        byte[] filled = Edit(source, password, editor => editor.AcroForm.Fill("page1.name", "Ada"));
        Assert.Equal("Ada", IText.Read(filled, password).Fields["page1.name"]);

        using AsymmetricAlgorithm signingKey = TestKeys.Create(TestKeys.Rsa);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=iText interop", signingKey);
        byte[] signed = Sign(filled, password, signingKey, certificate, PdfSignatureFormat.AdbePkcs7Detached);
        ITextReading reading = IText.Read(signed, password);
        Assert.Equal("Ada", reading.Fields["page1.name"]);
        ITextSignature signature = Assert.Single(reading.Signatures).Value;
        Assert.True(signature.Intact, shape + " " + cipher + " " + password + ": iText " + IText.Version + " does not verify the signature");
        Assert.True(signature.CoversWholeDocument);
    }

    // The G7 form shape at ten pages, with a classic cross-reference table.
    private static byte[] Form()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "form.pdf");
            G7Fixtures.WriteForm(path, FormPages, "Form");
            return File.ReadAllBytes(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] Edit(byte[] pdf, string? password, Action<PdfDocumentEditor> edit)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        using var editor = document.Edit();
        edit(editor);
        var destination = new MemoryStream();
        editor.SaveIncremental(destination, leaveOpen: true);
        byte[] output = destination.ToArray();
        Assert.True(output.AsSpan(0, pdf.Length).SequenceEqual(pdf), "the original is not a prefix");
        Assert.Equal(IText.EndsInClassicXref(pdf), IText.EndsInClassicXref(output));
        return output;
    }

    private static byte[] Sign(byte[] pdf, string? password, AsymmetricAlgorithm key, X509Certificate2 certificate, PdfSignatureFormat format)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        using var editor = document.Edit();
        var destination = new MemoryStream();
        var options = new PdfSignatureOptions { Reason = "W-112", Format = format };
        editor.Sign(destination, options, new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)), leaveOpen: true);
        byte[] output = destination.ToArray();
        Assert.Equal(IText.EndsInClassicXref(pdf), IText.EndsInClassicXref(output));
        return output;
    }

    // A classic source as it is, or rewritten by qpdf with object streams, which ends it in a cross-reference stream.
    private static byte[] Shape(byte[] pdf, string shape)
    {
        byte[] shaped = shape == "classic" ? pdf : Qpdf(pdf, "--object-streams=generate");
        Assert.True(IText.EndsInClassicXref(shaped) == (shape == "classic"), shape + ": the last section is not that shape");
        return shaped;
    }

    // qpdf's 128-bit RC4 is revision 3 and its AES-256 revision 6. User password "user", owner password "owner".
    private static byte[] QpdfEncrypt(byte[] pdf, string shape, string cipher)
    {
        var args = new List<string>();
        if (shape != "classic")
        {
            args.Add("--object-streams=generate");
        }

        args.AddRange(new[] { "--allow-weak-crypto", "--encrypt", "user", "owner" });
        args.AddRange(cipher == "128-rc4" ? new[] { "128", "--use-aes=n" } : new[] { "256" });
        args.Add("--");
        byte[] encrypted = Qpdf(pdf, args.ToArray());
        Assert.True(IText.EndsInClassicXref(encrypted) == (shape == "classic"), shape + " " + cipher + ": the last section is not that shape");
        return encrypted;
    }

    private static byte[] Qpdf(byte[] pdf, params string[] options)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string input = Path.Combine(directory, "input.pdf");
            string output = Path.Combine(directory, "output.pdf");
            File.WriteAllBytes(input, pdf);
            var args = new List<string>(options) { input, output };
            using System.Diagnostics.Process process = ExternalTool.Start(CorpusManifest.Qpdf(), args.ToArray());
            (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
            Assert.True(exitCode == 0, stderr + stdout);
            return File.ReadAllBytes(output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
