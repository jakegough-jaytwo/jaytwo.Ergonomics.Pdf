using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

public class SignatureTests
{
    private const string KnownRequest = "30390201013031300d060960864801650304020105000420ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad0101ff";

    private enum TimestampFault
    {
        None,
        Imprint,
        Algorithm,
        MissingNonce,
        MismatchedNonce,
        MissingCertificate,
        OtherCertificate,
        IssuerSerialMismatch,
        MissingSigningCertificateAttribute,
        MultipleSignerInfos,
        MultipleSigningCertificateAttributes,
        FirstCertificateIdentifierMismatch,
        MalformedIssuerSerial,
        UnsupportedEssHash,
        TrailingTokenData,
        NonMinimalDerLength,
    }

    private enum EssForm
    {
        V2DefaultHashWithIssuerSerial,
        V2Sha512WithoutIssuerSerial,
        V1Sha1WithIssuerSerial,
        V1AndV2,
    }

    [Fact]
    public void rfc3161_request_matches_the_sha256_abc_vector()
    {
        byte[] digest = TestCompat.Sha256(Encoding.ASCII.GetBytes("abc"));
        byte[] request = Rfc3161TimeStampRequest.Create(digest);
        string actual = TestCompat.ToHexString(request).ToLowerInvariant();

        Assert.Equal(KnownRequest, actual);
        Assert.Equal(59, request.Length);
        Assert.EndsWith("0101ff", actual, StringComparison.Ordinal);
    }

