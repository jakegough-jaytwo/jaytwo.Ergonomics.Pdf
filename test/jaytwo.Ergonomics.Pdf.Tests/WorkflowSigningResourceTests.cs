#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using jaytwo.Pdf;
using jaytwo.Pdf.Attachments;
using jaytwo.Pdf.Signatures;
using jaytwo.Pdf.Tests;
using Xunit;
using Xunit.Abstractions;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Run each case in a separate Release process, coverage off: WorkloadMeter counts the process heap.
public class WorkflowSigningResourceTests
{
    private const long Gibibyte = 1024L * 1024 * 1024;
    private const long Budget = 256L * 1024 * 1024;

    private readonly ITestOutputHelper _output;

    public WorkflowSigningResourceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void dense_gibibyte_workflow_signing_uses_bounded_memory_and_file_staging()
    {
        Measure(attachment: false);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void gibibyte_attachment_workflow_signing_uses_bounded_memory_and_file_staging()
    {
        Measure(attachment: true);
    }

    private static void AssertPrefix(string input, string output)
    {
        using var original = File.OpenRead(input);
        using var signed = File.OpenRead(output);
        var left = new byte[64 * 1024];
        var right = new byte[left.Length];
        int count;
        while ((count = original.Read(left, 0, left.Length)) > 0)
        {
            signed.ReadExactly(right, 0, count);
            Assert.True(left.AsSpan(0, count).SequenceEqual(right.AsSpan(0, count)));
        }
    }

    private void Measure(bool attachment)
    {
        using var directory = new TempDirectory();
        string input = directory.File("input.pdf");
        string output = directory.File("signed.pdf");
        string payload = directory.File("payload.bin");
        if (attachment)
        {
            File.WriteAllBytes(input, WorkflowPdf.WithInfo());
            using var file = File.Create(payload);
            var block = new byte[1024 * 1024];
            for (long written = 0; written < Gibibyte; written += block.Length)
            {
                RandomNumberGenerator.Fill(block);
                file.Write(block, 0, block.Length);
            }
        }
        else
        {
            G7Fixtures.WriteDense(input);
        }

        using AsymmetricAlgorithm key = TestKeys.Create(TestKeys.Rsa);
        using X509Certificate2 certificate = TestKeys.SelfSigned("CN=Workflow Resources", key);
        long stagingBytes = 0;
        long diskBytesAtSign = 0;
        var signer = new KeySigner(key, certificate.RawData, Array.Empty<byte[]>(), signature =>
        {
            Assert.False(File.Exists(output));
            string staging = Assert.Single(directory.Temporaries());
            stagingBytes = new FileInfo(directory.File(staging)).Length;
            diskBytesAtSign = Directory.GetFiles(directory.Path).Sum(path => new FileInfo(path).Length);
            return signature;
        });
        using var source = new ProbeStream(File.OpenRead(input));
        WorkloadMeter.Sample sample = WorkloadMeter.Measure(() =>
        {
            PdfWorkflowBuilder workflow = PdfWorkflow.From(source).WithTitle("Signed large workflow");
            if (attachment)
            {
                workflow.AttachFile("payload.bin", payload);
            }

            workflow.Sign(output, signer, new PdfSignatureOptions());
        });
        long inputBytes = new FileInfo(input).Length;
        long outputBytes = new FileInfo(output).Length;
        Assert.True(inputBytes >= Gibibyte || new FileInfo(payload).Length == Gibibyte);
        Assert.True(stagingBytes >= (attachment ? Gibibyte : inputBytes));
        Assert.True(sample.PeakHeapGrowth < Budget, sample.Describe("workflow signing", Budget));
        Assert.InRange(source.MaxRequested, 1, 1024 * 1024);
        Assert.Empty(directory.Temporaries());
        AssertPrefix(input, output);
        using PdfDocument document = PdfDocument.Open(File.OpenRead(output), leaveOpen: false);
        Assert.Single(document.Signatures);
        Assert.Equal("Signed large workflow", document.Metadata.Title);
        if (attachment)
        {
            PdfAttachment saved = Assert.Single(document.Attachments);
            using Stream content = saved.Open();
            using var expected = File.OpenRead(payload);
            Assert.Equal(SHA256.HashData(expected), SHA256.HashData(content));
        }

        string name = attachment ? "attachment" : "dense";
        _output.WriteLine(sample.Describe(name, Budget));
        string json = JsonSerializer.Serialize(
            new
            {
            workload = name,
            os = RuntimeInformation.OSDescription,
            framework = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            cpus = Environment.ProcessorCount,
            inputBytes,
            attachmentBytes = attachment ? Gibibyte : 0,
            outputBytes,
            stagingBytesAtSign = stagingBytes,
            totalLogicalDiskBytesAtSign = diskBytesAtSign,
            peakManagedHeapGrowthBytes = sample.PeakHeapGrowth,
            allocatedBytes = sample.Allocated,
            seconds = sample.Elapsed.TotalSeconds,
            samples = sample.Samples,
            inputBytesRead = source.BytesRead,
            maximumInputReadRequest = source.MaxRequested,
            prefixPreserved = true,
            stagingRemoved = true,
            },
            new JsonSerializerOptions { WriteIndented = true });
        _output.WriteLine(json);
        string? evidence = Environment.GetEnvironmentVariable("JAYTWO_R4_EVIDENCE");
        if (evidence != null)
        {
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, name + "-resources.json"), json);
        }
    }
}
#endif
