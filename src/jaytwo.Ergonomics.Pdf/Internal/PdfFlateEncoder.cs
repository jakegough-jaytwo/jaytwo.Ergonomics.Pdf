using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace jaytwo.Ergonomics.Pdf.Internal;

// Streams caller content into a PDF stream body, optionally as a zlib stream (RFC 1950, the FlateDecode format of
// ISO 32000-1 7.4.4), and hashes the bytes read. Memory is one 64 KiB buffer plus the deflater's state. The buffer is
// filled before each write, so two passes over the same bytes feed the deflater the same chunks and emit the same
// output; the attachment body relies on that to know its /Length before it is written.
internal static class PdfFlateEncoder
{
    private const int ChunkSize = 65536;
    private const int AdlerModulus = 65521;

    // The largest run of bytes whose Adler-32 sums cannot overflow a uint before the modulus is taken (zlib NMAX).
    private const int AdlerBlock = 5552;

    // CMF 0x78 (deflate, 32 KiB window) and FLG 0x9C (default level, check bits), as zlib writes them.
    private static readonly byte[] ZlibHeader = { 0x78, 0x9C };

    // A final fixed-Huffman block holding only the end-of-block code, the deflate encoding of no bytes.
    private static readonly byte[] EmptyFinalBlock = { 0x03, 0x00 };

    // Copies length bytes of source from start to destination, or only measures them when destination is null.
    // Returns the MD5 of the bytes read and the number of bytes written.
    public static (byte[] Md5, long Written) Copy(Stream source, long start, long length, Stream? destination, bool compress)
    {
        var counter = new CountingStream(destination);
        using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        source.Seek(start, SeekOrigin.Begin);
        var buffer = new byte[ChunkSize];
        if (!compress)
        {
            Pump(source, length, buffer, md5, counter, null);
            return (md5.GetHashAndReset(), counter.Count);
        }

        counter.Write(ZlibHeader, 0, ZlibHeader.Length);
        uint adler;
        using (var deflate = new DeflateStream(counter, CompressionLevel.Optimal, leaveOpen: true))
        {
            adler = Pump(source, length, buffer, md5, deflate, 1);
        }

        // A deflater given no input can emit no block at all, which is not a complete deflate stream (RFC 1951 3.2.3).
        if (counter.Count == ZlibHeader.Length)
        {
            counter.Write(EmptyFinalBlock, 0, EmptyFinalBlock.Length);
        }

        var trailer = new[] { (byte)(adler >> 24), (byte)(adler >> 16), (byte)(adler >> 8), (byte)adler };
        counter.Write(trailer, 0, trailer.Length);
        return (md5.GetHashAndReset(), counter.Count);
    }

    private static uint Pump(Stream source, long length, byte[] buffer, IncrementalHash md5, Stream target, uint? adler)
    {
        uint sum = adler ?? 0;
        long remaining = length;
        while (remaining > 0)
        {
            int wanted = (int)Math.Min(buffer.Length, remaining);
            int filled = 0;
            while (filled < wanted)
            {
                int read = source.Read(buffer, filled, wanted - filled);
                if (read == 0)
                {
                    throw new EndOfStreamException("Attachment stream ended before its declared length.");
                }

                filled += read;
            }

            md5.AppendData(buffer, 0, filled);
            if (adler != null)
            {
                sum = Adler32(sum, buffer, filled);
            }

            target.Write(buffer, 0, filled);
            remaining -= filled;
        }

        return sum;
    }

    private static uint Adler32(uint adler, byte[] data, int count)
    {
        uint a = adler & 0xFFFF;
        uint b = adler >> 16;
        int offset = 0;
        while (offset < count)
        {
            int end = Math.Min(count, offset + AdlerBlock);
            for (; offset < end; offset++)
            {
                a += data[offset];
                b += a;
            }

            a %= AdlerModulus;
            b %= AdlerModulus;
        }

        return (b << 16) | a;
    }

    // Counts the bytes written and forwards them, or discards them when there is no destination.
    private sealed class CountingStream : Stream
    {
        private readonly Stream? _inner;

        public CountingStream(Stream? inner)
        {
            _inner = inner;
        }

        public long Count { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner?.Write(buffer, offset, count);
            Count += count;
        }

        public override void Flush()
        {
            _inner?.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
