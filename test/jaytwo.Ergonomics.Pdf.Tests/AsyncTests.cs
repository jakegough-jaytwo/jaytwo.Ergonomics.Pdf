using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// W-69 (spec W20): the async save and sign share the synchronous path, so they write the same bytes. Cancellation
// and failures mid-write leave the destination at its starting length, and host exceptions do not leave the library.
public class AsyncTests
{
    private const string Secret = "tsa-secret-7f3a";

    private static readonly DateTimeOffset SigningTime = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task sync_and_async_saves_write_the_same_bytes(bool same)
    {
        byte[] pdf = Form();

        byte[] expected = await Save(pdf, null, same, useAsync: false);
        byte[] actual = await Save(pdf, null, same, useAsync: true);

        Assert.True(expected.Length > pdf.Length);
        Assert.Equal(pdf, Prefix(actual, pdf.Length));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task sync_and_async_signatures_write_the_same_bytes(bool same)
    {
        using var key = RSA.Create(2048);
        var signer = new DeferredSigner(key, Certificate(key));
        byte[] pdf = Form();

        byte[] expected = await Sign(pdf, null, signer, same, useAsync: false);
        byte[] actual = await Sign(pdf, null, signer, same, useAsync: true);

        Assert.Equal(pdf, Prefix(actual, pdf.Length));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("owner")]
    public async Task an_rc4_fill_and_signature_write_the_same_bytes_sync_and_async(string password)
    {
        using var key = RSA.Create(2048);
        var signer = new DeferredSigner(key, Certificate(key));
        byte[] pdf = Rc4Form(-4);

        byte[] expected = await Sign(pdf, password, signer, same: false, useAsync: false);
        byte[] actual = await Sign(pdf, password, signer, same: false, useAsync: true);

        Assert.Equal(pdf, Prefix(actual, pdf.Length));
        Assert.Equal(expected, actual);
        using var reopened = PdfDocument.Open(new MemoryStream(actual), password);
        Assert.Equal("Ada", reopened.AcroForm.Export()["Name"].Text);
    }

    [Fact]
    public async Task an_async_sign_the_permission_bits_refuse_leaves_the_destination_empty()
    {
        using var key = RSA.Create(2048);
        byte[] pdf = Rc4Form(-4 & ~32);
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, "user", leaveOpen: true);
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<PdfEncryptionRestrictedException>(() => document.SignAsync(destination, Options(), new CmsDetachedSignatureProvider(new DeferredSigner(key, Certificate(key))), leaveOpen: true));

        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, source.ToArray());
    }

    [Fact]
    public async Task a_canceled_token_writes_nothing_and_the_document_still_saves_and_signs()
    {
        using var key = RSA.Create(2048);
        var signer = new DeferredSigner(key, Certificate(key));
        byte[] pdf = Form();
        byte[] expectedSave = await Save(pdf, null, same: false, useAsync: false);
        byte[] expectedSign = await Sign(pdf, null, signer, same: false, useAsync: false);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        using (var source = new MemoryStream(pdf))
        using (var document = PdfDocument.Open(source, leaveOpen: true))
        {
            document.AcroForm.Fill("Name", "Ada");
            var destination = new MemoryStream();
            OperationCanceledException save = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.SaveIncrementalAsync(destination, true, canceled.Token));
            Assert.Equal(canceled.Token, save.CancellationToken);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.SaveIncrementalAsync(canceled.Token));
            Assert.Equal(0, destination.Length);
            Assert.Equal(pdf, source.ToArray());

            document.SaveIncremental(destination, leaveOpen: true);
            Assert.Equal(expectedSave, destination.ToArray());
        }

        using (var source = new MemoryStream(pdf))
        using (var document = PdfDocument.Open(source, leaveOpen: true))
        {
            document.AcroForm.Fill("Name", "Ada");
            var destination = new MemoryStream();
            OperationCanceledException sign = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.SignAsync(destination, Options(), new CmsDetachedSignatureProvider(signer), true, canceled.Token));
            Assert.Equal(canceled.Token, sign.CancellationToken);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.SignAsync(Options(), new CmsDetachedSignatureProvider(signer), canceled.Token));
            Assert.Equal(0, destination.Length);
            Assert.Equal(pdf, source.ToArray());

            document.Sign(destination, Options(), new CmsDetachedSignatureProvider(signer), leaveOpen: true);
            Assert.Equal(expectedSign, destination.ToArray());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task cancellation_inside_the_signer_is_a_fresh_cancellation_and_the_document_signs_again(bool hostThrowsSecret)
    {
        using var key = RSA.Create(2048);
        byte[] certificate = Certificate(key);
        var signer = new DeferredSigner(key, certificate);
        byte[] pdf = Form();
        byte[] expected = await Sign(pdf, null, signer, same: false, useAsync: false);
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        document.AcroForm.Fill("Name", "Ada");
        var destination = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        var stalling = new StallingSigner(certificate, cancellation, hostThrowsSecret);

        OperationCanceledException canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.SignAsync(destination, Options(), new CmsDetachedSignatureProvider(stalling), true, cancellation.Token));

        Assert.Equal(typeof(OperationCanceledException), canceled.GetType());
        Assert.Equal(cancellation.Token, canceled.CancellationToken);
        Assert.Null(canceled.InnerException);
        Assert.Empty(canceled.Data);
        Assert.DoesNotContain(Secret, canceled.Message, StringComparison.Ordinal);
        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, source.ToArray());

        document.Sign(destination, Options(), new CmsDetachedSignatureProvider(signer), leaveOpen: true);
        Assert.Equal(expected, destination.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task cancellation_inside_the_timestamp_authority_is_a_fresh_cancellation(bool documentTimestamp)
    {
        using var key = RSA.Create(2048);
        byte[] pdf = Form();
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        var destination = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        var authority = new StallingAuthority(cancellation);
        IPdfSignatureProvider provider = documentTimestamp
            ? new DocTimeStampSignatureProvider(authority)
            : new CmsDetachedSignatureProvider(new DeferredSigner(key, Certificate(key)), authority);
        PdfSignatureOptions options = Options(provider.Kind);

        OperationCanceledException canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.SignAsync(destination, options, provider, true, cancellation.Token));

        Assert.True(authority.Called);
        Assert.Equal(typeof(OperationCanceledException), canceled.GetType());
        Assert.Equal(cancellation.Token, canceled.CancellationToken);
        Assert.Null(canceled.InnerException);
        Assert.DoesNotContain(Secret, canceled.Message, StringComparison.Ordinal);
        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, source.ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void timestamp_authority_exceptions_carry_no_provider_details(bool documentTimestamp, bool asPdfTimestampException)
    {
        using var key = RSA.Create(2048);
        byte[] pdf = Form();
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        var destination = new MemoryStream();
        var authority = new LeakyAuthority(asPdfTimestampException);
        IPdfSignatureProvider provider = documentTimestamp
            ? new DocTimeStampSignatureProvider(authority)
            : new CmsDetachedSignatureProvider(new DeferredSigner(key, Certificate(key)), authority);
        PdfSignatureOptions options = Options(provider.Kind);

        PdfTimestampException failed = Assert.Throws<PdfTimestampException>(() => document.Sign(destination, options, provider, leaveOpen: true));

        Assert.True(authority.Called);
        Assert.Equal("The timestamp authority failed.", failed.Message);
        Assert.Null(failed.InnerException);
        Assert.Empty(failed.Data);
        Assert.DoesNotContain(Secret, failed.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, source.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task cancellation_during_the_prefix_copy_truncates_the_destination(bool sign)
    {
        using var key = RSA.Create(2048);
        var signer = new DeferredSigner(key, Certificate(key));
        byte[] pdf = Large();
        byte[] expected = sign
            ? await Sign(pdf, null, signer, same: false, useAsync: false)
            : await Save(pdf, null, same: false, useAsync: false);
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        document.AcroForm.Fill("Name", "Ada");
        using var cancellation = new CancellationTokenSource();
        var destination = new CancelingDestination(cancellation);

        Task pending = sign
            ? document.SignAsync(destination, Options(), new CmsDetachedSignatureProvider(signer), true, cancellation.Token)
            : document.SaveIncrementalAsync(destination, true, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.Equal(1, destination.AsyncWrites);
        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, source.ToArray());

        var retry = new MemoryStream();
        if (sign)
        {
            document.Sign(retry, Options(), new CmsDetachedSignatureProvider(signer), leaveOpen: true);
        }
        else
        {
            document.SaveIncremental(retry, leaveOpen: true);
        }

        Assert.Equal(expected, retry.ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task a_write_that_fails_part_way_truncates_the_destination(bool sign, bool useAsync)
    {
        using var key = RSA.Create(2048);
        var signer = new DeferredSigner(key, Certificate(key));
        byte[] pdf = Large();
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        document.AcroForm.Fill("Name", "Ada");
        var destination = new FailingStream(1024 * 1024, armed: true);

        Exception? failed = await Record.ExceptionAsync(() => Write(document, destination, sign ? signer : null, useAsync));

        Assert.IsType<IOException>(failed);
        Assert.True(destination.Failed);
        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, source.ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task a_same_stream_append_that_fails_part_way_restores_the_original_length(bool sign, bool useAsync)
    {
        using var key = RSA.Create(2048);
        var signer = new DeferredSigner(key, Certificate(key));
        byte[] pdf = Form();
        var source = new FailingStream(64, armed: false);
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        document.AcroForm.Fill("Name", "Ada");
        source.Arm();

        Exception? failed = await Record.ExceptionAsync(() => Write(document, source, sign ? signer : null, useAsync));

        Assert.IsType<IOException>(failed);
        Assert.True(source.Failed);
        Assert.Equal(pdf, source.ToArray());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task a_length_failure_during_finalization_rolls_back_and_preserves_pending_edits(bool sign, bool useAsync, bool same)
    {
        using var key = RSA.Create(2048);
        var signer = new DeferredSigner(key, Certificate(key));
        byte[] pdf = Form();
        byte[] expected = sign
            ? await Sign(pdf, null, signer, same: false, useAsync: false)
            : await Save(pdf, null, same: false, useAsync: false);
        var source = new FinalizationFailingStream();
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        document.AcroForm.Fill("Name", "Ada");
        FinalizationFailingStream destination = same ? source : new FinalizationFailingStream();
        destination.Arm();

        Exception? failed = await Record.ExceptionAsync(() => Write(document, destination, sign ? signer : null, useAsync));

        Assert.IsType<IOException>(failed);
        Assert.Equal(same ? pdf.Length : 0, destination.RawLength);
        if (same)
        {
            Assert.Equal(pdf, source.ToArray());
        }

        destination.Disarm();
        using var retry = new MemoryStream();
        await Write(document, retry, sign ? signer : null, useAsync);
        Assert.Equal(expected, retry.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task a_source_dispose_failure_during_a_copy_only_save_rolls_back_the_destination(bool useAsync)
    {
        byte[] pdf = Form();
        using var source = new DisposeFailingStream();
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: false);
        using var destination = new MemoryStream();

        Exception? failed = await Record.ExceptionAsync(() => useAsync
            ? document.SaveIncrementalAsync(destination, leaveOpen: true)
            : SaveSynchronously(document, destination));

        Assert.IsType<IOException>(failed);
        Assert.Equal(0, destination.Length);
        using var retry = new MemoryStream();
        if (useAsync)
        {
            await document.SaveIncrementalAsync(retry, leaveOpen: true);
        }
        else
        {
            document.SaveIncremental(retry, leaveOpen: true);
        }

        Assert.Equal(pdf, retry.ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task a_source_dispose_failure_during_finalization_rolls_back_and_preserves_pending_edits(bool sign, bool useAsync)
    {
        using var key = RSA.Create(2048);
        var signer = new DeferredSigner(key, Certificate(key));
        byte[] pdf = Form();
        byte[] expected = sign
            ? await Sign(pdf, null, signer, same: false, useAsync: false)
            : await Save(pdf, null, same: false, useAsync: false);
        using var source = new DisposeFailingStream();
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: false);
        document.AcroForm.Fill("Name", "Ada");
        using var destination = new MemoryStream();

        Exception? failed = await Record.ExceptionAsync(() => Write(document, destination, sign ? signer : null, useAsync));

        Assert.IsType<IOException>(failed);
        Assert.Equal(0, destination.Length);
        using var retry = new MemoryStream();
        await Write(document, retry, sign ? signer : null, useAsync);
        Assert.Equal(expected, retry.ToArray());
    }

    private static Task Write(PdfDocument document, Stream destination, IPdfExternalSigner? signer, bool useAsync)
    {
        if (signer == null)
        {
            if (useAsync)
            {
                return document.SaveIncrementalAsync(destination, true, CancellationToken.None);
            }

            document.SaveIncremental(destination, leaveOpen: true);
            return Task.CompletedTask;
        }

        if (useAsync)
        {
            return document.SignAsync(destination, Options(), new CmsDetachedSignatureProvider(signer), true, CancellationToken.None);
        }

        document.Sign(destination, Options(), new CmsDetachedSignatureProvider(signer), leaveOpen: true);
        return Task.CompletedTask;
    }

    private static Task SaveSynchronously(PdfDocument document, Stream destination)
    {
        document.SaveIncremental(destination, leaveOpen: true);
        return Task.CompletedTask;
    }

    // Fills Name and saves, on the opened stream or into an empty one.
    private static async Task<byte[]> Save(byte[] pdf, string? password, bool same, bool useAsync)
    {
        using var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        using var destination = new MemoryStream();
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        document.AcroForm.Fill("Name", "Ada");
        if (same && useAsync)
        {
            await document.SaveIncrementalAsync();
        }
        else if (same)
        {
            document.SaveIncremental();
        }
        else if (useAsync)
        {
            await document.SaveIncrementalAsync(destination, leaveOpen: true);
        }
        else
        {
            document.SaveIncremental(destination, leaveOpen: true);
        }

        return same ? source.ToArray() : destination.ToArray();
    }

    // Fills Name and signs in the same revision, on the opened stream or into an empty one.
    private static async Task<byte[]> Sign(byte[] pdf, string? password, IPdfExternalSigner signer, bool same, bool useAsync)
    {
        using var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        using var destination = new MemoryStream();
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        document.AcroForm.Fill("Name", "Ada");
        var provider = new CmsDetachedSignatureProvider(signer);
        if (same && useAsync)
        {
            await document.SignAsync(Options(), provider);
        }
        else if (same)
        {
            document.Sign(Options(), provider);
        }
        else if (useAsync)
        {
            await document.SignAsync(destination, Options(), provider, leaveOpen: true);
        }
        else
        {
            document.Sign(destination, Options(), provider, leaveOpen: true);
        }

        return same ? source.ToArray() : destination.ToArray();
    }

    private static PdfSignatureOptions Options(PdfSignatureKind kind = PdfSignatureKind.CmsDetached)
    {
        return new PdfSignatureOptions { Kind = kind, ReservedContentsBytes = 8192, SigningTime = SigningTime };
    }

    private static byte[] Prefix(byte[] data, int length)
    {
        var prefix = new byte[length];
        Buffer.BlockCopy(data, 0, prefix, 0, length);
        return prefix;
    }

    private static byte[] Certificate(RSA key)
    {
        var request = new CertificateRequest("CN=Async Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificate.RawData;
    }

    // A text field and an /ID, so neither save nor sign draws a random identifier.
    private static byte[] Form(PdfBuilder? builder = null)
    {
        builder ??= new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 240 720] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");
        return builder.Finish("/ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>]");
    }

    // The form after a 3 MiB stream, so the cross-stream copy takes several 1 MiB blocks.
    private static byte[] Large()
    {
        var builder = new PdfBuilder();
        builder.StreamObject(5, "/Type /XObject", new byte[3 * 1024 * 1024]);
        return Form(builder);
    }

    // 128-bit RC4 (revision 3) is deterministic, so the encrypted output can be compared byte for byte.
    private static byte[] Rc4Form(int permissions)
    {
        var crypt = new StandardEncryptor(StandardEncryptor.Kind.Revision3, "user", "owner", permissions, encryptMetadata: true, 128);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T " + crypt.Text("Name", 4) + " /Rect [72 700 240 720] /DA " + crypt.Text("/Helv 0 Tf 0 g", 4) + " /P 3 0 R >>");
        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    // Completes asynchronously, as a cloud key service does.
    private sealed class DeferredSigner : IPdfExternalSigner
    {
        private readonly RSA _key;

        public DeferredSigner(RSA key, byte[] certificate)
        {
            _key = key;
            CertificateDer = certificate;
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates => Array.Empty<byte[]>();

        public async ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return _key.SignHash(hash.ToArray(), hashAlgorithm, RSASignaturePadding.Pkcs1);
        }
    }

    // Cancels the caller's token once it is called, then either waits for the cancellation or throws a secret.
    private sealed class StallingSigner : IPdfExternalSigner
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly bool _throwSecret;

        public StallingSigner(byte[] certificate, CancellationTokenSource cancellation, bool throwSecret)
        {
            CertificateDer = certificate;
            _cancellation = cancellation;
            _throwSecret = throwSecret;
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates => Array.Empty<byte[]>();

        public async ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            _cancellation.CancelAfter(50);
            if (_throwSecret)
            {
                await Task.Delay(Timeout.Infinite, _cancellation.Token).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
                var exception = new InvalidOperationException(Secret, new InvalidOperationException(Secret));
                exception.Data[Secret] = Secret;
                throw exception;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("The delay ended without cancellation.");
        }
    }

    private sealed class StallingAuthority : ITimestampAuthority
    {
        private readonly CancellationTokenSource _cancellation;

        public StallingAuthority(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public bool Called { get; private set; }

        public async ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            Called = true;
            _cancellation.CancelAfter(50);
            await Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
            throw new InvalidOperationException(Secret, new IOException(Secret));
        }
    }

    private sealed class LeakyAuthority : ITimestampAuthority
    {
        private readonly bool _asPdfTimestampException;

        public LeakyAuthority(bool asPdfTimestampException)
        {
            _asPdfTimestampException = asPdfTimestampException;
        }

        public bool Called { get; private set; }

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            Called = true;
            Exception exception = _asPdfTimestampException
                ? new PdfTimestampException("TSA account " + Secret + " is over quota.")
                : new InvalidOperationException(Secret, new IOException(Secret));
            exception.Data[Secret] = Secret;
            throw exception;
        }
    }

    // Writes each asynchronous block, then cancels, so the next block sees the cancellation after a partial copy.
    private sealed class CancelingDestination : MemoryStream
    {
        private readonly CancellationTokenSource _cancellation;

        public CancelingDestination(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public int AsyncWrites { get; private set; }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            AsyncWrites++;
            _cancellation.Cancel();
            return Task.CompletedTask;
        }
    }

    // Once armed, writes failAfterBytes more and then throws part way through a write.
    private sealed class FailingStream : MemoryStream
    {
        private readonly long _failAfterBytes;
        private long? _remaining;

        public FailingStream(long failAfterBytes, bool armed)
        {
            _failAfterBytes = failAfterBytes;
            if (armed)
            {
                Arm();
            }
        }

        public bool Failed { get; private set; }

        public void Arm()
        {
            _remaining = _failAfterBytes;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_remaining == null || count <= _remaining.Value)
            {
                base.Write(buffer, offset, count);
                _remaining -= count;
                return;
            }

            base.Write(buffer, offset, (int)_remaining.Value);
            _remaining = 0;
            Failed = true;
            throw new IOException("The destination failed part way through a write.");
        }

#if NETCOREAPP
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Write(buffer.ToArray(), 0, buffer.Length);
        }
#endif

        public override void WriteByte(byte value)
        {
            Write(new[] { value }, 0, 1);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
    }

    private sealed class FinalizationFailingStream : MemoryStream
    {
        private bool _armed;
        private bool _wroteWhileArmed;

        public override long Length => _armed && _wroteWhileArmed ? throw new IOException("The destination length failed during finalization.") : base.Length;

        public long RawLength => base.Length;

        public void Arm()
        {
            _armed = true;
            _wroteWhileArmed = false;
        }

        public void Disarm()
        {
            _armed = false;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            _wroteWhileArmed |= _armed && count > 0;
        }

#if NETCOREAPP
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            _wroteWhileArmed |= _armed && buffer.Length > 0;
        }
#endif

        public override void WriteByte(byte value)
        {
            base.WriteByte(value);
            _wroteWhileArmed |= _armed;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
    }

    private sealed class DisposeFailingStream : MemoryStream
    {
        private bool _fail = true;

        protected override void Dispose(bool disposing)
        {
            if (disposing && _fail)
            {
                _fail = false;
                throw new IOException("The source failed during disposal.");
            }

            base.Dispose(disposing);
        }
    }
}
