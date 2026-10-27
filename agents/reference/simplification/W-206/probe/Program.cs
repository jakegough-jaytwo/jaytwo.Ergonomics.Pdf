using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Ergonomics.Pdf.Tests;

// Disposable filesystem probes of the production helper, linked without changing either library.
// Payloads are intentionally not PDFs: this isolates publication from PDF processing.
string root = Path.GetFullPath(args[0]);
Directory.CreateDirectory(root);
var observations = new List<object>();
byte[] original = "original input"u8.ToArray();
byte[] prior = "prior destination is different"u8.ToArray();
byte[] completed = "completed output"u8.ToArray();
int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
async Task Case(string name, Func<string, Task<object?>> action)
{
    string folder = Path.Combine(root, name);
    Directory.CreateDirectory(folder);
    try { observations.Add(new { name, status = "observed", detail = await action(folder) }); }
    catch (Exception ex) { observations.Add(new { name, status = "FAILED", detail = ex.ToString() }); }
}
async Task<Exception?> Failure(Func<Task> action)
{
    try { await action(); return null; } catch (Exception ex) { return ex; }
}
Task<int> Write(string target, string? input, Action? during = null, CancellationToken token = default) =>
    WorkflowFiles.WriteAndPublishAsync(target, true, true, true, token, async output =>
    { await output.WriteAsync(completed); during?.Invoke(); return completed.Length; }, input);
void Clean(string folder) => Check(Directory.GetFiles(folder, ".*.tmp").Length == 0, "temporary leaked");

