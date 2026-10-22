using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace jaytwo.Ergonomics.Pdf.Signatures;

// Reads the parts of a signature /Contents CMS SignedData that inspection reports (spec R9). It verifies nothing.
// ISO 32000-1 12.8.3.3.1 requires DER. BER indefinite lengths are read as a tolerated writer quirk (spec section 8),
// because Acrobat writes them on the outer containers (the GPO bill in the corpus). Any other departure throws.
internal static class CmsSignatureReader
{
    private const int MaxDepth = 64;
    private const string SignedDataOid = "1.2.840.113549.1.7.2";
    private const string TstInfoOid = "1.2.840.113549.1.9.16.1.4";
    private const string SigningTimeOid = "1.2.840.113549.1.9.5";
    private const string TimeStampTokenOid = "1.2.840.113549.1.9.16.2.14";
    private const string SubjectKeyIdentifierOid = "2.5.29.14";

    // A document timestamp's /Contents is itself the RFC 3161 token, so its time is the token's genTime.
    public static Result Read(byte[] contents, bool documentTimestamp, long offset)
    {
        var reader = new Reader(contents, offset);
        Node info = reader.Read(0, contents.Length, 0);
        for (int i = info.End; i < contents.Length; i++)
        {
            if (contents[i] != 0)
            {
                throw reader.Fail("Bytes other than zero padding follow the CMS signature.");
            }
        }

        SignedData signedData = reader.ReadSignedData(info);
        List<Node> signer = reader.Children(signedData.SignerInfo);
        int index = 0;
        reader.Expect(Take(signer, ref index, reader), 0x02);
        Node identifier = Take(signer, ref index, reader);
        string digestAlgorithm = reader.AlgorithmOid(Take(signer, ref index, reader));
        Node next = Take(signer, ref index, reader);
        DateTimeOffset? signingTime = null;
        if (next.Tag == 0xA0)
        {
            Node? value = reader.SingleAttribute(next, SigningTimeOid);
            signingTime = value == null ? null : reader.Time(value.Value);
            next = Take(signer, ref index, reader);
        }

        string signatureAlgorithm = reader.AlgorithmOid(next);
        reader.Expect(Take(signer, ref index, reader), 0x04);
        DateTimeOffset? timestampTime = documentTimestamp ? reader.GenTime(signedData) : null;
        if (index < signer.Count && signer[index].Tag == 0xA1)
        {
            if (!documentTimestamp)
            {
                Node? token = reader.FirstAttributeValue(signer[index], TimeStampTokenOid);
                timestampTime = token == null ? null : reader.GenTime(reader.ReadSignedData(token.Value));
            }

            index++;
        }

        if (index != signer.Count)
        {
            throw reader.Fail("The SignerInfo has unexpected fields.");
        }

        byte[]? signerCertificate = null;
        foreach (byte[] certificate in signedData.Certificates)
        {
            if (reader.Identifies(identifier, certificate))
            {
                signerCertificate = certificate;
                break;
            }
        }

        return new Result(signerCertificate, signedData.Certificates, digestAlgorithm, signatureAlgorithm, signingTime, timestampTime);
    }

    private static Node Take(List<Node> nodes, ref int index, Reader reader)
    {
        if (index >= nodes.Count)
        {
            throw reader.Fail("A CMS structure is missing a required field.");
        }

        return nodes[index++];
    }

    private readonly struct Node
    {
        public Node(byte tag, int start, int contentStart, int contentEnd, int end)
        {
            Tag = tag;
            Start = start;
            ContentStart = contentStart;
            ContentEnd = contentEnd;
            End = end;
        }

        public byte Tag { get; }

        public int Start { get; }

        public int ContentStart { get; }

        public int ContentEnd { get; }

        public int End { get; }
    }

