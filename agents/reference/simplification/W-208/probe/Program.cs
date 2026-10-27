// W-208 disposable probe. Public API of jaytwo.Pdf only. Measures source reads, allocation, blocking and cancellation
// of the current open/inspection/save paths against a local file and a delayed range-backed source.
// Usage: Probe <fixture.pdf>. Prints one JSON object per measurement; nothing here is product code.
using System.Diagnostics;
using System.Text.Json;
using jaytwo.Ergonomics.Pdf;
using jaytwo.Pdf;

var fixture = args[0];
var results = new List<object>();
var json = new JsonSerializerOptions { WriteIndented = false };

void Emit(string scenario, object data) => Console.WriteLine(JsonSerializer.Serialize(new { scenario, data }, json));

var handle = File.OpenHandle(fixture, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.Asynchronous | FileOptions.RandomAccess);
long fileLength = RandomAccess.GetLength(handle);
var c = new Counters();
Emit("environment", new
{
    fixtureBytes = fileLength,
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    processors = Environment.ProcessorCount,
    poolThreadsAtStart = ThreadPool.ThreadCount,
    minWorkerThreads = MinWorkers(),
    gcServer = System.Runtime.GCSettings.IsServerGC,
});

// Warm the JIT so allocation and time are not first-call costs.
for (int i = 0; i < 2; i++) { using var w = PdfDocument.Open(new RangeSource(handle, 0, new Counters()), leaveOpen: true); _ = w.PageCount; }

// 1. Open: sync versus async, local and delayed.
foreach (int latency in new[] { 0, 10 })
{
    var syncTimes = new List<double>(); var asyncTimes = new List<double>();
    long syncAlloc = 0, asyncAlloc = 0;
    for (int run = 0; run < 5; run++)
    {
        c.Reset();
        var s = P.Time(() => { using var d = PdfDocument.Open(new RangeSource(handle, latency, c), leaveOpen: true); });
        syncTimes.Add(s.ms); syncAlloc = s.gcBytes;
        if (run == 0) Emit("open_sync_counts", new { latency, c.SyncReads, c.AsyncReads, c.Bytes, fileLength, fractionOfFile = (double)c.Bytes / fileLength });
        c.Reset();
        var a = await P.TimeAsync(async () => { using var d = await PdfDocument.OpenAsync(new RangeSource(handle, latency, c), leaveOpen: true); });
        asyncTimes.Add(a.ms); asyncAlloc = a.gcBytes;
        if (run == 0) Emit("open_async_counts", new { latency, c.SyncReads, c.AsyncReads, c.Bytes, peakBlockedThreads = c.PeakInFlightSync });
    }
    Emit("open_compare", new { latency, runs = 5, syncMedianMs = P.Median(syncTimes), asyncMedianMs = P.Median(asyncTimes), syncAllocatedBytes = syncAlloc, asyncAllocatedBytes = asyncAlloc });
}

