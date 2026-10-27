// Disposable, tiny-fixture research probe; no product parser or signing implementation changes.
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;

bool candidateEngine = args.Contains("--candidate-engine");
using RSA key = RSA.Create(2048);
var request = new CertificateRequest("CN=W-203 disposable", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
var cases = new List<Case>
{
    new("filter", 1, new() { ["Filter"] = "/Adobe.PPKLite" }, Lost: true),
    new("ordered-format", 2, new() { ["SubFilter"] = "[/Unknown /adbe.pkcs7.detached /ETSI.CAdES.detached]" }, Lost: true),
    new("pades-format", 2, new() { ["SubFilter"] = "[/ETSI.CAdES.detached /adbe.pkcs7.detached]" }, Pades: true, Lost: true),
    new("parser-capability", 4, new() { ["V"] = "3" }, Lost: true),
    new("reason", 8, new() { ["Reasons"] = "[(Approved) (Reviewed)]" }, Reason: "Approved", Lost: true),
    new("reason-omitted", 8, new(), Lost: true),
    new("reason-unicode", 8, new() { ["Reasons"] = "[<FEFF0041007000700072006F007600650064>]" }, Reason: "Approved", Lost: true),
    new("no-revocation", 32, new() { ["AddRevInfo"] = "false" }, Lost: true),
    new("digest", 64, new() { ["DigestMethod"] = "[/SHA512 /SHA256]" }, Lost: true),
    new("no-document-lock", 128, new() { ["LockDocument"] = "/false" }, Lost: true),
    new("auto-document-lock", 128, new() { ["LockDocument"] = "/auto" }, Lost: true),
    new("approval-only", 0, new() { ["MDP"] = "<< /P 0 >>" }, Mdp: true, Lost: true),
    new("unconstrained", 0, new()),
    new("optional-unsupported", 0, new() { ["Filter"] = "/Other", ["SubFilter"] = "[/Unsupported]", ["Reasons"] = "[]", ["DigestMethod"] = "[/SHA1]", ["LockDocument"] = "/true" }),
    new("absent-entries", 99, new() { ["PrivateValue"] = "42" }),
    new("enrollment-browser", 0, new() { ["Cert"] = "<< /Ff 64 /URL (https://example.test/enroll) /URLType /Browser >>" }),
    new("advisory-policy", 0, new() { ["Cert"] = "<< /SignaturePolicyOID (1.2.3) >>" }),
    new("pdf17-new-flags", 384, new() { ["LockDocument"] = "/true", ["AppearanceFilter"] = "(Unavailable)" }, Version: "1.7"),
    new("filter-mismatch", 1, new() { ["Filter"] = "/Other" }, Accept: false),
    new("format-order-mismatch", 2, new() { ["SubFilter"] = "[/ETSI.CAdES.detached /adbe.pkcs7.detached]" }, Accept: false),
    new("digest-mismatch", 64, new() { ["DigestMethod"] = "[/SHA384]" }, Accept: false),
    new("reason-mismatch", 8, new() { ["Reasons"] = "[(Approved)]" }, Accept: false),
    new("certificate-required", 0, new() { ["Cert"] = "<< /Ff 1 /Subject [<00>] >>" }, CertRequired: true, Accept: false),
    new("cms-timestamp-required", 0, new() { ["TimeStamp"] = "<< /Ff 1 >>" }, TimestampRequired: true, Accept: false),
    new("certification-required", 0, new() { ["MDP"] = "<< /P 2 >>" }, Mdp: true, Accept: false),
    new("dts-timestamp-required", 0, new() { ["TimeStamp"] = "<< /Ff 1 >>" }, TimestampRequired: true, Dts: true, Lost: true),
    new("dts-digest", 64, new() { ["DigestMethod"] = "[/SHA256]" }, Dts: true, Lost: true),
    new("dts-reason-omission", 8, new() { ["Reasons"] = "[(.)]" }, Dts: true, Lost: true),
};
var observations = new List<object>();
foreach (var c in cases)
{
    byte[] input = Fixture(c);
    var baseline = Run(c, input, candidate: false);
    var candidate = Run(c, input, candidate: true);
    if (baseline.Accepted != (c.Accept && (!candidateEngine || !c.Lost)) || candidate.Accepted != (c.Accept && !c.Lost))
        throw new Exception("Unexpected disposition: " + c.Id);
    observations.Add(new { c.Id, c.Version, c.Flags, seed = Seed(c), c.Reason, c.Dts,
        baseline, candidate, lost = baseline.Accepted && !candidate.Accepted,
        fixtureSha256 = Convert.ToHexString(SHA256.HashData(input)) });
}
Console.WriteLine(JsonSerializer.Serialize(new { candidateEngine, cases = cases.Count, observations }, new JsonSerializerOptions { WriteIndented = true }));

Result Run(Case c, byte[] input, bool candidate)
{
    var capture = new Capture(c.Dts ? null : new CmsDetachedSignatureProvider(new RawSigner(key, cert)), c.Dts);
    using var source = new MemoryStream(input);
    using var document = PdfDocument.Open(source, leaveOpen: true);
    using var editor = document.Edit();
    using var destination = new MemoryStream();
    var options = new PdfSignatureOptions { FieldName = "SignHere", Reason = c.Reason,
        Kind = c.Dts ? PdfSignatureKind.DocTimeStamp : PdfSignatureKind.CmsDetached,
        Format = c.Pades ? PdfSignatureFormat.PadesBaselineB : PdfSignatureFormat.AdbePkcs7Detached,
        SigningTime = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero) };
    try
    {
        // This gate consumes fixture descriptors, not arbitrary PDFs. Current engine checks still run.
        if (candidate && RefuseCandidate(c)) throw new PdfSignException("R203 mandatory constraint admission refusal");
        editor.Sign(destination, options, capture, leaveOpen: true);
        if (capture.Calls != 1 || !destination.ToArray().AsSpan(0, input.Length).SequenceEqual(input))
            throw new Exception("Prefix/callback failure");
        bool cmsChecked = false;
        if (!c.Dts)
        {
            var signed = new SignedCms(new ContentInfo(capture.Content!), detached: true);
            signed.Decode(capture.Contents!); signed.CheckSignature(verifySignatureOnly: true);
            cmsChecked = true;
        }
        return new(true, capture.Calls, true, cmsChecked, c.Dts, null);
    }
    catch (PdfSignException ex)
    {
        if (capture.Calls != 0 || destination.Length != 0 || !source.ToArray().SequenceEqual(input))
            throw new Exception("Refusal changed bytes or called provider");
        return new(false, capture.Calls, true, false, false, ex.Message);
    }
}

