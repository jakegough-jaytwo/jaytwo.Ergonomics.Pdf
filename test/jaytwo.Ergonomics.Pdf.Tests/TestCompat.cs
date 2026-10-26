using System;
using System.Diagnostics;
using System.Text;

namespace jaytwo.Pdf.Tests;

// The one member the linked PdfBuilder needs from the engine tests' TestCompat, which this project cannot link: on net48
// it uses jaytwo.Pdf internals, and these tests use only the public API (D-54). PdfBuilder calls it only to write a
// cross-reference stream.
internal static class TestCompat
{
    public static byte[] Zlib(byte[] data)
    {
        throw new NotSupportedException("The workflow tests build classic cross-reference tables only.");
    }

    public static void AddArgument(ProcessStartInfo info, string argument)
    {
#if NETFRAMEWORK
        string quoted = QuoteArgument(argument);
        info.Arguments = info.Arguments.Length == 0 ? quoted : info.Arguments + " " + quoted;
#else
        info.ArgumentList.Add(argument);
#endif
    }

#if NETFRAMEWORK
    public static void Kill(this Process process, bool entireProcessTree)
    {
        if (entireProcessTree)
        {
            var info = new ProcessStartInfo("taskkill.exe", "/PID " + process.Id.ToString() + " /T /F")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process? taskkill = Process.Start(info);
            taskkill?.WaitForExit();
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill.
        }
    }

    // The quoting rules of CommandLineToArgvW, as .NET applies them for ProcessStartInfo.ArgumentList.
    private static string QuoteArgument(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
        {
            return argument;
        }

        var builder = new StringBuilder("\"");
        int backslashes = 0;
        foreach (char c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            builder.Append('\\', c == '"' ? (backslashes * 2) + 1 : backslashes);
            builder.Append(c);
            backslashes = 0;
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
#endif
}
