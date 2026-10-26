#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using jaytwo.Ergonomics.Pdf.Fuzz;
using jaytwo.Pdf.Signatures;
using Xunit;

namespace jaytwo.Pdf.Tests;

public class CmsFuzzTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void expected_failure_in_one_mode_does_not_suppress_the_other(bool failingMode)
    {
        var calls = new List<bool>();
        CmsFuzzTarget.Run(Array.Empty<byte>(), (_, mode) =>
        {
            calls.Add(mode);
            if (mode == failingMode)
            {
                throw new PdfParseException(0, "Expected rejection.");
            }
        });
        Assert.Equal(new[] { false, true }, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void unexpected_failures_escape_from_either_mode(bool failingMode)
    {
        Assert.Throws<InvalidOperationException>(() => CmsFuzzTarget.Run(Array.Empty<byte>(), (_, mode) =>
        {
            if (mode == failingMode)
            {
                throw new InvalidOperationException("Finding.");
            }
        }));
    }

    [Fact]
    public void cms_limit_is_inclusive_and_oversized_inputs_do_not_reach_either_parser()
    {
        int calls = 0;
        CmsFuzzTarget.Run(new byte[65536], (_, _) => calls++);
        Assert.Equal(2, calls);
        CmsFuzzTarget.Run(new byte[65537], (_, _) => calls++);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void every_hex_signature_is_extracted_with_pdf_whitespace_and_padding_preserved()
    {
        byte[] pdf = Encoding.ASCII.GetBytes("/ByteRange [0 1 2 3] /Contents<30800000> /Contents\t<30\n00> /Contents <123>");
        Assert.Equal(new[] { "30800000", "3000", "1230" }, CmsSeeds.Contents(pdf).Select(Convert.ToHexString));
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void registered_signed_fixtures_and_timestamp_seeds_reach_inspection()
    {
        string root = Root();
        IReadOnlyList<byte[]> seeds = CmsSeeds.Build(root);
        Assert.Contains(seeds, seed => seed[0] == 0x30 && seed[1] == 0x80);
        string external = Path.Combine(root, "tools", "fuzz", "seeds", "cms");
        Assert.NotNull(CmsSignatureReader.Read(File.ReadAllBytes(Path.Combine(external, "document-timestamp.cms")), true, 0).TimestampTime);
        Assert.NotNull(CmsSignatureReader.Read(File.ReadAllBytes(Path.Combine(external, "signature-timestamp.cms")), false, 0).TimestampTime);
        Assert.NotNull(CmsSignatureReader.Read(File.ReadAllBytes(Path.Combine(external, "subject-key-identifier.cms")), false, 0).SignerCertificate);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Makefile")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository not found.");
    }
}
#endif
