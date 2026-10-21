using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Poppler pdfsig, the second PDF-aware signature verifier for gate G4 (W-14). It verifies the CMS signature over the
// /ByteRange bytes, says whether that range reaches the end of the file, and validates the signer certificate
// against an NSS database that holds only the roots a test passes. It has no modification analysis, does not check
// an embedded RFC 3161 token, and does not verify an /ETSI.RFC3161 document timestamp.
internal static class Pdfsig
{
    public const string Valid = "Signature Validation: Signature is Valid.";
    public const string DigestMismatch = "Signature Validation: Digest Mismatch.";
    public const string Trusted = "Certificate Validation: Certificate is Trusted.";
    public const string Untrusted = "Certificate Validation: Certificate issuer isn't Trusted.";
    public const string WholeFile = "Total document signed";
    public const string NotWholeFile = "Not total document signed";

    public static string Locate()
    {
        return ExternalTool.Locate("JAYTWO_PDFSIG", ExternalTool.Executable("pdfsig"));
    }

    // Runs pdfsig with certificate validation against a fresh NSS database that trusts only the given roots for
    // signing, with OCSP off. The exit code covers the signature check only: it stays 0 for an untrusted signer.
    public static (int ExitCode, string Report) Verify(byte[] pdf, IReadOnlyList<byte[]> trustedRoots, string? password)
    {
        string pdfsig = Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            string certutil = LocateCertutil(pdfsig);
            string database = Path.Combine(directory, "nssdb");
            Directory.CreateDirectory(database);
            string nssdir = "sql:" + database;
            Checked(certutil, "-N", "-d", nssdir, "--empty-password");
            for (int i = 0; i < trustedRoots.Count; i++)
            {
                string rootPath = Path.Combine(directory, "root" + i.ToString(CultureInfo.InvariantCulture) + ".pem");
                File.WriteAllText(rootPath, ExternalTool.Pem(trustedRoots[i]));

                // The trust flags are SSL, S/MIME, and code signing. pdfsig validates the signer for S/MIME signing.
                Checked(certutil, "-A", "-d", nssdir, "-n", "root" + i.ToString(CultureInfo.InvariantCulture), "-t", ",C,", "-i", rootPath);
            }

            string pdfPath = Path.Combine(directory, "signed.pdf");
            File.WriteAllBytes(pdfPath, pdf);
            var args = new List<string> { "-nssdir", nssdir, "-no-ocsp" };
            if (password != null)
            {
                args.Add("-upw");
                args.Add(password);
            }

            args.Add(pdfPath);
            using Process process = ExternalTool.Start(pdfsig, args.ToArray());
            (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
            Assert.True(stdout.Contains("Signature #1:"), "pdfsig exited " + exitCode.ToString(CultureInfo.InvariantCulture) + Environment.NewLine + stderr + stdout);
            return (exitCode, stdout);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // One list of report lines per signature, with the "  - " prefix removed.
    public static List<List<string>> Signatures(string report)
    {
        var signatures = new List<List<string>>();
        foreach (string raw in report.Replace("\r", string.Empty).Split('\n'))
        {
            if (raw.StartsWith("Signature #", StringComparison.Ordinal))
            {
                signatures.Add(new List<string>());
            }
            else if (signatures.Count > 0 && raw.StartsWith("  - ", StringComparison.Ordinal))
            {
                signatures[signatures.Count - 1].Add(raw.Substring(4));
            }
        }

        return signatures;
    }

    // The /ByteRange of the file's one signature as pdfsig prints it: each range as its first and end offsets.
    public static string SignedRanges(byte[] pdf)
    {
        MatchCollection matches = Regex.Matches(TestCompat.Latin1.GetString(pdf), @"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]");
        Assert.Single(matches);
        GroupCollection groups = matches[0].Groups;
        long start1 = long.Parse(groups[1].Value, CultureInfo.InvariantCulture);
        long length1 = long.Parse(groups[2].Value, CultureInfo.InvariantCulture);
        long start2 = long.Parse(groups[3].Value, CultureInfo.InvariantCulture);
        long length2 = long.Parse(groups[4].Value, CultureInfo.InvariantCulture);
        return string.Format(CultureInfo.InvariantCulture, "Signed Ranges: [{0} - {1}], [{2} - {3}]", start1, start1 + length1, start2, start2 + length2);
    }

    // NSS certutil sits beside pdfsig in the MSYS2 build. On Windows a PATH lookup would find the unrelated
    // system certutil.exe, so PATH is searched on other platforms only.
    private static string LocateCertutil(string pdfsig)
    {
        string? configured = Environment.GetEnvironmentVariable("JAYTWO_CERTUTIL");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        string certutil = ExternalTool.Executable("certutil");
        string sibling = Path.Combine(Path.GetDirectoryName(pdfsig) ?? ".", certutil);
        if (File.Exists(sibling))
        {
            return sibling;
        }

        string? found = ExternalTool.IsWindows ? null : ExternalTool.OnPath(certutil);
        Assert.True(found != null, "NSS " + certutil + " was not found beside " + pdfsig + ". Set JAYTWO_CERTUTIL or put it on PATH.");
        return found!;
    }

    private static void Checked(string fileName, params string[] args)
    {
        using Process process = ExternalTool.Start(fileName, args);
        ExternalTool.Output(process);
    }
}
