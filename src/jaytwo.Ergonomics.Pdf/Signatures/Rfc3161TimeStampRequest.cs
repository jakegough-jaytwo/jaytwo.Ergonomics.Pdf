using System;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Builds a minimal RFC 3161 TimeStampReq for a SHA-256 message imprint.
/// </summary>
public static class Rfc3161TimeStampRequest
{
    /// <summary>
    /// Creates a version-1 request with no nonce and certReq omitted.
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
            DerCodec.Sequence(DerCodec.AlgorithmSha256(), DerCodec.OctetString(messageImprint.ToArray())));
    }
}
