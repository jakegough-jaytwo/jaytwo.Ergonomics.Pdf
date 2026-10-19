using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

internal static class ExternalTool
{
    private const int TimeoutMilliseconds = 600000;

    public static string Fixture(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        Assert.True(File.Exists(path), path);
        return path;
    }

    public static string Locate(string variable, string fileName)
    {
        string? configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        string? tools = Environment.GetEnvironmentVariable("JAYTWO_TOOLS");
        if (!string.IsNullOrWhiteSpace(tools))
        {
            string candidate = Path.Combine(tools, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        using Process where = Start("where.exe", fileName);
        (int exitCode, string stdout, _) = Run(where);
        string[] lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(exitCode == 0 && lines.Length > 0, fileName + " was not found. Set " + variable + " or put it on PATH.");
        return lines[0];
    }

    public static Process Start(string fileName, params string[] args)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        for (int i = 0; i < args.Length; i++)
        {
            TestCompat.AddArgument(info, args[i]);
        }

        return Start(info);
    }

    public static Process Start(ProcessStartInfo info)
    {
        Process? process = Process.Start(info);
        if (process == null)
        {
            throw new InvalidOperationException(info.FileName + " did not start.");
        }

        return process;
    }

    public static string Output(Process process)
    {
        (int exitCode, string stdout, string stderr) = Run(process);
        Assert.True(exitCode == 0, process.StartInfo.FileName + " exited " + exitCode.ToString() + Environment.NewLine + stderr + stdout);
        return stdout;
    }

    public static (int ExitCode, string Stdout, string Stderr) Run(Process process)
    {
        // Drain both pipes at once. Reading stdout to the end first deadlocks when the tool fills the stderr pipe,
        // and openssl key generation writes up to about 4 KB of progress dots to stderr.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(process.StartInfo.FileName + " did not exit within " + (TimeoutMilliseconds / 1000).ToString() + " seconds.");
        }

        return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    public static string Pem(byte[] der)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-----BEGIN CERTIFICATE-----");
        string base64 = Convert.ToBase64String(der);
        for (int offset = 0; offset < base64.Length; offset += 64)
        {
            builder.AppendLine(base64.Substring(offset, Math.Min(64, base64.Length - offset)));
        }

        builder.AppendLine("-----END CERTIFICATE-----");
        return builder.ToString();
    }

    public static string TempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "jaytwo-ergonomics-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
