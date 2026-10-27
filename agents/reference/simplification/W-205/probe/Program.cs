using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;
using jaytwo.Ergonomics.Pdf;

var scratch = args[0];
var evidence = args[1];
var rows = new List<object>();
var input = MakePdf();
int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
PdfSignatureOptions Options(bool stamp = false) => new() { Kind = stamp ? PdfSignatureKind.DocTimeStamp : PdfSignatureKind.CmsDetached, ReservedContentsBytes = 64, SigningTime = DateTimeOffset.Parse("2026-01-01T00:00:00Z") };

// Each selected case covers a distinct seam, rather than repeating every possible combination.
void Engine(string name, string operation, bool same, string stage, string rollback)
{
    using var source = new FaultStream(); source.Write(input); source.Position = 0;
    using var other = new FaultStream();
    var target = same ? source : other;
    using var document = PdfDocument.Open(source, leaveOpen: true);
    var editor = document.Edit(); editor.Metadata.Title = "Pending";
    var provider = new Provider(operation == "stamp");
    target.Arm(stage, rollback, input.Length);
    Exception? caught = null;
    try
    {
        if (operation == "save") editor.SaveIncremental(target, leaveOpen: true);
        else editor.Sign(target, Options(operation == "stamp"), provider, leaveOpen: true);
    }
    catch (Exception failure) { caught = failure; }
    var diagnostic = caught == null ? null : PdfSigningRollbackFailure.Get(caught);
    long expectedLength = same ? input.Length : 0;
    Check(ReferenceEquals(caught, target.Failure), name + " original exception");
    Check(document.Metadata.Title == "Pending", name + " pending title");
    Check(target.RawLength == (rollback == "length" ? target.BytesBeforeRestore : expectedLength), name + " length");
    Check((diagnostic != null) == (operation != "save" && rollback != "none"), name + " diagnostic presence");
    if (diagnostic != null)
    {
        Check(diagnostic.LengthRestored == (rollback == "seek"), name + " length diagnostic");
        Check(diagnostic.SeekAttempted == (rollback == "seek"), name + " seek diagnostic");
        Check(ReferenceEquals(diagnostic.RestorationException, target.RestorationFailure), name + " restoration identity");
    }
    var row = new { name, api = "engine", operation, same, stage, rollback, originalPreserved = ReferenceEquals(caught, target.Failure), length = target.RawLength, expectedLength,
        target.BytesBeforeRestore, target.LengthAttempts, target.SeekAttempts, diagnostic = diagnostic == null ? null : new { diagnostic.LengthRestored, diagnostic.SeekAttempted, diagnostic.SeekSucceeded },
        pendingTitle = document.Metadata.Title, provider.Calls, sourceOpen = source.CanRead, destinationOpen = target.CanWrite, failureStack = caught?.StackTrace };
    // Repair physical output explicitly before continuation. This does not claim logical restoration repairs bytes.
    target.Armed = false; target.SetLength(expectedLength); target.Position = expectedLength;
    editor.Dispose();
    using var next = document.Edit(); using var retry = new MemoryStream(); next.SaveIncremental(retry, leaveOpen: true);
    using var reopened = PdfDocument.Open(new MemoryStream(retry.ToArray()));
    Check(reopened.Metadata.Title == "Pending", name + " survives disposed editor and retry");
    Check(reopened.Signatures.Count == 0, name + " no leaked signature");
    Check(retry.ToArray().AsSpan(0, input.Length).SequenceEqual(input), name + " prefix");
    rows.Add(row);
}

Engine("save-copy-restored", "save", false, "copy", "none");
Engine("save-copy-truncate-refused", "save", false, "copy", "length");
Engine("save-copy-seek-refused", "save", false, "copy", "seek");
Engine("save-append-truncate-refused", "save", true, "append", "length");
Engine("save-finalization-restored", "save", false, "length", "none");
Engine("sign-copy-truncate-refused", "sign", false, "copy", "length");
Engine("sign-copy-seek-refused", "sign", false, "copy", "seek");
Engine("sign-append-restored", "sign", true, "append", "none");
Engine("sign-finalization-truncate-refused", "sign", false, "length", "length");
Engine("sign-finalization-seek-refused", "sign", true, "length", "seek");
Engine("timestamp-copy-restored", "stamp", false, "copy", "none");
Engine("timestamp-copy-truncate-refused", "stamp", false, "copy", "length");
Engine("timestamp-copy-seek-refused", "stamp", false, "copy", "seek");
Engine("timestamp-finalization-restored", "stamp", false, "length", "none");