// 2. After an asynchronous open, which members read synchronously? Fresh document per member (local, zero latency).
var members = new List<(string name, Action<PdfDocument> act)>
{
    ("PageCount", d => _ = d.PageCount),
    ("Pages", d => _ = d.Pages.Count),
    ("Permissions", d => _ = d.Permissions),
    ("AuthenticationRole", d => _ = d.AuthenticationRole),
    ("Signatures", d => _ = d.Signatures.Count),
    ("Revisions", d => _ = d.Revisions.Count),
    ("AcroForm.HasFields", d => _ = d.AcroForm.HasFields),
    ("AcroForm.NeedAppearances", d => _ = d.AcroForm.NeedAppearances),
    ("AcroForm.IsHybridXfa", d => _ = d.AcroForm.IsHybridXfa),
    ("AcroForm.IsXfaOnly", d => _ = d.AcroForm.IsXfaOnly),
    ("AcroForm.Fields", d => _ = d.AcroForm.Fields.Count),
    ("AcroForm.GetFields", d => _ = d.AcroForm.GetFields("f0").Count),
    ("AcroForm.Export", d => _ = d.AcroForm.Export().Count),
    ("Metadata.Title", d => _ = d.Metadata.Title),
    ("Metadata.Keys", d => _ = d.Metadata.Keys.Count),
    ("Metadata.HasXmp", d => _ = d.Metadata.HasXmp),
    ("Attachments.Count", d => _ = d.Attachments.Count),
};
var memberRows = new List<object>();
foreach (var (name, act) in members)
{
    c.Reset();
    using var d = await PdfDocument.OpenAsync(new RangeSource(handle, 0, c), leaveOpen: true);
    long openBytes = c.Bytes, openAsync = c.AsyncReads;
    c.Reset();
    act(d);
    memberRows.Add(new { member = name, syncReads = c.SyncReads, asyncReads = c.AsyncReads, bytes = c.Bytes, openAsyncReads = openAsync, openBytes });
}
// Editor members: reads while editing, and saving.
{
    c.Reset();
    using var d = await PdfDocument.OpenAsync(new RangeSource(handle, 0, c), leaveOpen: true);
    using var e = d.Edit();
    c.Reset();
    _ = e.Pages.Count;
    memberRows.Add(new { member = "Editor.Pages", syncReads = c.SyncReads, asyncReads = c.AsyncReads, bytes = c.Bytes });
    c.Reset();
    e.Metadata.Title = "edited";
    memberRows.Add(new { member = "Editor.Metadata.Title set", syncReads = c.SyncReads, asyncReads = c.AsyncReads, bytes = c.Bytes });
    c.Reset();
    e.Pages[0].Rotation = 90;
    memberRows.Add(new { member = "Editor.Pages[0].Rotation set", syncReads = c.SyncReads, asyncReads = c.AsyncReads, bytes = c.Bytes });
}
{
    c.Reset();
    using var d = await PdfDocument.OpenAsync(new RangeSource(handle, 0, c), leaveOpen: true);
    using var e = d.Edit();
    c.Reset();
    e.ExtractPages(new[] { 1, 2 }, Stream.Null);
    memberRows.Add(new { member = "Editor.ExtractPages(2 pages)", syncReads = c.SyncReads, asyncReads = c.AsyncReads, bytes = c.Bytes });
}
foreach (var (label, pages) in new[] { ("Editor.ExtractPages(pages 6000,6001; no annotations)", new[] { 6000, 6001 }), ("Editor.ExtractPages(page 1; has widget)", new[] { 1 }) })
{
    c.Reset();
    using var d = await PdfDocument.OpenAsync(new RangeSource(handle, 0, c), leaveOpen: true);
    using var e = d.Edit();
    c.Reset();
    e.ExtractPages(pages, Stream.Null);
    memberRows.Add(new { member = label, syncReads = c.SyncReads, asyncReads = c.AsyncReads, bytes = c.Bytes });
}
Emit("members_after_async_open", memberRows);

// 3. Saves: async versus sync on a delayed source, prefix copy through the source.
var tmp = Path.Combine(Path.GetTempPath(), "w208-save-" + Guid.NewGuid().ToString("N") + ".pdf");
try
{
    foreach (int latency in new[] { 0, 10 })
    {
        foreach (bool useAsync in new[] { false, true })
        {
            c.Reset();
            using var d = useAsync
                ? await PdfDocument.OpenAsync(new RangeSource(handle, latency, c), leaveOpen: true)
                : PdfDocument.Open(new RangeSource(handle, latency, c), leaveOpen: true);
            using var e = d.Edit();
            e.Metadata.Title = "w208";
            long afterOpenSync = c.SyncReads, afterOpenAsync = c.AsyncReads, afterOpenBytes = c.Bytes;
            c.PeakInFlightSync = 0;
            using var dest = new FileStream(tmp, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, useAsync);
            (long gcBytes, double ms) t;
            if (useAsync)
            {
                t = await P.TimeAsync(() => e.SaveIncrementalAsync(dest, leaveOpen: true));
            }
            else
            {
                t = P.Time(() => e.SaveIncremental(dest, leaveOpen: true));
            }
            Emit("save", new
            {
                latency, useAsync, destinationBytes = dest.Length,
                saveSyncReads = c.SyncReads - afterOpenSync, saveAsyncReads = c.AsyncReads - afterOpenAsync, saveBytes = c.Bytes - afterOpenBytes,
                peakBlockedThreads = c.PeakInFlightSync, ms = t.ms, allocatedBytes = t.gcBytes,
            });
        }
    }
}
finally { File.Delete(tmp); }

// 3b. Ergonomics workflow on an asynchronous path: title + fill + rotate, then SaveAsync to a file stream.
foreach (int latency in new[] { 0, 10 })
{
    c.Reset();
    var dest2 = Path.Combine(Path.GetTempPath(), "w208-wf-" + Guid.NewGuid().ToString("N") + ".pdf");
    try
    {
        using var destStream = new FileStream(dest2, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, true);
        var sw = Stopwatch.StartNew();
        var result = await PdfWorkflow.From(new RangeSource(handle, latency, c)).WithTitle("w208").Fill("f0", "x").RotatePage(1, 90).SaveAsync(destStream);
        Emit("workflow_save_async", new { latency, ms = sw.Elapsed.TotalMilliseconds, c.SyncReads, c.AsyncReads, c.Bytes, peakBlockedThreads = c.PeakInFlightSync, outputBytes = result.OutputLength });
    }
    finally { File.Delete(dest2); }
}

