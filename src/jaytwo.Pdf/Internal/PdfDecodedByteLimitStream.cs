using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.Attachments;

namespace jaytwo.Pdf.Internal;

// Limits bytes returned to a caller. The extra one-byte read proves whether content exists beyond an exact boundary.
internal sealed class PdfDecodedByteLimitStream : Stream
{
    private readonly Stream _inner;
    private readonly long _limit;
    private readonly string _name;
    private readonly byte[] _probe = new byte[1];
    private long _returned;

    public PdfDecodedByteLimitStream(Stream inner, string name, long limit)
    {
        _inner = inner;
        _name = name;
        _limit = limit;
    }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBuffer(buffer, offset, count);
        if (count == 0)
        {
            return 0;
        }

        if (_returned == _limit)
        {
            return FinishOrThrow(_inner.Read(_probe, 0, 1));
        }

        int read = _inner.Read(buffer, offset, Allowed(count));
        _returned += read;
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBuffer(buffer, offset, count);
        if (count == 0)
        {
            return 0;
        }

        if (_returned == _limit)
        {
            int probe = await _inner.ReadAsync(_probe, 0, 1, cancellationToken).ConfigureAwait(false);
            return FinishOrThrow(probe);
        }

        int read = await _inner.ReadAsync(buffer, offset, Allowed(count), cancellationToken).ConfigureAwait(false);
        _returned += read;
        return read;
    }

    public override int ReadByte()
    {
        if (_returned == _limit)
        {
            return FinishReadByteOrThrow(_inner.Read(_probe, 0, 1));
        }

        int value = _inner.ReadByte();
        if (value >= 0)
        {
            _returned++;
        }

        return value;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void ValidateBuffer(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (offset > buffer.Length - count)
        {
            throw new ArgumentException("The offset and count exceed the buffer length.");
        }
    }

    private int Allowed(int requested)
    {
        return (int)Math.Min(requested, _limit - _returned);
    }

    private int FinishOrThrow(int read)
    {
        if (read != 0)
        {
            throw new PdfAttachmentTooLargeException(_name, _limit);
        }

        return 0;
    }

    private int FinishReadByteOrThrow(int read)
    {
        if (read != 0)
        {
            throw new PdfAttachmentTooLargeException(_name, _limit);
        }

        return -1;
    }
}
