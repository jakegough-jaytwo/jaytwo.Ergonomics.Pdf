using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
#if NET8_0_OR_GREATER
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class SignatureTests
{
    private const string KnownRequest = "30360201013031300d060960864801650304020105000420ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void rfc3161_request_matches_the_sha256_abc_vector()
    {
        byte[] digest = SHA256.HashData(Encoding.ASCII.GetBytes("abc"));
        string actual = Convert.ToHexString(Rfc3161TimeStampRequest.Create(digest)).ToLowerInvariant();

        Assert.Equal(KnownRequest, actual);
    }

    [Fact]
    public void http_timestamp_authority_posts_the_query_and_returns_the_token()
    {
        byte[] token = DerCodec.Sequence(DerCodec.Integer(7));
        byte[] body = DerCodec.Sequence(DerCodec.Sequence(DerCodec.Integer(0)), token);
        var handler = new RecordingHandler(body);
        var authority = new HttpTimestampAuthority(new HttpClient(handler), new Uri("https://tsa.example/stamp"));

        byte[] returned = authority.GetTimeStampToken(new byte[] { 1, 2, 3 });

        Assert.Equal(token, returned);
        Assert.Equal("application/timestamp-query", handler.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, handler.Body);
    }

    [Fact]
    public void http_timestamp_authority_accepts_granted_with_mods()
    {
        byte[] token = DerCodec.Sequence(DerCodec.Integer(4));
        byte[] body = DerCodec.Sequence(DerCodec.Sequence(DerCodec.Integer(1)), token);
        var authority = new HttpTimestampAuthority(new HttpClient(new RecordingHandler(body)), new Uri("https://tsa.example/stamp"));

        Assert.Equal(token, authority.GetTimeStampToken(new byte[] { 1 }));
    }

    [Fact]
    public void http_timestamp_authority_rejects_a_failure_status()
    {
        byte[] body = DerCodec.Sequence(DerCodec.Sequence(DerCodec.Integer(2)));
        var authority = new HttpTimestampAuthority(new HttpClient(new RecordingHandler(body)), new Uri("https://tsa.example/stamp"));

        Assert.Throws<PdfTimestampException>(() => authority.GetTimeStampToken(new byte[] { 1 }));
    }

    [Fact]
    public void incremental_revision_appends_a_classic_xref()
    {
        byte[] pdf = Titled("Old");
        var offsets = StartXrefs(pdf);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Metadata.Title = "One";
            document.SaveIncremental(stream);
            document.Metadata.Author = "Two";
            document.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        List<long> xrefs = StartXrefs(saved);
        Assert.Equal(3, xrefs.Count);
        Assert.Equal(offsets[0], xrefs[0]);
        List<long> prev = PrevValues(saved);
        Assert.Equal(xrefs[0], prev[0]);
        Assert.Equal(xrefs[1], prev[1]);
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Match entry = Regex.Match(appendix, @"\d{10} \d{5} [nf]\r\n");
        Assert.Equal(20, entry.Length);
    }

    [Fact]
    public void fill_and_metadata_share_one_revision_and_can_be_signed_together()
    {
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Metadata.Title = "Signed";
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Name", "Ada"));
            document.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 128, SigningTime = DateTimeOffset.UnixEpoch }, new FixedProvider());
        }

        Assert.Equal(2, StartXrefs(stream.ToArray()).Count);
        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("Ada", again.AcroForm.Fields[0].Value);
        Assert.Equal("Signed", again.Metadata.Title);
        Assert.Contains(again.AcroForm.Fields, field => field.FieldType == PdfFieldType.Signature);
    }

    [Fact]
    public void byte_range_excludes_the_contents_gap_and_a_small_gap_rolls_back()
    {
        byte[] pdf = TextField();
        var hasher = new HashProvider();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 64 }, hasher);
            Assert.Throws<PdfSignatureTooLargeException>(() => document.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 8 }, new FixedProvider { Payload = new byte[32] }));
        }

        byte[] saved = stream.ToArray();
        Match range = Regex.Matches(Encoding.ASCII.GetString(saved), @"/ByteRange\s*\[\s*(\d{10})\s+(\d{10})\s+(\d{10})\s+(\d{10})\s*\]")[0];
        long offset0 = long.Parse(range.Groups[1].Value);
        long length0 = long.Parse(range.Groups[2].Value);
        long offset1 = long.Parse(range.Groups[3].Value);
        long length1 = long.Parse(range.Groups[4].Value);
        Assert.Equal(0, offset0);
        Assert.Equal(2 + (64 * 2), offset1 - (offset0 + length0));
        Assert.Equal(saved.Length, offset1 + length1);
        Assert.Equal(hasher.Digest, HashRanges(saved, offset0, length0, offset1, length1));
        saved[offset0 + length0 + 4] ^= 0x1;
        Assert.Equal(hasher.Digest, HashRanges(saved, offset0, length0, offset1, length1));
        Assert.Single(Regex.Matches(Encoding.ASCII.GetString(saved), "/ByteRange"));
    }

    [Fact]
    public void two_signatures_and_existing_id_are_preserved()
    {
        var builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 40 12] /P 3 0 R >>");
        builder.Ascii(string.Empty);
        byte[] pdf = builder.Finish("/ID [<00112233445566778899AABBCCDDEEFF><00112233445566778899AABBCCDDEEFF>]");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(stream, new PdfSignatureOptions { FieldName = "Signature1", ReservedContentsBytes = 32 }, new FixedProvider());
            document.Sign(stream, new PdfSignatureOptions { FieldName = "Signature2", ReservedContentsBytes = 32 }, new FixedProvider());
        }

        string saved = Encoding.ASCII.GetString(stream.ToArray());
        Assert.Equal(2, Regex.Matches(saved, "/ByteRange").Count);
        Assert.Contains("/ID", saved, StringComparison.Ordinal);
        Assert.True(Regex.Matches(saved, "00112233445566778899AABBCCDDEEFF").Count >= 4);
    }

    [Fact]
    public void missing_id_is_created_only_when_signing()
    {
        byte[] pdf = Titled("Old");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Metadata.Title = "New";
            document.SaveIncremental(stream);
            string afterInfo = Encoding.ASCII.GetString(stream.ToArray());
            Assert.DoesNotContain("/ID", afterInfo, StringComparison.Ordinal);
            document.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider());
        }

        Assert.Contains("/ID", Encoding.ASCII.GetString(stream.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public void need_appearances_blocks_signing_without_writing()
    {
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        long length = stream.Length;
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        document.AcroForm.SetText("Name", "日本語");

        Assert.Throws<PdfSignException>(() => document.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider()));
        Assert.Equal(length, stream.Length);
    }

    [Fact]
    public void document_timestamp_embeds_the_token_for_the_range_hash()
    {
        byte[] pdf = TextField();
        var authority = new RecordingAuthority();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(
                stream,
                new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 32 },
                new DocTimeStampSignatureProvider(authority));
        }

        byte[] saved = stream.ToArray();
        Match range = Regex.Match(Encoding.ASCII.GetString(saved), @"/ByteRange\s*\[\s*(\d{10})\s+(\d{10})\s+(\d{10})\s+(\d{10})\s*\]");
        byte[] digest = HashRanges(
            saved,
            long.Parse(range.Groups[1].Value),
            long.Parse(range.Groups[2].Value),
            long.Parse(range.Groups[3].Value),
            long.Parse(range.Groups[4].Value));
        Assert.Equal(Rfc3161TimeStampRequest.Create(digest), authority.Request);
        Assert.Contains("01020304", Encoding.ASCII.GetString(saved), StringComparison.Ordinal);
        Assert.Contains("/DocTimeStamp", Encoding.ASCII.GetString(saved), StringComparison.Ordinal);
    }

    [Fact]
    public void signature_kind_must_match_the_provider_and_the_source_stream_can_be_signed()
    {
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        long length = stream.Length;
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Throws<PdfSignException>(() => document.Sign(
                new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 32 },
                new FixedProvider()));
            Assert.Equal(length, stream.Length);
            document.AcroForm.SetText("Name", "Ada");
            document.SaveIncremental();
            document.Sign(new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider());
        }

        Assert.True(stream.Length > length);
        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("Ada", again.AcroForm.GetFields("Name")[0].Value);
        Assert.Contains(again.AcroForm.Fields, field => field.FieldType == PdfFieldType.Signature);

        var readOnly = new MemoryStream(pdf, writable: false);
        using var locked = PdfDocument.Open(readOnly, leaveOpen: true);
        Assert.Throws<ArgumentException>(() => locked.SaveIncremental());
        Assert.Throws<ArgumentException>(() => locked.Sign(new PdfSignatureOptions(), new FixedProvider()));
    }

    [Fact]
    public void cms_signature_verifies_and_covers_the_byte_range()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Pdf Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(
                stream,
                new PdfSignatureOptions { ReservedContentsBytes = 8192, SigningTime = DateTimeOffset.UnixEpoch },
                new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData)));
        }

        byte[] saved = stream.ToArray();
        Match range = Regex.Match(Encoding.ASCII.GetString(saved), @"/ByteRange\s*\[\s*(\d{10})\s+(\d{10})\s+(\d{10})\s+(\d{10})\s*\]");
        long offset0 = long.Parse(range.Groups[1].Value);
        long length0 = long.Parse(range.Groups[2].Value);
        long offset1 = long.Parse(range.Groups[3].Value);
        long length1 = long.Parse(range.Groups[4].Value);
        byte[] digest = HashRanges(saved, offset0, length0, offset1, length1);
        Match contents = Regex.Match(Encoding.ASCII.GetString(saved), @"/Contents\s*<([0-9A-Fa-f]+)>");
        byte[] padded = Convert.FromHexString(contents.Groups[1].Value);
        byte[] cmsBytes = DerCodec.ReadTlv(padded).Full;
        Assert.Contains(Convert.ToHexString(digest), Convert.ToHexString(cmsBytes), StringComparison.Ordinal);
        Assert.Contains("D:19700101000000Z", Encoding.ASCII.GetString(saved), StringComparison.Ordinal);
        Assert.Contains(Convert.ToHexString(Encoding.ASCII.GetBytes("700101000000Z")), Convert.ToHexString(cmsBytes), StringComparison.Ordinal);
