using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;
using jaytwo.Pdf.Tests;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Spec F18 to F21: signing and document-timestamp terminals, staged through one editor.
public class WorkflowSigningTests
{
    private const int Mebibyte = 1024 * 1024;

    private static readonly DateTimeOffset SigningTime = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("stream", false)]
    [InlineData("stream", true)]
    [InlineData("path", false)]
    [InlineData("path", true)]
    [InlineData("provider-stream", false)]
    [InlineData("provider-path", true)]
    public async Task signing_an_unchanged_document_adds_one_signature_revision(string terminal, bool useAsync)
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        using var signer = new TestSigner();
        string path = directory.File("out.pdf");
        var destination = new MemoryStream();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(input));

        PdfWorkflowResult result;
        switch (terminal)
        {
            case "stream":
                result = useAsync ? await workflow.SignAsync(destination, signer, Options()) : workflow.Sign(destination, signer, Options());
                break;
            case "path":
                result = useAsync ? await workflow.SignAsync(path, signer, Options()) : workflow.Sign(path, signer, Options());
                break;
            case "provider-stream":
                result = useAsync
                    ? await workflow.SignAsync(destination, new CmsDetachedSignatureProvider(signer), Options())
                    : workflow.Sign(destination, new CmsDetachedSignatureProvider(signer), Options());
                break;
            default:
                result = useAsync
                    ? await workflow.SignAsync(path, new CmsDetachedSignatureProvider(signer), Options())
                    : workflow.Sign(path, new CmsDetachedSignatureProvider(signer), Options());
                break;
        }

        byte[] output = terminal.EndsWith("path", StringComparison.Ordinal) ? File.ReadAllBytes(path) : destination.ToArray();
        WorkflowPdf.AssertOneRevision(input, output);
        AssertSigned(output, "adbe.pkcs7.detached");
        Assert.True(result.Modified);
        Assert.Equal(output.Length, result.OutputLength);
        Assert.Equal(1, signer.Calls);
        Assert.Empty(directory.Temporaries());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task edits_then_signing_add_an_edit_revision_and_a_separate_signature_revision(bool useAsync)
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var signer = new TestSigner();
        var destination = new MemoryStream();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(input)).WithTitle("Signed");

        if (useAsync)
        {
            await workflow.SignAsync(destination, signer, Options());
        }
        else
        {
            workflow.Sign(destination, signer, Options());
        }

        byte[] output = destination.ToArray();
        WorkflowPdf.AssertPrefix(input, output);
        using PdfDocument original = WorkflowPdf.Open(input);
        using PdfDocument signed = WorkflowPdf.Open(output);
        Assert.Equal(original.Revisions.Count + 2, signed.Revisions.Count);
        Assert.Equal("Signed", signed.Metadata.Title);
        Assert.Single(signed.Signatures);
    }

    [Fact]
    public void an_existing_empty_signature_field_is_signed_by_name()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] /SigFlags 3 >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Approval) /Rect [72 72 220 120] /F 4 /P 3 0 R >>");
        byte[] input = builder.Finish();
        using var signer = new TestSigner();
        var destination = new MemoryStream();

        PdfWorkflow.From(new MemoryStream(input)).Sign(destination, signer, new PdfSignatureOptions { FieldName = "Approval", ReservedContentsBytes = 8192, SigningTime = SigningTime });

        using PdfDocument signed = WorkflowPdf.Open(destination.ToArray());
        PdfSignatureInfo signature = Assert.Single(signed.Signatures);
        Assert.Equal("Approval", signature.FieldName);
    }

    [Fact]
    public void an_encrypted_input_is_edited_and_signed_through_one_editor()
    {
        byte[] input = WorkflowPdf.EncryptedForm();
        using var signer = new TestSigner();
        var destination = new MemoryStream();

        PdfWorkflow.From(new MemoryStream(input), new PdfWorkflowOpenOptions { Password = "user" })
            .Fill("Name", "Grace")
            .Sign(destination, signer, Options());

        byte[] output = destination.ToArray();
        WorkflowPdf.AssertPrefix(input, output);
        using PdfDocument signed = PdfDocument.Open(new MemoryStream(output), "user", leaveOpen: false);
        Assert.Single(signed.Signatures);
        Assert.Equal("Grace", signed.AcroForm.Fields.Single(field => field.FullName == "Name").Value?.ToString());
        Assert.Equal(1, signer.Calls);
    }

    [Fact]
    public void the_original_input_opens_once()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var signer = new TestSigner();
        var saved = new CountingSource(input);
        PdfWorkflow.From(saved).WithTitle("Once").Save(new MemoryStream());
        var signed = new CountingSource(input);

        PdfWorkflow.From(signed).WithTitle("Once").Sign(new MemoryStream(), signer, Options());

        Assert.True(saved.LengthReads > 0);
        Assert.Equal(saved.LengthReads, signed.LengthReads);
    }

    [Fact]
    public void an_attachment_larger_than_the_signature_appendix_is_staged_and_signed()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string attachment = directory.Write("big.bin", WorkflowPdf.Padding(10 * Mebibyte));
        using var signer = new TestSigner();
        string output = directory.File("out.pdf");

        PdfWorkflow.FromFile(directory.Write("in.pdf", input)).AttachFile("big.bin", attachment).Sign(output, signer, Options());

        using PdfDocument signed = PdfDocument.Open(new FileStream(output, FileMode.Open, FileAccess.Read), leaveOpen: false);
        Assert.Single(signed.Signatures);
        Assert.Single(signed.Attachments);
        Assert.True(new FileInfo(output).Length > 10 * Mebibyte);
        WorkflowPdf.AssertPrefix(input, File.ReadAllBytes(output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait(Lane.Trait, Lane.External)]
    public async Task a_document_timestamp_adds_its_own_revision_and_an_embedded_timestamp_adds_none(bool useAsync)
    {
        byte[] input = WorkflowPdf.WithInfo();
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            var authority = new CountingAuthority(new OpenSslTimestampAuthority(openssl, directory));
            using var signer = new TestSigner();
            var timestamped = new MemoryStream();
            var edited = new MemoryStream();
            var embedded = new MemoryStream();

            if (useAsync)
            {
                await PdfWorkflow.From(new MemoryStream(input)).TimestampAsync(timestamped, authority);
                await PdfWorkflow.From(new MemoryStream(input)).WithTitle("Stamped").TimestampAsync(edited, authority);
                await PdfWorkflow.From(new MemoryStream(input)).SignAsync(embedded, signer, Options(), authority);
            }
            else
            {
                PdfWorkflow.From(new MemoryStream(input)).Timestamp(timestamped, authority);
                PdfWorkflow.From(new MemoryStream(input)).WithTitle("Stamped").Timestamp(edited, authority);
                PdfWorkflow.From(new MemoryStream(input)).Sign(embedded, signer, Options(), authority);
            }

            using PdfDocument original = WorkflowPdf.Open(input);
            using PdfDocument stamp = WorkflowPdf.Open(timestamped.ToArray());
            using PdfDocument stampedEdit = WorkflowPdf.Open(edited.ToArray());
            using PdfDocument signed = WorkflowPdf.Open(embedded.ToArray());
            Assert.Equal(original.Revisions.Count + 1, stamp.Revisions.Count);
            Assert.Equal(original.Revisions.Count + 2, stampedEdit.Revisions.Count);
            Assert.Equal(original.Revisions.Count + 1, signed.Revisions.Count);
            Assert.Contains("ETSI.RFC3161", Encoding.ASCII.GetString(timestamped.ToArray()), StringComparison.Ordinal);
            Assert.Equal(3, authority.Calls);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void a_timestamp_authority_failure_leaves_the_outputs_unchanged_and_is_not_retried()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.File("out.pdf");
        var destination = new MemoryStream();
        var authority = new FailingAuthority();

        Assert.Throws<PdfTimestampException>(() => PdfWorkflow.From(new MemoryStream(input)).WithTitle("T").Timestamp(destination, authority));
        var secondAuthority = new FailingAuthority();
        Assert.Throws<PdfTimestampException>(() => PdfWorkflow.From(new MemoryStream(input)).Timestamp(path, secondAuthority));

        Assert.Equal(0, destination.Length);
        Assert.False(File.Exists(path));
        Assert.Empty(directory.Temporaries());
        Assert.Equal(1, authority.Calls);
        Assert.Equal(1, secondAuthority.Calls);
    }

    [Fact]
    public void a_signer_failure_before_the_copy_leaves_a_stream_and_an_existing_file_unchanged()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        byte[] existing = Encoding.ASCII.GetBytes("existing");
        string path = directory.Write("out.pdf", existing);
        var destination = new MemoryStream();
        using var signer = new TestSigner { Throw = true };

        Assert.ThrowsAny<Exception>(() => PdfWorkflow.From(new MemoryStream(input)).WithTitle("T").Sign(destination, signer, Options()));
        Assert.ThrowsAny<Exception>(() => PdfWorkflow.From(new MemoryStream(input)).Sign(path, signer, Options(), saveOptions: new PdfFileSaveOptions { Overwrite = true }));

        Assert.Equal(0, destination.Length);
        Assert.Equal(existing, File.ReadAllBytes(path));
        Assert.Empty(directory.Temporaries());
        Assert.Equal(2, signer.Calls);
    }

    [Fact]
    public void a_reservation_overflow_fails_without_calling_the_signer_again()
    {
        using var signer = new TestSigner();
        var destination = new MemoryStream();
        var options = new PdfSignatureOptions { ReservedContentsBytes = 64, SigningTime = SigningTime };

        Assert.Throws<PdfSignatureTooLargeException>(() => PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).Sign(destination, signer, options));

        Assert.Equal(0, destination.Length);
        Assert.Equal(1, signer.Calls);
    }

    [Fact]
    public async Task cancellation_before_the_copy_leaves_the_outputs_unchanged()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string path = directory.File("out.pdf");
        using var signer = new TestSigner();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var destination = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.From(new MemoryStream(input)).SignAsync(destination, signer, Options(), cancellationToken: canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.From(new MemoryStream(input)).SignAsync(path, signer, Options(), cancellationToken: canceled.Token));

        using var inside = new CancellationTokenSource();
        using var canceling = new TestSigner { OnSign = inside.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.From(new MemoryStream(input)).WithTitle("T").SignAsync(destination, canceling, Options(), cancellationToken: inside.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfWorkflow.From(new MemoryStream(input)).WithTitle("T").SignAsync(path, canceling, Options(), cancellationToken: inside.Token));

        Assert.Equal(0, destination.Length);
        Assert.False(File.Exists(path));
        Assert.Empty(directory.Temporaries());
        Assert.Equal(0, signer.Calls);
        Assert.Equal(1, canceling.Calls);
    }

    [Fact]
    public void a_failure_while_copying_sets_the_destination_back_to_empty()
    {
        using var signer = new TestSigner();
        var destination = new HookedStream();
        destination.BeforeWrite = () => throw new IOException("The disk is full.");

        Assert.Throws<IOException>(() => PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo())).Sign(destination, signer, Options()));

        Assert.Equal(0, destination.Length);
        Assert.False(destination.Disposed);
        Assert.Equal(1, signer.Calls);
    }

    [Fact]
    public void shape_errors_are_thrown_before_the_input_opens()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo()));
        using var signer = new TestSigner();
        var occupied = new MemoryStream(new byte[] { 1 });
        var provider = new DocTimeStampSignatureProvider(new FailingAuthority());

        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.From(input).Sign((Stream)null!, signer, Options()));
        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.From(input).Sign(new MemoryStream(), (IPdfExternalSigner)null!, Options()));
        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.From(input).Sign(new MemoryStream(), signer, null!));
        Assert.Throws<ArgumentNullException>(() => PdfWorkflow.From(input).Timestamp(new MemoryStream(), null!));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.From(input).Sign(occupied, signer, Options()));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.From(input).Sign(input, signer, Options()));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.From(input).Sign(new MemoryStream(), signer, new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp }));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.From(input).Sign(new MemoryStream(), provider, Options()));
        Assert.Throws<ArgumentException>(() => PdfWorkflow.From(input).Sign(string.Empty, signer, Options()));

        Assert.False(input.ReadCalled);
        Assert.Equal(0, signer.Calls);
    }

    [Fact]
    public void a_signing_workflow_runs_once_and_borrowed_streams_stay_open()
    {
        var input = new ProbeStream(new MemoryStream(WorkflowPdf.WithInfo()));
        var destination = new HookedStream();
        using var signer = new TestSigner();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(input);

        workflow.Sign(destination, signer, Options());

        Assert.Throws<InvalidOperationException>(() => workflow.Sign(new MemoryStream(), signer, Options()));
        Assert.Throws<InvalidOperationException>(() => workflow.WithTitle("Again"));
        Assert.False(input.Disposed);
        Assert.False(destination.Disposed);
        Assert.Equal(1, signer.Calls);
    }

    [Fact]
    public void file_outputs_keep_the_alias_and_overwrite_rules()
    {
        byte[] input = WorkflowPdf.WithInfo();
        using var directory = new TempDirectory();
        string inputPath = directory.Write("in.pdf", input);
        string existing = directory.Write("existing.pdf", new byte[] { 1, 2, 3 });
        using var signer = new TestSigner();

        Assert.Throws<ArgumentException>(() => PdfWorkflow.FromFile(inputPath).Sign(inputPath, signer, Options()));
        Assert.Throws<IOException>(() => PdfWorkflow.FromFile(inputPath).Sign(existing, signer, Options()));
        Assert.Throws<DirectoryNotFoundException>(() => PdfWorkflow.FromFile(inputPath).Sign(directory.File("missing/out.pdf"), signer, Options()));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(existing));
        Assert.Equal(0, signer.Calls);

        PdfWorkflow.FromFile(inputPath).Sign(existing, signer, Options(), saveOptions: new PdfFileSaveOptions { Overwrite = true, FlushToDisk = false });

        WorkflowPdf.AssertOneRevision(input, File.ReadAllBytes(existing));
        Assert.Equal(input, File.ReadAllBytes(inputPath));
        Assert.Empty(directory.Temporaries());
    }

    [Fact]
    public void existing_certification_refusals_still_apply_and_leave_the_destination_unchanged()
    {
        byte[] input = WorkflowPdf.Form(" /Perms << /DocMDP 30 0 R >>", "<< /Type /Sig /Reference [<< /TransformMethod /DocMDP /TransformParams << /P 1 /V /1.2 >> >>] >>");
        using var signer = new TestSigner();
        var destination = new MemoryStream();

        Assert.ThrowsAny<PdfException>(() => PdfWorkflow.From(new MemoryStream(input)).Sign(destination, signer, Options()));

        Assert.Equal(0, destination.Length);
    }

    // WORKFLOW-BEGIN-01: rejected arguments leave the description usable for a corrected signing call.
    [Theory]
    [InlineData("null-stream")]
    [InlineData("read-only")]
    [InlineData("non-seekable")]
    [InlineData("non-empty")]
    [InlineData("position-not-zero")]
    [InlineData("null-path")]
    [InlineData("empty-path")]
    [InlineData("illegal-path")]
    [InlineData("null-options")]
    [InlineData("null-signer")]
    [InlineData("wrong-kind-options")]
    public async Task rejected_signing_arguments_do_not_consume_the_description(string rejected)
    {
        using var directory = new TempDirectory();
        using var signer = new TestSigner();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo()));
        string good = directory.File("signed.pdf");

        Action sync;
        Func<Task> async;
        switch (rejected)
        {
            case "null-options":
                sync = () => workflow.Sign(new MemoryStream(), signer, null!);
                async = () => workflow.SignAsync(new MemoryStream(), signer, null!);
                break;
            case "null-signer":
                sync = () => workflow.Sign(new MemoryStream(), (IPdfExternalSigner)null!, Options());
                async = () => workflow.SignAsync(good, (IPdfExternalSigner)null!, Options());
                break;
            case "wrong-kind-options":
                sync = () => workflow.Sign(new MemoryStream(), signer, new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp });
                async = () => workflow.SignAsync(good, signer, new PdfSignatureOptions { Kind = PdfSignatureKind.DocTimeStamp });
                break;
            case "null-path":
            case "empty-path":
            case "illegal-path":
                string? bad = WorkflowTerminalArguments.BadPath(rejected == "null-path" ? "null" : rejected == "empty-path" ? "empty" : "illegal-character");
                sync = () => workflow.Sign(bad!, signer, Options());
                async = () => workflow.SignAsync(bad!, signer, Options());
                break;
            default:
                string kind = rejected == "null-stream" ? "null" : rejected;
                sync = () => workflow.Sign(WorkflowTerminalArguments.Ineligible(kind)!, signer, Options());
                async = () => workflow.SignAsync(WorkflowTerminalArguments.Ineligible(kind)!, signer, Options());
                break;
        }

        WorkflowTerminalArguments.AssertRejected(sync);
        await WorkflowTerminalArguments.AssertRejectedAsync(async);

        Assert.Equal(0, signer.Calls);
        PdfWorkflowResult result = await workflow.SignAsync(good, signer, Options());
        Assert.True(result.Modified);
        AssertSigned(File.ReadAllBytes(good), "adbe.pkcs7.detached");
        Assert.Equal(1, signer.Calls);
        Assert.Throws<InvalidOperationException>(() => workflow.Sign(new MemoryStream(), signer, Options()));
    }

    [Fact]
    public async Task a_pre_cancelled_signing_call_with_accepted_arguments_consumes_the_description_once()
    {
        using var signer = new TestSigner();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo()));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.SignAsync(new MemoryStream(), signer, Options(), cancellationToken: canceled.Token));

        var output = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => workflow.Sign(output, signer, Options()));
        Assert.Equal(0, output.Length);
        Assert.Equal(0, signer.Calls);
    }

    // WORKFLOW-BEGIN-01 for the timestamp terminals: rejected arguments leave the description usable, which a corrected call
    // proves by reaching the authority (the failing authority makes the run fail, and that failure consumes it).
    [Theory]
    [InlineData("null-stream", false)]
    [InlineData("null-stream", true)]
    [InlineData("read-only", true)]
    [InlineData("non-seekable", false)]
    [InlineData("non-empty", true)]
    [InlineData("position-not-zero", false)]
    [InlineData("null-path", true)]
    [InlineData("null-path", false)]
    [InlineData("empty-path", false)]
    [InlineData("illegal-path", true)]
    [InlineData("null-authority", false)]
    [InlineData("null-authority", true)]
    public async Task rejected_timestamp_arguments_do_not_consume_the_description(string rejected, bool useAsync)
    {
        var authority = new FailingAuthority();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo()));
        Func<Task> terminal;
        if (rejected == "null-authority")
        {
            terminal = () => useAsync ? workflow.TimestampAsync(new MemoryStream(), null!) : Task.FromResult(workflow.Timestamp(new MemoryStream(), null!));
        }
        else if (rejected.EndsWith("path", StringComparison.Ordinal))
        {
            string? path = WorkflowTerminalArguments.BadPath(rejected == "null-path" ? "null" : rejected == "empty-path" ? "empty" : "illegal-character");
            terminal = () => useAsync ? workflow.TimestampAsync(path!, authority) : Task.FromResult(workflow.Timestamp(path!, authority));
        }
        else
        {
            string kind = rejected == "null-stream" ? "null" : rejected;
            terminal = () => useAsync
                ? workflow.TimestampAsync(WorkflowTerminalArguments.Ineligible(kind)!, authority)
                : Task.FromResult(workflow.Timestamp(WorkflowTerminalArguments.Ineligible(kind)!, authority));
        }

        Exception? thrown = await Record.ExceptionAsync(terminal);
        Assert.True(thrown is ArgumentException, "Expected an argument error, got " + thrown);
        Assert.Equal(0, authority.Calls);

        await Assert.ThrowsAsync<PdfTimestampException>(() => workflow.TimestampAsync(new MemoryStream(), authority));
        Assert.Equal(1, authority.Calls);
        Assert.Throws<InvalidOperationException>(() => workflow.Timestamp(new MemoryStream(), authority));
        Assert.Equal(1, authority.Calls);
    }

    [Fact]
    public async Task a_timestamp_refusal_after_accepted_arguments_consumes_the_description_once()
    {
        var authority = new FailingAuthority();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.Restricted())).WithTitle("New");

        Assert.Throws<PdfEncryptionRestrictedException>(() => workflow.Timestamp(new MemoryStream(), authority));

        var output = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => workflow.Timestamp(output, authority));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.TimestampAsync(output, authority));
        Assert.Equal(0, output.Length);
        Assert.Equal(0, authority.Calls);
    }

    [Fact]
    public async Task a_pre_cancelled_timestamp_with_accepted_arguments_consumes_the_description_once()
    {
        var authority = new FailingAuthority();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo()));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.TimestampAsync(new MemoryStream(), authority, cancellationToken: canceled.Token));

        var output = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => workflow.Timestamp(output, authority));
        Assert.Equal(0, output.Length);
        Assert.Equal(0, authority.Calls);
    }

    [Fact]
    public async Task a_pre_cancelled_timestamp_with_a_rejected_argument_leaves_the_description_usable()
    {
        var authority = new FailingAuthority();
        PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(WorkflowPdf.WithInfo()));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAsync<ArgumentException>(() => workflow.TimestampAsync(new MemoryStream(new byte[] { 1 }), authority, cancellationToken: canceled.Token));

        await Assert.ThrowsAsync<PdfTimestampException>(() => workflow.TimestampAsync(new MemoryStream(), authority));
    }

    [Fact]
    public async Task of_concurrent_timestamp_calls_exactly_one_reaches_the_authority()
    {
        byte[] pdf = WorkflowPdf.WithInfo();
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var authority = new FailingAuthority();
            PdfWorkflowBuilder workflow = PdfWorkflow.From(new MemoryStream(pdf));
            using var barrier = new Barrier(3);
            Task<Exception?> Run(Stream destination) => Task.Run<Exception?>(() =>
            {
                barrier.SignalAndWait();
                return Record.Exception(() => workflow.Timestamp(destination, authority));
            });

            Exception?[] outcomes = await Task.WhenAll(Run(new MemoryStream()), Run(new MemoryStream(new byte[] { 1 })), Run(new MemoryStream()));

            Assert.IsType<ArgumentException>(outcomes[1]);
            Assert.Single(new[] { outcomes[0], outcomes[2] }, outcome => outcome is PdfTimestampException);
            Assert.Single(new[] { outcomes[0], outcomes[2] }, outcome => outcome is InvalidOperationException);
            Assert.Equal(1, authority.Calls);
        }
    }

    private static PdfSignatureOptions Options()
    {
        return new PdfSignatureOptions { ReservedContentsBytes = 8192, SigningTime = SigningTime };
    }

    private static void AssertSigned(byte[] output, string subFilter)
    {
        using PdfDocument signed = WorkflowPdf.Open(output);
        Assert.Single(signed.Signatures);
        Assert.Contains(subFilter, Encoding.ASCII.GetString(output), StringComparison.Ordinal);
    }

    private sealed class TestSigner : IPdfExternalSigner, IDisposable
    {
        private readonly RSA _key = RSA.Create(2048);

        public TestSigner()
        {
            var request = new CertificateRequest("CN=Workflow Signer", _key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            CertificateDer = certificate.RawData;
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates => Array.Empty<byte[]>();

        public int Calls { get; private set; }

        public bool Throw { get; set; }

        public Action? OnSign { get; set; }

        public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            Calls++;
            OnSign?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (Throw)
            {
                throw new InvalidOperationException("The key is unavailable.");
            }

            return new ValueTask<byte[]>(_key.SignHash(hash.ToArray(), hashAlgorithm, RSASignaturePadding.Pkcs1));
        }

        public void Dispose()
        {
            _key.Dispose();
        }
    }

    private sealed class CountingAuthority : ITimestampAuthority
    {
        private readonly ITimestampAuthority _inner;

        public CountingAuthority(ITimestampAuthority inner)
        {
            _inner = inner;
        }

        public int Calls { get; private set; }

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            Calls++;
            return _inner.GetTimeStampTokenAsync(timeStampReqDer, cancellationToken);
        }
    }

    private sealed class FailingAuthority : ITimestampAuthority
    {
        public int Calls { get; private set; }

        public ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("The authority is down.");
        }
    }

    // Counts how often the source's length is read, which an open and a copy of the opened bytes each do.
    private sealed class CountingSource : IPdfSource
    {
        private readonly TestPdfSource _inner;

        public CountingSource(byte[] data)
        {
            _inner = new TestPdfSource(data);
        }

        public int LengthReads { get; private set; }

        public long Length
        {
            get
            {
                LengthReads++;
                return _inner.Length;
            }
        }

        public int Read(long position, Span<byte> buffer)
        {
            return _inner.Read(position, buffer);
        }

        public ValueTask<int> ReadAsync(long position, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _inner.ReadAsync(position, buffer, cancellationToken);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }
    }
}
