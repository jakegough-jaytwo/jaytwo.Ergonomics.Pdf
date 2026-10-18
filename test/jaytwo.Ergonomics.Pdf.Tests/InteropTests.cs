using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class InteropTests
{
    private const string PythonExe = @"C:\Python313\python.exe";

    private const string XmpPacket =
        "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>"
        + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
        + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
        + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">XmpEdited</rdf:li></rdf:Alt></dc:title>"
        + "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    private const string ReaderScript =
        "import sys\n"
        + "from pypdf import PdfReader\n"
        + "reader = PdfReader(sys.argv[1])\n"
        + "info = reader.metadata\n"
        + "title = '' if info is None or info.title is None else str(info.title)\n"
        + "xmp_info = reader.xmp_metadata\n"
        + "titles = None if xmp_info is None else xmp_info.dc_title\n"
        + "xmp = '' if not titles else str(titles.get('x-default', ''))\n"
        + "data = reader.attachments['notes.bin'][0]\n"
        + "print('TITLE=' + title)\n"
        + "print('XMP=' + xmp)\n"
        + "print('BYTES=' + data.hex())\n";

    private readonly ITestOutputHelper _output;

    public InteropTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void qpdf_checks_built_incremental_filled_and_signed_pdfs()
    {
        Assert.True(TryLocateQpdf(out string skip), skip);

        string builtPath = TempPdf();
        string incrementalPath = TempPdf();
        string filledPath = TempPdf();
        string signedPath = TempPdf();
        try
        {
            byte[] built = BasicPdf();
            File.WriteAllBytes(builtPath, built);
            Check(builtPath);

            byte[] incremental = SaveWithTitle(built, "Checked");
            Assert.True(incremental.Length > built.Length);
            Assert.True(
                incremental.AsSpan(0, built.Length).SequenceEqual(built),
                "Incremental save did not keep the original bytes as a prefix.");
            File.WriteAllBytes(incrementalPath, incremental);
            Check(incrementalPath);

            byte[] filled = FillName("Ada");
            File.WriteAllBytes(filledPath, filled);
            Check(filledPath);

            byte[] signed = SignBasic();
            File.WriteAllBytes(signedPath, signed);
            Check(signedPath);
        }
        finally
        {
            Delete(builtPath);
            Delete(incrementalPath);
            Delete(filledPath);
            Delete(signedPath);
        }
    }

    [Fact]
    public void pypdf_reads_an_info_edit_an_xmp_edit_and_exact_attachment_bytes()
    {
        string version = RunPython(null, "-c", "import pypdf; print(pypdf.__version__)").Trim();
        Assert.True(version == "6.21.1", "pypdf version command returned " + version + ". Expected 6.21.1.");

        byte[] attachment = new byte[] { 0x00, 0x01, 0x02, 0x7F, 0x80, 0xFE, 0xFF, 0x42, 0x2D, 0x32, 0x33 };
        byte[] pdf = SaveInfoXmpAndAttachment(attachment);
        string path = TempPdf();
        try
        {
            File.WriteAllBytes(path, pdf);
            string report = RunPython(ReaderScript, "-", path);
            string title = Value(report, "TITLE", version);
            string xmp = Value(report, "XMP", version);
            string bytesHex = Value(report, "BYTES", version);
            _output.WriteLine("pypdf " + version);
            _output.WriteLine("Info title " + title);
            _output.WriteLine("XMP dc:title " + xmp);
            _output.WriteLine("attachment " + bytesHex);

            Assert.True(
                title == "InfoEdited",
                "pypdf " + version + " did not confirm the Info title edit. Expected InfoEdited. Actual " + title + ".");
            Assert.True(
                xmp == "XmpEdited",
                "pypdf " + version + " did not confirm the XMP dc:title edit. Expected XmpEdited. Actual " + xmp + ".");
            Assert.True(
                string.Equals(bytesHex, Convert.ToHexString(attachment), StringComparison.OrdinalIgnoreCase),
                "pypdf " + version + " attachment bytes differ. Expected " + Convert.ToHexString(attachment) + ". Actual " + bytesHex + ".");
        }
        finally
        {
            Delete(path);
        }
    }

    private static byte[] SaveInfoXmpAndAttachment(byte[] attachment)
    {
        byte[] pdf = BasicPdf();
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        document.Metadata.Title = "InfoEdited";
        document.Metadata.SetXmp(Encoding.UTF8.GetBytes(XmpPacket));
        using var content = new MemoryStream(attachment, writable: false);
        document.Attachments.Add("notes.bin", content, leaveOpen: true);
        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);
        return destination.ToArray();
    }

    private static string Value(string report, string key, string version)
    {
        string prefix = key + "=";
        string? found = null;
        string[] lines = report.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(found == null, "pypdf " + version + " repeated " + key + ".");
            found = lines[i].Substring(prefix.Length);
        }

        Assert.True(found != null, "pypdf " + version + " did not report " + key + "." + Environment.NewLine + report);
        return found!;
    }

    private static string RunPython(string? input, params string[] args)
    {
        Assert.True(File.Exists(PythonExe), "pypdf reader requires " + PythonExe + ".");
        var info = new ProcessStartInfo
        {
            FileName = PythonExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input != null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        for (int i = 0; i < args.Length; i++)
        {
            info.ArgumentList.Add(args[i]);
        }

        Process? process = Process.Start(info);
        if (process == null)
        {
            throw new InvalidOperationException(PythonExe + " did not start.");
        }

        using (process)
        {
            if (input != null)
            {
                process.StandardInput.Write(input);
                process.StandardInput.Close();
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                "pypdf reader exited " + process.ExitCode.ToString() + "." + Environment.NewLine + stderr + stdout);
            return stdout;
        }
    }

    private static bool TryLocateQpdf(out string reason)
    {
        try
        {
            using Process where = Start("where.exe", "qpdf");
            string stdout = where.StandardOutput.ReadToEnd();
            string stderr = where.StandardError.ReadToEnd();
            where.WaitForExit();
            if (where.ExitCode == 0 && stdout.Trim().Length > 0)
            {
                reason = string.Empty;
                return true;
            }

            reason = "qpdf is not on PATH. where.exe qpdf exited " + where.ExitCode.ToString() + ".";
            if (stderr.Trim().Length > 0)
            {
                reason += " " + stderr.Trim();
            }

            return false;
        }
        catch (Win32Exception)
        {
            return ProbeQpdf(out reason);
        }
    }

    private static bool ProbeQpdf(out string reason)
    {
        try
        {
            using Process process = Start("qpdf", "--version");
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            reason = string.Empty;
            return true;
        }
        catch (Win32Exception ex)
        {
            reason = "qpdf is not on PATH. " + ex.Message;
            return false;
        }
    }

    private static void Check(string path)
    {
        using Process process = Start("qpdf", "--check", path);
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            "qpdf --check exited " + process.ExitCode.ToString() + " for " + path + Environment.NewLine + stderr + stdout);
    }

    private static byte[] BasicPdf()
    {
        PdfBuilder builder = PdfBuilder.Basic(pageExtra: " /Resources << >>");
        builder.Indirect(4, "<< /Title (Interop) >>");
        return builder.Finish("/Info 4 0 R");
    }

    private static byte[] SaveWithTitle(byte[] pdf, string title)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        document.Metadata.Title = title;
        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);
        return destination.ToArray();
    }

    private static byte[] FillName(string value)
    {
        byte[] pdf = TextField();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.SetText("Name", value));
            document.SaveIncremental(stream);
        }

        return stream.ToArray();
    }

    private static byte[] TextField()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 300 724] /DA (/Helv 0 Tf 0 g) /MaxLen 8 /P 3 0 R >>");
        return builder.Finish();
    }

    private static byte[] SignBasic()
    {
        byte[] pdf = BasicPdf();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            document.Sign(stream, new PdfSignatureOptions { ReservedContentsBytes = 64 }, new SilentProvider());
        }

        return stream.ToArray();
    }

    private static Process Start(string fileName, params string[] args)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        for (int i = 0; i < args.Length; i++)
        {
            info.ArgumentList.Add(args[i]);
        }

        Process? process = Process.Start(info);
        if (process == null)
        {
            throw new InvalidOperationException(fileName + " did not start.");
        }

        return process;
    }

    private static string TempPdf()
    {
        return Path.Combine(Path.GetTempPath(), "jaytwo-ergonomics-pdf-" + Guid.NewGuid().ToString("N") + ".pdf");
    }

    private static void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class SilentProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public byte[] CreateContents(PdfSignatureContext context)
        {
            return new byte[] { 1, 2, 3, 4 };
        }
    }
}
