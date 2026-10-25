using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Signatures;

/// <summary>
/// Builds a detached CMS SignedData value from a host RSA or ECDSA signer, in the format that <see cref="PdfSignatureContext.Format"/> names.
/// </summary>
public sealed class CmsDetachedSignatureProvider : IPdfSignatureProvider
{
    private static readonly byte[] RsaEncryption = DerCodec.Oid("1.2.840.113549.1.1.1");
    private static readonly byte[] EcPublicKey = DerCodec.Oid("1.2.840.10045.2.1");
    private static readonly byte[] CurveP256 = DerCodec.Oid("1.2.840.10045.3.1.7");
    private static readonly byte[] CurveP384 = DerCodec.Oid("1.3.132.0.34");

    private readonly IPdfExternalSigner _signer;
    private readonly ITimestampAuthority? _timestampAuthority;

    /// <summary>
    /// Initializes a new instance of the <see cref="CmsDetachedSignatureProvider"/> class.
    /// </summary>
    /// <param name="signer">The host signer. For PKCS#11, <see cref="IPdfExternalSigner.SignHashAsync"/> must not hash the digest again.</param>
    public CmsDetachedSignatureProvider(IPdfExternalSigner signer)
        : this(signer, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CmsDetachedSignatureProvider"/> class.
    /// </summary>
    /// <param name="signer">The host signer. For PKCS#11, <see cref="IPdfExternalSigner.SignHashAsync"/> must not hash the digest again.</param>
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
    /// <param name="cancellationToken">Passed to the signer and the timestamp authority.</param>
    /// <returns>The CMS ContentInfo bytes.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled. An exception the signer or a host authority throws after cancellation is replaced with a fresh one.</exception>
    /// <exception cref="CryptographicException">A member of the host signer threw. The message is generic, and the exception has no inner exception.</exception>
    /// <exception cref="PdfSignException">The leaf certificate is null, empty, or not a DER-encoded certificate; its key is not RSA, ECDSA P-256, or ECDSA P-384; an additional certificate or the signature value is null or empty; or an ECDSA signature value is not the P1363 length for its curve.</exception>
    /// <exception cref="PdfSignException">The certificates exceed 1,048,576 bytes, the largest reservation, so no reservation can hold the signature. The chain is not enumerated further, and the signer is not called.</exception>
    /// <exception cref="PdfSignatureTooLargeException">The CMS value is larger than <see cref="PdfSignatureContext.ReservedContentsBytes"/>. <see cref="PdfSignatureTooLargeException.ActualBytes"/> is its exact length. The signer and any timestamp authority have been called, and a time-stamp token is not parsed.</exception>
    /// <exception cref="PdfTimestampException">A host timestamp authority threw; the message is generic, and the exception has no inner exception. Or the time-stamp token does not match the request by message imprint, digest algorithm, or nonce, or it does not include the TSA signing certificate that its ESS signing-certificate attribute identifies. The request sets certReq. Inclusion is checked; the certificate chain and trust are not.</exception>
    public async ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        cancellationToken.ThrowIfCancellationRequested();

        List<byte[]> certificates = ReadCertificates();
        (byte[] issuer, byte[] serial, int ecdsaHalf) = ReadLeaf(certificates[0]);
        (string digestOid, HashAlgorithmName hashAlgorithm, int arc) = Digest(context.DigestAlgorithm);

        byte[] digest;
        using (Stream signed = context.OpenSignedBytes())
        using (HashAlgorithm hasher = CreateHash(hashAlgorithm))
        {
            digest = hasher.ComputeHash(signed);
        }

        byte[] signedAttrs;
        byte[]? signature;
        try
        {
            byte[] contentType = DerCodec.Sequence(
                DerCodec.Oid("1.2.840.113549.1.9.3"),
                DerCodec.Set(DerCodec.Oid("1.2.840.113549.1.7.1")));
            byte[] messageDigest = DerCodec.Sequence(
                DerCodec.Oid("1.2.840.113549.1.9.4"),
                DerCodec.Set(DerCodec.OctetString(digest)));
            signedAttrs = context.Format == PdfSignatureFormat.PadesBaselineB
                ? DerCodec.Set(contentType, messageDigest, SigningCertificateV2(certificates[0], issuer, serial))
                : DerCodec.Set(contentType, messageDigest, SigningTime(context.SigningTime ?? DateTimeOffset.UtcNow));
            byte[] hash;
            using (HashAlgorithm hasher = CreateHash(hashAlgorithm))
            {
                hash = hasher.ComputeHash(signedAttrs);
            }

            try
            {
                signature = await CallSignerAsync(hash, hashAlgorithm, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Clear(hash);
            }
        }
        finally
        {
            Clear(digest);
        }

        if (signature == null || signature.Length == 0)
        {
            throw new PdfSignException("The external signer returned an empty signature.");
        }

        byte[] signatureAlgorithm;
        if (ecdsaHalf == 0)
        {
            // sha256WithRSAEncryption is arc 11, sha384 12, and sha512 13.
            signatureAlgorithm = DerCodec.Algorithm("1.2.840.113549.1.1." + (10 + arc).ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            // ecdsa-with-SHA256, -SHA384, and -SHA512 are arcs 2, 3, and 4 and take no parameters (RFC 5758 3.2).
            signatureAlgorithm = DerCodec.Sequence(DerCodec.Oid("1.2.840.10045.4.3." + (1 + arc).ToString(CultureInfo.InvariantCulture)));
            signature = EncodeEcdsa(signature, ecdsaHalf);
        }

        byte[]? unsignedAttrs = null;
        byte[]? token = null;
        byte[]? imprint = null;
        byte[]? nonce = null;
        try
        {
            if (_timestampAuthority != null)
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    imprint = sha256.ComputeHash(signature);
                }

                nonce = Rfc3161TimeStampRequest.NewNonce();
                byte[]? request = null;
                try
                {
                    request = Rfc3161TimeStampRequest.Create(imprint, nonce);
                    token = await TimestampAuthorityCall.GetTokenAsync(_timestampAuthority, request, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    Clear(request);
                }

                if (token == null)
                {
                    Rfc3161TimeStampToken.RequireMatch(token, imprint, nonce);
                }

                // The token is wrapped verbatim, so the CMS length below is exact before the token is parsed.
                byte[] attribute = DerCodec.Sequence(
                    DerCodec.Oid("1.2.840.113549.1.9.16.2.14"),
                    DerCodec.Set(token!));
                unsignedAttrs = DerCodec.ReplaceTag(DerCodec.Set(attribute), 0xA1);
            }

            byte[] signerInfo = unsignedAttrs == null
                ? DerCodec.Sequence(
                    DerCodec.Integer(1),
                    DerCodec.Sequence(issuer, serial),
                    DerCodec.Algorithm(digestOid),
                    DerCodec.ReplaceTag(signedAttrs, 0xA0),
                    signatureAlgorithm,
                    DerCodec.OctetString(signature))
                : DerCodec.Sequence(
                    DerCodec.Integer(1),
                    DerCodec.Sequence(issuer, serial),
                    DerCodec.Algorithm(digestOid),
                    DerCodec.ReplaceTag(signedAttrs, 0xA0),
                    signatureAlgorithm,
                    DerCodec.OctetString(signature),
                    unsignedAttrs);
            byte[] certificateSet = DerCodec.ReplaceTag(DerCodec.Set(certificates.ToArray()), 0xA0);
            byte[] signedData = DerCodec.Sequence(
                DerCodec.Integer(1),
                DerCodec.Set(DerCodec.Algorithm(digestOid)),
                DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.7.1")),
                certificateSet,
                DerCodec.Set(signerInfo));
            byte[] contents = DerCodec.Sequence(
                DerCodec.Oid("1.2.840.113549.1.7.2"),
                DerCodec.ExplicitContext(0, signedData));
            if (contents.Length > context.ReservedContentsBytes)
            {
                throw new PdfSignatureTooLargeException(contents.Length, context.ReservedContentsBytes);
            }

            if (token == null)
            {
                return contents;
            }

            Rfc3161TimeStampToken.RequireMatch(token, imprint!, nonce!);
            return contents;
        }
        finally
        {
            Clear(imprint);
            Clear(nonce);
        }
    }

    private static T CallSigner<T>(Func<T> access)
    {
        try
        {
            return access();
        }
        catch (Exception)
        {
            throw SignerFailed();
        }
    }

    // A fixed-depth walk: Certificate, then TBSCertificate, then its fields up to the public key. The leaf must be
    // one SEQUENCE with nothing after it, and the serial, issuer, and key algorithm must carry their DER tags.
    // EcdsaHalf is 0 for an RSA key, otherwise the length of r and of s in a P1363 signature.
    private static (byte[] Issuer, byte[] Serial, int EcdsaHalf) ReadLeaf(byte[] leaf)
    {
        DerCodec.DerTlv certificate;
        DerCodec.DerTlv tbs;
        (byte[] Issuer, byte[] Serial) identity;
        DerCodec.DerTlv keyAlgorithm;
        DerCodec.DerTlv keyOid;
        byte[]? curve = null;
        try
        {
            certificate = DerCodec.ReadTlv(leaf);
            tbs = DerCodec.ReadTlv(certificate.Content);
            identity = DerCodec.ReadCertificateIdentity(leaf);
            keyAlgorithm = DerCodec.ReadCertificateKeyAlgorithm(leaf);
            keyOid = DerCodec.ReadTlv(keyAlgorithm.Content);
            if (keyOid.Full.Length < keyAlgorithm.Content.Length)
            {
                curve = DerCodec.ReadTlv(keyAlgorithm.Content, keyOid.Full.Length).Full;
            }
        }
        catch (PdfTimestampException)
        {
            throw NotCertificate();
        }

        if (certificate.Tag != 0x30
            || certificate.Full.Length != leaf.Length
            || tbs.Tag != 0x30
            || identity.Serial[0] != 0x02
            || identity.Issuer[0] != 0x30
            || keyAlgorithm.Tag != 0x30)
        {
            throw NotCertificate();
        }

        if (Same(keyOid.Full, RsaEncryption))
        {
            return (identity.Issuer, identity.Serial, 0);
        }

        if (Same(keyOid.Full, EcPublicKey) && curve != null)
        {
            if (Same(curve, CurveP256))
            {
                return (identity.Issuer, identity.Serial, 32);
            }

            if (Same(curve, CurveP384))
            {
                return (identity.Issuer, identity.Serial, 48);
            }
        }

        throw new PdfSignException("The signer certificate key must be RSA, ECDSA P-256, or ECDSA P-384.");
    }

    private static byte[] SigningTime(DateTimeOffset when)
    {
        return DerCodec.Sequence(
            DerCodec.Oid("1.2.840.113549.1.9.5"),
            DerCodec.Set(DerCodec.UtcTime(when)));
    }

    // ESS signing-certificate-v2 (RFC 5035 3) holds one ESSCertIDv2. Its certHash is SHA-256, so hashAlgorithm takes its
    // DEFAULT and DER omits it. issuerSerial names the leaf issuer as the GeneralNames directoryName [4], as pyHanko writes it.
    private static byte[] SigningCertificateV2(byte[] leaf, byte[] issuer, byte[] serial)
    {
        byte[] certHash;
        using (SHA256 sha256 = SHA256.Create())
        {
            certHash = sha256.ComputeHash(leaf);
        }

        byte[] issuerSerial = DerCodec.Sequence(DerCodec.Sequence(DerCodec.ExplicitContext(4, issuer)), serial);
        byte[] certId = DerCodec.Sequence(DerCodec.OctetString(certHash), issuerSerial);
        return DerCodec.Sequence(
            DerCodec.Oid("1.2.840.113549.1.9.16.2.47"),
            DerCodec.Set(DerCodec.Sequence(DerCodec.Sequence(certId))));
    }

    private static bool Same(byte[] left, byte[] right)
    {
        return ((ReadOnlySpan<byte>)left).SequenceEqual(right);
    }

    private static void Clear(byte[]? bytes)
    {
        if (bytes != null)
        {
            Array.Clear(bytes, 0, bytes.Length);
        }
    }

    // The digest OID, its framework name, and its arc under 2.16.840.1.101.3.4.2, which also numbers the signature algorithms.
    private static (string Oid, HashAlgorithmName Name, int Arc) Digest(PdfDigestAlgorithm algorithm)
    {
        switch (algorithm)
        {
            case PdfDigestAlgorithm.Sha256:
                return ("2.16.840.1.101.3.4.2.1", HashAlgorithmName.SHA256, 1);
            case PdfDigestAlgorithm.Sha384:
                return ("2.16.840.1.101.3.4.2.2", HashAlgorithmName.SHA384, 2);
            case PdfDigestAlgorithm.Sha512:
                return ("2.16.840.1.101.3.4.2.3", HashAlgorithmName.SHA512, 3);
            default:
                throw new PdfSignException("The digest algorithm must be SHA-256, SHA-384, or SHA-512.");
        }
    }

    private static HashAlgorithm CreateHash(HashAlgorithmName name)
    {
        if (name == HashAlgorithmName.SHA384)
        {
            return SHA384.Create();
        }

        return name == HashAlgorithmName.SHA512 ? SHA512.Create() : SHA256.Create();
    }

    // CMS carries ECDSA-Sig-Value, SEQUENCE { r INTEGER, s INTEGER } (RFC 5753 7.2), not the P1363 r||s the host returns.
    private static byte[] EncodeEcdsa(byte[] signature, int half)
    {
        if (signature.Length != half * 2)
        {
            throw new PdfSignException("The ECDSA signature must be the " + (half * 2).ToString(CultureInfo.InvariantCulture) + "-byte IEEE P1363 r||s value.");
        }

        return DerCodec.Sequence(
            DerCodec.Integer(new ReadOnlySpan<byte>(signature, 0, half)),
            DerCodec.Integer(new ReadOnlySpan<byte>(signature, half, half)));
    }

    private static PdfSignException NotCertificate()
    {
        return new PdfSignException("The signer certificate is not a DER-encoded certificate.");
    }

    private static CryptographicException SignerFailed()
    {
        return new CryptographicException("The external signer failed.");
    }

    private static void AddToTotal(ref long total, int length)
    {
        total += length;
        if (total > PdfSignatureOptions.MaxReservedContentsBytes)
        {
            throw new PdfSignException("The signer certificates exceed 1048576 bytes, the largest signature that can be reserved.");
        }
    }

    // A host that throws after the caller canceled gets a fresh cancellation, so none of its details leave either way.
    private async ValueTask<byte[]?> CallSignerAsync(byte[] hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
    {
        try
        {
            return await _signer.SignHashAsync(hash, hashAlgorithm, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception)
        {
            throw SignerFailed();
        }
    }

    // Reads CertificateDer once, and enumerates AdditionalCertificates only until the running total passes
    // the largest reservation, which bounds an endless chain. A host exception becomes the generic signer failure.
    private List<byte[]> ReadCertificates()
    {
        long total = 0;
        byte[]? leaf = CallSigner(() => _signer.CertificateDer);
        if (leaf == null || leaf.Length == 0)
        {
            throw new PdfSignException("The signer certificate is empty.");
        }

        AddToTotal(ref total, leaf.Length);
        var certificates = new List<byte[]> { leaf };
        IReadOnlyList<byte[]>? additional = CallSigner(() => _signer.AdditionalCertificates);
        if (additional == null)
        {
            return certificates;
        }

        IEnumerator<byte[]>? enumerator = CallSigner(() => additional.GetEnumerator());
        try
        {
            while (CallSigner(() => enumerator!.MoveNext()))
            {
                byte[]? extra = CallSigner(() => enumerator!.Current);
                if (extra == null || extra.Length == 0)
                {
                    throw new PdfSignException("An additional certificate is empty.");
                }

                AddToTotal(ref total, extra.Length);
                certificates.Add(extra);
            }
        }
        catch (Exception)
        {
            try
            {
                enumerator?.Dispose();
            }
            catch (Exception)
            {
                // The exception already propagating wins over a failure to dispose.
            }

            throw;
        }

        CallSigner(() =>
        {
            enumerator?.Dispose();
            return true;
        });
        return certificates;
    }
}
