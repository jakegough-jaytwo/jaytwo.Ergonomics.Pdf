using System;
using System.IO;
using System.Runtime.InteropServices;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Hard links for the alias tests. Neither net48 nor netstandard2.0 has a managed API for them.
internal static class FileLinks
{
    public static void CreateHardLink(string link, string existing)
    {
        bool created = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? CreateHardLinkW(link, existing, IntPtr.Zero)
            : Link(existing, link) == 0;
        if (!created)
        {
            throw new IOException("Could not create the hard link '" + link + "' (error " + Marshal.GetLastWin32Error() + ").");
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr securityAttributes);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string link);
}
