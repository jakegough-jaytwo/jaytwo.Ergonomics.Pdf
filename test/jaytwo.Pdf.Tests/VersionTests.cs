using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Spec 7.5. Poppler pdfinfo is the independent reader: it reports the later of the header and catalog /Version.
public class VersionTests
{
    private const string TextField = "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 300 724] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>";
    private const string PasswordField = "<< /Subtype /Widget /FT /Tx /T (Secret) /Rect [72 600 300 620] /Ff 8192 /P 3 0 R >>";

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("1.4")]
    [InlineData("1.5")]
    [InlineData("1.6")]
    public void attachment_add_raises_an_earlier_file_to_1_7(string header)
    {
        byte[] pdf = PdfBuilder.Basic(version: header).Finish();
        byte[] output = Save(pdf, document => document.Attachments.Add("a.txt", new MemoryStream(Encoding.ASCII.GetBytes("alpha"))));

        Assert.Equal("%PDF-" + header, Encoding.ASCII.GetString(output, 0, 8));
        Assert.Equal("1.7", AppendedVersion(output, pdf.Length));
        Assert.Equal("1.7", PdfInfoVersion(output));
        using var reopened = PdfDocument.Open(new MemoryStream(output, writable: false));
        Assert.Equal(new[] { "a.txt" }, reopened.Attachments.Select(attachment => attachment.Name).ToArray());
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("1.7", "", null)]
    [InlineData("1.4", " /Version /1.7", "1.7")]
    [InlineData("1.7", " /Version /2.0", "2.0")]
    public void attachment_add_leaves_an_effective_1_7_or_later_alone(string header, string catalogExtra, string? kept)
    {
        byte[] pdf = PdfBuilder.Basic(catalogExtra, version: header).Finish();
        byte[] output = Save(pdf, document => document.Attachments.Add("a.txt", new MemoryStream(Encoding.ASCII.GetBytes("alpha"))));

        Assert.Equal(kept, AppendedVersion(output, pdf.Length));
        Assert.Equal(kept ?? header, PdfInfoVersion(output));
    }

    // Plan A2: a PDF/A-3 input keeps effective version 1.7 and /EmbeddedFiles still lists its file. No PDF/A claim is made.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    [Trait(Lane.Trait, Lane.Corpus)]
    public void pdfa3_input_stays_at_1_7_and_keeps_its_embedded_file()
    {
        CorpusEntry entry = CorpusManifest.Get("libreoffice-hybrid-pdfa3");
        byte[] pdf = File.ReadAllBytes(entry.FullPath);
        string[] before;
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(pdf, writable: false), entry.Password))
        {
            before = document.Attachments.Select(attachment => attachment.Name).ToArray();
        }

        Assert.NotEmpty(before);
        byte[] output = Save(pdf, document => document.Attachments.Add("invoice.xml", new MemoryStream(Encoding.ASCII.GetBytes("<invoice/>"))));

        Assert.Null(AppendedVersion(output, pdf.Length));
        Assert.Equal("1.7", PdfInfoVersion(output));
        string listed = WithFile(output, path =>
        {
            using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), "--list-attachments", path);
            return ExternalTool.Output(process);
        });
        foreach (string name in before.Append("invoice.xml"))
        {
            Assert.Contains(name + " ->", listed);
        }
    }

    // W-75: a relationship from the PDF/A-3 subset, the /Params dates and checksum, compression, and /PageMode
    // /UseAttachments (PDF 1.6) keep the PDF/A-3 input at 1.7. pypdf 6.21.1 reads the relationship and the page mode.
    // No PDF/A conformance is claimed (spec W9).
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    [Trait(Lane.Trait, Lane.Corpus)]
    public void pdfa3_input_with_relationship_dates_compression_and_page_mode_stays_at_1_7()
    {
        CorpusEntry entry = CorpusManifest.Get("libreoffice-hybrid-pdfa3");
        byte[] pdf = File.ReadAllBytes(entry.FullPath);
        string[] before;
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(pdf, writable: false), entry.Password))
        {
            before = document.Attachments.Select(attachment => attachment.Name).ToArray();
        }

        byte[] output = Save(pdf, document => document.Attachments.Add("factur-x.xml", new MemoryStream(Encoding.ASCII.GetBytes("<rsm:CrossIndustryInvoice/>")), options: new PdfAttachmentOptions
        {
            MimeType = "text/xml",
            Relationship = PdfAttachmentRelationship.Alternative,
            CreationDate = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            ModificationDate = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            Compress = true,
            OpenAttachmentsPanel = true,
        }));

        Assert.Null(AppendedVersion(output, pdf.Length));
        Assert.Equal("1.7", PdfInfoVersion(output));
        WithFile(output, path =>
        {
            using (Process process = ExternalTool.Start(CorpusManifest.Qpdf(), "--list-attachments", path))
            {
                string listed = ExternalTool.Output(process);
                foreach (string name in before.Append("factur-x.xml"))
                {
                    Assert.Contains(name + " ->", listed);
                }
            }

            System.Text.Json.JsonElement report = AttachmentTests.InspectAttachments(path, entry.Password);
            Assert.Equal("/UseAttachments", report.GetProperty("pageMode").GetString());
            System.Text.Json.JsonElement added = report.GetProperty("attachments").EnumerateArray().Single(item => item.GetProperty("name").GetString() == "factur-x.xml");
            Assert.Equal("/Alternative", added.GetProperty("relationship").GetString());
            Assert.Equal("2026-10-21T09:00:00Z", added.GetProperty("modDate").GetString());
            return 0;
        });
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("1.3", "1.4")]
    [InlineData("1.4", null)]
    public void xmp_write_raises_a_file_before_1_4(string header, string? raised)
    {
        byte[] pdf = PdfBuilder.Basic(version: header).Finish();
        byte[] packet = Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>");
        byte[] output = Save(pdf, document => document.Metadata.SetXmp(packet));

        Assert.Equal(raised, AppendedVersion(output, pdf.Length));
        Assert.Equal(raised ?? header, PdfInfoVersion(output));
    }

    // Title is PDF 1.1 and its XMP sync adds /Metadata, so a 1.0 file declares 1.4.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void info_edit_raises_a_1_0_file_to_1_4()
    {
        byte[] pdf = PdfBuilder.Basic(version: "1.0").Finish();
        byte[] output = Save(pdf, document => document.Metadata.Title = "Raised");

        Assert.Equal("1.4", AppendedVersion(output, pdf.Length));
        Assert.Equal("1.4", PdfInfoVersion(output));
    }

    // A fill needs only 1.2, but /Version is itself a PDF 1.4 key, so the raise declares 1.4.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("1.1", "1.4")]
    [InlineData("1.2", null)]
    public void fill_raises_a_file_before_1_2_to_1_4(string header, string? raised)
    {
        byte[] pdf = FormDocument(header);
        byte[] output = Save(pdf, document => Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada")));

        Assert.Equal(raised, AppendedVersion(output, pdf.Length));
        Assert.Equal(raised ?? header, PdfInfoVersion(output));
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void signing_a_1_2_file_raises_it_inside_the_signed_range()
    {
        byte[] pdf = FormDocument("1.2");
        var destination = new MemoryStream();
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(pdf, writable: false)))
        {
            document.Sign(destination, new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider(), leaveOpen: true);
        }

        byte[] output = destination.ToArray();
        Assert.True(output.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        Assert.Equal("1.4", AppendedVersion(output, pdf.Length));
        Assert.Equal("1.4", PdfInfoVersion(output));
        string text = Latin1(output);
        Match range = Regex.Match(text.Substring(pdf.Length), "/ByteRange \\[(\\d+) (\\d+) (\\d+) (\\d+)\\]");
        Assert.True(range.Success);
        long gapStart = long.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
        long gapEnd = long.Parse(range.Groups[3].Value, CultureInfo.InvariantCulture);
        Assert.Equal(output.Length, gapEnd + long.Parse(range.Groups[4].Value, CultureInfo.InvariantCulture));
        int version = text.IndexOf("/Version /1.4", pdf.Length, StringComparison.Ordinal);
        Assert.True(version < gapStart || version >= gapEnd, "The raised /Version is outside the signed range.");
    }

    // Plan C4: encrypted input under the owner and the user password keeps the prefix, /Encrypt, and /ID.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("owner")]
    [InlineData("user")]
    public void encrypted_input_is_raised_and_keeps_its_encryption(string password)
    {
        var crypt = new StandardEncryptor(StandardEncryptor.Kind.Revision3, "user", "owner", -4, encryptMetadata: true, 128);
        byte[] pdf = EncryptedDocument(crypt);
        byte[] content = Encoding.ASCII.GetBytes("secret attachment");
        byte[] output = Save(pdf, document => document.Attachments.Add("a.txt", new MemoryStream(content)), password);

        Assert.Equal("1.7", AppendedVersion(output, pdf.Length));
        Assert.Equal("1.7", PdfInfoVersion(output, password == "owner" ? "-opw" : "-upw", password));
        string appendix = Latin1(output).Substring(pdf.Length);
        Assert.Contains("/Encrypt 20 0 R", appendix);
        Assert.Contains(ToHex(crypt.Id), appendix, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret attachment", appendix);
        using var reopened = PdfDocument.Open(new MemoryStream(output, writable: false), password);
        using Stream opened = reopened.Attachments["a.txt"].Open();
        var read = new MemoryStream();
        opened.CopyTo(read);
        Assert.Equal(content, read.ToArray());
    }

    [Fact]
    public void restricted_user_password_is_refused_before_a_raise()
    {
        var crypt = new StandardEncryptor(StandardEncryptor.Kind.Revision3, "user", "owner", StandardEncryptor.FillOnly(), encryptMetadata: true, 128);
        byte[] pdf = EncryptedDocument(crypt);
        byte[] output = Save(pdf, document => Assert.Throws<PdfEncryptionRestrictedException>(() => document.Attachments.Add("a.txt", new MemoryStream(new byte[] { 1 }))), "user");

        Assert.Equal(pdf, output);
    }

    [Fact]
    public void refused_fill_leaves_no_raise()
    {
        byte[] pdf = FormDocument("1.1");
        byte[] output = Save(pdf, document => Assert.Throws<PdfFieldValueException>(() => document.AcroForm.Fill("Secret", "hidden")));

        Assert.Equal(pdf, output);
    }

    [Fact]
    public void failed_signature_leaves_no_raise()
    {
        byte[] pdf = FormDocument("1.2");
        var destination = new MemoryStream();
        var saved = new MemoryStream();
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(pdf, writable: false)))
        {
            Assert.Throws<PdfSignatureTooLargeException>(() => document.Sign(destination, new PdfSignatureOptions { ReservedContentsBytes = 8 }, new FixedProvider { Payload = new byte[32] }, leaveOpen: true));
            document.SaveIncremental(saved, leaveOpen: true);
        }

        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, saved.ToArray());
    }

    private static byte[] FormDocument(string header)
    {
        PdfBuilder builder = PdfBuilder.Basic(" /AcroForm << /Fields [4 0 R 5 0 R] >>", " /Annots [4 0 R 5 0 R]", header);
        builder.Indirect(4, TextField);
        builder.Indirect(5, PasswordField);
        return builder.Finish();
    }

    private static byte[] EncryptedDocument(StandardEncryptor crypt)
    {
        PdfBuilder builder = PdfBuilder.Basic(version: "1.4");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    // Opens a copy, applies the edit, and saves incrementally to a new stream; the original bytes must stay a prefix.
    private static byte[] Save(byte[] pdf, Action<PdfDocument> edit, string? password = null)
    {
        var destination = new MemoryStream();
        using (PdfDocument document = PdfDocument.Open(new MemoryStream(pdf, writable: false), password))
        {
            edit(document);
            document.SaveIncremental(destination, leaveOpen: true);
        }

        byte[] output = destination.ToArray();
        Assert.True(output.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        return output;
    }

    // The /Version the appended revisions write, or null when they write none.
    private static string? AppendedVersion(byte[] output, int prefix)
    {
        MatchCollection matches = Regex.Matches(Latin1(output).Substring(prefix), "/Version /([0-9.]+)");
        return matches.Count == 0 ? null : matches[matches.Count - 1].Groups[1].Value;
    }

    private static string PdfInfoVersion(byte[] pdf, params string[] options)
    {
        string pdfinfo = LocatePdfInfo();
        string output = WithFile(pdf, path =>
        {
            var info = new ProcessStartInfo
            {
                FileName = pdfinfo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            info.Environment["PATH"] = Path.GetDirectoryName(pdfinfo) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            foreach (string option in options.Append(path))
            {
                TestCompat.AddArgument(info, option);
            }

            using Process process = ExternalTool.Start(info);
            return ExternalTool.Output(process);
        });
        Match version = Regex.Match(output, "^PDF version:\\s*(\\S+)", RegexOptions.Multiline);
        Assert.True(version.Success, output);
        return version.Groups[1].Value;
    }

    private static string LocatePdfInfo()
    {
        string? directory = Environment.GetEnvironmentVariable("JAYTWO_POPPLER");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            string candidate = Path.Combine(directory, ExternalTool.Executable("pdfinfo"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return ExternalTool.Locate("JAYTWO_PDFINFO", ExternalTool.Executable("pdfinfo"));
    }

    private static T WithFile<T>(byte[] pdf, Func<string, T> run)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "output.pdf");
            File.WriteAllBytes(path, pdf);
            return run(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Latin1(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length);
        foreach (byte b in bytes)
        {
            builder.Append((char)b);
        }

        return builder.ToString();
    }

    private static string ToHex(byte[] bytes)
    {
        return string.Concat(bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public byte[] Payload { get; set; } = new byte[] { 1, 2, 3, 4 };

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(Payload);
        }
    }
}
