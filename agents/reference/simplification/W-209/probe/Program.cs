// W-209 disposable probe. Public API of jaytwo.Pdf only. For each generated fixture it exercises the one required
// decoding site (cross-reference stream or object stream via Open, XMP via OpenXmp, embedded file via Open) and prints one
// JSON object per case: outcome, exception type/message, match against the expected plain text, time and allocation.
// Usage: Probe <fixture-dir>. Nothing here is product code.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using jaytwo.Pdf;

string dir = args[0];
using var manifestDoc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")));
var root = manifestDoc.RootElement;
long attachCap = root.GetProperty("attachmentCapUsed").GetInt64();
var json = new JsonSerializerOptions { WriteIndented = false };
void Emit(object o) => Console.WriteLine(JsonSerializer.Serialize(o, json));

Emit(new { scenario = "environment", runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, structuralCap = root.GetProperty("structuralCap").GetInt64(), attachCap });

string Hex(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

// Warm-up on the first valid fixture so the first measured case does not carry JIT cost.
foreach (var warm in root.GetProperty("cases").EnumerateArray().Take(1))
{
    try { using var d = PdfDocument.Open(File.OpenRead(Path.Combine(dir, warm.GetProperty("file").GetString()!))); _ = d.PageCount; } catch { }
}

foreach (var c in root.GetProperty("cases").EnumerateArray())
{
    string id = c.GetProperty("id").GetString()!, site = c.GetProperty("site").GetString()!, kind = c.GetProperty("kind").GetString()!;
    string path = Path.Combine(dir, c.GetProperty("file").GetString()!);
    string outcome = "ok", exType = "", exMsg = "", inner = "";
    string? gotSha = null; long gotBytes = -1; int? pages = null;
    string stage = "open"; int? pagesAfterFailure = null;
    GC.Collect(); GC.WaitForPendingFinalizers();
    long a0 = GC.GetTotalAllocatedBytes(true);
    var sw = Stopwatch.StartNew();
    try
    {
        using var doc = PdfDocument.Open(File.OpenRead(path));
        stage = "operation";
        try
        {
        if (site is "xref" or "objstm") { pages = doc.PageCount; }
        else
        {
            Stream s = site == "xmp" ? doc.Metadata.OpenXmp()
                : (kind == "limit" ? doc.Attachments[0].Open(attachCap) : doc.Attachments[0].Open());
            using (s)
            {
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                var bytes = ms.ToArray();
                gotBytes = bytes.Length; gotSha = Hex(bytes);
            }
        }
        }
        catch when (Probe_AfterFailure(doc, ref pagesAfterFailure)) { throw; }
    }
    catch (Exception e)
    {
        outcome = "exception"; exType = e.GetType().Name; exMsg = e.Message.Length > 140 ? e.Message[..140] : e.Message;
        inner = e.InnerException?.GetType().Name ?? "";
    }
    sw.Stop();
    long alloc = GC.GetTotalAllocatedBytes(true) - a0;
    bool? match = null;
    if (kind != "limit" && c.TryGetProperty("expect", out var ex))
    {
        if (ex.TryGetProperty("pageCount", out var pc)) match = outcome == "ok" && pages == pc.GetInt32();
        else if (ex.TryGetProperty("sha256", out var sh)) match = outcome == "ok" && gotSha == sh.GetString();
    }
    Emit(new
    {
        scenario = "case", stage, pagesAfterFailure, id, site, variant = c.GetProperty("variant").GetString(), kind, outcome, exType, inner, exMsg, match, pages, gotBytes,
        ms = Math.Round(sw.Elapsed.TotalMilliseconds, 2), allocatedBytes = alloc, peakWorkingSetMiB = Process.GetCurrentProcess().PeakWorkingSet64 >> 20,
        fileBytes = c.GetProperty("fileBytes").GetInt64(),
    });
}

static bool Probe_AfterFailure(PdfDocument doc, ref int? pagesAfterFailure)
{
    // Exception filter: runs before unwinding, records whether the already-open document is still usable.
    try { pagesAfterFailure = doc.PageCount; } catch { pagesAfterFailure = -1; }
    return true;
}
