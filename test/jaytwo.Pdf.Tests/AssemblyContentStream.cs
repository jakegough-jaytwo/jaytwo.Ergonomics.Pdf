using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace jaytwo.Pdf.Tests;

// Original generated PDF: a valid, unfiltered whitespace content stream synthesized without allocating its body.
internal sealed class AssemblyContentStream : Stream
{
    private readonly byte[] _head;
    private readonly byte[] _tail;
    private readonly long _tailOffset;

    internal AssemblyContentStream(long contentLength)
    {
        var builder = PdfBuilder.Basic(pageExtra: " /Contents 4 0 R");
        long contentOffset = builder.Length;
        builder.Hole(4, contentOffset);
        builder.Ascii("4 0 obj\n<< /Length " + contentLength.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
        _head = builder.Snapshot();
        _tailOffset = _head.Length + contentLength;
        const string end = "\nendstream\nendobj\n";
        var split = builder.Split(_tailOffset + end.Length, string.Empty);
        byte[] ending = Encoding.ASCII.GetBytes(end);
        _tail = new byte[ending.Length + split.Tail.Length];
        Buffer.BlockCopy(ending, 0, _tail, 0, ending.Length);
        Buffer.BlockCopy(split.Tail, 0, _tail, ending.Length, split.Tail.Length);
    }

    public long BytesRead { get; private set; }

    public int MaxRequested { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _tailOffset + _tail.Length;

    public override long Position { get; set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        MaxRequested = Math.Max(MaxRequested, count);
        int read = (int)Math.Min(count, Length - Position);
        if (Position < _head.Length)
        {
            read = (int)Math.Min(read, _head.Length - Position);
            Buffer.BlockCopy(_head, (int)Position, buffer, offset, read);
        }
        else if (Position < _tailOffset)
        {
            read = (int)Math.Min(read, _tailOffset - Position);
            for (int index = 0; index < read; index++)
            {
                buffer[offset + index] = (byte)' ';
            }
        }
        else
        {
            Buffer.BlockCopy(_tail, (int)(Position - _tailOffset), buffer, offset, read);
        }

        Position += read;
        BytesRead += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        return Position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
