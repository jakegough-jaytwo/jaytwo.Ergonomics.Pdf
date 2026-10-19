using System;
using System.Security.Cryptography;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Builds a minimal RFC 3161 TimeStampReq for a SHA-256 message imprint.
/// </summary>
public static class Rfc3161TimeStampRequest
{
    /// <summary>
    /// Creates a version-1 request with no nonce and certReq set to TRUE.
    /// </summary>
    /// <param name="messageImprint">The 32-byte SHA-256 digest of the signed bytes.</param>
    /// <returns>The DER-encoded TimeStampReq.</returns>
    public static byte[] Create(ReadOnlySpan<byte> messageImprint)
    {
        if (messageImprint.Length != 32)
        {
            throw new ArgumentException("The message imprint must be a 32-byte SHA-256 digest.", nameof(messageImprint));
        }

        return DerCodec.Sequence(
            DerCodec.Integer(1),
            DerCodec.Sequence(DerCodec.AlgorithmSha256(), DerCodec.OctetString(messageImprint.ToArray())),
            CertReqTrue());
    }

    /// <summary>
    /// Creates a version-1 request with a nonce and certReq set to TRUE.
    /// </summary>
    /// <param name="messageImprint">The 32-byte SHA-256 digest of the signed bytes.</param>
    /// <param name="nonce">The unsigned nonce, as big-endian bytes. Leading zeros are not significant. Empty is rejected.</param>
    /// <returns>The DER-encoded TimeStampReq.</returns>
    public static byte[] Create(ReadOnlySpan<byte> messageImprint, ReadOnlySpan<byte> nonce)
    {
        if (messageImprint.Length != 32)
        {
            throw new ArgumentException("The message imprint must be a 32-byte SHA-256 digest.", nameof(messageImprint));
        }

        if (nonce.Length == 0)
        {
            throw new ArgumentException("The nonce must not be empty.", nameof(nonce));
        }

        return DerCodec.Sequence(
            DerCodec.Integer(1),
            DerCodec.Sequence(DerCodec.AlgorithmSha256(), DerCodec.OctetString(messageImprint.ToArray())),
            DerCodec.Integer(nonce),
            CertReqTrue());
    }

    internal static byte[] NewNonce()
    {
        var nonce = new byte[8];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(nonce);
        }

        return nonce;
    }

    // certReq is BOOLEAN DEFAULT FALSE, and DER omits a default, so it is written only as TRUE.
    // RFC 3161, as updated by RFC 5816, then requires the TSA to include the certificate
    // that its ESS signing-certificate attribute identifies.
    private static byte[] CertReqTrue()
    {
        return new byte[] { 0x01, 0x01, 0xFF };
    }
}
