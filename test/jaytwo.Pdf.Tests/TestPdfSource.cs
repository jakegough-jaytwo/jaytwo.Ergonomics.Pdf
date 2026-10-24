using System;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Tests;

// W-132: an IPdfSource over bytes that records how it is read. It can cap each read, refuse synchronous reads, yield
// before each asynchronous read, claim a length past its bytes, and fail if two reads overlap.
internal sealed class TestPdfSource : IPdfSource
{
    private readonly byte[] _data;
    private readonly long _length;
    private int _active;

    public TestPdfSource(byte[] data, long? claimedLength = null)
    {
        _data = data;
        _length = claimedLength ?? data.Length;
    }

    public long Length => _length;

    public int MaxRead { get; set; } = int.MaxValue;

    public bool AllowSync { get; set; } = true;

    public bool Yield { get; set; }

    // Called with the number of reads so far before each asynchronous read.
    public Action<int>? BeforeAsyncRead { get; set; }

    public int SyncReads { get; private set; }

    public int AsyncReads { get; private set; }

    public bool Disposed { get; private set; }

    public int Read(long position, Span<byte> buffer)
    {
        if (!AllowSync)
        {
            throw new InvalidOperationException("This source must not be read synchronously.");
        }

        Enter();
        try
        {
            SyncReads++;
            return Copy(position, buffer);
        }
        finally
        {
            Exit();
        }
    }

    public async ValueTask<int> ReadAsync(long position, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeforeAsyncRead?.Invoke(AsyncReads);
            AsyncReads++;
            if (Yield)
            {
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Copy(position, buffer.Span);
        }
        finally
        {
            Exit();
        }
    }

    public void Dispose()
    {
        Disposed = true;
    }

    private int Copy(long position, Span<byte> buffer)
    {
        if (Disposed)
        {
            throw new ObjectDisposedException(nameof(TestPdfSource));
        }

        if (position >= _data.Length)
        {
            return 0;
        }

        int count = (int)Math.Min(Math.Min(buffer.Length, MaxRead), _data.Length - position);
        _data.AsSpan((int)position, count).CopyTo(buffer);
        return count;
    }

    private void Enter()
    {
        if (Interlocked.Increment(ref _active) != 1)
        {
            throw new InvalidOperationException("Two reads of one source overlapped.");
        }
    }

    private void Exit()
    {
        Interlocked.Decrement(ref _active);
    }
}
