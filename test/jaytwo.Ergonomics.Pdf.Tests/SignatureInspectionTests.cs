using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// W-72, spec R9: signature inspection reads the signature dictionary and the CMS, and validates nothing.
// SignatureInspectionInteropTests compares the same reads with pyHanko and pdfsig.
public class SignatureInspectionTests
{
    private static readonly DateTimeOffset SigningTime = new DateTimeOffset(2026, 10, 21, 9, 30, 15, TimeSpan.FromHours(-6));

    [Fact]
    public void unsigned_signature_field_reports_only_its_name()
    {
        using var stream = new MemoryStream(File.ReadAllBytes(ExternalTool.Fixture("pyhanko-sigfield.pdf")), writable: false);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        PdfSignatureInfo signature = Assert.Single(document.Signatures);

        Assert.Equal("ExistingSig", signature.FieldName);
        Assert.False(signature.IsSigned);
        Assert.Null(signature.SubFilter);
        Assert.Null(signature.SignerCertificate);
        Assert.Empty(signature.Certificates);
        Assert.Null(signature.DigestAlgorithm);
        Assert.Null(signature.SigningTime);
        Assert.Null(signature.ByteRange);
        Assert.Null(signature.SignedRevisionEnd);
        Assert.False(signature.HasLaterRevisions);
        Assert.Null(signature.TimestampTime);
        Assert.Null(signature.CertificationLevel);
    }

    [Fact]
    public void library_signature_reports_its_dictionary_and_cms()
    {
        using var rootKey = RSA.Create(2048);
        using var leafKey = RSA.Create(2048);
        using X509Certificate2 root = Authority("Inspection Root", rootKey);
        using X509Certificate2 leaf = Leaf("Inspection Leaf", leafKey, root);
        var provider = new CapturingProvider(new CmsDetachedSignatureProvider(new KeySigner(leafKey, leaf.RawData, new[] { root.RawData }, null)));
        byte[] signed = Sign(PdfBuilder.Basic().Finish(), provider, new PdfSignatureOptions
        {
            SigningTime = SigningTime,
            Reason = "Approved",
            Location = "Denver",
            ContactInfo = "signer@example.test",
            SignerName = "Ada Signer",
        });

        using var stream = new MemoryStream(signed, writable: false);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        PdfSignatureInfo signature = Assert.Single(document.Signatures);

        Assert.Equal("Signature1", signature.FieldName);
        Assert.True(signature.IsSigned);
        Assert.Equal("adbe.pkcs7.detached", signature.SubFilter);
        Assert.Equal(leaf.RawData, signature.SignerCertificate);
        Assert.Equal(new[] { leaf.RawData, root.RawData }.OrderBy(Convert.ToBase64String), signature.Certificates.OrderBy(Convert.ToBase64String));
        Assert.Equal(HashAlgorithmName.SHA256, signature.DigestAlgorithm);
        Assert.Equal("2.16.840.1.101.3.4.2.1", signature.DigestAlgorithmOid);
        Assert.Equal("1.2.840.113549.1.1.11", signature.SignatureAlgorithmOid);
        Assert.Equal(SigningTime, signature.SigningTime);
        Assert.Equal(TimeSpan.FromHours(-6), signature.SigningTime!.Value.Offset);
        Assert.Equal(SigningTime, signature.CmsSigningTime);
        Assert.Equal("Ada Signer", signature.Name);
        Assert.Equal("Approved", signature.Reason);
        Assert.Equal("Denver", signature.Location);
        Assert.Equal("signer@example.test", signature.ContactInfo);
        Assert.Equal(provider.Range, signature.ByteRange);
        Assert.Equal(signed.Length, signature.SignedRevisionEnd);
        Assert.False(signature.HasLaterRevisions);
        Assert.Null(signature.TimestampTime);
        Assert.Null(signature.CertificationLevel);
    }

