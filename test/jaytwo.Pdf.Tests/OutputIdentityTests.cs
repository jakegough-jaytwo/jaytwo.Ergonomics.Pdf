using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-123 (D-52): interop tests run on net10.0 only. That evidence covers the other targets because the library appends
// the same revision on every target, and this test checks it there. Each scenario makes a fixed edit, and the appended
// revision's canonical text must equal Fixtures/output-identity/<scenario>.txt. The canonical text inflates FlateDecode
// streams, whose encoded bytes depend on the runtime's DeflateStream, and masks what changes between runs: /ModDate and
// the XMP modify and metadata dates, the signature /Contents, and, when a stream was inflated, the offsets. After an intended change to the
// written bytes, `make output-identity-record` rewrites the files from net10.0, and every other target must then agree.
public class OutputIdentityTests
{
    private static readonly DateTimeOffset FixedTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    public static TheoryData<string> Scenarios => new TheoryData<string>
    {
        "metadata",
        "fill",
        "flatten",
        "rotate",
        "attachment",
        "rc4-fill",
        "sign",
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void every_target_appends_the_same_revision(string scenario)
    {
        byte[] original = scenario == "rc4-fill" ? Rc4Form() : Form();
        byte[] saved = Write(scenario, original);
        Assert.True(saved.AsSpan(0, original.Length).SequenceEqual(original), scenario + ": the original is not a prefix");
        string actual = Canonical(saved, original.Length);

        if (Environment.GetEnvironmentVariable("JAYTWO_RECORD_OUTPUT_IDENTITY") == "1")
        {
            string path = SourcePath(scenario);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual, new UTF8Encoding(false));
            return;
        }

        string expected = File.ReadAllText(ExternalTool.Fixture(Path.Combine("output-identity", scenario + ".txt"))).Replace("\r\n", "\n");
        string[] expectedLines = expected.Split('\n');
        string[] actualLines = actual.Split('\n');
        for (int i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            string? want = i < expectedLines.Length ? expectedLines[i] : null;
            string? got = i < actualLines.Length ? actualLines[i] : null;
            Assert.True(
                want == got,
                scenario + " line " + (i + 1).ToString(CultureInfo.InvariantCulture) + " differs on this target.\nexpected: " + want + "\nactual:   " + got
                + "\nIf the change to the written bytes is intended, run make output-identity-record and review the diff.");
        }
    }

    private static byte[] Write(string scenario, byte[] original)
    {
        using var stream = new MemoryStream();
        stream.Write(original, 0, original.Length);
        using (var document = scenario == "rc4-fill" ? PdfDocument.Open(stream, "user", leaveOpen: true) : PdfDocument.Open(stream, leaveOpen: true))
        {
            switch (scenario)
            {
                case "metadata":
                    document.Metadata.Title = "Identité";
                    document.Metadata["Department"] = "Billing (East)";
                    break;
                case "fill":
                case "rc4-fill":
                    Fill(document);
                    break;
                case "flatten":
                    Fill(document);
                    document.AcroForm.Flatten();
                    break;
                case "rotate":
                    document.Pages[0].Rotation = 90;
                    break;
                case "attachment":
                    byte[] content = Enumerable.Range(0, 3000).Select(value => (byte)(value % 7 == 0 ? value : 0x41)).ToArray();
                    document.Attachments.Add("invoice.xml", new MemoryStream(content), options: new PdfAttachmentOptions
                    {
                        Relationship = PdfAttachmentRelationship.Alternative,
                        CreationDate = FixedTime,
                        ModificationDate = FixedTime,
                        Compress = true,
                        OpenAttachmentsPanel = true,
                    });
                    break;
                case "sign":
                    using (var key = RSA.Create(2048))
                    using (X509Certificate2 certificate = TestKeys.SelfSigned("CN=Output Identity", key))
                    {
                        var options = new PdfSignatureOptions
                        {
                            FieldName = "Approval",
                            Rect = new PdfRect(72, 72, 272, 122),
                            Reason = "Approved",
                            Location = "Desk 4",
                            SigningTime = FixedTime,
                        };
                        document.Sign(stream, options, new CmsDetachedSignatureProvider(new KeySigner(key, certificate.RawData)), leaveOpen: true);
                    }

                    return stream.ToArray();
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
            }

            document.SaveIncremental(stream);
        }

        return stream.ToArray();
    }

    private static void Fill(PdfDocument document)
    {
        Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Fit", "ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789"));
        Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Centered", "Café • (1/3)"));
        Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Choice", "Beta"));
        document.AcroForm.Fill("Agree", true);
    }

