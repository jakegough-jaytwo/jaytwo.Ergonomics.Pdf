using System;
using System.Collections.Generic;
using System.Text;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Filespec;
using iText.Signatures;
using Xunit;
using Xunit.Abstractions;
using ITextAcroForm = iText.Forms.PdfAcroForm;
using ITextDocument = iText.Kernel.Pdf.PdfDocument;

namespace jaytwo.Ergonomics.Pdf.Comparison;

// The comparison workloads through iText Core, in its best configuration for large files: it opens each file by path,
// and PdfSigner writes its interim output to a temporary file. dense_inspect_from_stream passes a Stream, as this
// library's callers do, and dense_sign_in_memory uses PdfSigner's default MemoryStream. Writes use append mode, so the
// source stays a byte prefix of the output, except flatten: iText refuses to flatten in append mode, so it rewrites
// the file and reports under its own label.
[Collection("comparison")]
public class ITextWorkloads
{
    private const string Library = "iText 9.8.0";

    private readonly ITestOutputHelper _output;

    public ITextWorkloads(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void dense_inspect()
    {
        string source = ComparisonFixtures.Dense();
        Measure(
            Workloads.DenseInspect,
            Workloads.GibibytePasses,
            () =>
            {
                using var pdf = new ITextDocument(new PdfReader(source));
                InspectDense(pdf);
                return -1;
            },
            () => { });
    }

    [Fact]
    public void dense_inspect_from_stream()
    {
        string source = ComparisonFixtures.Dense();
        ComparisonRun.Measure(
            _output,
            Workloads.DenseInspect,
            Library + ", Stream input",
            Workloads.GibibytePasses,
            () =>
            {
                using var file = ComparisonFixtures.OpenRead(source);
                var probe = new ProbeStream(file);
                using (var pdf = new ITextDocument(new PdfReader(probe)))
                {
                    InspectDense(pdf);
                }

                return probe.BytesRead;
            },
            () => { });
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
        string temporary = ComparisonFixtures.Output("dense-signed.tmp");
        var chain = ITextRsaSignature.Chain();
        Measure(Workloads.DenseSign, Workloads.GibibytePasses, () => Sign(source, output, chain, temporary), () => OutputCheck.Signed(source, output, qpdf: false));
    }

    [Fact]
    public void dense_sign_in_memory()
    {
        string source = ComparisonFixtures.Dense();
        string output = ComparisonFixtures.Output("dense-signed.pdf");
        var chain = ITextRsaSignature.Chain();
        ComparisonRun.Measure(_output, Workloads.DenseSign, Library + ", in-memory signer", Workloads.GibibytePasses, () => Sign(source, output, chain, null), () => OutputCheck.Signed(source, output, qpdf: false));
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
                using var content = ComparisonFixtures.OpenRead(payload);
                using (var pdf = Append(source, output))
                {
                    PdfFileSpec spec = PdfFileSpec.CreateEmbeddedFileSpec(pdf, content, "G7 payload", ComparisonFixtures.AttachmentName, new PdfName("text/csv"), null, null);
                    pdf.AddFileAttachment(ComparisonFixtures.AttachmentName, spec);
                }

                return -1;
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
                using var pdf = new ITextDocument(new PdfReader(source));
                PdfDictionary? spec = null;
                foreach (KeyValuePair<PdfString, PdfObject> entry in pdf.GetCatalog().GetNameTree(PdfName.EmbeddedFiles).GetNames())
                {
                    if (entry.Key.ToUnicodeString() == ComparisonFixtures.AttachmentName)
                    {
                        PdfObject value = entry.Value;
                        spec = (PdfDictionary)(value.IsIndirectReference() ? ((PdfIndirectReference)value).GetRefersTo() : value);
                    }
                }

                Assert.NotNull(spec);
                byte[] bytes = spec!.GetAsDictionary(PdfName.EF).GetAsStream(PdfName.F).GetBytes();
                using var destination = ComparisonFixtures.CreateFile(output);
                destination.Write(bytes, 0, bytes.Length);
                return -1;
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
                using var pdf = new ITextDocument(new PdfReader(source));
                Assert.Equal(ComparisonFixtures.FormPages, pdf.GetNumberOfPages());
                int count = Terminal(pdf, (name, field, page) =>
                {
                    string value = field.GetValueAsString();
                    Expected.FormField(name, value, value == "Yes", page);
                });
                Assert.Equal(ComparisonFixtures.FormPages * 2, count);
                return -1;
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
                using var pdf = Append(source, output);
                ITextAcroForm form = ITextAcroForm.GetAcroForm(pdf, false);
                for (int page = 1; page <= ComparisonFixtures.FormPages; page++)
                {
                    string number = page.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    form.GetField("page" + number + ".name").SetValue("Filled " + number);
                }

                return -1;
            },
            () => OutputCheck.Filled(source, output, ComparisonFixtures.FormPages));
    }

    [Fact]
    public void form_sign()
    {
        string source = ComparisonFixtures.FilledForm();
        string output = ComparisonFixtures.Output("form-signed.pdf");
        string temporary = ComparisonFixtures.Output("form-signed.tmp");
        var chain = ITextRsaSignature.Chain();
        Measure(Workloads.FormSign, Workloads.FormPasses, () => Sign(source, output, chain, temporary), () => OutputCheck.Signed(source, output, qpdf: true));
    }

    [Fact]
    public void flatten()
    {
        string source = ComparisonFixtures.FilledFlattenForm();
        string output = ComparisonFixtures.Output("flatten-flattened.pdf");
        ComparisonRun.Measure(
            _output,
            Workloads.Flatten,
            Library + ", full rewrite",
            Workloads.FormPasses,
            () =>
            {
                using var pdf = new ITextDocument(new PdfReader(source), new PdfWriter(ComparisonFixtures.CreateFile(output)));
                ITextAcroForm.GetAcroForm(pdf, false).FlattenFields();
                return -1;
            },
            () => OutputCheck.Flattened(source, output, ComparisonFixtures.FlattenPages, incremental: false));
    }

    [Fact]
    public void encrypted_inspect_user()
    {
        string source = ComparisonFixtures.EncryptedDense();
        Measure(Workloads.EncryptedUser, Workloads.GibibytePasses, () => InspectEncrypted(source, "user-secret"), () => { });
    }

    [Fact]
    public void encrypted_inspect_owner()
    {
        string source = ComparisonFixtures.EncryptedDense();
        Measure(Workloads.EncryptedOwner, Workloads.GibibytePasses, () => InspectEncrypted(source, "owner-secret"), () => { });
    }

    // A document that fails to open still holds its reader and writer, so they are closed before the exception leaves.
    private static ITextDocument Append(string source, string output)
    {
        var reader = new PdfReader(source);
        var writer = new PdfWriter(ComparisonFixtures.CreateFile(output));
        try
        {
            return new ITextDocument(reader, writer, new StampingProperties().UseAppendMode());
        }
        catch
        {
            writer.Close();
            reader.Close();
            throw;
        }
    }

    private static void InspectDense(ITextDocument pdf)
    {
        Assert.Equal(G7Fixtures.DensePages, pdf.GetNumberOfPages());
        PdfDocumentInfo info = pdf.GetDocumentInfo();
        Assert.Equal("Dense GiB", info.GetTitle());
        Assert.Equal("QA", info.GetAuthor());
        Assert.Null(pdf.GetCatalog().GetPdfObject().GetAsDictionary(PdfName.Names));
        int count = Terminal(pdf, (name, field, page) => Expected.DenseField(name, field.GetValueAsString(), page));
        Assert.Equal(G7Fixtures.DensePages, count);
    }

    private static long InspectEncrypted(string source, string password)
    {
        using var pdf = new ITextDocument(new PdfReader(source, new ReaderProperties().SetPassword(Encoding.ASCII.GetBytes(password))));
        InspectDense(pdf);
        return -1;
    }

    // Each terminal field with its fully qualified name and the number of the page its first widget is on, as this
    // library's PdfField reports them.
    private static int Terminal(ITextDocument pdf, Action<string, PdfFormField, int> check)
    {
        int count = 0;
        foreach (KeyValuePair<string, PdfFormField> pair in ITextAcroForm.GetAcroForm(pdf, false).GetAllFormFields())
        {
            if (pair.Value.GetChildFormFields().Count > 0)
            {
                continue;
            }

            check(pair.Key, pair.Value, pdf.GetPageNumber(pair.Value.GetWidgets()[0].GetPage()));
            count++;
        }

        return count;
    }

    private static long Edit(string source, string output, string title)
    {
        using var pdf = Append(source, output);
        pdf.GetDocumentInfo().SetTitle(title);
        return -1;
    }

    private static long Sign(string source, string output, iText.Commons.Bouncycastle.Cert.IX509Certificate[] chain, string? temporary)
    {
        using var reader = new PdfReader(source);
        using var destination = ComparisonFixtures.CreateFile(output);
        var signer = new PdfSigner(reader, destination, temporary, new StampingProperties().UseAppendMode());
        signer.SetSignerProperties(new SignerProperties().SetFieldName("Signature1").SetReason("G7").SetClaimedSignDate(ComparisonFixtures.FixedDate.UtcDateTime));
        signer.SignDetached(new ITextRsaSignature(), chain, null, null, null, 0, PdfSigner.CryptoStandard.CMS);
        return -1;
    }

    private void Measure(string workload, int passes, Func<long> operation, Action verify)
    {
        ComparisonRun.Measure(_output, workload, Library, passes, operation, verify);
    }
}
