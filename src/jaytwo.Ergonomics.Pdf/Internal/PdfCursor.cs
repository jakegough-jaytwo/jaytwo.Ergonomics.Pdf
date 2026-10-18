using System;
using System.IO;

namespace jaytwo.Ergonomics.Pdf.Internal;

internal sealed class PdfCursor
{
    private readonly Stream? _stream;
    private readonly byte[]? _buffer;
    private readonly byte[] _window = new byte[65536];
    private long _next;
    private int _unread = -1;
    private int _windowIndex;
    private int _windowLength;

    public PdfCursor(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    private PdfCursor(byte[] buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    public long Length => _buffer?.Length ?? _stream!.Length;

    public long Position => _unread >= 0 ? _next - 1 : _next;

    public static PdfCursor FromBytes(byte[] buffer)
    {
        return new PdfCursor(buffer);
    }

    public void Seek(long position)
    {
        if (position < 0)
        {
            throw new PdfParseException(position, "Seek offset is negative.");
        }

        _unread = -1;
        _next = position;
        _windowIndex = 0;
        _windowLength = 0;
    }

    public int PeekByte()
    {
        int value = ReadByte();
        if (value >= 0)
        {
            Unread(value);
        }

        return value;
    }

    public int ReadByte()
    {
        if (_unread >= 0)
        {
            int value = _unread;
            _unread = -1;
            return value;
        }

        if (_windowIndex >= _windowLength)
        {
            Fill();
            if (_windowIndex >= _windowLength)
            {
                return -1;
            }
        }

        _next++;
        return _window[_windowIndex++];
    }

    public void Skip(long count)
    {
        if (count < 0)
        {
            throw new PdfParseException(Position, "Cannot skip backwards.");
        }

        Seek(Position + count);
    }

    private void Unread(int value)
    {
        if (_unread >= 0)
        {
            throw new InvalidOperationException("Only one unread byte is supported.");
        }

        _unread = value;
    }

    private void Fill()
    {
        if (_buffer != null)
        {
            if (_next >= _buffer.Length)
            {
                _windowLength = 0;
                _windowIndex = 0;
                return;
            }

            int count = (int)Math.Min(_window.Length, _buffer.Length - _next);
            Buffer.BlockCopy(_buffer, (int)_next, _window, 0, count);
            _windowLength = count;
            _windowIndex = 0;
            return;
        }

        _stream!.Seek(_next, SeekOrigin.Begin);
        _windowLength = _stream.Read(_window, 0, _window.Length);
        _windowIndex = 0;
    }
}
