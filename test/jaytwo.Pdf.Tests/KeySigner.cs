using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Tests;

// Signs with an in-memory RSA or ECDSA key the way a host does: PKCS#1 v1.5 for RSA, and the
// P1363 r||s that ECDsa.SignHash returns for ECDSA. It records what the library asked it to sign.
internal sealed class KeySigner : IPdfExternalSigner
{
    private readonly AsymmetricAlgorithm _key;
    private readonly Func<byte[], byte[]>? _transform;

    public KeySigner(AsymmetricAlgorithm key, byte[] certificate)
        : this(key, certificate, Array.Empty<byte[]>(), null)
    {
    }

    public KeySigner(AsymmetricAlgorithm key, byte[] certificate, IReadOnlyList<byte[]> additionalCertificates, Func<byte[], byte[]>? transform)
    {
        _key = key;
        _transform = transform;
        CertificateDer = certificate;
        AdditionalCertificates = additionalCertificates;
    }

    public byte[] CertificateDer { get; }

    public IReadOnlyList<byte[]> AdditionalCertificates { get; }

    public List<(int HashLength, HashAlgorithmName Algorithm)> Calls { get; } = new List<(int HashLength, HashAlgorithmName Algorithm)>();

    public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
    {
        Calls.Add((hash.Length, hashAlgorithm));
        byte[] signature = _key is RSA rsa
            ? rsa.SignHash(hash.ToArray(), hashAlgorithm, RSASignaturePadding.Pkcs1)
            : ((ECDsa)_key).SignHash(hash.ToArray());
        return new ValueTask<byte[]>(_transform == null ? signature : _transform(signature));
    }
}
