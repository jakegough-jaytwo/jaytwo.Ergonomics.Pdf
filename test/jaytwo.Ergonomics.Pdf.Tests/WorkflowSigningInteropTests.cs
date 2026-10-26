#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// W-181: independent PDF-aware verification of the public workflow terminals.
public class WorkflowSigningInteropTests
{
    public static IEnumerable<object[]> Algorithms()
    {
        foreach (string key in new[] { TestKeys.Rsa, TestKeys.P256, TestKeys.P384 })
        {
            foreach (PdfDigestAlgorithm digest in new[] { PdfDigestAlgorithm.Sha256, PdfDigestAlgorithm.Sha384, PdfDigestAlgorithm.Sha512 })
            {
                foreach (PdfSignatureFormat format in new[] { PdfSignatureFormat.AdbePkcs7Detached, PdfSignatureFormat.PadesBaselineB })
                {
                    yield return new object[] { key, digest, format };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Algorithms))]
    [Trait(Lane.Trait, Lane.Interop)]
    public void both_verifiers_accept_every_supported_key_digest_and_format(string keyKind, PdfDigestAlgorithm digest, PdfSignatureFormat format)
    {
        using var directory = new TempDirectory();
        using AsymmetricAlgorithm key = TestKeys.Create(keyKind);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Workflow Interop", key);
        var signer = new KeySigner(key, certificate.RawData);
        byte[] input = WorkflowPdf.WithInfo();
        byte[] signed = Sign(input, null, signer, new PdfSignatureOptions { DigestAlgorithm = digest, Format = format });
        WorkflowPdf.AssertPrefix(input, signed);
        string checks = format == PdfSignatureFormat.PadesBaselineB ? "pades-b" : string.Empty;
        string report = Verify(directory, signed, certificate.RawData, null, "entire", checks, true, keyKind + "-" + digest + "-" + format);
        string bits = digest.ToString().Substring(3);
        Assert.Contains("md_algorithm=sha" + bits, report, StringComparison.Ordinal);
        Assert.Contains("mechanism=sha" + bits + (keyKind == TestKeys.Rsa ? "_rsa" : "_ecdsa"), report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait(Lane.Trait, Lane.Interop)]
    public void signature_timestamps_verify_on_new_and_existing_fields(bool existing)
    {
        using var directory = new TempDirectory();
        string openssl = OpenSslTimestampAuthority.Locate();
        OpenSslTimestampAuthority.CreatePki(openssl, directory.Path);
        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.P256);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Workflow Timestamp", key);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Approval) /Rect [72 72 220 120] /P 3 0 R /F 4 >>");
        byte[] input = existing ? builder.Finish() : WorkflowPdf.WithInfo();
        byte[] signed = Sign(input, null, new KeySigner(key, certificate.RawData), new PdfSignatureOptions { FieldName = existing ? "Approval" : null, Format = PdfSignatureFormat.PadesBaselineB }, new OpenSslTimestampAuthority(openssl, directory.Path));
        string report = Verify(directory, signed, certificate.RawData, null, "entire", "pades-t,timestamp,tsa=" + directory.File("root.pem"), true, "timestamp-" + existing);
        Assert.Contains("timestamp_intact=true", report, StringComparison.Ordinal);
        Assert.Contains("timestamp_valid=true", report, StringComparison.Ordinal);
        Assert.Contains("timestamp_trusted=true", report, StringComparison.Ordinal);
        WorkflowPdf.AssertPrefix(input, signed);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("owner")]
    [Trait(Lane.Trait, Lane.Interop)]
    public void encrypted_workflow_edits_and_signs_with_both_password_roles(string password)
    {
        using var directory = new TempDirectory();
        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.Rsa);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Workflow Encrypted", key);
        byte[] input = WorkflowPdf.EncryptedForm();
        using var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(input), new PdfWorkflowOpenOptions { Password = password })
            .Fill("Name", "Grace")
            .Sign(output, new KeySigner(key, certificate.RawData), new PdfSignatureOptions());
        byte[] signed = output.ToArray();
        WorkflowPdf.AssertPrefix(input, signed);
        Verify(directory, signed, certificate.RawData, password, "entire", string.Empty, true, "encrypted-" + password);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void a_permitted_later_fill_keeps_the_signature_and_both_verifiers_detect_tampering()
    {
        using var directory = new TempDirectory();
        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.Rsa);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Workflow Revision", key);
        byte[] signed = Sign(WorkflowPdf.Form(), null, new KeySigner(key, certificate.RawData), new PdfSignatureOptions());
        using var revised = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(signed)).Fill("Name", "Grace").Save(revised);
        byte[] output = revised.ToArray();
        WorkflowPdf.AssertPrefix(signed, output);
        Verify(directory, output, certificate.RawData, null, "revision", "docmdp-ok,modification=FORM_FILLING", false, "later-fill");

        byte[] tampered = (byte[])signed.Clone();
        int marker = Encoding.ASCII.GetString(tampered).IndexOf("Header", StringComparison.Ordinal);
        Assert.True(marker >= 0);
        tampered[marker] ^= 1;
        string path = directory.Write("tampered.pdf", tampered);
        string trust = directory.Write("trust.pem", Encoding.ASCII.GetBytes(ExternalTool.Pem(certificate.RawData)));
        using Process python = ExternalTool.Start(ExternalTool.Python(), ExternalTool.Fixture("validate_signature.py"), path, trust, "-", "entire", "broken");
        string report = ExternalTool.Output(python);
        Assert.Contains("intact=false", report, StringComparison.Ordinal);
        (int _, string poppler) = Pdfsig.Verify(tampered, new[] { certificate.RawData }, null);
        Assert.Contains(Pdfsig.DigestMismatch, Assert.Single(Pdfsig.Signatures(poppler)));
        Retain(directory, "tamper", report, poppler);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait(Lane.Trait, Lane.Interop)]
    public void a_document_timestamp_is_a_separate_revision_validated_by_pyhanko_and_openssl(bool existing)
    {
        using var directory = new TempDirectory();
        string openssl = OpenSslTimestampAuthority.Locate();
        OpenSslTimestampAuthority.CreatePki(openssl, directory.Path);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Timestamp) /Rect [0 0 0 0] /P 3 0 R >>");
        byte[] input = existing ? builder.Finish() : WorkflowPdf.WithInfo();
        using var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(input)).WithTitle("Timestamped").Timestamp(output, new OpenSslTimestampAuthority(openssl, directory.Path), new PdfSignatureOptions { FieldName = existing ? "Timestamp" : null });
        byte[] stamped = output.ToArray();
        WorkflowPdf.AssertPrefix(input, stamped);
        using PdfDocument original = WorkflowPdf.Open(input);
        using PdfDocument result = WorkflowPdf.Open(stamped);
        Assert.Equal(original.Revisions.Count + 2, result.Revisions.Count);
        string path = directory.Write("document-timestamp.pdf", stamped);
        using Process python = ExternalTool.Start(ExternalTool.Python(), ExternalTool.Fixture("validate_doc_timestamp.py"), path, directory.File("root.pem"));
        string report = ExternalTool.Output(python);
        Assert.Contains("intact=true", report, StringComparison.Ordinal);
        Assert.Contains("valid=true", report, StringComparison.Ordinal);
        Assert.Contains("trusted=true", report, StringComparison.Ordinal);
        Assert.Contains("coverage=ENTIRE_FILE", report, StringComparison.Ordinal);
        string text = Encoding.ASCII.GetString(stamped);
        Match range = Regex.Match(text, @"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]");
        Assert.True(range.Success);
        int firstLength = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
        int secondStart = int.Parse(range.Groups[3].Value, CultureInfo.InvariantCulture);
        int secondLength = int.Parse(range.Groups[4].Value, CultureInfo.InvariantCulture);
        Assert.Equal(stamped.Length, secondStart + secondLength);
        byte[] padded = Convert.FromHexString(text.Substring(firstLength, secondStart - firstLength).Trim('<', '>'));
        byte[] token = new AsnReader(padded, AsnEncodingRules.BER).ReadEncodedValue().ToArray();
        string tokenPath = directory.Write("token.tst", token);
        string rangePath = directory.File("range.bin");
        using (var ranges = File.Create(rangePath))
        {
            ranges.Write(stamped, 0, firstLength);
            ranges.Write(stamped, secondStart, secondLength);
        }

        using Process verify = ExternalTool.Start(openssl, "ts", "-verify", "-data", rangePath, "-in", tokenPath, "-token_in", "-CAfile", directory.File("root.pem"));
        (int exit, string stdout, string stderr) = ExternalTool.Run(verify);
        Assert.True(exit == 0, stdout + stderr);
        File.WriteAllText(directory.File("openssl.txt"), stdout + stderr);
        Retain(directory, "document-timestamp-" + existing, report, "Poppler does not validate ETSI.RFC3161 document timestamps.");
    }

    private static byte[] Sign(byte[] input, string? password, IPdfExternalSigner signer, PdfSignatureOptions options, ITimestampAuthority? authority = null)
    {
        using var output = new MemoryStream();
        PdfWorkflow.From(new MemoryStream(input), new PdfWorkflowOpenOptions { Password = password }).Sign(output, signer, options, authority);
        return output.ToArray();
    }

    private static string Verify(TempDirectory directory, byte[] pdf, byte[] trust, string? password, string coverage, string checks, bool entire, string name)
    {
        string path = directory.Write("signed.pdf", pdf);
        string root = directory.Write("trust.pem", Encoding.ASCII.GetBytes(ExternalTool.Pem(trust)));
        using Process python = ExternalTool.Start(ExternalTool.Python(), ExternalTool.Fixture("validate_signature.py"), path, root, password ?? "-", coverage, checks);
        string report = ExternalTool.Output(python);
        Assert.Contains("intact=true", report, StringComparison.Ordinal);
        Assert.Contains("valid=true", report, StringComparison.Ordinal);
        Assert.Contains("trusted=true", report, StringComparison.Ordinal);
        // Pdfsig's helper uses -upw; signing with the owner role does not change this fixture's user password.
        (int exit, string poppler) = Pdfsig.Verify(pdf, new[] { trust }, password == "owner" ? "user" : password);
        Assert.Equal(0, exit);
        List<string> signature = Assert.Single(Pdfsig.Signatures(poppler));
        Assert.Contains(Pdfsig.Valid, signature);
        Assert.Contains(Pdfsig.Trusted, signature);
        Assert.Contains(Pdfsig.SignedRanges(pdf), signature);
        Assert.Contains(entire ? Pdfsig.WholeFile : Pdfsig.NotWholeFile, signature);
        Retain(directory, name, report, poppler);
        return report;
    }

    private static void Retain(TempDirectory directory, string name, string pyhanko, string poppler)
    {
        string? root = Environment.GetEnvironmentVariable("JAYTWO_R4_EVIDENCE");
        if (root == null)
        {
            return;
        }

        string destination = Path.Combine(root, name);
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(directory.Path))
        {
            if (file.EndsWith(".pdf", StringComparison.Ordinal) || file.EndsWith(".pem", StringComparison.Ordinal) || file.EndsWith(".txt", StringComparison.Ordinal) || file.EndsWith(".tst", StringComparison.Ordinal) || file.EndsWith(".bin", StringComparison.Ordinal))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
            }
        }

        File.WriteAllText(Path.Combine(destination, "pyhanko.txt"), pyhanko);
        File.WriteAllText(Path.Combine(destination, "pdfsig.txt"), poppler);
        File.WriteAllText(Path.Combine(destination, "produced-utc.txt"), DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }
}
#endif
