using System.Diagnostics;
using jaytwo.Pdf;

// Shared counters so a scenario can total many sources.
sealed class Counters
{
    public long SyncReads, AsyncReads, Bytes, Disposed, ReadsAfterCancel;
    public int InFlightSync, PeakInFlightSync;
    public volatile bool Canceled;
    public void Reset() { SyncReads = AsyncReads = Bytes = Disposed = ReadsAfterCancel = 0; InFlightSync = PeakInFlightSync = 0; Canceled = false; }
}

// A random-access snapshot of a local file with a fixed per-read latency. Sync reads block the calling thread for the
// latency (a blocking remote range request); async reads yield for it.
sealed class RangeSource : IPdfSource
{
    private readonly Microsoft.Win32.SafeHandles.SafeFileHandle _handle;
    private readonly int _latencyMs;
    private readonly Counters _c;

    public RangeSource(Microsoft.Win32.SafeHandles.SafeFileHandle handle, int latencyMs, Counters c)
    {
        _handle = handle; _latencyMs = latencyMs; _c = c; Length = RandomAccess.GetLength(handle);
    }

    public long Length { get; }

    public int Read(long position, Span<byte> buffer)
    {
        Interlocked.Increment(ref _c.SyncReads);
        int now = Interlocked.Increment(ref _c.InFlightSync);
        int peak;
        while ((peak = Volatile.Read(ref _c.PeakInFlightSync)) < now && Interlocked.CompareExchange(ref _c.PeakInFlightSync, now, peak) != peak) { }
        try
        {
            if (_latencyMs > 0) Thread.Sleep(_latencyMs);
            int n = RandomAccess.Read(_handle, buffer, position);
            Interlocked.Add(ref _c.Bytes, n);
            return n;
        }
        finally { Interlocked.Decrement(ref _c.InFlightSync); }
    }

    public async ValueTask<int> ReadAsync(long position, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _c.AsyncReads);
        if (_c.Canceled) Interlocked.Increment(ref _c.ReadsAfterCancel);
        if (_latencyMs > 0) await Task.Delay(_latencyMs, cancellationToken).ConfigureAwait(false);
        else cancellationToken.ThrowIfCancellationRequested();
        int n = await RandomAccess.ReadAsync(_handle, buffer, position, cancellationToken).ConfigureAwait(false);
        Interlocked.Add(ref _c.Bytes, n);
        return n;
    }

    public void Dispose() => Interlocked.Increment(ref _c.Disposed);
}

// A seekable Stream that overrides only Read, like a typical adapter over a ranged network resource.
sealed class SyncOnlyStream : Stream
{
    private readonly Microsoft.Win32.SafeHandles.SafeFileHandle _handle;
    private readonly int _latencyMs;
    private readonly Counters _c;
    private long _pos;

    public SyncOnlyStream(Microsoft.Win32.SafeHandles.SafeFileHandle handle, int latencyMs, Counters c)
    { _handle = handle; _latencyMs = latencyMs; _c = c; Length = RandomAccess.GetLength(handle); }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length { get; }
    public override long Position { get => _pos; set => _pos = value; }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => _pos = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => _pos + offset, _ => Length + offset };
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        Interlocked.Increment(ref _c.SyncReads);
        int now = Interlocked.Increment(ref _c.InFlightSync);
        int peak;
        while ((peak = Volatile.Read(ref _c.PeakInFlightSync)) < now && Interlocked.CompareExchange(ref _c.PeakInFlightSync, now, peak) != peak) { }
        try
        {
            if (_latencyMs > 0) Thread.Sleep(_latencyMs);
            int n = RandomAccess.Read(_handle, buffer.AsSpan(offset, count), _pos);
            _pos += n;
            Interlocked.Add(ref _c.Bytes, n);
            return n;
        }
        finally { Interlocked.Decrement(ref _c.InFlightSync); }
    }
}

static class P
{
    public static (long gcBytes, double ms) Time(Action a)
    {
        long before = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();
        a();
        sw.Stop();
        return (GC.GetTotalAllocatedBytes(true) - before, sw.Elapsed.TotalMilliseconds);
    }

    public static async Task<(long gcBytes, double ms)> TimeAsync(Func<Task> a)
    {
        long before = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();
        await a();
        sw.Stop();
        return (GC.GetTotalAllocatedBytes(true) - before, sw.Elapsed.TotalMilliseconds);
    }

    public static double Median(List<double> v) { v.Sort(); return v[v.Count / 2]; }
}

