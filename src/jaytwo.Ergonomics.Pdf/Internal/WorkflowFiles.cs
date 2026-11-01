using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Ergonomics.Pdf.Internal;

// Spec F17 and D-60 item 5: path checks, the temporary sibling, and publication for a workflow's file save.
internal static class WorkflowFiles
{
    private static readonly StringComparison PathComparison =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    // Resolve links in every segment. Older targets cannot resolve reparse points portably, so refuse them.
    public static string Normalize(string path)
    {
        return Normalize(path, 0);
    }

    public static bool SamePath(string first, string second)
    {
        return string.Equals(Normalize(first), Normalize(second), PathComparison);
    }

    public static string TemporarySibling(string target)
    {
        string directory = Path.GetDirectoryName(target)!;
        string random = Guid.NewGuid().ToString("N").Substring(0, 8);
        return Path.Combine(directory, "." + Path.GetFileName(target) + "." + random + ".tmp");
    }

    // Spec F17: the shared lifecycle of a file terminal. The write callback fills a new temporary sibling; the
    // helper flushes it when configured, observes cancellation, publishes it, and deletes it unless published.
    public static async Task<T> WriteAndPublishAsync<T>(string target, bool overwrite, bool flushToDisk, bool asyncStream, CancellationToken cancellationToken, Func<FileStream, Task<T>> write, params string?[] inputPaths)
    {
        string temporary = TemporarySibling(target);
        bool published = false;
        try
        {
            T result;
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, asyncStream))
            {
                result = await write(output).ConfigureAwait(false);
                if (flushToDisk)
                {
                    output.Flush(flushToDisk: true);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Callers keep their inputs open through this call, so the alias check sees them as the run read them.
            Publish(temporary, target, overwrite, inputPaths);
            published = true;
            return result;
        }
        finally
        {
            if (!published)
            {
                TryDelete(temporary);
            }
        }
    }

    // D-60 best-effort checks, repeated for every input immediately before atomic publication.
    public static void Publish(string temporary, string target, bool overwrite, params string?[] inputPaths)
    {
        CheckDestination(target, overwrite, true, inputPaths);
        if (!File.Exists(target))
        {
            File.Move(temporary, target);
            return;
        }

        File.Replace(temporary, target, null);
    }

    // WORKFLOW-FILE-01 to 04: a direct file save opens its destination only after the edits are applied and
    // CheckDestination passes, so a refusal creates or truncates nothing. CreateNew keeps fail-if-exists true even if the
    // file appears after the check.
    public static FileStream OpenDirect(string target, bool overwrite, bool asyncStream)
    {
        return new FileStream(target, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, asyncStream);
    }

    // The alias and existence checks, before anything is created, truncated or replaced. The staged terminals also refuse
    // a destination with the input's length and last-write time (F17); a direct save does not (WORKFLOW-ALIAS-01), since
    // matching metadata does not establish that two files are the same.
    public static void CheckDestination(string target, bool overwrite, bool refuseMatchingMetadata, params string?[] inputPaths)
    {
        foreach (string? inputPath in inputPaths)
        {
            if (inputPath != null && SamePath(inputPath, target))
            {
                throw new IOException("The destination is the input file. Save to a different path.");
            }
        }

        var existing = new FileInfo(target);
        if (!existing.Exists)
        {
            return;
        }

        if (!overwrite)
        {
            throw Exists(target);
        }

        if (!refuseMatchingMetadata)
        {
            return;
        }

        foreach (string? inputPath in inputPaths)
        {
            if (inputPath == null)
            {
                continue;
            }

            var input = new FileInfo(inputPath);
            // Length throws if input metadata is unavailable; never silently skip the safety check.
            if (input.Length == existing.Length && input.LastWriteTimeUtc == existing.LastWriteTimeUtc)
            {
                throw new IOException("The destination '" + target + "' may be the input file: it has the input's length and last-write time. Save to a different path, or use AppendToSource to add a revision to the input.");
            }
        }
    }

    // Spec F20: copies a finished staging file from its start to an empty destination in 1 MiB blocks. A failure or
    // cancellation sets the destination back to its starting length, as far as it allows.
    public static async Task CopyAsync(Stream staging, Stream destination, bool useAsync, CancellationToken cancellationToken)
    {
        long start = destination.Position;
        try
        {
            staging.Position = 0;
            var buffer = new byte[1024 * 1024];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = useAsync
                    ? await staging.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)
                    : staging.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                if (useAsync)
                {
                    await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    destination.Write(buffer, 0, read);
                }
            }

            destination.Flush();
        }
        catch
        {
            try
            {
                destination.SetLength(start);
                destination.Position = start;
            }
            catch (Exception rollback) when (rollback is IOException || rollback is NotSupportedException || rollback is ObjectDisposedException)
            {
            }

            throw;
        }
    }

    public static IOException Exists(string target)
    {
        return new IOException("The destination '" + target + "' already exists and PdfFileSaveOptions.Overwrite is false.");
    }

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string Normalize(string path, int links)
    {
        if (links > 40)
        {
            throw new IOException("The file path contains too many symbolic links.");
        }

        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full)!;
        string current = root;
        string[] segments = full.Substring(root.Length).Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (FileNotFoundException) when (index == segments.Length - 1)
            {
                return current;
            }

            if ((attributes & FileAttributes.ReparsePoint) == 0)
            {
                continue;
            }

#if NET6_0_OR_GREATER
            FileSystemInfo info = (attributes & FileAttributes.Directory) != 0 ? new DirectoryInfo(current) : new FileInfo(current);
            FileSystemInfo target = info.ResolveLinkTarget(true) ?? throw new IOException("The file path contains an unresolved reparse point.");
            current = Normalize(target.FullName, links + 1);
#else
            throw new IOException("The file path contains a reparse point that this runtime cannot resolve safely.");
#endif
        }

        return current;
    }
}
