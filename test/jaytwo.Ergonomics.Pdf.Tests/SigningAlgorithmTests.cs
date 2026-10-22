using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
#if NET8_0_OR_GREATER
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Ergonomics.Pdf.Internal;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// W-70 (spec W15, W16): RSA, ECDSA P-256, and ECDSA P-384, each with SHA-256, SHA-384, and SHA-512.
public class SigningAlgorithmTests
{
    public static TheoryData<string, PdfDigestAlgorithm> Combinations
    {
        get
        {
            var data = new TheoryData<string, PdfDigestAlgorithm>();
            foreach (string key in new[] { TestKeys.Rsa, TestKeys.P256, TestKeys.P384 })
            {
                foreach (PdfDigestAlgorithm digest in new[] { PdfDigestAlgorithm.Sha256, PdfDigestAlgorithm.Sha384, PdfDigestAlgorithm.Sha512 })
                {
                    data.Add(key, digest);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void every_key_and_digest_writes_its_cms_algorithms_and_a_signature_that_verifies(string keyKind, PdfDigestAlgorithm digestAlgorithm)
    {
        using AsymmetricAlgorithm key = TestKeys.Create(keyKind);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Algorithm Test", key);
        var signer = new KeySigner(key, certificate.RawData);
        (string digestOid, HashAlgorithmName name, int length) = Expected(digestAlgorithm);
        string signatureOid = key is RSA
            ? "1.2.840.113549.1.1." + (10 + (int)digestAlgorithm + 1)
            : "1.2.840.10045.4.3." + ((int)digestAlgorithm + 2);

        byte[] saved = Sign(Page(), digestAlgorithm, new CmsDetachedSignatureProvider(signer));

        Assert.Equal(new[] { (length, name) }, signer.Calls);
        (byte[] signedBytes, byte[] cms) = SignedParts(saved);
        List<DerCodec.DerTlv> signedData = SignedData(cms);
        Assert.Equal(DerCodec.Set(DerCodec.Algorithm(digestOid)), signedData[1].Full);
        List<DerCodec.DerTlv> signerInfo = Children(Children(signedData[4].Content)[0].Content);
        Assert.Equal(DerCodec.Algorithm(digestOid), signerInfo[2].Full);
        Assert.Equal(key is RSA ? DerCodec.Algorithm(signatureOid) : DerCodec.Sequence(DerCodec.Oid(signatureOid)), signerInfo[4].Full);

        byte[] attributes = DerCodec.ReplaceTag(signerInfo[3].Full, 0x31);
        Assert.Equal(Hash(name, signedBytes), MessageDigest(attributes));
        byte[] attributesHash = Hash(name, attributes);
        byte[] signature = signerInfo[5].Content;
        if (key is RSA rsa)
        {
            Assert.True(rsa.VerifyHash(attributesHash, signature, name, RSASignaturePadding.Pkcs1));
        }
        else
        {
            int half = keyKind == TestKeys.P256 ? 32 : 48;
            Assert.True(((ECDsa)key).VerifyHash(attributesHash, P1363(signature, half)));
        }

#if NET8_0_OR_GREATER
        var signedCms = new SignedCms(new ContentInfo(signedBytes), detached: true);
        signedCms.Decode(cms);
        signedCms.CheckSignature(verifySignatureOnly: true);
        Assert.Equal(digestOid, signedCms.SignerInfos[0].DigestAlgorithm.Value);
        Assert.Equal(signatureOid, signedCms.SignerInfos[0].SignatureAlgorithm.Value);
#endif
    }

    [Fact]
    public void ecdsa_values_with_high_bits_and_leading_zeros_are_minimal_der_integers()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Integer Test", key);
        var cases = new[]
        {
            (Fill(0x80), "0221008080"),
            (Fill(0x00, 0x01), "021f01"),
            (Fill(0x00, 0x00), "020100"),
        };
        foreach ((byte[] half, string prefix) in cases)
        {
            byte[] p1363 = new byte[64];
            Buffer.BlockCopy(half, 0, p1363, 0, 32);
            Buffer.BlockCopy(half, 0, p1363, 32, 32);
            var signer = new KeySigner(key, certificate.RawData, Array.Empty<byte[]>(), _ => p1363);
            byte[] saved = Sign(Page(), PdfDigestAlgorithm.Sha256, new CmsDetachedSignatureProvider(signer));
            (_, byte[] cms) = SignedParts(saved);
            List<DerCodec.DerTlv> signedData = SignedData(cms);
            byte[] value = Children(Children(signedData[4].Content)[0].Content)[5].Content;
            List<DerCodec.DerTlv> integers = Children(DerCodec.ReadTlv(value).Content);
            Assert.Equal(2, integers.Count);
            Assert.StartsWith(prefix, TestCompat.ToHexString(integers[0].Full).ToLowerInvariant(), StringComparison.Ordinal);
            Assert.Equal(integers[0].Full, integers[1].Full);
            Assert.Equal(half, P1363(value, 32).AsSpan(0, 32).ToArray());
        }
    }

    [Fact]
    public void a_p521_leaf_is_refused_before_the_signer_is_called()
    {
        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.P521);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=P-521", key);
        var signer = new KeySigner(key, certificate.RawData);
        PdfSignException exception = AssertSignFailsWithoutWriting(new PdfSignatureOptions(), () => new CmsDetachedSignatureProvider(signer));
        Assert.Equal("The signer certificate key must be RSA, ECDSA P-256, or ECDSA P-384.", exception.Message);
        Assert.Empty(signer.Calls);
    }

    [Theory]
    [InlineData(TestKeys.P256, "der")]
    [InlineData(TestKeys.P256, "short")]
    [InlineData(TestKeys.P384, "der")]
    [InlineData(TestKeys.P384, "long")]
    public void an_ecdsa_value_that_is_not_p1363_for_its_curve_is_refused(string keyKind, string form)
    {
        using AsymmetricAlgorithm key = TestKeys.Create(keyKind);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=P1363", key);
        int half = keyKind == TestKeys.P256 ? 32 : 48;
        Func<byte[], byte[]> transform = form switch
        {
            "der" => p1363 => DerCodec.Sequence(DerCodec.Integer(p1363.AsSpan(0, half)), DerCodec.Integer(p1363.AsSpan(half, half))),
            "short" => p1363 => p1363.AsSpan(1).ToArray(),
            _ => p1363 => Concat(p1363, new byte[] { 0 }),
        };
        PdfSignException exception = AssertSignFailsWithoutWriting(
            new PdfSignatureOptions(),
            () => new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData, Array.Empty<byte[]>(), transform)));
        Assert.Equal("The ECDSA signature must be the " + (half * 2) + "-byte IEEE P1363 r||s value.", exception.Message);
    }