    [Fact]
    public void rfc3161_request_with_a_nonce_appends_the_integer_and_sets_cert_req()
    {
        byte[] digest = TestCompat.Sha256(Encoding.ASCII.GetBytes("abc"));
        byte[] nonce = new byte[] { 0x80 };
        byte[] actual = Rfc3161TimeStampRequest.Create(digest, nonce);

        Assert.Equal(
            DerCodec.Sequence(
                DerCodec.Integer(1),
                DerCodec.Sequence(DerCodec.AlgorithmSha256(), DerCodec.OctetString(digest)),
                DerCodec.Integer(nonce),
                new byte[] { 0x01, 0x01, 0xFF }),
            actual);
        Assert.NotEqual(Rfc3161TimeStampRequest.Create(digest), actual);
        Assert.EndsWith("020200800101FF", TestCompat.ToHexString(actual), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Rfc3161TimeStampRequest.Create(digest, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public async Task http_timestamp_authority_posts_the_query_and_returns_the_token()
    {
        byte[] token = DerCodec.Sequence(DerCodec.Integer(7));
        byte[] body = DerCodec.Sequence(DerCodec.Sequence(DerCodec.Integer(0)), token);
        var handler = new RecordingHandler(body);
        var authority = new HttpTimestampAuthority(new HttpClient(handler), new Uri("https://tsa.example/stamp"));

        byte[] returned = await authority.GetTimeStampTokenAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.Equal(token, returned);
        Assert.Equal("application/timestamp-query", handler.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, handler.Body);
    }

    [Fact]
    public async Task http_timestamp_authority_accepts_granted_with_mods()
    {
        byte[] token = DerCodec.Sequence(DerCodec.Integer(4));
        byte[] body = DerCodec.Sequence(DerCodec.Sequence(DerCodec.Integer(1)), token);
        var authority = new HttpTimestampAuthority(new HttpClient(new RecordingHandler(body)), new Uri("https://tsa.example/stamp"));

        Assert.Equal(token, await authority.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None));
    }

    [Fact]
    public async Task http_timestamp_authority_rejects_a_failure_status()
    {
        byte[] body = DerCodec.Sequence(DerCodec.Sequence(DerCodec.Integer(2)));
        var authority = new HttpTimestampAuthority(new HttpClient(new RecordingHandler(body)), new Uri("https://tsa.example/stamp"));

        await Assert.ThrowsAsync<PdfTimestampException>(async () => await authority.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None));
    }

    [Fact]
    public void negative_timestamp_status_does_not_extend_the_file()
    {
        byte[] negativeStatus = new byte[] { 0x02, 0x04, 0x80, 0x00, 0x00, 0x00 };
        byte[] token = DerCodec.Sequence(DerCodec.Integer(7));
        byte[] body = DerCodec.Sequence(DerCodec.Sequence(negativeStatus), token);

        AssertTimestampResponseDoesNotExtend(body, "rejected the request with status");
    }

    [Fact]
    public void overflowing_der_length_does_not_extend_the_file()
    {
        byte[] body = new byte[] { 0x30, 0x84, 0x7F, 0xFF, 0xFF, 0xFF };

        AssertTimestampResponseDoesNotExtend(body, "exceeds its buffer");
    }

    [Fact]
    public async Task oversized_timestamp_response_is_refused_after_a_bounded_read()
    {
        var body = new GeneratedStream(Array.Empty<byte>(), 16777216);
        var authority = new HttpTimestampAuthority(new HttpClient(new StreamHandler(body)), new Uri("https://tsa.example/stamp"));

        Exception? thrown = await Record.ExceptionAsync(async () => await authority.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None));

        Assert.InRange(body.BytesRead, 0, 2179072);
        PdfTimestampException rejected = Assert.IsType<PdfTimestampException>(thrown);
        Assert.Contains("larger than", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void oversized_timestamp_response_does_not_extend_the_file()
    {
        var body = new GeneratedStream(Array.Empty<byte>(), 16777216);

        AssertTimestampResponseDoesNotExtend(new StreamHandler(body), "larger than");
        Assert.InRange(body.BytesRead, 0, 2179072);
    }

    [Fact]
    public async Task declared_oversized_timestamp_response_is_refused_before_reading()
    {
        var body = new GeneratedStream(Array.Empty<byte>(), 16777216);
        var authority = new HttpTimestampAuthority(new HttpClient(new StreamHandler(body, 16777216)), new Uri("https://tsa.example/stamp"));

        Exception? thrown = await Record.ExceptionAsync(async () => await authority.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None));

        Assert.Equal(0, body.BytesRead);
        PdfTimestampException rejected = Assert.IsType<PdfTimestampException>(thrown);
        Assert.Contains("larger than", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task timestamp_response_at_the_cap_returns_its_token_and_one_byte_more_is_refused()
    {
        (byte[] atCap, byte[] token) = PaddedTimestampResponse(2097152);
        var atCapBody = new GeneratedStream(atCap, atCap.Length);
        var accepted = new HttpTimestampAuthority(new HttpClient(new StreamHandler(atCapBody)), new Uri("https://tsa.example/stamp"));

        Assert.Equal(token, await accepted.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None));
        Assert.Equal(2097152, atCapBody.BytesRead);

        (byte[] overCap, _) = PaddedTimestampResponse(2097153);
        var overCapBody = new GeneratedStream(overCap, overCap.Length);
        var refused = new HttpTimestampAuthority(new HttpClient(new StreamHandler(overCapBody)), new Uri("https://tsa.example/stamp"));

        PdfTimestampException rejected = await Assert.ThrowsAsync<PdfTimestampException>(async () => await refused.GetTimeStampTokenAsync(new byte[] { 1 }, CancellationToken.None));
        Assert.Contains("larger than", rejected.Message, StringComparison.Ordinal);
        Assert.InRange(overCapBody.BytesRead, 0, 2179072);
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
            using var editor = document.Edit();
            editor.Metadata.Title = "One";
            editor.SaveIncremental(stream);
            editor.Metadata.Author = "Two";
            editor.SaveIncremental(stream);
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
            using var editor = document.Edit();
            editor.Metadata.Title = "Signed";
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Name", "Ada"));
            editor.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 128, SigningTime = TestCompat.UnixEpoch }, new FixedProvider());
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
            using var editor = document.Edit();
            editor.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 64 }, hasher);
            Assert.Throws<PdfSignatureTooLargeException>(() => editor.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 8 }, new FixedProvider { Payload = new byte[32] }));
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
            using var editor = document.Edit();
            editor.Sign(stream, new PdfSignatureOptions { FieldName = "Signature1", ReservedContentsBytes = 32 }, new FixedProvider());
            editor.Sign(stream, new PdfSignatureOptions { FieldName = "Signature2", ReservedContentsBytes = 32 }, new FixedProvider());
        }

        string saved = Encoding.ASCII.GetString(stream.ToArray());
        Assert.Equal(2, Regex.Matches(saved, "/ByteRange").Count);
        Assert.Contains("/ID", saved, StringComparison.Ordinal);
        Assert.True(Regex.Matches(saved, "00112233445566778899AABBCCDDEEFF").Count >= 4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void cached_signature_state_resets_after_a_signature_revision(bool documentTimestamp)
    {
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();
        editor.Metadata.Title = "Primes the unsigned cache";

        PdfSignatureOptions options;
        IPdfSignatureProvider provider;
        if (documentTimestamp)
        {
            options = new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 4096 };
            provider = new DocTimeStampSignatureProvider(new RecordingAuthority());
        }
        else
        {
            options = new PdfSignatureOptions { ReservedContentsBytes = 32 };
            provider = new FixedProvider();
        }

        editor.Sign(stream, options, provider);

        Assert.Throws<PdfSignatureConflictException>(() => editor.Pages[0].Rotation = 90);
    }

    [Fact]
    public void missing_id_is_created_only_when_signing()
    {
        byte[] pdf = Titled("Old");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Metadata.Title = "New";
            editor.SaveIncremental(stream);
            string afterInfo = Encoding.ASCII.GetString(stream.ToArray());
            Assert.DoesNotContain("/ID", afterInfo, StringComparison.Ordinal);
            editor.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider());
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
        using var editor = document.Edit();
        editor.AcroForm.Fill("Name", "日本語");

        Assert.Throws<PdfSignException>(() => editor.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider()));
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
            using var editor = document.Edit();
            editor.Sign(
                stream,
                new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 4096 },
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
        (byte[] hashed, byte[] nonce) = ReadTimestampRequest(authority.Request);
        Assert.Equal(digest, hashed);
        Assert.NotEqual(Rfc3161TimeStampRequest.Create(digest), authority.Request);
        AssertNonceWidth(nonce);
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
            using var editor = document.Edit();
            Assert.Throws<PdfSignException>(() => editor.Sign(
                new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 32 },
                new FixedProvider()));
            Assert.Equal(length, stream.Length);
            editor.AcroForm.Fill("Name", "Ada");
            editor.SaveIncremental();
            editor.Sign(new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider());
        }

        Assert.True(stream.Length > length);
        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("Ada", again.AcroForm.GetFields("Name")[0].Value);
        Assert.Contains(again.AcroForm.Fields, field => field.FieldType == PdfFieldType.Signature);

        var readOnly = new MemoryStream(pdf, writable: false);
        using var locked = PdfDocument.Open(readOnly, leaveOpen: true);
        using var lockedEditor = locked.Edit();
        Assert.Throws<ArgumentException>(() => lockedEditor.SaveIncremental());
        Assert.Throws<ArgumentException>(() => lockedEditor.Sign(new PdfSignatureOptions(), new FixedProvider()));
    }

    [Fact]
    public void sign_to_another_stream_leaves_the_opened_stream_unchanged()
    {
        byte[] pdf = TextField();
        var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        var destination = new MemoryStream();
        using (var document = PdfDocument.Open(source, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Sign(
                destination,
                new PdfSignatureOptions { ReservedContentsBytes = 32 },
                new FixedProvider(),
                leaveOpen: true);
        }

        Assert.Equal(pdf, source.ToArray());
        byte[] signed = destination.ToArray();
        Assert.True(signed.Length > pdf.Length);
        var prefix = new byte[pdf.Length];
        Buffer.BlockCopy(signed, 0, prefix, 0, pdf.Length);
        Assert.Equal(pdf, prefix);
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
            using var editor = document.Edit();
            editor.Sign(
                stream,
                new PdfSignatureOptions { ReservedContentsBytes = 8192, SigningTime = TestCompat.UnixEpoch },
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
        byte[] padded = TestCompat.FromHexString(contents.Groups[1].Value);
        byte[] cmsBytes = DerCodec.ReadTlv(padded).Full;
        Assert.Contains(TestCompat.ToHexString(digest), TestCompat.ToHexString(cmsBytes), StringComparison.Ordinal);
        Assert.Contains("D:19700101000000Z", Encoding.ASCII.GetString(saved), StringComparison.Ordinal);
        Assert.Contains(TestCompat.ToHexString(Encoding.ASCII.GetBytes("700101000000Z")), TestCompat.ToHexString(cmsBytes), StringComparison.Ordinal);
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
        var authority = new TokenAuthority();
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Sign(
                stream,
                new PdfSignatureOptions { ReservedContentsBytes = 16384, SigningTime = TestCompat.UnixEpoch },
                new CmsDetachedSignatureProvider(signer, authority));
        }

        byte[] saved = stream.ToArray();
        Match contents = Regex.Match(Encoding.ASCII.GetString(saved), @"/Contents\s*<([0-9A-Fa-f]+)>");
        byte[] cmsBytes = DerCodec.ReadTlv(TestCompat.FromHexString(contents.Groups[1].Value)).Full;
        string cmsHex = TestCompat.ToHexString(cmsBytes);
        Assert.Contains(TestCompat.ToHexString(certificate.RawData), cmsHex, StringComparison.Ordinal);
        Assert.Contains(TestCompat.ToHexString(intermediate.RawData), cmsHex, StringComparison.Ordinal);
        Assert.Contains("2A864886F70D010910020E", cmsHex, StringComparison.Ordinal);
        (byte[] hashed, byte[] nonce) = ReadTimestampRequest(authority.Request);
        Assert.Equal(TestCompat.Sha256(signer.LastSignature), hashed);
        AssertNonceWidth(nonce);
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
            using var editor = document.Edit();
            Assert.Equal(1, document.AcroForm.Fields.Count);
            editor.Sign(
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
            Assert.Throws<PdfSignException>(() => editor.Sign(
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
            using var editor = document.Edit();
            editor.Sign(stream, new PdfSignatureOptions { FieldName = "SignHere", ReservedContentsBytes = 32 }, new FixedProvider());
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
            using var editor = document.Edit();
            editor.Sign(
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
            Assert.Throws<PdfSignException>(() => editor.Sign(
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
        using var editor = document.Edit();

        Assert.Throws<PdfSignException>(() => editor.Sign(
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
        using var editor = document.Edit();

        Assert.Throws<PdfFieldReadOnlyException>(() => editor.Sign(
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
            using var editor = document.Edit();
            editor.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider());
            editor.Sign(
                stream,
                new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 4096 },
                new DocTimeStampSignatureProvider(new RecordingAuthority()));
        }

        byte[] saved = stream.ToArray();
        string text = Encoding.ASCII.GetString(saved);
        Assert.Contains("/Type /DocTimeStamp", text, StringComparison.Ordinal);
        Assert.Contains("/SubFilter /ETSI.RFC3161", text, StringComparison.Ordinal);
        Assert.Equal(3, StartXrefs(saved).Count);
    }

    [Fact]
    public void mismatched_timestamp_imprint_does_not_extend_the_file()
    {
        AssertTimestampRejected(TimestampFault.Imprint, "message imprint");
    }

    [Fact]
    public void mismatched_timestamp_digest_algorithm_does_not_extend_the_file()
    {
        AssertTimestampRejected(TimestampFault.Algorithm, "digest algorithm");
    }

    [Fact]
    public void missing_or_mismatched_timestamp_nonce_does_not_extend_the_file()
    {
        AssertTimestampRejected(TimestampFault.MissingNonce, "nonce");
        AssertTimestampRejected(TimestampFault.MismatchedNonce, "nonce");
    }

    [Fact]
    public void timestamp_token_without_its_signing_certificate_does_not_extend_the_file()
    {
        AssertTimestampRejected(TimestampFault.MissingCertificate, "does not include the signing certificate");
        AssertTimestampRejected(TimestampFault.OtherCertificate, "does not include the signing certificate");
        AssertTimestampRejected(TimestampFault.IssuerSerialMismatch, "does not include the signing certificate");
        AssertTimestampRejected(TimestampFault.MissingSigningCertificateAttribute, "no ESS signing-certificate attribute");
    }

    [Fact]
    public void timestamp_token_with_its_signing_certificate_is_embedded_by_both_providers()
    {
        byte[] pdf = TextField();
        foreach (EssForm form in new[] { EssForm.V2DefaultHashWithIssuerSerial, EssForm.V2Sha512WithoutIssuerSerial, EssForm.V1Sha1WithIssuerSerial })
        {
            var documentAuthority = new ChainAuthority(form, TimestampCertificates.Tsa, Array.Empty<byte[]>());
            byte[] stamped = SignWithTimestamp(pdf, documentAuthority, true, new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 4096 }, null);
            Assert.Contains(TestCompat.ToHexString(documentAuthority.Token), Encoding.ASCII.GetString(stamped), StringComparison.Ordinal);
            Assert.Contains(TestCompat.ToHexString(TimestampCertificates.Tsa), TestCompat.ToHexString(documentAuthority.Token), StringComparison.Ordinal);

            var signatureAuthority = new ChainAuthority(form, TimestampCertificates.Tsa, Array.Empty<byte[]>());
            byte[] signed = SignWithTimestamp(pdf, signatureAuthority, false, new PdfSignatureOptions { ReservedContentsBytes = 8192, SigningTime = TestCompat.UnixEpoch }, null);
            Assert.Contains(TestCompat.ToHexString(signatureAuthority.Token), Encoding.ASCII.GetString(signed), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ambiguous_or_malformed_ess_identifiers_do_not_extend_the_file()
    {
        AssertTimestampRejected(TimestampFault.MultipleSignerInfos, "not a time-stamp token");
        AssertTimestampRejected(TimestampFault.MultipleSigningCertificateAttributes, "multiple ESS signing-certificate attributes");
        AssertTimestampRejected(TimestampFault.FirstCertificateIdentifierMismatch, "does not include the signing certificate");
        AssertTimestampRejected(TimestampFault.MalformedIssuerSerial, "not a time-stamp token");
        AssertTimestampRejected(TimestampFault.UnsupportedEssHash, "unsupported hash algorithm");
        AssertTimestampRejected(TimestampFault.TrailingTokenData, "not a time-stamp token");
        AssertTimestampRejected(TimestampFault.NonMinimalDerLength, "not a time-stamp token");
    }

    [Fact]
    public void default_reservation_fits_a_timestamp_token_that_carries_a_certificate_chain()
    {
        byte[] pdf = TextField();
        int reserved = new PdfSignatureOptions().ReservedContentsBytes;
        TimestampChain chain = TimestampCertificates.Chain;
        byte[][] tsaChain = new[] { chain.Intermediate, chain.Root };
        Assert.InRange(chain.Tsa.Length + chain.Intermediate.Length + chain.Root.Length, 3072, 6144);

        var documentAuthority = new ChainAuthority(EssForm.V2DefaultHashWithIssuerSerial, chain.Tsa, tsaChain);
        byte[] stamped = SignWithTimestamp(pdf, documentAuthority, true, new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp }, null);
        Assert.Contains(TestCompat.ToHexString(documentAuthority.Token), Encoding.ASCII.GetString(stamped), StringComparison.Ordinal);
        Assert.InRange(documentAuthority.Token.Length, 3072, reserved / 2);

        var signatureAuthority = new ChainAuthority(EssForm.V2DefaultHashWithIssuerSerial, chain.Tsa, tsaChain);
        byte[] signed = SignWithTimestamp(pdf, signatureAuthority, false, new PdfSignatureOptions { SigningTime = TestCompat.UnixEpoch }, chain);
        Match contents = Regex.Match(Encoding.ASCII.GetString(signed), @"/Contents\s*<([0-9A-Fa-f]+)>");
        int cmsLength = DerCodec.ReadTlv(TestCompat.FromHexString(contents.Groups[1].Value)).Full.Length;
        Assert.InRange(cmsLength, 6144, reserved * 3 / 4);
    }

    [Fact]
    public void undersized_reservation_for_a_certificate_bearing_timestamp_fails_without_extending_the_file()
    {
        byte[] pdf = TextField();
        foreach (bool documentTimestamp in new[] { true, false })
        {
            using var stream = new MemoryStream();
            stream.Write(pdf, 0, pdf.Length);
            using var document = PdfDocument.Open(stream, leaveOpen: true);
            using var editor = document.Edit();
            var authority = new ChainAuthority(EssForm.V2DefaultHashWithIssuerSerial, TimestampCertificates.Tsa, Array.Empty<byte[]>());

            // The CMS reservation fits the leaf and the 256-byte signature value, so only the token overflows it.
            int reserved = documentTimestamp ? 1024 : TimestampCertificates.Signer.Signer.Length + 256 + 64;
            PdfSignatureOptions options = documentTimestamp
                ? new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = reserved }
                : new PdfSignatureOptions { ReservedContentsBytes = reserved, SigningTime = TestCompat.UnixEpoch };

            PdfSignatureTooLargeException tooLarge = Assert.Throws<PdfSignatureTooLargeException>(() => editor.Sign(stream, options, TimestampProvider(authority, documentTimestamp, null)));

            Assert.Equal(reserved, tooLarge.ReservedBytes);
            Assert.True(tooLarge.ActualBytes > reserved);
            Assert.True(authority.Token.Length > 0);
            Assert.Equal(pdf, stream.ToArray());
        }
    }

    [Fact]
    public void external_signer_failure_does_not_extend_the_file()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Pdf Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        long length = stream.Length;
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<CryptographicException>(() => editor.Sign(
            stream,
            new PdfSignatureOptions { ReservedContentsBytes = 8192, SigningTime = TestCompat.UnixEpoch },
            new CmsDetachedSignatureProvider(new ThrowingSigner(certificate.RawData))));

        Assert.Equal(length, stream.Length);
        Assert.Equal(pdf, stream.ToArray());
    }

    [Fact]
    public void failed_sign_diagnostic_omits_the_password_and_file_key()
    {
        const string password = "user-secret-k2p";
        const string owner = "owner-secret-k2p";
        byte[] fileKey = new byte[32];
        for (int i = 0; i < fileKey.Length; i++)
        {
            fileKey[i] = (byte)(i + 1);
        }

        byte[] pdf = EncryptedPage(password, owner);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Pdf Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, password, leaveOpen: true);
        using var editor = document.Edit();
        CryptographicException exception = Assert.Throws<CryptographicException>(() => editor.Sign(
            stream,
            new PdfSignatureOptions { ReservedContentsBytes = 8192, SigningTime = TestCompat.UnixEpoch },
            new CmsDetachedSignatureProvider(new ThrowingSigner(certificate.RawData))));

        AssertDiagnosticOmits(exception, password, "password");
        AssertDiagnosticOmits(exception, owner, "password");
        AssertDiagnosticOmitsKey(exception, fileKey);
    }

    [Fact]
    public void external_signer_diagnostic_is_sanitized_and_does_not_extend_the_file()
    {
        const string sentinel = "sentinel-pin-value";
        byte[] sentinelKey = Encoding.ASCII.GetBytes("sentinel-key-material-32-bytes!!");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Pdf Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        CryptographicException exception = Assert.Throws<CryptographicException>(() => editor.Sign(
            stream,
            new PdfSignatureOptions { ReservedContentsBytes = 8192, SigningTime = TestCompat.UnixEpoch },
            new CmsDetachedSignatureProvider(new LeakySigner(certificate.RawData, sentinel, sentinelKey))));

        Assert.Equal("The external signer failed.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Empty(exception.Data);
        AssertDiagnosticOmits(exception, sentinel, "PIN sentinel");
        AssertDiagnosticOmitsKey(exception, sentinelKey);
        Assert.Equal(pdf, stream.ToArray());
    }

    [Fact]
    public void timestamp_authority_failure_does_not_leave_a_timestamped_signature()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Pdf Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        long length = stream.Length;
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfTimestampException>(() => editor.Sign(
            stream,
            new PdfSignatureOptions { ReservedContentsBytes = 16384, SigningTime = TestCompat.UnixEpoch },
            new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData), new ThrowingAuthority())));

        Assert.Equal(length, stream.Length);
        Assert.Equal(pdf, stream.ToArray());
        using var again = PdfDocument.Open(new MemoryStream(stream.ToArray()), leaveOpen: true);
        Assert.DoesNotContain(again.AcroForm.Fields, field => field.FieldType == PdfFieldType.Signature);
    }

    [Fact]
    public void signer_certificate_getter_failure_is_sanitized_and_does_not_extend_the_file()
    {
        const string secret = "sentinel-certificate-pin";
        var signers = new List<HostileSigner>();
        AssertSignerFailureIsSanitized(secret, () => Track(signers, Hostile(certificate: () => throw new InvalidOperationException(secret))));
        Assert.All(signers, signer => Assert.Equal(0, signer.SignCalls));
    }

    [Fact]
    public void signer_chain_getter_failure_is_sanitized_and_does_not_extend_the_file()
    {
        const string secret = "sentinel-chain-pin";
        var signers = new List<HostileSigner>();
        AssertSignerFailureIsSanitized(secret, () => Track(signers, Hostile(additional: () => throw new InvalidOperationException(secret))));
        Assert.All(signers, signer => Assert.Equal(0, signer.SignCalls));
    }

    [Fact]
    public void signer_chain_enumeration_failure_is_sanitized_and_does_not_extend_the_file()
    {
        const string secret = "sentinel-enumeration-pin";
        var chains = new List<LazyCertificates>();
        var signers = new List<HostileSigner>();
        AssertSignerFailureIsSanitized(secret, () =>
        {
            var chain = new LazyCertificates(TimestampCertificates.Signer.Intermediate, 2, secret);
            chains.Add(chain);
            return Track(signers, Hostile(additional: () => chain));
        });

        Assert.All(chains, chain => Assert.Equal(3, chain.MoveNextCalls));
        Assert.All(signers, signer => Assert.Equal(0, signer.SignCalls));
    }

    [Fact]
    public void empty_signer_outputs_throw_sign_exception_and_do_not_extend_the_file()
    {
        PdfSignatureOptions options = SignerBoundOptions(8192);
        Assert.Contains("certificate is empty", AssertSignFailsWithoutWriting<PdfSignException>(options, () => new CmsDetachedSignatureProvider(Hostile(certificate: () => Array.Empty<byte>()))).Message, StringComparison.Ordinal);
        Assert.Contains("additional certificate is empty", AssertSignFailsWithoutWriting<PdfSignException>(options, () => new CmsDetachedSignatureProvider(Hostile(additional: () => new[] { Array.Empty<byte>() }))).Message, StringComparison.Ordinal);
        Assert.Contains("empty signature", AssertSignFailsWithoutWriting<PdfSignException>(options, () => new CmsDetachedSignatureProvider(Hostile(sign: hash => Array.Empty<byte>()))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void successful_sign_reads_the_leaf_once_and_enumerates_the_chain_once()
    {
        TimestampChain chain = TimestampCertificates.Signer;
        var additional = new CountingCertificates(chain.Intermediate, chain.Root);
        HostileSigner signer = Hostile(additional: () => additional);
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Sign(stream, SignerBoundOptions(8192), new CmsDetachedSignatureProvider(signer));
        }

        Assert.True(stream.Length > pdf.Length);
        Assert.Equal(1, signer.CertificateReads);
        Assert.Equal(1, signer.AdditionalReads);
        Assert.Equal(1, additional.GetEnumeratorCalls);
        Assert.Equal(1, signer.SignCalls);
    }

    [Fact]
    public void leaf_certificate_that_changes_after_the_first_read_is_not_read_again()
    {
        const int reserved = 8192;
        byte[] leaf = TimestampCertificates.Signer.Signer;
        int reads = 0;

        // A second read would return zeros twice the reserved size, which is neither a certificate nor in bounds.
        HostileSigner signer = Hostile(certificate: () => ++reads == 1 ? leaf : new byte[reserved * 2]);
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Sign(stream, SignerBoundOptions(reserved), new CmsDetachedSignatureProvider(signer));
        }

        Assert.Equal(1, reads);
        Assert.True(Encoding.ASCII.GetString(stream.ToArray()).IndexOf(TestCompat.ToHexString(leaf), StringComparison.OrdinalIgnoreCase) > 0, "The first leaf was not embedded.");
    }

    [Fact]
    public void malformed_signer_certificate_throws_sign_exception_and_does_not_extend_the_file()
    {
        byte[] leaf = TimestampCertificates.Signer.Signer;
        var trailing = new byte[leaf.Length + 1];
        Buffer.BlockCopy(leaf, 0, trailing, 0, leaf.Length);
        var malformed = new[]
        {
            new byte[] { 0x30, 0x05 },
            new byte[] { 0x04, 0x00 },
            new byte[] { 0x30, 0x03, 0x02, 0x01, 0x01 },
            new byte[] { 0x30, 0x08, 0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0x01 },
            new byte[] { 0x30, 0x09, 0x30, 0x07, 0x02, 0x01, 0x01, 0x30, 0x00, 0x04, 0x00 },
            trailing,
        };

        foreach (byte[] certificate in malformed)
        {
            var signers = new List<HostileSigner>();
            PdfSignException rejected = AssertSignFailsWithoutWriting<PdfSignException>(
                SignerBoundOptions(8192),
                () => new CmsDetachedSignatureProvider(Track(signers, Hostile(certificate: () => certificate))));

            Assert.Equal("The signer certificate is not a DER-encoded certificate.", rejected.Message);
            Assert.All(signers, signer => Assert.Equal(0, signer.SignCalls));
        }
    }

    // W-71: certificates past the largest reservation can never fit, so they are refused before the leaf is parsed. Zero bytes
    // are not a certificate, so reaching the parser would say so instead.
    [Fact]
    public void certificates_past_the_largest_reservation_are_refused_before_they_are_parsed_or_signed()
    {
        var signers = new List<HostileSigner>();
        PdfSignException refused = AssertSignFailsWithoutWriting<PdfSignException>(
            SignerBoundOptions(8192),
            () => new CmsDetachedSignatureProvider(Track(signers, Hostile(certificate: () => new byte[(1024 * 1024) + 1]))));

        Assert.Equal("The signer certificates exceed 1048576 bytes, the largest signature that can be reserved.", refused.Message);
        Assert.All(signers, signer => Assert.Equal(0, signer.SignCalls));
    }

    [Fact]
    public void endless_signer_chain_stops_enumerating_at_the_largest_reservation()
    {
        const int certificateBytes = 4096;
        byte[] leaf = TimestampCertificates.Signer.Signer;
        int expectedCalls = (((1024 * 1024) - leaf.Length) / certificateBytes) + 1;
        var chains = new List<LazyCertificates>();
        var signers = new List<HostileSigner>();
        AssertSignFailsWithoutWriting<PdfSignException>(
            SignerBoundOptions(8192),
            () =>
            {
                var chain = new LazyCertificates(new byte[certificateBytes], -1, string.Empty);
                chains.Add(chain);
                return new CmsDetachedSignatureProvider(Track(signers, Hostile(additional: () => chain)));
            });

        Assert.Equal(2, chains.Count);
        Assert.All(chains, chain => Assert.Equal(expectedCalls, chain.MoveNextCalls));
        Assert.All(signers, signer => Assert.Equal(0, signer.SignCalls));
    }

    // W-71: ActualBytes is the exact CMS length, like pyHanko's "contents required N bytes": reserving exactly that many bytes
    // signs with no padding, and one byte fewer reports the same length again. The chain and the signature value are embedded
    // verbatim, so zero bytes stand in for them, and the RSA test key signs deterministically.
    [Theory]
    [InlineData("chain")]
    [InlineData("signature")]
    public void overflow_reports_the_exact_cms_length_and_that_reservation_signs(string part)
    {
        const int reserved = 8192;
        var signers = new List<HostileSigner>();
        Func<HostileSigner> create = part == "chain"
            ? () => Track(signers, Hostile(additional: () => new[] { new byte[reserved / 2], new byte[reserved / 2] }))
            : () => Track(signers, Hostile(sign: hash => new byte[reserved]));

        PdfSignatureTooLargeException tooLarge = AssertSignFailsWithoutWriting<PdfSignatureTooLargeException>(
            SignerBoundOptions(reserved),
            () => new CmsDetachedSignatureProvider(create()));

        Assert.True(tooLarge.ActualBytes > reserved, tooLarge.Message);
        Assert.Equal(reserved, tooLarge.ReservedBytes);
        Assert.All(signers, signer => Assert.Equal(1, signer.SignCalls));
        Assert.Equal(tooLarge.ActualBytes, AssertSignFailsWithoutWriting<PdfSignatureTooLargeException>(SignerBoundOptions(tooLarge.ActualBytes - 1), () => new CmsDetachedSignatureProvider(create())).ActualBytes);

        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Sign(stream, SignerBoundOptions(tooLarge.ActualBytes), new CmsDetachedSignatureProvider(create()));
        }

        Match contents = Regex.Match(Encoding.ASCII.GetString(stream.ToArray()), @"/Contents\s*<([0-9A-Fa-f]+)>");
        byte[] gap = TestCompat.FromHexString(contents.Groups[1].Value);
        Assert.Equal(tooLarge.ActualBytes, gap.Length);
        Assert.Equal(gap.Length, DerCodec.ReadTlv(gap).Full.Length);
    }

    // W-71: an oversize token is wrapped verbatim to measure the CMS, and parsed only once it fits. At the reported size the
    // zero-byte token fits, and only then does the parser reject it.
    [Fact]
    public void oversize_signature_timestamp_token_reports_the_exact_length_before_it_is_parsed()
    {
        const int reserved = 8192;
        var authorities = new List<OversizeAuthority>();
        Func<IPdfSignatureProvider> create = () =>
        {
            var authority = new OversizeAuthority(reserved + 1);
            authorities.Add(authority);
            return new CmsDetachedSignatureProvider(Hostile(), authority);
        };

        PdfSignatureTooLargeException tooLarge = AssertSignFailsWithoutWriting<PdfSignatureTooLargeException>(SignerBoundOptions(reserved), create);

        Assert.True(tooLarge.ActualBytes > reserved + 1);
        Assert.Equal(reserved, tooLarge.ReservedBytes);
        Assert.All(authorities, authority => Assert.Equal(1, authority.Calls));
        Assert.Equal(tooLarge.ActualBytes, AssertSignFailsWithoutWriting<PdfSignatureTooLargeException>(SignerBoundOptions(tooLarge.ActualBytes - 1), create).ActualBytes);
        AssertSignFailsWithoutWriting<PdfTimestampException>(SignerBoundOptions(tooLarge.ActualBytes), create);
    }

    [Fact]
    public void oversize_document_timestamp_token_is_refused_before_it_is_parsed()
    {
        const int reserved = 8192;
        var authorities = new List<OversizeAuthority>();
        PdfSignatureTooLargeException tooLarge = AssertSignFailsWithoutWriting<PdfSignatureTooLargeException>(
            new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = reserved },
            () =>
            {
                var authority = new OversizeAuthority(reserved + 1);
                authorities.Add(authority);
                return new DocTimeStampSignatureProvider(authority);
            });

        Assert.Equal(reserved + 1, tooLarge.ActualBytes);
        Assert.Equal(reserved, tooLarge.ReservedBytes);
        Assert.All(authorities, authority => Assert.Equal(1, authority.Calls));
    }

    [Fact]
    public void reused_field_whose_dictionary_exceeds_8_mib_is_refused_without_writing_and_the_document_can_still_be_signed()
    {
        // Audit SA-07: the estimate runs before BuildSignature dirties the reused field, so only
        // the bounded appendix buffer sees the 9 MiB literal that SealExistingField rewrites.
        byte[] pdf = FilledSignatureField(9 * 1024 * 1024);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            PdfSignException same = Assert.Throws<PdfSignException>(() => editor.Sign(stream, AppendixOptions("SignHere"), new FixedProvider()));
            Assert.Equal("The signature appendix would exceed 8 MiB. Call SaveIncremental before Sign.", same.Message);
            AssertPrefix(pdf, stream.ToArray(), pdf.Length);

            using (var destination = new MemoryStream())
            {
                PdfSignException cross = Assert.Throws<PdfSignException>(() => editor.Sign(destination, AppendixOptions("SignHere"), new FixedProvider(), leaveOpen: true));
                Assert.Equal(same.Message, cross.Message);
                Assert.Equal(0, destination.Length);
                AssertPrefix(pdf, stream.ToArray(), pdf.Length);
            }

            PdfSignException again = Assert.Throws<PdfSignException>(() => editor.Sign(stream, AppendixOptions("SignHere"), new FixedProvider()));
            Assert.Equal(same.Message, again.Message);
            AssertPrefix(pdf, stream.ToArray(), pdf.Length);

            editor.Sign(stream, AppendixOptions("Other"), new FixedProvider());
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.Length - pdf.Length < 64 * 1024, "appendix " + (saved.Length - pdf.Length).ToString());
        AssertPrefix(pdf, saved, saved.Length);
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.DoesNotContain("SignHere", appendix, StringComparison.Ordinal);
        Assert.Contains("(Other)", appendix, StringComparison.Ordinal);
        using var reopened = PdfDocument.Open(new MemoryStream(saved), leaveOpen: true);
        Assert.Equal(new[] { "Other", "SignHere" }, reopened.AcroForm.Fields.Select(field => field.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void signature_appendix_of_exactly_8_mib_is_written_and_one_byte_more_is_refused()
    {
        const int cap = 8 * 1024 * 1024;
        const int probe = 8000000;
        byte[] probed = FilledSignatureField(probe);
        long overhead;
        using (var source = new MemoryStream(probed, false))
        using (var document = PdfDocument.Open(source, leaveOpen: true))
        using (var destination = new MemoryStream())
        {
            using var editor = document.Edit();
            editor.Sign(destination, AppendixOptions("SignHere"), new FixedProvider(), leaveOpen: true);
            overhead = destination.Length - probed.Length - probe;
        }

        byte[] fits = FilledSignatureField(checked((int)(cap - overhead)));
        using (var stream = new MemoryStream())
        {
            stream.Write(fits, 0, fits.Length);
            using (var document = PdfDocument.Open(stream, leaveOpen: true))
            {
                using var editor = document.Edit();
                editor.Sign(stream, AppendixOptions("SignHere"), new FixedProvider());
            }

            AssertPrefix(fits, stream.ToArray(), fits.Length + (long)cap);
        }

        byte[] over = FilledSignatureField(checked((int)(cap - overhead + 1)));
        using (var stream = new MemoryStream())
        {
            stream.Write(over, 0, over.Length);
            using var document = PdfDocument.Open(stream, leaveOpen: true);
            using var editor = document.Edit();
            PdfSignException same = Assert.Throws<PdfSignException>(() => editor.Sign(stream, AppendixOptions("SignHere"), new FixedProvider()));
            Assert.Equal("The signature appendix would exceed 8 MiB. Call SaveIncremental before Sign.", same.Message);
            AssertPrefix(over, stream.ToArray(), over.Length);
            using var destination = new MemoryStream();
            Assert.Throws<PdfSignException>(() => editor.Sign(destination, AppendixOptions("SignHere"), new FixedProvider(), leaveOpen: true));
            Assert.Equal(0, destination.Length);
            AssertPrefix(over, stream.ToArray(), over.Length);
        }
    }

    [Fact]
    public void attachment_appendix_of_exactly_8_mib_is_signed_and_one_byte_more_is_refused()
    {
        // QA-B37-01: the early estimate must not refuse an appendix whose bulk is a dirty stream body.
        const int cap = 8 * 1024 * 1024;
        const int probe = 8000000;
        byte[] pdf = PdfBuilder.Basic().Finish();
        long overhead = SignWithAttachment(probe, same: false).Length - pdf.Length - probe;

        int fits = checked((int)(cap - overhead));
        byte[] signed = SignWithAttachment(fits, same: true);
        AssertPrefix(pdf, signed, pdf.Length + (long)cap);

        var content = new byte[fits + 1];
        using var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        editor.Attachments.Add("big.bin", new MemoryStream(content), leaveOpen: false);
        PdfSignException same = Assert.Throws<PdfSignException>(() => editor.Sign(source, AppendixOptions("Signature1"), new FixedProvider()));
        Assert.Equal("The signature appendix would exceed 8 MiB. Call SaveIncremental before Sign.", same.Message);
        AssertPrefix(pdf, source.ToArray(), pdf.Length);
        using var destination = new MemoryStream();
        Assert.Throws<PdfSignException>(() => editor.Sign(destination, AppendixOptions("Signature1"), new FixedProvider(), leaveOpen: true));
        Assert.Equal(0, destination.Length);
        AssertPrefix(pdf, source.ToArray(), pdf.Length);
    }

    [Fact]
    public void appendix_buffer_holds_exactly_8_mib_and_never_allocates_past_it()
    {
        const int cap = 8 * 1024 * 1024;
        var chunk = new byte[1000];
        using var buffer = new PdfAppendixBuffer();
        while (buffer.Length + chunk.Length <= cap)
        {
            buffer.Write(chunk, 0, chunk.Length);
            Assert.True(buffer.Capacity <= cap, "capacity " + buffer.Capacity.ToString());
        }

        int rest = (int)(cap - buffer.Length);
        buffer.Write(chunk, 0, rest);
        Assert.Equal(cap, buffer.Length);
        Assert.Equal(cap, buffer.Capacity);

        Assert.Throws<PdfSignException>(() => buffer.WriteByte(0));
        Assert.Throws<PdfSignException>(() => buffer.Write(chunk, 0, 1));
#if NET6_0_OR_GREATER
        Assert.Throws<PdfSignException>(() => buffer.Write(new ReadOnlySpan<byte>(chunk, 0, 1)));
#endif
        Assert.Throws<PdfSignException>(() => buffer.SetLength(cap + 1L));
        Assert.Equal(cap, buffer.Length);
        Assert.Equal(cap, buffer.Capacity);

        using var large = new PdfAppendixBuffer();
        Assert.Throws<PdfSignException>(() => large.Write(new byte[cap + 1], 0, cap + 1));
        Assert.Equal(0, large.Length);
        Assert.True(large.Capacity <= 256, "capacity " + large.Capacity.ToString());

        buffer.Position = 10;
        buffer.Write(chunk, 0, chunk.Length);
        Assert.Equal(cap, buffer.Length);
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

    private static void AssertTimestampRejected(TimestampFault fault, string phrase)
    {
        byte[] pdf = TextField();
        RejectTimestamp(pdf, fault, phrase, documentTimestamp: true);
        RejectTimestamp(pdf, fault, phrase, documentTimestamp: false);
    }

    private static void RejectTimestamp(byte[] pdf, TimestampFault fault, string phrase, bool documentTimestamp)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        long length = stream.Length;
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();
        PdfSignatureOptions options;
        IPdfSignatureProvider provider;
        if (documentTimestamp)
        {
            options = new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 8192 };
            provider = new DocTimeStampSignatureProvider(new FaultAuthority(fault));
            PdfTimestampException rejected = Assert.Throws<PdfTimestampException>(() => editor.Sign(stream, options, provider));
            Assert.Contains(phrase, rejected.Message, StringComparison.Ordinal);
            Assert.Equal(length, stream.Length);
            return;
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Pdf Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        options = new PdfSignatureOptions { ReservedContentsBytes = 8192, SigningTime = TestCompat.UnixEpoch };
        provider = new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData), new FaultAuthority(fault));
        PdfTimestampException exception = Assert.Throws<PdfTimestampException>(() => editor.Sign(stream, options, provider));
        Assert.Contains(phrase, exception.Message, StringComparison.Ordinal);
        Assert.Equal(length, stream.Length);
    }

    private static PdfSignatureOptions SignerBoundOptions(int reserved)
    {
        return new PdfSignatureOptions { ReservedContentsBytes = reserved, SigningTime = TestCompat.UnixEpoch };
    }

    // A signer over the shared 2048-bit test chain. Each argument replaces one well-behaved member.
    private static HostileSigner Hostile(Func<byte[]>? certificate = null, Func<IReadOnlyList<byte[]>>? additional = null, Func<byte[], byte[]>? sign = null)
    {
        TimestampChain chain = TimestampCertificates.Signer;
        return new HostileSigner(
            certificate ?? (() => chain.Signer),
            additional ?? (() => Array.Empty<byte[]>()),
            sign ?? (hash => chain.SignerKey.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)));
    }

    private static HostileSigner Track(List<HostileSigner> signers, HostileSigner signer)
    {
        signers.Add(signer);
        return signer;
    }

    private static void AssertSignerFailureIsSanitized(string secret, Func<HostileSigner> createSigner)
    {
        CryptographicException exception = AssertSignFailsWithoutWriting<CryptographicException>(SignerBoundOptions(8192), () => new CmsDetachedSignatureProvider(createSigner()));
        Assert.Equal("The external signer failed.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Empty(exception.Data);
        AssertDiagnosticOmits(exception, secret, "signer secret");
    }

    // Signs once into the source stream and once into an empty cross-stream destination.
    // Both must throw the same exception type and leave their destination unchanged.
    private static TException AssertSignFailsWithoutWriting<TException>(PdfSignatureOptions options, Func<IPdfSignatureProvider> createProvider)
        where TException : Exception
    {
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        TException same = Assert.Throws<TException>(() => editor.Sign(stream, options, createProvider()));
        Assert.Equal(pdf, stream.ToArray());

        using var destination = new MemoryStream();
        TException cross = Assert.Throws<TException>(() => editor.Sign(destination, options, createProvider(), leaveOpen: true));
        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, stream.ToArray());
        Assert.Equal(same.Message, cross.Message);
        return cross;
    }

    private static byte[] SignWithTimestamp(byte[] pdf, ITimestampAuthority authority, bool documentTimestamp, PdfSignatureOptions options, TimestampChain? signerChain)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Sign(stream, options, TimestampProvider(authority, documentTimestamp, signerChain));
        }

        return stream.ToArray();
    }

    private static IPdfSignatureProvider TimestampProvider(ITimestampAuthority authority, bool documentTimestamp, TimestampChain? signerChain)
    {
        if (documentTimestamp)
        {
            return new DocTimeStampSignatureProvider(authority);
        }

        TimestampChain chain = signerChain ?? TimestampCertificates.Signer;
        IReadOnlyList<byte[]> additional = signerChain == null ? Array.Empty<byte[]>() : new[] { chain.Intermediate, chain.Root };
        return new CmsDetachedSignatureProvider(new RsaSigner(chain.SignerKey, chain.Signer, additional), authority);
    }

    private static void AssertNonceWidth(byte[] nonce)
    {
        Assert.InRange(nonce.Length, 1, 9);
        if (nonce[0] == 0)
        {
            Assert.True(nonce.Length == 1 || (nonce[1] & 0x80) != 0);
        }
        else
        {
            Assert.Equal(0, nonce[0] & 0x80);
        }
    }

    private static void AssertTimestampResponseDoesNotExtend(byte[] response, string phrase)
    {
        AssertTimestampResponseDoesNotExtend(new RecordingHandler(response), phrase);
    }

    private static void AssertTimestampResponseDoesNotExtend(HttpMessageHandler handler, string phrase)
    {
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();
        var authority = new HttpTimestampAuthority(new HttpClient(handler), new Uri("https://tsa.example/stamp"));

        PdfTimestampException rejected = Assert.Throws<PdfTimestampException>(() => editor.Sign(
            stream,
            new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 1024 },
            new DocTimeStampSignatureProvider(authority)));

        Assert.Contains(phrase, rejected.Message, StringComparison.Ordinal);
        Assert.Equal(pdf, stream.ToArray());
    }

    private static (byte[] Response, byte[] Token) PaddedTimestampResponse(int length)
    {
        int padding = length - 64;
        (byte[] response, byte[] token) = BuildPaddedTimestampResponse(padding);
        (response, token) = BuildPaddedTimestampResponse(padding + length - response.Length);
        Assert.Equal(length, response.Length);
        return (response, token);
    }

    private static (byte[] Response, byte[] Token) BuildPaddedTimestampResponse(int padding)
    {
        byte[] token = DerCodec.Sequence(DerCodec.Integer(7), DerCodec.OctetString(new byte[padding]));
        return (DerCodec.Sequence(DerCodec.Sequence(DerCodec.Integer(0)), token), token);
    }

    private static (byte[] Imprint, byte[] Nonce) ReadTimestampRequest(byte[] request)
    {
        DerCodec.DerTlv sequence = DerCodec.ReadTlv(request);
        int pos = 0;
        Take(sequence.Content, ref pos);
        DerCodec.DerTlv messageImprint = Take(sequence.Content, ref pos);
        int imprintPos = 0;
        Take(messageImprint.Content, ref imprintPos);
        DerCodec.DerTlv hashed = Take(messageImprint.Content, ref imprintPos);
        DerCodec.DerTlv nonce = Take(sequence.Content, ref pos);
        DerCodec.DerTlv certReq = Take(sequence.Content, ref pos);
        Assert.Equal(sequence.Content.Length, pos);
        Assert.Equal(0x04, hashed.Tag);
        Assert.Equal(0x02, nonce.Tag);
        Assert.Equal(new byte[] { 0x01, 0x01, 0xFF }, certReq.Full);
        return (hashed.Content, nonce.Content);
    }

    private static DerCodec.DerTlv Take(ReadOnlySpan<byte> data, ref int offset)
    {
        DerCodec.DerTlv tlv = DerCodec.ReadTlv(data, offset);
        offset += tlv.Full.Length;
        return tlv;
    }

    private static byte[] BuildTimestampToken(byte[] imprint, byte[] nonce, TimestampFault fault)
    {
        return BuildTimestampToken(imprint, nonce, fault, EssForm.V2DefaultHashWithIssuerSerial, TimestampCertificates.Tsa, Array.Empty<byte[]>());
    }

    private static byte[] BuildTimestampToken(byte[] imprint, byte[] nonce, TimestampFault fault, EssForm form, byte[] tsa, IReadOnlyList<byte[]> chain)
    {
        byte[] hashed = imprint;
        if (fault == TimestampFault.Imprint)
        {
            hashed = new byte[imprint.Length];
            for (int i = 0; i < hashed.Length; i++)
            {
                hashed[i] = 0x11;
            }
        }

        byte[] algorithm = fault == TimestampFault.Algorithm
            ? DerCodec.Sequence(DerCodec.Oid("1.3.14.3.2.26"), DerCodec.Null())
            : DerCodec.AlgorithmSha256();
        byte[] echoed = nonce;
        if (fault == TimestampFault.MismatchedNonce)
        {
            echoed = (byte[])nonce.Clone();
            echoed[echoed.Length - 1] ^= 0xFF;
        }

        var parts = new List<byte[]>
        {
            DerCodec.Integer(1),
            DerCodec.Oid("1.2.3.4"),
            DerCodec.Sequence(algorithm, DerCodec.OctetString(hashed)),
            DerCodec.Integer(0x01020304),
            GeneralizedTime(),
        };
        if (fault != TimestampFault.MissingNonce)
        {
            parts.Add(DerCodec.Integer(echoed));
        }

        byte[] tstInfo = DerCodec.Sequence(parts.ToArray());
        byte[] encapsulated = DerCodec.Sequence(
            DerCodec.Oid("1.2.840.113549.1.9.16.1.4"),
            DerCodec.ExplicitContext(0, DerCodec.OctetString(tstInfo)));
        var certificates = new List<byte[]>();
        if (fault == TimestampFault.OtherCertificate)
        {
            certificates.Add(TimestampCertificates.Other);
        }
        else if (fault != TimestampFault.MissingCertificate)
        {
            certificates.Add(tsa);
        }

        certificates.AddRange(chain);
        var signedDataParts = new List<byte[]>
        {
            DerCodec.Integer(3),
            DerCodec.Set(DerCodec.AlgorithmSha256()),
            encapsulated,
        };
        if (certificates.Count > 0)
        {
            signedDataParts.Add(DerCodec.ReplaceTag(DerCodec.Set(certificates.ToArray()), 0xA0));
        }

        byte[] signerInfo = TimestampSignerInfo(tstInfo, tsa, fault, form);
        signedDataParts.Add(fault == TimestampFault.MultipleSignerInfos
            ? DerCodec.Set(signerInfo, signerInfo)
            : DerCodec.Set(signerInfo));
        byte[] token = DerCodec.Sequence(
            DerCodec.Oid("1.2.840.113549.1.7.2"),
            DerCodec.ExplicitContext(0, DerCodec.Sequence(signedDataParts.ToArray())));
        if (fault != TimestampFault.TrailingTokenData && fault != TimestampFault.NonMinimalDerLength)
        {
            return token;
        }

        if (fault == TimestampFault.NonMinimalDerLength)
        {
            Assert.True((token[1] & 0x80) != 0);
            var nonMinimal = new byte[token.Length + 1];
            nonMinimal[0] = token[0];
            nonMinimal[1] = (byte)(token[1] + 1);
            Buffer.BlockCopy(token, 2, nonMinimal, 3, token.Length - 2);
            return nonMinimal;
        }

        var trailing = new byte[token.Length + 1];
        Buffer.BlockCopy(token, 0, trailing, 0, token.Length);
        return trailing;
    }

    // The SignerInfo is shaped like a TSA's, but its signature is not real: the library does not verify it.
    private static byte[] TimestampSignerInfo(byte[] tstInfo, byte[] tsa, TimestampFault fault, EssForm form)
    {
        (byte[] issuer, byte[] serial) = DerCodec.ReadCertificateIdentity(tsa);
        var attributes = new List<byte[]>
        {
            DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.9.3"), DerCodec.Set(DerCodec.Oid("1.2.840.113549.1.9.16.1.4"))),
            DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.9.4"), DerCodec.Set(DerCodec.OctetString(TestCompat.Sha256(tstInfo)))),
        };
        if (fault != TimestampFault.MissingSigningCertificateAttribute)
        {
            byte[] essSerial = fault == TimestampFault.IssuerSerialMismatch ? DerCodec.Integer(0x7FFFFFFF) : serial;
            byte[] issuerSerial = fault == TimestampFault.MalformedIssuerSerial
                ? DerCodec.Sequence(DerCodec.Sequence(DerCodec.ExplicitContext(4, issuer)), essSerial, DerCodec.Null())
                : DerCodec.Sequence(DerCodec.Sequence(DerCodec.ExplicitContext(4, issuer)), essSerial);
            if (form != EssForm.V1Sha1WithIssuerSerial)
            {
                byte[] certId;
                if (fault == TimestampFault.UnsupportedEssHash)
                {
                    certId = DerCodec.Sequence(DerCodec.Sequence(DerCodec.Oid("1.2.3")), DerCodec.OctetString(new byte[32]));
                }
                else if (form == EssForm.V2Sha512WithoutIssuerSerial)
                {
                    certId = DerCodec.Sequence(DerCodec.Sequence(DerCodec.Oid("2.16.840.1.101.3.4.2.3")), DerCodec.OctetString(Digest(SHA512.Create(), tsa)));
                }
                else
                {
                    certId = DerCodec.Sequence(DerCodec.OctetString(TestCompat.Sha256(tsa)), issuerSerial);
                }

                byte[] certIds = fault == TimestampFault.FirstCertificateIdentifierMismatch
                    ? DerCodec.Sequence(DerCodec.Sequence(DerCodec.OctetString(new byte[32])), certId)
                    : DerCodec.Sequence(certId);
                attributes.Add(DerCodec.Sequence(
                    DerCodec.Oid("1.2.840.113549.1.9.16.2.47"),
                    DerCodec.Set(DerCodec.Sequence(certIds))));
            }

            if (form == EssForm.V1Sha1WithIssuerSerial
                || form == EssForm.V1AndV2
                || fault == TimestampFault.MultipleSigningCertificateAttributes)
            {
                byte[] certId = DerCodec.Sequence(DerCodec.OctetString(Digest(SHA1.Create(), tsa)), issuerSerial);
                attributes.Add(DerCodec.Sequence(
                    DerCodec.Oid("1.2.840.113549.1.9.16.2.12"),
                    DerCodec.Set(DerCodec.Sequence(DerCodec.Sequence(certId)))));
            }
        }

        return DerCodec.Sequence(
            DerCodec.Integer(1),
            DerCodec.Sequence(issuer, serial),
            DerCodec.AlgorithmSha256(),
            DerCodec.ReplaceTag(DerCodec.Set(attributes.ToArray()), 0xA0),
            DerCodec.AlgorithmSha256WithRsa(),
            DerCodec.OctetString(new byte[256]));
    }

    private static byte[] GeneralizedTime()
    {
        byte[] text = Encoding.ASCII.GetBytes("20261018120000Z");
        var tlv = new byte[2 + text.Length];
        tlv[0] = 0x18;
        tlv[1] = (byte)text.Length;
        Buffer.BlockCopy(text, 0, tlv, 2, text.Length);
        return tlv;
    }

    private static byte[] Digest(HashAlgorithm algorithm, byte[] data)
    {
        using (algorithm)
        {
            return algorithm.ComputeHash(data);
        }
    }

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

    private static byte[] FilledSignatureField(int filler)
    {
        return SignatureField("<< /Subtype /Widget /FT /Sig /T (SignHere) /Rect [10 20 110 40] /P 3 0 R /Filler (" + new string('a', filler) + ") >>");
    }

    // Adds a zero-filled attachment to a one-page file and signs it in the same revision.
    // Returns the signed file, from the source stream or from an empty cross-stream destination.
    private static byte[] SignWithAttachment(int attachmentLength, bool same)
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        using var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        using var destination = new MemoryStream();
        using (var document = PdfDocument.Open(source, leaveOpen: true))
        {
            using var editor = document.Edit();
            editor.Attachments.Add("big.bin", new MemoryStream(new byte[attachmentLength]), leaveOpen: false);
            if (same)
            {
                editor.Sign(source, AppendixOptions("Signature1"), new FixedProvider());
            }
            else
            {
                editor.Sign(destination, AppendixOptions("Signature1"), new FixedProvider(), leaveOpen: true);
            }
        }

        if (same)
        {
            return source.ToArray();
        }

        AssertPrefix(pdf, source.ToArray(), pdf.Length);
        return destination.ToArray();
    }

    // Checks that actual has the expected length and starts with every byte of prefix.
    private static void AssertPrefix(byte[] prefix, byte[] actual, long length)
    {
        Assert.Equal(length, actual.Length);
        int difference = -1;
        for (int i = 0; i < prefix.Length; i++)
        {
            if (prefix[i] != actual[i])
            {
                difference = i;
                break;
            }
        }

        Assert.Equal(-1, difference);
    }

    private static PdfSignatureOptions AppendixOptions(string field)
    {
        return new PdfSignatureOptions
        {
            FieldName = field,
            ReservedContentsBytes = 32,
            SigningTime = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero),
        };
    }

    private static byte[] EncryptedPage(string user, string owner)
    {
        var crypt = new StandardEncryptor(revision6: true, user, owner, permissions: -4, encryptMetadata: true);
        PdfBuilder builder = PdfBuilder.Basic();
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private static void AssertDiagnosticOmits(Exception exception, string secret, string kind)
    {
        Assert.False(string.IsNullOrEmpty(secret));
        string diagnostic = PublicDiagnostic(exception);
        Assert.True(exception.Message.IndexOf(secret, StringComparison.Ordinal) < 0, "The exception message contains the " + kind + ".");
        Assert.True(diagnostic.IndexOf(secret, StringComparison.Ordinal) < 0, "A public diagnostic contains the " + kind + ".");
    }

    private static void AssertDiagnosticOmitsKey(Exception exception, byte[] key)
    {
        Assert.True(key.Length >= 16);
        string hex = TestCompat.ToHexString(key);
        string diagnostic = PublicDiagnostic(exception);
        Assert.True(exception.Message.IndexOf(hex, StringComparison.OrdinalIgnoreCase) < 0, "The exception message contains the key.");
        Assert.True(diagnostic.IndexOf(hex, StringComparison.OrdinalIgnoreCase) < 0, "A public diagnostic contains the key.");
        AssertDiagnosticOmits(exception, Convert.ToBase64String(key), "key");
        AssertDiagnosticOmits(exception, Encoding.GetEncoding(28591).GetString(key), "key");
    }

    private static string PublicDiagnostic(Exception exception)
    {
        var text = new StringBuilder();
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            text.Append(current.Message);
            text.Append('\n');
            text.Append(current.ToString());
            text.Append('\n');
            if (current.Source != null)
            {
                text.Append(current.Source);
                text.Append('\n');
            }

            if (current.HelpLink != null)
            {
                text.Append(current.HelpLink);
                text.Append('\n');
            }

            foreach (object key in current.Data.Keys)
            {
                text.Append(key);
                text.Append('=');
                text.Append(current.Data[key]);
                text.Append('\n');
            }
        }

        return text.ToString();
    }

    // Generated once per test run. The signer keys live as long as the process.
    private static class TimestampCertificates
    {
        private static readonly Lazy<TimestampChain> SmallChain = new Lazy<TimestampChain>(() => CreateChain(2048));
        private static readonly Lazy<TimestampChain> RealisticChain = new Lazy<TimestampChain>(() => CreateChain(4096));
        private static readonly Lazy<byte[]> OtherCertificate = new Lazy<byte[]>(() => SelfSigned("CN=Other TSA,O=jaytwo,C=US"));

        // A 2048-bit chain, for tests that only need the TSA certificate.
        public static TimestampChain Signer => SmallChain.Value;

        public static byte[] Tsa => SmallChain.Value.Tsa;

        public static byte[] Other => OtherCertificate.Value;

        // A 4096-bit root and intermediate over a 2048-bit TSA, as public TSAs issue. About 4 KB in all.
        public static TimestampChain Chain => RealisticChain.Value;

        private static TimestampChain CreateChain(int authorityBits)
        {
            using RSA rootKey = RSA.Create(authorityBits);
            using RSA intermediateKey = RSA.Create(authorityBits);
            using RSA tsaKey = RSA.Create(2048);
            RSA signerKey = RSA.Create(2048);
            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddYears(1);
            using X509Certificate2 root = Request("CN=Timestamp Test Root,O=jaytwo,C=US", rootKey, true).CreateSelfSigned(notBefore, notAfter);
            using X509Certificate2 issued = Request("CN=Timestamp Test Intermediate CA,O=jaytwo,C=US", intermediateKey, true).Create(root, notBefore, notAfter.AddDays(-1), new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF });
            using X509Certificate2 intermediate = issued.CopyWithPrivateKey(intermediateKey);
            CertificateRequest tsaRequest = Request("CN=Timestamp Test TSA,O=jaytwo,C=US", tsaKey, false);
            tsaRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.8") }, true));
            using X509Certificate2 tsa = tsaRequest.Create(intermediate, notBefore, notAfter.AddDays(-2), new byte[] { 0x00, 0x9A, 0xBC, 0xDE, 0xF0 });
            using X509Certificate2 signer = Request("CN=Timestamp Test Signer,O=jaytwo,C=US", signerKey, false).Create(intermediate, notBefore, notAfter.AddDays(-2), new byte[] { 0x11, 0x22, 0x33 });
            return new TimestampChain(root.RawData, intermediate.RawData, tsa.RawData, signer.RawData, signerKey);
        }

        private static CertificateRequest Request(string subject, RSA key, bool authority)
        {
            var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(authority, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(authority ? X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign : X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request;
        }

        private static byte[] SelfSigned(string subject)
        {
            using RSA key = RSA.Create(2048);
            using X509Certificate2 certificate = Request(subject, key, false).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return certificate.RawData;
        }
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public byte[] Payload { get; set; } = new byte[] { 1, 2, 3, 4 };

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(Payload);
        }
    }

    private sealed class HashProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public byte[] Digest { get; private set; } = Array.Empty<byte>();

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            using Stream signed = context.OpenSignedBytes();
            using SHA256 sha = SHA256.Create();
            Digest = sha.ComputeHash(signed);
            return new ValueTask<byte[]>(new byte[] { 1, 2, 3, 4 });
        }
    }

    private sealed class RecordingAuthority : ITimestampAuthority
    {
        public byte[] Request { get; private set; } = Array.Empty<byte>();

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            Request = timeStampReqDer.ToArray();
            (byte[] imprint, byte[] nonce) = ReadTimestampRequest(Request);
            return new ValueTask<byte[]>(BuildTimestampToken(imprint, nonce, TimestampFault.None));
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

        public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            LastSignature = _rsa.SignHash(hash.ToArray(), hashAlgorithm, RSASignaturePadding.Pkcs1);
            return new ValueTask<byte[]>(LastSignature);
        }
    }

    private sealed class ThrowingSigner : IPdfExternalSigner
    {
        public ThrowingSigner(byte[] certificate)
        {
            CertificateDer = certificate;
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates { get; } = Array.Empty<byte[]>();

        public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            throw new CryptographicException("The external signer failed.");
        }
    }

    private sealed class LeakySigner : IPdfExternalSigner
    {
        private readonly string _sentinel;
        private readonly byte[] _sentinelKey;

        public LeakySigner(byte[] certificate, string sentinel, byte[] sentinelKey)
        {
            CertificateDer = certificate;
            _sentinel = sentinel;
            _sentinelKey = sentinelKey;
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates { get; } = Array.Empty<byte[]>();

        public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            var inner = new InvalidOperationException(Convert.ToBase64String(_sentinelKey));
            var exception = new InvalidOperationException(_sentinel, inner);
            exception.Data[_sentinel] = TestCompat.ToHexString(_sentinelKey);
            exception.Data["raw"] = Encoding.GetEncoding(28591).GetString(_sentinelKey);
            throw exception;
        }
    }

    private sealed class HostileSigner : IPdfExternalSigner
    {
        private readonly Func<byte[]> _certificate;
        private readonly Func<IReadOnlyList<byte[]>> _additional;
        private readonly Func<byte[], byte[]> _sign;

        public HostileSigner(Func<byte[]> certificate, Func<IReadOnlyList<byte[]>> additional, Func<byte[], byte[]> sign)
        {
            _certificate = certificate;
            _additional = additional;
            _sign = sign;
        }

        public byte[] CertificateDer
        {
            get
            {
                CertificateReads++;
                return _certificate();
            }
        }

        public IReadOnlyList<byte[]> AdditionalCertificates
        {
            get
            {
                AdditionalReads++;
                return _additional();
            }
        }

        public int CertificateReads { get; private set; }

        public int AdditionalReads { get; private set; }

        public int SignCalls { get; private set; }

        public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            SignCalls++;
            return new ValueTask<byte[]>(_sign(hash.ToArray()));
        }
    }

    // An endless lazy chain. MoveNext throws a secret-bearing exception at throwAt, or never when it is negative.
    private sealed class LazyCertificates : IReadOnlyList<byte[]>
    {
        private readonly byte[] _certificate;
        private readonly int _throwAt;
        private readonly string _secret;

        public LazyCertificates(byte[] certificate, int throwAt, string secret)
        {
            _certificate = certificate;
            _throwAt = throwAt;
            _secret = secret;
        }

        public int Count => int.MaxValue;

        public int MoveNextCalls { get; private set; }

        public byte[] this[int index] => _certificate;

        public IEnumerator<byte[]> GetEnumerator()
        {
            for (int index = 0; ; index++)
            {
                MoveNextCalls++;
                if (index == _throwAt)
                {
                    throw new InvalidOperationException(_secret);
                }

                yield return _certificate;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    // A finite chain that counts how often it is enumerated.
    private sealed class CountingCertificates : IReadOnlyList<byte[]>
    {
        private readonly byte[][] _certificates;

        public CountingCertificates(params byte[][] certificates)
        {
            _certificates = certificates;
        }

        public int Count => _certificates.Length;

        public int GetEnumeratorCalls { get; private set; }

        public byte[] this[int index] => _certificates[index];

        public IEnumerator<byte[]> GetEnumerator()
        {
            GetEnumeratorCalls++;
            return ((IEnumerable<byte[]>)_certificates).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class OversizeAuthority : ITimestampAuthority
    {
        private readonly int _length;

        public OversizeAuthority(int length)
        {
            _length = length;
        }

        public int Calls { get; private set; }

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            Calls++;
            return new ValueTask<byte[]>(new byte[_length]);
        }
    }

    private sealed class ThrowingAuthority : ITimestampAuthority
    {
        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            throw new PdfTimestampException("The timestamp authority failed.");
        }
    }

    private sealed class TokenAuthority : ITimestampAuthority
    {
        public byte[] Request { get; private set; } = Array.Empty<byte>();

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            Request = timeStampReqDer.ToArray();
            (byte[] imprint, byte[] nonce) = ReadTimestampRequest(Request);
            return new ValueTask<byte[]>(BuildTimestampToken(imprint, nonce, TimestampFault.None));
        }
    }

    private sealed class FaultAuthority : ITimestampAuthority
    {
        private readonly TimestampFault _fault;

        public FaultAuthority(TimestampFault fault)
        {
            _fault = fault;
        }

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            (byte[] imprint, byte[] nonce) = ReadTimestampRequest(timeStampReqDer.ToArray());
            return new ValueTask<byte[]>(BuildTimestampToken(imprint, nonce, _fault));
        }
    }

    private sealed class ChainAuthority : ITimestampAuthority
    {
        private readonly EssForm _form;
        private readonly byte[] _tsa;
        private readonly IReadOnlyList<byte[]> _chain;

        public ChainAuthority(EssForm form, byte[] tsa, IReadOnlyList<byte[]> chain)
        {
            _form = form;
            _tsa = tsa;
            _chain = chain;
        }

        public byte[] Token { get; private set; } = Array.Empty<byte>();

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            (byte[] imprint, byte[] nonce) = ReadTimestampRequest(timeStampReqDer.ToArray());
            Token = BuildTimestampToken(imprint, nonce, TimestampFault.None, _form, _tsa, _chain);
            return new ValueTask<byte[]>(Token);
        }
    }

    private sealed class TimestampChain
    {
        public TimestampChain(byte[] root, byte[] intermediate, byte[] tsa, byte[] signer, RSA signerKey)
        {
            Root = root;
            Intermediate = intermediate;
            Tsa = tsa;
            Signer = signer;
            SignerKey = signerKey;
        }

        public byte[] Root { get; }

        public byte[] Intermediate { get; }

        public byte[] Tsa { get; }

        public byte[] Signer { get; }

        public RSA SignerKey { get; }
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

#if NETFRAMEWORK
        private HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
#else
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
#endif
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

    private sealed class GeneratedStream : Stream
    {
        private readonly byte[] _prefix;
        private readonly long _length;

        public GeneratedStream(byte[] prefix, long length)
        {
            _prefix = prefix;
            _length = length;
        }

        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = (int)Math.Min(count, _length - BytesRead);
            int fromPrefix = (int)Math.Max(0, Math.Min(read, _prefix.Length - BytesRead));
            if (fromPrefix > 0)
            {
                Buffer.BlockCopy(_prefix, (int)BytesRead, buffer, offset, fromPrefix);
            }

            Array.Clear(buffer, offset + fromPrefix, read - fromPrefix);
            BytesRead += read;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
