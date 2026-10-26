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

    // A full path, with a symbolic link in the last segment resolved where the runtime can (net6+).
    public static string Normalize(string path)
    {
        string full = Path.GetFullPath(path);
#if NET6_0_OR_GREATER
        var info = new FileInfo(full);
        if (info.LinkTarget != null && info.ResolveLinkTarget(true) is FileSystemInfo target)
        {
            return target.FullName;
        }
#endif
        return full;
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

    // Moves the finished temporary file to the target. An existing target is replaced only with overwrite, and only when
    // it cannot be the input: a true alias always has the input's length and last-write time, so a match is refused even
    // though a copy with copied timestamps matches too (D-60 item 5).
    public static void Publish(string temporary, string target, bool overwrite, string? inputPath)
    {
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

        if (inputPath != null)
        {
            var input = new FileInfo(inputPath);
            if (input.Exists && input.Length == existing.Length && input.LastWriteTimeUtc == existing.LastWriteTimeUtc)
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
}
