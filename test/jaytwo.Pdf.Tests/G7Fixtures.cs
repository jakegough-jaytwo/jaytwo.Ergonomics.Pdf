using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace jaytwo.Pdf.Tests;

// The QUALIFICATION.md G7 fixtures. LargeWorkloadTests and the W-111 iText comparison write the same bytes from
// these generators.
internal static class G7Fixtures
{
    public const int DensePages = 64;
    private const int ImageWidth = 2048;
    private const int ImageHeight = 2731;

    private static readonly string[] Customers = { "Acme Widgets Ltd", "Globex Corporation", "Initech", "Umbrella Supply Co", "Hooli Services", "Stark Industrial", "Wayne Logistics", "Tyrell Parts" };

    // CSV invoice rows whose fields come from SplitMix64 with a fixed seed: every run writes the same bytes,
    // and the repeated layout with random digits gives Flate real work at a realistic ratio.
    public static void WriteRecords(Stream stream, long count, ulong seed)
    {
        var buffer = new byte[1024 * 1024];
        int used = 0;
        ulong state = seed;
        long row = 0;
        long remaining = count;
        while (remaining > 0)
        {
            ulong a = NextRandom(ref state);
            ulong b = NextRandom(ref state);
            row++;
            string line = row.ToString("D10", CultureInfo.InvariantCulture)
                + ",INV-" + ((uint)a).ToString("X8", CultureInfo.InvariantCulture)
                + ",2026-" + (1 + ((a >> 32) % 12)).ToString("D2", CultureInfo.InvariantCulture)
                + "-" + (1 + ((a >> 40) % 28)).ToString("D2", CultureInfo.InvariantCulture)
                + "," + ((b % 10000000) / 100).ToString(CultureInfo.InvariantCulture)
                + "." + (b % 100).ToString("D2", CultureInfo.InvariantCulture)
                + "," + Customers[(int)(b >> 56) % Customers.Length] + "\n";
            int length = (int)Math.Min(line.Length, remaining);
            if (used + length > buffer.Length)
            {
                stream.Write(buffer, 0, used);
                used = 0;
            }

            used += Encoding.ASCII.GetBytes(line, 0, length, buffer, used);
            remaining -= length;
        }

        stream.Write(buffer, 0, used);
    }

