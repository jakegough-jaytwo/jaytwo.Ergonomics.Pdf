using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace jaytwo.Ergonomics.Pdf.Comparison;

// One RSA 2048 key and self-signed certificate per process, created before the first measured pass.
internal static class SigningKey
{
    public static RSA Rsa { get; } = RSA.Create(2048);

    public static byte[] Certificate { get; } = SelfSigned(Rsa);

    private static byte[] SelfSigned(RSA rsa)
    {
        var request = new CertificateRequest("CN=G7 Comparison", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificate.RawData;
    }
}
