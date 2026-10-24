using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace jaytwo.Pdf.Tests;

// W-70: the signing keys the library supports, plus P-521, which it refuses.
internal static class TestKeys
{
    public const string Rsa = "rsa-2048";
    public const string P256 = "p-256";
    public const string P384 = "p-384";
    public const string P521 = "p-521";

    public static AsymmetricAlgorithm Create(string kind)
    {
        return kind switch
        {
            Rsa => RSA.Create(2048),
            P256 => ECDsa.Create(ECCurve.NamedCurves.nistP256),
            P384 => ECDsa.Create(ECCurve.NamedCurves.nistP384),
            P521 => ECDsa.Create(ECCurve.NamedCurves.nistP521),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    // The request only names the subject key; the issuer's key signs the certificate.
    public static CertificateRequest Request(string subject, AsymmetricAlgorithm key)
    {
        return key is RSA rsa
            ? new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            : new CertificateRequest(subject, (ECDsa)key, HashAlgorithmName.SHA256);
    }

    public static X509Certificate2 SelfSigned(string subject, AsymmetricAlgorithm key)
    {
        return Request(subject, key).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
