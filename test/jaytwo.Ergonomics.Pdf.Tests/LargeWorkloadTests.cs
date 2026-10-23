using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
#if NET8_0_OR_GREATER
using System.Security.Cryptography.Pkcs;
#endif
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace jaytwo.Ergonomics.Pdf.Tests;

// QUALIFICATION.md G7 workloads under the D-20 working budget. Fixtures are written to real files in a
// temp directory at test time and deleted afterward. Each measured window covers only library calls on
// file streams; fixture generation and verification run outside the windows. See WorkloadMeter. The
// heap counter is process-wide, so `make g7-workloads` runs each test in its own Release process for the
// fixture budgets in agents/evidence/qualification/memory-budgets.md.
[Collection("dense-memory")]
public class LargeWorkloadTests
{
    // D-20: inspect, edit, and sign.
    private const long SimpleBudget = 256L * 1024 * 1024;

    // D-20: complex structural traversals.
    private const long TraversalBudget = 512L * 1024 * 1024;

    private const long OneGibibyte = 1024L * 1024 * 1024;
    private const int DensePages = G7Fixtures.DensePages;
    private const int FormPages = 500;
    private const int FlattenPages = 1000;
    private const int Cycles = 20;

    // Reads that stay this small on a 1 GiB file cannot include a signed range or a discovery scan.
    private const long SmallReadLimit = 16L * 1024 * 1024;

    // Retained-heap drift allowed across repeated use, after the warm-up cycle.
    private const long RetainedDrift = 1024 * 1024;

    // Every date a workload writes, so a run's outputs compare byte for byte with a run at another revision.
    private static readonly DateTimeOffset FixedDate = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly ITestOutputHelper _output;