// 4. Cancellation on a 50 ms-per-read source.
{
    c.Reset();
    using var cts = new CancellationTokenSource();
    var sw = Stopwatch.StartNew();
    var task = PdfDocument.OpenAsync(new RangeSource(handle, 50, c), leaveOpen: false, cancellationToken: cts.Token);
    await Task.Delay(120);
    double cancelAt = sw.Elapsed.TotalMilliseconds;
    c.Canceled = true;
    cts.Cancel();
    string outcome;
    try { await task; outcome = "completed"; } catch (OperationCanceledException) { outcome = "canceled"; }
    Emit("cancel_open", new { latencyPerRead = 50, cancelRequestedAtMs = cancelAt, completedAtMs = sw.Elapsed.TotalMilliseconds, outcome, readsAfterCancelRequested = c.ReadsAfterCancel, sourceDisposed = c.Disposed, asyncReadsTotal = c.AsyncReads });
}
{
    c.Reset();
    using var d = await PdfDocument.OpenAsync(new RangeSource(handle, 50, c), leaveOpen: true);
    c.Reset();
    var sw = Stopwatch.StartNew();
    _ = d.Pages.Count;
    Emit("inspect_not_cancelable", new { latencyPerRead = 50, member = "Pages", blockedMs = sw.Elapsed.TotalMilliseconds, c.SyncReads, hasCancellationToken = false });
    c.Reset();
    sw.Restart();
    _ = d.AcroForm.Fields.Count;
    Emit("inspect_not_cancelable", new { latencyPerRead = 50, member = "AcroForm.Fields", blockedMs = sw.Elapsed.TotalMilliseconds, c.SyncReads, hasCancellationToken = false });
}

// 5. Concurrency: 64 opens against 20 ms reads.
async Task Concurrent(string label, Func<Counters, Task> one, int n)
{
    var cc = new Counters();
    int threadsBefore = ThreadPool.ThreadCount;
    int peakThreads = threadsBefore;
    using var stop = new CancellationTokenSource();
    var sampler = Task.Run(async () => { while (!stop.IsCancellationRequested) { peakThreads = Math.Max(peakThreads, ThreadPool.ThreadCount); await Task.Delay(5); } });
    var sw = Stopwatch.StartNew();
    await Task.WhenAll(Enumerable.Range(0, n).Select(_ => one(cc)));
    double ms = sw.Elapsed.TotalMilliseconds;
    stop.Cancel();
    await sampler;
    Emit("concurrency", new { label, concurrent = n, latencyPerRead = 20, wallMs = ms, peakBlockedThreads = cc.PeakInFlightSync, syncReads = cc.SyncReads, asyncReads = cc.AsyncReads, poolThreadsBefore = threadsBefore, poolThreadsPeak = peakThreads });
}
const int N = 64;
await Concurrent("OpenAsync", async cc => { using var d = await PdfDocument.OpenAsync(new RangeSource(handle, 20, cc), leaveOpen: true); }, N);
await Concurrent("Task.Run(Open)", cc => Task.Run(() => { using var d = PdfDocument.Open(new RangeSource(handle, 20, cc), leaveOpen: true); }), N);
await Concurrent("OpenAsync then Task.Run(Pages)", async cc => { using var d = await PdfDocument.OpenAsync(new RangeSource(handle, 20, cc), leaveOpen: true); await Task.Run(() => _ = d.Pages.Count); }, N);
await Concurrent("OpenAsync then Pages inline", async cc => { using var d = await PdfDocument.OpenAsync(new RangeSource(handle, 20, cc), leaveOpen: true); _ = d.Pages.Count; }, N);

// 6. Stream adapter over a seekable stream that overrides only Read.
{
    var cs = new Counters();
    var sw = Stopwatch.StartNew();
    using (var d = await PdfDocument.OpenAsync(new SyncOnlyStream(handle, 20, cs), leaveOpen: false)) { }
    Emit("stream_sync_only_openasync", new { latencyPerRead = 20, wallMs = sw.Elapsed.TotalMilliseconds, streamSyncReads = cs.SyncReads, peakBlockedThreads = cs.PeakInFlightSync, bytes = cs.Bytes });
}
{
    var cs = new Counters();
    int n = 64;
    var sw = Stopwatch.StartNew();
    await Task.WhenAll(Enumerable.Range(0, n).Select(async _ => { using var d = await PdfDocument.OpenAsync(new SyncOnlyStream(handle, 20, cs), leaveOpen: false); }));
    Emit("stream_sync_only_openasync_concurrent", new { concurrent = n, latencyPerRead = 20, wallMs = sw.Elapsed.TotalMilliseconds, streamSyncReads = cs.SyncReads, peakBlockedThreads = cs.PeakInFlightSync });
}

handle.Dispose();

static int MinWorkers() { ThreadPool.GetMinThreads(out int w, out _); return w; }
