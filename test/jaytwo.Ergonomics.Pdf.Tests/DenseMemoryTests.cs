using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

[Collection("dense-memory")]
public class DenseMemoryTests
{
    [Fact]
    public void peak_managed_memory_does_not_follow_a_dense_file()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string smallPath = Path.Combine(directory, "small.pdf");
            string largePath = Path.Combine(directory, "large.pdf");
            string streamedPath = Path.Combine(directory, "streamed.pdf");
            WriteDense(smallPath, 8 * 1024 * 1024);
            WriteDense(largePath, 32 * 1024 * 1024);
            string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
            using (Process process = ExternalTool.Start(qpdf, "--object-streams=generate", "--stream-data=preserve", largePath, streamedPath))
            {
                ExternalTool.Output(process);
            }

            long smallLength = new FileInfo(smallPath).Length;
            long largeLength = new FileInfo(largePath).Length;
            long streamedLength = new FileInfo(streamedPath).Length;
            Assert.True(smallLength > (8 * 1024 * 1024));
            Assert.True(largeLength > (32 * 1024 * 1024));
            Assert.True(streamedLength > (32 * 1024 * 1024));
            Assert.Contains("/ObjStm", ReadHead(streamedPath), StringComparison.Ordinal);

            MemorySample small = Measure(smallPath);
            MemorySample large = Measure(largePath);
            MemorySample streamed = Measure(streamedPath);
            Assert.True(large.Allocated < (8 * 1024 * 1024), "allocated " + large.Allocated.ToString() + " for " + largeLength.ToString());
            Assert.True(large.PeakHeap < (8 * 1024 * 1024), "peak heap delta " + large.PeakHeap.ToString());
            Assert.True(streamed.Allocated < (8 * 1024 * 1024), "object-stream allocated " + streamed.Allocated.ToString());
            Assert.True(Math.Abs(large.Allocated - small.Allocated) < (3 * 1024 * 1024), "allocated grew from " + small.Allocated.ToString() + " to " + large.Allocated.ToString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MemorySample Measure(string path)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long heapBefore = GC.GetTotalMemory(true);
        long allocatedBefore = TestCompat.TotalAllocatedBytes();
        long peak = heapBefore;
        using var done = new ManualResetEventSlim(false);
        var sampler = new Thread(() =>
        {
            while (!done.IsSet)
            {
                long now = GC.GetTotalMemory(false);
                long seen = Interlocked.Read(ref peak);
                while (now > seen)
                {
                    long replaced = Interlocked.CompareExchange(ref peak, now, seen);
                    if (replaced == seen)
                    {
                        break;
                    }

                    seen = replaced;
                }

                Thread.Sleep(1);
            }
        });
        sampler.IsBackground = true;
        sampler.Start();
        string destinationPath = path + ".revised.pdf";
        using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var document = PdfDocument.Open(source, leaveOpen: true))
        {
            Assert.Equal("Dense", document.Metadata.Title);
            document.Metadata.Title = "Touched";
            using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            document.SaveIncremental(destination, leaveOpen: true);
        }

        done.Set();
        sampler.Join();
        using (var check = new FileStream(destinationPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var document = PdfDocument.Open(check, leaveOpen: true))
        {
            Assert.Equal("Touched", document.Metadata.Title);
        }

        return new MemorySample(TestCompat.TotalAllocatedBytes() - allocatedBefore, Math.Max(0, peak - heapBefore));
    }

    private static string ReadHead(string path)
    {
        var head = new byte[4096];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        int read = stream.Read(head, 0, head.Length);
        return Encoding.ASCII.GetString(head, 0, read);
    }

    private static void WriteDense(string path, int contentLength)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(stream, "%PDF-1.4\n");
        long[] offsets = new long[5];
        offsets[0] = stream.Position;
        Write(stream, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        offsets[1] = stream.Position;
        Write(stream, "2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n");
        offsets[2] = stream.Position;
        Write(stream, "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Contents 4 0 R >>\nendobj\n");
        offsets[3] = stream.Position;
        Write(stream, "4 0 obj\n<< /Length " + contentLength.ToString() + " >>\nstream\n");
        var chunk = new byte[1024 * 1024];
        for (int i = 0; i < chunk.Length; i++)
        {
            chunk[i] = (byte)'A';
        }

        int remaining = contentLength;
        while (remaining > 0)
        {
            int count = Math.Min(chunk.Length, remaining);
            stream.Write(chunk, 0, count);
            remaining -= count;
        }

        Write(stream, "\nendstream\nendobj\n");
        offsets[4] = stream.Position;
        Write(stream, "5 0 obj\n<< /Title (Dense) >>\nendobj\n");
        long xref = stream.Position;
        var table = new StringBuilder();
        table.Append("xref\n0 6\n");
        table.Append("0000000000 65535 f \n");
        for (int i = 0; i < offsets.Length; i++)
        {
            table.Append(offsets[i].ToString("0000000000"));
            table.Append(" 00000 n \n");
        }

        table.Append("trailer\n<< /Size 6 /Root 1 0 R /Info 5 0 R >>\nstartxref\n");
        table.Append(xref.ToString());
        table.Append("\n%%EOF\n");
        Write(stream, table.ToString());
    }

    private static void Write(Stream stream, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private readonly struct MemorySample
    {
        public MemorySample(long allocated, long peakHeap)
        {
            Allocated = allocated;
            PeakHeap = peakHeap;
        }

        public long Allocated { get; }

        public long PeakHeap { get; }
    }
}