static bool RefuseCandidate(Case c)
{
    string[] entries = { "Filter", "SubFilter", "V", "Reasons", "LegalAttestation", "AddRevInfo", "DigestMethod", "LockDocument", "AppearanceFilter" };
    int count = c.Version == "2.0" ? 9 : 7;
    for (int i = 0; i < count; i++)
        if ((c.Flags & (1 << i)) != 0 && (entries[i] == "Reasons" || c.Entries.ContainsKey(entries[i]))) return true;
    return c.CertRequired || c.TimestampRequired || c.Mdp;
}

static string Seed(Case c) => "<< /Ff " + c.Flags + " " + string.Join(" ", c.Entries.Select(p => "/" + p.Key + " " + p.Value)) + " >>";

static byte[] Fixture(Case c)
{
    var body = new StringBuilder("%PDF-" + c.Version + "\n");
    var offsets = new List<int>();
    foreach (string obj in new[] {
        "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>",
        "<< /Fields [5 0 R] /SigFlags 3 >>",
        "<< /FT /Sig /T (SignHere) /Type /Annot /Subtype /Widget /Rect [0 0 0 0] /P 3 0 R /SV 6 0 R >>",
        Seed(c) })
    {
        offsets.Add(body.Length); body.Append($"{offsets.Count} 0 obj\n{obj}\nendobj\n");
    }
    int xref = body.Length;
    body.Append("xref\n0 7\n0000000000 65535 f\r\n");
    foreach (int offset in offsets) body.Append($"{offset:D10} 00000 n\r\n");
    body.Append($"trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    return Encoding.ASCII.GetBytes(body.ToString());
}

record Case(string Id, long Flags, Dictionary<string, string> Entries, string Version = "2.0", string? Reason = null,
    bool Pades = false, bool Dts = false, bool CertRequired = false, bool TimestampRequired = false,
    bool Mdp = false, bool Accept = true, bool Lost = false);
record Result(bool Accepted, int Calls, bool ByteGuarantee, bool CmsSignatureChecked, bool OpaqueTimestampPreflightOnly, string? Refusal);

sealed class Capture(IPdfSignatureProvider? inner, bool dts) : IPdfSignatureProvider
{
    public PdfSignatureKind Kind => dts ? PdfSignatureKind.DocTimeStamp : PdfSignatureKind.CmsDetached;
    public int Calls { get; private set; }
    public byte[]? Content { get; private set; }
    public byte[]? Contents { get; private set; }
    public async ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
    {
        Calls++;
        using var bytes = context.OpenSignedBytes(); using var copy = new MemoryStream();
        await bytes.CopyToAsync(copy, cancellationToken); Content = copy.ToArray();
        Contents = inner == null ? new byte[] { 1, 2, 3 } : await inner.CreateContentsAsync(context, cancellationToken);
        return Contents;
    }
}

sealed class RawSigner(RSA key, X509Certificate2 certificate) : IPdfExternalSigner
{
    public byte[] CertificateDer => certificate.RawData;
    public IReadOnlyList<byte[]> AdditionalCertificates => Array.Empty<byte[]>();
    public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        => new(key.SignHash(hash.Span, hashAlgorithm, RSASignaturePadding.Pkcs1));
}
