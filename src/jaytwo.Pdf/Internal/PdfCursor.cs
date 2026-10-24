using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// D-49: a source cursor keeps its most recently filled 64 KiB windows, least recently used first out, and serves a
// position inside one of them without I/O. The engine calls Release when each public operation returns, so no
// source byte read in one operation is served in another. These windows are the bounded cache and read coalescing
// above the source (spec R13): a fill reads one 64 KiB window, so neighboring small reads cost one source read.
// D-55: a byte in the current window is returned without a state machine; only a fill awaits the source.
internal sealed class PdfCursor
{
    internal const int WindowSize = 65536;

    // W-108: chosen from the measurements recorded in the work item. 16 windows is the 1 MiB ceiling.
    internal const int DefaultRetainedWindows = 8;

    private const int MaxRetainedWindows = 16;

    private readonly IPdfSource? _source;
    private readonly byte[]? _buffer;
    private readonly int _retainedWindows;

    // Most recently used last.
    private readonly List<Window> _windows = new List<Window>();
    private Window? _spare;
    private byte[] _window;
    private long _next;
    private int _windowIndex;
    private int _windowLength;
    private long _peakRetainedBytes;

    public PdfCursor(IPdfSource source)
        : this(source, DefaultRetainedWindows)
    {
    }

    // Zero retained windows refills on every seek.
    public PdfCursor(IPdfSource source, int retainedWindows)
    {
        if (retainedWindows < 0 || retainedWindows > MaxRetainedWindows)
        {
            throw new ArgumentOutOfRangeException(nameof(retainedWindows));
        }

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _retainedWindows = retainedWindows;
        _window = Array.Empty<byte>();
    }

    // W-115: the buffer is the window, so a cursor over a decoded object stream copies nothing.
    private PdfCursor(byte[] buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _window = buffer;
    }

    public long Length => _buffer?.Length ?? _source!.Length;

    public long Position => _next;

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

        _next = position;
        _windowIndex = 0;
        _windowLength = 0;
    }

    // Drops the retained windows and keeps one buffer for the next fill. The position is unchanged.
    public void Release()
    {
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
        return PdfSync.Result(PeekByteAsync(false, CancellationToken.None));
    }

    public int ReadByte()
    {
        return PdfSync.Result(ReadByteAsync(false, CancellationToken.None));
    }

    public ValueTask<int> PeekByteAsync(bool useAsync, CancellationToken cancellationToken)
    {
        if (_windowIndex < _windowLength)
        {
            return new ValueTask<int>(_window[_windowIndex]);
        }

        return PeekAfterFillAsync(useAsync, cancellationToken);
    }

    public ValueTask<int> ReadByteAsync(bool useAsync, CancellationToken cancellationToken)
    {
        if (_windowIndex < _windowLength)
        {
            _next++;
            return new ValueTask<int>(_window[_windowIndex++]);
        }

        return ReadAfterFillAsync(useAsync, cancellationToken);
    }

    public void Skip(long count)
    {
        if (count < 0)
        {
            throw new PdfParseException(Position, "Cannot skip backwards.");
        }

        Seek(Position + count);
    }

    // A source may return fewer bytes than asked; the window is then shorter and the next fill continues after it.
    private static int ReadWindow(IPdfSource source, long start, byte[] data)
    {
        return source.Read(start, new Span<byte>(data, 0, WindowSize));
    }

    private async ValueTask<int> PeekAfterFillAsync(bool useAsync, CancellationToken cancellationToken)
    {
        await FillAsync(useAsync, cancellationToken).ConfigureAwait(false);
        return _windowIndex < _windowLength ? _window[_windowIndex] : -1;
    }

    private async ValueTask<int> ReadAfterFillAsync(bool useAsync, CancellationToken cancellationToken)
    {
        await FillAsync(useAsync, cancellationToken).ConfigureAwait(false);
        if (_windowIndex >= _windowLength)
        {
            return -1;
        }

        _next++;
        return _window[_windowIndex++];
    }

    private async ValueTask FillAsync(bool useAsync, CancellationToken cancellationToken)
    {
        _windowIndex = 0;
        _windowLength = 0;
        if (_buffer != null)
        {
            if (_next >= _buffer.Length)
            {
                return;
            }

            _windowIndex = (int)_next;
            _windowLength = _buffer.Length;
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

        // Past the end reads as EOF, as in the buffer path. The length is read again on every fill, so a window that
        // ended at an earlier end is not taken as EOF after the document appends to its own source.
        if (_next >= _source!.Length)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
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

        long start = _next;
        int length = useAsync
            ? await _source.ReadAsync(start, new Memory<byte>(window.Data, 0, WindowSize), cancellationToken).ConfigureAwait(false)
            : ReadWindow(_source, start, window.Data);
        window.Start = start;
        window.Length = length;
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
