using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public void oversized_timestamp_response_is_refused_after_a_bounded_read()
    {
        var body = new GeneratedStream(Array.Empty<byte>(), 16777216);
        var authority = new HttpTimestampAuthority(new HttpClient(new StreamHandler(body)), new Uri("https://tsa.example/stamp"));

        Exception? thrown = Record.Exception(() => authority.GetTimeStampToken(new byte[] { 1 }));

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
    public void declared_oversized_timestamp_response_is_refused_before_reading()
    {
        var body = new GeneratedStream(Array.Empty<byte>(), 16777216);
        var authority = new HttpTimestampAuthority(new HttpClient(new StreamHandler(body, 16777216)), new Uri("https://tsa.example/stamp"));

        Exception? thrown = Record.Exception(() => authority.GetTimeStampToken(new byte[] { 1 }));

        Assert.Equal(0, body.BytesRead);
        PdfTimestampException rejected = Assert.IsType<PdfTimestampException>(thrown);
        Assert.Contains("larger than", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void timestamp_response_at_the_cap_returns_its_token_and_one_byte_more_is_refused()
    {
        (byte[] atCap, byte[] token) = PaddedTimestampResponse(2097152);
        var atCapBody = new GeneratedStream(atCap, atCap.Length);
        var accepted = new HttpTimestampAuthority(new HttpClient(new StreamHandler(atCapBody)), new Uri("https://tsa.example/stamp"));

        Assert.Equal(token, accepted.GetTimeStampToken(new byte[] { 1 }));
        Assert.Equal(2097152, atCapBody.BytesRead);

        (byte[] overCap, _) = PaddedTimestampResponse(2097153);
        var overCapBody = new GeneratedStream(overCap, overCap.Length);
        var refused = new HttpTimestampAuthority(new HttpClient(new StreamHandler(overCapBody)), new Uri("https://tsa.example/stamp"));

        PdfTimestampException rejected = Assert.Throws<PdfTimestampException>(() => refused.GetTimeStampToken(new byte[] { 1 }));
        Assert.Contains("larger than", rejected.Message, StringComparison.Ordinal);
        Assert.InRange(overCapBody.BytesRead, 0, 2179072);
    }

    [Fact]
    public void stalled_timestamp_response_body_still_times_out()
    {
        var client = new HttpClient(new StreamHandler(new StallingStream())) { Timeout = TimeSpan.FromMilliseconds(500) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        var watch = Stopwatch.StartNew();

        TaskCanceledException canceled = Assert.Throws<TaskCanceledException>(() => authority.GetTimeStampToken(new byte[] { 1 }));
        Assert.IsType<TimeoutException>(canceled.InnerException);
        Assert.InRange(watch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void stalled_timestamp_response_headers_still_time_out()
    {
        var client = new HttpClient(new StallingHandler()) { Timeout = TimeSpan.FromMilliseconds(500) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        var watch = Stopwatch.StartNew();

        TaskCanceledException canceled = Assert.Throws<TaskCanceledException>(() => authority.GetTimeStampToken(new byte[] { 1 }));
        Assert.IsType<TimeoutException>(canceled.InnerException);
        Assert.InRange(watch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void cancel_pending_timestamp_requests_is_not_reported_as_a_timeout()
    {
        var handler = new ObservableStallingHandler();
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        Task<Exception> pending = Task.Run(() => Record.Exception(() => authority.GetTimeStampToken(new byte[] { 1 })));
        Assert.True(handler.Started.Wait(TimeSpan.FromSeconds(10)));

        client.CancelPendingRequests();

        Exception canceled = Assert.IsAssignableFrom<OperationCanceledException>(pending.GetAwaiter().GetResult());
        Assert.False(canceled.InnerException is TimeoutException);
    }

    [Fact]
    public void stalled_timestamp_body_that_ignores_the_token_is_aborted_by_disposing_the_response()
    {
        var body = new TokenIgnoringStream();
        var client = new HttpClient(new StreamHandler(body)) { Timeout = TimeSpan.FromMilliseconds(500) };
        var authority = new HttpTimestampAuthority(client, new Uri("https://tsa.example/stamp"));
        var watch = Stopwatch.StartNew();

        TaskCanceledException canceled = Assert.Throws<TaskCanceledException>(() => authority.GetTimeStampToken(new byte[] { 1 }));
        TimeoutException timeout = Assert.IsType<TimeoutException>(canceled.InnerException);
        Assert.IsType<ObjectDisposedException>(timeout.InnerException);
        Assert.True(body.Disposed);
        Assert.InRange(watch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(10));
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
            document.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 128, SigningTime = TestCompat.UnixEpoch }, new FixedProvider());
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
    public void sign_to_another_stream_leaves_the_opened_stream_unchanged()
    {
        byte[] pdf = TextField();
        var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        var destination = new MemoryStream();
        using (var document = PdfDocument.Open(source, leaveOpen: true))
        {
            document.Sign(
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
            document.Sign(
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
            document.Sign(
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
            var authority = new ChainAuthority(EssForm.V2DefaultHashWithIssuerSerial, TimestampCertificates.Tsa, Array.Empty<byte[]>());
            PdfSignatureOptions options = documentTimestamp
                ? new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 1024 }
                : new PdfSignatureOptions { ReservedContentsBytes = 1024, SigningTime = TestCompat.UnixEpoch };

            PdfSignatureTooLargeException tooLarge = Assert.Throws<PdfSignatureTooLargeException>(() => document.Sign(stream, options, TimestampProvider(authority, documentTimestamp, null)));

            Assert.Equal(1024, tooLarge.ReservedBytes);
            Assert.True(tooLarge.ActualBytes > 1024);
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

        Assert.Throws<CryptographicException>(() => document.Sign(
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
        CryptographicException exception = Assert.Throws<CryptographicException>(() => document.Sign(
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

        CryptographicException exception = Assert.Throws<CryptographicException>(() => document.Sign(
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

        Assert.Throws<PdfTimestampException>(() => document.Sign(
            stream,
            new PdfSignatureOptions { ReservedContentsBytes = 16384, SigningTime = TestCompat.UnixEpoch },
            new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData), new ThrowingAuthority())));

        Assert.Equal(length, stream.Length);
        Assert.Equal(pdf, stream.ToArray());
        using var again = PdfDocument.Open(new MemoryStream(stream.ToArray()), leaveOpen: true);
        Assert.DoesNotContain(again.AcroForm.Fields, field => field.FieldType == PdfFieldType.Signature);
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
        PdfSignatureOptions options;
        IPdfSignatureProvider provider;
        if (documentTimestamp)
        {
            options = new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 1024 };
            provider = new DocTimeStampSignatureProvider(new FaultAuthority(fault));
            PdfTimestampException rejected = Assert.Throws<PdfTimestampException>(() => document.Sign(stream, options, provider));
            Assert.Contains(phrase, rejected.Message, StringComparison.Ordinal);
            Assert.Equal(length, stream.Length);
            return;
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Pdf Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        options = new PdfSignatureOptions { ReservedContentsBytes = 8192, SigningTime = TestCompat.UnixEpoch };
        provider = new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate.RawData), new FaultAuthority(fault));
        PdfTimestampException exception = Assert.Throws<PdfTimestampException>(() => document.Sign(stream, options, provider));
        Assert.Contains(phrase, exception.Message, StringComparison.Ordinal);
        Assert.Equal(length, stream.Length);
    }

    private static byte[] SignWithTimestamp(byte[] pdf, ITimestampAuthority authority, bool documentTimestamp, PdfSignatureOptions options, TimestampChain? signerChain)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(stream, options, TimestampProvider(authority, documentTimestamp, signerChain));
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
        var authority = new HttpTimestampAuthority(new HttpClient(handler), new Uri("https://tsa.example/stamp"));

        PdfTimestampException rejected = Assert.Throws<PdfTimestampException>(() => document.Sign(
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
            (byte[] imprint, byte[] nonce) = ReadTimestampRequest(Request);
            return BuildTimestampToken(imprint, nonce, TimestampFault.None);
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

    private sealed class ThrowingSigner : IPdfExternalSigner
    {
        public ThrowingSigner(byte[] certificate)
        {
            CertificateDer = certificate;
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates { get; } = Array.Empty<byte[]>();

        public byte[] SignHash(ReadOnlySpan<byte> hash)
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

        public byte[] SignHash(ReadOnlySpan<byte> hash)
        {
            var inner = new InvalidOperationException(Convert.ToBase64String(_sentinelKey));
            var exception = new InvalidOperationException(_sentinel, inner);
            exception.Data[_sentinel] = TestCompat.ToHexString(_sentinelKey);
            exception.Data["raw"] = Encoding.GetEncoding(28591).GetString(_sentinelKey);
            throw exception;
        }
    }

    private sealed class ThrowingAuthority : ITimestampAuthority
    {
        public byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer)
        {
            throw new PdfTimestampException("The timestamp authority failed.");
        }
    }

    private sealed class TokenAuthority : ITimestampAuthority
    {
        public byte[] Request { get; private set; } = Array.Empty<byte>();

        public byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer)
        {
            Request = timeStampReqDer.ToArray();
            (byte[] imprint, byte[] nonce) = ReadTimestampRequest(Request);
            return BuildTimestampToken(imprint, nonce, TimestampFault.None);
        }
    }

    private sealed class FaultAuthority : ITimestampAuthority
    {
        private readonly TimestampFault _fault;

        public FaultAuthority(TimestampFault fault)
        {
            _fault = fault;
        }

        public byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer)
        {
            (byte[] imprint, byte[] nonce) = ReadTimestampRequest(timeStampReqDer.ToArray());
            return BuildTimestampToken(imprint, nonce, _fault);
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

        public byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer)
        {
            (byte[] imprint, byte[] nonce) = ReadTimestampRequest(timeStampReqDer.ToArray());
            Token = BuildTimestampToken(imprint, nonce, TimestampFault.None, _form, _tsa, _chain);
            return Token;
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

    private sealed class StreamHandler : HttpMessageHandler
    {
        private readonly Stream _body;
        private readonly long? _declaredLength;

        public StreamHandler(Stream body, long? declaredLength = null)
        {
            _body = body;
            _declaredLength = declaredLength;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StreamContent(_body);
            if (_declaredLength.HasValue)
            {
                content.Headers.ContentLength = _declaredLength;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class StallingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            throw new IOException("The stalled headers were not cancelled.");
        }
    }

    private sealed class ObservableStallingHandler : HttpMessageHandler
    {
        public ManualResetEventSlim Started { get; } = new ManualResetEventSlim();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.Set();
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            throw new IOException("The canceled headers resumed without cancellation.");
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

    private sealed class StallingStream : Stream
    {
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
            Thread.Sleep(TimeSpan.FromSeconds(30));
            throw new IOException("The stalled body was read synchronously.");
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            throw new IOException("The stalled body was not cancelled.");
        }

#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            throw new IOException("The stalled body was not cancelled.");
        }
#endif

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Models a network stream that ignores the read token. Only Dispose ends a pending read early.
    private sealed class TokenIgnoringStream : Stream
    {
        private readonly TaskCompletionSource<int> _pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }

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
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(
                delay => _pending.TrySetException(new IOException("The stalled body was not disposed.")),
                TaskScheduler.Default);
            return _pending.Task;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            _pending.TrySetException(new ObjectDisposedException(nameof(TokenIgnoringStream)));
            base.Dispose(disposing);
        }
    }
}