#if NET8_0_OR_GREATER
        var content = new byte[length0 + length1];
        Buffer.BlockCopy(saved, (int)offset0, content, 0, (int)length0);
        Buffer.BlockCopy(saved, (int)offset1, content, (int)length0, (int)length1);
        var cms = new SignedCms(new ContentInfo(content), detached: true);
        cms.Decode(cmsBytes);
        cms.CheckSignature(verifySignatureOnly: true);
        byte[] messageDigest = MessageDigest(cms);
        Assert.Equal(digest, messageDigest);
#endif
    }

    [Fact]
    public void cms_embeds_the_certificate_chain_and_a_timestamp_over_the_signature_value()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Pdf Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        using var intermediateRsa = RSA.Create(2048);
        var intermediateRequest = new CertificateRequest("CN=Pdf Intermediate", intermediateRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var intermediate = intermediateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var signer = new RsaSigner(rsa, certificate.RawData, new[] { intermediate.RawData });
        var authority = new TokenAuthority(DerCodec.Sequence(DerCodec.Integer(1)));
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(
                stream,
                new PdfSignatureOptions { ReservedContentsBytes = 16384, SigningTime = DateTimeOffset.UnixEpoch },
                new CmsDetachedSignatureProvider(signer, authority));
        }

        byte[] saved = stream.ToArray();
        Match contents = Regex.Match(Encoding.ASCII.GetString(saved), @"/Contents\s*<([0-9A-Fa-f]+)>");
        byte[] cmsBytes = DerCodec.ReadTlv(Convert.FromHexString(contents.Groups[1].Value)).Full;
        string cmsHex = Convert.ToHexString(cmsBytes);
        Assert.Contains(Convert.ToHexString(certificate.RawData), cmsHex, StringComparison.Ordinal);
        Assert.Contains(Convert.ToHexString(intermediate.RawData), cmsHex, StringComparison.Ordinal);
        Assert.Contains("2A864886F70D010910020E", cmsHex, StringComparison.Ordinal);
        byte[] imprint = SHA256.HashData(signer.LastSignature);
        Assert.Contains(Convert.ToHexString(imprint), Convert.ToHexString(authority.Request), StringComparison.Ordinal);
