using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Signatures;

// Disposable public API observation, not product implementation or a test suite.
string input = args[0], output = args[1];
Directory.CreateDirectory(output);
using var key = RSA.Create(2048);
var request = new CertificateRequest("CN=W194 disposable", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
var signer = new HostSigner(key, cert.Export(X509ContentType.Cert));
var rows = new List<object>();
foreach (string file in Directory.GetFiles(input, "*.pdf").Order())
{
    string id = Path.GetFileNameWithoutExtension(file);
    using var source = PdfDocument.Open(File.OpenRead(file));
    foreach (string op in new[] { "extract", "repeat" })
    {
        using var dest = new MemoryStream();
        using var editor = source.Edit();
        var result = op == "extract" ? editor.ExtractPages(new[] { 1 }, dest, new PdfAssemblyOptions())
            : PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(source, new[] { 2, 1, 1 }), new PdfAssemblyInput(source, new[] { 1 }) }, dest);
        File.WriteAllBytes(Path.Combine(output, id+"-"+op+".pdf"), dest.ToArray());
        rows.Add(new { id, op, bytes = dest.Length, omissions = result.Omissions.Where(x => x.Value != 0).ToDictionary(x => x.Key.ToString(), x => x.Value) });
    }
    using var signed = new MemoryStream();
    using var signingEditor = source.Edit();
    int beforeCalls = signer.Calls;
    string status = "signed", laterFill = "not attempted";
    try
    {
        signingEditor.Sign(signed, new PdfSignatureOptions { FieldName = "SignHere" }, new CmsDetachedSignatureProvider(signer), leaveOpen: true);
        File.WriteAllBytes(Path.Combine(output, id+"-signed.pdf"), signed.ToArray());
        using var reopened = PdfDocument.Open(new MemoryStream(signed.ToArray()));
        using var fill = reopened.Edit();
        try { laterFill = fill.AcroForm.Fill("Other", PdfFieldValue.FromText("Changed")).ToString(); }
        catch (Exception e) { laterFill = e.GetType().Name; }
    }
    catch (Exception e) { status = e.GetType().Name; }
    rows.Add(new { id, op = "sign-original", status, bytes = signed.Length, hostCalls = signer.Calls - beforeCalls, laterFill });
}
File.WriteAllText(Path.Combine(output, "probe-results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Recorded {rows.Count} outcomes");

sealed class HostSigner(RSA key, byte[] certificate) : IPdfExternalSigner
{
    public byte[] CertificateDer => certificate;
    public IReadOnlyList<byte[]> AdditionalCertificates => Array.Empty<byte[]>();
    public int Calls { get; private set; }
    public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
    {
        Calls++;
        return new(key.SignHash(hash.Span, hashAlgorithm, RSASignaturePadding.Pkcs1));
    }
}