await Case("normalized-paths", folder =>
{
    string input = Path.Combine(folder, "in"); File.WriteAllBytes(input, original);
    Directory.CreateDirectory(Path.Combine(folder, "sub"));
    Check(WorkflowFiles.SamePath(input, Path.Combine(folder, "sub", "..", "in")), "dot path");
    bool caseEqual = WorkflowFiles.SamePath(input, Path.Combine(folder, "IN"));
    Check(caseEqual == OperatingSystem.IsWindows(), "platform comparison (Linux/Windows probe)");
    return Task.FromResult<object?>(new { caseEqual });
});
await Case("intermediate-directory-link", folder =>
{
    string input = Path.Combine(folder, "in"); File.WriteAllBytes(input, original);
    string link = Path.Combine(folder, "link"); FileLinks.CreateDirectoryLink(link, folder);
    try { Check(WorkflowFiles.SamePath(input, Path.Combine(link, "in")), "directory link alias"); }
    finally { Directory.Delete(link); }
    return Task.FromResult<object?>("junction on Windows; symlink on Linux resolves to input");
});
foreach (string kind in new[] { "final", "dangling", "cycle" })
await Case(kind + "-symlink", folder =>
{
    string input = Path.Combine(folder, "in"); File.WriteAllBytes(input, original);
    string link = Path.Combine(folder, "link");
    try { File.CreateSymbolicLink(link, kind == "final" ? input : kind == "cycle" ? link : Path.Combine(folder, "absent")); }
    catch (Exception ex) when (OperatingSystem.IsWindows() && (ex is UnauthorizedAccessException || (ex is IOException && (ex.HResult & 0xffff) == 1314)))
    { return Task.FromResult<object?>(new { unavailable = ex.GetType().Name, reason = "symlink privilege unavailable", ex.HResult }); }
    try
    {
        if (kind == "final") { Check(WorkflowFiles.SamePath(input, link), "final alias"); return Task.FromResult<object?>("resolved"); }
        try { string normalized = WorkflowFiles.Normalize(link); return Task.FromResult<object?>(new { normalized, limitation = "resolution outcome only; not file existence proof" }); }
        catch (IOException ex) { return Task.FromResult<object?>(new { refusal = ex.GetType().Name, ex.Message }); }
    }
    finally { File.Delete(link); }
});
foreach (string kind in new[] { "hard-link-closed", "hard-link-open", "equal-metadata-copy", "different-time-copy" })
await Case(kind, async folder =>
{
    string input = Path.Combine(folder, "in"), target = Path.Combine(folder, "out");
    File.WriteAllBytes(input, original);
    if (kind.StartsWith("hard-link")) FileLinks.CreateHardLink(target, input);
    else { File.WriteAllBytes(target, original); File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(input).AddHours(kind == "different-time-copy" ? -1 : 0)); }
    using FileStream? held = kind.EndsWith("open") ? new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    Exception? failure = await Failure(() => Write(target, input));
    Check((failure == null) == (kind == "different-time-copy"), "metadata refusal disposition");
    Check(File.ReadAllBytes(input).SequenceEqual(original), "input changed");
    Check(File.ReadAllBytes(target).SequenceEqual(failure == null ? completed : original), "destination unexpected");
    Clean(folder);
    return new { failure = failure?.GetType().Name, message = failure?.Message, inputHash = Hash(input), destinationHash = Hash(target) };
});
await Case("missing-input-metadata", async folder =>
{
    string target = Path.Combine(folder, "out"); File.WriteAllBytes(target, prior);
    Exception? failure = await Failure(() => Write(target, Path.Combine(folder, "absent")));
    Check(failure is IOException, "missing metadata must refuse"); Check(File.ReadAllBytes(target).SequenceEqual(prior), "destination changed"); Clean(folder);
    return failure!.GetType().Name;
});
await Case("hard-link-introduced-during-write", async folder =>
{
    string input = Path.Combine(folder, "in"), target = Path.Combine(folder, "out"); File.WriteAllBytes(input, original); File.WriteAllBytes(target, prior);
    using var held = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read);
    Exception? failure = await Failure(() => Write(target, input, () => { File.Delete(target); FileLinks.CreateHardLink(target, input); }));
    Check(failure is IOException, "late alias not refused"); Check(File.ReadAllBytes(input).SequenceEqual(original), "input changed"); Clean(folder);
    return new { failure = failure!.GetType().Name, failure.Message };
});
foreach (string operation in new[] { "rename", "delete" })
await Case("open-input-" + operation, folder =>
{
    string input = Path.Combine(folder, "in"), moved = Path.Combine(folder, "moved"); File.WriteAllBytes(input, original);
    using var held = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read);
    Exception? failure = null;
    try { if (operation == "rename") File.Move(input, moved); else File.Delete(input); } catch (IOException ex) { failure = ex; }
    Check((failure != null) == OperatingSystem.IsWindows(), "platform sharing outcome");
    Check(held.ReadByte() == original[0], "held input unreadable");
    return Task.FromResult<object?>(new { blocked = failure != null, heldLength = held.Length, originalPathExists = File.Exists(input) });
});
foreach (string kind in new[] { "success-new", "success-replace", "write-failure", "cancel", "publication-failure" })
await Case(kind, async folder =>
{
    string target = Path.Combine(folder, "out");
    if (kind == "publication-failure") Directory.CreateDirectory(target);
    else if (kind != "success-new") File.WriteAllBytes(target, prior);
    using var cancel = new CancellationTokenSource();
    Exception? failure = await Failure(() => Write(target, null, () =>
    { if (kind == "write-failure") throw new InvalidOperationException("injected processing failure"); if (kind == "cancel") cancel.Cancel(); }, cancel.Token));
    Check((failure == null) == kind.StartsWith("success"), "publication outcome");
    if (kind == "publication-failure") Check(Directory.Exists(target), "directory lost");
    else Check(File.ReadAllBytes(target).SequenceEqual(failure == null ? completed : prior), "destination content");
    Clean(folder); return new { failure = failure?.GetType().Name };
});
await Case("replace-preserves-other-hard-link", async folder =>
{
    string target = Path.Combine(folder, "out"), other = Path.Combine(folder, "other"); File.WriteAllBytes(target, prior); FileLinks.CreateHardLink(other, target);
    await Write(target, null); Check(File.ReadAllBytes(target).SequenceEqual(completed), "new bytes"); Check(File.ReadAllBytes(other).SequenceEqual(prior), "other hard link changed"); Clean(folder);
    return "replacement changes directory entry; other hard link keeps old bytes";
});
await Case("cleanup-locked-temporary", async folder =>
{
    string path = Path.Combine(folder, "temporary"); File.WriteAllBytes(path, prior);
    using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
    { WorkflowFiles.TryDelete(path); Check(File.Exists(path) == OperatingSystem.IsWindows(), "cleanup sharing"); }
    bool retained = File.Exists(path); File.Delete(path);
    await Task.CompletedTask; return new { retained, policy = "TryDelete is best effort; this probe removes retained file after unlocking" };
});
var result = new { platform = RuntimeInformation.OSDescription, runtime = RuntimeInformation.FrameworkDescription, filesystemRoot = root, assertions, observations };
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
return observations.Any(x => JsonSerializer.Serialize(x).Contains("\"FAILED\"")) ? 1 : 0;
