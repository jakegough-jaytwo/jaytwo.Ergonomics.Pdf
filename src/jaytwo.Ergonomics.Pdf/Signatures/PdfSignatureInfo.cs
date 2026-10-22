using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// One signature field and what its signature dictionary and CMS value say about the signature (spec R9).
/// Nothing here is validated: the signature is not verified, the certificates are not checked for trust or revocation,
/// and the signed byte range is not read.
/// </summary>
public sealed class PdfSignatureInfo
{
    internal PdfSignatureInfo(
        string fieldName,
        bool isSigned,
        string? subFilter,
        byte[]? signerCertificate,
        IReadOnlyList<byte[]> certificates,
        string? digestAlgorithmOid,
        string? signatureAlgorithmOid,
        DateTimeOffset? signingTime,
        DateTimeOffset? cmsSigningTime,
        string? name,
        string? reason,
        string? location,
        string? contactInfo,
        PdfByteRange? byteRange,
        int? signedRevisionNumber,
        bool hasLaterRevisions,
        DateTimeOffset? timestampTime,
        int? certificationLevel)
    {
        FieldName = fieldName;
        IsSigned = isSigned;
        SubFilter = subFilter;
        SignerCertificate = signerCertificate;
        Certificates = certificates ?? throw new ArgumentNullException(nameof(certificates));
        DigestAlgorithmOid = digestAlgorithmOid;
        DigestAlgorithm = digestAlgorithmOid switch
        {
            "1.2.840.113549.2.5" => HashAlgorithmName.MD5,
            "1.3.14.3.2.26" => HashAlgorithmName.SHA1,
            "2.16.840.1.101.3.4.2.1" => HashAlgorithmName.SHA256,
            "2.16.840.1.101.3.4.2.2" => HashAlgorithmName.SHA384,
            "2.16.840.1.101.3.4.2.3" => HashAlgorithmName.SHA512,
            _ => null,
        };
        SignatureAlgorithmOid = signatureAlgorithmOid;
        SigningTime = signingTime;
        CmsSigningTime = cmsSigningTime;
        Name = name;
        Reason = reason;
        Location = location;
        ContactInfo = contactInfo;
        ByteRange = byteRange;
        SignedRevisionNumber = signedRevisionNumber;
        HasLaterRevisions = hasLaterRevisions;
        TimestampTime = timestampTime;
        CertificationLevel = certificationLevel;
    }

    /// <summary>
    /// Gets the field's full dotted name.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// Gets a value indicating whether the field's <c>/V</c> resolves to a signature dictionary.
    /// When false, every other property is null, empty, or false.
    /// </summary>
    public bool IsSigned { get; }

    /// <summary>
    /// Gets the <c>/SubFilter</c> name without the slash, such as <c>adbe.pkcs7.detached</c>, <c>ETSI.CAdES.detached</c>,
    /// or <c>ETSI.RFC3161</c> for a document timestamp. The CMS-derived properties are read for those three and
    /// <c>adbe.pkcs7.sha1</c>, and are null for any other sub-filter.
    /// </summary>
    public string? SubFilter { get; }

    /// <summary>
    /// Gets the DER certificate in <see cref="Certificates"/> that the SignerInfo identifies, by issuer and serial number or by
    /// subject key identifier, or null when the CMS does not embed it. For a document timestamp it is the TSA certificate.
    /// </summary>
    public byte[]? SignerCertificate { get; }

    /// <summary>
    /// Gets every DER certificate in the CMS certificate set, in the order stored, the signer's included.
    /// Attribute certificates and other certificate choices are left out.
    /// </summary>
    public IReadOnlyList<byte[]> Certificates { get; }

    /// <summary>
    /// Gets the SignerInfo digest algorithm, or null when it is not MD5, SHA-1, SHA-256, SHA-384, or SHA-512.
    /// </summary>
    public HashAlgorithmName? DigestAlgorithm { get; }

    /// <summary>
    /// Gets the dotted object identifier of the SignerInfo digest algorithm.
    /// </summary>
    public string? DigestAlgorithmOid { get; }

    /// <summary>
    /// Gets the dotted object identifier of the SignerInfo signature algorithm, such as <c>1.2.840.113549.1.1.1</c> (rsaEncryption),
    /// <c>1.2.840.113549.1.1.11</c> (sha256WithRSAEncryption), or <c>1.2.840.10045.4.3.2</c> (ecdsa-with-SHA256).
    /// </summary>
    public string? SignatureAlgorithmOid { get; }

    /// <summary>
    /// Gets the signature dictionary's <c>/M</c>, the signing time the signer claims. It is not a trusted time.
    /// </summary>
    public DateTimeOffset? SigningTime { get; }

    /// <summary>
    /// Gets the CMS signing-time signed attribute. A PAdES signature has none, so its time is only in <see cref="SigningTime"/>.
    /// </summary>
    public DateTimeOffset? CmsSigningTime { get; }

    /// <summary>
    /// Gets <c>/Name</c>, the signer's name as the signature dictionary states it.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets <c>/Reason</c>.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Gets <c>/Location</c>.
    /// </summary>
    public string? Location { get; }

    /// <summary>
    /// Gets <c>/ContactInfo</c>.
    /// </summary>
    public string? ContactInfo { get; }

    /// <summary>
    /// Gets <c>/ByteRange</c>: the two signed ranges, with the <c>/Contents</c> value between them.
    /// </summary>
    public PdfByteRange? ByteRange { get; }

    /// <summary>
    /// Gets the end of the second signed range, which is the offset where the revision the signature signs ends.
    /// </summary>
    public long? SignedRevisionEnd => ByteRange is PdfByteRange range ? range.Offset1 + range.Length1 : null;

    /// <summary>
    /// Gets the 1-based logical revision whose end offset matches <see cref="SignedRevisionEnd"/>, or null when no listed
    /// revision ends there. A linearized file's main and first-page cross-reference sections count as one revision.
    /// </summary>
    public int? SignedRevisionNumber { get; }

    /// <summary>
    /// Gets a value indicating whether the newest cross-reference section starts at or after <see cref="SignedRevisionEnd"/>,
    /// so a later incremental update follows the signed revision. Bytes after the signed range that add no cross-reference
    /// section are not a revision.
    /// </summary>
    public bool HasLaterRevisions { get; }

    /// <summary>
    /// Gets the <c>genTime</c> of the RFC 3161 time-stamp token: the first signature-time-stamp unsigned attribute of a CMS signature,
    /// or for an <c>ETSI.RFC3161</c> document timestamp the token that is the whole <c>/Contents</c>. Null when there is no token.
    /// The token is not verified.
    /// </summary>
    public DateTimeOffset? TimestampTime { get; }

    /// <summary>
    /// Gets the DocMDP <c>P</c> value, 1, 2, or 3, when the signature certifies the document through a DocMDP <c>/Reference</c>,
    /// otherwise null. A certification whose parameters do not resolve or whose <c>P</c> is not 1 to 3 reports 1, as the write rules treat it.
    /// </summary>
    public int? CertificationLevel { get; }
}
