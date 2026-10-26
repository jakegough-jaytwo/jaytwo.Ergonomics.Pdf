using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.Signatures;
using Xunit;

namespace jaytwo.Pdf.Tests;

public sealed class SignatureSeedValueTests
{
    [Theory]
    [InlineData("<< /Ff 1 /Filter /Other >>")]
    [InlineData("<< /Ff 2 /SubFilter [/ETSI.CAdES.detached /adbe.pkcs7.detached] >>")]
    [InlineData("<< /Ff 2 /SubFilter [] >>")]
    [InlineData("<< /Ff 2 /SubFilter (adbe.pkcs7.detached) >>")]
    [InlineData("<< /Ff 4 /V 4 >>")]
    [InlineData("<< /Ff 8 /Reasons [(Approved)] >>")]
    [InlineData("<< /Ff 8 /Reasons [] >>")]
    [InlineData("<< /Ff 8 /Reasons [42] >>")]
    [InlineData("<< /Ff 16 /LegalAttestation [(Reviewed)] >>")]
    [InlineData("<< /Ff 32 /AddRevInfo true >>")]
    [InlineData("<< /Ff 32 /AddRevInfo /false >>")]
    [InlineData("<< /Ff 64 /DigestMethod [/SHA384] >>")]
    [InlineData("<< /Ff 64 /DigestMethod [(SHA256)] >>")]
    [InlineData("<< /Ff 128 /LockDocument /true >>")]
    [InlineData("<< /Ff 256 /AppearanceFilter (Corporate) >>")]
    [InlineData("<< /MDP << /P 1 >> >>")]
    [InlineData("<< /MDP << /P 2 >> >>")]
    [InlineData("<< /MDP << /P 3 >> >>")]
    [InlineData("<< /MDP << /P 4 >> >>")]
    [InlineData("<< /TimeStamp << /Ff 1 >> >>")]
    [InlineData("<< /Ff (8) >>")]
    public void incompatible_cms_seeds_refuse_before_the_provider_or_destination_is_touched(string seed)
    {
        Reject(seed, Options());
    }

    [Theory]
    [InlineData("<< /Cert << /Ff 1 /Subject [<00>] >> >>")]
    [InlineData("<< /Cert << /Ff 2 /Issuer [<00>] >> >>")]
    [InlineData("<< /Cert << /Ff 4 /OID [(1.2.3)] /Issuer [<00>] >> >>")]
    [InlineData("<< /Cert << /Ff 8 /SubjectDN [<< /CN (Signer) >>] >> >>")]
    [InlineData("<< /Cert << /Ff 32 /KeyUsage [(1)] >> >>")]
    [InlineData("<< /Cert << /Ff 64 /URL (https://example.test/) /URLType /Custom >> >>")]
    public void nested_certificate_flags_apply_without_a_top_level_required_flag(string seed)
    {
        Reject(seed, Options());
        Reject(seed, Options(PdfSignatureKind.DocTimeStamp));
    }

    [Theory]
    [InlineData("<< /Ff 0 /Filter /Other /SubFilter [/Unsupported] /V 100 /Reasons [] /DigestMethod [/SHA1] /LegalAttestation [] /AddRevInfo true /LockDocument /true /AppearanceFilter (Unknown) >>", null)]
    [InlineData("<< /Filter 42 /SubFilter 42 /V (bad) /Reasons 42 /DigestMethod 42 /AddRevInfo /bad /Cert << /Subject 42 >> >>", null)]
    [InlineData("<< /Ff 1 /Filter /Adobe.PPKLite >>", null)]
    [InlineData("<< /Ff 2 /SubFilter [/Unsupported /adbe.pkcs7.detached /ETSI.CAdES.detached] >>", null)]
    [InlineData("<< /Ff 4 /V 3 >>", null)]
    [InlineData("<< /Ff 8 >>", null)]
    [InlineData("<< /Ff 8 /Reasons [(.)] >>", null)]
    [InlineData("<< /Ff 8 /Reasons [(Approved) (Reviewed)] >>", "Reviewed")]
    [InlineData("<< /Ff 8 /Reasons [<FEFF0041007000700072006F007600650064>] >>", "Approved")]
    [InlineData("<< /Ff 8 /Reasons [()] >>", "")]
    [InlineData("<< /Ff 32 /AddRevInfo false >>", null)]
    [InlineData("<< /Ff 64 /DigestMethod [/SHA512 /SHA256] >>", null)]
    [InlineData("<< /MDP << /P 0 >> >>", null)]
    [InlineData("<< /MDP << >> /TimeStamp << /Ff 0 /URL (https://example.test/) >> >>", null)]
    [InlineData("<< /Cert << /Ff 64 /URL (https://example.test/enroll) >> >>", null)]
    [InlineData("<< /Cert << /Ff 64 /URLType /Browser /URL (https://example.test/enroll) >> >>", null)]
    [InlineData("<< /Cert << /SignaturePolicyOID (1.2.3) /SignaturePolicyHashValue <00> /SignaturePolicyHashAlgorithm /SHA256 /SignaturePolicyCommitmentType [(1.2.4)] >> >>", null)]
    [InlineData("<< /Cert << /Ff 16 /SignaturePolicyHashValue 42 /SignaturePolicyHashAlgorithm 42 /SignaturePolicyCommitmentType 42 >> >>", null)]
    [InlineData("<< /Ff 128 /LockDocument /auto >>", null)]
    [InlineData("<< /Ff 128 /LockDocument /false >>", null)]
    [InlineData("<< /Ff 99 /PrivateValue 42 >>", null)]
    public void compatible_and_advisory_cms_seeds_preserve_the_selected_operation(string seed, string? reason)
    {
        Accept(seed, Options(reason: reason));
    }

