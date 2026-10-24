using System;
using System.Security.Cryptography;

namespace jaytwo.Pdf.Signatures;

internal static class Rfc3161TimeStampToken
{
    private const int MaxTokenBytes = 8 * 1024 * 1024;
    private const string SignedDataOid = "1.2.840.113549.1.7.2";
    private const string TstInfoOid = "1.2.840.113549.1.9.16.1.4";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string Sha384Oid = "2.16.840.1.101.3.4.2.2";
    private const string Sha512Oid = "2.16.840.1.101.3.4.2.3";
    private const string Sha1Oid = "1.3.14.3.2.26";
    private const string SigningCertificateOid = "1.2.840.113549.1.9.16.2.12";
    private const string SigningCertificateV2Oid = "1.2.840.113549.1.9.16.2.47";

    internal static void RequireMatch(byte[]? token, ReadOnlySpan<byte> messageImprint, ReadOnlySpan<byte> nonce)
    {
        if (token == null || token.Length == 0)
        {
            throw new PdfTimestampException("The timestamp authority returned an empty token.");
        }

        if (token.Length > MaxTokenBytes)
        {
            throw new PdfTimestampException("The timestamp token is larger than 8 MiB.");
        }

        DerCodec.DerTlv contentInfo = ReadOnlyTlv(token);
        RequireTag(contentInfo, 0x30);
        int pos = 0;
        if (!IsOid(Take(contentInfo.Content, ref pos), SignedDataOid))
        {
            throw NotToken();
        }

        DerCodec.DerTlv explicitContent = Take(contentInfo.Content, ref pos);
        RequireTag(explicitContent, 0xA0);
        RequireEnd(contentInfo.Content, pos);
        DerCodec.DerTlv signedData = ReadOnlyTlv(explicitContent.Content);
        RequireTag(signedData, 0x30);
        CheckSignedData(signedData, messageImprint, nonce);
    }

    private static void CheckSignedData(DerCodec.DerTlv signedData, ReadOnlySpan<byte> messageImprint, ReadOnlySpan<byte> nonce)
    {
        int pos = 0;
        ValidatePositiveInteger(Take(signedData.Content, ref pos));
        DerCodec.DerTlv digestAlgorithms = Take(signedData.Content, ref pos);
        RequireTag(digestAlgorithms, 0x31);
        ValidateAlgorithmSet(digestAlgorithms.Content);
        CheckEncapsulatedContent(Take(signedData.Content, ref pos), messageImprint, nonce);

        byte[]? certificates = null;
        DerCodec.DerTlv field = Take(signedData.Content, ref pos);
        if (field.Tag == 0xA0)
        {
            certificates = field.Content;
            ValidateCertificateSet(certificates);
            field = Take(signedData.Content, ref pos);
        }

        if (field.Tag == 0xA1)
        {
            ValidateValues(field.Content);
            field = Take(signedData.Content, ref pos);
        }

        RequireTag(field, 0x31);
        RequireEnd(signedData.Content, pos);
        RequireSigningCertificate(certificates, field);
    }

    private static void CheckEncapsulatedContent(DerCodec.DerTlv encapsulated, ReadOnlySpan<byte> messageImprint, ReadOnlySpan<byte> nonce)
    {
        RequireTag(encapsulated, 0x30);
        int pos = 0;
        if (!IsOid(Take(encapsulated.Content, ref pos), TstInfoOid))
        {
            throw NotToken();
        }

        DerCodec.DerTlv explicitOctet = Take(encapsulated.Content, ref pos);
        RequireTag(explicitOctet, 0xA0);
        RequireEnd(encapsulated.Content, pos);
        DerCodec.DerTlv octet = ReadOnlyTlv(explicitOctet.Content);
        RequireTag(octet, 0x04);
        DerCodec.DerTlv tstInfo = ReadOnlyTlv(octet.Content);
        RequireTag(tstInfo, 0x30);
        CheckTstInfo(tstInfo, messageImprint, nonce);
    }

