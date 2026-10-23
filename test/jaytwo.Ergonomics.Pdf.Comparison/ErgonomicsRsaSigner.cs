using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Ergonomics.Pdf.Comparison;

internal sealed class ErgonomicsRsaSigner : IPdfExternalSigner
{
    public byte[] CertificateDer => SigningKey.Certificate;

    public IReadOnlyList<byte[]> AdditionalCertificates { get; } = Array.Empty<byte[]>();

    public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
    {
        return new ValueTask<byte[]>(SigningKey.Rsa.SignHash(hash.ToArray(), hashAlgorithm, RSASignaturePadding.Pkcs1));
    }
}