    [Theory]
    [InlineData("<< /Ff 8 >>")]
    [InlineData("<< /Ff 8 /Reasons [(.)] >>")]
    public void required_reason_omission_disallows_even_an_empty_emitted_reason(string seed)
    {
        Reject(seed, Options(reason: string.Empty));
    }

    [Theory]
    [InlineData("<< /Ff 8 /Reasons [(Approved)] >>")]
    [InlineData("<< /Ff 8 /Reasons [] >>")]
    [InlineData("<< /Ff 64 /DigestMethod [/SHA384 /SHA512] >>")]
    [InlineData("<< /MDP << /P 0 >> >>")]
    [InlineData("<< /MDP << /P 1 >> >>")]
    [InlineData("<< /MDP << /P 2 >> >>")]
    [InlineData("<< /MDP << /P 3 >> >>")]
    [InlineData("<< /TimeStamp << /Ff 1 /URL (https://example.test/tsa) >> >>")]
    public void timestamp_constraints_follow_the_approved_operation_policy(string seed)
    {
        Reject(seed, Options(PdfSignatureKind.DocTimeStamp));
    }

    [Theory]
    [InlineData("<< /Ff 8 >>")]
    [InlineData("<< /Ff 8 /Reasons [(.)] >>")]
    [InlineData("<< /Ff 64 /DigestMethod [/SHA512 /SHA256] >>")]
    [InlineData("<< /Ff 2 /SubFilter [/ETSI.CAdES.detached /ETSI.RFC3161] >>")]
    [InlineData("<< /TimeStamp << /Ff 1 >> >>")]
    [InlineData("<< /TimeStamp << /Ff 1 /URL null >> >>")]
    [InlineData("<< /TimeStamp << /Ff 0 /URL (https://example.test/tsa) >> >>")]
    [InlineData("<< /Cert << /Ff 0 /SignaturePolicyOID (1.2.3) >> >>")]
    public void timestamp_omission_imprint_and_endpoint_rules_allow_compatible_requests(string seed)
    {
        Accept(seed, Options(PdfSignatureKind.DocTimeStamp, reason: "Not emitted"));
    }

    [Fact]
    public void cms_ordering_is_not_narrowed_to_the_callers_format_preference()
    {
        const string seed = "<< /Ff 2 /SubFilter [/ETSI.RFC3161 /ETSI.CAdES.detached /adbe.pkcs7.detached] >>";
        Reject(seed, Options());
        Accept(seed, Options(format: PdfSignatureFormat.PadesBaselineB));
    }

    [Theory]
    [InlineData(PdfDigestAlgorithm.Sha384, "SHA384")]
    [InlineData(PdfDigestAlgorithm.Sha512, "SHA512")]
    public void digest_selection_is_enforced_and_passed_to_the_provider(PdfDigestAlgorithm digest, string name)
    {
        Accept("<< /Ff 64 /DigestMethod [/" + name + "] >>", Options(digest: digest));
    }

    [Fact]
    public void pdf17_unknown_key_type_mismatches_refuse_and_version_controls_new_seed_entries()
    {
        Reject("<< /Ff 64 /DigestMethod [/SHA384] >>", Options(), version: "1.7");
        Reject("<< /Ff 4 /V 3 >>", Options(), version: "1.7");
        Accept("<< /Ff 4 /V 2.0 >>", Options(), version: "1.7");
        Reject("<< /Ff 4 /V 2.0 >>", Options());
        Accept("<< /Ff 384 /LockDocument /true /AppearanceFilter (Unavailable) >>", Options(), version: "1.7");
        Reject("<< /Ff 128 /LockDocument /true >>", Options(), version: "1.7", catalogExtra: " /Version /2.0");
    }