    public LargeWorkloadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void dense_gibibyte_pdf_is_inspected_edited_and_signed_within_budget()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "dense.pdf");
            string editedPath = Path.Combine(directory, "dense-edited.pdf");
            string signedPath = Path.Combine(directory, "dense-signed.pdf");
            G7Fixtures.WriteDense(sourcePath);
            long sourceLength = new FileInfo(sourcePath).Length;
            Assert.True(sourceLength >= OneGibibyte, "dense fixture is " + sourceLength.ToString(CultureInfo.InvariantCulture) + " bytes");
            _output.WriteLine("dense source " + sourceLength.ToString(CultureInfo.InvariantCulture) + " bytes, " + DensePages.ToString(CultureInfo.InvariantCulture) + " pages of random 8-bit RGB images");

            ProbeStream? inspectProbe = null;
            long inspectDecoded = 0;
            WorkloadMeter.Sample inspect = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(sourcePath);
                inspectProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(inspectProbe, leaveOpen: true);
                Assert.Equal(DensePages, document.PageCount);
                Assert.Equal(DensePages, document.Pages.Count);
                Assert.Equal("Dense GiB", document.Metadata.Title);
                Assert.Equal("QA", document.Metadata.Author);
                Assert.Empty(document.Attachments);
                IReadOnlyList<PdfField> fields = document.AcroForm.Fields;
                Assert.Equal(DensePages, fields.Count);
                for (int i = 0; i < fields.Count; i++)
                {
                    Assert.Equal("field" + (i + 1).ToString(CultureInfo.InvariantCulture), fields[i].FullName);
                    Assert.Equal("value " + (i + 1).ToString(CultureInfo.InvariantCulture), fields[i].Value);
                    Assert.Equal(i + 1, fields[i].PageNumber);
                }

                inspectDecoded = document.DecodedByteCount;
            });
            Report("dense inspect", inspect, SimpleBudget, inspectProbe!, inspectDecoded);

            ProbeStream? editProbe = null;
            long editDecoded = 0;
            WorkloadMeter.Sample edit = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(sourcePath);
                editProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(editProbe, leaveOpen: true);
                document.Metadata.Title = "Dense GiB edited";
                document.Metadata.ModificationDate = FixedDate;
                using var destination = CreateFile(editedPath);
                document.SaveIncremental(destination, leaveOpen: true);
                editDecoded = document.DecodedByteCount;
            });
            Report("dense metadata edit", edit, SimpleBudget, editProbe!, editDecoded);
            ReportOutput("dense metadata edit", editedPath);

            long editedLength = new FileInfo(editedPath).Length;
            Assert.True(editedLength > sourceLength);
            Assert.Equal(HashFile(sourcePath, 0, sourceLength), HashFile(editedPath, 0, sourceLength));

            ProbeStream? signProbe = null;
            long signDecoded = 0;
            using var rsa = RSA.Create(2048);
            byte[] certificate = SelfSigned(rsa);
            WorkloadMeter.Sample sign = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(editedPath);
                signProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(signProbe, leaveOpen: true);
                Assert.Equal("Dense GiB edited", document.Metadata.Title);
                using var destination = CreateFile(signedPath);
                document.Sign(destination, new PdfSignatureOptions { Reason = "G7", SigningTime = TestCompat.UnixEpoch }, new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate)), leaveOpen: true);
                signDecoded = document.DecodedByteCount;
            });
            Report("dense sign", sign, SimpleBudget, signProbe!, signDecoded);

            long signedLength = new FileInfo(signedPath).Length;
            _output.WriteLine("dense edited " + editedLength.ToString(CultureInfo.InvariantCulture) + " bytes, signed " + signedLength.ToString(CultureInfo.InvariantCulture) + " bytes");
            Assert.Equal(HashFile(editedPath, 0, editedLength), HashFile(signedPath, 0, editedLength));
            ReportSigned("dense sign", VerifySignatureCoverage(signedPath, editedLength, verifyCms: false));
            using (var file = OpenRead(signedPath))
            using (var document = PdfDocument.Open(file, leaveOpen: true))
            {
                Assert.Equal("Dense GiB edited", document.Metadata.Title);
                Assert.Contains(document.AcroForm.Fields, field => field.FieldType == PdfFieldType.Signature);
            }

            // Signature inspection and revision listing report the coverage from the xref sections, the
            // field tree, and the signature dictionary, so neither reads the signed ranges.
            ProbeStream? signaturesProbe = null;
            long signaturesDecoded = 0;
            WorkloadMeter.Sample signatures = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(signedPath);
                signaturesProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(signaturesProbe, leaveOpen: true);
                PdfSignatureInfo signature = Assert.Single(document.Signatures);
                Assert.True(signature.IsSigned);
                Assert.Equal("G7", signature.Reason);
                Assert.Equal(signedLength, signature.SignedRevisionEnd);
                Assert.Equal(3, signature.SignedRevisionNumber);
                Assert.NotNull(signature.SignerCertificate);
                signaturesDecoded = document.DecodedByteCount;
            });
            Report("dense signature inspection", signatures, SimpleBudget, signaturesProbe!, signaturesDecoded);

            ProbeStream? revisionsProbe = null;
            long revisionsDecoded = 0;
            WorkloadMeter.Sample revisions = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(signedPath);
                revisionsProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(revisionsProbe, leaveOpen: true);
                IReadOnlyList<PdfRevision> listed = document.Revisions;
                Assert.Equal(3, listed.Count);
                Assert.Equal(new[] { sourceLength, editedLength, signedLength }, new[] { listed[0].EndOffset, listed[1].EndOffset, listed[2].EndOffset });
                Assert.Equal(new[] { false, false, true }, new[] { listed[0].IsCoveredBySignature, listed[1].IsCoveredBySignature, listed[2].IsCoveredBySignature });
                revisionsDecoded = document.DecodedByteCount;
            });
            Report("dense revision listing", revisions, SimpleBudget, revisionsProbe!, revisionsDecoded);

            // No whole-document materialization: inspection reads a small part of the file, and no
            // window's heap grows by anything near the file size.
            Assert.True(inspectProbe!.BytesRead < 16L * 1024 * 1024, "inspect read " + inspectProbe.BytesRead.ToString(CultureInfo.InvariantCulture) + " bytes");
            Assert.True(editProbe!.MaxRequested <= 1024 * 1024, "edit requested " + editProbe.MaxRequested.ToString(CultureInfo.InvariantCulture) + " bytes in one read");
            Assert.True(signaturesProbe!.BytesRead < SmallReadLimit, "signature inspection read " + signaturesProbe.BytesRead.ToString(CultureInfo.InvariantCulture) + " bytes");
            Assert.True(revisionsProbe!.BytesRead < SmallReadLimit, "revision listing read " + revisionsProbe.BytesRead.ToString(CultureInfo.InvariantCulture) + " bytes");
            AssertBudget("dense inspect", inspect, SimpleBudget);
            AssertBudget("dense metadata edit", edit, SimpleBudget);
            AssertBudget("dense sign", sign, SimpleBudget);
            AssertBudget("dense signature inspection", signatures, SimpleBudget);
            AssertBudget("dense revision listing", revisions, SimpleBudget);
            AssertSourceIo("dense inspect", inspectProbe, inspectDecoded, 9320000, 146, 0);
            AssertSourceIo("dense metadata edit", editProbe, editDecoded, 1200000000, 1280, 0);
            AssertSourceIo("dense sign", signProbe!, signDecoded, 2380000000, 290000, 0);
            AssertSourceIo("dense signature inspection", signaturesProbe, signaturesDecoded, 9480000, 151, 0);
            AssertSourceIo("dense revision listing", revisionsProbe, revisionsDecoded, 9480000, 151, 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.External)]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void large_attachment_is_added_and_extracted_through_file_streams_within_budget()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string pdfPath = Path.Combine(directory, "carrier.pdf");
            string payloadPath = Path.Combine(directory, "payload.csv");
            string extractedPath = Path.Combine(directory, "extracted.csv");
            // qpdf --check below needs a page with /Resources.
            File.WriteAllBytes(pdfPath, PdfBuilder.Basic(pageExtra: " /Resources << >>").Finish());
            long payloadLength = OneGibibyte + 4097;
            using (var payload = CreateFile(payloadPath))
            {
                G7Fixtures.WriteRecords(payload, payloadLength, 0x5EED0250UL);
            }

            long carrierLength = new FileInfo(pdfPath).Length;
            byte[] carrierHash = HashFile(pdfPath, 0, carrierLength);
            _output.WriteLine("attachment payload " + payloadLength.ToString(CultureInfo.InvariantCulture) + " bytes, carrier " + carrierLength.ToString(CultureInfo.InvariantCulture) + " bytes");

            // The payload probe checks the read size; the carrier probe sees the source reads of a same-stream save.
            ProbeStream? addProbe = null;
            ProbeStream? carrierProbe = null;
            long addDecoded = 0;
            WorkloadMeter.Sample add = WorkloadMeter.Measure(() =>
            {
                using var pdf = new FileStream(pdfPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                using var content = OpenRead(payloadPath);
                addProbe = new ProbeStream(content);
                carrierProbe = new ProbeStream(pdf);
                using var document = PdfDocument.Open(carrierProbe, leaveOpen: true);
                document.Attachments.Add("payload.csv", addProbe, leaveOpen: true, new PdfAttachmentOptions { Description = "G7 payload", MimeType = "text/csv", Compress = true, CreationDate = FixedDate, ModificationDate = FixedDate });
                document.SaveIncremental();
                addDecoded = document.DecodedByteCount;
            });
            Report("compressed attachment add and save", add, SimpleBudget, carrierProbe!, addDecoded);
            ReportOutput("compressed attachment add and save", pdfPath);

            // The payload compresses, so the save grows the carrier by less than half the payload.
            long savedLength = new FileInfo(pdfPath).Length;
            Assert.True(savedLength - carrierLength < payloadLength / 2, "the save added " + (savedLength - carrierLength).ToString(CultureInfo.InvariantCulture) + " bytes");
            _output.WriteLine("attachment carrier after save " + savedLength.ToString(CultureInfo.InvariantCulture) + " bytes");

            ProbeStream? extractProbe = null;
            long extractDecoded = 0;
            WorkloadMeter.Sample extract = WorkloadMeter.Measure(() =>
            {
                using var pdf = OpenRead(pdfPath);
                extractProbe = new ProbeStream(pdf);
                using var document = PdfDocument.Open(extractProbe, leaveOpen: true);
                PdfAttachment attachment = document.Attachments["payload.csv"];
                Assert.Equal(payloadLength, attachment.Size);
                Assert.Equal("G7 payload", attachment.Description);
                using Stream opened = attachment.Open();
                using var destination = CreateFile(extractedPath);
                opened.CopyTo(destination);
                extractDecoded = document.DecodedByteCount;
            });
            Report("compressed attachment reopen and extract", extract, SimpleBudget, extractProbe!, extractDecoded);

            Assert.Equal(payloadLength, new FileInfo(extractedPath).Length);
            Assert.Equal(HashFile(payloadPath, 0, payloadLength), HashFile(extractedPath, 0, payloadLength));
            Assert.Equal(carrierHash, HashFile(pdfPath, 0, carrierLength));
            Assert.True(addProbe!.MaxRequested <= 64 * 1024, "add requested " + addProbe.MaxRequested.ToString(CultureInfo.InvariantCulture) + " bytes in one read");

            // qpdf inflates every stream it checks, so it also confirms the Flate body independently.
            string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
            using (Process process = ExternalTool.Start(qpdf, "--check", pdfPath))
            {
                ExternalTool.Output(process);
            }

            AssertBudget("compressed attachment add and save", add, SimpleBudget);
            AssertBudget("compressed attachment reopen and extract", extract, SimpleBudget);
            AssertSourceIo("compressed attachment add and save", carrierProbe!, addDecoded, 1300, 6, 0);
            AssertSourceIo("compressed attachment reopen and extract", extractProbe!, extractDecoded, 364000000, 44300, 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.External)]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void five_hundred_page_form_is_extracted_edited_and_signed_within_budget()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string classicPath = Path.Combine(directory, "form-classic.pdf");
            string sourcePath = Path.Combine(directory, "form.pdf");
            string editedPath = Path.Combine(directory, "form-edited.pdf");
            string filledPath = Path.Combine(directory, "form-filled.pdf");
            string signedPath = Path.Combine(directory, "form-signed.pdf");
            G7Fixtures.WriteForm(classicPath, FormPages, "Five Hundred");
            string qpdf = G7Fixtures.ObjectStreamCopy(classicPath, sourcePath);
            long sourceLength = new FileInfo(sourcePath).Length;
            _output.WriteLine("form classic " + new FileInfo(classicPath).Length.ToString(CultureInfo.InvariantCulture) + " bytes, qpdf object-stream source " + sourceLength.ToString(CultureInfo.InvariantCulture) + " bytes");

            ProbeStream? extractProbe = null;
            long extractDecoded = 0;
            WorkloadMeter.Sample extract = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(sourcePath);
                extractProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(extractProbe, leaveOpen: true);
                Assert.Equal(FormPages, document.PageCount);
                IReadOnlyList<PdfPage> pages = document.Pages;
                Assert.Equal(FormPages, pages.Count);
                Assert.Equal(FormPages, pages[FormPages - 1].Number);
                IReadOnlyList<PdfField> fields = document.AcroForm.Fields;
                Assert.Equal(FormPages * 2, fields.Count);
                for (int page = 1; page <= FormPages; page++)
                {
                    string number = page.ToString(CultureInfo.InvariantCulture);
                    PdfField name = fields[(page - 1) * 2];
                    PdfField agree = fields[((page - 1) * 2) + 1];
                    Assert.Equal("page" + number + ".name", name.FullName);
                    Assert.Equal(PdfFieldType.Text, name.FieldType);
                    Assert.Equal("Name " + number, name.Value);
                    Assert.Equal(page, name.PageNumber);
                    Assert.Equal("page" + number + ".agree", agree.FullName);
                    Assert.Equal(PdfFieldType.Button, agree.FieldType);
                    Assert.Equal(page % 2 == 0, agree.IsChecked);
                    Assert.Equal(page, agree.PageNumber);
                }

                extractDecoded = document.DecodedByteCount;
            });
            Report("500-page form extraction", extract, TraversalBudget, extractProbe!, extractDecoded);

            ProbeStream? editProbe = null;
            long editDecoded = 0;
            WorkloadMeter.Sample edit = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(sourcePath);
                editProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(editProbe, leaveOpen: true);
                document.Metadata.Title = "Five Hundred edited";
                document.Metadata.Subject = "G7";
                document.Metadata.ModificationDate = FixedDate;
                using var destination = CreateFile(editedPath);
                document.SaveIncremental(destination, leaveOpen: true);
                editDecoded = document.DecodedByteCount;
            });
            Report("500-page metadata edit", edit, SimpleBudget, editProbe!, editDecoded);
            ReportOutput("500-page metadata edit", editedPath);

            long editedLength = new FileInfo(editedPath).Length;
            Assert.Equal(HashFile(sourcePath, 0, sourceLength), HashFile(editedPath, 0, sourceLength));

            ProbeStream? fillProbe = null;
            long fillDecoded = 0;
            WorkloadMeter.Sample fill = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(editedPath);
                fillProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(fillProbe, leaveOpen: true);
                IReadOnlyDictionary<string, PdfAppearanceDisposition> filled = document.AcroForm.Fill(G7Fixtures.TextValues(FormPages));
                Assert.Equal(FormPages, filled.Count);
                Assert.All(filled.Values, disposition => Assert.Equal(PdfAppearanceDisposition.Generated, disposition));
                using var destination = CreateFile(filledPath);
                document.SaveIncremental(destination, leaveOpen: true);
                fillDecoded = document.DecodedByteCount;
            });
            Report("500-page bulk fill of 500 fields", fill, SimpleBudget, fillProbe!, fillDecoded);
            ReportOutput("500-page bulk fill of 500 fields", filledPath);

            long filledLength = new FileInfo(filledPath).Length;
            Assert.Equal(HashFile(editedPath, 0, editedLength), HashFile(filledPath, 0, editedLength));

            ProbeStream? signProbe = null;
            long signDecoded = 0;
            using var rsa = RSA.Create(2048);
            byte[] certificate = SelfSigned(rsa);
            WorkloadMeter.Sample sign = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(filledPath);
                signProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(signProbe, leaveOpen: true);
                using var destination = CreateFile(signedPath);
                document.Sign(destination, new PdfSignatureOptions { Reason = "G7", SigningTime = TestCompat.UnixEpoch }, new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate)), leaveOpen: true);
                signDecoded = document.DecodedByteCount;
            });
            Report("500-page sign", sign, SimpleBudget, signProbe!, signDecoded);

            long signedLength = new FileInfo(signedPath).Length;
            _output.WriteLine("form edited " + editedLength.ToString(CultureInfo.InvariantCulture) + " bytes, filled " + filledLength.ToString(CultureInfo.InvariantCulture) + " bytes, signed " + signedLength.ToString(CultureInfo.InvariantCulture) + " bytes");
            Assert.Equal(HashFile(filledPath, 0, filledLength), HashFile(signedPath, 0, filledLength));
            ReportSigned("500-page sign", VerifySignatureCoverage(signedPath, filledLength, verifyCms: true));
            using (var file = OpenRead(signedPath))
            using (var document = PdfDocument.Open(file, leaveOpen: true))
            {
                Assert.Equal("Five Hundred edited", document.Metadata.Title);
                Assert.Equal("G7", document.Metadata.Subject);
                IReadOnlyList<PdfField> fields = document.AcroForm.Fields;
                Assert.Equal((FormPages * 2) + 1, fields.Count);
                for (int page = 1; page <= FormPages; page++)
                {
                    Assert.Equal("Filled " + page.ToString(CultureInfo.InvariantCulture), fields[(page - 1) * 2].Value);
                }

                Assert.Equal(signedLength, Assert.Single(document.Signatures).SignedRevisionEnd);
            }

            using (Process process = ExternalTool.Start(qpdf, "--check", signedPath))
            {
                ExternalTool.Output(process);
            }

            AssertBudget("500-page form extraction", extract, TraversalBudget);
            AssertBudget("500-page metadata edit", edit, SimpleBudget);
            AssertBudget("500-page bulk fill of 500 fields", fill, SimpleBudget);
            AssertBudget("500-page sign", sign, SimpleBudget);
            AssertSourceIo("500-page form extraction", extractProbe!, extractDecoded, 369000, 139, 1840000);
            AssertSourceIo("500-page metadata edit", editProbe!, editDecoded, 961000, 335, 4450000);
            AssertSourceIo("500-page bulk fill of 500 fields", fillProbe!, fillDecoded, 697000, 118, 1480000);
            AssertSourceIo("500-page sign", signProbe!, signDecoded, 1970000, 314, 2300000);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.External)]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void thousand_page_form_is_flattened_within_budget()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string classicPath = Path.Combine(directory, "flatten-classic.pdf");
            string sourcePath = Path.Combine(directory, "flatten.pdf");
            string filledPath = Path.Combine(directory, "flatten-filled.pdf");
            string flattenedPath = Path.Combine(directory, "flatten-flattened.pdf");
            G7Fixtures.WriteForm(classicPath, FlattenPages, "One Thousand");
            string qpdf = G7Fixtures.ObjectStreamCopy(classicPath, sourcePath);

            // Flatten refuses a field that shows a value without an appearance, so the text fields are filled
            // first, outside the measured window.
            using (var file = OpenRead(sourcePath))
            using (var document = PdfDocument.Open(file, leaveOpen: true))
            {
                document.AcroForm.Fill(G7Fixtures.TextValues(FlattenPages));
                using var destination = CreateFile(filledPath);
                document.SaveIncremental(destination, leaveOpen: true);
            }

            long filledLength = new FileInfo(filledPath).Length;
            _output.WriteLine("flatten source " + new FileInfo(sourcePath).Length.ToString(CultureInfo.InvariantCulture) + " bytes, filled " + filledLength.ToString(CultureInfo.InvariantCulture) + " bytes, " + (FlattenPages * 2).ToString(CultureInfo.InvariantCulture) + " fields");

            ProbeStream? flattenProbe = null;
            long flattenDecoded = 0;
            WorkloadMeter.Sample flatten = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(filledPath);
                flattenProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(flattenProbe, leaveOpen: true);
                document.AcroForm.Flatten();
                using var destination = CreateFile(flattenedPath);
                document.SaveIncremental(destination, leaveOpen: true);
                flattenDecoded = document.DecodedByteCount;
            });
            Report("1,000-page flatten", flatten, TraversalBudget, flattenProbe!, flattenDecoded);
            ReportOutput("1,000-page flatten", flattenedPath);

            long flattenedLength = new FileInfo(flattenedPath).Length;
            _output.WriteLine("flattened " + flattenedLength.ToString(CultureInfo.InvariantCulture) + " bytes");
            Assert.Equal(HashFile(filledPath, 0, filledLength), HashFile(flattenedPath, 0, filledLength));
            using (var file = OpenRead(flattenedPath))
            using (var document = PdfDocument.Open(file, leaveOpen: true))
            {
                Assert.Equal(FlattenPages, document.PageCount);
                Assert.Equal("One Thousand", document.Metadata.Title);
                Assert.Empty(document.AcroForm.Fields);
            }

            using (Process process = ExternalTool.Start(qpdf, "--check", flattenedPath))
            {
                ExternalTool.Output(process);
            }

            AssertBudget("1,000-page flatten", flatten, TraversalBudget);
            AssertSourceIo("1,000-page flatten", flattenProbe!, flattenDecoded, 2970000, 344, 4520000);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // W-108: not a G7 workload. Flatten and sign on the G7 form shape at two sizes, so the source I/O gates show how
    // the operations scale. The heap is not measured here.
    [Theory]
    [Trait(Lane.Trait, Lane.External)]
    [Trait(Lane.Trait, Lane.LargeData)]
    [InlineData(1000, 2970000, 344, 4520000, 3330000, 597, 4560000)]
    [InlineData(4000, 11900000, 1340, 18400000, 13300000, 2350, 18500000)]
    public void object_stream_form_flatten_and_sign_read_in_proportion_to_the_form(int pages, long flattenBytes, int flattenCalls, long flattenDecodedBytes, long signBytes, int signCalls, long signDecodedBytes)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string classicPath = Path.Combine(directory, "scale-classic.pdf");
            string sourcePath = Path.Combine(directory, "scale.pdf");
            string filledPath = Path.Combine(directory, "scale-filled.pdf");
            string flattenedPath = Path.Combine(directory, "scale-flattened.pdf");
            string signedPath = Path.Combine(directory, "scale-signed.pdf");
            G7Fixtures.WriteForm(classicPath, pages, "Scale");
            G7Fixtures.ObjectStreamCopy(classicPath, sourcePath);
            using (var file = OpenRead(sourcePath))
            using (var document = PdfDocument.Open(file, leaveOpen: true))
            {
                document.AcroForm.Fill(G7Fixtures.TextValues(pages));
                using var destination = CreateFile(filledPath);
                document.SaveIncremental(destination, leaveOpen: true);
            }

            long filledLength = new FileInfo(filledPath).Length;
            string size = pages.ToString("N0", CultureInfo.InvariantCulture) + "-page";
            _output.WriteLine(size + " form, filled " + filledLength.ToString(CultureInfo.InvariantCulture) + " bytes");

            ProbeStream flattenProbe;
            long flattenDecoded;
            using (var file = OpenRead(filledPath))
            {
                flattenProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(flattenProbe, leaveOpen: true);
                document.AcroForm.Flatten();
                using var destination = CreateFile(flattenedPath);
                document.SaveIncremental(destination, leaveOpen: true);
                flattenDecoded = document.DecodedByteCount;
            }

            ReportSourceIo(size + " flatten", flattenProbe, flattenDecoded);
            ReportOutput(size + " flatten", flattenedPath);

            ProbeStream signProbe;
            long signDecoded;
            using var rsa = RSA.Create(2048);
            byte[] certificate = SelfSigned(rsa);
            using (var file = OpenRead(filledPath))
            {
                signProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(signProbe, leaveOpen: true);
                using var destination = CreateFile(signedPath);
                document.Sign(destination, new PdfSignatureOptions { Reason = "W-108", SigningTime = TestCompat.UnixEpoch }, new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate)), leaveOpen: true);
                signDecoded = document.DecodedByteCount;
            }

            ReportSourceIo(size + " sign", signProbe, signDecoded);
            Assert.Equal(HashFile(filledPath, 0, filledLength), HashFile(signedPath, 0, filledLength));
            ReportSigned(size + " sign", VerifySignatureCoverage(signedPath, filledLength, verifyCms: false));
            AssertSourceIo(size + " flatten", flattenProbe, flattenDecoded, flattenBytes, flattenCalls, flattenDecodedBytes);
            AssertSourceIo(size + " sign", signProbe, signDecoded, signBytes, signCalls, signDecodedBytes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.External)]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void repeated_open_and_object_stream_reads_do_not_retain_memory()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string classicPath = Path.Combine(directory, "repeat-classic.pdf");
            string sourcePath = Path.Combine(directory, "repeat.pdf");
            G7Fixtures.WriteForm(classicPath, FormPages, "Five Hundred");
            G7Fixtures.ObjectStreamCopy(classicPath, sourcePath);

            // Each cycle decodes the object streams again; the document releases their clean objects when the
            // public operation returns, and Dispose releases the rest.
            var opened = new long[Cycles];
            var clock = Stopwatch.StartNew();
            for (int cycle = 0; cycle < Cycles; cycle++)
            {
                using (var file = OpenRead(sourcePath))
                using (var document = PdfDocument.Open(file, leaveOpen: true))
                {
                    Assert.Equal(FormPages * 2, document.AcroForm.Fields.Count);
                    Assert.Equal(FormPages, document.Pages.Count);
                }

                opened[cycle] = Retained();
            }

            clock.Stop();
            ReportDrift("repeated open, field and page read, and dispose", opened, clock.Elapsed);

            var reads = new long[Cycles];
            clock.Restart();
            using (var file = OpenRead(sourcePath))
            using (var document = PdfDocument.Open(file, leaveOpen: true))
            {
                for (int read = 0; read < Cycles; read++)
                {
                    IReadOnlyList<PdfField> fields = document.AcroForm.Fields;
                    Assert.Equal("Name " + FormPages.ToString(CultureInfo.InvariantCulture), fields[(FormPages * 2) - 2].Value);
                    reads[read] = Retained();
                }
            }

            clock.Stop();
            ReportDrift("repeated object-stream field reads on one document", reads, clock.Elapsed);
            AssertDrift("repeated open, field and page read, and dispose", opened);
            AssertDrift("repeated object-stream field reads on one document", reads);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.External)]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void encrypted_xref_stream_gibibyte_pdf_opens_without_a_discovery_scan()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string densePath = Path.Combine(directory, "dense.pdf");
            string encryptedPath = Path.Combine(directory, "dense-aes256.pdf");
            G7Fixtures.WriteDense(densePath);
            string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
            using (Process process = ExternalTool.Start(qpdf, "--object-streams=generate", "--encrypt", "user-secret", "owner-secret", "256", "--", densePath, encryptedPath))
            {
                ExternalTool.Output(process);
            }

            File.Delete(densePath);
            long length = new FileInfo(encryptedPath).Length;
            Assert.True(length >= OneGibibyte, "encrypted fixture is " + length.ToString(CultureInfo.InvariantCulture) + " bytes");

            // qpdf's own listing shows objects stored in object streams, and the tail is a cross-reference
            // stream whose dictionary names /Encrypt.
            using (Process process = ExternalTool.Start(qpdf, "--password=owner-secret", "--show-xref", encryptedPath))
            {
                Assert.Contains("compressed; stream", ExternalTool.Output(process), StringComparison.Ordinal);
            }

            int tailLength = 64 * 1024;
            var tail = new byte[tailLength];
            using (var file = OpenRead(encryptedPath))
            {
                file.Seek(length - tailLength, SeekOrigin.Begin);
                int total = 0;
                while (total < tailLength)
                {
                    int read = file.Read(tail, total, tailLength - total);
                    Assert.True(read > 0);
                    total += read;
                }
            }

            string tailText = TestCompat.Latin1.GetString(tail);
            Assert.Contains("/XRef", tailText, StringComparison.Ordinal);
            Assert.Contains("/Encrypt", tailText, StringComparison.Ordinal);
            _output.WriteLine("encrypted source " + length.ToString(CultureInfo.InvariantCulture) + " bytes, AES-256 with a cross-reference stream and object streams");

            foreach (string password in new[] { "user-secret", "owner-secret" })
            {
                ProbeStream? probe = null;
                long decoded = 0;
                WorkloadMeter.Sample inspect = WorkloadMeter.Measure(() =>
                {
                    using var file = OpenRead(encryptedPath);
                    probe = new ProbeStream(file);
                    using var document = PdfDocument.Open(probe, password, leaveOpen: true);
                    Assert.Equal(DensePages, document.PageCount);
                    Assert.Equal("Dense GiB", document.Metadata.Title);
                    Assert.True(document.Permissions.CanModify);
                    IReadOnlyList<PdfField> fields = document.AcroForm.Fields;
                    Assert.Equal(DensePages, fields.Count);
                    Assert.Equal("value " + DensePages.ToString(CultureInfo.InvariantCulture), fields[DensePages - 1].Value);
                    decoded = document.DecodedByteCount;
                });
                string workload = "encrypted xref-stream inspect, " + password;
                Report(workload, inspect, SimpleBudget, probe!, decoded);
                Assert.True(probe!.BytesRead < SmallReadLimit, workload + " read " + probe.BytesRead.ToString(CultureInfo.InvariantCulture) + " bytes");
                AssertBudget(workload, inspect, SimpleBudget);
                AssertSourceIo(workload, probe, decoded, 160000, 17, 105000);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertBudget(string workload, WorkloadMeter.Sample sample, long budget)
    {
        Assert.True(sample.PeakHeapGrowth <= budget, sample.Describe(workload, budget));
    }

    // D-49 tripwires. The thresholds are the W-108 measurements plus about 10% (memory-budgets.md); a change that
    // raises one updates its threshold in the same commit, with the reason.
    private static void AssertSourceIo(string workload, ProbeStream probe, long decoded, long maxBytes, int maxCalls, long maxDecoded)
    {
        Assert.True(probe.BytesRead <= maxBytes, workload + " read " + probe.BytesRead.ToString(CultureInfo.InvariantCulture) + " source bytes, above " + maxBytes.ToString(CultureInfo.InvariantCulture));
        Assert.True(probe.ReadCalls <= maxCalls, workload + " made " + probe.ReadCalls.ToString(CultureInfo.InvariantCulture) + " read calls, above " + maxCalls.ToString(CultureInfo.InvariantCulture));
        Assert.True(decoded <= maxDecoded, workload + " decoded " + decoded.ToString(CultureInfo.InvariantCulture) + " bytes, above " + maxDecoded.ToString(CultureInfo.InvariantCulture));
    }

    // The first cycle loads code and static state; growth is measured from the heap retained after it.
    private static void AssertDrift(string workload, long[] retained)
    {
        long drift = Drift(retained);
        Assert.True(drift <= RetainedDrift, workload + ": retained heap grew " + drift.ToString(CultureInfo.InvariantCulture) + " bytes after the warm-up cycle");
    }

    private static long Drift(long[] retained)
    {
        long highest = retained[1];
        for (int i = 2; i < retained.Length; i++)
        {
            highest = Math.Max(highest, retained[i]);
        }

        return highest - retained[0];
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
        var request = new CertificateRequest("CN=G7 Workload", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return certificate.RawData;
    }

    // Returns the SHA-256 of the signed ranges: every byte of the file except the /Contents hex string.
    private static byte[] VerifySignatureCoverage(string path, long revisionLength, bool verifyCms)
    {
        long length = new FileInfo(path).Length;
        var appendix = new byte[length - revisionLength];
        using (var file = OpenRead(path))
        {
            file.Seek(revisionLength, SeekOrigin.Begin);
            int total = 0;
            while (total < appendix.Length)
            {
                int read = file.Read(appendix, total, appendix.Length - total);
                Assert.True(read > 0);
                total += read;
            }
        }

        string text = Encoding.ASCII.GetString(appendix);
        Match range = Regex.Match(text, @"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]");
        Assert.True(range.Success);
        long offset0 = long.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture);
        long length0 = long.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
        long offset1 = long.Parse(range.Groups[3].Value, CultureInfo.InvariantCulture);
        long length1 = long.Parse(range.Groups[4].Value, CultureInfo.InvariantCulture);
        Assert.Equal(0, offset0);
        Assert.True(length0 > revisionLength);
        Assert.Equal(length, offset1 + length1);
        Match contents = Regex.Match(text, @"/Contents\s*<([0-9A-Fa-f]+)>");
        Assert.True(contents.Success);
        Assert.Equal(offset1 - length0, contents.Groups[1].Value.Length + 2L);
        byte[] digest;
        using (var file = OpenRead(path))
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            AppendRange(hash, file, offset0, length0);
            AppendRange(hash, file, offset1, length1);
            digest = hash.GetHashAndReset();
        }

        string cmsHex = contents.Groups[1].Value.ToUpperInvariant();
        Assert.Contains(TestCompat.ToHexString(digest), cmsHex, StringComparison.Ordinal);
#if NET8_0_OR_GREATER
        if (verifyCms)
        {
            byte[] signed = File.ReadAllBytes(path);
            var content = new byte[length0 + length1];
            Buffer.BlockCopy(signed, (int)offset0, content, 0, (int)length0);
            Buffer.BlockCopy(signed, (int)offset1, content, (int)length0, (int)length1);
            byte[] padded = TestCompat.FromHexString(contents.Groups[1].Value);
            var cms = new SignedCms(new ContentInfo(content), detached: true);
            cms.Decode(DerCodec.ReadTlv(padded).Full);
            cms.CheckSignature(verifySignatureOnly: true);
        }
#else
        _ = verifyCms;
#endif
        return digest;
    }

    private static byte[] HashFile(string path, long offset, long count)
    {
        using var file = OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendRange(hash, file, offset, count);
        return hash.GetHashAndReset();
    }

    private static void AppendRange(IncrementalHash hash, Stream stream, long offset, long count)
    {
        var buffer = new byte[1024 * 1024];
        stream.Seek(offset, SeekOrigin.Begin);
        long remaining = count;
        while (remaining > 0)
        {
            int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            Assert.True(read > 0, "unexpected end of file");
            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }
    }

    // The managed heap after a full blocking collection: what the process still holds.
    private static long Retained()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(true);
    }

    private void Report(string workload, WorkloadMeter.Sample sample, long budget, ProbeStream probe, long decoded)
    {
        _output.WriteLine(
            sample.Describe(workload, budget)
            + ", source bytes read " + probe.BytesRead.ToString(CultureInfo.InvariantCulture)
            + ", read calls " + probe.ReadCalls.ToString(CultureInfo.InvariantCulture)
            + ", decoded bytes " + decoded.ToString(CultureInfo.InvariantCulture)
            + ", largest read request " + probe.MaxRequested.ToString(CultureInfo.InvariantCulture));
    }

    private void ReportSourceIo(string workload, ProbeStream probe, long decoded)
    {
        _output.WriteLine(
            workload
            + ": source bytes read " + probe.BytesRead.ToString(CultureInfo.InvariantCulture)
            + ", read calls " + probe.ReadCalls.ToString(CultureInfo.InvariantCulture)
            + ", decoded bytes " + decoded.ToString(CultureInfo.InvariantCulture));
    }

    // The output hash lets a run be compared byte for byte with a run at another revision.
    private void ReportOutput(string workload, string path)
    {
        _output.WriteLine(workload + ": output SHA-256 " + TestCompat.ToHexString(HashFile(path, 0, new FileInfo(path).Length)));
    }

    // Each run signs with a fresh key, so a signed output is compared by its signed ranges.
    private void ReportSigned(string workload, byte[] signedRangeHash)
    {
        _output.WriteLine(workload + ": signed-range SHA-256 " + TestCompat.ToHexString(signedRangeHash));
    }

    private void ReportDrift(string workload, long[] retained, TimeSpan elapsed)
    {
        _output.WriteLine(
            workload
            + ": " + retained.Length.ToString(CultureInfo.InvariantCulture) + " cycles"
            + ", retained after warm-up " + (retained[0] / (1024.0 * 1024.0)).ToString("0.00", CultureInfo.InvariantCulture)
            + " MiB, last " + (retained[retained.Length - 1] / (1024.0 * 1024.0)).ToString("0.00", CultureInfo.InvariantCulture)
            + " MiB, largest growth " + (Drift(retained) / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)
            + " KiB, time " + elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s"
            + ", growth per cycle in KiB " + string.Join(" ", Array.ConvertAll(retained, bytes => ((bytes - retained[0]) / 1024).ToString(CultureInfo.InvariantCulture))));
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