    private readonly struct SignedData
    {
        public SignedData(Node encapsulated, List<byte[]> certificates, Node signerInfo)
        {
            Encapsulated = encapsulated;
            Certificates = certificates;
            SignerInfo = signerInfo;
        }

        public Node Encapsulated { get; }

        public List<byte[]> Certificates { get; }

        public Node SignerInfo { get; }
    }

    internal sealed class Result
    {
        public Result(byte[]? signerCertificate, IReadOnlyList<byte[]> certificates, string digestAlgorithm, string signatureAlgorithm, DateTimeOffset? signingTime, DateTimeOffset? timestampTime)
        {
            SignerCertificate = signerCertificate;
            Certificates = certificates;
            DigestAlgorithm = digestAlgorithm;
            SignatureAlgorithm = signatureAlgorithm;
            SigningTime = signingTime;
            TimestampTime = timestampTime;
        }

        public byte[]? SignerCertificate { get; }

        public IReadOnlyList<byte[]> Certificates { get; }

        public string DigestAlgorithm { get; }

        public string SignatureAlgorithm { get; }

        public DateTimeOffset? SigningTime { get; }

        public DateTimeOffset? TimestampTime { get; }
    }

    private sealed class Reader
    {
        private readonly byte[] _data;
        private readonly long _offset;

        public Reader(byte[] data, long offset)
        {
            _data = data;
            _offset = offset;
        }

        public PdfParseException Fail(string message)
        {
            return new PdfParseException(_offset, message);
        }

        // One TLV in [position, limit). An indefinite length is allowed only on a constructed value and ends at the
        // first end-of-contents pair between its children.
        public Node Read(int position, int limit, int depth)
        {
            if (depth > MaxDepth)
            {
                throw Fail("The CMS signature nests too deeply.");
            }

            if (limit - position < 2)
            {
                throw Fail("The CMS signature is truncated.");
            }

            byte tag = _data[position];
            if ((tag & 0x1F) == 0x1F)
            {
                throw Fail("The CMS signature uses a high tag number.");
            }

            int index = position + 1;
            byte first = _data[index++];
            if (first == 0x80)
            {
                if ((tag & 0x20) == 0)
                {
                    throw Fail("A primitive CMS value has an indefinite length.");
                }

                int child = index;
                while (true)
                {
                    if (limit - child >= 2 && _data[child] == 0 && _data[child + 1] == 0)
                    {
                        return new Node(tag, position, index, child, child + 2);
                    }

                    child = Read(child, limit, depth + 1).End;
                }
            }

            int length;
            if ((first & 0x80) == 0)
            {
                length = first;
            }
            else
            {
                int count = first & 0x7F;
                if (count > 4 || count > limit - index)
                {
                    throw Fail("A CMS length is invalid.");
                }

                long value = 0;
                for (int i = 0; i < count; i++)
                {
                    value = (value << 8) | _data[index++];
                }

                if (value > limit - index)
                {
                    throw Fail("A CMS value exceeds its container.");
                }

                length = (int)value;
            }

            if (length > limit - index)
            {
                throw Fail("A CMS value exceeds its container.");
            }

            return new Node(tag, position, index, index + length, index + length);
        }

        public List<Node> Children(Node node)
        {
            if ((node.Tag & 0x20) == 0)
            {
                throw Fail("A constructed CMS value was expected.");
            }

            var children = new List<Node>();
            int position = node.ContentStart;
            while (position < node.ContentEnd)
            {
                Node child = Read(position, node.ContentEnd, 0);
                children.Add(child);
                position = child.End;
            }

            return children;
        }

        public Node Expect(Node node, byte tag)
        {
            if (node.Tag != tag)
            {
                throw Fail("A CMS value has tag 0x" + node.Tag.ToString("X2", CultureInfo.InvariantCulture) + " where 0x" + tag.ToString("X2", CultureInfo.InvariantCulture) + " was expected.");
            }

            return node;
        }

