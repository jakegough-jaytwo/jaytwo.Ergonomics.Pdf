using System;

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
}