#if NET8_0_OR_GREATER
        Match range = Regex.Match(Encoding.ASCII.GetString(saved), @"/ByteRange\s*\[\s*(\d{10})\s+(\d{10})\s+(\d{10})\s+(\d{10})\s*\]");
        long offset0 = long.Parse(range.Groups[1].Value);
        long length0 = long.Parse(range.Groups[2].Value);
        long offset1 = long.Parse(range.Groups[3].Value);
        long length1 = long.Parse(range.Groups[4].Value);
        var content = new byte[length0 + length1];
        Buffer.BlockCopy(saved, (int)offset0, content, 0, (int)length0);
        Buffer.BlockCopy(saved, (int)offset1, content, (int)length0, (int)length1);
        var cms = new SignedCms(new ContentInfo(content), detached: true);
        cms.Decode(cmsBytes);
        cms.CheckSignature(verifySignatureOnly: true);
        Assert.Equal(2, cms.Certificates.Count);
        Assert.Equal(HashRanges(saved, offset0, length0, offset1, length1), MessageDigest(cms));
#endif
    }

    [Fact]
    public void empty_signature_field_is_reused_and_a_second_sign_does_not_grow()
    {
        byte[] pdf = SignatureField("<< /Subtype /Widget /FT /Sig /T (SignHere) /Rect [10 20 110 40] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(1, document.AcroForm.Fields.Count);
            document.Sign(
                stream,
                new PdfSignatureOptions
                {
                    FieldName = "SignHere",
                    PageNumber = 9,
                    Rect = new PdfRect(1, 2, 3, 4),
                    ReservedContentsBytes = 32,
                },
                new FixedProvider());
            Assert.Equal(1, document.AcroForm.Fields.Count);
            PdfField signed = Assert.Single(document.AcroForm.Fields);
            Assert.Equal(new PdfRect(10, 20, 110, 40), signed.Rect);
            long length = stream.Length;
            Assert.Throws<PdfSignException>(() => document.Sign(
                stream,
                new PdfSignatureOptions { FieldName = "SignHere", ReservedContentsBytes = 32 },
                new FixedProvider()));
            Assert.Equal(length, stream.Length);
        }

        byte[] saved = stream.ToArray();
        using var again = PdfDocument.Open(new MemoryStream(saved), leaveOpen: true);
        PdfField field = Assert.Single(again.AcroForm.Fields);
        Assert.Equal(PdfFieldType.Signature, field.FieldType);
        Assert.Equal("SignHere", field.FullName);
        Assert.Equal(new PdfRect(10, 20, 110, 40), field.Rect);
        Assert.Equal(1, field.PageNumber);
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("/Rect [10 20 110 40]", appendix, StringComparison.Ordinal);
        Assert.Contains("/P 3 0 R", appendix, StringComparison.Ordinal);
        Assert.Contains("/SigFlags 3", appendix, StringComparison.Ordinal);
        Match value = Regex.Match(appendix, @"/V (\d+) 0 R");
        Assert.True(value.Success);
        int valueId = int.Parse(value.Groups[1].Value);
        Assert.NotEqual(4, valueId);
        Assert.Contains(valueId + " 0 obj", appendix, StringComparison.Ordinal);
        Assert.Contains("/Type /Sig", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public void null_signature_value_is_signable()
    {
        byte[] pdf = SignatureField("<< /Subtype /Widget /FT /Sig /T (SignHere) /V null /Rect [10 20 110 40] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(stream, new PdfSignatureOptions { FieldName = "SignHere", ReservedContentsBytes = 32 }, new FixedProvider());
            Assert.Equal(1, document.AcroForm.Fields.Count);
        }

        byte[] saved = stream.ToArray();
        using var again = PdfDocument.Open(new MemoryStream(saved), leaveOpen: true);
        PdfField field = Assert.Single(again.AcroForm.Fields);
        Assert.Equal(new PdfRect(10, 20, 110, 40), field.Rect);
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Matches(@"/V \d+ 0 R", appendix);
    }

    [Fact]
    public void signature_parent_keeps_its_widget_and_receives_the_value()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>");
        builder.Indirect(4, "<< /FT /Sig /T (SignHere) /Kids [5 0 R] >>");
        builder.Indirect(5, "<< /Subtype /Widget /Rect [10 20 110 40] /P 3 0 R /Parent 4 0 R >>");
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(
                stream,
                new PdfSignatureOptions
                {
                    FieldName = "SignHere",
                    PageNumber = 9,
                    Rect = new PdfRect(1, 2, 3, 4),
                    ReservedContentsBytes = 32,
                },
                new FixedProvider());
            long length = stream.Length;
            Assert.Throws<PdfSignException>(() => document.Sign(
                stream,
                new PdfSignatureOptions { FieldName = "SignHere", ReservedContentsBytes = 32 },
                new FixedProvider()));
            Assert.Equal(length, stream.Length);
        }

        byte[] saved = stream.ToArray();
        using var again = PdfDocument.Open(new MemoryStream(saved), leaveOpen: true);
        PdfField field = Assert.Single(again.AcroForm.Fields);
        Assert.Equal(PdfFieldType.Signature, field.FieldType);
        Assert.Equal(new PdfRect(10, 20, 110, 40), field.Rect);
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.DoesNotContain("/Subtype /Widget", appendix, StringComparison.Ordinal);
        Assert.Contains("/V ", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public void text_field_name_is_rejected_without_writing()
    {
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        long length = stream.Length;
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        Assert.Throws<PdfSignException>(() => document.Sign(
            stream,
            new PdfSignatureOptions { FieldName = "Name", ReservedContentsBytes = 32 },
            new FixedProvider()));
        Assert.Equal(length, stream.Length);
    }

    [Fact]
    public void read_only_signature_field_is_rejected_without_writing()
    {
        byte[] pdf = SignatureField("<< /Subtype /Widget /FT /Sig /T (SignHere) /Ff 1 /Rect [10 20 110 40] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        long length = stream.Length;
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        Assert.Throws<PdfFieldReadOnlyException>(() => document.Sign(
            stream,
            new PdfSignatureOptions { FieldName = "SignHere", ReservedContentsBytes = 32 },
            new FixedProvider()));
        Assert.Equal(length, stream.Length);
    }

    [Fact]
    public void document_timestamp_follows_a_cms_signature()
    {
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider());
            document.Sign(
                stream,
                new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 32 },
                new DocTimeStampSignatureProvider(new RecordingAuthority()));
        }

        byte[] saved = stream.ToArray();
        string text = Encoding.ASCII.GetString(saved);
        Assert.Contains("/Type /DocTimeStamp", text, StringComparison.Ordinal);
        Assert.Contains("/SubFilter /ETSI.RFC3161", text, StringComparison.Ordinal);
        Assert.Equal(3, StartXrefs(saved).Count);
    }

