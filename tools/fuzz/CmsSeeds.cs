using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using jaytwo.Pdf;
using jaytwo.Pdf.Signatures;
using jaytwo.Pdf.Tests;

namespace jaytwo.Ergonomics.Pdf.Fuzz;

// Parser seeds, not signature-validation evidence. Keep controlled rejections as well as accepted structures.
internal static class CmsSeeds
{
    public static IReadOnlyList<byte[]> Build(string root)
    {
        var seeds = new List<byte[]>();
        string fixtures = Path.Combine(root, "test", "jaytwo.Pdf.Tests", "Fixtures");
        foreach (string path in Directory.GetFiles(fixtures, "*.pdf", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            seeds.AddRange(Contents(File.ReadAllBytes(path)));
        }

        // Fail rather than silently losing the real BER/foreign-signer coverage when fixture discovery changes.
        foreach (string file in new[] { "gpo-bills-118hr815enr.pdf", "cms-40b-pyhanko-approved.pdf", "opm-of306-pyhanko-certified.pdf" })
        {
            byte[] cms = Contents(File.ReadAllBytes(Path.Combine(fixtures, "corpus", file))).First();
            CmsSignatureReader.Read(cms, false, 0);
            if (file.StartsWith("gpo-", StringComparison.Ordinal) && (cms[0] != 0x30 || cms[1] != 0x80))
            {
                throw new InvalidOperationException("The GPO indefinite-length BER CMS seed is missing.");
            }
        }

        string external = Path.Combine(root, "tools", "fuzz", "seeds", "cms");
        byte[] basic = File.ReadAllBytes(Path.Combine(external, "library-basic.cms"));
        seeds.Add(basic);
        seeds.Add(File.ReadAllBytes(Path.Combine(external, "library-pades.cms")));
        byte[] der = DerCodec.ReadTlv(basic).Full;
        byte[] ber = Indefinite(der, 0);
        seeds.Add(ber);
        seeds.Add(ber[..^1]);
        seeds.Add(der[..^1]);
        seeds.Add(new byte[] { 0x30, 0x85, 0x01, 0, 0, 0, 0 });
        seeds.Add(new byte[] { 0x04, 0x80, 0, 0 });
        seeds.Add(Enumerable.Repeat(new byte[] { 0x30, 0x80 }, 65).SelectMany(bytes => bytes).Concat(new byte[130]).ToArray());

        byte[] certificate = CmsSignatureReader.Read(basic, false, 0).SignerCertificate!;
        (byte[] issuer, byte[] serial) = DerCodec.ReadCertificateIdentity(certificate);
        byte[] identifier = DerCodec.Sequence(issuer, serial);
        seeds.Add(Minimal(identifier, certificate, 0));
        seeds.Add(Minimal(identifier, certificate, 2));
        seeds.Add(Minimal(DerCodec.OctetString(new byte[] { 1 }), certificate, 1));
        byte[] time = DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.9.5"), DerCodec.Set(DerCodec.UtcTime(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero))));
        seeds.Add(Minimal(identifier, certificate, 1, DerCodec.ReplaceTag(DerCodec.Set(time, time), 0xA0)));
        seeds.Add(Minimal(identifier, certificate, 1, contentType: "1.2.3.4"));
        seeds.Add(Minimal(identifier, certificate, 1, algorithm: "1.2.3.4"));
        seeds.Add(DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.7.1"), DerCodec.ExplicitContext(0, DerCodec.OctetString(new byte[] { 1 }))));

        // OpenSSL-produced timestamp and SKI seeds are committed for tool-free, deterministic CI smoke coverage.
        foreach (string name in new[] { "document-timestamp.cms", "signature-timestamp.cms", "subject-key-identifier.cms" })
        {
            seeds.Add(File.ReadAllBytes(Path.Combine(external, name)));
        }

        if (seeds.Any(seed => seed.Length > CmsFuzzTarget.MaxLength))
        {
            throw new InvalidOperationException("A CMS seed exceeds the fragment limit.");
        }

        foreach (bool timestamp in new[] { false, true })
        {
            int accepted = 0;
            int rejected = 0;
            foreach (byte[] seed in seeds)
            {
                try
                {
                    CmsSignatureReader.Read(seed, timestamp, 0);
                    accepted++;
                }
                catch (PdfException)
                {
                    rejected++;
                }
            }

            Console.WriteLine($"cms seeds ({(timestamp ? "timestamp" : "ordinary")}): {accepted} accepted, {rejected} controlled rejections");
        }

        return seeds;
    }

    public static void WriteGenerated(string directory)
    {
        Directory.CreateDirectory(directory);
        SeedSigner signer = SeedSigner.Create();
        File.WriteAllBytes(Path.Combine(directory, "library-basic.cms"), DerCodec.ReadTlv(Generated(signer, PdfSignatureFormat.AdbePkcs7Detached)).Full);
        File.WriteAllBytes(Path.Combine(directory, "library-pades.cms"), DerCodec.ReadTlv(Generated(signer, PdfSignatureFormat.PadesBaselineB)).Full);
    }

    internal static IEnumerable<byte[]> Contents(byte[] pdf)
    {
        string text = Encoding.Latin1.GetString(pdf);
        if (!text.Contains("/ByteRange", StringComparison.Ordinal))
        {
            yield break;
        }

        // All registered signed fixtures use hex strings. Collect every signature, including prior revisions,
        // and keep zero padding: the inspection reader explicitly accepts it after either DER or BER.
        foreach (Match match in Regex.Matches(text, @"/Contents[\x00\t\n\f\r ]*<([0-9a-fA-F\x00\t\n\f\r ]*)>"))
        {
            string hex = Regex.Replace(match.Groups[1].Value, @"[\x00\t\n\f\r ]", string.Empty);
            if ((hex.Length & 1) != 0)
            {
                hex += "0";
            }

            yield return Convert.FromHexString(hex);
        }
    }

    private static byte[] Generated(SeedSigner signer, PdfSignatureFormat format)
    {
        using var document = PdfDocument.Open(new MemoryStream(PdfBuilder.Basic().Finish(), false));
        using var editor = document.Edit();
        using var output = new MemoryStream();
        editor.Sign(output, new PdfSignatureOptions { Format = format, SigningTime = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero) }, new CmsDetachedSignatureProvider(signer), leaveOpen: true);
        return Contents(output.ToArray()).Single();
    }

    private static byte[] Minimal(byte[] identifier, byte[] certificate, int count, byte[]? attributes = null, string contentType = "1.2.840.113549.1.7.1", string algorithm = "1.2.840.113549.1.1.11")
    {
        byte[] signer = DerCodec.Sequence(DerCodec.Integer(1), identifier, DerCodec.AlgorithmSha256(), attributes ?? Array.Empty<byte>(), DerCodec.Sequence(DerCodec.Oid(algorithm)), DerCodec.OctetString(new byte[] { 1 }));
        byte[] signed = DerCodec.Sequence(DerCodec.Integer(1), DerCodec.Set(DerCodec.AlgorithmSha256()), DerCodec.Sequence(DerCodec.Oid(contentType)), DerCodec.ReplaceTag(DerCodec.Set(certificate), 0xA0), DerCodec.Set(Enumerable.Repeat(signer, count).ToArray()));
        return DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.7.2"), DerCodec.ExplicitContext(0, signed));
    }

    private static byte[] Indefinite(byte[] data, int depth)
    {
        DerCodec.DerTlv node = DerCodec.ReadTlv(data);
        // Only ContentInfo, its wrapper, and SignedData change encoding. Signed attributes stay DER.
        if ((node.Tag & 0x20) == 0 || depth > 2)
        {
            return node.Full;
        }

        var output = new List<byte> { node.Tag, 0x80 };
        int offset = 0;
        while (offset < node.Content.Length)
        {
            DerCodec.DerTlv child = DerCodec.ReadTlv(node.Content, offset);
            output.AddRange(Indefinite(child.Full, depth + 1));
            offset += child.Full.Length;
        }

        output.AddRange(new byte[2]);
        return output.ToArray();
    }
}
