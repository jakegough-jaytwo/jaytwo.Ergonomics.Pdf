using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using jaytwo.Ergonomics.Pdf.AcroForm;
using jaytwo.Ergonomics.Pdf.Signatures;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class ForeignPdfTests
{
    [Fact]
    public void qpdf_object_stream_is_read_revised_and_accepted()
    {
        byte[] original = ReadFixture("qpdf-object-streams.pdf");
        string structure = Encoding.ASCII.GetString(original);
        Assert.Contains("/ObjStm", structure, StringComparison.Ordinal);
        Assert.Contains("/XRef", structure, StringComparison.Ordinal);

        byte[] revised = ReviseTitle(original, null, "Foreign base", "Revised by library");
        AssertPrefix(revised, original);
        using (var stream = new MemoryStream(revised, writable: false))
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal("Revised by library", document.Metadata.Title);
            Assert.Equal("pypdf", document.Metadata.Author);
            Assert.Equal(1, document.PageCount);
        }

        Check(revised, null);
    }

    [Fact]
    public void qpdf_encrypted_pdf_is_revised_signed_and_accepted()
    {
        byte[] original = ReadFixture("qpdf-encrypted.pdf");
        using (var rejected = new MemoryStream(original, writable: false))
        {
            Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(rejected, "wrong-password", leaveOpen: true));
        }

        using var rsa = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(rsa);
        byte[] signed = Sign(original, "user-secret", "Foreign base", "Sealed", certificate, rsa, null);
        AssertPrefix(signed, original);

        using (var stream = new MemoryStream(signed, writable: false))
        using (var document = PdfDocument.Open(stream, "user-secret", leaveOpen: true))
        {
            Assert.Equal("Sealed", document.Metadata.Title);
            Assert.True(document.Permissions.CanModify);
        }

        Check(signed, "user-secret");
        Validate(signed, certificate, "user-secret", "entire");
    }

    [Fact]
    public void qpdf_attachment_survives_an_incremental_revision()
    {
        byte[] original = ReadFixture("qpdf-attachment.pdf");
        Assert.Equal("foreign-attachment", ReadAttachment(original, null, "note.txt"));

        byte[] revised = ReviseTitle(original, null, "Foreign base", "With attachment");
        AssertPrefix(revised, original);
        Assert.Equal("foreign-attachment", ReadAttachment(revised, null, "note.txt"));
        Check(revised, null);
    }

    [Fact]
    public void qpdf_rewrite_with_indirect_filters_is_read_and_decodes_as_qpdf_decodes_it()
    {
        // W-40: qpdf keeps the indirect /Filter and /DecodeParms of each attachment stream, moves the referenced names,
        // arrays, and dictionaries into an object stream, and decodes each attachment itself for comparison.
        byte[] plain = Encoding.ASCII.GetBytes("attachment-bytes");
        byte[] predicted = new byte[20];
        for (int row = 0; row < 4; row++)
        {
            Buffer.BlockCopy(plain, row * 4, predicted, (row * 5) + 1, 4);
        }

        var builder = PdfBuilder.Basic(" /Names << /EmbeddedFiles << /Names [(array.txt) 9 0 R (name.txt) 6 0 R] >> >>");
        builder.StreamObject(5, "/Type /EmbeddedFile /Filter 7 0 R", TestCompat.Zlib(plain));
        builder.Indirect(6, "<< /Type /Filespec /UF (name.txt) /EF << /F 5 0 R >> >>");
        builder.Indirect(7, "/FlateDecode");
        builder.StreamObject(8, "/Type /EmbeddedFile /Filter 10 0 R /DecodeParms 11 0 R", Encoding.ASCII.GetBytes(TestCompat.ToHexString(TestCompat.Zlib(predicted)) + ">"));
        builder.Indirect(9, "<< /Type /Filespec /UF (array.txt) /EF << /F 8 0 R >> >>");
        builder.Indirect(10, "[/ASCIIHexDecode 12 0 R]");
        builder.Indirect(11, "[null << /Predictor 12 /Columns 13 0 R >>]");
        builder.Indirect(12, "/FlateDecode");
        builder.Indirect(13, "4");
        string input = WriteTemp(builder.Finish());
        string output = WriteTemp(Array.Empty<byte>());
        try
        {
            Run(LocateQpdf(), "--stream-data=preserve", "--object-streams=generate", input, output);
            byte[] rewritten = File.ReadAllBytes(output);
            string text = TestCompat.Latin1.GetString(rewritten);
            Assert.Contains("/ObjStm", text, StringComparison.Ordinal);
            Assert.Equal(2, Regex.Matches(text, @"/Filter \d+ 0 R").Count);
            Assert.Single(Regex.Matches(text, @"/DecodeParms \d+ 0 R"));

            foreach (string name in new[] { "name.txt", "array.txt" })
            {
                Assert.Equal(plain, Run(LocateQpdf(), "--show-attachment=" + name, output));
                using var document = PdfDocument.Open(new MemoryStream(rewritten, writable: false), leaveOpen: true);
                using Stream content = document.Attachments[name].Open();
                using var copy = new MemoryStream();
                content.CopyTo(copy);
                Assert.Equal(plain, copy.ToArray());
            }
        }
        finally
        {
            File.Delete(input);
            File.Delete(output);
        }
    }

    [Fact]
    public void pypdf_form_fill_is_checked_by_qpdf()
    {
        byte[] original = ReadFixture("pypdf-acroform.pdf");
        using var stream = new MemoryStream();
        stream.Write(original, 0, original.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada"));
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Agree", true));
            Assert.False(document.AcroForm.NeedAppearances);
            document.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        AssertPrefix(saved, original);
        using (var again = new MemoryStream(saved, writable: false))
        using (var document = PdfDocument.Open(again, leaveOpen: true))
        {
            Assert.Equal("Ada", document.AcroForm.GetFields("Name")[0].Value);
            PdfField box = document.AcroForm.GetFields("Agree")[0];
            Assert.Equal(PdfFieldType.Button, box.FieldType);
            Assert.True(box.IsChecked);
            Assert.False(document.AcroForm.NeedAppearances);
        }

        Check(saved, null);
    }

    [Fact]
    public void pypdf_free_undefined_and_stale_generation_references_are_null()
    {
        // pypdf writes the deleted field as a free entry. qpdf and pypdf both read the free, undefined, and
        // generation-1 references in /Fields as null and report only Name (ISO 32000-1 7.3.10).
        byte[] original = ReadFixture("pypdf-dangling.pdf");
        using var stream = new MemoryStream();
        stream.Write(original, 0, original.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            PdfField field = Assert.Single(document.AcroForm.Fields);
            Assert.Equal("Name", field.FullName);
            Assert.Equal(1, field.PageNumber);
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada"));
            document.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        AssertPrefix(saved, original);
        using (var again = new MemoryStream(saved, writable: false))
        using (var document = PdfDocument.Open(again, leaveOpen: true))
        {
            Assert.Equal("Ada", Assert.Single(document.AcroForm.Fields).Value);
        }

        Check(saved, null);
    }

    [Fact]
    public void pyhanko_signature_stays_intact_after_an_incremental_append()
    {
        byte[] original = ReadFixture("pyhanko-signed.pdf");
        byte[] basis = ReadFixture("pypdf-base.pdf");
        AssertPrefix(original, basis);

        byte[] revised = ReviseTitle(original, null, "Foreign base", "After signature");
        AssertPrefix(revised, original);
        Check(revised, null);
        Validate(revised, null, null, "revision");
    }

    [Fact]
    public void pyhanko_signature_field_is_signed_and_accepted()
    {
        byte[] original = ReadFixture("pyhanko-sigfield.pdf");
        using (var stream = new MemoryStream(original, writable: false))
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            PdfField field = document.AcroForm.GetFields("ExistingSig")[0];
            Assert.Equal(PdfFieldType.Signature, field.FieldType);
            Assert.Null(field.Value);
        }

        using var rsa = RSA.Create(2048);
        using X509Certificate2 certificate = CreateCertificate(rsa);
        byte[] signed = Sign(original, null, "Foreign base", null, certificate, rsa, "ExistingSig");
        AssertPrefix(signed, original);
        Check(signed, null);
        Validate(signed, certificate, null, "entire");
    }

    private static byte[] ReviseTitle(byte[] pdf, string? password, string expectedTitle, string title)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        Assert.Equal(expectedTitle, document.Metadata.Title);
        document.Metadata.Title = title;
        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);
        return destination.ToArray();
    }

    private static byte[] Sign(
        byte[] pdf,
        string? password,
        string expectedTitle,
        string? title,
        X509Certificate2 certificate,
        RSA rsa,
        string? fieldName)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        Assert.Equal(expectedTitle, document.Metadata.Title);
        if (title != null)
        {
            document.Metadata.Title = title;
        }

        var destination = new MemoryStream();
        document.Sign(
            destination,
            new PdfSignatureOptions
            {
                FieldName = fieldName,
                ReservedContentsBytes = 16384,
                SigningTime = TestCompat.UnixEpoch,
            },
            new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData)),
            leaveOpen: true);
        return destination.ToArray();
    }

    private static string ReadAttachment(byte[] pdf, string? password, string name)
    {
        using var stream = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(stream, password, leaveOpen: true);
        using Stream content = document.Attachments[name].Open();
        using var reader = new StreamReader(content, Encoding.ASCII);
        return reader.ReadToEnd();
    }

    private static void Check(byte[] pdf, string? password)
    {
        string path = WriteTemp(pdf);
        try
        {
            string qpdf = LocateQpdf();
            using Process process = password == null
                ? Start(qpdf, "--check", path)
                : Start(qpdf, "--password=" + password, "--check", path);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                "qpdf --check exited " + process.ExitCode.ToString() + Environment.NewLine + stderr + stdout);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Validate(byte[] pdf, X509Certificate2? certificate, string? password, string coverage)
    {
        string pdfPath = WriteTemp(pdf);
        string certPath = Path.Combine(Path.GetTempPath(), "jaytwo-ergonomics-pdf-" + Guid.NewGuid().ToString("N") + ".pem");
        try
        {
            if (certificate != null)
            {
                File.WriteAllText(certPath, ToPem(certificate.RawData));
            }

            string script = Path.Combine(AppContext.BaseDirectory, "Fixtures", "validate_signature.py");
            using Process process = Start(
                "python",
                script,
                pdfPath,
                certificate == null ? "-" : certPath,
                password ?? "-",
                coverage);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                "pyHanko validation exited " + process.ExitCode.ToString() + Environment.NewLine + stderr + stdout);
        }
        finally
        {
            File.Delete(pdfPath);
            if (File.Exists(certPath))
            {
                File.Delete(certPath);
            }
        }
    }

    private static byte[] Run(string fileName, params string[] args)
    {
        using Process process = Start(fileName, args);
        using var stdout = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(stdout);
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            Path.GetFileName(fileName) + " " + string.Join(" ", args) + " exited " + process.ExitCode.ToString() + Environment.NewLine + stderr);
        return stdout.ToArray();
    }

    private static string LocateQpdf()
    {
        return ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
    }

    private static X509Certificate2 CreateCertificate(RSA rsa)
    {
        var request = new CertificateRequest("CN=Library Interop", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static string ToPem(byte[] der)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-----BEGIN CERTIFICATE-----");
        string base64 = Convert.ToBase64String(der);
        for (int offset = 0; offset < base64.Length; offset += 64)
        {
            int count = Math.Min(64, base64.Length - offset);
            builder.AppendLine(base64.Substring(offset, count));
        }

        builder.AppendLine("-----END CERTIFICATE-----");
        return builder.ToString();
    }

    private static void AssertPrefix(byte[] actual, byte[] prefix)
    {
        Assert.True(actual.Length > prefix.Length);
        Assert.True(actual.AsSpan(0, prefix.Length).SequenceEqual(prefix));
    }

    private static byte[] ReadFixture(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        Assert.True(File.Exists(path), path);
        return File.ReadAllBytes(path);
    }

    private static string WriteTemp(byte[] pdf)
    {
        string path = Path.Combine(Path.GetTempPath(), "jaytwo-ergonomics-pdf-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(path, pdf);
        return path;
    }

    private static Process Start(string fileName, params string[] args)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        for (int i = 0; i < args.Length; i++)
        {
            TestCompat.AddArgument(info, args[i]);
        }

        Process? process = Process.Start(info);
        if (process == null)
        {
            throw new InvalidOperationException(fileName + " did not start.");
        }

        return process;
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
