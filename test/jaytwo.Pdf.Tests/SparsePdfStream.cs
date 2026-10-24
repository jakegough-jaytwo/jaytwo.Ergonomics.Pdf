using System;
using System.IO;

namespace jaytwo.Pdf.Tests;

internal sealed class SparsePdfStream : Stream
{
    private readonly byte[] _head;
    private readonly byte[] _tail;
    private readonly long _tailOffset;
    private readonly MemoryStream _appendix = new MemoryStream();
    private readonly long _originalLength;
    private long _length;
    private long _position;

    public SparsePdfStream(byte[] head, byte[] tail, long tailOffset)
    {
        _head = head;
        _tail = tail;
        _tailOffset = tailOffset;
        _originalLength = tailOffset + tail.Length;
        _length = _originalLength;
    }

    public long BytesRead { get; private set; }

    public int MaxRequested { get; private set; }

    public bool TouchedHole { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        MaxRequested = Math.Max(MaxRequested, count);
        if (count == 0 || _position >= _length)
        {
            return 0;
        }

        if (_position < _head.Length)
        {
            int available = (int)Math.Min(count, _head.Length - _position);
            Buffer.BlockCopy(_head, (int)_position, buffer, offset, available);
            _position += available;
            BytesRead += available;
            return available;
        }

        if (_position < _tailOffset)
        {
            TouchedHole = true;
            throw new InvalidOperationException("A read started inside the unread middle of the PDF.");
        }

        long tailEnd = _tailOffset + _tail.Length;
        if (_position < tailEnd)
        {
            int available = (int)Math.Min(count, tailEnd - _position);
            Buffer.BlockCopy(_tail, (int)(_position - _tailOffset), buffer, offset, available);
            _position += available;
            BytesRead += available;
            return available;
        }

        if (_position < _originalLength)
        {
            TouchedHole = true;
            throw new InvalidOperationException("A read started inside the unread middle of the PDF.");
        }

        int appendixAt = (int)(_position - _originalLength);
        int appendixCount = (int)Math.Min(count, _appendix.Length - appendixAt);
        if (appendixCount <= 0)
        {
            return 0;
        }

        Buffer.BlockCopy(_appendix.GetBuffer(), appendixAt, buffer, offset, appendixCount);
        _position += appendixCount;
        BytesRead += appendixCount;
        return appendixCount;
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
        if (target < 0)
        {
            throw new IOException("Seek is before the start of the stream.");
        }

        _position = target;
        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_position != _length)
        {
            throw new NotSupportedException("This sparse PDF stream only appends.");
        }

        _appendix.Write(buffer, offset, count);
        _position += count;
        _length += count;
    }
}
