using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using jaytwo.Ergonomics.Pdf;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;

// Disposable public API observation of current assembly admission, not product implementation or a test suite.
string input = args[0], output = args[1];
Directory.CreateDirectory(output);

// One real engine-signed approval input: a new invisible CMS signature field on page 2 of the control fixture.
using (var key = RSA.Create(2048))
{
    var request = new CertificateRequest("CN=W197 disposable", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
    using var control = PdfDocument.Open(File.OpenRead(Path.Combine(input, "control.pdf")));
    using var signed = File.Create(Path.Combine(input, "engine-signed-p2.pdf"));
    using var editor = control.Edit();
    editor.Sign(signed, new PdfSignatureOptions { FieldName = "EngineApproval", PageNumber = 2 }, new CmsDetachedSignatureProvider(new HostSigner(key, cert.Export(X509ContentType.Cert))), leaveOpen: true);
}

// The supported two-step recovery for document attachments: remove them in an incremental revision, then assemble.
using (var attached = PdfDocument.Open(File.OpenRead(Path.Combine(input, "embedded-files.pdf"))))
using (var prepared = File.Create(Path.Combine(input, "embedded-files-removed.pdf")))
using (var editor = attached.Edit())
{
    editor.Attachments.Remove("data.txt");
    editor.SaveIncremental(prepared, leaveOpen: true);
}

var rows = new List<object>();
foreach (string file in Directory.GetFiles(input, "*.pdf").Order(StringComparer.Ordinal))
{
    string id = Path.GetFileNameWithoutExtension(file);
    foreach (string op in new[] { "extract1", "extract2", "concat" })
    {
        using var source = PdfDocument.Open(File.OpenRead(file));
        using var control = PdfDocument.Open(File.OpenRead(Path.Combine(input, "control.pdf")));
        using var dest = new MemoryStream();
        rows.Add(Observe(id, op, dest, () =>
        {
            if (op == "concat")
            {
                return PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(control, new[] { 1, 2 }), new PdfAssemblyInput(source, new[] { 1 }) }, dest);
            }

            using var editor = source.Edit();
            return editor.ExtractPages(new[] { op == "extract1" ? 1 : 2 }, dest, new PdfAssemblyOptions());
        }));
        if (dest.Length > 0)
        {
            File.WriteAllBytes(Path.Combine(output, id + "-" + op + ".pdf"), dest.ToArray());
        }
    }
}

// Publication and late-failure cases. Each names the destination kind and when the failure occurs.
string refused = Path.Combine(input, "ocprops.pdf"), accepted = Path.Combine(input, "control.pdf");
string publish = Path.Combine(output, "publish");
Directory.CreateDirectory(publish);
rows.Add(Publication("preflight-refusal", "workflow-file", () => PdfWorkflow.Combine(new[] { PdfCombineInput.FromFile(refused) }).Save(Path.Combine(publish, "refused.pdf")), publish, "refused.pdf"));
using (var stream = new MemoryStream())
{
    rows.Add(Observe("ocprops", "preflight-refusal-workflow-stream", stream, () => PdfWorkflow.Combine(new[] { PdfCombineInput.FromFile(refused) }).Save(stream)));
}

using (var failing = new FailingStream(failAfterBytes: 64))
{
    using var source = PdfDocument.Open(File.OpenRead(accepted));
    using var editor = source.Edit();
    rows.Add(Observe("control", "late-destination-engine", failing, () => editor.ExtractPages(new[] { 1 }, failing, new PdfAssemblyOptions())));
}

using (var failing = new FailingStream(failAfterBytes: 64))
{
    rows.Add(Observe("control", "late-destination-workflow-stream", failing, () => PdfWorkflow.Combine(new[] { PdfCombineInput.FromFile(accepted) }).Save(failing)));
}

int readsBeforeOutput;
using (var dest = new ArmingStream())
{
    ArmedSource? armed = null;
    armed = new ArmedSource(File.ReadAllBytes(accepted), () => dest.Armed);
    using var source = PdfDocument.Open(armed, leaveOpen: true);
    using var editor = source.Edit();
    rows.Add(Observe("control", "late-source-read-engine", dest, () => editor.ExtractPages(new[] { 1, 2 }, dest, new PdfAssemblyOptions()), () => armed.Failures));
    readsBeforeOutput = armed.ReadsBeforeFailure;
}

{
    // The workflow opens and reads the same way; fail every read after the count the engine needed before output.
    ArmedSource? armed = null;
    armed = new ArmedSource(File.ReadAllBytes(accepted), () => armed!.Reads > readsBeforeOutput);
    rows.Add(Publication("late-source-read", "workflow-file", () => PdfWorkflow.Combine(new[] { PdfCombineInput.From(armed, new[] { 1, 2 }) }).Save(Path.Combine(publish, "late.pdf")), publish, "late.pdf", () => armed.Failures));
}

