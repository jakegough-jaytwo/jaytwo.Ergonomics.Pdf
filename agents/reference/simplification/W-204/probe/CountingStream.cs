using System;
using System.IO;

internal sealed class CountingStream : MemoryStream
{
    public CountingStream(byte[] bytes)
    {
        Write(bytes);
        Position = 0;
    }

    public long BytesRead { get; private set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = base.Read(buffer, offset, count);
        BytesRead += read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = base.Read(buffer);
        BytesRead += read;
        return read;
    }
}