    [Fact]
    public void a_save_after_the_signature_is_a_later_revision()
    {
        using var key = RSA.Create(2048);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Inspection Signer", key);
        byte[] signed = Sign(PdfBuilder.Basic().Finish(), new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)), new PdfSignatureOptions());
        var stream = new MemoryStream();
        stream.Write(signed, 0, signed.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.False(document.Signatures[0].HasLaterRevisions);
            document.Metadata.Title = "Later";
            document.SaveIncremental();

            PdfSignatureInfo signature = Assert.Single(document.Signatures);
            Assert.True(signature.HasLaterRevisions);
            Assert.Equal(signed.Length, signature.SignedRevisionEnd);
        }

        using var reopened = PdfDocument.Open(stream, leaveOpen: true);
        Assert.True(reopened.Signatures[0].HasLaterRevisions);
        Assert.Equal(signed.Length, reopened.Signatures[0].SignedRevisionEnd);
    }

    [Fact]
    public void pades_signature_has_its_time_only_in_m()
    {
        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.P384);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Inspection PAdES", key);
        byte[] signed = Sign(
            PdfBuilder.Basic().Finish(),
            new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)),
            new PdfSignatureOptions { SigningTime = SigningTime, Format = PdfSignatureFormat.PadesBaselineB, DigestAlgorithm = PdfDigestAlgorithm.Sha512 });

        using var stream = new MemoryStream(signed, writable: false);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        PdfSignatureInfo signature = Assert.Single(document.Signatures);

        Assert.Equal("ETSI.CAdES.detached", signature.SubFilter);
        Assert.Equal(SigningTime, signature.SigningTime);
        Assert.Null(signature.CmsSigningTime);
        Assert.Equal(HashAlgorithmName.SHA512, signature.DigestAlgorithm);
        Assert.Equal("1.2.840.10045.4.3.4", signature.SignatureAlgorithmOid);
        Assert.Equal(certificate.RawData, signature.SignerCertificate);
    }

    [Fact]
    public void signer_named_by_subject_key_identifier_is_found()
    {
        using var key = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        using X509Certificate2 signer = Authority("Inspection SKI Signer", key);
        using X509Certificate2 other = Authority("Inspection SKI Other", otherKey);
        byte[] keyId = SubjectKeyIdentifier(signer);
        byte[] identifier = DerCodec.ReplaceTag(DerCodec.OctetString(keyId), 0x80);
        DateTimeOffset time = new DateTimeOffset(2049, 12, 31, 23, 59, 59, TimeSpan.Zero);

        CmsSignatureReader.Result result = CmsSignatureReader.Read(Cms(identifier, new[] { other.RawData, signer.RawData }, SigningTimeAttribute(time), 1), false, 0);

        Assert.Equal(signer.RawData, result.SignerCertificate);
        Assert.Equal(2, result.Certificates.Count);
        Assert.Equal(time, result.SigningTime);

        byte[] unknown = DerCodec.ReplaceTag(DerCodec.OctetString(new byte[] { 1, 2, 3 }), 0x80);
        Assert.Null(CmsSignatureReader.Read(Cms(unknown, new[] { other.RawData, signer.RawData }, null, 1), false, 0).SignerCertificate);
    }

    // Spec section 8: ISO 32000-1 12.8.3.3.1 requires DER, and Acrobat writes indefinite lengths on the outer containers
    // (WriterQuirkTests reads the GPO bill). The same CMS in either encoding reads the same.
    [Fact]
    public void indefinite_lengths_read_like_der()
    {
        using var key = RSA.Create(2048);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Inspection BER", key);
        var provider = new CapturingProvider(new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)));
        Sign(PdfBuilder.Basic().Finish(), provider, new PdfSignatureOptions { SigningTime = SigningTime });
        byte[] der = provider.Contents;
        byte[] ber = Indefinite(der, 0);
        Assert.Equal(new byte[] { 0x30, 0x80 }, ber.Take(2));

        CmsSignatureReader.Result fromDer = CmsSignatureReader.Read(der, false, 0);
        CmsSignatureReader.Result fromBer = CmsSignatureReader.Read(ber.Concat(new byte[64]).ToArray(), false, 0);

        Assert.Equal(certificate.RawData, fromBer.SignerCertificate);
        Assert.Equal(fromDer.Certificates, fromBer.Certificates);
        Assert.Equal(fromDer.DigestAlgorithm, fromBer.DigestAlgorithm);
        Assert.Equal(fromDer.SignatureAlgorithm, fromBer.SignatureAlgorithm);
        Assert.Equal(SigningTime, fromBer.SigningTime);
    }

    [Theory]
    [InlineData("zeros")]
    [InlineData("not-signed-data")]
    [InlineData("trailing")]
    [InlineData("truncated")]
    [InlineData("primitive-indefinite")]
    [InlineData("unterminated-indefinite")]
    [InlineData("high-tag")]
    [InlineData("long-length")]
    [InlineData("deep")]
    [InlineData("two-signers")]
    [InlineData("no-signers")]
    [InlineData("local-time")]
    [InlineData("two-signing-times")]
    public void malformed_cms_is_a_parse_error(string kind)
    {
        using var key = RSA.Create(2048);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Inspection Malformed", key);
        (byte[] issuer, byte[] serial) = DerCodec.ReadCertificateIdentity(certificate.RawData);
        byte[] identifier = DerCodec.Sequence(issuer, serial);
        byte[] valid = Cms(identifier, new[] { certificate.RawData }, null, 1);
        Assert.Equal(certificate.RawData, CmsSignatureReader.Read(valid, false, 0).SignerCertificate);
        byte[] contents = kind switch
        {
            "zeros" => new byte[64],
            "not-signed-data" => DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.7.1"), DerCodec.ExplicitContext(0, DerCodec.OctetString(new byte[] { 1 }))),
            "trailing" => valid.Concat(new byte[] { 0, 0, 1 }).ToArray(),
            "truncated" => valid.Take(valid.Length - 1).ToArray(),
            "primitive-indefinite" => new byte[] { 0x04, 0x80, 0x01, 0x00, 0x00 },
            "unterminated-indefinite" => new byte[] { 0x30, 0x80, 0x02, 0x01, 0x01 },
            "high-tag" => new byte[] { 0x1F, 0x01, 0x00 },
            "long-length" => new byte[] { 0x30, 0x85, 0x01, 0x00, 0x00, 0x00, 0x00 },
            "deep" => Enumerable.Repeat(new byte[] { 0x30, 0x80 }, 100).SelectMany(x => x).Concat(new byte[200]).ToArray(),
            "two-signers" => Cms(identifier, new[] { certificate.RawData }, null, 2),
            "no-signers" => Cms(identifier, new[] { certificate.RawData }, null, 0),
            "local-time" => Cms(identifier, new[] { certificate.RawData }, Attributes(Attribute("1.2.840.113549.1.9.5", Tlv(0x18, Encoding.ASCII.GetBytes("20261021093015")))), 1),
            _ => Cms(identifier, new[] { certificate.RawData }, Attributes(SigningTimeValue(SigningTime), SigningTimeValue(SigningTime)), 1),
        };

        Assert.Throws<PdfParseException>(() => CmsSignatureReader.Read(contents, false, 0));
    }

    [Fact]
    public void non_certificate_choices_are_streamed_without_a_node_per_choice()
    {
        const int count = 65536;
        byte[] choices = CertificateChoices(count);
        byte[] contents = CmsWithCertificateChoices(choices);
        Assert.Empty(CmsSignatureReader.Read(CmsWithCertificateChoices(CertificateChoices(1)), false, 0).Certificates);

        long before = GC.GetAllocatedBytesForCurrentThread();
        CmsSignatureReader.Result result = CmsSignatureReader.Read(contents, false, 0);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Empty(result.Certificates);
        Assert.True(allocated < 512 * 1024, "Certificate-choice parsing allocated " + allocated.ToString() + " bytes.");
    }

    [Fact]
    public void certificate_choice_count_is_bounded()
    {
        byte[] contents = CmsWithCertificateChoices(CertificateChoices(65537));

        PdfParseException error = Assert.Throws<PdfParseException>(() => CmsSignatureReader.Read(contents, false, 0));

        Assert.Contains("more than 65536 choices", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tag")]
    [InlineData("padding")]
    public void file_whose_contents_do_not_parse_throws_and_fields_still_read(string damage)
    {
        using var key = RSA.Create(2048);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Inspection Damaged", key);
        byte[] signed = Sign(PdfBuilder.Basic().Finish(), new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)), new PdfSignatureOptions());
        string text = TestCompat.Latin1.GetString(signed);
        int hex = text.IndexOf("/Contents <", StringComparison.Ordinal) + "/Contents <".Length;
        int position = damage == "tag" ? hex + 1 : text.IndexOf('>', hex) - 1;
        Assert.Equal((byte)'0', signed[position]);
        signed[position] = (byte)'1';

        using var stream = new MemoryStream(signed, writable: false);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        Assert.Throws<PdfParseException>(() => document.Signatures);
        Assert.Equal("Signature1", Assert.Single(document.AcroForm.Fields).FullName);
    }

    // A field with two widgets is one entry. A sub-filter that is not CMS reports the dictionary only. A DocMDP
    // /Reference reports P. The made-up /ByteRange ends before this file's xref, so a later revision exists.
    [Fact]
    public void dictionary_entries_are_read_for_every_sub_filter()
    {
        byte[] pdf = SignatureFieldFile(
            "/SubFilter /adbe.x509.rsa_sha1 /Contents <0102> /Cert <0304> /ByteRange [0 10 20 30] /M (D:20240102030405Z) /Reason (Checked)"
            + " /Reference [<< /Type /SigRef /TransformMethod /DocMDP /TransformParams << /P 3 /V /1.2 >> >>]");
        using var stream = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        IReadOnlyList<PdfSignatureInfo> signatures = document.Signatures;

        Assert.Equal(new[] { "Approval", "Empty" }, signatures.Select(s => s.FieldName));
        Assert.Equal(3, document.AcroForm.Fields.Count);
        PdfSignatureInfo approval = signatures[0];
        Assert.True(approval.IsSigned);
        Assert.Equal("adbe.x509.rsa_sha1", approval.SubFilter);
        Assert.Null(approval.SignerCertificate);
        Assert.Empty(approval.Certificates);
        Assert.Null(approval.DigestAlgorithmOid);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), approval.SigningTime);
        Assert.Equal("Checked", approval.Reason);
        Assert.Equal(new PdfByteRange(0, 10, 20, 30), approval.ByteRange);
        Assert.Equal(50, approval.SignedRevisionEnd);
        Assert.True(approval.HasLaterRevisions);
        Assert.Equal(3, approval.CertificationLevel);
        Assert.False(signatures[1].IsSigned);
    }

    [Theory]
    [InlineData("/SubFilter /adbe.x509.rsa_sha1 /ByteRange [0 10 20]")]
    [InlineData("/SubFilter /adbe.x509.rsa_sha1 /ByteRange [0 -1 20 30]")]
    [InlineData("/SubFilter /adbe.x509.rsa_sha1 /ByteRange [0 10 20 30.5]")]
    [InlineData("/SubFilter /adbe.x509.rsa_sha1 /ByteRange [0 10 9223372036854775807 1]")]
    [InlineData("/SubFilter /adbe.x509.rsa_sha1 /M (D:20241399)")]
    [InlineData("/SubFilter /adbe.pkcs7.detached /ByteRange [0 10 20 30]")]
    [InlineData("/SubFilter /ETSI.RFC3161 /Contents 5")]
    public void malformed_signature_dictionary_is_a_parse_error(string entries)
    {
        using var stream = new MemoryStream(SignatureFieldFile(entries), writable: false);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        Assert.Throws<PdfParseException>(() => document.Signatures);
        Assert.Equal(3, document.AcroForm.Fields.Count);
    }

    [Fact]
    public void a_signed_file_past_one_gigabyte_is_inspected_without_reading_the_middle()
    {
        using var key = RSA.Create(2048);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Inspection Sparse", key);
        var capture = new CapturingProvider(new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)));
        Sign(PdfBuilder.Basic().Finish(), capture, new PdfSignatureOptions());

        var builder = PdfBuilder.Basic();
        builder.Ascii(new string('\n', 4096));
        long tailOffset = (1L << 30) + 65536;
        (byte[] head, byte[] tail) = builder.Split(tailOffset, string.Empty);
        var sparse = new SparsePdfStream(head, tail, tailOffset);
        var fixedProvider = new FixedProvider(capture.Contents);
        using (var document = PdfDocument.Open(sparse, leaveOpen: true))
        {
            document.Sign(sparse, new PdfSignatureOptions { ReservedContentsBytes = capture.Contents.Length }, fixedProvider);
        }

        long before = sparse.BytesRead;
        using (var document = PdfDocument.Open(sparse, leaveOpen: true))
        {
            PdfSignatureInfo signature = Assert.Single(document.Signatures);
            Assert.Equal(certificate.RawData, signature.SignerCertificate);
            Assert.Equal(fixedProvider.Range, signature.ByteRange);
            Assert.True(signature.ByteRange!.Value.Length0 > 1L << 30);
            Assert.Equal(sparse.Length, signature.SignedRevisionEnd);
            Assert.Equal(2, signature.SignedRevisionNumber);
            Assert.False(signature.HasLaterRevisions);
            Assert.Collection(
                document.Revisions,
                revision => Assert.False(revision.IsCoveredBySignature),
                revision =>
                {
                    Assert.Equal(2, revision.Number);
                    Assert.Equal(sparse.Length, revision.EndOffset);
                    Assert.True(revision.IsCoveredBySignature);
                });
        }

        Assert.False(sparse.TouchedHole);
        Assert.True(sparse.BytesRead - before < 2 * 1024 * 1024);
    }

    private static byte[] SignatureFieldFile(string signatureEntries)
    {
        var builder = PdfBuilder.Basic(" /AcroForm << /Fields [5 0 R 9 0 R] /SigFlags 3 >>", " /Annots [6 0 R 7 0 R 9 0 R]");
        builder.Indirect(5, "<< /FT /Sig /T (Approval) /V 8 0 R /Kids [6 0 R 7 0 R] >>");
        builder.Indirect(6, "<< /Subtype /Widget /Parent 5 0 R /Rect [0 0 10 10] /P 3 0 R >>");
        builder.Indirect(7, "<< /Subtype /Widget /Parent 5 0 R /Rect [20 0 30 10] /P 3 0 R >>");
        builder.Indirect(8, "<< /Type /Sig /Filter /Adobe.PPKLite " + signatureEntries + " >>");
        builder.Indirect(9, "<< /FT /Sig /T (Empty) /Subtype /Widget /Rect [40 0 50 10] /P 3 0 R >>");
        return builder.Finish();
    }

    private static byte[] Sign(byte[] pdf, IPdfSignatureProvider provider, PdfSignatureOptions options)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        var destination = new MemoryStream();
        document.Sign(destination, options, provider, leaveOpen: true);
        return destination.ToArray();
    }

    private static X509Certificate2 Authority(string commonName, RSA key)
    {
        var request = new CertificateRequest("CN=" + commonName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static X509Certificate2 Leaf(string commonName, RSA key, X509Certificate2 issuer)
    {
        var request = new CertificateRequest("CN=" + commonName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        byte[] serial = new byte[8];
        TestCompat.FillRandom(serial);
        serial[0] &= 0x7F;
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), issuer.NotAfter.AddMinutes(-1), serial);
    }

    private static byte[] SubjectKeyIdentifier(X509Certificate2 certificate)
    {
        foreach (X509Extension extension in certificate.Extensions)
        {
            if (extension is X509SubjectKeyIdentifierExtension identifier)
            {
                return TestCompat.FromHexString(identifier.SubjectKeyIdentifier!);
            }
        }

        throw new InvalidOperationException("The certificate has no subject key identifier.");
    }

    // A minimal SignedData around one SignerInfo, repeated signerCount times, with the certificates as [0] IMPLICIT.
    private static byte[] Cms(byte[] signerIdentifier, IReadOnlyList<byte[]> certificates, byte[]? signedAttributes, int signerCount)
    {
        byte[] signerInfo = DerCodec.Sequence(
            DerCodec.Integer(signerIdentifier[0] == 0x80 ? 3 : 1),
            signerIdentifier,
            DerCodec.AlgorithmSha256(),
            signedAttributes ?? Array.Empty<byte>(),
            DerCodec.AlgorithmSha256WithRsa(),
            DerCodec.OctetString(new byte[] { 1, 2, 3 }));
        byte[] signedData = DerCodec.Sequence(
            DerCodec.Integer(1),
            DerCodec.Set(DerCodec.AlgorithmSha256()),
            DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.7.1")),
            DerCodec.ReplaceTag(DerCodec.Set(certificates.ToArray()), 0xA0),
            DerCodec.Set(Enumerable.Repeat(signerInfo, signerCount).ToArray()));
        return DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.7.2"), DerCodec.ExplicitContext(0, signedData));
    }

    private static byte[] CmsWithCertificateChoices(byte[] choices)
    {
        byte[] identifier = DerCodec.ReplaceTag(DerCodec.OctetString(new byte[] { 1 }), 0x80);
        byte[] signerInfo = DerCodec.Sequence(
            DerCodec.Integer(3),
            identifier,
            DerCodec.AlgorithmSha256(),
            DerCodec.AlgorithmSha256WithRsa(),
            DerCodec.OctetString(new byte[] { 1, 2, 3 }));
        byte[] signedData = DerCodec.Sequence(
            DerCodec.Integer(1),
            DerCodec.Set(DerCodec.AlgorithmSha256()),
            DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.7.1")),
            DerCodec.ReplaceTag(DerCodec.Set(choices), 0xA0),
            DerCodec.Set(signerInfo));
        return DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.7.2"), DerCodec.ExplicitContext(0, signedData));
    }

    private static byte[] CertificateChoices(int count)
    {
        var choices = new byte[count * 2];
        for (int i = 0; i < choices.Length; i += 2)
        {
            choices[i] = 0x05;
        }

        return choices;
    }

    private static byte[] SigningTimeAttribute(DateTimeOffset time)
    {
        return Attributes(SigningTimeValue(time));
    }

    private static byte[] SigningTimeValue(DateTimeOffset time)
    {
        return Attribute("1.2.840.113549.1.9.5", DerCodec.UtcTime(time));
    }

    private static byte[] Attribute(string oid, byte[] value)
    {
        return DerCodec.Sequence(DerCodec.Oid(oid), DerCodec.Set(value));
    }

    private static byte[] Attributes(params byte[][] attributes)
    {
        return DerCodec.ReplaceTag(DerCodec.Set(attributes), 0xA0);
    }

    private static byte[] Tlv(byte tag, byte[] content)
    {
        return new[] { tag, (byte)content.Length }.Concat(content).ToArray();
    }

    // Re-encodes the constructed values down to SignedData's own fields with indefinite lengths, as Acrobat does.
    // Certificates and the SignerInfo below that depth keep their DER bytes.
    private static byte[] Indefinite(byte[] tlv, int depth)
    {
        DerCodec.DerTlv node = DerCodec.ReadTlv(tlv);
        if ((node.Tag & 0x20) == 0 || depth > 3)
        {
            return node.Full;
        }

        var output = new List<byte> { node.Tag, 0x80 };
        int position = 0;
        while (position < node.Content.Length)
        {
            DerCodec.DerTlv child = DerCodec.ReadTlv(node.Content, position);
            output.AddRange(Indefinite(child.Full, depth + 1));
            position += child.Full.Length;
        }

        output.Add(0);
        output.Add(0);
        return output.ToArray();
    }

    private sealed class CapturingProvider : IPdfSignatureProvider
    {
        private readonly IPdfSignatureProvider _inner;

        public CapturingProvider(IPdfSignatureProvider inner)
        {
            _inner = inner;
        }

        public PdfSignatureKind Kind => _inner.Kind;

        public PdfByteRange Range { get; private set; }

        public byte[] Contents { get; private set; } = Array.Empty<byte>();

        public async ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            Range = context.ByteRange;
            Contents = await _inner.CreateContentsAsync(context, cancellationToken);
            return Contents;
        }
    }

    // Returns CMS bytes made earlier, so a signature lands in a sparse file without hashing its middle.
    private sealed class FixedProvider : IPdfSignatureProvider
    {
        private readonly byte[] _contents;

        public FixedProvider(byte[] contents)
        {
            _contents = contents;
        }

        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public PdfByteRange Range { get; private set; }

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            Range = context.ByteRange;
            return new ValueTask<byte[]>(_contents);
        }
    }
}
