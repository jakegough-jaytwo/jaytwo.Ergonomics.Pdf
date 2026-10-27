// Disposable net10 research probe for W-210. All PDF buffering is a tiny generated fixture. Public API only, plus byte edits of the fixture.
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;

using RSA key = RSA.Create(2048);
var request = new CertificateRequest("CN=W-210 disposable", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
var options = new PdfSignatureOptions { SigningTime = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero), Reason = "probe", Location = "lab" };

byte[] signed = Sign(Fixture());
byte[] later = Metadata(signed, "Later revision");
var observations = new List<object>();

// P1: a library-signed file followed by a metadata revision. Dump every public inspection member.
using (var s = new MemoryStream(later))
using (var doc = PdfDocument.Open(s, leaveOpen: true))
{
    PdfSignatureInfo info = doc.Signatures.Single();
    var cmsDerived = new[] { "SignerCertificate", "Certificates", "DigestAlgorithm", "DigestAlgorithmOid", "SignatureAlgorithmOid", "CmsSigningTime", "TimestampTime" };
    var props = typeof(PdfSignatureInfo).GetProperties().Select(p => (p.Name, Value: Describe(p.GetValue(info)))).ToList();
    observations.Add(new
    {
        id = "P1-library-signed-plus-metadata-revision",
        publicPropertyCount = props.Count,
        cmsDerivedProperties = cmsDerived,
        properties = props.ToDictionary(p => p.Name, p => p.Value),
        revisions = doc.Revisions.Select(r => new { r.Number, r.EndOffset, r.EndsAtSignatureByteRange }).ToList(),
        fileLength = later.Length,
    });
}

// P2: replace the first two hex digits of /Contents so the CMS is no longer SignedData (same length, offsets unchanged).
byte[] malformedCms = MutateContents(later);
observations.Add(Observe("P2-malformed-cms-contents", malformedCms, tryWrite: true));

// P3: same-length unknown SubFilter name, so no CMS is read.
byte[] unknownSubFilter = Replace(later, "adbe.pkcs7.detached", "adbe.pkcs7.dxtached");
observations.Add(Observe("P3-unknown-subfilter", unknownSubFilter, tryWrite: true));

// P4: damage the first revision's %%EOF so the historical revision footer is malformed (same length).
byte[] badFooter = ReplaceFirst(later, "%%EOF", "%%EOX");
observations.Add(Observe("P4-malformed-historical-footer", badFooter, tryWrite: true));

// P5: recovery path if the rich reader were removed. A caller with its own source bytes uses only the retained ByteRange to extract
// the /Contents gap, trims the zero padding, and decodes the CMS with an external library. Caller-side cryptographic verification is shown only
// to prove the extracted bytes are the real signature; the library under study performs none.
using (var s = new MemoryStream(later))
using (var doc = PdfDocument.Open(s, leaveOpen: true))
{
    PdfSignatureInfo info = doc.Signatures.Single();
    PdfByteRange range = info.ByteRange!.Value;
    string gap = Encoding.ASCII.GetString(later, (int)range.Length0, (int)(range.Offset1 - range.Length0));
    byte[] padded = Convert.FromHexString(gap.Trim('<', '>'));
    int declared = DerTotalLength(padded);
    byte[] cmsBytes = padded[..declared];
    bool onlyZeroPadding = padded[declared..].All(b => b == 0);
    var content = later.AsSpan(0, (int)range.Length0).ToArray().Concat(later.AsSpan((int)range.Offset1, (int)range.Length1).ToArray()).ToArray();
    var decoded = new SignedCms(new ContentInfo(content), detached: true);
    decoded.Decode(cmsBytes);
    decoded.CheckSignature(verifySignatureOnly: true);
    observations.Add(new
    {
        id = "P5-caller-side-recovery-from-retained-byte-range",
        extractedCmsBytes = cmsBytes.Length,
        onlyZeroPaddingRemoved = onlyZeroPadding,
        signerCertificateEqualsLibraryReader = decoded.SignerInfos[0].Certificate!.RawData.AsSpan().SequenceEqual(info.SignerCertificate),
        digestOidEqualsLibraryReader = decoded.SignerInfos[0].DigestAlgorithm.Value == info.DigestAlgorithmOid,
        externalCheckSignatureOnly = "passed",
        callerLinesOfCode = "about 12 including the DER length helper, plus a Pkcs/BouncyCastle/asn1crypto-class dependency owned by the caller",
    });
}

Console.WriteLine(JsonSerializer.Serialize(new { fixtureSha256 = Convert.ToHexString(SHA256.HashData(later)), observations }, new JsonSerializerOptions { WriteIndented = true }));

