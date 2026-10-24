using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Ergonomics.Pdf.Tests;

// A memory stream that calls a hook before each write and records disposal, so a test can cancel a run partway through
// writing its output.
internal sealed class HookedStream : MemoryStream
{
    public HookedStream()
    {
    }

    public HookedStream(byte[] data)
    {
        Write(data, 0, data.Length);
        Position = 0;
    }

    public Action? BeforeWrite { get; set; }

    public bool Disposed { get; private set; }

    public override void Write(byte[] buffer, int offset, int count)
    {
        BeforeWrite?.Invoke();
        base.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        BeforeWrite?.Invoke();
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

#if NETCOREAPP
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        BeforeWrite?.Invoke();
        base.Write(buffer);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        BeforeWrite?.Invoke();
        return base.WriteAsync(buffer, cancellationToken);
    }
#endif

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
