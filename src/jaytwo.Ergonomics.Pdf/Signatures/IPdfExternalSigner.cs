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
    /// It must not be empty, and it counts toward the reserved contents size.
    /// </summary>
    byte[] CertificateDer { get; }

    /// <summary>
    /// Gets intermediate and root certificates, DER encoded, not including <see cref="CertificateDer"/>.
    /// An empty list means the host has only the leaf. The list is enumerated once, before <see cref="SignHashAsync"/>.
    /// Enumeration stops with <see cref="PdfSignatureTooLargeException"/> as soon as the certificates read so far exceed the reserved contents size.
    /// </summary>
    IReadOnlyList<byte[]> AdditionalCertificates { get; }

    /// <summary>
    /// Signs a SHA-256 digest with RSA PKCS#1 v1.5, matching <c>RSA.SignHash(hash, SHA256, Pkcs1)</c>.
    /// Exceptions thrown by the implementation are replaced with a generic <see cref="CryptographicException"/>,
    /// or with a fresh <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> has been canceled.
    /// A signature that, with the certificates, exceeds the reserved contents size throws <see cref="PdfSignatureTooLargeException"/>.
    /// </summary>
    /// <param name="hash">The 32-byte SHA-256 digest. Do not hash it again.</param>
    /// <param name="cancellationToken">Canceled when the caller of <see cref="PdfDocument.SignAsync(PdfSignatureOptions, IPdfSignatureProvider, CancellationToken)"/> cancels. <see cref="PdfDocument.Sign(PdfSignatureOptions, IPdfSignatureProvider)"/> passes <see cref="CancellationToken.None"/> and waits for the result.</param>
    /// <returns>The RSA signature bytes.</returns>
    ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, CancellationToken cancellationToken);
}
