using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Hard links for the alias tests. Neither net48 nor netstandard2.0 has a managed API for them.
internal static class FileLinks
{
    public static void CreateDirectoryLink(string link, string existing)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (Symlink(existing, link) != 0)
            {
                throw new IOException("Could not create the directory link (error " + Marshal.GetLastWin32Error() + ").");
            }

            return;
        }

        // A junction exercises a directory reparse point without Windows' symbolic-link privilege.
        Directory.CreateDirectory(link);
        using SafeFileHandle handle = CreateFileW(link, 0x40000000, 0, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        byte[] substitute = Encoding.Unicode.GetBytes("\\??\\" + existing);
        byte[] printable = Encoding.Unicode.GetBytes(existing);
        var data = new byte[16 + substitute.Length + 2 + printable.Length + 2];
        Buffer.BlockCopy(BitConverter.GetBytes(0xA0000003U), 0, data, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)(data.Length - 8)), 0, data, 4, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)substitute.Length), 0, data, 10, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)(substitute.Length + 2)), 0, data, 12, 2);
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)printable.Length), 0, data, 14, 2);
        Buffer.BlockCopy(substitute, 0, data, 16, substitute.Length);
        Buffer.BlockCopy(printable, 0, data, 18 + substitute.Length, printable.Length);
        if (handle.IsInvalid || !DeviceIoControl(handle, 0x900A4, data, data.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            throw new IOException("Could not create the junction (error " + Marshal.GetLastWin32Error() + ").");
        }
    }

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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint control, byte[] input, int inputSize, IntPtr output, int outputSize, out int returned, IntPtr overlapped);

    [DllImport("libc", EntryPoint = "symlink", SetLastError = true)]
    private static extern int Symlink(string existing, string link);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string link);
}
