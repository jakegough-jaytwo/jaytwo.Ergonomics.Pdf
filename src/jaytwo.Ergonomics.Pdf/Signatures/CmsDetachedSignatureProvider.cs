using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Builds a detached CMS SignedData value from a host RSA signer.
/// </summary>
public sealed class CmsDetachedSignatureProvider : IPdfSignatureProvider
{
    private readonly IPdfExternalSigner _signer;
    private readonly ITimestampAuthority? _timestampAuthority;

    /// <summary>
    /// Initializes a new instance of the <see cref="CmsDetachedSignatureProvider"/> class.
    /// </summary>
    /// <param name="signer">The host signer. For PKCS#11, <see cref="IPdfExternalSigner.SignHash"/> must not hash the digest again.</param>
    public CmsDetachedSignatureProvider(IPdfExternalSigner signer)
        : this(signer, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CmsDetachedSignatureProvider"/> class.
    /// </summary>
    /// <param name="signer">The host signer. For PKCS#11, <see cref="IPdfExternalSigner.SignHash"/> must not hash the digest again.</param>
    /// <param name="timestampAuthority">When set, the signature value is timestamped and the token is stored as an unsigned CMS attribute. This is not a document timestamp revision.</param>
    public CmsDetachedSignatureProvider(IPdfExternalSigner signer, ITimestampAuthority? timestampAuthority)
    {
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _timestampAuthority = timestampAuthority;
    }

    /// <summary>
    /// Gets <see cref="PdfSignatureKind.CmsDetached"/>.
    /// </summary>
    public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

    /// <summary>
    /// Creates a detached CMS SignedData value over the signed byte ranges.
    /// </summary>
    /// <param name="context">The signed ranges.</param>
    /// <returns>The CMS ContentInfo bytes.</returns>
    public byte[] CreateContents(PdfSignatureContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        byte[] digest;
        using (Stream signed = context.OpenSignedBytes())
        using (SHA256 sha = SHA256.Create())
        {
            digest = sha.ComputeHash(signed);
        }

        (byte[] issuer, byte[] serial) = DerCodec.ReadCertificateIdentity(_signer.CertificateDer);
        byte[] contentType = DerCodec.Sequence(
            DerCodec.Oid("1.2.840.113549.1.9.3"),
            DerCodec.Set(DerCodec.Oid("1.2.840.113549.1.7.1")));
        byte[] messageDigest = DerCodec.Sequence(
            DerCodec.Oid("1.2.840.113549.1.9.4"),
            DerCodec.Set(DerCodec.OctetString(digest)));
        DateTimeOffset when = context.SigningTime ?? DateTimeOffset.UtcNow;
        byte[] signingTime = DerCodec.Sequence(
            DerCodec.Oid("1.2.840.113549.1.9.5"),
            DerCodec.Set(DerCodec.UtcTime(when)));
        byte[] signedAttrs = DerCodec.Set(contentType, messageDigest, signingTime);
        byte[] signature;
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(signedAttrs);
            try
            {
                signature = _signer.SignHash(hash);
            }
            catch (Exception)
            {
                throw new CryptographicException("The external signer failed.");
            }
        }

        byte[]? unsignedAttrs = null;
        if (_timestampAuthority != null)
        {
            byte[] imprint;
            using (SHA256 sha256 = SHA256.Create())
            {
                imprint = sha256.ComputeHash(signature);
            }

            byte[] nonce = Rfc3161TimeStampRequest.NewNonce();
            byte[] token = _timestampAuthority.GetTimeStampToken(Rfc3161TimeStampRequest.Create(imprint, nonce));
            Rfc3161TimeStampToken.RequireMatch(token, imprint, nonce);

            byte[] attribute = DerCodec.Sequence(
                DerCodec.Oid("1.2.840.113549.1.9.16.2.14"),
                DerCodec.Set(token));
            unsignedAttrs = DerCodec.ReplaceTag(DerCodec.Set(attribute), 0xA1);
        }

        byte[] signerInfo = unsignedAttrs == null
            ? DerCodec.Sequence(
                DerCodec.Integer(1),
                DerCodec.Sequence(issuer, serial),
                DerCodec.AlgorithmSha256(),
                DerCodec.ReplaceTag(signedAttrs, 0xA0),
                DerCodec.AlgorithmSha256WithRsa(),
                DerCodec.OctetString(signature))
            : DerCodec.Sequence(
                DerCodec.Integer(1),
                DerCodec.Sequence(issuer, serial),
                DerCodec.AlgorithmSha256(),
                DerCodec.ReplaceTag(signedAttrs, 0xA0),
                DerCodec.AlgorithmSha256WithRsa(),
                DerCodec.OctetString(signature),
                unsignedAttrs);
        var certificates = new List<byte[]> { _signer.CertificateDer };
        if (_signer.AdditionalCertificates != null)
        {
            foreach (byte[] extra in _signer.AdditionalCertificates)
            {
                if (extra == null || extra.Length == 0)
                {
                    throw new PdfSignException("An additional certificate is empty.");
                }

                certificates.Add(extra);
            }
        }

        byte[] certificateSet = DerCodec.ReplaceTag(DerCodec.Set(certificates.ToArray()), 0xA0);
        byte[] signedData = DerCodec.Sequence(
            DerCodec.Integer(1),
            DerCodec.Set(DerCodec.AlgorithmSha256()),
            DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.7.1")),
            certificateSet,
            DerCodec.Set(signerInfo));
        return DerCodec.Sequence(
            DerCodec.Oid("1.2.840.113549.1.7.2"),
            DerCodec.ExplicitContext(0, signedData));
    }
}
