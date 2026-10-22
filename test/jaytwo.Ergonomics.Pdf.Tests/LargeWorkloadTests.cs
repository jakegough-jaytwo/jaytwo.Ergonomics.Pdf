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
    private const int DensePages = 64;
    private const int ImageWidth = 2048;
    private const int ImageHeight = 2731;
    private const int FormPages = 500;
    private const int FlattenPages = 1000;
    private const int Cycles = 20;

    // Reads that stay this small on a 1 GiB file cannot include a signed range or a discovery scan.
    private const long SmallReadLimit = 16L * 1024 * 1024;

    // Retained-heap drift allowed across repeated use, after the warm-up cycle.
    private const long RetainedDrift = 1024 * 1024;

    private static readonly string[] Customers = { "Acme Widgets Ltd", "Globex Corporation", "Initech", "Umbrella Supply Co", "Hooli Services", "Stark Industrial", "Wayne Logistics", "Tyrell Parts" };

    private readonly ITestOutputHelper _output;

    public LargeWorkloadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void dense_gibibyte_pdf_is_inspected_edited_and_signed_within_budget()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "dense.pdf");
            string editedPath = Path.Combine(directory, "dense-edited.pdf");
            string signedPath = Path.Combine(directory, "dense-signed.pdf");
            WriteDense(sourcePath);
            long sourceLength = new FileInfo(sourcePath).Length;
            Assert.True(sourceLength >= OneGibibyte, "dense fixture is " + sourceLength.ToString(CultureInfo.InvariantCulture) + " bytes");
            _output.WriteLine("dense source " + sourceLength.ToString(CultureInfo.InvariantCulture) + " bytes, " + DensePages.ToString(CultureInfo.InvariantCulture) + " pages of random 8-bit RGB images");

            ProbeStream? inspectProbe = null;
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
            });
            Report("dense inspect", inspect, SimpleBudget, inspectProbe!);

            ProbeStream? editProbe = null;
            WorkloadMeter.Sample edit = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(sourcePath);
                editProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(editProbe, leaveOpen: true);
                document.Metadata.Title = "Dense GiB edited";
                using var destination = CreateFile(editedPath);
                document.SaveIncremental(destination, leaveOpen: true);
            });
            Report("dense metadata edit", edit, SimpleBudget, editProbe!);

            long editedLength = new FileInfo(editedPath).Length;
            Assert.True(editedLength > sourceLength);
            Assert.Equal(HashFile(sourcePath, 0, sourceLength), HashFile(editedPath, 0, sourceLength));

            ProbeStream? signProbe = null;
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
            });
            Report("dense sign", sign, SimpleBudget, signProbe!);

            long signedLength = new FileInfo(signedPath).Length;
            _output.WriteLine("dense edited " + editedLength.ToString(CultureInfo.InvariantCulture) + " bytes, signed " + signedLength.ToString(CultureInfo.InvariantCulture) + " bytes");
            Assert.Equal(HashFile(editedPath, 0, editedLength), HashFile(signedPath, 0, editedLength));
            VerifySignatureCoverage(signedPath, editedLength, verifyCms: false);
            using (var file = OpenRead(signedPath))
            using (var document = PdfDocument.Open(file, leaveOpen: true))
            {
                Assert.Equal("Dense GiB edited", document.Metadata.Title);
                Assert.Contains(document.AcroForm.Fields, field => field.FieldType == PdfFieldType.Signature);
            }

            // Signature inspection and revision listing report the coverage from the xref sections, the
            // field tree, and the signature dictionary, so neither reads the signed ranges.
            ProbeStream? signaturesProbe = null;
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
            });
            Report("dense signature inspection", signatures, SimpleBudget, signaturesProbe!);

            ProbeStream? revisionsProbe = null;
            WorkloadMeter.Sample revisions = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(signedPath);
                revisionsProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(revisionsProbe, leaveOpen: true);
                IReadOnlyList<PdfRevision> listed = document.Revisions;
                Assert.Equal(3, listed.Count);
                Assert.Equal(new[] { sourceLength, editedLength, signedLength }, new[] { listed[0].EndOffset, listed[1].EndOffset, listed[2].EndOffset });
                Assert.Equal(new[] { false, false, true }, new[] { listed[0].IsCoveredBySignature, listed[1].IsCoveredBySignature, listed[2].IsCoveredBySignature });
            });
            Report("dense revision listing", revisions, SimpleBudget, revisionsProbe!);

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
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
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
                WriteRecords(payload, payloadLength, 0x5EED0250UL);
            }

            long carrierLength = new FileInfo(pdfPath).Length;
            byte[] carrierHash = HashFile(pdfPath, 0, carrierLength);
            _output.WriteLine("attachment payload " + payloadLength.ToString(CultureInfo.InvariantCulture) + " bytes, carrier " + carrierLength.ToString(CultureInfo.InvariantCulture) + " bytes");

            ProbeStream? addProbe = null;
            WorkloadMeter.Sample add = WorkloadMeter.Measure(() =>
            {
                using var pdf = new FileStream(pdfPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                using var content = OpenRead(payloadPath);
                addProbe = new ProbeStream(content);
                using var document = PdfDocument.Open(pdf, leaveOpen: true);
                document.Attachments.Add("payload.csv", addProbe, leaveOpen: true, new PdfAttachmentOptions { Description = "G7 payload", MimeType = "text/csv", Compress = true });
                document.SaveIncremental();
            });
            Report("compressed attachment add and save", add, SimpleBudget, addProbe!);

            // The payload compresses, so the save grows the carrier by less than half the payload.
            long savedLength = new FileInfo(pdfPath).Length;
            Assert.True(savedLength - carrierLength < payloadLength / 2, "the save added " + (savedLength - carrierLength).ToString(CultureInfo.InvariantCulture) + " bytes");
            _output.WriteLine("attachment carrier after save " + savedLength.ToString(CultureInfo.InvariantCulture) + " bytes");

            ProbeStream? extractProbe = null;
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
            });
            Report("compressed attachment reopen and extract", extract, SimpleBudget, extractProbe!);

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
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
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
            WriteForm(classicPath, FormPages, "Five Hundred");
            string qpdf = ObjectStreamCopy(classicPath, sourcePath);
            long sourceLength = new FileInfo(sourcePath).Length;
            _output.WriteLine("form classic " + new FileInfo(classicPath).Length.ToString(CultureInfo.InvariantCulture) + " bytes, qpdf object-stream source " + sourceLength.ToString(CultureInfo.InvariantCulture) + " bytes");

            ProbeStream? extractProbe = null;
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
            });
            Report("500-page form extraction", extract, TraversalBudget, extractProbe!);

            ProbeStream? editProbe = null;
            WorkloadMeter.Sample edit = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(sourcePath);
                editProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(editProbe, leaveOpen: true);
                document.Metadata.Title = "Five Hundred edited";
                document.Metadata.Subject = "G7";
                using var destination = CreateFile(editedPath);
                document.SaveIncremental(destination, leaveOpen: true);
            });
            Report("500-page metadata edit", edit, SimpleBudget, editProbe!);

            long editedLength = new FileInfo(editedPath).Length;
            Assert.Equal(HashFile(sourcePath, 0, sourceLength), HashFile(editedPath, 0, sourceLength));

            ProbeStream? fillProbe = null;
            WorkloadMeter.Sample fill = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(editedPath);
                fillProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(fillProbe, leaveOpen: true);
                IReadOnlyDictionary<string, PdfAppearanceDisposition> filled = document.AcroForm.Fill(TextValues(FormPages));
                Assert.Equal(FormPages, filled.Count);
                Assert.All(filled.Values, disposition => Assert.Equal(PdfAppearanceDisposition.Generated, disposition));
                using var destination = CreateFile(filledPath);
                document.SaveIncremental(destination, leaveOpen: true);
            });
            Report("500-page bulk fill of 500 fields", fill, SimpleBudget, fillProbe!);

            long filledLength = new FileInfo(filledPath).Length;
            Assert.Equal(HashFile(editedPath, 0, editedLength), HashFile(filledPath, 0, editedLength));

            ProbeStream? signProbe = null;
            using var rsa = RSA.Create(2048);
            byte[] certificate = SelfSigned(rsa);
            WorkloadMeter.Sample sign = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(filledPath);
                signProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(signProbe, leaveOpen: true);
                using var destination = CreateFile(signedPath);
                document.Sign(destination, new PdfSignatureOptions { Reason = "G7", SigningTime = TestCompat.UnixEpoch }, new CmsDetachedSignatureProvider(new RsaSigner(rsa, certificate)), leaveOpen: true);
            });
            Report("500-page sign", sign, SimpleBudget, signProbe!);

            long signedLength = new FileInfo(signedPath).Length;
            _output.WriteLine("form edited " + editedLength.ToString(CultureInfo.InvariantCulture) + " bytes, filled " + filledLength.ToString(CultureInfo.InvariantCulture) + " bytes, signed " + signedLength.ToString(CultureInfo.InvariantCulture) + " bytes");
            Assert.Equal(HashFile(filledPath, 0, filledLength), HashFile(signedPath, 0, filledLength));
            VerifySignatureCoverage(signedPath, filledLength, verifyCms: true);
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
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void thousand_page_form_is_flattened_within_budget()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string classicPath = Path.Combine(directory, "flatten-classic.pdf");
            string sourcePath = Path.Combine(directory, "flatten.pdf");
            string filledPath = Path.Combine(directory, "flatten-filled.pdf");
            string flattenedPath = Path.Combine(directory, "flatten-flattened.pdf");
            WriteForm(classicPath, FlattenPages, "One Thousand");
            string qpdf = ObjectStreamCopy(classicPath, sourcePath);

            // Flatten refuses a field that shows a value without an appearance, so the text fields are filled
            // first, outside the measured window.
            using (var file = OpenRead(sourcePath))
            using (var document = PdfDocument.Open(file, leaveOpen: true))
            {
                document.AcroForm.Fill(TextValues(FlattenPages));
                using var destination = CreateFile(filledPath);
                document.SaveIncremental(destination, leaveOpen: true);
            }

            long filledLength = new FileInfo(filledPath).Length;
            _output.WriteLine("flatten source " + new FileInfo(sourcePath).Length.ToString(CultureInfo.InvariantCulture) + " bytes, filled " + filledLength.ToString(CultureInfo.InvariantCulture) + " bytes, " + (FlattenPages * 2).ToString(CultureInfo.InvariantCulture) + " fields");

            ProbeStream? flattenProbe = null;
            WorkloadMeter.Sample flatten = WorkloadMeter.Measure(() =>
            {
                using var file = OpenRead(filledPath);
                flattenProbe = new ProbeStream(file);
                using var document = PdfDocument.Open(flattenProbe, leaveOpen: true);
                document.AcroForm.Flatten();
                using var destination = CreateFile(flattenedPath);
                document.SaveIncremental(destination, leaveOpen: true);
            });
            Report("1,000-page flatten", flatten, TraversalBudget, flattenProbe!);

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
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void repeated_open_and_object_stream_reads_do_not_retain_memory()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string classicPath = Path.Combine(directory, "repeat-classic.pdf");
            string sourcePath = Path.Combine(directory, "repeat.pdf");
            WriteForm(classicPath, FormPages, "Five Hundred");
            ObjectStreamCopy(classicPath, sourcePath);

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
    public void encrypted_xref_stream_gibibyte_pdf_opens_without_a_discovery_scan()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string densePath = Path.Combine(directory, "dense.pdf");
            string encryptedPath = Path.Combine(directory, "dense-aes256.pdf");
            WriteDense(densePath);
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
                });
                string workload = "encrypted xref-stream inspect, " + password;
                Report(workload, inspect, SimpleBudget, probe!);
                Assert.True(probe!.BytesRead < SmallReadLimit, workload + " read " + probe.BytesRead.ToString(CultureInfo.InvariantCulture) + " bytes");
                AssertBudget(workload, inspect, SimpleBudget);
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

    private static void VerifySignatureCoverage(string path, long revisionLength, bool verifyCms)
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

    // CSV invoice rows whose fields come from SplitMix64 with a fixed seed: every run writes the same bytes,
    // and the repeated layout with random digits gives Flate real work at a realistic ratio.
    private static void WriteRecords(Stream stream, long count, ulong seed)
    {
        var buffer = new byte[1024 * 1024];
        int used = 0;
        ulong state = seed;
        long row = 0;
        long remaining = count;
        while (remaining > 0)
        {
            ulong a = NextRandom(ref state);
            ulong b = NextRandom(ref state);
            row++;
            string line = row.ToString("D10", CultureInfo.InvariantCulture)
                + ",INV-" + ((uint)a).ToString("X8", CultureInfo.InvariantCulture)
                + ",2026-" + (1 + ((a >> 32) % 12)).ToString("D2", CultureInfo.InvariantCulture)
                + "-" + (1 + ((a >> 40) % 28)).ToString("D2", CultureInfo.InvariantCulture)
                + "," + ((b % 10000000) / 100).ToString(CultureInfo.InvariantCulture)
                + "." + (b % 100).ToString("D2", CultureInfo.InvariantCulture)
                + "," + Customers[(int)(b >> 56) % Customers.Length] + "\n";
            int length = (int)Math.Min(line.Length, remaining);
            if (used + length > buffer.Length)
            {
                stream.Write(buffer, 0, used);
                used = 0;
            }

            used += Encoding.ASCII.GetBytes(line, 0, length, buffer, used);
            remaining -= length;
        }

        stream.Write(buffer, 0, used);
    }

    private static ulong NextRandom(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    // SplitMix64 with a fixed seed, so every run writes the same incompressible bytes.
    private static ulong FillRandom(byte[] buffer, int count, ulong state)
    {
        unchecked
        {
            for (int i = 0; i < count; i += 8)
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                int bytes = Math.Min(8, count - i);
                for (int b = 0; b < bytes; b++)
                {
                    buffer[i + b] = (byte)(z >> (8 * b));
                }
            }
        }

        return state;
    }

    // 64 pages, each drawing one unfiltered 2048 x 2731 RGB image of random bytes (16,779,264 bytes),
    // with one text field per page. Classic xref. About 1.0001 GiB.
    private static void WriteDense(string path)
    {
        int size = 10 + (DensePages * 4);
        using var writer = new FixtureWriter(path, size);
        var fields = new StringBuilder();
        var kids = new StringBuilder();
        for (int i = 0; i < DensePages; i++)
        {
            fields.Append(Id(13 + (i * 4))).Append(' ');
            kids.Append(Id(10 + (i * 4))).Append(' ');
        }

        writer.Object(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [" + fields + "] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> >>");
        writer.Object(2, "<< /Type /Pages /Count " + DensePages.ToString(CultureInfo.InvariantCulture) + " /Kids [" + kids + "] >>");
        writer.Object(3, "<< /Title (Dense GiB) /Author (QA) /Producer (LargeWorkloadTests) >>");
        writer.Object(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        ulong state = 0x5EED1610UL;
        var buffer = new byte[1024 * 1024];
        long imageLength = (long)ImageWidth * ImageHeight * 3;
        for (int i = 0; i < DensePages; i++)
        {
            int page = 10 + (i * 4);
            string number = (i + 1).ToString(CultureInfo.InvariantCulture);
            writer.Object(page, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im0 " + Id(page + 2) + " >> /Font << /Helv 4 0 R >> >> /Contents " + Id(page + 1) + " /Annots [" + Id(page + 3) + "] >>");
            writer.Stream(page + 1, string.Empty, Encoding.ASCII.GetBytes("q 540 0 0 700 36 36 cm /Im0 Do Q\n"));
            writer.BeginStream(page + 2, "/Type /XObject /Subtype /Image /Width " + ImageWidth.ToString(CultureInfo.InvariantCulture) + " /Height " + ImageHeight.ToString(CultureInfo.InvariantCulture) + " /ColorSpace /DeviceRGB /BitsPerComponent 8", imageLength);
            long remaining = imageLength;
            while (remaining > 0)
            {
                int chunk = (int)Math.Min(buffer.Length, remaining);
                state = FillRandom(buffer, chunk, state);
                writer.Raw(buffer, chunk);
                remaining -= chunk;
            }

            writer.EndStream();
            writer.Object(page + 3, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (field" + number + ") /V (value " + number + ") /Rect [36 750 300 774] /P " + Id(page) + " /DA (/Helv 10 Tf 0 g) /F 4 >>");
        }

        writer.Finish("/Root 1 0 R /Info 3 0 R");
    }

    // Pages under ten intermediate page-tree nodes. Each page has a Flate content stream of text and a
    // parent field with a text kid and a check-box kid (shared on and off appearances). Classic xref; the
    // test then has qpdf rewrite it with object streams and a cross-reference stream.
    private static void WriteForm(string path, int pages, string title)
    {
        const int nodes = 10;
        int perNode = pages / nodes;
        int size = 100 + (pages * 5);
        using var writer = new FixtureWriter(path, size);
        var fields = new StringBuilder();
        var nodeKids = new StringBuilder();
        for (int i = 0; i < pages; i++)
        {
            fields.Append(Id(100 + (i * 5) + 2)).Append(' ');
        }

        for (int n = 0; n < nodes; n++)
        {
            nodeKids.Append(Id(10 + n)).Append(' ');
        }

        writer.Object(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [" + fields + "] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> >>");
        writer.Object(2, "<< /Type /Pages /Count " + pages.ToString(CultureInfo.InvariantCulture) + " /Kids [" + nodeKids + "] >>");
        writer.Object(3, "<< /Title (" + title + ") /Author (QA) /Producer (LargeWorkloadTests) >>");
        writer.Object(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        writer.Stream(5, "/Type /XObject /Subtype /Form /BBox [0 0 14 14]", Encoding.ASCII.GetBytes("q 0 g 3 3 8 8 re f Q\n"));
        writer.Stream(6, "/Type /XObject /Subtype /Form /BBox [0 0 14 14]", Encoding.ASCII.GetBytes("q 0 G 0.5 0.5 13 13 re S Q\n"));
        for (int n = 0; n < nodes; n++)
        {
            var kids = new StringBuilder();
            for (int k = 0; k < perNode; k++)
            {
                kids.Append(Id(100 + (((n * perNode) + k) * 5))).Append(' ');
            }

            writer.Object(10 + n, "<< /Type /Pages /Parent 2 0 R /Count " + perNode.ToString(CultureInfo.InvariantCulture) + " /Kids [" + kids + "] >>");
        }

        for (int i = 0; i < pages; i++)
        {
            int page = 100 + (i * 5);
            string number = (i + 1).ToString(CultureInfo.InvariantCulture);
            string state = (i + 1) % 2 == 0 ? "/Yes" : "/Off";
            writer.Object(page, "<< /Type /Page /Parent " + Id(10 + (i / perNode)) + " /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents " + Id(page + 1) + " /Annots [" + Id(page + 3) + " " + Id(page + 4) + "] >>");
            var content = new StringBuilder();
            for (int line = 0; line < 45; line++)
            {
                content.Append("BT /F1 10 Tf 72 ").Append((660 - (line * 14)).ToString(CultureInfo.InvariantCulture)).Append(" Td (Page ").Append(number).Append(" line ").Append(line.ToString(CultureInfo.InvariantCulture)).Append(": the quick brown fox jumps over the lazy dog 0123456789) Tj ET\n");
            }

            writer.Stream(page + 1, "/Filter /FlateDecode", TestCompat.Zlib(Encoding.ASCII.GetBytes(content.ToString())));
            writer.Object(page + 2, "<< /T (page" + number + ") /Kids [" + Id(page + 3) + " " + Id(page + 4) + "] >>");
            writer.Object(page + 3, "<< /Type /Annot /Subtype /Widget /Parent " + Id(page + 2) + " /FT /Tx /T (name) /V (Name " + number + ") /Rect [72 720 300 744] /P " + Id(page) + " /DA (/Helv 10 Tf 0 g) /F 4 >>");
            writer.Object(page + 4, "<< /Type /Annot /Subtype /Widget /Parent " + Id(page + 2) + " /FT /Btn /T (agree) /V " + state + " /AS " + state + " /AP << /N << /Yes 5 0 R /Off 6 0 R >> >> /Rect [72 690 86 704] /P " + Id(page) + " /F 4 >>");
        }

        writer.Finish("/Root 1 0 R /Info 3 0 R");
    }

    // qpdf rewrites the classic fixture with object streams and a cross-reference stream.
    private static string ObjectStreamCopy(string classicPath, string path)
    {
        string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
        using (Process process = ExternalTool.Start(qpdf, "--object-streams=generate", classicPath, path))
        {
            ExternalTool.Output(process);
        }

        string layout = TestCompat.Latin1.GetString(File.ReadAllBytes(path));
        Assert.Contains("/ObjStm", layout, StringComparison.Ordinal);
        Assert.Contains("/XRef", layout, StringComparison.Ordinal);
        return qpdf;
    }

    // The text field of every page of a WriteForm fixture.
    private static Dictionary<string, PdfFieldValue> TextValues(int pages)
    {
        var values = new Dictionary<string, PdfFieldValue>();
        for (int page = 1; page <= pages; page++)
        {
            string number = page.ToString(CultureInfo.InvariantCulture);
            values["page" + number + ".name"] = "Filled " + number;
        }

        return values;
    }

    // The managed heap after a full blocking collection: what the process still holds.
    private static long Retained()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(true);
    }

    private static string Id(int id)
    {
        return id.ToString(CultureInfo.InvariantCulture) + " 0 R";
    }

    private void Report(string workload, WorkloadMeter.Sample sample, long budget, ProbeStream probe)
    {
        _output.WriteLine(
            sample.Describe(workload, budget)
            + ", source bytes read " + probe.BytesRead.ToString(CultureInfo.InvariantCulture)
            + ", largest read request " + probe.MaxRequested.ToString(CultureInfo.InvariantCulture));
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

    private sealed class FixtureWriter : IDisposable
    {
        private readonly FileStream _stream;
        private readonly long[] _offsets;

        public FixtureWriter(string path, int size)
        {
            _stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024);
            _offsets = new long[size];
            Ascii("%PDF-1.7\n");
            byte[] binary = { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' };
            _stream.Write(binary, 0, binary.Length);
        }

        public void Object(int id, string body)
        {
            _offsets[id] = _stream.Position;
            Ascii(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n" + body + "\nendobj\n");
        }

        public void Stream(int id, string dictionary, byte[] data)
        {
            BeginStream(id, dictionary, data.Length);
            Raw(data, data.Length);
            EndStream();
        }

        public void BeginStream(int id, string dictionary, long length)
        {
            _offsets[id] = _stream.Position;
            Ascii(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n<< " + dictionary + " /Length " + length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
        }

        public void Raw(byte[] data, int count)
        {
            _stream.Write(data, 0, count);
        }

        public void EndStream()
        {
            Ascii("\nendstream\nendobj\n");
        }

        public void Finish(string trailer)
        {
            long xref = _stream.Position;
            var table = new StringBuilder();
            table.Append("xref\n0 ").Append(_offsets.Length.ToString(CultureInfo.InvariantCulture)).Append('\n');
            table.Append("0000000000 65535 f\r\n");
            for (int id = 1; id < _offsets.Length; id++)
            {
                if (_offsets[id] == 0)
                {
                    table.Append("0000000000 65535 f\r\n");
                }
                else
                {
                    table.Append(_offsets[id].ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n\r\n");
                }
            }

            table.Append("trailer\n<< /Size ").Append(_offsets.Length.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(trailer).Append(" >>\nstartxref\n");
            table.Append(xref.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
            Ascii(table.ToString());
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private void Ascii(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            _stream.Write(bytes, 0, bytes.Length);
        }
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