        // ContentInfo { signedData, [0] EXPLICIT SignedData } (RFC 5652 3 and 5.1).
        public SignedData ReadSignedData(Node contentInfo)
        {
            List<Node> info = Children(Expect(contentInfo, 0x30));
            if (info.Count != 2 || Oid(info[0]) != SignedDataOid)
            {
                throw Fail("The CMS value is not a SignedData.");
            }

            List<Node> wrapper = Children(Expect(info[1], 0xA0));
            if (wrapper.Count != 1)
            {
                throw Fail("The CMS value is not a SignedData.");
            }

            List<Node> parts = Children(Expect(wrapper[0], 0x30));
            int index = 0;
            Expect(Take(parts, ref index, this), 0x02);
            Expect(Take(parts, ref index, this), 0x31);
            Node encapsulated = Expect(Take(parts, ref index, this), 0x30);
            var certificates = new List<byte[]>();
            if (index < parts.Count && parts[index].Tag == 0xA0)
            {
                foreach (Node choice in Children(parts[index]))
                {
                    // Only the plain Certificate choice is a certificate; the attribute and other choices are skipped.
                    if (choice.Tag == 0x30)
                    {
                        certificates.Add(Copy(choice));
                    }
                }

                index++;
            }

            if (index < parts.Count && parts[index].Tag == 0xA1)
            {
                index++;
            }

            Node signerInfos = Expect(Take(parts, ref index, this), 0x31);
            if (index != parts.Count)
            {
                throw Fail("The SignedData has unexpected fields.");
            }

            List<Node> signers = Children(signerInfos);
            if (signers.Count != 1)
            {
                throw Fail("The CMS signature must have exactly one SignerInfo.");
            }

            return new SignedData(encapsulated, certificates, Expect(signers[0], 0x30));
        }

        // EncapsulatedContentInfo { id-ct-TSTInfo, [0] EXPLICIT OCTET STRING TSTInfo }, and TSTInfo's fifth field (RFC 3161 2.4.2).
        public DateTimeOffset GenTime(SignedData token)
        {
            List<Node> encapsulated = Children(token.Encapsulated);
            if (encapsulated.Count != 2 || Oid(encapsulated[0]) != TstInfoOid)
            {
                throw Fail("The time-stamp token does not hold a TSTInfo.");
            }

            List<Node> wrapper = Children(Expect(encapsulated[1], 0xA0));
            if (wrapper.Count != 1)
            {
                throw Fail("The time-stamp token does not hold a TSTInfo.");
            }

            Node octets = Expect(wrapper[0], 0x04);
            Node tstInfo = Read(octets.ContentStart, octets.ContentEnd, 0);
            if (tstInfo.End != octets.ContentEnd)
            {
                throw Fail("The TSTInfo has trailing bytes.");
            }

            List<Node> fields = Children(Expect(tstInfo, 0x30));
            if (fields.Count < 5)
            {
                throw Fail("The TSTInfo is missing a required field.");
            }

            return Time(Expect(fields[4], 0x18));
        }

        public string AlgorithmOid(Node algorithm)
        {
            List<Node> parts = Children(Expect(algorithm, 0x30));
            if (parts.Count == 0)
            {
                throw Fail("An AlgorithmIdentifier is empty.");
            }

            return Oid(parts[0]);
        }

        // The one value of an attribute that RFC 5652 11 allows once with one value, or null when it is absent.
        public Node? SingleAttribute(Node attributes, string oid)
        {
            Node? found = null;
            foreach (Node attribute in Children(attributes))
            {
                List<Node> parts = Children(Expect(attribute, 0x30));
                if (parts.Count != 2 || Oid(parts[0]) != oid)
                {
                    continue;
                }

                List<Node> values = Children(Expect(parts[1], 0x31));
                if (found != null || values.Count != 1)
                {
                    throw Fail("A CMS attribute that allows one value appears more than once.");
                }

                found = values[0];
            }

            return found;
        }

