using System;
using System.IO;

namespace jaytwo.Ergonomics.Pdf.Internal;

internal sealed class PdfSliceStream : Stream
{
    private readonly Stream _source;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public PdfSliceStream(Stream source, long start, long length)
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

        int allowed = (int)Math.Min(count, Math.Min(65536, _length - _position));
        _source.Seek(_start + _position, SeekOrigin.Begin);
        int read = _source.Read(buffer, offset, allowed);
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
}
