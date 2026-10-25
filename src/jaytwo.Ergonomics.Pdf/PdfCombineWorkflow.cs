using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Pdf;

namespace jaytwo.Ergonomics.Pdf;

/// <summary>A single-use workflow that writes a new, unencrypted PDF through the engine's assembly API.</summary>
/// <remarks>
/// Caller streams and sources are borrowed. Engine exceptions propagate unchanged. All selections and permissions
/// are checked before output. Streams must be empty, writable and seekable; a failed stream save is truncated to zero
/// on a best-effort basis. File saves follow F17: a temporary sibling is flushed before publication, overwrite is
/// explicit, and known or possible aliases of any input are refused. These checks are best effort and imply no
/// power-loss durability. The engine assembles synchronously, including during asynchronous saves; cancellation is
/// observed during opening and before and after assembly, and before publication. Inputs remain open until the run ends.
/// </remarks>
public sealed class PdfCombineWorkflow
{
    private readonly PdfCombineInput[] _inputs;
    private readonly PdfAssemblyOptions _options;
    private int _started;

    internal PdfCombineWorkflow(IEnumerable<PdfCombineInput> inputs, PdfAssemblyOptions? options)
    {
        _inputs = (inputs ?? throw new ArgumentNullException(nameof(inputs))).ToArray();
        if (_inputs.Length == 0 || _inputs.Any(input => input == null))
        {
            throw new ArgumentException("Provide at least one non-null input.", nameof(inputs));
        }

        _options = new PdfAssemblyOptions
        {
            AllowUnencryptedOutput = options?.AllowUnencryptedOutput ?? false,
            ExternalLinks = options?.ExternalLinks ?? PdfExternalLinkMode.PreserveSupported,
        };
        if (_options.ExternalLinks != PdfExternalLinkMode.PreserveSupported && _options.ExternalLinks != PdfExternalLinkMode.Drop)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    /// <summary>Runs once and writes to an empty writable seekable stream, left open.</summary>
    /// <param name="destination">The output, distinct from every input.</param>
    /// <returns>The engine's aggregate omission counts.</returns>
    public PdfAssemblyResult Save(Stream destination)
    {
        return RunAsync(destination, null, null, false, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Runs once, opening asynchronously and assembling synchronously, leaving the destination open.</summary>
    /// <param name="destination">An empty writable seekable stream, distinct from every input.</param>
    /// <param name="cancellationToken">Observed during opening and before and after assembly.</param>
    /// <returns>The engine's aggregate omission counts.</returns>
    public Task<PdfAssemblyResult> SaveAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        return RunAsync(destination, null, null, false, true, cancellationToken);
    }

    /// <summary>Runs once and publishes a complete file through a temporary sibling (F17).</summary>
    /// <param name="path">The output path, distinct from every input; its directory must exist.</param>
    /// <param name="options">Explicit overwrite and flush options; flushing to disk defaults to true.</param>
    /// <returns>The engine's aggregate omission counts.</returns>
    public PdfAssemblyResult Save(string path, PdfFileSaveOptions? options = null)
    {
        return RunAsync(null, path, options, true, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Runs once and publishes a complete file through a temporary sibling (F17).</summary>
    /// <param name="path">The output path, distinct from every input; its directory must exist.</param>
    /// <param name="options">Explicit overwrite and flush options; flushing to disk defaults to true.</param>
    /// <param name="cancellationToken">Observed during opening and until publication; assembly is synchronous.</param>
    /// <returns>The engine's aggregate omission counts.</returns>
    public Task<PdfAssemblyResult> SaveAsync(string path, PdfFileSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        return RunAsync(null, path, options, true, true, cancellationToken);
    }

    private async Task<PdfAssemblyResult> RunAsync(Stream? destination, string? path, PdfFileSaveOptions? options, bool fileOutput, bool useAsync, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The workflow has already run.");
        }

        if (fileOutput && path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!fileOutput && destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        string? target = fileOutput ? Path.GetFullPath(path!) : null;
        string? streamPath = (destination as FileStream)?.Name;
        foreach (PdfCombineInput input in _inputs)
        {
            if ((destination != null && input.Input.IsStream(destination)) ||
                (input.Input.FilePath is string inputPath && (target ?? streamPath) is string outputPath && WorkflowFiles.SamePath(inputPath, outputPath)))
            {
                throw new ArgumentException("The destination is an input.");
            }
        }

        bool overwrite = options?.Overwrite ?? false;
        bool flushToDisk = options?.FlushToDisk ?? true;
        if (target != null)
        {
            if (!Directory.Exists(Path.GetDirectoryName(target)))
            {
                throw new DirectoryNotFoundException("The destination directory does not exist.");
            }

            if (!overwrite && File.Exists(target))
            {
                throw WorkflowFiles.Exists(target);
            }
        }
        else if (destination == null || !destination.CanWrite || !destination.CanSeek || destination.Length != 0 || destination.Position != 0)
        {
            throw new ArgumentException("The destination must be an empty writable seekable stream at position zero.", nameof(destination));
        }

        var sessions = new List<WorkflowSession>();
        string? temporary = null;
        bool published = false;
        try
        {
            var selections = new List<PdfAssemblyInput>();
            foreach (PdfCombineInput input in _inputs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WorkflowSession session = await input.Input.OpenAsync(false, useAsync, cancellationToken).ConfigureAwait(false);
                sessions.Add(session);
                selections.Add(new PdfAssemblyInput(session.Document, input.PageNumbers ?? Enumerable.Range(1, session.Document.Pages.Count)));
            }

            cancellationToken.ThrowIfCancellationRequested();
            PdfAssemblyResult result;
            if (target == null)
            {
                try
                {
                    result = PdfDocumentEditor.Concatenate(selections, destination!, _options);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch
                {
                    try
                    {
                        destination!.SetLength(0);
                        destination.Position = 0;
                    }
                    catch (IOException)
                    {
                    }
                    catch (NotSupportedException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    throw;
                }
            }
            else
            {
                temporary = WorkflowFiles.TemporarySibling(target);
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    result = PdfDocumentEditor.Concatenate(selections, output, _options);
                    if (flushToDisk)
                    {
                        output.Flush(flushToDisk: true);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (overwrite && File.Exists(target))
                {
                    var existing = new FileInfo(target);
                    foreach (PdfCombineInput input in _inputs)
                    {
                        if (input.Input.FilePath is string inputPath)
                        {
                            var file = new FileInfo(inputPath);
                            if (file.Exists && file.Length == existing.Length && file.LastWriteTimeUtc == existing.LastWriteTimeUtc)
                            {
                                throw new IOException("The destination may alias an input: its length and last-write time match.");
                            }
                        }
                    }
                }

                WorkflowFiles.Publish(temporary, target, overwrite, null);
                published = true;
            }

            return result;
        }
        finally
        {
            if (temporary != null && !published)
            {
                WorkflowFiles.TryDelete(temporary);
            }

            foreach (WorkflowSession session in sessions)
            {
                session.Dispose();
            }
        }
    }
}