object Observe(string id, byte[] pdf, bool tryWrite)
{
    var result = new Dictionary<string, object?> { ["id"] = id };
    using var s = new MemoryStream(pdf);
    using var doc = PdfDocument.Open(s, leaveOpen: true);
    result["opens"] = true;
    result["signatures"] = Try(() => { var list = doc.Signatures; var i = list.Single(); return $"ok count={list.Count} subFilter={i.SubFilter} byteRangeStatus={i.ByteRangeStatus} revisionNumber={i.ByteRangeRevisionNumber} signerCertificate={(i.SignerCertificate == null ? "null" : "present")} timestampTime={i.TimestampTime?.ToString() ?? "null"}"; });
    result["revisions"] = Try(() => $"ok count={doc.Revisions.Count}");
    result["pages"] = Try(() => $"ok count={doc.Pages.Count}");
    result["permissions"] = Try(() => $"ok modify={doc.Permissions.CanModify}");
    if (tryWrite)
    {
        result["editorMetadataSave"] = Try(() =>
        {
            using var editor = doc.Edit();
            editor.Metadata.Title = "after";
            using var dest = new MemoryStream();
            editor.SaveIncremental(dest, leaveOpen: true);
            return $"ok appended={dest.Length - pdf.Length} bytes prefixPreserved={dest.ToArray().AsSpan(0, pdf.Length).SequenceEqual(pdf)}";
        });
    }

    return result;
}

static int DerTotalLength(byte[] data)
{
    int lengthByte = data[1];
    if (lengthByte < 0x80) return 2 + lengthByte;
    int count = lengthByte & 0x7F, length = 0;
    for (int i = 0; i < count; i++) length = (length << 8) | data[2 + i];
    return 2 + count + length;
}

static string Try(Func<string> action)
{
    try { return action(); }
    catch (Exception ex) { return $"{ex.GetType().Name}: {ex.Message}"; }
}

static string Describe(object? value) => value switch
{
    null => "null",
    byte[] b => $"byte[{b.Length}]",
    IReadOnlyList<byte[]> l => $"byte[][{l.Count}]",
    _ => value.ToString() ?? "null",
};

byte[] Sign(byte[] input)
{
    using var source = new MemoryStream(input);
    using var document = PdfDocument.Open(source, leaveOpen: true);
    using var editor = document.Edit();
    using var destination = new MemoryStream();
    editor.Sign(destination, options, new CmsDetachedSignatureProvider(new RawSigner(key, certificate)), leaveOpen: true);
    return destination.ToArray();
}

static byte[] Metadata(byte[] input, string title)
{
    using var source = new MemoryStream(input);
    using var document = PdfDocument.Open(source, leaveOpen: true);
    using var editor = document.Edit();
    editor.Metadata.Title = title;
    using var destination = new MemoryStream();
    editor.SaveIncremental(destination, leaveOpen: true);
    return destination.ToArray();
}

static byte[] Replace(byte[] data, string from, string to)
{
    if (from.Length != to.Length) throw new ArgumentException("same length required");
    byte[] needle = Encoding.ASCII.GetBytes(from);
    int index = data.AsSpan().IndexOf(needle);
    if (index < 0) throw new Exception("pattern not found: " + from);
    byte[] copy = (byte[])data.Clone();
    Encoding.ASCII.GetBytes(to).CopyTo(copy, index);
    return copy;
}

// Changes the second hex digit of the first /Contents string, so the CMS SEQUENCE tag 0x30 becomes 0x31 (a SET); length unchanged.
static byte[] MutateContents(byte[] data)
{
    int key = data.AsSpan().IndexOf(Encoding.ASCII.GetBytes("/Contents"));
    if (key < 0) throw new Exception("no /Contents");
    int lt = Array.IndexOf(data, (byte)'<', key);
    byte[] copy = (byte[])data.Clone();
    if (copy[lt + 1] != (byte)'3' || copy[lt + 2] != (byte)'0') throw new Exception("unexpected CMS start " + (char)copy[lt + 1] + (char)copy[lt + 2]);
    copy[lt + 2] = (byte)'1';
    return copy;
}

static byte[] ReplaceFirst(byte[] data, string from, string to) => Replace(data, from, to);

static byte[] Fixture()
{
    var body = new StringBuilder("%PDF-1.7\n");
    var offsets = new List<int>();
    foreach (string obj in new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Count 1 /Kids [3 0 R] >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>" })
    {
        offsets.Add(body.Length); body.Append($"{offsets.Count} 0 obj\n{obj}\nendobj\n");
    }
    int xref = body.Length;
    body.Append("xref\n0 4\r\n0000000000 65535 f\r\n");
    foreach (int offset in offsets) body.Append($"{offset:D10} 00000 n\r\n");
    body.Append($"trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    return Encoding.ASCII.GetBytes(body.ToString());
}

sealed class RawSigner(RSA key, X509Certificate2 certificate) : IPdfExternalSigner
{
    public byte[] CertificateDer => certificate.RawData;
    public IReadOnlyList<byte[]> AdditionalCertificates => Array.Empty<byte[]>();
    public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        => new(key.SignHash(hash.Span, hashAlgorithm, RSASignaturePadding.Pkcs1));
}
