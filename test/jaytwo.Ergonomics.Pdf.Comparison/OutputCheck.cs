using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Comparison;

// Checks on a workload's output, run after the measured passes. Every write is an incremental update, so the source
// is a byte prefix of the output; this library then reads the result back, and qpdf checks the form outputs.
internal static class OutputCheck
{
    public static void Edited(string source, string output, string title)
    {
        Prefix(source, output);
        using var file = ComparisonFixtures.OpenRead(output);
        using var document = PdfDocument.Open(file, leaveOpen: true);
        Assert.Equal(title, document.Metadata.Title);
    }

    public static void Filled(string source, string output, int pages)
    {
        Prefix(source, output);
        using (var file = ComparisonFixtures.OpenRead(output))
        using (var document = PdfDocument.Open(file, leaveOpen: true))
        {
            int filled = 0;
            foreach (PdfField field in document.AcroForm.Fields)
            {
                if (field.FieldType == PdfFieldType.Text)
                {
                    string number = field.FullName.Substring("page".Length, field.FullName.IndexOf('.') - "page".Length);
                    Assert.Equal("Filled " + number, field.Value);
                    filled++;
                }
            }

            Assert.Equal(pages, filled);
        }

        Qpdf(output);
    }

    public static void Flattened(string source, string output, int pages, bool incremental)
    {
        if (incremental)
        {
            Prefix(source, output);
        }

        using (var file = ComparisonFixtures.OpenRead(output))
        using (var document = PdfDocument.Open(file, leaveOpen: true))
        {
            Assert.Equal(pages, document.PageCount);
            Assert.Empty(document.AcroForm.Fields);
        }

        Qpdf(output);
    }

    public static void Signed(string source, string output, bool qpdf)
    {
        long length = Prefix(source, output);
        using (var file = ComparisonFixtures.OpenRead(output))
        using (var document = PdfDocument.Open(file, leaveOpen: true))
        {
            PdfSignatureInfo signature = Assert.Single(document.Signatures);
            Assert.True(signature.IsSigned);
            Assert.Equal("G7", signature.Reason);
            Assert.Equal(length, signature.SignedRevisionEnd);
        }

        if (qpdf)
        {
            Qpdf(output);
        }
    }

    // The payload compresses, so the save grows the carrier by less than half the payload.
    public static void Attached(string source, string output, string payload)
    {
        long length = Prefix(source, output);
        Assert.True(length - new FileInfo(source).Length < ComparisonFixtures.PayloadLength / 2, "the save added " + (length - new FileInfo(source).Length).ToString(CultureInfo.InvariantCulture) + " bytes");
        using var file = ComparisonFixtures.OpenRead(output);
        using var document = PdfDocument.Open(file, leaveOpen: true);
        using Stream opened = document.Attachments[ComparisonFixtures.AttachmentName].Open();
        Assert.Equal(Hash(payload), Hash(opened));
    }

    public static void SameBytes(string expected, string actual)
    {
        Assert.Equal(Hash(expected), Hash(actual));
    }

    // Returns the output length.
    private static long Prefix(string source, string output)
    {
        long sourceLength = new FileInfo(source).Length;
        long outputLength = new FileInfo(output).Length;
        Assert.True(outputLength > sourceLength, output + " is not longer than its source");
        Assert.Equal(Hash(source, sourceLength), Hash(output, sourceLength));
        return outputLength;
    }

    private static string Hash(string path)
    {
        return Hash(path, new FileInfo(path).Length);
    }

    private static string Hash(string path, long count)
    {
        using var file = ComparisonFixtures.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        long remaining = count;
        while (remaining > 0)
        {
            int read = file.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            Assert.True(read > 0, "unexpected end of " + path);
            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }

        return TestCompat.ToHexString(hash.GetHashAndReset());
    }

    private static string Hash(Stream stream)
    {
        using var sha = SHA256.Create();
        return TestCompat.ToHexString(sha.ComputeHash(stream));
    }

    private static void Qpdf(string path)
    {
        string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
        using Process process = ExternalTool.Start(qpdf, "--check", path);
        ExternalTool.Output(process);
    }
}
