using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace jaytwo.Ergonomics.Pdf.Comparison;

// The comparison workloads through this library, on file streams wrapped in a ProbeStream for the source bytes read.
// Writes are cross-stream incremental saves, as iText's are.
[Collection("comparison")]
public class ErgonomicsWorkloads
{
    private const string Library = "jaytwo.Ergonomics.Pdf";

    private readonly ITestOutputHelper _output;

    public ErgonomicsWorkloads(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void dense_inspect()
    {
        string source = ComparisonFixtures.Dense();
        Measure(Workloads.DenseInspect, Workloads.GibibytePasses, () => Inspect(source, null), () => { });
    }

    [Fact]
    public void dense_metadata_edit()
    {
        string source = ComparisonFixtures.Dense();
        string output = ComparisonFixtures.Output("dense-edited.pdf");
        Measure(Workloads.DenseEdit, Workloads.GibibytePasses, () => Edit(source, output, "Dense GiB edited"), () => OutputCheck.Edited(source, output, "Dense GiB edited"));
    }

    [Fact]
    public void dense_sign()
    {
        string source = ComparisonFixtures.Dense();
        string output = ComparisonFixtures.Output("dense-signed.pdf");
        _ = SigningKey.Certificate;
        Measure(Workloads.DenseSign, Workloads.GibibytePasses, () => Sign(source, output), () => OutputCheck.Signed(source, output, qpdf: false));
    }

    [Fact]
    public void attachment_add()
    {
        string source = ComparisonFixtures.Carrier();
        string payload = ComparisonFixtures.Payload();
        string output = ComparisonFixtures.Output("carrier-attached.pdf");
        Measure(
            Workloads.AttachmentAdd,
            Workloads.AttachmentPasses,
            () =>
            {
                using var file = ComparisonFixtures.OpenRead(source);
                using var content = ComparisonFixtures.OpenRead(payload);
                var probe = new ProbeStream(file);
                using var document = PdfDocument.Open(probe, leaveOpen: true);
                document.Attachments.Add(ComparisonFixtures.AttachmentName, content, leaveOpen: true, ComparisonFixtures.AttachmentOptions());
                using var destination = ComparisonFixtures.CreateFile(output);
                document.SaveIncremental(destination, leaveOpen: true);
                return probe.BytesRead;
            },
            () => OutputCheck.Attached(source, output, payload));
    }

    [Fact]
    public void attachment_extract()
    {
        string source = ComparisonFixtures.AttachedCarrier();
        string payload = ComparisonFixtures.Payload();
        string output = ComparisonFixtures.Output("extracted.csv");
        Measure(
            Workloads.AttachmentExtract,
            Workloads.AttachmentPasses,
            () =>
            {
                using var file = ComparisonFixtures.OpenRead(source);
                var probe = new ProbeStream(file);
                using var document = PdfDocument.Open(probe, leaveOpen: true);
                PdfAttachment attachment = document.Attachments[ComparisonFixtures.AttachmentName];
                Assert.Equal(ComparisonFixtures.PayloadLength, attachment.Size);
                using Stream opened = attachment.Open();
                using var destination = ComparisonFixtures.CreateFile(output);
                opened.CopyTo(destination);
                return probe.BytesRead;
            },
            () => OutputCheck.SameBytes(payload, output));
    }

    [Fact]
    public void form_extraction()
    {
        string source = ComparisonFixtures.Form();
        Measure(
            Workloads.FormExtraction,
            Workloads.FormPasses,
            () =>
            {
                using var file = ComparisonFixtures.OpenRead(source);
                var probe = new ProbeStream(file);
                using var document = PdfDocument.Open(probe, leaveOpen: true);
                Assert.Equal(ComparisonFixtures.FormPages, document.PageCount);
                Assert.Equal(ComparisonFixtures.FormPages, document.Pages.Count);
                IReadOnlyList<PdfField> fields = document.AcroForm.Fields;
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (PdfField field in fields)
                {
                    Expected.FormField(field.FullName, field.Value, field.IsChecked == true, field.PageNumber);
                    names.Add(field.FullName);
                }

                Assert.Equal(ComparisonFixtures.FormPages * 2, names.Count);
                return probe.BytesRead;
            },
            () => { });
    }

    [Fact]
    public void form_metadata_edit()
    {
        string source = ComparisonFixtures.Form();
        string output = ComparisonFixtures.Output("form-edited.pdf");
        Measure(Workloads.FormEdit, Workloads.FormPasses, () => Edit(source, output, "Five Hundred edited"), () => OutputCheck.Edited(source, output, "Five Hundred edited"));
    }

    [Fact]
    public void form_bulk_fill()
    {
        string source = ComparisonFixtures.Form();
        string output = ComparisonFixtures.Output("form-filled.pdf");
        Measure(
            Workloads.FormFill,
            Workloads.FormPasses,
            () =>
            {
                using var file = ComparisonFixtures.OpenRead(source);
                var probe = new ProbeStream(file);
                using var document = PdfDocument.Open(probe, leaveOpen: true);
                IReadOnlyDictionary<string, PdfAppearanceDisposition> filled = document.AcroForm.Fill(G7Fixtures.TextValues(ComparisonFixtures.FormPages));
                Assert.Equal(ComparisonFixtures.FormPages, filled.Count);
                using var destination = ComparisonFixtures.CreateFile(output);
                document.SaveIncremental(destination, leaveOpen: true);
                return probe.BytesRead;
            },
            () => OutputCheck.Filled(source, output, ComparisonFixtures.FormPages));
    }

    [Fact]
    public void form_sign()
    {
        string source = ComparisonFixtures.FilledForm();
        string output = ComparisonFixtures.Output("form-signed.pdf");
        _ = SigningKey.Certificate;
        Measure(Workloads.FormSign, Workloads.FormPasses, () => Sign(source, output), () => OutputCheck.Signed(source, output, qpdf: true));
    }

    [Fact]
    public void flatten()
    {
        string source = ComparisonFixtures.FilledFlattenForm();
        string output = ComparisonFixtures.Output("flatten-flattened.pdf");
        Measure(
            Workloads.Flatten,
            Workloads.FormPasses,
            () =>
            {
                using var file = ComparisonFixtures.OpenRead(source);
                var probe = new ProbeStream(file);
                using var document = PdfDocument.Open(probe, leaveOpen: true);
                document.AcroForm.Flatten();
                using var destination = ComparisonFixtures.CreateFile(output);
                document.SaveIncremental(destination, leaveOpen: true);
                return probe.BytesRead;
            },
            () => OutputCheck.Flattened(source, output, ComparisonFixtures.FlattenPages, incremental: true));
    }

    [Fact]
    public void encrypted_inspect_user()
    {
        string source = ComparisonFixtures.EncryptedDense();
        Measure(Workloads.EncryptedUser, Workloads.GibibytePasses, () => Inspect(source, "user-secret"), () => { });
    }

    [Fact]
    public void encrypted_inspect_owner()
    {
        string source = ComparisonFixtures.EncryptedDense();
        Measure(Workloads.EncryptedOwner, Workloads.GibibytePasses, () => Inspect(source, "owner-secret"), () => { });
    }

    private static long Inspect(string source, string? password)
    {
        using var file = ComparisonFixtures.OpenRead(source);
        var probe = new ProbeStream(file);
        using var document = password == null ? PdfDocument.Open(probe, leaveOpen: true) : PdfDocument.Open(probe, password, leaveOpen: true);
        Assert.Equal(G7Fixtures.DensePages, document.PageCount);
        Assert.Equal("Dense GiB", document.Metadata.Title);
        Assert.Equal("QA", document.Metadata.Author);
        Assert.Empty(document.Attachments);
        IReadOnlyList<PdfField> fields = document.AcroForm.Fields;
        Assert.Equal(G7Fixtures.DensePages, fields.Count);
        foreach (PdfField field in fields)
        {
            Expected.DenseField(field.FullName, field.Value, field.PageNumber);
        }

        return probe.BytesRead;
    }

    private static long Edit(string source, string output, string title)
    {
        using var file = ComparisonFixtures.OpenRead(source);
        var probe = new ProbeStream(file);
        using var document = PdfDocument.Open(probe, leaveOpen: true);
        document.Metadata.Title = title;
        document.Metadata.ModificationDate = ComparisonFixtures.FixedDate;
        using var destination = ComparisonFixtures.CreateFile(output);
        document.SaveIncremental(destination, leaveOpen: true);
        return probe.BytesRead;
    }

    private static long Sign(string source, string output)
    {
        using var file = ComparisonFixtures.OpenRead(source);
        var probe = new ProbeStream(file);
        using var document = PdfDocument.Open(probe, leaveOpen: true);
        using var destination = ComparisonFixtures.CreateFile(output);
        var options = new PdfSignatureOptions { Reason = "G7", SigningTime = ComparisonFixtures.FixedDate };
        document.Sign(destination, options, new CmsDetachedSignatureProvider(new ErgonomicsRsaSigner()), leaveOpen: true);
        return probe.BytesRead;
    }

    private void Measure(string workload, int passes, Func<long> operation, Action verify)
    {
        ComparisonRun.Measure(_output, workload, Library, passes, operation, verify);
    }
}
