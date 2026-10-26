#if NET10_0_OR_GREATER
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using jaytwo.Pdf.Signatures;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Linked into workflow tests: all document access here is through the public engine API.
internal static class DssCases
{
    public static void Run(
        string origin,
        string keyKind,
        PdfDigestAlgorithm digest,
        bool timestamp,
        string? password,
        bool controls,
        Func<byte[], string?, PdfSignatureOptions, IPdfExternalSigner, ITimestampAuthority?, byte[]> sign)
    {
        string caseName = origin + "-" + keyKind + "-" + digest + (timestamp ? "-bt" : "-bb") + (password == null ? string.Empty : "-" + password) + (controls ? "-controls" : string.Empty);
        string evidence = Environment.GetEnvironmentVariable("JAYTWO_DSS_EVIDENCE") ?? Path.Combine(AppContext.BaseDirectory, "dss-evidence");
        string directory = Path.Combine(evidence, caseName);
        Directory.CreateDirectory(directory);
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=DSS Local Test CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
        using X509Certificate2 root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3));
        WriteTrust(directory, "signer-root", root);
        using AsymmetricAlgorithm key = TestKeys.Create(keyKind);
        CertificateRequest leafRequest = TestKeys.Request("CN=DSS Local Signer", key);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        using X509Certificate2 leaf = leafRequest.Create(root.SubjectName, X509SignatureGenerator.CreateForRSA(rootKey, RSASignaturePadding.Pkcs1), DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
        var signer = new KeySigner(key, leaf.RawData, new[] { root.RawData }, null);
        byte[] input = Input();
        File.WriteAllBytes(Path.Combine(directory, "input.pdf"), input);
        if (password != null)
        {
            string encrypted = Path.Combine(directory, "encrypted.pdf");
            using Process qpdf = ExternalTool.Start(ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf")), "--encrypt", "user", "owner", "256", "--", Path.Combine(directory, "input.pdf"), encrypted);
            ExternalTool.Output(qpdf);
            input = File.ReadAllBytes(encrypted);
        }

        string tsaDirectory = ExternalTool.TempDirectory();
        try
        {
            ITimestampAuthority? authority = null;
            if (timestamp || controls)
            {
                string openssl = OpenSslTimestampAuthority.Locate();
                OpenSslTimestampAuthority.CreatePki(openssl, tsaDirectory);
                using X509Certificate2 tsaRoot = X509Certificate2.CreateFromPemFile(Path.Combine(tsaDirectory, "root.pem"), Path.Combine(tsaDirectory, "root.key"));
                WriteTrust(directory, "tsa-root", tsaRoot);
                authority = new OpenSslTimestampAuthority(openssl, tsaDirectory);
            }

            var options = new PdfSignatureOptions { Format = PdfSignatureFormat.PadesBaselineB, DigestAlgorithm = digest, SigningTime = DateTimeOffset.UtcNow, ReservedContentsBytes = 32768 };
            byte[] signed = sign(input, password, options, signer, timestamp ? authority : null);
            Assert.True(signed.AsSpan(0, input.Length).SequenceEqual(input));
            JsonElement result = Validate(directory, "positive", signed, password);
            AssertGood(result, timestamp ? "PAdES_BASELINE_T" : "PAdES_BASELINE_B");
            Assert.Equal(digest.ToString().ToUpperInvariant(), result.GetProperty("digest").GetString());
            Assert.Equal(keyKind == TestKeys.Rsa ? "RSA" : "ECDSA", result.GetProperty("encryption").GetString());
            if (timestamp)
            {
                JsonElement stamp = Assert.Single(result.GetProperty("timestamps").EnumerateArray());
                Assert.True(stamp.GetProperty("signatureIntact").GetBoolean());
                Assert.True(stamp.GetProperty("signatureValid").GetBoolean());
                Assert.True(stamp.GetProperty("imprintIntact").GetBoolean());
                Assert.True(stamp.GetProperty("trustedChain").GetBoolean());
                // DSS uses PASSED for a timestamp building block, TOTAL_PASSED for the signature conclusion.
                Assert.Equal("PASSED", stamp.GetProperty("indication").GetString());
            }
            else
            {
                Assert.Empty(result.GetProperty("timestamps").EnumerateArray());
            }

            if (controls)
            {
                var legacyOptions = new PdfSignatureOptions { Format = PdfSignatureFormat.AdbePkcs7Detached, DigestAlgorithm = digest, SigningTime = options.SigningTime, ReservedContentsBytes = 32768 };
                JsonElement legacy = Validate(directory, "legacy", sign(input, null, legacyOptions, signer, null), null);
                AssertGood(legacy, "PKCS7_B");
                byte[] changed = (byte[])signed.Clone();
                int marker = Encoding.ASCII.GetString(changed).IndexOf("DSS signed marker", StringComparison.Ordinal);
                Assert.True(marker >= 0);
                changed[marker] = (byte)'X';
                JsonElement tampered = Validate(directory, "modified-byte", changed, null);
                Assert.False(tampered.GetProperty("digestIntact").GetBoolean());
                Assert.Equal("TOTAL_FAILED", tampered.GetProperty("indication").GetString());
                var corruptSigner = new KeySigner(key, leaf.RawData, new[] { root.RawData }, value =>
                {
                    value[value.Length / 2] ^= 1;
                    return value;
                });
                JsonElement corrupt = Validate(directory, "corrupt-signature", sign(input, null, options, corruptSigner, null), null);
                Assert.False(corrupt.GetProperty("signatureIntact").GetBoolean());
                Assert.Equal("TOTAL_FAILED", corrupt.GetProperty("indication").GetString());
                byte[] first = sign(input, null, options, signer, authority);
                byte[] secondInput = (byte[])input.Clone();
                secondInput[marker] = (byte)'Y';
                byte[] second = sign(secondInput, null, options, signer, authority);
                JsonElement mismatch = Validate(directory, "mismatched-timestamp", ReplaceTimestamp(first, second), null);
                Assert.True(mismatch.GetProperty("signatureIntact").GetBoolean());
                Assert.True(mismatch.GetProperty("digestIntact").GetBoolean());
                JsonElement badStamp = Assert.Single(mismatch.GetProperty("timestamps").EnumerateArray());
                Assert.True(badStamp.GetProperty("signatureIntact").GetBoolean());
                Assert.False(badStamp.GetProperty("imprintIntact").GetBoolean());
                Assert.False(badStamp.GetProperty("signatureValid").GetBoolean());
                Assert.Equal("FAILED", badStamp.GetProperty("indication").GetString());
                Assert.Equal("HASH_FAILURE", badStamp.GetProperty("subIndication").GetString());
                // DSS-6.3 limitation, recorded in the W-103 G4 evidence: the structural profile remains T
                // despite HASH_FAILURE. Never use that label or the base-signature conclusion as a timestamp pass.
                Assert.Equal("PAdES_BASELINE_T", mismatch.GetProperty("profile").GetString());
            }
        }
        finally
        {
            Directory.Delete(tsaDirectory, recursive: true);
        }
    }

    private static byte[] Input()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /DssMarker (DSS signed marker) >>");
        return builder.Finish();
    }

    private static void WriteTrust(string directory, string name, X509Certificate2 root)
    {
        File.WriteAllBytes(Path.Combine(directory, name + ".cer"), root.RawData);
        var crl = new CertificateRevocationListBuilder();
        File.WriteAllBytes(Path.Combine(directory, name + ".crl"), crl.Build(root, BigInteger.One, DateTimeOffset.UtcNow.AddDays(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, DateTimeOffset.UtcNow));
    }

    private static JsonElement Validate(string directory, string name, byte[] pdf, string? password)
    {
        string path = Path.Combine(directory, name + ".pdf");
        string reports = Path.Combine(directory, name);
        File.WriteAllBytes(path, pdf);
        Directory.CreateDirectory(reports);
        string java = ExternalTool.Locate("JAYTWO_JAVA", ExternalTool.Executable("java"));
        string wrapper = ExternalTool.Locate("JAYTWO_DSS", "DssValidate.java");
        string time = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O", CultureInfo.InvariantCulture);
        File.WriteAllText(Path.Combine(reports, "fixture.json"), JsonSerializer.Serialize(new
        {
            inputSha256 = Convert.ToHexString(SHA256.HashData(pdf)),
            producingCommit = Environment.GetEnvironmentVariable("JAYTWO_EVIDENCE_COMMIT") ?? "not-recorded",
            validationTime = time,
            passwordRole = password ?? "unencrypted",
            anchors = Directory.GetFiles(directory, "*.cer").Select(file => new { name = Path.GetFileName(file), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) }).ToArray(),
            crls = Directory.GetFiles(directory, "*.crl").Select(file => new { name = Path.GetFileName(file), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) }).ToArray(),
        }));
        using Process process = ExternalTool.Start(java, "--class-path", Path.Combine(Path.GetDirectoryName(wrapper)!, "lib", "*"), wrapper, path, reports, directory, time, password ?? "-");
        (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
        File.WriteAllText(Path.Combine(reports, "stdout.txt"), stdout);
        File.WriteAllText(Path.Combine(reports, "stderr.txt"), stderr);
        File.WriteAllText(Path.Combine(reports, "exit-code.txt"), exitCode.ToString(CultureInfo.InvariantCulture));
        Assert.True(exitCode == 0, "DSS exit " + exitCode.ToString(CultureInfo.InvariantCulture) + ": " + stderr + " Reports: " + reports);
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(Path.Combine(reports, "result.json")));
        Assert.Equal("completed", report.RootElement.GetProperty("execution").GetString());
        return Assert.Single(report.RootElement.GetProperty("signatures").EnumerateArray()).Clone();
    }

    private static void AssertGood(JsonElement signature, string profile)
    {
        Assert.Equal(profile, signature.GetProperty("profile").GetString());
        Assert.True(signature.GetProperty("signatureIntact").GetBoolean());
        Assert.True(signature.GetProperty("signatureValid").GetBoolean());
        Assert.True(signature.GetProperty("digestIntact").GetBoolean());
        Assert.True(signature.GetProperty("trustedChain").GetBoolean());
        Assert.Equal("TOTAL_PASSED", signature.GetProperty("indication").GetString());
    }

    private static byte[] ReplaceTimestamp(byte[] first, byte[] second)
    {
        const string oid = "1.2.840.113549.1.9.16.2.14";
        Match target = Regex.Match(Encoding.ASCII.GetString(first), @"/Contents <([0-9A-Fa-f]+)>");
        Match source = Regex.Match(Encoding.ASCII.GetString(second), @"/Contents <([0-9A-Fa-f]+)>");
        var cms = new SignedCms();
        cms.Decode(Convert.FromHexString(target.Groups[1].Value));
        var other = new SignedCms();
        other.Decode(Convert.FromHexString(source.Groups[1].Value));
        SignerInfo signer = cms.SignerInfos[0];
        signer.RemoveUnsignedAttribute(signer.UnsignedAttributes.Cast<CryptographicAttributeObject>().Single(attribute => attribute.Oid.Value == oid).Values[0]);
        signer.AddUnsignedAttribute(other.SignerInfos[0].UnsignedAttributes.Cast<CryptographicAttributeObject>().Single(attribute => attribute.Oid.Value == oid).Values[0]);
        string encoded = Convert.ToHexString(cms.Encode());
        Assert.True(encoded.Length <= target.Groups[1].Length);
        byte[] result = (byte[])first.Clone();
        Encoding.ASCII.GetBytes(encoded.PadRight(target.Groups[1].Length, '0')).CopyTo(result, target.Groups[1].Index);
        return result;
    }
}
#endif
