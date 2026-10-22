using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Ergonomics.Pdf.Signatures;

namespace jaytwo.Ergonomics.Pdf.Fuzz;

/// <summary>
/// A self-signed RSA signer so the edit target reaches the signing path. The key lives only for the process.
/// </summary>
internal sealed class SeedSigner : IPdfExternalSigner
{
    private readonly RSA _rsa;

    private SeedSigner(RSA rsa, byte[] certificate)
    {
        _rsa = rsa;
        CertificateDer = certificate;
    }

    public byte[] CertificateDer { get; }

    public IReadOnlyList<byte[]> AdditionalCertificates { get; } = Array.Empty<byte[]>();

    public static SeedSigner Create()
    {
        RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=jaytwo fuzz signer", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return new SeedSigner(rsa, certificate.RawData);
    }

    public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
    {
        return new ValueTask<byte[]>(_rsa.SignHash(hash.ToArray(), hashAlgorithm, RSASignaturePadding.Pkcs1));
    }
}
