using System;
using System.IO;

namespace jaytwo.Pdf.Internal;

internal sealed class SplicedStream : Stream
{
    private readonly Piece[] _pieces;
    private int _index;
    private long _offset;

    public SplicedStream(Piece[] pieces)
    {
        _pieces = pieces;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length
    {
        get
        {
            long total = 0;
            foreach (Piece piece in _pieces)
            {
                total += piece.Count;
            }

            return total;
        }
    }

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
        int written = 0;
        while (written < count && _index < _pieces.Length)
        {
            Piece piece = _pieces[_index];
            if (_offset >= piece.Count)
            {
                _index++;
                _offset = 0;
                continue;
            }

            int wanted = (int)Math.Min(count - written, Math.Min(1024 * 1024, piece.Count - _offset));
            piece.Source.Seek(piece.Start + _offset, SeekOrigin.Begin);
            int read = piece.Source.Read(buffer, offset + written, wanted);
            if (read == 0)
            {
                throw new EndOfStreamException("Signed byte range ended early.");
            }

            _offset += read;
            written += read;
        }

        return written;
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

    internal readonly struct Piece
    {
        public Piece(Stream source, long start, long count)
        {
            Source = source;
            Start = start;
            Count = count;
        }

        public Stream Source { get; }

        public long Start { get; }

        public long Count { get; }
    }
}
