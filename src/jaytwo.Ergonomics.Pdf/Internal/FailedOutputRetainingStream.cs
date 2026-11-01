using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Ergonomics.Pdf.Internal;

// WORKFLOW-RESTORE-01: the engine restores a failed separate destination by setting its length back to zero. An ordinary
// workflow save leaves the bytes it wrote, so this wrapper declines that one call and forwards everything else. Source
// append and signing never use it, and the engine's own restoration is unchanged.
internal sealed class FailedOutputRetainingStream : Stream
{
    private readonly Stream _inner;

    public FailedOutputRetainingStream(Stream inner)
    {
        _inner = inner;
    }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => _inner.CanWrite;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush()
    {
        _inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _inner.Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _inner.Seek(offset, origin);
    }

    // The restoration call: deliberately not forwarded.
    public override void SetLength(long value)
    {
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override void WriteByte(byte value)
    {
        _inner.WriteByte(value);
    }

#if NET6_0_OR_GREATER
    public override int Read(Span<byte> buffer)
    {
        return _inner.Read(buffer);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _inner.ReadAsync(buffer, cancellationToken);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _inner.WriteAsync(buffer, cancellationToken);
    }
#endif
}
