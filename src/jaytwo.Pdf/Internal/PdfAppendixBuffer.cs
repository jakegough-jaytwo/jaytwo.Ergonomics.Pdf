using System;
using System.IO;
using jaytwo.Pdf.Signatures;

namespace jaytwo.Pdf.Internal;

// Holds a signature appendix in memory and refuses any write that would make it longer than
// MaxLength. Capacity grows by doubling but never past MaxLength, so the buffer is bounded.
internal sealed class PdfAppendixBuffer : MemoryStream
{
    public const int MaxLength = 8 * 1024 * 1024;

    public const string TooLargeMessage = "The signature appendix would exceed 8 MiB. Call SaveIncremental before Sign.";

    public override void Write(byte[] buffer, int offset, int count)
    {
        Reserve(count);
        base.Write(buffer, offset, count);
    }

#if !NETSTANDARD2_0
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Reserve(buffer.Length);
        base.Write(buffer);
    }
#endif

    public override void WriteByte(byte value)
    {
        Reserve(1);
        base.WriteByte(value);
    }

    public override void SetLength(long value)
    {
        if (value > MaxLength)
        {
            throw new PdfSignException(TooLargeMessage);
        }

        if (value > Capacity)
        {
            Capacity = (int)value;
        }

        base.SetLength(value);
    }

    private void Reserve(int count)
    {
        long end = Position + Math.Max(count, 0);
        if (end > MaxLength)
        {
            throw new PdfSignException(TooLargeMessage);
        }

        if (end > Capacity)
        {
            Capacity = (int)Math.Min(MaxLength, Math.Max(end, Math.Max(256L, Capacity * 2L)));
        }
    }
}