foreach (bool stamp in new[] { false, true })
{
    using var source = new MemoryStream(input); using var output = new MemoryStream();
    using var document = PdfDocument.Open(source, leaveOpen: true);
    var editor = document.Edit(); editor.Metadata.Title = "Pending";
    var provider = new Provider(stamp) { Throw = true };
    Exception? caught = null;
    try { editor.Sign(output, Options(stamp), provider, leaveOpen: true); } catch (Exception e) { caught = e; }
    Check(output.Length == 0 && provider.Calls == 1, "pre-output failure");
    editor.Dispose(); using var next = document.Edit(); next.SaveIncremental(output, leaveOpen: true);
    using var reopened = PdfDocument.Open(new MemoryStream(output.ToArray()));
    Check(reopened.Metadata.Title == "Pending" && reopened.Signatures.Count == 0, "pre-output pending preserved");
    rows.Add(new { name = stamp ? "timestamp-provider-failure" : "sign-provider-failure", api = "engine", error = caught?.GetType().Name, outputBeforeRetry = 0, provider.Calls, pendingAfterEditorDisposal = reopened.Metadata.Title });
}

void WorkflowCopy(string name, string stage, string rollback)
{
    using var source = new MemoryStream(input); using var target = new FaultStream();
    target.Arm(stage, rollback, input.Length);
    var provider = new Provider(false);
    var workflow = PdfWorkflow.From(source).WithTitle("Pending");
    Exception? caught = null;
    try { workflow.Sign(target, provider, Options()); } catch (Exception e) { caught = e; }
    bool consumed = false; try { workflow.Save(new MemoryStream()); } catch (InvalidOperationException) { consumed = true; }
    bool originalPreserved = ReferenceEquals(caught, target.Failure);
    Check(caught != null && consumed && provider.Calls == 1, name + " consumed and no retry");
    Check(source.ToArray().SequenceEqual(input) && source.CanRead && target.CanWrite, name + " source and ownership");
    // Record an observed inconsistency; the research does not change production behavior.
    Check(originalPreserved == (rollback != "unexpected"), name + " exception observation");
    rows.Add(new { name, api = "workflow", stage, rollback, originalPreserved, error = caught?.GetType().Name, length = target.RawLength,
        target.LengthAttempts, target.SeekAttempts, diagnostic = caught == null ? false : PdfSigningRollbackFailure.Get(caught) != null, consumed, provider.Calls });
}
WorkflowCopy("workflow-copy-restored", "copy", "none");
WorkflowCopy("workflow-copy-truncate-refused", "copy", "length");
WorkflowCopy("workflow-copy-seek-refused", "copy", "seek");
WorkflowCopy("workflow-copy-unexpected-restoration", "copy", "unexpected");
WorkflowCopy("workflow-flush-restored", "flush", "none");

foreach (string mode in new[] { "provider", "publication", "cancel" })
{
    string directory = Path.Combine(scratch, mode); Directory.CreateDirectory(directory);
    string path = Path.Combine(directory, "result.pdf");
    byte[] prior = Encoding.ASCII.GetBytes("prior destination");
    if (mode == "provider") File.WriteAllBytes(path, prior);
    using var source = new MemoryStream(input); using var cancel = new CancellationTokenSource();
    var provider = new Provider(false) { Throw = mode == "provider", OnCall = () => { if (mode == "publication") Directory.CreateDirectory(path); if (mode == "cancel") cancel.Cancel(); } };
    Exception? caught = null;
    try { PdfWorkflow.From(source).WithTitle("Pending").SignAsync(path, provider, Options(), new PdfFileSaveOptions { Overwrite = true }, cancel.Token).GetAwaiter().GetResult(); } catch (Exception e) { caught = e; }
    Check(caught != null && provider.Calls == 1, mode + " failure");
    Check(!Directory.EnumerateFiles(directory, ".*.tmp").Any(), mode + " temporary cleanup");
    bool priorPreserved = mode != "provider" || File.ReadAllBytes(path).SequenceEqual(prior);
    Check(priorPreserved && source.ToArray().SequenceEqual(input), mode + " preservation");
    rows.Add(new { name = "workflow-file-" + mode, api = "workflow-file", error = caught?.GetType().Name, provider.Calls, priorPreserved, temporaryCount = Directory.EnumerateFiles(directory, ".*.tmp").Count() });
    if (Directory.Exists(path)) Directory.Delete(path); else if (File.Exists(path)) File.Delete(path);
}

