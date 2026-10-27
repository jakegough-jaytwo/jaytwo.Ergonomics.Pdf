// Disposable net10 research probe. All PDF buffering here is a tiny generated fixture.
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;
using jaytwo.Ergonomics.Pdf;

const string CustomOid = "1.3.6.1.4.1.55555.202";
byte[] input = Fixture();
using RSA key = RSA.Create(2048);
var request = new CertificateRequest("CN=W-202 disposable", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
var options = new PdfSignatureOptions { SigningTime = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero) };
var observations = new List<object>();
foreach (bool workflow in new[] { false, true })
{
    var provider = new CompleteCms(certificate, CustomOid);
    using var destination = new MemoryStream();
    if (workflow)
        await PdfWorkflow.From(new MemoryStream(input)).SignAsync(destination, provider, options);
    else
    {
        using var source = new MemoryStream(input);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        await editor.SignAsync(destination, options, provider, leaveOpen: true);
    }
    Verify(provider.Content!, provider.Cms!);
    var decoded = new SignedCms();
    decoded.Decode(provider.Cms!);
    bool custom = decoded.SignerInfos[0].SignedAttributes.Cast<CryptographicAttributeObject>().Any(a => a.Oid.Value == CustomOid);
    if (!custom || provider.Calls != 1 || !destination.ToArray().AsSpan(0, input.Length).SequenceEqual(input)) throw new Exception("complete CMS probe failed");
    observations.Add(new { path = workflow ? "workflow-complete-cms" : "engine-complete-cms", provider.Calls, customSignedAttribute = custom, cryptographicSignatureOnly = true, prefixPreserved = true, outputBytes = destination.Length });
}
var raw = new RawSigner(key, certificate);
var capture = new Capture(new CmsDetachedSignatureProvider(raw));
using (var source = new MemoryStream(input))
using (var document = PdfDocument.Open(source, leaveOpen: true))
using (var editor = document.Edit())
using (var destination = new MemoryStream())
{
    await editor.SignAsync(destination, options, capture, leaveOpen: true);
    Verify(capture.Content!, capture.Cms!);
    var decoded = new SignedCms(); decoded.Decode(capture.Cms!);
    bool custom = decoded.SignerInfos[0].SignedAttributes.Cast<CryptographicAttributeObject>().Any(a => a.Oid.Value == CustomOid);
    bool different = !raw.Hash!.SequenceEqual(SHA256.HashData(capture.Content!));
    if (custom || !different || raw.Calls != 1) throw new Exception("raw signer probe failed");
    observations.Add(new { path = "engine-raw-signer-via-builtin", raw.Calls, customSignedAttribute = custom, signerDigestDiffersFromPdfDigest = different, cryptographicSignatureOnly = true });
}
Console.WriteLine(JsonSerializer.Serialize(new { fixtureSha256 = Convert.ToHexString(SHA256.HashData(input)), observations }, new JsonSerializerOptions { WriteIndented = true }));

static void Verify(byte[] content, byte[] cms)
{
    var signed = new SignedCms(new ContentInfo(content), detached: true);
    signed.Decode(cms); signed.CheckSignature(verifySignatureOnly: true);
}

static byte[] Fixture()
{
    var body = new StringBuilder("%PDF-1.7\n");
    var offsets = new List<int>();
    foreach (string obj in new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Count 1 /Kids [3 0 R] >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>" })
    {
        offsets.Add(body.Length); body.Append($"{offsets.Count} 0 obj\n{obj}\nendobj\n");
    }
    int xref = body.Length;
    body.Append("xref\n0 4\n0000000000 65535 f\r\n");
    foreach (int offset in offsets) body.Append($"{offset:D10} 00000 n\r\n");
    body.Append($"trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    return Encoding.ASCII.GetBytes(body.ToString());
}

sealed class CompleteCms(X509Certificate2 certificate, string customOid) : IPdfSignatureProvider
{
    public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;
    public int Calls { get; private set; }
    public byte[]? Content { get; private set; }
    public byte[]? Cms { get; private set; }
    public async ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
    {
        // Models a service returning a complete CMS during the live callback, not a persistent prepare/complete API.
        Calls++;
        using var content = context.OpenSignedBytes();
        using var bytes = new MemoryStream();
        await content.CopyToAsync(bytes, cancellationToken);
        Content = bytes.ToArray();
        var signed = new SignedCms(new ContentInfo(Content), detached: true);
        var signer = new CmsSigner(certificate) { DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), IncludeOption = X509IncludeOption.EndCertOnly };
        signer.SignedAttributes.Add(new Pkcs9SigningTime(context.SigningTime!.Value.UtcDateTime));
        signer.SignedAttributes.Add(new AsnEncodedData(new Oid(customOid), new byte[] { 12, 3, 119, 50, 48 }));
        signed.ComputeSignature(signer);
        Cms = signed.Encode();
        return Cms;
    }
}

sealed class Capture(IPdfSignatureProvider inner) : IPdfSignatureProvider
{
    public PdfSignatureKind Kind => inner.Kind;
    public byte[]? Content { get; private set; }
    public byte[]? Cms { get; private set; }
    public async ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
    {
        using var content = context.OpenSignedBytes();
        using var bytes = new MemoryStream();
        await content.CopyToAsync(bytes, cancellationToken); Content = bytes.ToArray();
        Cms = await inner.CreateContentsAsync(context, cancellationToken); return Cms;
    }
}

sealed class RawSigner(RSA key, X509Certificate2 certificate) : IPdfExternalSigner
{
    public byte[] CertificateDer => certificate.RawData;
    public IReadOnlyList<byte[]> AdditionalCertificates => Array.Empty<byte[]>();
    public int Calls { get; private set; }
    public byte[]? Hash { get; private set; }
    public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
    {
        Calls++; Hash = hash.ToArray();
        return new ValueTask<byte[]>(key.SignHash(hash.Span, hashAlgorithm, RSASignaturePadding.Pkcs1));
    }
}