    [Fact]
    public void an_undefined_digest_algorithm_is_refused_before_the_signer_is_called()
    {
        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.Rsa);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Digest", key);
        var signer = new KeySigner(key, certificate.RawData);
        PdfSignException exception = AssertSignFailsWithoutWriting(
            new PdfSignatureOptions { DigestAlgorithm = (PdfDigestAlgorithm)3 },
            () => new CmsDetachedSignatureProvider(signer));
        Assert.Equal("The digest algorithm must be SHA-256, SHA-384, or SHA-512.", exception.Message);
        Assert.Empty(signer.Calls);
    }

    [Theory]
    [InlineData(PdfDigestAlgorithm.Sha384)]
    [InlineData(PdfDigestAlgorithm.Sha512)]
    public void a_document_timestamp_refuses_a_digest_other_than_sha256(PdfDigestAlgorithm digestAlgorithm)
    {
        var authority = new UncalledAuthority();
        PdfSignException exception = AssertSignFailsWithoutWriting(
            new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, DigestAlgorithm = digestAlgorithm },
            () => new DocTimeStampSignatureProvider(authority));
        Assert.Equal("A document timestamp uses SHA-256.", exception.Message);
        Assert.Equal(0, authority.Calls);
    }

    private static (string Oid, HashAlgorithmName Name, int Length) Expected(PdfDigestAlgorithm digestAlgorithm)
    {
        return digestAlgorithm switch
        {
            PdfDigestAlgorithm.Sha256 => ("2.16.840.1.101.3.4.2.1", HashAlgorithmName.SHA256, 32),
            PdfDigestAlgorithm.Sha384 => ("2.16.840.1.101.3.4.2.2", HashAlgorithmName.SHA384, 48),
            _ => ("2.16.840.1.101.3.4.2.3", HashAlgorithmName.SHA512, 64),
        };
    }

    private static byte[] Hash(HashAlgorithmName name, byte[] data)
    {
        using var incremental = IncrementalHash.CreateHash(name);
        incremental.AppendData(data);
        return incremental.GetHashAndReset();
    }

    private static byte[] MessageDigest(byte[] attributes)
    {
        byte[] oid = DerCodec.Oid("1.2.840.113549.1.9.4");
        foreach (DerCodec.DerTlv attribute in Children(DerCodec.ReadTlv(attributes).Content))
        {
            List<DerCodec.DerTlv> parts = Children(attribute.Content);
            if (parts[0].Full.AsSpan().SequenceEqual(oid))
            {
                return Children(parts[1].Content)[0].Content;
            }
        }

        throw new InvalidOperationException("The signed attributes have no messageDigest.");
    }

    private static byte[] P1363(byte[] der, int half)
    {
        List<DerCodec.DerTlv> integers = Children(DerCodec.ReadTlv(der).Content);
        Assert.Equal(2, integers.Count);
        var result = new byte[half * 2];
        for (int i = 0; i < 2; i++)
        {
            byte[] value = integers[i].Content;
            Assert.True(value.Length <= half + 1, "The DER integer is longer than the curve allows.");
            Assert.False(value.Length > 1 && value[0] == 0 && (value[1] & 0x80) == 0, "The DER integer is not minimal.");
            int skip = value.Length > half ? 1 : 0;
            Buffer.BlockCopy(value, skip, result, (i * half) + half - (value.Length - skip), value.Length - skip);
        }

        return result;
    }

    // ContentInfo, then its [0] EXPLICIT SignedData: version, digestAlgorithms, encapContentInfo, [0] certificates, signerInfos.
    private static List<DerCodec.DerTlv> SignedData(byte[] cms)
    {
        DerCodec.DerTlv explicitContent = Children(DerCodec.ReadTlv(cms).Content)[1];
        return Children(Children(explicitContent.Content)[0].Content);
    }

    private static List<DerCodec.DerTlv> Children(byte[] content)
    {
        var children = new List<DerCodec.DerTlv>();
        int offset = 0;
        while (offset < content.Length)
        {
            DerCodec.DerTlv child = DerCodec.ReadTlv(content, offset);
            children.Add(child);
            offset += child.Full.Length;
        }

        return children;
    }

    private static (byte[] SignedBytes, byte[] Cms) SignedParts(byte[] saved)
    {
        string text = Encoding.ASCII.GetString(saved);
        Match range = Regex.Match(text, @"/ByteRange\s*\[\s*(\d{10})\s+(\d{10})\s+(\d{10})\s+(\d{10})\s*\]");
        int length0 = int.Parse(range.Groups[2].Value);
        int offset1 = int.Parse(range.Groups[3].Value);
        int length1 = int.Parse(range.Groups[4].Value);
        var signed = new byte[length0 + length1];
        Buffer.BlockCopy(saved, 0, signed, 0, length0);
        Buffer.BlockCopy(saved, offset1, signed, length0, length1);
        Match contents = Regex.Match(text, @"/Contents\s*<([0-9A-Fa-f]+)>");
        return (signed, DerCodec.ReadTlv(TestCompat.FromHexString(contents.Groups[1].Value)).Full);
    }

    private static byte[] Sign(byte[] pdf, PdfDigestAlgorithm digestAlgorithm, IPdfSignatureProvider provider)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var destination = new MemoryStream();
        document.Sign(
            destination,
            new PdfSignatureOptions { DigestAlgorithm = digestAlgorithm, SigningTime = TestCompat.UnixEpoch },
            provider,
            leaveOpen: true);
        return destination.ToArray();
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
        Assert.Equal(pdf, stream.ToArray());
        Assert.Equal(same.Message, cross.Message);
        return cross;
    }

    private static byte[] Page()
    {
        return PdfBuilder.Basic().Finish();
    }

    private static byte[] Fill(byte first, byte rest = 0x80)
    {
        var half = new byte[32];
        for (int i = 0; i < half.Length; i++)
        {
            half[i] = rest;
        }

        half[0] = first;
        return half;
    }

    private static byte[] Concat(byte[] left, byte[] right)
    {
        var result = new byte[left.Length + right.Length];
        Buffer.BlockCopy(left, 0, result, 0, left.Length);
        Buffer.BlockCopy(right, 0, result, left.Length, right.Length);
        return result;
    }

    private sealed class UncalledAuthority : ITimestampAuthority
    {
        public int Calls { get; private set; }

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("The authority must not be called.");
        }
    }
}