    [Fact]
    public void mandatory_false_distinguishes_document_permissions_from_field_locks()
    {
        const string seed = "<< /Ff 128 /LockDocument /false >>";
        Reject(seed, Options(), fieldExtra: " /Lock << /Action /All /P 1 >>");
        Reject(seed, Options(), fieldExtra: " /Lock << /Action /All /P 2 >>");
        Accept(seed, Options(), fieldExtra: " /Lock << /Action /Include /Fields [(Other)] >>");
        Accept("<< /Ff 128 /LockDocument /auto >>", Options(), fieldExtra: " /Lock << /Action /All /P 2 >>");
    }

    [Fact]
    public void indirect_seed_entries_and_decoded_text_are_checked_on_the_field_owner()
    {
        var builder = PdfBuilder.Basic(" /AcroForm 4 0 R", " /Annots [7 0 R]", version: "2.0");
        builder.Indirect(4, "<< /Fields [6 0 R] >>");
        builder.Indirect(5, "<< /Ff 8 /Reasons 8 0 R >>");
        builder.Indirect(6, "<< /FT /Sig /T (Signature1) /Kids [7 0 R] /SV 5 0 R >>");
        builder.Indirect(7, "<< /Subtype /Widget /Parent 6 0 R /Rect [0 0 0 0] /P 3 0 R /SV << /Ff 1 /Filter /Wrong >> >>");
        builder.Indirect(8, "[9 0 R]");
        builder.Indirect(9, "(Approved)");
        byte[] pdf = builder.Finish();
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        using var destination = new MemoryStream();
        var provider = new RecordingProvider(PdfSignatureKind.CmsDetached);
        Assert.Throws<PdfSignException>(() => editor.Sign(destination, Options(), provider, leaveOpen: true));
        Assert.Equal(0, provider.Calls);
        editor.Sign(destination, Options(reason: "Approved"), provider, leaveOpen: true);
        Assert.Equal(1, provider.Calls);
        AssertPrefix(pdf, destination);
    }

