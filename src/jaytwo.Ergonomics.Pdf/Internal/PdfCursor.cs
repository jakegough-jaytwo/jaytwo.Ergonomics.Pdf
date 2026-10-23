using System;
using System.Collections.Generic;
using System.IO;

namespace jaytwo.Ergonomics.Pdf.Internal;

// D-49: a stream cursor keeps its most recently filled 64 KiB windows, least recently used first out, and serves a
// position inside one of them without I/O. The engine calls Release when each public operation returns, so no
// source byte read in one operation is served in another.
internal sealed class PdfCursor
{
    internal const int WindowSize = 65536;

    // W-108: chosen from the measurements recorded in the work item. 16 windows is the 1 MiB ceiling.
    internal const int DefaultRetainedWindows = 8;

    private const int MaxRetainedWindows = 16;

    private readonly Stream? _stream;
    private readonly byte[]? _buffer;
    private readonly int _retainedWindows;

    // Most recently used last.
    private readonly List<Window> _windows = new List<Window>();
    private Window? _spare;
    private byte[] _window;
    private long _next;
    private int _unread = -1;
    private int _windowIndex;
    private int _windowLength;
    private long _peakRetainedBytes;

    public PdfCursor(Stream stream)
        : this(stream, DefaultRetainedWindows)
    {
    }

    // Zero retained windows refills on every seek.
    public PdfCursor(Stream stream, int retainedWindows)
    {
        if (retainedWindows < 0 || retainedWindows > MaxRetainedWindows)
        {
            throw new ArgumentOutOfRangeException(nameof(retainedWindows));
        }

        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _retainedWindows = retainedWindows;
        _window = Array.Empty<byte>();
    }

    private PdfCursor(byte[] buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _window = new byte[WindowSize];
    }

    public long Length => _buffer?.Length ?? _stream!.Length;

    public long Position => _unread >= 0 ? _next - 1 : _next;

    public int RetainedWindows => _retainedWindows;

    // The most bytes the retained windows have held at once since the cursor was created.
    public long PeakRetainedBytes => _peakRetainedBytes;

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

    // Drops the retained windows and keeps one buffer for the next fill. The position is unchanged.
    public void Release()
    {
        if (_unread >= 0)
        {
            _unread = -1;
            _next--;
        }

        _windowIndex = 0;
        _windowLength = 0;
        if (_windows.Count > 0)
        {
            _spare ??= _windows[0];
            _windows.Clear();
        }
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
        _windowIndex = 0;
        _windowLength = 0;
        if (_buffer != null)
        {
            if (_next >= _buffer.Length)
            {
                return;
            }

            int count = (int)Math.Min(_window.Length, _buffer.Length - _next);
            Buffer.BlockCopy(_buffer, (int)_next, _window, 0, count);
            _windowLength = count;
            return;
        }

        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            Window retained = _windows[i];
            if (_next >= retained.Start && _next - retained.Start < retained.Length)
            {
                if (i != _windows.Count - 1)
                {
                    _windows.RemoveAt(i);
                    _windows.Add(retained);
                }

                _window = retained.Data;
                _windowIndex = (int)(_next - retained.Start);
                _windowLength = retained.Length;
                return;
            }
        }

        // Past the end reads as EOF, as in the buffer path. Some streams, such as MemoryStream, refuse such a seek.
        // The length is read again on every fill, so a window that ended at an earlier end is not taken as EOF
        // after the document appends to its own source.
        if (_next >= _stream!.Length)
        {
            return;
        }

        Window window;
        if (_retainedWindows > 0 && _windows.Count >= _retainedWindows)
        {
            window = _windows[0];
            _windows.RemoveAt(0);
        }
        else if (_spare != null)
        {
            window = _spare;
            _spare = null;
        }
        else
        {
            window = new Window(new byte[WindowSize]);
        }

        _stream.Seek(_next, SeekOrigin.Begin);
        window.Start = _next;
        window.Length = _stream.Read(window.Data, 0, WindowSize);
        if (window.Length > 0 && _retainedWindows > 0)
        {
            _windows.Add(window);
            _peakRetainedBytes = Math.Max(_peakRetainedBytes, (long)_windows.Count * WindowSize);
        }
        else
        {
            _spare = window;
        }

        _window = window.Data;
        _windowLength = window.Length;
    }

    private sealed class Window
    {
        public Window(byte[] data)
        {
            Data = data;
        }

        public byte[] Data { get; }

        public long Start { get; set; }

        public int Length { get; set; }
    }
}
