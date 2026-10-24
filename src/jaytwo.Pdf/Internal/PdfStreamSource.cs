using System;
#if NETSTANDARD2_0
using System.Buffers;
using System.Runtime.InteropServices;
#endif
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// The seekable-stream adapter of spec R13. The snapshot is the stream's length when the adapter is made; a read after the
// length changes fails, except that the document may grow its own source while it appends to it.
internal sealed class PdfStreamSource : IPdfSource
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly bool _detectChange;
    private long _length;

    public PdfStreamSource(Stream stream, bool leaveOpen)
        : this(stream, leaveOpen, true)
    {
    }

    private PdfStreamSource(Stream stream, bool leaveOpen, bool detectChange)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
        _detectChange = detectChange;
        _length = stream.Length;
    }

    public Stream Stream => _stream;

    public long Length => _length;

    // Set while the document appends to this stream, so reads of the prefix see a longer stream.
    public bool Appending { get; set; }

    // A borrowed stream that is not a document snapshot, such as pending attachment content, which checks its own bytes.
    public static PdfStreamSource Borrow(Stream stream)
    {
        return new PdfStreamSource(stream, true, false);
    }

    // The document's own append becomes part of the snapshot.
    public void AcceptAppend()
    {
        _length = _stream.Length;
    }

    public void CheckUnchanged()
    {
        if (!_detectChange)
        {
            return;
        }

        long current = _stream.Length;
        if (Appending ? current < _length : current != _length)
        {
            throw new PdfSourceChangedException("The source stream length changed under the document.");
        }
    }

    public int Read(long position, Span<byte> buffer)
    {
        CheckUnchanged();
        _stream.Seek(position, SeekOrigin.Begin);
#if NETSTANDARD2_0
        byte[] rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            int read = _stream.Read(rented, 0, buffer.Length);
            rented.AsSpan(0, read).CopyTo(buffer);
            return read;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
#else
        return _stream.Read(buffer);
#endif
    }

    public async ValueTask<int> ReadAsync(long position, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckUnchanged();
        _stream.Seek(position, SeekOrigin.Begin);
#if NETSTANDARD2_0
        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
        {
            return await _stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            int read = await _stream.ReadAsync(rented, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            rented.AsMemory(0, read).CopyTo(buffer);
            return read;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
#else
        return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
#endif
    }

    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }
}