#if NET8_0_OR_GREATER
    private static byte[] MessageDigest(SignedCms cms)
    {
        foreach (CryptographicAttributeObject attribute in cms.SignerInfos[0].SignedAttributes)
        {
            if (attribute.Oid.Value == "1.2.840.113549.1.9.4")
            {
                return System.Formats.Asn1.AsnDecoder.ReadOctetString(attribute.Values[0].RawData, System.Formats.Asn1.AsnEncodingRules.DER, out _);
            }
        }

        throw new InvalidOperationException("messageDigest was not found.");
    }
#endif

    private static byte[] HashRanges(byte[] file, long offset0, long length0, long offset1, long length1)
    {
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        incremental.AppendData(file, (int)offset0, (int)length0);
        incremental.AppendData(file, (int)offset1, (int)length1);
        return incremental.GetHashAndReset();
    }

    private static List<long> StartXrefs(byte[] file)
    {
        var values = new List<long>();
        string text = Encoding.ASCII.GetString(file);
        foreach (Match match in Regex.Matches(text, @"startxref\s+(\d+)\s+%%EOF"))
        {
            values.Add(long.Parse(match.Groups[1].Value));
        }

        return values;
    }

    private static List<long> PrevValues(byte[] file)
    {
        var values = new List<long>();
        foreach (Match match in Regex.Matches(Encoding.ASCII.GetString(file), @"/Prev (\d+)"))
        {
            values.Add(long.Parse(match.Groups[1].Value));
        }

        return values;
    }

    private static byte[] Titled(string title)
    {
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title (" + title + ") >>");
        return builder.Finish("/Info 4 0 R");
    }

    private static byte[] TextField()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 240 720] /P 3 0 R >>");
        return builder.Finish();
    }

    private static byte[] SignatureField(string field)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, field);
        return builder.Finish();
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public byte[] Payload { get; set; } = new byte[] { 1, 2, 3, 4 };

        public byte[] CreateContents(PdfSignatureContext context)
        {
            return Payload;
        }
    }

    private sealed class HashProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public byte[] Digest { get; private set; } = Array.Empty<byte>();

        public byte[] CreateContents(PdfSignatureContext context)
        {
            using Stream signed = context.OpenSignedBytes();
            using SHA256 sha = SHA256.Create();
            Digest = sha.ComputeHash(signed);
            return new byte[] { 1, 2, 3, 4 };
        }
    }

    private sealed class RecordingAuthority : ITimestampAuthority
    {
        public byte[] Request { get; private set; } = Array.Empty<byte>();

        public byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer)
        {
            Request = timeStampReqDer.ToArray();
            return new byte[] { 1, 2, 3, 4 };
        }
    }

    private sealed class RsaSigner : IPdfExternalSigner
    {
        private readonly RSA _rsa;

        public RsaSigner(RSA rsa, byte[] certificate)
            : this(rsa, certificate, Array.Empty<byte[]>())
        {
        }

        public RsaSigner(RSA rsa, byte[] certificate, IReadOnlyList<byte[]> additionalCertificates)
        {
            _rsa = rsa;
            CertificateDer = certificate;
            AdditionalCertificates = additionalCertificates ?? throw new ArgumentNullException(nameof(additionalCertificates));
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates { get; }

        public byte[] LastSignature { get; private set; } = Array.Empty<byte>();

        public byte[] SignHash(ReadOnlySpan<byte> hash)
        {
            LastSignature = _rsa.SignHash(hash.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return LastSignature;
        }
    }

    private sealed class TokenAuthority : ITimestampAuthority
    {
        private readonly byte[] _token;

        public TokenAuthority(byte[] token)
        {
            _token = token;
        }

        public byte[] Request { get; private set; } = Array.Empty<byte>();

        public byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer)
        {
            Request = timeStampReqDer.ToArray();
            return _token;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly byte[] _response;

        public RecordingHandler(byte[] response)
        {
            _response = response;
        }

        public string? ContentType { get; private set; }

        public byte[] Body { get; private set; } = Array.Empty<byte>();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = request.Content == null
                ? Array.Empty<byte>()
                : request.Content.ReadAsByteArrayAsync(cancellationToken).GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_response),
            };
        }
    }
}
