using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// A read-only window on the source. Each read is one positional source read of at most 64 KiB.
internal sealed class PdfSliceStream : Stream
{
    private readonly IPdfSource _source;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public PdfSliceStream(IPdfSource source, long start, long length)
    {
        _source = source;
        _start = start;
        _length = length;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _position = value;
        }
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length)
        {
            return 0;
        }

        int read = _source.Read(_start + _position, new Span<byte>(buffer, offset, Allowed(count)));
        _position += read;
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_position >= _length)
        {
            return 0;
        }

        int read = await _source.ReadAsync(_start + _position, new Memory<byte>(buffer, offset, Allowed(count)), cancellationToken).ConfigureAwait(false);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        Position = target;
        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    private int Allowed(int count)
    {
        return (int)Math.Min(count, Math.Min(65536, _length - _position));
    }
}
