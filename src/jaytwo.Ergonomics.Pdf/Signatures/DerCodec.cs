using System;
using System.Collections.Generic;

namespace jaytwo.Ergonomics.Pdf.Signatures;

internal static class DerCodec
{
    public static byte[] Sequence(params byte[][] parts)
    {
        return Tlv(0x30, Concat(parts));
    }

    public static byte[] Set(params byte[][] parts)
    {
        var ordered = (byte[][])parts.Clone();
        Array.Sort(ordered, CompareBytes);
        return Tlv(0x31, Concat(ordered));
    }

    public static byte[] ExplicitContext(int number, byte[] innerTlv)
    {
        return Tlv((byte)(0xA0 | number), innerTlv);
    }

    public static byte[] ReplaceTag(byte[] tlv, byte tag)
    {
        var copy = new byte[tlv.Length];
        Buffer.BlockCopy(tlv, 0, copy, 0, tlv.Length);
        copy[0] = tag;
        return copy;
    }

    public static byte[] Oid(string dotted)
    {
        return Tlv(0x06, EncodeOid(dotted));
    }

    public static byte[] Null()
    {
        return new byte[] { 0x05, 0x00 };
    }

    public static byte[] Integer(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (value == 0)
        {
            return new byte[] { 0x02, 0x01, 0x00 };
        }

        var bytes = new List<byte>();
        while (value > 0)
        {
            bytes.Add((byte)(value & 0xFF));
            value >>= 8;
        }

        bytes.Reverse();
        if ((bytes[0] & 0x80) != 0)
        {
            bytes.Insert(0, 0);
        }

        return Tlv(0x02, bytes.ToArray());
    }

    public static byte[] OctetString(byte[] data)
    {
        return Tlv(0x04, data);
    }

    public static byte[] UtcTime(DateTimeOffset time)
    {
        string text = time.ToUniversalTime().ToString("yyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + "Z";
        return Tlv(0x17, System.Text.Encoding.ASCII.GetBytes(text));
    }

    public static byte[] AlgorithmSha256()
    {
        return Sequence(Oid("2.16.840.1.101.3.4.2.1"), Null());
    }

    public static byte[] AlgorithmSha256WithRsa()
    {
        return Sequence(Oid("1.2.840.113549.1.1.11"), Null());
    }

    public static DerTlv ReadTlv(ReadOnlySpan<byte> data)
    {
        return ReadTlv(data, 0);
    }

    public static DerTlv ReadTlv(ReadOnlySpan<byte> data, int offset)
    {
        if (offset >= data.Length)
        {
            throw new PdfTimestampException("DER value is truncated.");
        }

        byte tag = data[offset];
        int index = offset + 1;
        if (index >= data.Length)
        {
            throw new PdfTimestampException("DER length is truncated.");
        }

        int length;
        byte first = data[index++];
        if ((first & 0x80) == 0)
        {
            length = first;
        }
        else
        {
            int count = first & 0x7F;
            if (count == 0 || count > 4 || index + count > data.Length)
            {
                throw new PdfTimestampException("DER length is invalid.");
            }

            length = 0;
            for (int i = 0; i < count; i++)
            {
                length = (length << 8) | data[index++];
            }
        }

        if (length < 0 || index + length > data.Length)
        {
            throw new PdfTimestampException("DER value exceeds its buffer.");
        }

        return new DerTlv(tag, data.Slice(offset, (index - offset) + length).ToArray(), data.Slice(index, length).ToArray());
    }

    public static int ReadInteger(DerTlv tlv)
    {
        if (tlv.Tag != 0x02 || tlv.Content.Length == 0 || tlv.Content.Length > 4)
        {
            throw new PdfTimestampException("DER integer was expected.");
        }

        int value = 0;
        foreach (byte b in tlv.Content)
        {
            value = (value << 8) | b;
        }

        return value;
    }

    public static (byte[] Issuer, byte[] Serial) ReadCertificateIdentity(byte[] certificate)
    {
        DerTlv cert = ReadTlv(certificate);
        DerTlv tbs = ReadTlv(cert.Content);
        int pos = 0;
        if (tbs.Content.Length > 0 && tbs.Content[0] == 0xA0)
        {
            pos += ReadTlv(tbs.Content, 0).Full.Length;
        }

        DerTlv serial = ReadTlv(tbs.Content, pos);
        pos += serial.Full.Length;
        DerTlv signature = ReadTlv(tbs.Content, pos);
        pos += signature.Full.Length;
        DerTlv issuer = ReadTlv(tbs.Content, pos);
        return (issuer.Full, serial.Full);
    }

    private static byte[] Tlv(byte tag, byte[] content)
    {
        byte[] length = EncodeLength(content.Length);
        var result = new byte[1 + length.Length + content.Length];
        result[0] = tag;
        Buffer.BlockCopy(length, 0, result, 1, length.Length);
        Buffer.BlockCopy(content, 0, result, 1 + length.Length, content.Length);
        return result;
    }

    private static byte[] EncodeLength(int length)
    {
        if (length < 0x80)
        {
            return new[] { (byte)length };
        }

        var bytes = new List<byte>();
        int value = length;
        while (value > 0)
        {
            bytes.Add((byte)(value & 0xFF));
            value >>= 8;
        }

        bytes.Reverse();
        var result = new byte[bytes.Count + 1];
        result[0] = (byte)(0x80 | bytes.Count);
        for (int i = 0; i < bytes.Count; i++)
        {
            result[i + 1] = bytes[i];
        }

        return result;
    }

    private static byte[] EncodeOid(string dotted)
    {
        string[] parts = dotted.Split('.');
        var body = new List<byte>
        {
            (byte)((int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) * 40) + int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)),
        };
        for (int i = 2; i < parts.Length; i++)
        {
            body.AddRange(Base128(int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return body.ToArray();
    }

    private static byte[] Base128(int value)
    {
        var stack = new Stack<byte>();
        stack.Push((byte)(value & 0x7F));
        value >>= 7;
        while (value > 0)
        {
            stack.Push((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        return stack.ToArray();
    }

    private static byte[] Concat(byte[][] parts)
    {
        int length = 0;
        foreach (byte[] part in parts)
        {
            length += part.Length;
        }

        var result = new byte[length];
        int offset = 0;
        foreach (byte[] part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }

    private static int CompareBytes(byte[] left, byte[] right)
    {
        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
        {
            int compare = left[i].CompareTo(right[i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    internal readonly struct DerTlv
    {
        public DerTlv(byte tag, byte[] full, byte[] content)
        {
            Tag = tag;
            Full = full;
            Content = content;
        }

        public byte Tag { get; }

        public byte[] Full { get; }

        public byte[] Content { get; }
    }
}