    // This is an input check, not signature or trust validation. The token must carry the
    // certificate named by the single ESS signing-certificate attribute of its one signer.
    private static void RequireSigningCertificate(byte[]? certificates, DerCodec.DerTlv signerInfos)
    {
        int signerPos = 0;
        DerCodec.DerTlv signerInfo = Take(signerInfos.Content, ref signerPos);
        RequireTag(signerInfo, 0x30);
        RequireEnd(signerInfos.Content, signerPos);

        int pos = 0;
        ValidatePositiveInteger(Take(signerInfo.Content, ref pos));
        DerCodec.DerTlv signerIdentifier = Take(signerInfo.Content, ref pos);
        ValidateSignerIdentifier(signerIdentifier);

        ValidateAlgorithm(Take(signerInfo.Content, ref pos));
        DerCodec.DerTlv signedAttributes = Take(signerInfo.Content, ref pos);
        RequireTag(signedAttributes, 0xA0);
        CertificateIdentifier identifier = ReadSigningCertificateAttribute(signedAttributes.Content);
        ValidateAlgorithm(Take(signerInfo.Content, ref pos));
        DerCodec.DerTlv signature = Take(signerInfo.Content, ref pos);
        RequireTag(signature, 0x04);
        if (signature.Content.Length == 0)
        {
            throw NotToken();
        }

        if (pos < signerInfo.Content.Length)
        {
            DerCodec.DerTlv unsignedAttributes = Take(signerInfo.Content, ref pos);
            RequireTag(unsignedAttributes, 0xA1);
            ValidateAttributes(unsignedAttributes.Content);
        }

        RequireEnd(signerInfo.Content, pos);
        if (!IncludesCertificate(certificates, identifier))
        {
            throw new PdfTimestampException("The timestamp token does not include the signing certificate that its ESS signing-certificate attribute identifies.");
        }
    }

    private static CertificateIdentifier ReadSigningCertificateAttribute(byte[] attributes)
    {
        CertificateIdentifier? identifier = null;
        int pos = 0;
        while (pos < attributes.Length)
        {
            DerCodec.DerTlv attribute = Take(attributes, ref pos);
            RequireTag(attribute, 0x30);
            int attributePos = 0;
            DerCodec.DerTlv type = Take(attribute.Content, ref attributePos);
            RequireTag(type, 0x06);
            DerCodec.DerTlv values = Take(attribute.Content, ref attributePos);
            RequireTag(values, 0x31);
            RequireEnd(attribute.Content, attributePos);

            bool version2 = IsOid(type, SigningCertificateV2Oid);
            if (version2 || IsOid(type, SigningCertificateOid))
            {
                if (identifier.HasValue)
                {
                    throw new PdfTimestampException("The timestamp token has multiple ESS signing-certificate attributes.");
                }

                int valuePos = 0;
                DerCodec.DerTlv signingCertificate = Take(values.Content, ref valuePos);
                RequireEnd(values.Content, valuePos);
                identifier = ReadSigningCertificate(signingCertificate, version2);
            }
            else
            {
                ValidateValues(values.Content);
            }
        }

        if (!identifier.HasValue)
        {
            throw new PdfTimestampException("The timestamp token has no ESS signing-certificate attribute that identifies its signing certificate.");
        }

        return identifier.Value;
    }

    private static CertificateIdentifier ReadSigningCertificate(DerCodec.DerTlv signingCertificate, bool version2)
    {
        RequireTag(signingCertificate, 0x30);
        int pos = 0;
        DerCodec.DerTlv certIds = Take(signingCertificate.Content, ref pos);
        RequireTag(certIds, 0x30);
        CertificateIdentifier? first = null;
        int certIdPos = 0;
        while (certIdPos < certIds.Content.Length)
        {
            CertificateIdentifier current = ReadCertificateIdentifier(Take(certIds.Content, ref certIdPos), version2);
            if (!first.HasValue)
            {
                first = current;
            }
        }

        if (!first.HasValue)
        {
            throw NotToken();
        }

        if (pos < signingCertificate.Content.Length)
        {
            DerCodec.DerTlv policies = Take(signingCertificate.Content, ref pos);
            RequireTag(policies, 0x30);
            ValidateValues(policies.Content);
        }

        RequireEnd(signingCertificate.Content, pos);
        return first.Value;
    }

    // ESSCertID is { certHash (SHA-1), issuerSerial OPTIONAL }.
    // ESSCertIDv2 is { hashAlgorithm DEFAULT SHA-256, certHash, issuerSerial OPTIONAL }.
    private static CertificateIdentifier ReadCertificateIdentifier(DerCodec.DerTlv certId, bool version2)
    {
        RequireTag(certId, 0x30);
        int pos = 0;
        DerCodec.DerTlv field = Take(certId.Content, ref pos);
        string hashOid = version2 ? Sha256Oid : Sha1Oid;
        if (version2 && field.Tag == 0x30)
        {
            hashOid = ReadSupportedHashAlgorithm(field);
            field = Take(certId.Content, ref pos);
        }

        RequireTag(field, 0x04);
        if (field.Content.Length != HashLength(hashOid))
        {
            throw NotToken();
        }

        DerCodec.DerTlv? issuerSerial = null;
        if (pos < certId.Content.Length)
        {
            issuerSerial = Take(certId.Content, ref pos);
            ValidateIssuerSerial(issuerSerial.Value);
        }

        RequireEnd(certId.Content, pos);
        return new CertificateIdentifier(hashOid, field.Content, issuerSerial);
    }

