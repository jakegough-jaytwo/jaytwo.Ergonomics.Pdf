using System;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;

namespace jaytwo.Ergonomics.Pdf.Fuzz;

// Each parse mode owns its expected failures. A malformed ordinary signature must still reach token inspection.
internal static class CmsFuzzTarget
{
    public const int MaxLength = 64 * 1024;

    public static void Run(byte[] data)
    {
        Run(data, (contents, timestamp) => CmsSignatureReader.Read(contents, timestamp, 0));
    }

    internal static void Run(byte[] data, Action<byte[], bool> read)
    {
        if (data.Length > MaxLength)
        {
            return;
        }

        foreach (bool timestamp in new[] { false, true })
        {
            try
            {
                read(data, timestamp);
            }
            catch (PdfException)
            {
            }
        }
    }
}
