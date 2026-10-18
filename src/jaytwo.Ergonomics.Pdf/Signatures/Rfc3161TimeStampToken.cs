using System;

namespace jaytwo.Ergonomics.Pdf.Signatures;

internal static class Rfc3161TimeStampToken
{
    private const string SignedDataOid = "1.2.840.113549.1.7.2";

    private const string TstInfoOid = "1.2.840.113549.1.9.16.1.4";

    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    internal static void RequireMatch(byte[]? token, ReadOnlySpan<byte> messageImprint, ReadOnlySpan<byte> nonce)
    {
        if (token == null || token.Length == 0)
        {
            throw new PdfTimestampException("The timestamp authority returned an empty token.");
        }

        DerCodec.DerTlv contentInfo = DerCodec.ReadTlv(token);
        if (contentInfo.Tag != 0x30)
        {
            throw NotToken();
        }

        int pos = 0;
        DerCodec.DerTlv contentType = Take(contentInfo.Content, ref pos);
        if (!IsOid(contentType, SignedDataOid))
        {
            throw NotToken();
        }

        DerCodec.DerTlv explicitContent = Take(contentInfo.Content, ref pos);
        if (explicitContent.Tag != 0xA0)
        {
            throw NotToken();
        }

        DerCodec.DerTlv signedData = DerCodec.ReadTlv(explicitContent.Content);
        if (signedData.Tag != 0x30)
        {
            throw NotToken();
        }

        pos = 0;
        Take(signedData.Content, ref pos);
        Take(signedData.Content, ref pos);
        DerCodec.DerTlv encapsulated = Take(signedData.Content, ref pos);
        if (encapsulated.Tag != 0x30)
        {
            throw NotToken();
        }

        int inner = 0;
        DerCodec.DerTlv encapsulatedType = Take(encapsulated.Content, ref inner);
        if (!IsOid(encapsulatedType, TstInfoOid) || inner >= encapsulated.Content.Length)
        {
            throw NotToken();
        }

        DerCodec.DerTlv explicitOctet = Take(encapsulated.Content, ref inner);
        if (explicitOctet.Tag != 0xA0)
        {
            throw NotToken();
        }

        DerCodec.DerTlv octet = DerCodec.ReadTlv(explicitOctet.Content);
        if (octet.Tag != 0x04)
        {
            throw NotToken();
        }

        DerCodec.DerTlv tstInfo = DerCodec.ReadTlv(octet.Content);
        if (tstInfo.Tag != 0x30)
        {
            throw NotToken();
        }

        CheckTstInfo(tstInfo, messageImprint, nonce);
    }

    private static void CheckTstInfo(DerCodec.DerTlv tstInfo, ReadOnlySpan<byte> messageImprint, ReadOnlySpan<byte> nonce)
    {
        int pos = 0;
        Take(tstInfo.Content, ref pos);
        Take(tstInfo.Content, ref pos);
        CheckImprint(Take(tstInfo.Content, ref pos), messageImprint);
        Take(tstInfo.Content, ref pos);
        DerCodec.DerTlv generated = Take(tstInfo.Content, ref pos);
        if (generated.Tag != 0x18)
        {
            throw NotToken();
        }

        while (pos < tstInfo.Content.Length)
        {
            DerCodec.DerTlv field = Take(tstInfo.Content, ref pos);
            if (field.Tag == 0x30 || field.Tag == 0x01)
            {
                continue;
            }

            if (field.Tag == 0x02)
            {
                if (!SameUnsigned(field.Content, nonce))
                {
                    throw new PdfTimestampException("The timestamp token's nonce does not match the request.");
                }

                return;
            }

            break;
        }

        throw new PdfTimestampException("The timestamp token's nonce does not match the request.");
    }

    private static void CheckImprint(DerCodec.DerTlv messageImprint, ReadOnlySpan<byte> expected)
    {
        if (messageImprint.Tag != 0x30)
        {
            throw NotToken();
        }

        int pos = 0;
        DerCodec.DerTlv algorithm = Take(messageImprint.Content, ref pos);
        if (algorithm.Tag != 0x30)
        {
            throw NotToken();
        }

        int algorithmPos = 0;
        DerCodec.DerTlv oid = Take(algorithm.Content, ref algorithmPos);
        if (!IsOid(oid, Sha256Oid))
        {
            throw new PdfTimestampException("The timestamp token's digest algorithm does not match the request.");
        }

        DerCodec.DerTlv hashed = Take(messageImprint.Content, ref pos);
        if (hashed.Tag != 0x04 || !hashed.Content.AsSpan().SequenceEqual(expected))
        {
            throw new PdfTimestampException("The timestamp token's message imprint does not match the request.");
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

    private static DerCodec.DerTlv Take(ReadOnlySpan<byte> data, ref int offset)
    {
        DerCodec.DerTlv tlv = DerCodec.ReadTlv(data, offset);
        offset += tlv.Full.Length;
        return tlv;
    }

    private static bool IsOid(DerCodec.DerTlv tlv, string dotted)
    {
        return tlv.Full.AsSpan().SequenceEqual(DerCodec.Oid(dotted));
    }

    private static PdfTimestampException NotToken()
    {
        return new PdfTimestampException("The timestamp token is not a time-stamp token.");
    }
}
