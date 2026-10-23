using System;
using System.IO;

namespace jaytwo.Ergonomics.Pdf.Tests;

internal sealed class ProbeStream : Stream
{
    private readonly Stream _inner;
    private readonly bool _allowRead;

    public ProbeStream(Stream inner, bool allowRead = true)
    {
        _inner = inner;
        _allowRead = allowRead;
    }

    public long BytesRead { get; private set; }

    public int ReadCalls { get; private set; }

    public int MaxRequested { get; private set; }

    public bool ReadCalled { get; private set; }

    public bool Disposed { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => _allowRead && _inner.CanSeek;

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

    public override int Read(byte[] buffer, int offset, int count)
    {
        ReadCalled = true;
        ReadCalls++;
        MaxRequested = Math.Max(MaxRequested, count);
        if (!_allowRead)
        {
            throw new InvalidOperationException("This stream must not be read.");
        }

        int read = _inner.Read(buffer, offset, count);
        BytesRead += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
