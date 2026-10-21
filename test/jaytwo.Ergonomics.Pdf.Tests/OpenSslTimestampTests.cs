using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class OpenSslTimestampTests
{
    [Fact]
    public void openssl_tsa_document_timestamp_is_accepted_by_pyhanko_and_openssl_verify()
    {
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            var authority = new OpenSslTimestampAuthority(openssl, directory);
            byte[] stamped = Stamp(authority);

            string request = OpenSslTimestampAuthority.Checked(openssl, "ts", "-query", "-in", authority.RequestPath, "-text");
            Assert.Contains("Certificate required: yes", request, StringComparison.Ordinal);
            Assert.Contains("Nonce: 0x", request, StringComparison.Ordinal);

            string report = PyHanko(directory, stamped, "root.pem");
            Assert.Contains("type=/DocTimeStamp", report, StringComparison.Ordinal);
            Assert.Contains("embedded_certs=1", report, StringComparison.Ordinal);
            Assert.Contains("intact=true", report, StringComparison.Ordinal);
            Assert.Contains("valid=true", report, StringComparison.Ordinal);
            Assert.Contains("trusted=true", report, StringComparison.Ordinal);
            Assert.Contains("coverage=ENTIRE_FILE", report, StringComparison.Ordinal);

            byte[] range = SignedRange(stamped, out byte[] token);
            Assert.Equal(authority.Token, token);
            (int exitCode, string output) = OpenSslVerify(openssl, directory, range, token, "root.pem");
            Assert.True(exitCode == 0, output);
            Assert.Contains("Verification: OK", output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void verifiers_reject_an_untrusted_root_and_a_tampered_byte()
    {
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            byte[] stamped = Stamp(new OpenSslTimestampAuthority(openssl, directory));
            byte[] tampered = (byte[])stamped.Clone();
            Assert.Equal((byte)'3', tampered[7]);
            tampered[7] = (byte)'4';

            string untrusted = PyHanko(directory, stamped, "other.pem");
            Assert.Contains("intact=true", untrusted, StringComparison.Ordinal);
            Assert.Contains("trusted=false", untrusted, StringComparison.Ordinal);

            string broken = PyHanko(directory, tampered, "root.pem");
            Assert.Contains("intact=false", broken, StringComparison.Ordinal);

            byte[] range = SignedRange(stamped, out byte[] token);
            (int exitCode, string output) = OpenSslVerify(openssl, directory, range, token, "other.pem");
            Assert.True(exitCode != 0, output);
            Assert.Contains("unable to get local issuer certificate", output, StringComparison.Ordinal);

            byte[] tamperedRange = SignedRange(tampered, out byte[] sameToken);
            Assert.Equal(token, sameToken);
            (exitCode, output) = OpenSslVerify(openssl, directory, tamperedRange, token, "root.pem");
            Assert.True(exitCode != 0, output);
            Assert.Contains("message imprint mismatch", output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] Stamp(ITimestampAuthority authority)
    {
        using var source = new MemoryStream(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        var destination = new MemoryStream();
        document.Sign(
            destination,
            new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp, ReservedContentsBytes = 16384 },
            new DocTimeStampSignatureProvider(authority),
            leaveOpen: true);
        return destination.ToArray();
    }

    private static byte[] SignedRange(byte[] pdf, out byte[] token)
    {
        string text = TestCompat.Latin1.GetString(pdf);
        MatchCollection matches = Regex.Matches(text, @"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]");
        Assert.Single(matches);
        GroupCollection groups = matches[0].Groups;
        int start1 = int.Parse(groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        int length1 = int.Parse(groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        int start2 = int.Parse(groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
        int length2 = int.Parse(groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(pdf.Length, start2 + length2);

        byte[] contents = TestCompat.FromHexString(text.Substring(start1 + length1, start2 - start1 - length1).Trim('<', '>'));
        int length = contents[1];
        int header = 2;
        if ((length & 0x80) != 0)
        {
            int count = length & 0x7F;
            length = 0;
            for (int i = 0; i < count; i++)
            {
                length = (length << 8) | contents[2 + i];
            }

            header += count;
        }

        token = contents.AsSpan(0, header + length).ToArray();
        var range = new byte[length1 + length2];
        Buffer.BlockCopy(pdf, start1, range, 0, length1);
        Buffer.BlockCopy(pdf, start2, range, length1, length2);
        return range;
    }

    private static string PyHanko(string directory, byte[] pdf, string root)
    {
        string pdfPath = Path.Combine(directory, "stamped.pdf");
        File.WriteAllBytes(pdfPath, pdf);
        using Process process = ExternalTool.Start(
            "python",
            ExternalTool.Fixture("validate_doc_timestamp.py"),
            pdfPath,
            Path.Combine(directory, root));
        return ExternalTool.Output(process);
    }

    private static (int ExitCode, string Output) OpenSslVerify(string openssl, string directory, byte[] range, byte[] token, string root)
    {
        string rangePath = Path.Combine(directory, "range.bin");
        string tokenPath = Path.Combine(directory, "embedded.tst");
        File.WriteAllBytes(rangePath, range);
        File.WriteAllBytes(tokenPath, token);
        using Process process = ExternalTool.Start(
            openssl,
            "ts",
            "-verify",
            "-data",
            rangePath,
            "-in",
            tokenPath,
            "-token_in",
            "-CAfile",
            Path.Combine(directory, root));
        (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
        return (exitCode, stdout + stderr);
    }
}