    private static bool IncludesCertificate(byte[]? certificates, CertificateIdentifier identifier)
    {
        if (certificates == null)
        {
            return false;
        }

        using HashAlgorithm hash = CreateHash(identifier.HashOid);
        int pos = 0;
        while (pos < certificates.Length)
        {
            DerCodec.DerTlv certificate = Take(certificates, ref pos);
            if (certificate.Tag == 0x30)
            {
                (byte[] issuer, byte[] serial) = ReadCertificateIdentity(certificate);
                if (hash.ComputeHash(certificate.Full).AsSpan().SequenceEqual(identifier.Hash)
                    && (!identifier.IssuerSerial.HasValue || MatchesIssuerSerial(issuer, serial, identifier.IssuerSerial.Value)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (byte[] Issuer, byte[] Serial) ReadCertificateIdentity(DerCodec.DerTlv certificate)
    {
        RequireTag(certificate, 0x30);
        int outer = 0;
        DerCodec.DerTlv tbs = Take(certificate.Content, ref outer);
        ValidateAlgorithm(Take(certificate.Content, ref outer));
        RequireTag(Take(certificate.Content, ref outer), 0x03);
        RequireEnd(certificate.Content, outer);
        RequireTag(tbs, 0x30);

        int pos = 0;
        if (tbs.Content.Length > 0 && tbs.Content[0] == 0xA0)
        {
            RequireTag(ReadOnlyTlv(Take(tbs.Content, ref pos).Content), 0x02);
        }

        DerCodec.DerTlv serial = Take(tbs.Content, ref pos);
        ValidatePositiveInteger(serial);
        ValidateAlgorithm(Take(tbs.Content, ref pos));
        DerCodec.DerTlv issuer = Take(tbs.Content, ref pos);
        RequireTag(issuer, 0x30);
        return (issuer.Full, serial.Content);
    }

    private static void ValidateCertificateSet(byte[] certificates)
    {
        int pos = 0;
        while (pos < certificates.Length)
        {
            DerCodec.DerTlv certificate = Take(certificates, ref pos);
            if (certificate.Tag == 0x30)
            {
                ReadCertificateIdentity(certificate);
            }
            else if (certificate.Tag < 0xA0 || certificate.Tag > 0xA3)
            {
                throw NotToken();
            }
        }
    }

    private static void ValidateSignerIdentifier(DerCodec.DerTlv signerIdentifier)
    {
        if (signerIdentifier.Tag == 0x80)
        {
            if (signerIdentifier.Content.Length == 0)
            {
                throw NotToken();
            }

            return;
        }

        RequireTag(signerIdentifier, 0x30);
        int pos = 0;
        RequireTag(Take(signerIdentifier.Content, ref pos), 0x30);
        ValidatePositiveInteger(Take(signerIdentifier.Content, ref pos));
        RequireEnd(signerIdentifier.Content, pos);
    }

    private static bool MatchesIssuerSerial(byte[] certificateIssuer, byte[] certificateSerial, DerCodec.DerTlv issuerSerial)
    {
        int pos = 0;
        DerCodec.DerTlv names = Take(issuerSerial.Content, ref pos);
        DerCodec.DerTlv serial = Take(issuerSerial.Content, ref pos);
        RequireEnd(issuerSerial.Content, pos);
        if (!SameUnsigned(serial.Content, certificateSerial))
        {
            return false;
        }

        int namePos = 0;
        while (namePos < names.Content.Length)
        {
            DerCodec.DerTlv name = Take(names.Content, ref namePos);
            if ((name.Tag & 0xC0) != 0x80)
            {
                throw NotToken();
            }

            if (name.Tag == 0xA4)
            {
                DerCodec.DerTlv directoryName = ReadOnlyTlv(name.Content);
                RequireTag(directoryName, 0x30);
                if (directoryName.Full.AsSpan().SequenceEqual(certificateIssuer))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ValidateIssuerSerial(DerCodec.DerTlv issuerSerial)
    {
        RequireTag(issuerSerial, 0x30);
        int pos = 0;
        DerCodec.DerTlv names = Take(issuerSerial.Content, ref pos);
        RequireTag(names, 0x30);
        if (names.Content.Length == 0)
        {
            throw NotToken();
        }

        ValidatePositiveInteger(Take(issuerSerial.Content, ref pos));
        RequireEnd(issuerSerial.Content, pos);
        int namePos = 0;
        while (namePos < names.Content.Length)
        {
            DerCodec.DerTlv name = Take(names.Content, ref namePos);
            if ((name.Tag & 0xC0) != 0x80)
            {
                throw NotToken();
            }

            if (name.Tag == 0xA4)
            {
                RequireTag(ReadOnlyTlv(name.Content), 0x30);
            }
        }
    }

    private static void CheckTstInfo(DerCodec.DerTlv tstInfo, ReadOnlySpan<byte> messageImprint, ReadOnlySpan<byte> nonce)
    {
        int pos = 0;
        ValidatePositiveInteger(Take(tstInfo.Content, ref pos));
        RequireTag(Take(tstInfo.Content, ref pos), 0x06);
        CheckImprint(Take(tstInfo.Content, ref pos), messageImprint);
        ValidatePositiveInteger(Take(tstInfo.Content, ref pos));
        RequireTag(Take(tstInfo.Content, ref pos), 0x18);
        if (pos < tstInfo.Content.Length && tstInfo.Content[pos] == 0x30)
        {
            ValidateValues(Take(tstInfo.Content, ref pos).Content);
        }

        if (pos < tstInfo.Content.Length && tstInfo.Content[pos] == 0x01)
        {
            DerCodec.DerTlv ordering = Take(tstInfo.Content, ref pos);
            if (ordering.Content.Length != 1)
            {
                throw NotToken();
            }
        }

        bool foundNonce = false;
        if (pos < tstInfo.Content.Length && tstInfo.Content[pos] == 0x02)
        {
            DerCodec.DerTlv actualNonce = Take(tstInfo.Content, ref pos);
            ValidatePositiveInteger(actualNonce);
            foundNonce = SameUnsigned(actualNonce.Content, nonce);
        }

        if (pos < tstInfo.Content.Length && tstInfo.Content[pos] == 0xA0)
        {
            ValidateValues(Take(tstInfo.Content, ref pos).Content);
        }

        if (pos < tstInfo.Content.Length && tstInfo.Content[pos] == 0xA1)
        {
            ValidateValues(Take(tstInfo.Content, ref pos).Content);
        }

        RequireEnd(tstInfo.Content, pos);
        if (!foundNonce)
        {
            throw new PdfTimestampException("The timestamp token's nonce does not match the request.");
        }
    }

    private static void CheckImprint(DerCodec.DerTlv messageImprint, ReadOnlySpan<byte> expected)
    {
        RequireTag(messageImprint, 0x30);
        int pos = 0;
        if (ReadHashAlgorithm(Take(messageImprint.Content, ref pos)) != Sha256Oid)
        {
            throw new PdfTimestampException("The timestamp token's digest algorithm does not match the request.");
        }

        DerCodec.DerTlv hashed = Take(messageImprint.Content, ref pos);
        RequireEnd(messageImprint.Content, pos);
        if (hashed.Tag != 0x04 || !hashed.Content.AsSpan().SequenceEqual(expected))
        {
            throw new PdfTimestampException("The timestamp token's message imprint does not match the request.");
        }
    }

    private static string ReadSupportedHashAlgorithm(DerCodec.DerTlv algorithm)
    {
        string oid = ReadHashAlgorithm(algorithm);
        if (oid != Sha1Oid && oid != Sha256Oid && oid != Sha384Oid && oid != Sha512Oid)
        {
            throw new PdfTimestampException("The timestamp token identifies its signing certificate with an unsupported hash algorithm.");
        }

        return oid;
    }

    private static string ReadHashAlgorithm(DerCodec.DerTlv algorithm)
    {
        RequireTag(algorithm, 0x30);
        int pos = 0;
        DerCodec.DerTlv oid = Take(algorithm.Content, ref pos);
        string value = IsOid(oid, Sha1Oid) ? Sha1Oid
            : IsOid(oid, Sha256Oid) ? Sha256Oid
            : IsOid(oid, Sha384Oid) ? Sha384Oid
            : IsOid(oid, Sha512Oid) ? Sha512Oid
            : string.Empty;
        if (pos < algorithm.Content.Length)
        {
            DerCodec.DerTlv parameters = Take(algorithm.Content, ref pos);
            if (parameters.Tag != 0x05 || parameters.Content.Length != 0)
            {
                throw NotToken();
            }
        }

        RequireEnd(algorithm.Content, pos);
        return value;
    }

    private static void ValidateAlgorithmSet(byte[] algorithms)
    {
        if (algorithms.Length == 0)
        {
            throw NotToken();
        }

        int pos = 0;
        while (pos < algorithms.Length)
        {
            ValidateAlgorithm(Take(algorithms, ref pos));
        }
    }

    private static void ValidateAlgorithm(DerCodec.DerTlv algorithm)
    {
        RequireTag(algorithm, 0x30);
        int pos = 0;
        RequireTag(Take(algorithm.Content, ref pos), 0x06);
        if (pos < algorithm.Content.Length)
        {
            Take(algorithm.Content, ref pos);
        }

        RequireEnd(algorithm.Content, pos);
    }

    private static void ValidateAttributes(byte[] attributes)
    {
        int pos = 0;
        while (pos < attributes.Length)
        {
            DerCodec.DerTlv attribute = Take(attributes, ref pos);
            RequireTag(attribute, 0x30);
            int inner = 0;
            RequireTag(Take(attribute.Content, ref inner), 0x06);
            DerCodec.DerTlv values = Take(attribute.Content, ref inner);
            RequireTag(values, 0x31);
            RequireEnd(attribute.Content, inner);
            ValidateValues(values.Content);
        }
    }

    private static void ValidateValues(byte[] values)
    {
        int pos = 0;
        while (pos < values.Length)
        {
            Take(values, ref pos);
        }
    }

    private static void ValidatePositiveInteger(DerCodec.DerTlv integer)
    {
        RequireTag(integer, 0x02);
        if (integer.Content.Length == 0
            || (integer.Content[0] & 0x80) != 0
            || (integer.Content.Length > 1 && integer.Content[0] == 0 && (integer.Content[1] & 0x80) == 0))
        {
            throw NotToken();
        }
    }

    private static int HashLength(string oid)
    {
        switch (oid)
        {
            case Sha1Oid:
                return 20;
            case Sha384Oid:
                return 48;
            case Sha512Oid:
                return 64;
            default:
                return 32;
        }
    }

    private static HashAlgorithm CreateHash(string oid)
    {
        switch (oid)
        {
            case Sha1Oid:
                return SHA1.Create();
            case Sha384Oid:
                return SHA384.Create();
            case Sha512Oid:
                return SHA512.Create();
            default:
                return SHA256.Create();
        }
    }

    private static bool SameUnsigned(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        return Trim(left).SequenceEqual(Trim(right));
    }

    private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
    {
        int start = 0;
        while (start < value.Length - 1 && value[start] == 0)
        {
            start++;
        }

        return start == 0 ? value : value.Slice(start);
    }

    private static DerCodec.DerTlv ReadOnlyTlv(ReadOnlySpan<byte> data)
    {
        DerCodec.DerTlv value = DerCodec.ReadTlv(data);
        ValidateDerEncoding(value.Full);
        if (value.Full.Length != data.Length)
        {
            throw NotToken();
        }

        return value;
    }

    private static DerCodec.DerTlv Take(ReadOnlySpan<byte> data, ref int offset)
    {
        DerCodec.DerTlv tlv = DerCodec.ReadTlv(data, offset);
        ValidateDerEncoding(tlv.Full);
        offset += tlv.Full.Length;
        return tlv;
    }

    private static void ValidateDerEncoding(byte[] full)
    {
        byte first = full[1];
        if ((first & 0x80) == 0)
        {
            return;
        }

        int count = first & 0x7F;
        if (full[2] == 0 || (count == 1 && full[2] < 0x80))
        {
            throw NotToken();
        }
    }

    private static void RequireEnd(ReadOnlySpan<byte> data, int offset)
    {
        if (offset != data.Length)
        {
            throw NotToken();
        }
    }

    private static void RequireTag(DerCodec.DerTlv value, byte tag)
    {
        if (value.Tag != tag)
        {
            throw NotToken();
        }
    }

    private static bool IsOid(DerCodec.DerTlv tlv, string dotted)
    {
        return tlv.Full.AsSpan().SequenceEqual(DerCodec.Oid(dotted));
    }

    private static PdfTimestampException NotToken()
    {
        return new PdfTimestampException("The timestamp token is not a time-stamp token.");
    }

    private readonly struct CertificateIdentifier
    {
        public CertificateIdentifier(string hashOid, byte[] hash, DerCodec.DerTlv? issuerSerial)
        {
            HashOid = hashOid;
            Hash = hash;
            IssuerSerial = issuerSerial;
        }

        public string HashOid { get; }

        public byte[] Hash { get; }

        public DerCodec.DerTlv? IssuerSerial { get; }
    }
}