        public Node? FirstAttributeValue(Node attributes, string oid)
        {
            foreach (Node attribute in Children(attributes))
            {
                List<Node> parts = Children(Expect(attribute, 0x30));
                if (parts.Count == 2 && Oid(parts[0]) == oid)
                {
                    List<Node> values = Children(Expect(parts[1], 0x31));
                    if (values.Count == 0)
                    {
                        throw Fail("A CMS attribute has no value.");
                    }

                    return values[0];
                }
            }

            return null;
        }

        // SignerIdentifier: IssuerAndSerialNumber, or [0] SubjectKeyIdentifier (RFC 5652 5.3).
        public bool Identifies(Node identifier, byte[] certificate)
        {
            var reader = new Reader(certificate, _offset);
            List<Node> tbs = reader.TbsFields(certificate);
            int index = tbs[0].Tag == 0xA0 ? 1 : 0;
            if (tbs.Count < index + 6)
            {
                throw Fail("An embedded certificate is missing a required field.");
            }

            if (identifier.Tag == 0x30)
            {
                List<Node> parts = Children(identifier);
                if (parts.Count != 2)
                {
                    throw Fail("The SignerInfo IssuerAndSerialNumber is malformed.");
                }

                Node serial = reader.Expect(tbs[index], 0x02);
                Node issuer = reader.Expect(tbs[index + 2], 0x30);
                return Same(_data, Expect(parts[0], 0x30), certificate, issuer, whole: true)
                    && Same(_data, Expect(parts[1], 0x02), certificate, serial, whole: false);
            }

            if (identifier.Tag != 0x80)
            {
                throw Fail("The SignerInfo identifier is neither IssuerAndSerialNumber nor SubjectKeyIdentifier.");
            }

            for (int i = index + 6; i < tbs.Count; i++)
            {
                if (tbs[i].Tag != 0xA3)
                {
                    continue;
                }

                List<Node> wrapper = reader.Children(tbs[i]);
                if (wrapper.Count != 1)
                {
                    throw Fail("An embedded certificate's extensions are malformed.");
                }

                foreach (Node extension in reader.Children(reader.Expect(wrapper[0], 0x30)))
                {
                    List<Node> parts = reader.Children(reader.Expect(extension, 0x30));
                    if (parts.Count < 2 || reader.Oid(parts[0]) != SubjectKeyIdentifierOid)
                    {
                        continue;
                    }

                    Node value = reader.Expect(parts[parts.Count - 1], 0x04);
                    Node keyId = reader.Read(value.ContentStart, value.ContentEnd, 0);
                    return Same(_data, identifier, certificate, reader.Expect(keyId, 0x04), whole: false);
                }
            }

            return false;
        }

