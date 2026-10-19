using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using jaytwo.Ergonomics.Pdf.Tests;

namespace jaytwo.Ergonomics.Pdf.Fuzz;

/// <summary>
/// Builds the deterministic in-process seed corpus: repository fixtures plus builder variants for each structure the
/// targets read. External seeds (qpdf encryption variants and OpenSSL timestamp responses) come from
/// <c>external-seeds.sh</c>.
/// </summary>
internal static class SeedWriter
{
    public static Dictionary<string, List<byte[]>> Build(string repositoryRoot)
    {
        var seeds = new Dictionary<string, List<byte[]>>(StringComparer.Ordinal);
        foreach (FuzzTarget target in FuzzTargets.All)
        {
            seeds[target.Name] = new List<byte[]>();
        }

        var documents = new List<byte[]>();
        string fixtures = Path.Combine(repositoryRoot, "test", "jaytwo.Ergonomics.Pdf.Tests", "Fixtures");
        foreach (string path in Sorted(Directory.GetFiles(fixtures, "*.pdf")))
        {
            documents.Add(File.ReadAllBytes(path));
        }

        documents.AddRange(BuilderDocuments());
        seeds["open"].AddRange(documents);
        seeds["edit"].AddRange(documents);

        seeds["object"].AddRange(Ascii(
            "<< /Type /Page /MediaBox [0 0 612 792] /Kids [1 0 R 2 0 R] /Rotate -90 >>",
            "(Literal \\(nested\\) \\101 \\r\\n string)",
            "<FEFF00410042>",
            "[1 -2 +3 4.5 -.25 /Name#20With#23Escapes true false null 12 0 R]",
            "<< /A << /B << /C [[[]]] >> >> >>",
            "% comment\n<< /K (v) >>"));

        seeds["objstm"].AddRange(Ascii(
            "2 12\n4 0 5 15 << /A 1 >> << /B [2 3] >>",
            "1 4\n7 0 (string)",
            "0 0\n"));

        seeds["decode"].AddRange(DecodeSeeds());
        seeds["xmp"].AddRange(Ascii(
            "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
                + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">T</rdf:li></rdf:Alt></dc:title>"
                + "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>",
            "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>",
            "<!DOCTYPE x [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;\">]><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">&b;</x:xmpmeta>"));

        foreach (byte[] document in documents)
        {
            byte[]? contents = SignatureContents(document);
            if (contents != null)
            {
                seeds["der"].Add(contents);
                seeds["tsr"].Add(TsrInput(new byte[32], Array.Empty<byte>(), 1, contents));
            }
        }

        seeds["der"].Add(new byte[] { 0x30, 0x03, 0x02, 0x01, 0x05 });
        seeds["tsr"].Add(TsrInput(new byte[32], new byte[] { 1 }, 0, new byte[] { 0x30, 0x05, 0x30, 0x03, 0x02, 0x01, 0x00 }));
        return seeds;
    }

    public static void Write(Dictionary<string, List<byte[]>> seeds, string directory)
    {
        foreach (KeyValuePair<string, List<byte[]>> pair in seeds)
        {
            string targetDirectory = Path.Combine(directory, pair.Key);
            Directory.CreateDirectory(targetDirectory);
            foreach (byte[] seed in pair.Value)
            {
                File.WriteAllBytes(Path.Combine(targetDirectory, Hash(seed)), seed);
            }
        }
    }

    public static string Hash(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    public static string[] Sorted(string[] paths)
    {
        Array.Sort(paths, StringComparer.Ordinal);
        return paths;
    }

    /// <summary>
    /// Formats a <c>tsr</c> target input. See <c>FuzzTargets.TimestampResponse</c>.
    /// </summary>
    public static byte[] TsrInput(byte[] imprint, byte[] nonce, byte mode, byte[] body)
    {
        var output = new MemoryStream();
        output.Write(imprint, 0, 32);
        output.WriteByte((byte)nonce.Length);
        output.Write(nonce, 0, nonce.Length);
        output.WriteByte(mode);
        output.Write(body, 0, body.Length);
        return output.ToArray();
    }

    private static IEnumerable<byte[]> BuilderDocuments()
    {
        yield return PdfBuilder.Basic().Finish();

        var preamble = new PdfBuilder(preamble: "junk before the header\n", version: "1.4");
        preamble.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Version /1.7 >>");
        preamble.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        preamble.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /CropBox [10 10 600 780] /Rotate 90 >>");
        yield return preamble.Finish(crlfEntries: false);

        PdfBuilder form = PdfBuilder.Basic(
            " /AcroForm << /Fields [4 0 R 5 0 R 6 0 R 9 0 R 10 0 R] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 11 0 R >> >> >>",
            " /Annots [4 0 R 5 0 R 7 0 R 8 0 R 9 0 R 10 0 R]");
        form.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /V (Old) /DA (/Helv 12 Tf 0 g) /Rect [50 700 250 720] /P 3 0 R /MaxLen 40 >>");
        form.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /V /Off /AS /Off /Rect [50 650 70 670] /P 3 0 R /AP << /N << /Yes 12 0 R /Off 12 0 R >> >> >>");
        form.Indirect(6, "<< /FT /Btn /Ff 49152 /T (Choice) /V /Off /Kids [7 0 R 8 0 R] >>");
        form.Indirect(7, "<< /Type /Annot /Subtype /Widget /Parent 6 0 R /Rect [50 600 70 620] /AS /Off /AP << /N << /A 12 0 R /Off 12 0 R >> >> >>");
        form.Indirect(8, "<< /Type /Annot /Subtype /Widget /Parent 6 0 R /Rect [80 600 100 620] /AS /Off /AP << /N << /B 12 0 R /Off 12 0 R >> >> >>");
        form.Indirect(9, "<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 131072 /T (Pick) /Opt [[(r) (Red)] [(g) (Green)]] /V (r) /I [0] /Rect [50 550 250 570] /P 3 0 R >>");
        form.Indirect(10, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [300 50 500 100] /P 3 0 R >>");
        form.Indirect(11, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        form.StreamObject(12, "/Type /XObject /Subtype /Form /BBox [0 0 20 20]", Encoding.ASCII.GetBytes("0 g 0 0 20 20 re f"));
        yield return form.Finish();

        PdfBuilder metadata = PdfBuilder.Basic(" /Metadata 5 0 R /Names << /EmbeddedFiles << /Names [(a.txt) 6 0 R (b.bin) 8 0 R] >> >>");
        metadata.Indirect(4, "<< /Title (Info title) /Author <FEFF0041> /CreationDate (D:20260101120000+01'00') /Custom (x) >>");
        metadata.StreamObject(5, "/Type /Metadata /Subtype /XML", Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>"));
        metadata.Indirect(6, "<< /Type /Filespec /F (a.txt) /UF (a.txt) /Desc (first) /EF << /F 7 0 R >> >>");
        metadata.StreamObject(7, "/Type /EmbeddedFile /Subtype /text#2Fplain /Filter /FlateDecode /Params << /Size 11 >>", Zlib(Encoding.ASCII.GetBytes("hello world")));
        metadata.Indirect(8, "<< /Type /Filespec /F (b.bin) /EF << /F 9 0 R >> >>");
        metadata.StreamObject(9, "/Type /EmbeddedFile /Filter [/ASCIIHexDecode /RunLengthDecode]", Encoding.ASCII.GetBytes("02616263FD7880>"));
        yield return metadata.Finish("/Info 4 0 R /ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>]");

        PdfBuilder objectStream = PdfBuilder.Basic();
        objectStream.StreamObject(5, "/Type /ObjStm /N 2 /First 9", Encoding.ASCII.GetBytes("4 0 6 21 << /Title (FromStream) >> << /K 1 >>"));
        objectStream.Compressed(4, 5, 0);
        objectStream.Compressed(6, 5, 1);
        objectStream.AppendXrefStream(7, "/Info 4 0 R");
        yield return objectStream.Snapshot();

        PdfBuilder hybrid = PdfBuilder.Basic();
        hybrid.Indirect(4, "<< /Title (Stream) >>");
        long streamAt = hybrid.AppendXrefStream(5, "/Info 4 0 R");
        hybrid.Indirect(4, "<< /Title (Classic) >>");
        yield return hybrid.Finish("/Info 4 0 R /XRefStm " + streamAt.ToString(CultureInfo.InvariantCulture));

        PdfBuilder incremental = PdfBuilder.Basic();
        incremental.Indirect(4, "<< /Title (Base) >>");
        long baseXref = incremental.Length;
        incremental.Finish("/Info 4 0 R");
        incremental.Indirect(4, "<< /Title (Update) >>");
        long updateXref = incremental.Length;
        incremental.Ascii("xref\n4 1\n" + incremental.OffsetOf(4).ToString("D10", CultureInfo.InvariantCulture) + " 00000 n\r\n"
            + "trailer\n<< /Size 5 /Root 1 0 R /Info 4 0 R /Prev " + baseXref.ToString(CultureInfo.InvariantCulture)
            + " >>\nstartxref\n" + updateXref.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        yield return incremental.Snapshot();
    }

    private static IEnumerable<byte[]> DecodeSeeds()
    {
        byte[] plain = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog. 0123456789 0123456789");
        yield return Stream("<< /Filter /FlateDecode >>", Zlib(plain));
        yield return Stream("<< /Filter /Fl >>", Zlib(plain));
        yield return Stream("<< /Filter /ASCIIHexDecode >>", Encoding.ASCII.GetBytes(Convert.ToHexString(plain) + ">"));
        yield return Stream("<< /Filter /ASCII85Decode >>", Encoding.ASCII.GetBytes("<~87cURD]i,\"Ebo80~>"));
        yield return Stream("<< /Filter /RunLengthDecode >>", new byte[] { 2, (byte)'a', (byte)'b', (byte)'c', 253, (byte)'x', 128 });
        yield return Stream("<< /Filter /LZWDecode >>", new byte[] { 0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01 });
        yield return Stream("<< /Filter /LZWDecode /DecodeParms << /EarlyChange 0 >> >>", new byte[] { 0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01 });
        yield return Stream("<< /Filter [/ASCIIHexDecode /FlateDecode] >>", Encoding.ASCII.GetBytes(Convert.ToHexString(Zlib(plain)) + ">"));
        yield return Stream("<< /Filter /FlateDecode /DecodeParms << /Predictor 12 /Columns 4 >> >>", Zlib(new byte[] { 2, 1, 2, 3, 4, 2, 1, 1, 1, 1 }));
        yield return Stream("<< /Filter /FlateDecode /DecodeParms << /Predictor 2 /Colors 3 /BitsPerComponent 8 /Columns 2 >> >>", Zlib(new byte[] { 1, 2, 3, 1, 1, 1 }));
        yield return Stream("<< /Filter /FlateDecode /DecodeParms << /Predictor 15 /Colors 1 /BitsPerComponent 16 /Columns 2 >> >>", Zlib(new byte[] { 4, 0, 1, 0, 2 }));
    }

    private static byte[]? SignatureContents(byte[] document)
    {
        string text = Encoding.Latin1.GetString(document);
        int at = text.IndexOf("/Contents <", StringComparison.Ordinal);
        if (at < 0 || !text.Contains("/ByteRange", StringComparison.Ordinal))
        {
            return null;
        }

        int start = at + "/Contents <".Length;
        int end = text.IndexOf('>', start);
        string hex = text.Substring(start, end - start);
        if (hex.Length < 4 || hex.Length % 2 != 0)
        {
            return null;
        }

        // The placeholder is zero-padded; keep only the outer DER value.
        byte[] padded = Convert.FromHexString(hex);
        int lengthBytes = padded[1] & 0x7F;
        if ((padded[1] & 0x80) == 0 || lengthBytes > 4 || padded.Length < 2 + lengthBytes)
        {
            return null;
        }

        long length = 0;
        for (int i = 0; i < lengthBytes; i++)
        {
            length = (length << 8) | padded[2 + i];
        }

        long total = 2 + lengthBytes + length;
        return total > padded.Length ? null : padded.AsSpan(0, (int)total).ToArray();
    }

    private static byte[] Stream(string dictionary, byte[] data)
    {
        byte[] head = Encoding.ASCII.GetBytes(dictionary + "stream\n");
        byte[] output = new byte[head.Length + data.Length];
        Buffer.BlockCopy(head, 0, output, 0, head.Length);
        Buffer.BlockCopy(data, 0, output, head.Length, data.Length);
        return output;
    }

    private static IEnumerable<byte[]> Ascii(params string[] values)
    {
        foreach (string value in values)
        {
            yield return Encoding.UTF8.GetBytes(value);
        }
    }

    private static byte[] Zlib(byte[] data)
    {
        var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }
}
