using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.Signatures;
using Xunit;

namespace jaytwo.Pdf.Tests;

public sealed class SigningRollbackTests
{
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 2)]
    public async Task partial_write_preserves_the_original_failure_and_reports_restoration(bool same, bool useAsync, int rollback)
    {
        await CheckFailure(same, useAsync, rollback, finalization: false);
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 2)]
    public async Task finalization_failure_uses_the_same_signing_restoration_contract(bool same, bool useAsync, int rollback)
    {
        await CheckFailure(same, useAsync, rollback, finalization: true);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task source_disposal_failure_preserves_both_failures(bool useAsync, int rollback)
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        using var source = new DisposalFault();
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: false);
        using var editor = document.Edit();
        using var destination = new OutputFault { Rollback = rollback, Armed = true };
        source.Fail = true;

        Exception? caught = await Record.ExceptionAsync(() => Sign(editor, destination, useAsync));

        Assert.Same(source.Failure, caught);
        AssertRestoration(destination, source.Failure, 0, rollback);
        Assert.Equal(pdf, source.ToArray());
        using var retry = new MemoryStream();
        await Sign(editor, retry, useAsync);
        Assert.True(retry.Length > pdf.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task output_cancellation_retains_its_type_identity_and_token(int rollback)
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var original = new OperationCanceledException(cancellation.Token);
        using var destination = new OutputFault { Armed = true, PartialWrite = true, Rollback = rollback, Failure = original };

        Exception? caught = await Record.ExceptionAsync(() => Sign(editor, destination, useAsync: true));

        Assert.Same(original, caught);
        Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(caught).CancellationToken);
        AssertRestoration(destination, original, 0, rollback);
    }

    [Fact]
    public void a_throwing_exception_data_dictionary_cannot_replace_the_original_failure()
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        var original = new DataFault();
        using var destination = new OutputFault { Armed = true, PartialWrite = true, Rollback = 1, Failure = original };

        Assert.Same(original, Assert.Throws<DataFault>(() => editor.Sign(destination, Options(), new FixedProvider(), leaveOpen: true)));
        PdfSigningRollbackFailure failure = Assert.IsType<PdfSigningRollbackFailure>(PdfSigningRollbackFailure.Get(original));
        Assert.False(failure.LengthRestored);
        Assert.Same(destination.RestorationFailure, failure.RestorationException);
    }

    [Fact]
    public void ordinary_incremental_save_keeps_its_existing_rollback_behavior()
    {
        using var source = new MemoryStream(PdfBuilder.Basic().Finish());
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        editor.Metadata.Title = "Pending";
        using var destination = new OutputFault { Armed = true, PartialWrite = true, Rollback = 1 };

        Assert.Same(destination.Failure, Assert.Throws<IOException>(() => editor.SaveIncremental(destination, leaveOpen: true)));
        Assert.Null(PdfSigningRollbackFailure.Get(destination.Failure));
        Assert.False(destination.Failure.Data.Contains(PdfSigningRollbackFailure.DataKey));
    }

    private static async Task CheckFailure(bool same, bool useAsync, int rollback, bool finalization)
    {
        byte[] pdf = PdfBuilder.Basic().Finish();
        using var source = new OutputFault();
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        editor.Metadata.Title = "Pending";
        using var other = new OutputFault();
        OutputFault destination = same ? source : other;
        destination.Rollback = rollback;
        destination.PartialWrite = !finalization;
        destination.Finalization = finalization;
        destination.Arm();

        Exception? caught = await Record.ExceptionAsync(() => Sign(editor, destination, useAsync));

        Assert.Same(destination.Failure, caught);
        long originalLength = same ? pdf.Length : 0;
        AssertRestoration(destination, destination.Failure, originalLength, rollback);
        if (rollback != 1)
        {
            Assert.Equal(same ? pdf : Array.Empty<byte>(), destination.ToArray());
        }

        if (!same)
        {
            Assert.Equal(pdf, source.ToArray());
        }

        // A damaged same-stream source must be repaired before retry; logical edit restoration alone is insufficient.
        destination.Armed = false;
        destination.SetLength(originalLength);
        destination.Seek(originalLength, SeekOrigin.Begin);
        using var retry = new MemoryStream();
        await Sign(editor, retry, useAsync);
        using var reopened = PdfDocument.Open(new MemoryStream(retry.ToArray()));
        Assert.Equal("Pending", reopened.Metadata.Title);
    }

    private static void AssertRestoration(OutputFault destination, Exception original, long originalLength, int rollback)
    {
        if (rollback == 0)
        {
            Assert.Null(PdfSigningRollbackFailure.Get(original));
            Assert.False(original.Data.Contains(PdfSigningRollbackFailure.DataKey));
            Assert.Equal(originalLength, destination.RawLength);
            Assert.Equal(originalLength, destination.Position);
            return;
        }

        PdfSigningRollbackFailure failure = Assert.IsType<PdfSigningRollbackFailure>(PdfSigningRollbackFailure.Get(original));
        Assert.Same(failure, original.Data[PdfSigningRollbackFailure.DataKey]);
        Assert.Same(destination.RestorationFailure, failure.RestorationException);
        Assert.Equal(originalLength, failure.OriginalLength);
        Assert.Equal(rollback == 2, failure.LengthRestored);
        Assert.Equal(rollback == 2, failure.SeekAttempted);
        Assert.False(failure.SeekSucceeded);
        if (rollback == 1)
        {
            Assert.True(destination.RawLength > originalLength);
        }
        else
        {
            Assert.Equal(originalLength, destination.RawLength);
        }
    }

    private static PdfSignatureOptions Options() => new() { ReservedContentsBytes = 64 };

    private static Task Sign(PdfDocumentEditor editor, Stream destination, bool useAsync)
    {
        if (useAsync)
        {
            return editor.SignAsync(destination, Options(), new FixedProvider(), leaveOpen: true);
        }

        editor.Sign(destination, Options(), new FixedProvider(), leaveOpen: true);
        return Task.CompletedTask;
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(new byte[] { 1, 2, 3, 4 });
        }
    }

    private sealed class OutputFault : MemoryStream
    {
        private bool _wrote;
        private bool _restoring;

        public bool Armed { get; set; }

        public bool PartialWrite { get; set; }

        public bool Finalization { get; set; }

        public int Rollback { get; set; }

        public Exception Failure { get; set; } = new IOException("Original output failure.");

        public Exception RestorationFailure { get; } = new IOException("Restoration failure.");

        public long RawLength => base.Length;

        public override long Length
        {
            get
            {
                if (Armed && _wrote && Finalization)
                {
                    throw Failure;
                }

                return base.Length;
            }
        }

        public void Arm()
        {
            _wrote = false;
            Armed = true;
        }

        public override void SetLength(long value)
        {
            if (Armed)
            {
                _restoring = true;
                if (Rollback == 1)
                {
                    throw RestorationFailure;
                }
            }

            base.SetLength(value);
        }

        public override long Seek(long offset, SeekOrigin loc)
        {
            if (Armed && _restoring && Rollback == 2)
            {
                throw RestorationFailure;
            }

            return base.Seek(offset, loc);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Armed && PartialWrite)
            {
                base.Write(buffer, offset, Math.Min(count, 17));
                throw Failure;
            }

            base.Write(buffer, offset, count);
            _wrote |= Armed && count > 0;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
    }

    private sealed class DisposalFault : MemoryStream
    {
        public bool Fail { get; set; }

        public IOException Failure { get; } = new("Original source disposal failure.");

        protected override void Dispose(bool disposing)
        {
            if (disposing && Fail)
            {
                Fail = false;
                throw Failure;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class DataFault : IOException
    {
        public override IDictionary Data => throw new InvalidOperationException("Data unavailable.");
    }
}
