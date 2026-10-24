using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.Internal;
using Xunit;
using Xunit.Abstractions;

namespace jaytwo.Pdf.Tests;

// W-167: baseline work per public operation on benign fixtures, for agents/reference/w-149-work-budgets.md. The tests
// report the counts; they assert only that each operation succeeded. Each operation opens the file afresh, so its
// counters include the open, which the "open" row reports alone.
[Collection("dense-memory")]
public class WorkBaselineTests
{
    private static readonly DateTimeOffset FixedDate = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly ITestOutputHelper _output;

    public WorkBaselineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [Trait(Lane.Trait, Lane.External)]
    [Trait(Lane.Trait, Lane.LargeData)]
    [InlineData(1000)]
    [InlineData(4000)]
    public void object_stream_form_operations(int pages)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string classicPath = Path.Combine(directory, "classic.pdf");
            string sourcePath = Path.Combine(directory, "form.pdf");
            string filledPath = Path.Combine(directory, "filled.pdf");
            G7Fixtures.WriteForm(classicPath, pages, "Baseline");
            G7Fixtures.ObjectStreamCopy(classicPath, sourcePath);
            using (var file = OpenRead(sourcePath))
            using (var document = PdfDocument.Open(file, leaveOpen: true))
            using (var editor = document.Edit())
            {
                editor.AcroForm.Fill(G7Fixtures.TextValues(pages));
                using var destination = CreateFile(filledPath);
                editor.SaveIncremental(destination, leaveOpen: true);
            }

            string size = pages.ToString(CultureInfo.InvariantCulture) + "-page";
            _output.WriteLine(size + " qpdf form: " + new FileInfo(sourcePath).Length.ToString(CultureInfo.InvariantCulture) + " bytes, " + ObjectStreams(sourcePath).ToString(CultureInfo.InvariantCulture) + " object streams; filled copy " + new FileInfo(filledPath).Length.ToString(CultureInfo.InvariantCulture) + " bytes");
            _output.WriteLine(Header());