using (var source = new MemoryStream(input))
using (var destination = new MemoryStream())
{
    var authority = new FailingAuthority(); Exception? caught = null;
    try { PdfWorkflow.From(source).WithTitle("Pending").Timestamp(destination, authority); } catch (Exception e) { caught = e; }
    Check(caught is PdfTimestampException && caught.InnerException == null && !caught.ToString().Contains("SECRET"), "host redaction");
    Check(destination.Length == 0 && authority.Calls == 1, "authority no output or retry");
    rows.Add(new { name = "workflow-authority-redaction", error = caught?.GetType().Name, authority.Calls, destination.Length, redacted = true });
}

File.WriteAllText(Path.Combine(evidence, "observations.json"), JsonSerializer.Serialize(new { baseline = "see execution.json", fixtureSha256 = Convert.ToHexString(SHA256.HashData(input)), fixtureBytes = input.Length, assertions, cases = rows.Count, rows }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{rows.Count} cases, {assertions} assertions passed.");

static byte[] MakePdf()
{
    var text = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
    string[] objects = { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>" };
    for (int i = 0; i < objects.Length; i++) { offsets.Add(text.Length); text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
    int xref = text.Length; text.Append("xref\n0 4\n0000000000 65535 f \n");
    foreach (int offset in offsets.Skip(1)) text.Append($"{offset:D10} 00000 n \n");
    text.Append($"trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    return Encoding.ASCII.GetBytes(text.ToString());
}

sealed class Provider(bool stamp) : IPdfSignatureProvider
{
    public PdfSignatureKind Kind => stamp ? PdfSignatureKind.DocTimeStamp : PdfSignatureKind.CmsDetached;
    public bool Throw { get; set; }
    public Action? OnCall { get; set; }
    public int Calls { get; private set; }
    public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
    { Calls++; OnCall?.Invoke(); if (Throw) throw new IOException("Probe provider failure"); return new(new byte[] { 1, 2, 3, 4 }); }
}

sealed class FailingAuthority : ITimestampAuthority
{
    public int Calls { get; private set; }
    public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    { Calls++; throw new IOException("SECRET authority credentials"); }
}

sealed class FaultStream : MemoryStream
{
    // Avoid MemoryStream capacity growth reading our faulting Length during writing.
    public FaultStream() : base(65536) { }
    private bool _restoring;
    private bool _wrote;
    private string _stage = "", _rollback = "";
    private int _prefix;
    public bool Armed { get; set; }
    public Exception Failure { get; } = new IOException("Original output failure");
    public Exception RestorationFailure { get; private set; } = new IOException("Restoration failure");
    public long RawLength => base.Length;
    public long BytesBeforeRestore { get; private set; }
    public int LengthAttempts { get; private set; }
    public int SeekAttempts { get; private set; }
    public void Arm(string stage, string rollback, int prefix) { Armed = true; _stage = stage; _rollback = rollback; _prefix = prefix; _wrote = false; }
    public override long Length => Armed && _wrote && base.Length > _prefix && _stage == "length" ? throw Failure : base.Length;
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (Armed && (_stage == "copy" || (_stage == "append" && Position >= _prefix))) { base.Write(buffer, offset, Math.Min(17, count)); _wrote = true; throw Failure; }
        base.Write(buffer, offset, count); _wrote |= Armed && count > 0;
    }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { Write(buffer, offset, count); return Task.CompletedTask; }
    public override void SetLength(long length)
    {
        if (Armed) { LengthAttempts++; _restoring = true; BytesBeforeRestore = base.Length; if (_rollback == "unexpected") { RestorationFailure = new InvalidOperationException("Unexpected restoration failure"); throw RestorationFailure; } if (_rollback == "length") throw RestorationFailure; }
        base.SetLength(length);
    }
    public override long Seek(long offset, SeekOrigin origin) { if (Armed && _restoring) { SeekAttempts++; if (_rollback == "seek") throw RestorationFailure; } return base.Seek(offset, origin); }
    public override long Position { get => base.Position; set { if (Armed && _restoring) { SeekAttempts++; if (_rollback == "seek") throw RestorationFailure; } base.Position = value; } }
    public override void Flush() { if (Armed && _wrote && _stage == "flush") throw Failure; base.Flush(); }
}
