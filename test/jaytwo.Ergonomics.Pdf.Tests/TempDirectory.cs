using System;
using System.IO;
using System.Linq;

namespace jaytwo.Ergonomics.Pdf.Tests;

// A directory of its own for each file-save test, removed afterwards.
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jaytwo-workflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name)
    {
        return System.IO.Path.Combine(Path, name);
    }

    public string Write(string name, byte[] data)
    {
        string path = File(name);
        System.IO.File.WriteAllBytes(path, data);
        return path;
    }

    // The names of the temporary siblings a file save leaves in the directory.
    public string[] Temporaries()
    {
        return Directory.GetFiles(Path)
            .Select(file => System.IO.Path.GetFileName(file)!)
            .Where(name => name.EndsWith(".tmp", StringComparison.Ordinal))
            .ToArray();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