{
    // Same-length mutation of a custom snapshot after Open: the caller broke IPdfSource stability.
    byte[] bytes = File.ReadAllBytes(accepted);
    var mutable = new ArmedSource(bytes, () => false);
    using var source = PdfDocument.Open(mutable, leaveOpen: true);
    int at = IndexOf(bytes, Encoding.ASCII.GetBytes("(Page 1)"));
    bytes[at + 6] = (byte)'X';
    using var dest = new MemoryStream();
    using var editor = source.Edit();
    rows.Add(Observe("control", "source-mutation-after-open", dest, () => editor.ExtractPages(new[] { 1 }, dest, new PdfAssemblyOptions()), () => IndexOf(dest.ToArray(), Encoding.ASCII.GetBytes("(Page X)")) >= 0 ? 1 : 0));
}

File.WriteAllText(Path.Combine(output, "probe-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Recorded {rows.Count} outcomes");

static object Observe(string id, string op, Stream dest, Func<PdfAssemblyResult> run, Func<int>? extra = null)
{
    string status = "accepted", exception = "", message = "";
    Dictionary<string, long> omissions = new();
    try
    {
        omissions = run().Omissions.Where(x => x.Value != 0).ToDictionary(x => x.Key.ToString(), x => x.Value);
    }
    catch (Exception e)
    {
        status = "refused";
        exception = e.GetType().Name;
        message = e.Message;
    }

    long bytes = dest is FailingStream f ? f.Written : dest.Length;
    return new { id, op, status, exception, message, bytes, omissions, extra = extra?.Invoke() };
}

static object Publication(string id, string op, Func<PdfAssemblyResult> run, string directory, string name, Func<int>? extra = null)
{
    string status = "accepted", exception = "";
    try
    {
        run();
    }
    catch (Exception e)
    {
        status = "refused";
        exception = e.GetType().Name;
    }

    string[] left = Directory.GetFiles(directory).Select(Path.GetFileName).Where(x => x!.Contains(Path.GetFileNameWithoutExtension(name))).ToArray()!;
    return new { id, op, status, exception, destinationExists = File.Exists(Path.Combine(directory, name)), leftovers = left.Length, extra = extra?.Invoke() };
}

static int IndexOf(byte[] haystack, byte[] needle)
{
    for (int i = 0; i + needle.Length <= haystack.Length; i++)
    {
        if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
        {
            return i;
        }
    }

    return -1;
}

sealed class HostSigner(RSA key, byte[] certificate) : IPdfExternalSigner
{
    public byte[] CertificateDer => certificate;
    public IReadOnlyList<byte[]> AdditionalCertificates => Array.Empty<byte[]>();
    public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        => new(key.SignHash(hash.Span, hashAlgorithm, RSASignaturePadding.Pkcs1));
}

// A seekable destination that fails once more than failAfterBytes have been written.
sealed class FailingStream(int failAfterBytes) : MemoryStream
{
    public long Written => Length;
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (Length + count > failAfterBytes)
        {
            throw new IOException("W197 simulated destination failure");
        }

        base.Write(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        if (Length + 1 > failAfterBytes)
        {
            throw new IOException("W197 simulated destination failure");
        }

        base.WriteByte(value);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (Length + buffer.Length > failAfterBytes)
        {
            throw new IOException("W197 simulated destination failure");
        }

        base.Write(buffer);
    }
}

// A destination that records whether output has begun.
sealed class ArmingStream : MemoryStream
{
    public bool Armed { get; private set; }
    public override void Write(byte[] buffer, int offset, int count) { Armed = true; base.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { Armed = true; base.Write(buffer); }
}

// A snapshot that fails every read once armed.
sealed class ArmedSource(byte[] bytes, Func<bool> armed) : IPdfSource
{
    public int Reads { get; private set; }
    public int ReadsBeforeFailure { get; private set; }
    public int Failures { get; private set; }
    public long Length => bytes.Length;
    public void Dispose() { }
    public int Read(long position, Span<byte> buffer)
    {
        Reads++;
        if (armed())
        {
            if (Failures++ == 0)
            {
                ReadsBeforeFailure = Reads - 1;
            }

            throw new IOException("W197 simulated late source read failure");
        }

        int count = (int)Math.Max(0, Math.Min(buffer.Length, bytes.Length - position));
        bytes.AsSpan((int)position, count).CopyTo(buffer);
        return count;
    }

    public ValueTask<int> ReadAsync(long position, Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Read(position, buffer.Span));
}
