using System;
using System.Diagnostics;
using System.Globalization;
using Xunit.Abstractions;

namespace jaytwo.Ergonomics.Pdf.Comparison;

// Runs one workload for one library: a cold pass, then warm passes, each measured by WorkloadMeter. The operation
// returns the source bytes it read, or -1 when the library opens the file itself. `verify` checks the last pass's
// output outside every measured window. The result is one tab-separated COMPARE line that tools/comparison/report.py
// tabulates; the peak working set is the process's, so `make compare-itext` runs each test in its own process.
internal static class ComparisonRun
{
    public static void Measure(ITestOutputHelper output, string workload, string library, int passes, Func<long> operation, Action verify)
    {
        using Process self = Process.GetCurrentProcess();
        self.Refresh();
        long baselineWorkingSet = self.WorkingSet64;
        var samples = new WorkloadMeter.Sample[passes];
        long sourceBytes = -1;
        for (int pass = 0; pass < passes; pass++)
        {
            ComparisonFixtures.ClearOutput();
            long read = -1;
            samples[pass] = WorkloadMeter.Measure(() => read = operation());
            sourceBytes = read;
            output.WriteLine(
                workload + ", " + library + ", pass " + (pass + 1).ToString(CultureInfo.InvariantCulture)
                + ": time " + Seconds(samples[pass].Elapsed)
                + " s, peak heap growth " + Mib(samples[pass].PeakHeapGrowth)
                + " MiB, allocated " + Mib(samples[pass].Allocated) + " MiB");
        }

        verify();
        self.Refresh();
        long peakWorkingSet = ComparisonFixtures.GeneratedInProcess ? -1 : self.PeakWorkingSet64;
        // Warm time is the fastest pass after the first; peak heap growth is the largest of any pass.
        TimeSpan warm = samples[passes > 1 ? 1 : 0].Elapsed;
        long peakHeap = 0;
        for (int pass = 0; pass < passes; pass++)
        {
            if (pass > 0 && samples[pass].Elapsed < warm)
            {
                warm = samples[pass].Elapsed;
            }

            peakHeap = Math.Max(peakHeap, samples[pass].PeakHeapGrowth);
        }

        output.WriteLine(string.Join(
            "\t",
            "COMPARE",
            workload,
            library,
            passes.ToString(CultureInfo.InvariantCulture),
            Seconds(samples[0].Elapsed),
            Seconds(warm),
            Mib(peakHeap),
            Mib(samples[passes - 1].Allocated),
            peakWorkingSet < 0 ? "-" : Mib(peakWorkingSet),
            Mib(baselineWorkingSet),
            sourceBytes < 0 ? "-" : sourceBytes.ToString(CultureInfo.InvariantCulture)));
    }

    private static string Seconds(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static string Mib(long bytes)
    {
        return (bytes / (1024.0 * 1024.0)).ToString("0.00", CultureInfo.InvariantCulture);
    }
}
