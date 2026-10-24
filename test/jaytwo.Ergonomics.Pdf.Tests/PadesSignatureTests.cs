using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Formats.Asn1;
#endif
using System.IO;
using System.Linq;
using System.Security.Cryptography;
#if NET8_0_OR_GREATER
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// W-71 (spec W17): PAdES baseline B-B, ETSI EN 319 142-1 6.3. The CMS is read with SignedCms and AsnReader, not the
// library's DerCodec, so the encoder is not checked against itself.
public class PadesSignatureTests
{
    private const string ContentType = "1.2.840.113549.1.9.3";
    private const string MessageDigest = "1.2.840.113549.1.9.4";
    private const string SigningTime = "1.2.840.113549.1.9.5";
    private const string SigningCertificateV2 = "1.2.840.113549.1.9.16.2.47";
    private static readonly DateTimeOffset When = new DateTimeOffset(2026, 10, 21, 12, 30, 0, TimeSpan.Zero);

    [Theory]
    [MemberData(nameof(SigningAlgorithmTests.Combinations), MemberType = typeof(SigningAlgorithmTests))]
    public void pades_baseline_b_signs_signing_certificate_v2_and_keeps_the_time_in_m_only(string keyKind, PdfDigestAlgorithm digestAlgorithm)
    {
        using AsymmetricAlgorithm key = TestKeys.Create(keyKind);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=PAdES Test", key);
        byte[] saved = Sign(Options(PdfSignatureFormat.PadesBaselineB, digestAlgorithm), new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)));

        string text = Encoding.ASCII.GetString(saved);
        Assert.Contains("/SubFilter /ETSI.CAdES.detached", text, StringComparison.Ordinal);
        Assert.DoesNotContain("adbe.pkcs7", text, StringComparison.Ordinal);
        Assert.Contains("(D:20261021123000Z)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/Cert", text, StringComparison.Ordinal);
#if NET8_0_OR_GREATER
        SignerInfo signer = Decode(saved);
        AssertAttributes(signer.SignedAttributes, ContentType, MessageDigest, SigningCertificateV2);
        Assert.Empty(signer.UnsignedAttributes);
        Assert.Equal(certificate.RawData, signer.Certificate!.RawData);
        AssertContentTypeIsData(signer);
        AssertSigningCertificateV2(signer, certificate);
#endif
    }

    [Fact]
    public void the_default_format_stays_adbe_pkcs7_detached_with_a_cms_signing_time()
    {
        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.Rsa);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Default Format", key);
        Assert.Equal(PdfSignatureFormat.AdbePkcs7Detached, new PdfSignatureOptions().Format);
        byte[] saved = Sign(Options(PdfSignatureFormat.AdbePkcs7Detached, PdfDigestAlgorithm.Sha256), new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)));

        Assert.Contains("/SubFilter /adbe.pkcs7.detached", Encoding.ASCII.GetString(saved), StringComparison.Ordinal);
#if NET8_0_OR_GREATER
        SignerInfo signer = Decode(saved);
        AssertAttributes(signer.SignedAttributes, ContentType, MessageDigest, SigningTime);
        Pkcs9SigningTime time = Assert.IsType<Pkcs9SigningTime>(signer.SignedAttributes.Cast<CryptographicAttributeObject>().Single(a => a.Oid.Value == SigningTime).Values[0]);
        Assert.Equal(When.UtcDateTime, time.SigningTime.ToUniversalTime());
#endif
    }

    [Fact]
    public async Task sign_async_writes_the_same_pades_signature_as_sign()
    {
        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.Rsa);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=PAdES Async", key);
        PdfSignatureOptions options = Options(PdfSignatureFormat.PadesBaselineB, PdfDigestAlgorithm.Sha256);
        byte[] pdf = Page();
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var destination = new MemoryStream();
        await document.SignAsync(destination, options, new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)), leaveOpen: true);

        byte[] saved = destination.ToArray();
        Assert.Equal(pdf, saved.AsSpan(0, pdf.Length).ToArray());
        Assert.Contains("/SubFilter /ETSI.CAdES.detached", Encoding.ASCII.GetString(saved), StringComparison.Ordinal);
#if NET8_0_OR_GREATER
        SignerInfo signer = Decode(saved);
        AssertAttributes(signer.SignedAttributes, ContentType, MessageDigest, SigningCertificateV2);
        AssertSigningCertificateV2(signer, certificate);
