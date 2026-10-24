using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-72, spec R9: PdfDocument.Signatures agrees with two readers written elsewhere. pyHanko 0.38.0 with asn1crypto
// (Fixtures/inspect_signatures.py) reads every reported property; pdfsig reads the field names, signed state, signer
// common name, sub-filter, hash algorithm, signed ranges, and whole-file coverage. Neither result is a validation.
public class SignatureInspectionInteropTests
{
    private static readonly DateTimeOffset SigningTime = new DateTimeOffset(2026, 10, 21, 9, 30, 15, TimeSpan.FromHours(-6));

    public static IEnumerable<object[]> Ids => CorpusManifest.Ids();

    public static TheoryData<string, PdfDigestAlgorithm, PdfSignatureFormat> OwnSignatures
    {
        get
        {
            var data = new TheoryData<string, PdfDigestAlgorithm, PdfSignatureFormat>();
            foreach (PdfSignatureFormat format in new[] { PdfSignatureFormat.AdbePkcs7Detached, PdfSignatureFormat.PadesBaselineB })
            {
                data.Add(TestKeys.Rsa, PdfDigestAlgorithm.Sha256, format);
                data.Add(TestKeys.P256, PdfDigestAlgorithm.Sha384, format);
                data.Add(TestKeys.P384, PdfDigestAlgorithm.Sha512, format);
            }

            return data;
        }
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [Trait(Lane.Trait, Lane.Corpus)]
    [MemberData(nameof(Ids))]
    public void corpus_inspection_agrees_with_pyhanko_and_pdfsig(string id)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        if (entry.Open != "ok")
        {
            return;
        }

        AssertReadersAgree(File.ReadAllBytes(entry.FullPath), entry.Password);
    }

    // The corpus has three signed fields. Each is pinned here so a corpus change cannot silently drop the coverage.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [Trait(Lane.Trait, Lane.Corpus)]
    [InlineData("gpo-bills-118hr815enr", "USGPOSignature", 1)]
    [InlineData("opm-of306-pyhanko-certified", "CorpusCertification", 2)]
    [InlineData("cms-40b-pyhanko-approved", "Signature-1", null)]
    public void corpus_signatures_are_found(string id, string fieldName, int? certificationLevel)
    {
        CorpusEntry entry = CorpusManifest.Get(id);
        IReadOnlyList<PdfSignatureInfo> signatures = AssertReadersAgree(File.ReadAllBytes(entry.FullPath), entry.Password);

        PdfSignatureInfo signature = Assert.Single(signatures, s => s.HasSignatureDictionary);
        Assert.Equal(fieldName, signature.FieldName);
        Assert.Equal(certificationLevel, signature.CertificationLevel);
        Assert.NotNull(signature.SignerCertificate);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("pyhanko-signed.pdf")]
    [InlineData("pyhanko-sigfield.pdf")]
    public void pyhanko_fixture_inspection_agrees(string name)
    {
        AssertReadersAgree(File.ReadAllBytes(ExternalTool.Fixture(name)), null);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [MemberData(nameof(OwnSignatures))]
    public void own_signature_inspection_agrees(string keyKind, PdfDigestAlgorithm digestAlgorithm, PdfSignatureFormat format)
    {
        using var rootKey = RSA.Create(2048);
        using X509Certificate2 root = Authority("Inspection Interop Root", rootKey);
        using AsymmetricAlgorithm leafKey = TestKeys.Create(keyKind);
        using X509Certificate2 leaf = Leaf("Inspection Interop Leaf", leafKey, root);
        var signer = new KeySigner(leafKey, leaf.RawData, new[] { root.RawData }, null);
        byte[] signed = Sign(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), null, new CmsDetachedSignatureProvider(signer), new PdfSignatureOptions
        {
            SigningTime = SigningTime,
            DigestAlgorithm = digestAlgorithm,
            Format = format,
            Reason = "Inspection",
            Location = "Denver",
            ContactInfo = "signer@example.test",
            SignerName = "Ada Signer",
        });

        PdfSignatureInfo signature = Assert.Single(AssertReadersAgree(signed, null));
        Assert.Equal(leaf.RawData, signature.SignerCertificate);
        Assert.Equal(format == PdfSignatureFormat.PadesBaselineB ? null : SigningTime, signature.CmsSigningTime);
        Assert.False(signature.HasLaterRevisions);
    }

