using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

internal static class ExternalTool
{
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
        string stdout = where.StandardOutput.ReadToEnd();
        where.StandardError.ReadToEnd();
        where.WaitForExit();
        string[] lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(where.ExitCode == 0 && lines.Length > 0, fileName + " was not found. Set " + variable + " or put it on PATH.");
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
            info.ArgumentList.Add(args[i]);
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
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StartInfo.FileName + " exited " + process.ExitCode.ToString() + Environment.NewLine + stderr + stdout);
        return stdout;
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