#endif
    }

    [Fact]
    public void a_document_timestamp_or_an_undefined_format_is_refused_without_writing()
    {
        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.Rsa);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Refused Format", key);
        var authority = new UnusedAuthority();

        PdfSignException timestamp = AssertSignFailsWithoutWriting(
            new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, Format = PdfSignatureFormat.PadesBaselineB },
            () => new DocTimeStampSignatureProvider(authority));
        Assert.Equal("A document timestamp has no signature format.", timestamp.Message);

        var signer = new KeySigner(key, certificate.RawData);
        PdfSignException undefined = AssertSignFailsWithoutWriting(
            new PdfSignatureOptions { Format = (PdfSignatureFormat)2 },
            () => new CmsDetachedSignatureProvider(signer));
        Assert.Equal("The signature format must be adbe.pkcs7.detached or PAdES baseline B-B.", undefined.Message);
        Assert.Equal(0, authority.Calls);
        Assert.Empty(signer.Calls);
    }

    private static PdfSignatureOptions Options(PdfSignatureFormat format, PdfDigestAlgorithm digestAlgorithm)
    {
        return new PdfSignatureOptions { Format = format, DigestAlgorithm = digestAlgorithm, SigningTime = When, ReservedContentsBytes = 8192 };
    }

    private static byte[] Sign(PdfSignatureOptions options, IPdfSignatureProvider provider)
    {
        byte[] pdf = Page();
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var destination = new MemoryStream();
        document.Sign(destination, options, provider, leaveOpen: true);
        byte[] saved = destination.ToArray();
        Assert.Equal(pdf, saved.AsSpan(0, pdf.Length).ToArray());
        return saved;
    }

    private static PdfSignException AssertSignFailsWithoutWriting(PdfSignatureOptions options, Func<IPdfSignatureProvider> createProvider)
    {
        byte[] pdf = Page();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        PdfSignException same = Assert.Throws<PdfSignException>(() => document.Sign(stream, options, createProvider()));
        Assert.Equal(pdf, stream.ToArray());

        using var destination = new MemoryStream();
        PdfSignException cross = Assert.Throws<PdfSignException>(() => document.Sign(destination, options, createProvider(), leaveOpen: true));
        Assert.Equal(0, destination.Length);
        Assert.Equal(same.Message, cross.Message);
        return cross;
    }

    private static byte[] Page()
    {
        return PdfBuilder.Basic().Finish();
    }

#if NET8_0_OR_GREATER
    private static SignerInfo Decode(byte[] saved)
    {
        string text = Encoding.ASCII.GetString(saved);
        Match range = Regex.Match(text, @"/ByteRange\s*\[\s*(\d{10})\s+(\d{10})\s+(\d{10})\s+(\d{10})\s*\]");
        int length0 = int.Parse(range.Groups[2].Value);
        int offset1 = int.Parse(range.Groups[3].Value);
        int length1 = int.Parse(range.Groups[4].Value);
        var signed = new byte[length0 + length1];
        Buffer.BlockCopy(saved, 0, signed, 0, length0);
        Buffer.BlockCopy(saved, offset1, signed, length0, length1);
        byte[] gap = TestCompat.FromHexString(Regex.Match(text, @"/Contents\s*<([0-9A-Fa-f]+)>").Groups[1].Value);
        var reader = new AsnReader(gap, AsnEncodingRules.DER);
        var cms = new SignedCms(new ContentInfo(signed), detached: true);
        cms.Decode(reader.ReadEncodedValue().ToArray());
        cms.CheckSignature(verifySignatureOnly: true);
        Assert.Single(cms.SignerInfos);
        return cms.SignerInfos[0];
    }

    private static void AssertAttributes(CryptographicAttributeObjectCollection attributes, params string[] expected)
    {
        string[] actual = attributes.Cast<CryptographicAttributeObject>().Select(a => a.Oid.Value!).ToArray();
        Assert.Equal(expected.OrderBy(oid => oid, StringComparer.Ordinal), actual.OrderBy(oid => oid, StringComparer.Ordinal));
    }

    private static void AssertContentTypeIsData(SignerInfo signer)
    {
        CryptographicAttributeObject attribute = signer.SignedAttributes.Cast<CryptographicAttributeObject>().Single(a => a.Oid.Value == ContentType);
        Assert.Single(attribute.Values);
        var reader = new AsnReader(attribute.Values[0].RawData, AsnEncodingRules.DER);
        Assert.Equal("1.2.840.113549.1.7.1", reader.ReadObjectIdentifier());
    }

    // RFC 5035 3: SigningCertificateV2 ::= SEQUENCE { certs SEQUENCE OF ESSCertIDv2, policies OPTIONAL }, and
    // ESSCertIDv2 ::= SEQUENCE { hashAlgorithm DEFAULT id-sha256, certHash OCTET STRING, issuerSerial IssuerSerial OPTIONAL }.
    // DER omits a DEFAULT, so a SHA-256 certHash comes first. IssuerSerial is { GeneralNames, serialNumber }.
    private static void AssertSigningCertificateV2(SignerInfo signer, X509Certificate2 certificate)
    {
        CryptographicAttributeObject attribute = signer.SignedAttributes.Cast<CryptographicAttributeObject>().Single(a => a.Oid.Value == SigningCertificateV2);
        Assert.Single(attribute.Values);
        var value = new AsnReader(attribute.Values[0].RawData, AsnEncodingRules.DER);
        AsnReader signingCertificate = value.ReadSequence();
        value.ThrowIfNotEmpty();
        AsnReader certs = signingCertificate.ReadSequence();
        signingCertificate.ThrowIfNotEmpty();
        AsnReader certId = certs.ReadSequence();
        certs.ThrowIfNotEmpty();
        Assert.Equal(SHA256.HashData(certificate.RawData), certId.ReadOctetString());
        AsnReader issuerSerial = certId.ReadSequence();
        certId.ThrowIfNotEmpty();
        AsnReader generalNames = issuerSerial.ReadSequence();
        AsnReader directoryName = generalNames.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 4, isConstructed: true));
        generalNames.ThrowIfNotEmpty();
        Assert.Equal(certificate.IssuerName.RawData, directoryName.ReadEncodedValue().ToArray());
        directoryName.ThrowIfNotEmpty();
        Assert.Equal(certificate.SerialNumberBytes.ToArray(), issuerSerial.ReadIntegerBytes().ToArray());
        issuerSerial.ThrowIfNotEmpty();
    }
#endif

    private sealed class UnusedAuthority : ITimestampAuthority
    {
        public int Calls { get; private set; }

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, System.Threading.CancellationToken cancellationToken)
        {
            Calls++;
            return new ValueTask<byte[]>(Array.Empty<byte>());
        }
    }
}
