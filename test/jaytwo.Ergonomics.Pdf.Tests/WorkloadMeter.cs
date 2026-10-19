using System;
using System.Diagnostics;
using System.Threading;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Measures one operation the way DenseMemoryTests does, and adds timing and the sample count.
// Baseline: GC.GetTotalMemory(true) after a full blocking collection, before the operation starts.
// Peak: the largest GC.GetTotalMemory(false) seen by a background thread that samples about every
// millisecond while the operation runs, plus one final sample taken before any collection after it
// returns. GetTotalMemory(false) counts the managed heap including garbage not yet collected, so the
// growth figure is an upper bound on live managed memory. Native memory and file-system cache are not
// counted. Allocated: TestCompat.TotalAllocatedBytes() after minus before.
internal static class WorkloadMeter
{
    public static Sample Measure(Action operation)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long heapBefore = GC.GetTotalMemory(true);
        long allocatedBefore = TestCompat.TotalAllocatedBytes();
        long peak = heapBefore;
        long samples = 0;
        using var done = new ManualResetEventSlim(false);
        var sampler = new Thread(() =>
        {
            while (!done.IsSet)
            {
                Raise(ref peak, GC.GetTotalMemory(false));
                Interlocked.Increment(ref samples);
                Thread.Sleep(1);
            }
        });
        sampler.IsBackground = true;
        sampler.Start();
        var clock = Stopwatch.StartNew();
        try
        {
            operation();
        }
        finally
        {
            clock.Stop();
            Raise(ref peak, GC.GetTotalMemory(false));
            done.Set();
            sampler.Join();
        }

        long allocated = TestCompat.TotalAllocatedBytes() - allocatedBefore;
        return new Sample(Math.Max(0, Interlocked.Read(ref peak) - heapBefore), allocated, clock.Elapsed, Interlocked.Read(ref samples));
    }

    private static void Raise(ref long peak, long now)
    {
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
    }

    public readonly struct Sample
    {
        public Sample(long peakHeapGrowth, long allocated, TimeSpan elapsed, long samples)
        {
            PeakHeapGrowth = peakHeapGrowth;
            Allocated = allocated;
            Elapsed = elapsed;
            Samples = samples;
        }

        public long PeakHeapGrowth { get; }

        public long Allocated { get; }

        public TimeSpan Elapsed { get; }

        public long Samples { get; }

        public string Describe(string workload, long budget)
        {
            return workload
                + ": peak heap growth " + Mib(PeakHeapGrowth)
                + " MiB, allocated " + Mib(Allocated)
                + " MiB, time " + Elapsed.TotalSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                + " s, samples " + Samples.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ", budget " + Mib(budget) + " MiB";
        }

        private static string Mib(long bytes)
        {
            return (bytes / (1024.0 * 1024.0)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