    // A PAdES B-T signature with an OpenSSL token, then an OpenSSL document timestamp in a later revision.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void timestamp_tokens_and_document_timestamps_agree()
    {
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            using var key = RSA.Create(2048);
            using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Inspection Timestamped", key);
            byte[] signed = Sign(
                File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")),
                null,
                new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData), new OpenSslTimestampAuthority(openssl, directory)),
                new PdfSignatureOptions { ReservedContentsBytes = 32768, Format = PdfSignatureFormat.PadesBaselineB });
            byte[] stamped = Sign(
                signed,
                null,
                new DocTimeStampSignatureProvider(new OpenSslTimestampAuthority(openssl, directory)),
                new PdfSignatureOptions { ReservedContentsBytes = 32768, Kind = PdfSignatureKind.DocTimeStamp });

            IReadOnlyList<PdfSignatureInfo> signatures = AssertReadersAgree(stamped, null);

            Assert.Equal(2, signatures.Count);
            Assert.NotNull(signatures[0].TimestampTime);
            Assert.True(signatures[0].HasLaterRevisions);
            Assert.Equal(signed.Length, signatures[0].ByteRangeEnd);
            Assert.Equal("ETSI.RFC3161", signatures[1].SubFilter);
            Assert.NotNull(signatures[1].TimestampTime);
            Assert.False(signatures[1].HasLaterRevisions);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void signature_followed_by_a_metadata_revision_agrees()
    {
        using var key = RSA.Create(2048);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Inspection Revised", key);
        byte[] signed = Sign(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), null, new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)), new PdfSignatureOptions());
        using var source = new MemoryStream(signed, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        editor.Metadata.Title = "Revised";
        var destination = new MemoryStream();
        editor.SaveIncremental(destination, leaveOpen: true);

        PdfSignatureInfo signature = Assert.Single(AssertReadersAgree(destination.ToArray(), null));
        Assert.True(signature.HasLaterRevisions);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void signature_on_an_aes256_file_opened_with_the_user_password_agrees()
    {
        using var key = RSA.Create(2048);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Inspection Encrypted", key);
        byte[] encrypted = QpdfEncrypt(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")));
        byte[] signed = Sign(encrypted, "user-secret", new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)), new PdfSignatureOptions
        {
            SigningTime = SigningTime,
            Reason = "Encrypted reason",
            Location = "Encrypted location",
        });

        PdfSignatureInfo signature = Assert.Single(AssertReadersAgree(signed, "user-secret"));
        Assert.Equal("Encrypted reason", signature.Reason);
        Assert.Equal(certificate.RawData, signature.SignerCertificate);
    }

    private static IReadOnlyList<PdfSignatureInfo> AssertReadersAgree(byte[] pdf, string? password)
    {
        IReadOnlyList<PdfSignatureInfo> signatures;
        using (var stream = new MemoryStream(pdf, writable: false))
        using (var document = PdfDocument.Open(stream, password, leaveOpen: true))
        {
            signatures = document.Signatures;
        }

        using JsonDocument report = Inspect(pdf, password);
        JsonElement root = report.RootElement;
        Assert.Equal(
            root.GetProperty("fields").EnumerateArray().Select(f => (f.GetProperty("name").GetString(), f.GetProperty("signed").GetBoolean())).ToArray(),
            signatures.Select(s => ((string?)s.FieldName, s.HasSignatureDictionary)).ToArray());
        foreach (PdfSignatureInfo signature in signatures.Where(s => s.HasSignatureDictionary))
        {
            AssertPyhankoAgrees(root.GetProperty("signatures").GetProperty(signature.FieldName), signature);
        }

        if (signatures.Count > 0)
        {
            AssertPdfsigAgrees(pdf, password, signatures);
        }

        return signatures;
    }

    private static void AssertPyhankoAgrees(JsonElement expected, PdfSignatureInfo actual)
    {
        string field = actual.FieldName + ": ";
        Assert.True(CorpusManifest.Text(expected, "subFilter") == actual.SubFilter, field + "subFilter");
        Assert.True(CorpusManifest.Text(expected, "signerCertificate") == Hash(actual.SignerCertificate), field + "signerCertificate");
        Assert.Equal(expected.GetProperty("certificates").EnumerateArray().Select(c => c.GetString()).ToArray(), actual.Certificates.Select(Hash).ToArray());
        Assert.True(CorpusManifest.Text(expected, "digestAlgorithm") == actual.DigestAlgorithmOid, field + "digestAlgorithm");
        Assert.True(CorpusManifest.Text(expected, "signatureAlgorithm") == actual.SignatureAlgorithmOid, field + "signatureAlgorithm");
        AssertTime(field + "signingTime", CorpusManifest.Text(expected, "signingTime"), actual.SigningTime);
        AssertTime(field + "cmsSigningTime", CorpusManifest.Text(expected, "cmsSigningTime"), actual.CmsSigningTime);
        AssertTime(field + "timestampTime", CorpusManifest.Text(expected, "timestampTime"), actual.TimestampTime);
        Assert.True(CorpusManifest.Text(expected, "name") == actual.Name, field + "name");
        Assert.True(CorpusManifest.Text(expected, "reason") == actual.Reason, field + "reason");
        Assert.True(CorpusManifest.Text(expected, "location") == actual.Location, field + "location");
        Assert.True(CorpusManifest.Text(expected, "contactInfo") == actual.ContactInfo, field + "contactInfo");
        PdfByteRange range = actual.ByteRange ?? throw new InvalidOperationException(field + "no /ByteRange");
        Assert.Equal(expected.GetProperty("byteRange").EnumerateArray().Select(x => x.GetInt64()).ToArray(), new[] { range.Offset0, range.Length0, range.Offset1, range.Length1 });

        // W-150: every signature pyHanko reads has a /ByteRange that starts the file and brackets a hex string.
        Assert.True(actual.ByteRangeStatus == PdfByteRangeStatus.WellFormed, field + "byteRangeStatus " + actual.ByteRangeStatus);
        Assert.True(expected.GetProperty("laterRevisions").GetBoolean() == actual.HasLaterRevisions, field + "laterRevisions");
        JsonElement level = expected.GetProperty("certificationLevel");
        Assert.Equal(level.ValueKind == JsonValueKind.Null ? null : level.GetInt32(), actual.CertificationLevel);
    }

    // pdfsig lists every signature field in the same order, signed or not, and reads these properties of each signed one.
    // It does not parse an ETSI.RFC3161 document timestamp: its type, common name, and hash print as unknown or empty.
    private static void AssertPdfsigAgrees(byte[] pdf, string? password, IReadOnlyList<PdfSignatureInfo> signatures)
    {
        (_, string report) = Pdfsig.Verify(pdf, Array.Empty<byte[]>(), password);
        List<List<string>> reported = Pdfsig.Signatures(report);
        Assert.True(signatures.Count == reported.Count, report);
        for (int i = 0; i < signatures.Count; i++)
        {
            PdfSignatureInfo signature = signatures[i];
            List<string> lines = reported[i];
            Assert.Contains("Signature Field Name: " + signature.FieldName, lines);
            if (!signature.HasSignatureDictionary)
            {
                Assert.DoesNotContain(lines, line => line.StartsWith("Signature Type:", StringComparison.Ordinal));
                continue;
            }

            if (signature.SubFilter != "ETSI.RFC3161")
            {
                Assert.True(lines.Contains("Signature Type: " + signature.SubFilter), string.Join(" | ", lines));
                using X509Certificate2 certificate = TestCompat.LoadCertificate(signature.SignerCertificate!);
                Assert.Contains("Signer Certificate Common Name: " + certificate.GetNameInfo(X509NameType.SimpleName, false), lines);
                Assert.Contains("Signing Hash Algorithm: " + signature.DigestAlgorithm!.Value.Name!.Replace("SHA", "SHA-"), lines);
            }

            PdfByteRange range = signature.ByteRange!.Value;
            Assert.Contains(string.Format(CultureInfo.InvariantCulture, "Signed Ranges: [{0} - {1}], [{2} - {3}]", range.Offset0, range.Offset0 + range.Length0, range.Offset1, range.Offset1 + range.Length1), lines);
            Assert.Contains(signature.HasLaterRevisions ? Pdfsig.NotWholeFile : Pdfsig.WholeFile, lines);
        }
    }

    private static void AssertTime(string what, string? expected, DateTimeOffset? actual)
    {
        DateTimeOffset? parsed = expected == null ? null : DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Assert.True(parsed.HasValue == actual.HasValue && (!parsed.HasValue || parsed.Value.EqualsExact(actual!.Value)), what + ": pyHanko " + expected + ", library " + actual?.ToString("o", CultureInfo.InvariantCulture));
    }

    private static string? Hash(byte[]? der)
    {
        return der == null ? null : CorpusManifest.Sha256(der);
    }

    private static JsonDocument Inspect(byte[] pdf, string? password)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "inspect.pdf");
            File.WriteAllBytes(path, pdf);
            var args = new List<string> { ExternalTool.Fixture("inspect_signatures.py"), path };
            if (password != null)
            {
                args.Add(password);
            }

            using Process process = ExternalTool.Start(ExternalTool.Python(), args.ToArray());
            return JsonDocument.Parse(ExternalTool.Output(process));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] Sign(byte[] pdf, string? password, IPdfSignatureProvider provider, PdfSignatureOptions options)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        using var editor = document.Edit();
        var destination = new MemoryStream();
        editor.Sign(destination, options, provider, leaveOpen: true);
        return destination.ToArray();
    }

    private static byte[] QpdfEncrypt(byte[] pdf)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string input = Path.Combine(directory, "base.pdf");
            string output = Path.Combine(directory, "encrypted.pdf");
            File.WriteAllBytes(input, pdf);
            using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), "--encrypt", "user-secret", "owner-secret", "256", "--", input, output);
            ExternalTool.Output(process);
            return File.ReadAllBytes(output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static X509Certificate2 Authority(string commonName, RSA key)
    {
        var request = new CertificateRequest("CN=" + commonName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    // The issuer is RSA. Naming the generator lets it sign an ECDSA leaf.
    private static X509Certificate2 Leaf(string commonName, AsymmetricAlgorithm key, X509Certificate2 issuer)
    {
        CertificateRequest request = TestKeys.Request("CN=" + commonName, key);
        byte[] serial = new byte[8];
        TestCompat.FillRandom(serial);
        serial[0] &= 0x7F;
        using RSA issuerKey = issuer.GetRSAPrivateKey() ?? throw new InvalidOperationException("The issuer has no RSA private key.");
        return request.Create(issuer.SubjectName, X509SignatureGenerator.CreateForRSA(issuerKey, RSASignaturePadding.Pkcs1), DateTimeOffset.UtcNow.AddDays(-1), issuer.NotAfter.AddMinutes(-1), serial);
    }
}