            Measure(size + " open", sourcePath, (document, output) => Assert.Equal(pages, document.PageCount));
            Measure(size + " pages", sourcePath, (document, output) => Assert.Equal(pages, document.Pages.Count));
            Measure(size + " fields", sourcePath, (document, output) => Assert.Equal(pages * 2, document.AcroForm.Fields.Count));
            Measure(size + " fields twice", sourcePath, (document, output) =>
            {
                Assert.Equal(pages * 2, document.AcroForm.Fields.Count);
                Assert.Equal(pages * 2, document.AcroForm.Fields.Count);
            });
            Measure(size + " export", sourcePath, (document, output) => Assert.Equal(pages * 2, document.AcroForm.Export().Count));
            Measure(size + " metadata edit and save", sourcePath, (document, output) =>
            {
                using var editor = document.Edit();
                editor.Metadata.Title = "Edited";
                editor.Metadata.ModificationDate = FixedDate;
                editor.SaveIncremental(output, leaveOpen: true);
            });
            Measure(size + " bulk fill and save", sourcePath, (document, output) =>
            {
                using var editor = document.Edit();
                Assert.Equal(pages, editor.AcroForm.Fill(G7Fixtures.TextValues(pages)).Count);
                editor.SaveIncremental(output, leaveOpen: true);
            });
            Measure(size + " flatten and save", filledPath, (document, output) =>
            {
                using var editor = document.Edit();
                editor.AcroForm.Flatten();
                editor.SaveIncremental(output, leaveOpen: true);
            });
            using var rsa = RSA.Create(2048);
            byte[] certificate = SelfSigned(rsa);
            Measure(size + " sign", filledPath, (document, output) =>
            {
                using var editor = document.Edit();
                editor.Sign(output, new PdfSignatureOptions { Reason = "W-167", SigningTime = TestCompat.UnixEpoch }, new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate)), leaveOpen: true);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Corpus)]
    public void corpus_read_operations()
    {
        _output.WriteLine(Header() + " | failures");
        long maxParsesPerStream = 0;
        foreach (object[] row in CorpusManifest.Ids())
        {
            CorpusEntry entry = CorpusManifest.Get((string)row[0]);
            if (entry.Open != "ok")
            {
                continue;
            }

            byte[] pdf = File.ReadAllBytes(entry.FullPath);
            int streams = ObjectStreams(pdf);
            var failures = new List<string>();
            ProbeStream? probe = null;
            PdfWorkCounters? work = null;
            long decoded = 0;
            WorkloadMeter.Sample sample = WorkloadMeter.Measure(() =>
            {
                probe = new ProbeStream(new MemoryStream(pdf, writable: false));
                using var document = PdfDocument.Open(probe, entry.Password, leaveOpen: true);
                Try(failures, "pages", () => _ = document.Pages.Count);
                Try(failures, "metadata", () => _ = document.Metadata.Title);
                Try(failures, "fields", () => _ = document.AcroForm.Fields.Count);
                Try(failures, "export", () => _ = document.AcroForm.Export().Count);
                Try(failures, "attachments", () =>
                {
                    foreach (var attachment in document.Attachments)
                    {
                        using Stream content = attachment.Open();
                        content.CopyTo(Stream.Null);
                    }
                });
                Try(failures, "signatures", () => _ = document.Signatures.Count);
                Try(failures, "revisions", () => _ = document.Revisions.Count);
                work = document.WorkCounters;
                decoded = document.DecodedByteCount;
            });
            string label = entry.Id + " (" + pdf.Length.ToString(CultureInfo.InvariantCulture) + " bytes, " + streams.ToString(CultureInfo.InvariantCulture) + " object streams)";
            _output.WriteLine(Row(label, sample, probe!, work!, decoded) + " | " + (failures.Count == 0 ? "-" : string.Join(", ", failures)));
            if (streams > 0)
            {
                maxParsesPerStream = Math.Max(maxParsesPerStream, work!.ObjectStreamParses / streams);
            }
        }

        _output.WriteLine("largest object-stream parses per distinct object stream, in whole multiples: " + maxParsesPerStream.ToString(CultureInfo.InvariantCulture));
    }

    private static void Try(List<string> failures, string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is PdfException or NotSupportedException or InvalidOperationException)
        {
            failures.Add(operation + ": " + ex.GetType().Name);
        }
    }

    private static string Header()
    {
        return "workload | source bytes | read calls | decoded bytes | stage decodes | stage bytes | objstm parses | objstm evictions | cached evictions | object loads | resolutions | page visits | field visits | peak heap MiB | allocated MiB | time s";
    }

    private static string Row(string workload, WorkloadMeter.Sample sample, ProbeStream probe, PdfWorkCounters work, long decoded)
    {
        return string.Join(
            " | ",
            workload,
            N(probe.BytesRead),
            N(probe.ReadCalls),
            N(decoded),
            N(work.FilterStageDecodes),
            N(work.FilterStageBytes),
            N(work.ObjectStreamParses),
            N(work.ObjectStreamEvictions),
            N(work.CachedObjectEvictions),
            N(work.ObjectLoads),
            N(work.ReferenceResolutions),
            N(work.PageNodeVisits),
            N(work.FieldNodeVisits),
            (sample.PeakHeapGrowth / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture),
            (sample.Allocated / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture),
            sample.Elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture));
    }

    private static string N(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static int ObjectStreams(string path)
    {
        return ObjectStreams(File.ReadAllBytes(path));
    }

    private static int ObjectStreams(byte[] pdf)
    {
        return Regex.Matches(TestCompat.Latin1.GetString(pdf), @"/Type\s*/ObjStm").Count;
    }

    private static FileStream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private static FileStream CreateFile(string path)
    {
        return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    }

    private static byte[] SelfSigned(RSA rsa)
    {
        var request = new CertificateRequest("CN=W-167 Baseline", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificate.RawData;
    }

    // The probe sees only source reads; a save or a signature writes to a fresh file beside the source.
    private void Measure(string workload, string path, Action<PdfDocument, Stream> operation)
    {
        string outputPath = Path.Combine(Path.GetDirectoryName(path)!, "output.pdf");
        ProbeStream? probe = null;
        PdfWorkCounters? work = null;
        long decoded = 0;
        WorkloadMeter.Sample sample = WorkloadMeter.Measure(() =>
        {
            using var file = OpenRead(path);
            using var output = CreateFile(outputPath);
            probe = new ProbeStream(file);
            using var document = PdfDocument.Open(probe, leaveOpen: true);
            operation(document, output);
            work = document.WorkCounters;
            decoded = document.DecodedByteCount;
        });
        File.Delete(outputPath);
        _output.WriteLine(Row(workload, sample, probe!, work!, decoded));
    }

    private sealed class RsaSigner : IPdfExternalSigner
    {
        private readonly RSA _rsa;

        public RsaSigner(RSA rsa, byte[] certificate)
        {
            _rsa = rsa;
            CertificateDer = certificate;
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates { get; } = Array.Empty<byte[]>();

        public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(_rsa.SignHash(hash.ToArray(), hashAlgorithm, RSASignaturePadding.Pkcs1));
        }
    }
}
