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

    // D-60's best-effort checks, repeated for every input immediately before atomic publication.
    public static void Publish(string temporary, string target, bool overwrite, params string?[] inputPaths)
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
            File.Move(temporary, target);
            return;
        }

        if (!overwrite)
        {
            throw Exists(target);
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

        File.Replace(temporary, target, null);
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
        return new IOException("The destination '" + target + "' already exists. Set PdfFileSaveOptions.Overwrite to replace it.");
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