    [Fact]
    public void every_distinct_field_owner_with_the_same_name_is_checked()
    {
        var builder = PdfBuilder.Basic(" /AcroForm 4 0 R", " /Annots [6 0 R 7 0 R]");
        builder.Indirect(4, "<< /Fields [6 0 R 7 0 R] >>");
        builder.Indirect(6, "<< /FT /Sig /T (Signature1) /Subtype /Widget /Rect [0 0 0 0] >>");
        builder.Indirect(7, "<< /FT /Sig /T (Signature1) /Subtype /Widget /Rect [0 0 0 0] /SV << /Ff 1 /Filter /Wrong >> >>");
        using var source = new MemoryStream(builder.Finish());
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        using var destination = new MemoryStream();
        var provider = new RecordingProvider(PdfSignatureKind.CmsDetached);
        Assert.Throws<PdfSignException>(() => editor.Sign(destination, Options(), provider, leaveOpen: true));
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task asynchronous_same_stream_refusal_preserves_pending_edits_and_can_be_retried()
    {
        byte[] pdf = Fixture("<< /Ff 8 /Reasons [(Approved)] >>");
        using var source = new MemoryStream();
        source.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        editor.Metadata.Title = "Pending";
        var provider = new RecordingProvider(PdfSignatureKind.CmsDetached);
        await Assert.ThrowsAsync<PdfSignException>(() => editor.SignAsync(source, Options(), provider, leaveOpen: true));
        Assert.Equal(pdf, source.ToArray());
        Assert.Equal(0, provider.Calls);
        await editor.SignAsync(source, Options(reason: "Approved"), provider, leaveOpen: true);
        Assert.Equal(1, provider.Calls);
        AssertPrefix(pdf, source);
        Assert.Contains("/Reason (Approved)", Encoding.ASCII.GetString(source.ToArray()));
        using var reopened = PdfDocument.Open(new MemoryStream(source.ToArray()));
        Assert.Equal("Pending", reopened.Metadata.Title);
    }

    private static PdfSignatureOptions Options(
        PdfSignatureKind kind = PdfSignatureKind.CmsDetached,
        string? reason = null,
        PdfDigestAlgorithm digest = PdfDigestAlgorithm.Sha256,
        PdfSignatureFormat format = PdfSignatureFormat.AdbePkcs7Detached)
    {
        return new PdfSignatureOptions
        {
            FieldName = "Signature1",
            Kind = kind,
            Reason = reason,
            DigestAlgorithm = digest,
            Format = format,
            ReservedContentsBytes = 32,
            SigningTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
    }

    private static byte[] Fixture(string seed, string fieldExtra = "", string version = "2.0", string catalogExtra = "")
    {
        var builder = PdfBuilder.Basic(" /AcroForm 4 0 R" + catalogExtra, " /Annots [6 0 R]", version: version);
        builder.Indirect(4, "<< /Fields [6 0 R] >>");
        builder.Indirect(5, seed);
        builder.Indirect(6, "<< /FT /Sig /T (Signature1) /Subtype /Widget /Rect [10 20 30 40] /P 3 0 R /SV 5 0 R" + fieldExtra + " >>");
        return builder.Finish();
    }

    private static void Reject(string seed, PdfSignatureOptions options, string fieldExtra = "", string version = "2.0", string catalogExtra = "")
    {
        byte[] pdf = Fixture(seed, fieldExtra, version, catalogExtra);
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        editor.Metadata.Title = "Pending";
        using var destination = new MemoryStream();
        var provider = new RecordingProvider(options.Kind);
        Assert.Throws<PdfSignException>(() => editor.Sign(destination, options, provider, leaveOpen: true));
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, destination.Length);
        Assert.Equal(pdf, source.ToArray());
        editor.SaveIncremental(destination, leaveOpen: true);
        using var reopened = PdfDocument.Open(new MemoryStream(destination.ToArray()));
        Assert.Equal("Pending", reopened.Metadata.Title);
        Assert.Single(reopened.AcroForm.GetFields("Signature1"));
        Assert.DoesNotContain("/ByteRange", Encoding.ASCII.GetString(destination.ToArray()));
    }

    private static void Accept(string seed, PdfSignatureOptions options, string fieldExtra = "", string version = "2.0")
    {
        byte[] pdf = Fixture(seed, fieldExtra, version);
        using var source = new MemoryStream(pdf);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        using var editor = document.Edit();
        using var destination = new MemoryStream();
        var provider = new RecordingProvider(options.Kind);
        editor.Sign(destination, options, provider, leaveOpen: true);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(options.DigestAlgorithm, provider.Digest);
        Assert.Equal(options.Format, provider.Format);
        Assert.True(provider.SignedLength > pdf.Length);
        AssertPrefix(pdf, destination);
        string appendix = Encoding.ASCII.GetString(destination.ToArray(), pdf.Length, (int)destination.Length - pdf.Length);
        Assert.DoesNotContain("/TransformMethod", appendix);
        Assert.DoesNotContain("/LockDocument", appendix);
        Assert.DoesNotContain("/SignaturePolicy", appendix);
        if (options.Kind == PdfSignatureKind.DocTimeStamp)
        {
            Assert.Contains("/SubFilter /ETSI.RFC3161", appendix);
            Assert.DoesNotContain("/Reason", appendix);
        }
        else
        {
            Assert.Contains(options.Format == PdfSignatureFormat.PadesBaselineB ? "/SubFilter /ETSI.CAdES.detached" : "/SubFilter /adbe.pkcs7.detached", appendix);
        }
    }

    private static void AssertPrefix(byte[] pdf, MemoryStream output)
    {
        byte[] prefix = new byte[pdf.Length];
        Buffer.BlockCopy(output.ToArray(), 0, prefix, 0, prefix.Length);
        Assert.Equal(pdf, prefix);
    }

    // Deliberately opaque contents: these tests establish engine preflight and provider inputs,
    // not CMS validity or independent verification of arbitrary provider output.
    private sealed class RecordingProvider : IPdfSignatureProvider
    {
        internal RecordingProvider(PdfSignatureKind kind)
        {
            Kind = kind;
        }

        public PdfSignatureKind Kind { get; }

        internal int Calls { get; private set; }

        internal PdfDigestAlgorithm Digest { get; private set; }

        internal PdfSignatureFormat Format { get; private set; }

        internal long SignedLength { get; private set; }

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            Calls++;
            Digest = context.DigestAlgorithm;
            Format = context.Format;
            using Stream bytes = context.OpenSignedBytes();
            using var copy = new MemoryStream();
            bytes.CopyTo(copy);
            SignedLength = copy.Length;
            return new ValueTask<byte[]>(new byte[] { 1, 2, 3 });
        }
    }
}