    // A text field sized to its content, a centered field with colors and a border, a combo box, and a check box,
    // with an /ID so the save does not draw a random one.
    private static byte[] Form()
    {
        var builder = new PdfBuilder();
        Objects(builder, (text, id) => "(" + text + ")");
        return builder.Finish("/ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>]");
    }

    // Revision 3 RC4 has no initialization vector, so the encrypted revision is the same on every run.
    private static byte[] Rc4Form()
    {
        var crypt = new StandardEncryptor(StandardEncryptor.Kind.Revision3, "user", "owner", -4, encryptMetadata: true, 128);
        var builder = new PdfBuilder();
        Objects(builder, crypt.Text);
        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    // text(value, objectNumber) writes a string in the object that holds it.
    private static void Objects(PdfBuilder builder, Func<string, int, string> text)
    {
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R 7 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R 6 0 R 7 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T " + text("Fit", 4) + " /Rect [40 700 180.5 724] /DA " + text("/Helv 0 Tf 0 g", 4) + " /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T " + text("Centered", 5) + " /Rect [220 700 410 724] /Q 1 /DA " + text("/Helv 11.5 Tf 0.2 0.4 0.6 rg", 5)
            + " /MK << /BG [0.9 0.9 0.75] /BC [0 0 1] >> /BS << /W 1.5 /S /S >> /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Ch /Ff 131072 /T " + text("Choice", 6) + " /Opt [" + text("Alpha", 6) + " " + text("Beta", 6) + "] /Rect [40 650 180 674] /DA "
            + text("/Helv 10 Tf 0 g", 6) + " /P 3 0 R >>");
        builder.Indirect(7, "<< /Subtype /Widget /FT /Btn /T " + text("Agree", 7) + " /Rect [220 650 240 670] /AS /Off /AP << /N << /Yes 8 0 R /Off 9 0 R >> >> /P 3 0 R >>");
        builder.StreamObject(8, "/Type /XObject /Subtype /Form /BBox [0 0 20 20]", Array.Empty<byte>());
        builder.StreamObject(9, "/Type /XObject /Subtype /Form /BBox [0 0 20 20]", Array.Empty<byte>());
    }

    private static string Canonical(byte[] saved, int start)
    {
        string text = TestCompat.Latin1.GetString(saved, start, saved.Length - start);
        bool inflated = false;
        text = Regex.Replace(
            text,
            @"(?s)(?<head>\d+ \d+ obj\s*<<(?<dict>(?:(?!endobj).)*?)>>\s*stream\r?\n)(?<data>.*?)(?<tail>\r?\nendstream)",
            match =>
            {
                string dict = match.Groups["dict"].Value;
                if (!dict.Contains("/FlateDecode"))
                {
                    return match.Value;
                }

                inflated = true;
                byte[] encoded = TestCompat.Latin1.GetBytes(match.Groups["data"].Value);
                using var deflate = new DeflateStream(new MemoryStream(encoded, 2, encoded.Length - 2), CompressionMode.Decompress);
                using var decoded = new MemoryStream();
                deflate.CopyTo(decoded);
                string head = Regex.Replace(match.Groups["head"].Value, @"/Length \d+", "/Length *");
                return head + "[inflated] " + TestCompat.Latin1.GetString(decoded.ToArray()) + match.Groups["tail"].Value;
            });
        if (inflated)
        {
            text = Regex.Replace(text, @"(?m)^\d{10}( \d{5} [nf])", "**********$1");
            text = Regex.Replace(text, @"startxref(\s+)\d+", "startxref$1*");
        }

        text = Regex.Replace(text, @"/ModDate\s*\((?:[^()\\]|\\.)*\)", "/ModDate (*)");
        text = Regex.Replace(text, @"<xmp:(?<name>ModifyDate|MetadataDate)>[^<]*</xmp:\k<name>>", "<xmp:${name}>*</xmp:${name}>");
        text = Regex.Replace(text, @"/Contents\s*<[0-9A-Fa-f]+>", "/Contents <*>");

        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c == '\n' || (c >= ' ' && c <= '~' && c != '\\'))
            {
                builder.Append(c);
            }
            else
            {
                builder.Append(c == '\\' ? "\\\\" : c == '\r' ? "\\r" : "\\x" + ((int)c).ToString("x2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static string SourcePath(string scenario, [CallerFilePath] string source = "")
    {
        return Path.Combine(Path.GetDirectoryName(source)!, "Fixtures", "output-identity", scenario + ".txt");
    }
}