        public string Oid(Node node)
        {
            Expect(node, 0x06);
            if (node.ContentEnd == node.ContentStart)
            {
                throw Fail("An object identifier is empty.");
            }

            var builder = new StringBuilder();
            long value = 0;
            bool pending = false;
            for (int i = node.ContentStart; i < node.ContentEnd; i++)
            {
                byte b = _data[i];
                if ((!pending && b == 0x80) || value > (long.MaxValue >> 7))
                {
                    throw Fail("An object identifier is malformed.");
                }

                value = (value << 7) | (long)(b & 0x7F);
                pending = (b & 0x80) != 0;
                if (pending)
                {
                    continue;
                }

                if (builder.Length == 0)
                {
                    long arc = value < 40 ? 0 : value < 80 ? 1 : 2;
                    builder.Append(arc.ToString(CultureInfo.InvariantCulture)).Append('.').Append((value - (arc * 40)).ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append('.').Append(value.ToString(CultureInfo.InvariantCulture));
                }

                value = 0;
            }

            if (pending)
            {
                throw Fail("An object identifier is truncated.");
            }

            return builder.ToString();
        }

        // UTCTime (two-digit year, 1950 to 2049 as RFC 5280 4.1.2.5.1) or GeneralizedTime with an optional fraction.
        // Seconds may be omitted, as BER allows. A time without Z or an offset is local to nobody and is refused.
        public DateTimeOffset Time(Node node)
        {
            if (node.Tag != 0x17 && node.Tag != 0x18)
            {
                throw Fail("A CMS time is neither UTCTime nor GeneralizedTime.");
            }

            string text = Encoding.ASCII.GetString(_data, node.ContentStart, node.ContentEnd - node.ContentStart);
            int position = 0;
            int year;
            if (node.Tag == 0x17)
            {
                year = Digits(text, ref position, 2);
                year += year >= 50 ? 1900 : 2000;
            }
            else
            {
                year = Digits(text, ref position, 4);
            }

            int month = Digits(text, ref position, 2);
            int day = Digits(text, ref position, 2);
            int hour = Digits(text, ref position, 2);
            int minute = Digits(text, ref position, 2);
            int second = position < text.Length && IsDigit(text[position]) ? Digits(text, ref position, 2) : 0;
            long ticks = 0;
            if (node.Tag == 0x18 && position < text.Length && (text[position] == '.' || text[position] == ','))
            {
                position++;
                int start = position;
                long scale = TimeSpan.TicksPerSecond;
                while (position < text.Length && IsDigit(text[position]))
                {
                    scale /= 10;
                    ticks += (text[position] - '0') * scale;
                    position++;
                }

                if (position == start)
                {
                    throw Fail("A CMS time has an empty fraction.");
                }
            }

            TimeSpan offset;
            if (position == text.Length - 1 && text[position] == 'Z')
            {
                offset = TimeSpan.Zero;
            }
            else if (position == text.Length - 5 && (text[position] == '+' || text[position] == '-'))
            {
                bool negative = text[position] == '-';
                position++;
                int hours = Digits(text, ref position, 2);
                int minutes = Digits(text, ref position, 2);
                offset = new TimeSpan(hours, minutes, 0);
                offset = negative ? -offset : offset;
            }
            else
            {
                throw Fail("A CMS time has no time zone.");
            }

            try
            {
                return new DateTimeOffset(year, month, day, hour, minute, second, offset).AddTicks(ticks);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw Fail("A CMS time is out of range.");
            }
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        // Equal TLVs, or for an INTEGER or OCTET STRING equal contents.
        private static bool Same(byte[] leftData, Node left, byte[] rightData, Node right, bool whole)
        {
            int leftStart = whole ? left.Start : left.ContentStart;
            int rightStart = whole ? right.Start : right.ContentStart;
            int leftEnd = whole ? left.End : left.ContentEnd;
            int rightEnd = whole ? right.End : right.ContentEnd;
            return leftData.AsSpan(leftStart, leftEnd - leftStart).SequenceEqual(rightData.AsSpan(rightStart, rightEnd - rightStart));
        }

        private int Digits(string text, ref int position, int count)
        {
            if (position + count > text.Length)
            {
                throw Fail("A CMS time is truncated.");
            }

            int value = 0;
            for (int i = 0; i < count; i++)
            {
                char c = text[position + i];
                if (!IsDigit(c))
                {
                    throw Fail("A CMS time has a character that is not a digit.");
                }

                value = (value * 10) + (c - '0');
            }

            position += count;
            return value;
        }

        // Certificate { TBSCertificate, ... } read from this reader's own bytes, which are the one certificate.
        private List<Node> TbsFields(byte[] certificate)
        {
            List<Node> outer = Children(Expect(Read(0, certificate.Length, 0), 0x30));
            if (outer.Count == 0)
            {
                throw Fail("An embedded certificate is empty.");
            }

            List<Node> tbs = Children(Expect(outer[0], 0x30));
            if (tbs.Count == 0)
            {
                throw Fail("An embedded certificate is empty.");
            }

            return tbs;
        }

        private byte[] Copy(Node node)
        {
            return _data.AsSpan(node.Start, node.End - node.Start).ToArray();
        }
    }
}
