using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Host-supplied signer. A PKCS#11 module or a cloud key service implements this; the library does not load one.
/// An exception thrown by any member, including the enumeration of <see cref="AdditionalCertificates"/>,
/// is replaced with a generic <see cref="CryptographicException"/> that has no inner exception, and the destination is not written.
/// </summary>
public interface IPdfExternalSigner
{
    /// <summary>
    /// Gets the leaf certificate, DER encoded. It is read once per signature, before <see cref="SignHashAsync"/>.
    /// It must not be empty.
    /// </summary>
    byte[] CertificateDer { get; }

    /// <summary>
    /// Gets intermediate and root certificates, DER encoded, not including <see cref="CertificateDer"/>.
    /// An empty list means the host has only the leaf. The list is enumerated once, before <see cref="SignHashAsync"/>.
    /// Enumeration stops with <see cref="PdfSignException"/> as soon as the certificates read so far exceed 1,048,576 bytes, the largest reservation.
    /// </summary>
    IReadOnlyList<byte[]> AdditionalCertificates { get; }

    /// <summary>
    /// Signs a digest with the key of <see cref="CertificateDer"/>. The library reads the key type from that certificate.
    /// For an RSA key, sign with PKCS#1 v1.5, matching <c>RSA.SignHash(hash, hashAlgorithm, RSASignaturePadding.Pkcs1)</c>;
    /// a PKCS#11 <c>CKM_RSA_PKCS</c> host prepends the DigestInfo for <paramref name="hashAlgorithm"/>.
    /// For an ECDSA P-256 or P-384 key, return the IEEE P1363 <c>r||s</c> value, 64 or 96 bytes, that <c>ECDsa.SignHash(hash)</c>
    /// and PKCS#11 <c>CKM_ECDSA</c> produce; the library DER-encodes it.
    /// Exceptions thrown by the implementation are replaced with a generic <see cref="CryptographicException"/>,
    /// or with a fresh <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> has been canceled.
    /// A CMS value that exceeds the reserved contents size throws <see cref="PdfSignatureTooLargeException"/> with its exact length, after this call.
    /// </summary>
    /// <param name="hash">The raw digest: 32 bytes for SHA-256, 48 for SHA-384, 64 for SHA-512. Do not hash it again; a mechanism such as <c>CKM_SHA256_RSA_PKCS</c> or <c>CKM_ECDSA_SHA256</c> would.</param>
    /// <param name="hashAlgorithm">The digest algorithm from <see cref="PdfSignatureOptions.DigestAlgorithm"/>: <see cref="HashAlgorithmName.SHA256"/>, <see cref="HashAlgorithmName.SHA384"/>, or <see cref="HashAlgorithmName.SHA512"/>.</param>
    /// <param name="cancellationToken">Canceled when the caller of <see cref="PdfDocument.SignAsync(PdfSignatureOptions, IPdfSignatureProvider, CancellationToken)"/> cancels. <see cref="PdfDocument.Sign(PdfSignatureOptions, IPdfSignatureProvider)"/> passes <see cref="CancellationToken.None"/> and waits for the result.</param>
    /// <returns>The RSA signature, or the P1363 ECDSA signature.</returns>
    ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken);
}
