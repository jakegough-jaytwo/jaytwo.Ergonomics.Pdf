#if NET10_0_OR_GREATER
using System.IO;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class DssInteropTests
{
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData(TestKeys.Rsa, PdfDigestAlgorithm.Sha256)]
    [InlineData(TestKeys.Rsa, PdfDigestAlgorithm.Sha384)]
    [InlineData(TestKeys.Rsa, PdfDigestAlgorithm.Sha512)]
    [InlineData(TestKeys.P256, PdfDigestAlgorithm.Sha256)]
    [InlineData(TestKeys.P256, PdfDigestAlgorithm.Sha384)]
    [InlineData(TestKeys.P256, PdfDigestAlgorithm.Sha512)]
    [InlineData(TestKeys.P384, PdfDigestAlgorithm.Sha256)]
    [InlineData(TestKeys.P384, PdfDigestAlgorithm.Sha384)]
    [InlineData(TestKeys.P384, PdfDigestAlgorithm.Sha512)]
    public void dss_classifies_baseline_b_for_every_supported_key_and_digest(string key, PdfDigestAlgorithm digest)
    {
        DssCases.Run("workflow", key, digest, false, null, false, Sign);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData(TestKeys.Rsa, PdfDigestAlgorithm.Sha256)]
    [InlineData(TestKeys.P256, PdfDigestAlgorithm.Sha384)]
    public void dss_classifies_baseline_t_with_an_openssl_timestamp(string key, PdfDigestAlgorithm digest)
    {
        DssCases.Run("workflow", key, digest, true, null, false, Sign);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("owner")]
    [InlineData("user")]
    public void dss_classifies_encrypted_baseline_b(string password)
    {
        DssCases.Run("workflow", TestKeys.Rsa, PdfDigestAlgorithm.Sha256, false, password, false, Sign);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void dss_distinguishes_basic_tampered_corrupt_and_mismatched_timestamp_controls()
    {
        DssCases.Run("workflow", TestKeys.Rsa, PdfDigestAlgorithm.Sha256, false, null, true, Sign);
    }

    private static byte[] Sign(byte[] input, string? password, PdfSignatureOptions options, IPdfExternalSigner signer, ITimestampAuthority? authority)
    {
        using var source = new MemoryStream(input);
        using var output = new MemoryStream();
        PdfWorkflow.From(source, new PdfWorkflowOpenOptions { Password = password }).Sign(output, signer, options, authority);
        return output.ToArray();
    }
}
#endif
