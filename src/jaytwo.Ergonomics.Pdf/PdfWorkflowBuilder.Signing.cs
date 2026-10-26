using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Pdf;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Signatures;

namespace jaytwo.Ergonomics.Pdf;

// Spec F18 to F21: the signing and document-timestamp terminals. The ordinary edits are saved to a private staging file
// through the run's one editor, which then appends the signature or timestamp to that file. Only a finished, signed
// file is copied to a stream or published to a path.
public sealed partial class PdfWorkflowBuilder
{
    /// <summary>
    /// Runs the workflow, adds one CMS signature, and writes the result to an empty writable seekable stream (spec F18 to F21).
    /// </summary>
    /// <remarks>
    /// The ordinary edits are saved to a private temporary file, which the same editor then signs. The signed file is copied
    /// to <paramref name="destination"/> only after signing, and any embedded timestamp, has finished. The edits add at most
    /// one revision and the signature one more. A failure before the copy leaves the destination unchanged; after a failure
    /// or cancellation during the copy it is set back to its starting length, as far as it allows. The signer and
    /// authority are each called at most once, and never again after a failure.
    /// </remarks>
    /// <param name="destination">The destination. It is not disposed.</param>
    /// <param name="signer">The host signer.</param>
    /// <param name="options">The signature field, page, format and reserved size. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="timestampAuthority">An authority for an RFC 3161 timestamp stored inside the signature, or null.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, the destination is not empty, writable, and seekable, or it is the input stream.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public PdfWorkflowResult Sign(Stream destination, IPdfExternalSigner signer, PdfSignatureOptions options, ITimestampAuthority? timestampAuthority = null)
    {
        return SignStreamAsync(destination, options, CmsProvider(signer, timestampAuthority), PdfSignatureKind.CmsDetached, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the workflow, adds one CMS signature from a signature provider, and writes the result to an empty writable seekable stream (spec F18 to F21).
    /// </summary>
    /// <param name="destination">The destination. It is not disposed.</param>
    /// <param name="provider">The signature provider, called at most once. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="options">The signature field, page, format and reserved size. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, the destination is not empty, writable, and seekable, or it is the input stream.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public PdfWorkflowResult Sign(Stream destination, IPdfSignatureProvider provider, PdfSignatureOptions options)
    {
        return SignStreamAsync(destination, options, provider, PdfSignatureKind.CmsDetached, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the workflow, adds one CMS signature, and writes the result to a file (spec F18 to F21).
    /// </summary>
    /// <remarks>
    /// The signed output is staged in the temporary sibling that <see cref="Save(string, PdfFileSaveOptions)"/> uses and is
    /// published under the same rules, only after signing has finished.
    /// </remarks>
    /// <param name="path">The destination path. Its directory must exist.</param>
    /// <param name="signer">The host signer.</param>
    /// <param name="options">The signature field, page, format and reserved size. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="timestampAuthority">An authority for an RFC 3161 timestamp stored inside the signature, or null.</param>
    /// <param name="saveOptions">Overwrite and flush options.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, or the path is the input's path.</exception>
    /// <exception cref="IOException">The destination exists without <see cref="PdfFileSaveOptions.Overwrite"/>, or it may be the input.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public PdfWorkflowResult Sign(string path, IPdfExternalSigner signer, PdfSignatureOptions options, ITimestampAuthority? timestampAuthority = null, PdfFileSaveOptions? saveOptions = null)
    {
        return SignFileAsync(path, options, CmsProvider(signer, timestampAuthority), PdfSignatureKind.CmsDetached, saveOptions, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the workflow, adds one CMS signature from a signature provider, and writes the result to a file (spec F18 to F21).
    /// </summary>
    /// <param name="path">The destination path. Its directory must exist.</param>
    /// <param name="provider">The signature provider, called at most once. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="options">The signature field, page, format and reserved size. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="saveOptions">Overwrite and flush options.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, or the path is the input's path.</exception>
    /// <exception cref="IOException">The destination exists without <see cref="PdfFileSaveOptions.Overwrite"/>, or it may be the input.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public PdfWorkflowResult Sign(string path, IPdfSignatureProvider provider, PdfSignatureOptions options, PdfFileSaveOptions? saveOptions = null)
    {
        return SignFileAsync(path, options, provider, PdfSignatureKind.CmsDetached, saveOptions, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the workflow, adds one CMS signature, and writes the result to an empty writable seekable stream (spec F18 to F21).
    /// </summary>
    /// <param name="destination">The destination. It is not disposed.</param>
    /// <param name="signer">The host signer.</param>
    /// <param name="options">The signature field, page, format and reserved size. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="timestampAuthority">An authority for an RFC 3161 timestamp stored inside the signature, or null.</param>
    /// <param name="cancellationToken">Passed to the signer and authority, and observed until the signed output is copied.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, the destination is not empty, writable, and seekable, or it is the input stream.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public Task<PdfWorkflowResult> SignAsync(Stream destination, IPdfExternalSigner signer, PdfSignatureOptions options, ITimestampAuthority? timestampAuthority = null, CancellationToken cancellationToken = default)
    {
        return SignStreamAsync(destination, options, CmsProvider(signer, timestampAuthority), PdfSignatureKind.CmsDetached, true, cancellationToken);
    }

    /// <summary>
    /// Runs the workflow, adds one CMS signature from a signature provider, and writes the result to an empty writable seekable stream (spec F18 to F21).
    /// </summary>
    /// <param name="destination">The destination. It is not disposed.</param>
    /// <param name="provider">The signature provider, called at most once. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="options">The signature field, page, format and reserved size. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="cancellationToken">Passed to the provider, and observed until the signed output is copied.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, the destination is not empty, writable, and seekable, or it is the input stream.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public Task<PdfWorkflowResult> SignAsync(Stream destination, IPdfSignatureProvider provider, PdfSignatureOptions options, CancellationToken cancellationToken = default)
    {
        return SignStreamAsync(destination, options, provider, PdfSignatureKind.CmsDetached, true, cancellationToken);
    }

    /// <summary>
    /// Runs the workflow, adds one CMS signature, and writes the result to a file (spec F18 to F21).
    /// </summary>
    /// <param name="path">The destination path. Its directory must exist.</param>
    /// <param name="signer">The host signer.</param>
    /// <param name="options">The signature field, page, format and reserved size. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="timestampAuthority">An authority for an RFC 3161 timestamp stored inside the signature, or null.</param>
    /// <param name="saveOptions">Overwrite and flush options.</param>
    /// <param name="cancellationToken">Passed to the signer and authority, and observed until the output is published.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, or the path is the input's path.</exception>
    /// <exception cref="IOException">The destination exists without <see cref="PdfFileSaveOptions.Overwrite"/>, or it may be the input.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public Task<PdfWorkflowResult> SignAsync(string path, IPdfExternalSigner signer, PdfSignatureOptions options, ITimestampAuthority? timestampAuthority = null, PdfFileSaveOptions? saveOptions = null, CancellationToken cancellationToken = default)
    {
        return SignFileAsync(path, options, CmsProvider(signer, timestampAuthority), PdfSignatureKind.CmsDetached, saveOptions, true, cancellationToken);
    }

    /// <summary>
    /// Runs the workflow, adds one CMS signature from a signature provider, and writes the result to a file (spec F18 to F21).
    /// </summary>
    /// <param name="path">The destination path. Its directory must exist.</param>
    /// <param name="provider">The signature provider, called at most once. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="options">The signature field, page, format and reserved size. Its kind must be <see cref="PdfSignatureKind.CmsDetached"/>.</param>
    /// <param name="saveOptions">Overwrite and flush options.</param>
    /// <param name="cancellationToken">Passed to the provider, and observed until the output is published.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, or the path is the input's path.</exception>
    /// <exception cref="IOException">The destination exists without <see cref="PdfFileSaveOptions.Overwrite"/>, or it may be the input.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public Task<PdfWorkflowResult> SignAsync(string path, IPdfSignatureProvider provider, PdfSignatureOptions options, PdfFileSaveOptions? saveOptions = null, CancellationToken cancellationToken = default)
    {
        return SignFileAsync(path, options, provider, PdfSignatureKind.CmsDetached, saveOptions, true, cancellationToken);
    }

    /// <summary>
    /// Runs the workflow, adds one RFC 3161 document timestamp, and writes the result to an empty writable seekable stream (spec F18 to F21).
    /// </summary>
    /// <param name="destination">The destination. It is not disposed.</param>
    /// <param name="authority">The timestamp authority, called at most once.</param>
    /// <param name="options">The field, page and reserved size, or null for the defaults. Its kind is ignored: the result is a <see cref="PdfSignatureKind.DocTimeStamp"/>.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, the destination is not empty, writable, and seekable, or it is the input stream.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public PdfWorkflowResult Timestamp(Stream destination, ITimestampAuthority authority, PdfSignatureOptions? options = null)
    {
        return SignStreamAsync(destination, TimestampOptions(options), TimestampProvider(authority), PdfSignatureKind.DocTimeStamp, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the workflow, adds one RFC 3161 document timestamp, and writes the result to a file (spec F18 to F21).
    /// </summary>
    /// <param name="path">The destination path. Its directory must exist.</param>
    /// <param name="authority">The timestamp authority, called at most once.</param>
    /// <param name="options">The field, page and reserved size, or null for the defaults. Its kind is ignored: the result is a <see cref="PdfSignatureKind.DocTimeStamp"/>.</param>
    /// <param name="saveOptions">Overwrite and flush options.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, or the path is the input's path.</exception>
    /// <exception cref="IOException">The destination exists without <see cref="PdfFileSaveOptions.Overwrite"/>, or it may be the input.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public PdfWorkflowResult Timestamp(string path, ITimestampAuthority authority, PdfSignatureOptions? options = null, PdfFileSaveOptions? saveOptions = null)
    {
        return SignFileAsync(path, TimestampOptions(options), TimestampProvider(authority), PdfSignatureKind.DocTimeStamp, saveOptions, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs the workflow, adds one RFC 3161 document timestamp, and writes the result to an empty writable seekable stream (spec F18 to F21).
    /// </summary>
    /// <param name="destination">The destination. It is not disposed.</param>
    /// <param name="authority">The timestamp authority, called at most once.</param>
    /// <param name="options">The field, page and reserved size, or null for the defaults. Its kind is ignored: the result is a <see cref="PdfSignatureKind.DocTimeStamp"/>.</param>
    /// <param name="cancellationToken">Passed to the authority, and observed until the signed output is copied.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, the destination is not empty, writable, and seekable, or it is the input stream.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public Task<PdfWorkflowResult> TimestampAsync(Stream destination, ITimestampAuthority authority, PdfSignatureOptions? options = null, CancellationToken cancellationToken = default)
    {
        return SignStreamAsync(destination, TimestampOptions(options), TimestampProvider(authority), PdfSignatureKind.DocTimeStamp, true, cancellationToken);
    }

    /// <summary>
    /// Runs the workflow, adds one RFC 3161 document timestamp, and writes the result to a file (spec F18 to F21).
    /// </summary>
    /// <param name="path">The destination path. Its directory must exist.</param>
    /// <param name="authority">The timestamp authority, called at most once.</param>
    /// <param name="options">The field, page and reserved size, or null for the defaults. Its kind is ignored: the result is a <see cref="PdfSignatureKind.DocTimeStamp"/>.</param>
    /// <param name="saveOptions">Overwrite and flush options.</param>
    /// <param name="cancellationToken">Passed to the authority, and observed until the output is published.</param>
    /// <returns>The run's result.</returns>
    /// <exception cref="ArgumentException">An argument is invalid, or the path is the input's path.</exception>
    /// <exception cref="IOException">The destination exists without <see cref="PdfFileSaveOptions.Overwrite"/>, or it may be the input.</exception>
    /// <exception cref="InvalidOperationException">The workflow has already run.</exception>
    public Task<PdfWorkflowResult> TimestampAsync(string path, ITimestampAuthority authority, PdfSignatureOptions? options = null, PdfFileSaveOptions? saveOptions = null, CancellationToken cancellationToken = default)
    {
        return SignFileAsync(path, TimestampOptions(options), TimestampProvider(authority), PdfSignatureKind.DocTimeStamp, saveOptions, true, cancellationToken);
    }

    private static IPdfSignatureProvider CmsProvider(IPdfExternalSigner signer, ITimestampAuthority? timestampAuthority)
    {
        if (signer == null)
        {
            throw new ArgumentNullException(nameof(signer));
        }

        return new CmsDetachedSignatureProvider(signer, timestampAuthority);
    }

    private static IPdfSignatureProvider TimestampProvider(ITimestampAuthority authority)
    {
        if (authority == null)
        {
            throw new ArgumentNullException(nameof(authority));
        }

        return new DocTimeStampSignatureProvider(authority);
    }

    private static PdfSignatureOptions TimestampOptions(PdfSignatureOptions? options)
    {
        return new PdfSignatureOptions
        {
            FieldName = options?.FieldName,
            PageNumber = options?.PageNumber ?? 1,
            Rect = options?.Rect ?? default,
            Kind = PdfSignatureKind.DocTimeStamp,
            Format = options?.Format ?? PdfSignatureFormat.AdbePkcs7Detached,
            DigestAlgorithm = options?.DigestAlgorithm ?? PdfDigestAlgorithm.Sha256,
            ReservedContentsBytes = options?.ReservedContentsBytes ?? 16384,
            Reason = options?.Reason,
            Location = options?.Location,
            ContactInfo = options?.ContactInfo,
            SignerName = options?.SignerName,
            SigningTime = options?.SigningTime,
        };
    }

    // Spec F21: the shape errors that need no document are thrown before the input opens.
    private static void CheckSigning(PdfSignatureOptions options, IPdfSignatureProvider provider, PdfSignatureKind kind)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        if (options.Kind != kind)
        {
            throw new ArgumentException("The options' kind is " + options.Kind + ", but this terminal adds a " + kind + ".", nameof(options));
        }

        if (provider.Kind != kind)
        {
            throw new ArgumentException("The provider's kind is " + provider.Kind + ", but this terminal adds a " + kind + ".", nameof(provider));
        }
    }

    private async Task<PdfWorkflowResult> SignStreamAsync(Stream destination, PdfSignatureOptions options, IPdfSignatureProvider provider, PdfSignatureKind kind, bool useAsync, CancellationToken cancellationToken)
    {
        Begin();
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        CheckSigning(options, provider, kind);
        if (_input.IsStream(destination))
        {
            throw new ArgumentException("The destination is the input stream. Signing needs a separate output.", nameof(destination));
        }

        if (!destination.CanWrite || !destination.CanSeek || destination.Length != 0)
        {
            throw new ArgumentException("The destination must be an empty writable seekable stream.", nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using WorkflowSession session = await _input.OpenAsync(false, useAsync, cancellationToken).ConfigureAwait(false);
        string stagingPath = Path.Combine(Path.GetTempPath(), "jaytwo-pdf-" + Guid.NewGuid().ToString("N") + ".tmp");
        FileOptions fileOptions = FileOptions.DeleteOnClose | (useAsync ? FileOptions.Asynchronous : FileOptions.None);
        using var staging = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, fileOptions);
        IReadOnlyDictionary<string, PdfAppearanceDisposition> appearances = await StageSignedAsync(session, staging, options, provider, useAsync, cancellationToken).ConfigureAwait(false);
        await WorkflowFiles.CopyAsync(staging, destination, useAsync, cancellationToken).ConfigureAwait(false);
        return new PdfWorkflowResult(true, destination.Length, appearances);
    }

    private async Task<PdfWorkflowResult> SignFileAsync(string path, PdfSignatureOptions options, IPdfSignatureProvider provider, PdfSignatureKind kind, PdfFileSaveOptions? saveOptions, bool useAsync, CancellationToken cancellationToken)
    {
        Begin();
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (path.Length == 0)
        {
            throw new ArgumentException("The destination path is empty.", nameof(path));
        }

        CheckSigning(options, provider, kind);
        saveOptions ??= new PdfFileSaveOptions();
        string target = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(target) ?? throw new ArgumentException("The destination path names no file.", nameof(path));
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("The destination directory '" + directory + "' does not exist.");
        }

        string? inputPath = _input.FilePath;
        if (inputPath != null && WorkflowFiles.SamePath(inputPath, target))
        {
            throw new ArgumentException("The destination is the input file. Signing needs a separate output.", nameof(path));
        }

        if (!saveOptions.Overwrite && File.Exists(target))
        {
            throw WorkflowFiles.Exists(target);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using WorkflowSession session = await _input.OpenAsync(false, useAsync, cancellationToken).ConfigureAwait(false);
        string temporary = WorkflowFiles.TemporarySibling(target);
        bool published = false;
        try
        {
            IReadOnlyDictionary<string, PdfAppearanceDisposition> appearances;
            long outputLength;
            using (var staging = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, useAsync))
            {
                appearances = await StageSignedAsync(session, staging, options, provider, useAsync, cancellationToken).ConfigureAwait(false);
                if (saveOptions.FlushToDisk)
                {
                    staging.Flush(flushToDisk: true);
                }

                outputLength = staging.Length;
            }

            cancellationToken.ThrowIfCancellationRequested();
            WorkflowFiles.Publish(temporary, target, saveOptions.Overwrite, inputPath);
            published = true;
            return new PdfWorkflowResult(true, outputLength, appearances);
        }
        finally
        {
            if (!published)
            {
                WorkflowFiles.TryDelete(temporary);
            }
        }
    }

    // Spec F19: the ordinary edits are saved to the staging file through the run's editor, which then signs that file.
    // With no edits the save is a copy, so the signature is the only revision added.
    private async Task<IReadOnlyDictionary<string, PdfAppearanceDisposition>> StageSignedAsync(WorkflowSession session, FileStream staging, PdfSignatureOptions options, IPdfSignatureProvider provider, bool useAsync, CancellationToken cancellationToken)
    {
        using PdfDocumentEditor editor = session.Document.Edit();
        IReadOnlyDictionary<string, PdfAppearanceDisposition> appearances = Apply(session, editor);
        if (useAsync)
        {
            await editor.SaveIncrementalAsync(staging, leaveOpen: true, cancellationToken).ConfigureAwait(false);
            await editor.SignAsync(options, provider, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            editor.SaveIncremental(staging, leaveOpen: true);
            editor.Sign(options, provider);
        }

        return appearances;
    }
}
