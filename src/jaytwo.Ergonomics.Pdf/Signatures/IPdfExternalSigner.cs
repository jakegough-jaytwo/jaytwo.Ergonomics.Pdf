using System;
using System.Collections.Generic;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Host-supplied signer. A PKCS#11 module implements this; the library does not load one.
/// </summary>
public interface IPdfExternalSigner
{
    /// <summary>
    /// Gets the leaf certificate, DER encoded.
    /// </summary>
    byte[] CertificateDer { get; }

    /// <summary>
    /// Gets intermediate and root certificates, DER encoded, not including <see cref="CertificateDer"/>.
    /// An empty list means the host has only the leaf.
    /// </summary>
    IReadOnlyList<byte[]> AdditionalCertificates { get; }

    /// <summary>
    /// Signs a SHA-256 digest with RSA PKCS#1 v1.5, matching <c>RSA.SignHash(hash, SHA256, Pkcs1)</c>.
    /// </summary>
    /// <param name="hash">The 32-byte SHA-256 digest. Do not hash it again.</param>
    /// <returns>The RSA signature bytes.</returns>
    byte[] SignHash(ReadOnlySpan<byte> hash);
}