    // 64 pages, each drawing one unfiltered 2048 x 2731 RGB image of random bytes (16,779,264 bytes),
    // with one text field per page. Classic xref. About 1.0001 GiB.
    public static void WriteDense(string path)
    {
        int size = 10 + (DensePages * 4);
        using var writer = new FixtureWriter(path, size);
        var fields = new StringBuilder();
        var kids = new StringBuilder();
        for (int i = 0; i < DensePages; i++)
        {
            fields.Append(Id(13 + (i * 4))).Append(' ');
            kids.Append(Id(10 + (i * 4))).Append(' ');
        }

        writer.Object(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [" + fields + "] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> >>");
        writer.Object(2, "<< /Type /Pages /Count " + DensePages.ToString(CultureInfo.InvariantCulture) + " /Kids [" + kids + "] >>");
        writer.Object(3, "<< /Title (Dense GiB) /Author (QA) /Producer (LargeWorkloadTests) >>");
        writer.Object(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        ulong state = 0x5EED1610UL;
        var buffer = new byte[1024 * 1024];
        long imageLength = (long)ImageWidth * ImageHeight * 3;
        for (int i = 0; i < DensePages; i++)
        {
            int page = 10 + (i * 4);
            string number = (i + 1).ToString(CultureInfo.InvariantCulture);
            writer.Object(page, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im0 " + Id(page + 2) + " >> /Font << /Helv 4 0 R >> >> /Contents " + Id(page + 1) + " /Annots [" + Id(page + 3) + "] >>");
            writer.Stream(page + 1, string.Empty, Encoding.ASCII.GetBytes("q 540 0 0 700 36 36 cm /Im0 Do Q\n"));
            writer.BeginStream(page + 2, "/Type /XObject /Subtype /Image /Width " + ImageWidth.ToString(CultureInfo.InvariantCulture) + " /Height " + ImageHeight.ToString(CultureInfo.InvariantCulture) + " /ColorSpace /DeviceRGB /BitsPerComponent 8", imageLength);
            long remaining = imageLength;
            while (remaining > 0)
            {
                int chunk = (int)Math.Min(buffer.Length, remaining);
                state = FillRandom(buffer, chunk, state);
                writer.Raw(buffer, chunk);
                remaining -= chunk;
            }

            writer.EndStream();
            writer.Object(page + 3, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (field" + number + ") /V (value " + number + ") /Rect [36 750 300 774] /P " + Id(page) + " /DA (/Helv 10 Tf 0 g) /F 4 >>");
        }

        writer.Finish("/Root 1 0 R /Info 3 0 R /ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>]");
    }

    // Pages under ten intermediate page-tree nodes. Each page has a Flate content stream of text and a
    // parent field with a text kid and a check-box kid (shared on and off appearances). Classic xref; the
    // test then has qpdf rewrite it with object streams and a cross-reference stream.
    public static void WriteForm(string path, int pages, string title)
    {
        const int nodes = 10;
        int perNode = pages / nodes;
        int size = 100 + (pages * 5);
        using var writer = new FixtureWriter(path, size);
        var fields = new StringBuilder();
        var nodeKids = new StringBuilder();
        for (int i = 0; i < pages; i++)
        {
            fields.Append(Id(100 + (i * 5) + 2)).Append(' ');
        }

        for (int n = 0; n < nodes; n++)
        {
            nodeKids.Append(Id(10 + n)).Append(' ');
        }

        writer.Object(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [" + fields + "] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> >>");
        writer.Object(2, "<< /Type /Pages /Count " + pages.ToString(CultureInfo.InvariantCulture) + " /Kids [" + nodeKids + "] >>");
        writer.Object(3, "<< /Title (" + title + ") /Author (QA) /Producer (LargeWorkloadTests) >>");
        writer.Object(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        writer.Stream(5, "/Type /XObject /Subtype /Form /BBox [0 0 14 14]", Encoding.ASCII.GetBytes("q 0 g 3 3 8 8 re f Q\n"));
        writer.Stream(6, "/Type /XObject /Subtype /Form /BBox [0 0 14 14]", Encoding.ASCII.GetBytes("q 0 G 0.5 0.5 13 13 re S Q\n"));
        for (int n = 0; n < nodes; n++)
        {
            var kids = new StringBuilder();
            for (int k = 0; k < perNode; k++)
            {
                kids.Append(Id(100 + (((n * perNode) + k) * 5))).Append(' ');
            }

            writer.Object(10 + n, "<< /Type /Pages /Parent 2 0 R /Count " + perNode.ToString(CultureInfo.InvariantCulture) + " /Kids [" + kids + "] >>");
        }

        for (int i = 0; i < pages; i++)
        {
            int page = 100 + (i * 5);
            string number = (i + 1).ToString(CultureInfo.InvariantCulture);
            string state = (i + 1) % 2 == 0 ? "/Yes" : "/Off";
            writer.Object(page, "<< /Type /Page /Parent " + Id(10 + (i / perNode)) + " /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents " + Id(page + 1) + " /Annots [" + Id(page + 3) + " " + Id(page + 4) + "] >>");
            var content = new StringBuilder();
            for (int line = 0; line < 45; line++)
            {
                content.Append("BT /F1 10 Tf 72 ").Append((660 - (line * 14)).ToString(CultureInfo.InvariantCulture)).Append(" Td (Page ").Append(number).Append(" line ").Append(line.ToString(CultureInfo.InvariantCulture)).Append(": the quick brown fox jumps over the lazy dog 0123456789) Tj ET\n");
            }

            writer.Stream(page + 1, "/Filter /FlateDecode", TestCompat.Zlib(Encoding.ASCII.GetBytes(content.ToString())));
            writer.Object(page + 2, "<< /T (page" + number + ") /Kids [" + Id(page + 3) + " " + Id(page + 4) + "] >>");
            writer.Object(page + 3, "<< /Type /Annot /Subtype /Widget /Parent " + Id(page + 2) + " /FT /Tx /T (name) /V (Name " + number + ") /Rect [72 720 300 744] /P " + Id(page) + " /DA (/Helv 10 Tf 0 g) /F 4 >>");
            writer.Object(page + 4, "<< /Type /Annot /Subtype /Widget /Parent " + Id(page + 2) + " /FT /Btn /T (agree) /V " + state + " /AS " + state + " /AP << /N << /Yes 5 0 R /Off 6 0 R >> >> /Rect [72 690 86 704] /P " + Id(page) + " /F 4 >>");
        }

        writer.Finish("/Root 1 0 R /Info 3 0 R");
    }

    // qpdf rewrites the classic fixture with object streams and a cross-reference stream, with an /ID computed from
    // the content, so every run writes the same bytes.
    public static string ObjectStreamCopy(string classicPath, string path)
    {
        string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
        using (Process process = ExternalTool.Start(qpdf, "--object-streams=generate", "--deterministic-id", classicPath, path))
        {
            ExternalTool.Output(process);
        }

        string layout = TestCompat.Latin1.GetString(File.ReadAllBytes(path));
        Assert.Contains("/ObjStm", layout, StringComparison.Ordinal);
        Assert.Contains("/XRef", layout, StringComparison.Ordinal);
        return qpdf;
    }

    // The text field of every page of a WriteForm fixture.
    public static Dictionary<string, PdfFieldValue> TextValues(int pages)
    {
        var values = new Dictionary<string, PdfFieldValue>();
        for (int page = 1; page <= pages; page++)
        {
            string number = page.ToString(CultureInfo.InvariantCulture);
            values["page" + number + ".name"] = "Filled " + number;
        }

        return values;
    }

    private static ulong NextRandom(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    // SplitMix64 with a fixed seed, so every run writes the same incompressible bytes.
    private static ulong FillRandom(byte[] buffer, int count, ulong state)
    {
        unchecked
        {
            for (int i = 0; i < count; i += 8)
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                int bytes = Math.Min(8, count - i);
                for (int b = 0; b < bytes; b++)
                {
                    buffer[i + b] = (byte)(z >> (8 * b));
                }
            }
        }

        return state;
    }

    private static string Id(int id)
    {
        return id.ToString(CultureInfo.InvariantCulture) + " 0 R";
    }

    private sealed class FixtureWriter : IDisposable
    {
        private readonly FileStream _stream;
        private readonly long[] _offsets;

        public FixtureWriter(string path, int size)
        {
            _stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024);
            _offsets = new long[size];
            Ascii("%PDF-1.7\n");
            byte[] binary = { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' };
            _stream.Write(binary, 0, binary.Length);
        }

        public void Object(int id, string body)
        {
            _offsets[id] = _stream.Position;
            Ascii(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n" + body + "\nendobj\n");
        }

        public void Stream(int id, string dictionary, byte[] data)
        {
            BeginStream(id, dictionary, data.Length);
            Raw(data, data.Length);
            EndStream();
        }

        public void BeginStream(int id, string dictionary, long length)
        {
            _offsets[id] = _stream.Position;
            Ascii(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n<< " + dictionary + " /Length " + length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
        }

        public void Raw(byte[] data, int count)
        {
            _stream.Write(data, 0, count);
        }

        public void EndStream()
        {
            Ascii("\nendstream\nendobj\n");
        }

        public void Finish(string trailer)
        {
            long xref = _stream.Position;
            var table = new StringBuilder();
            table.Append("xref\n0 ").Append(_offsets.Length.ToString(CultureInfo.InvariantCulture)).Append('\n');
            table.Append("0000000000 65535 f\r\n");
            for (int id = 1; id < _offsets.Length; id++)
            {
                if (_offsets[id] == 0)
                {
                    table.Append("0000000000 65535 f\r\n");
                }
                else
                {
                    table.Append(_offsets[id].ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n\r\n");
                }
            }

            table.Append("trailer\n<< /Size ").Append(_offsets.Length.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(trailer).Append(" >>\nstartxref\n");
            table.Append(xref.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
            Ascii(table.ToString());
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private void Ascii(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            _stream.Write(bytes, 0, bytes.Length);
        }
    }
}
