using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using jaytwo.Ergonomics.Pdf.AcroForm;
using jaytwo.Ergonomics.Pdf.Signatures;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Net.Pkcs11Interop.HighLevelAPI.Factories;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class SigningInteropTests
{
    [Fact]
    public void pyhanko_trusts_an_embedded_chain_from_the_root_alone()
    {
        using var rootKey = RSA.Create(2048);
        using var intermediateKey = RSA.Create(2048);
        using var leafKey = RSA.Create(2048);
        using X509Certificate2 root = CreateAuthority("Interop Root", rootKey, null);
        using X509Certificate2 intermediate = CreateAuthority("Interop Intermediate", intermediateKey, root);
        using X509Certificate2 leaf = CreateLeaf("Interop Leaf", leafKey, intermediate);
        byte[] signed = Sign(
            File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")),
            leaf,
            leafKey,
            new[] { intermediate.RawData, root.RawData },
            null);

        string report = Validate(signed, root.RawData, null, "entire", "chain");
        Assert.Contains("intact=true", report, StringComparison.Ordinal);
        Assert.Contains("trusted=true", report, StringComparison.Ordinal);
    }

    [Fact]
    public void pyhanko_trusts_the_rfc3161_token_embedded_in_the_cms_signature()
    {
        using var signerKey = RSA.Create(2048);
        using X509Certificate2 signer = CreateLeaf("Interop Signer", signerKey, null);
        using var tsaRootKey = RSA.Create(2048);
        using var tsaKey = RSA.Create(2048);
        using X509Certificate2 tsaRoot = CreateAuthority("Interop TSA Root", tsaRootKey, null);
        using X509Certificate2 tsa = CreateTimestampAuthority(tsaKey, tsaRoot);
        string directory = ExternalTool.TempDirectory();
        try
        {
            string certPath = Path.Combine(directory, "tsa.pem");
            string keyPath = Path.Combine(directory, "tsa.key");
            string rootPath = Path.Combine(directory, "tsa-root.pem");
            File.WriteAllText(certPath, ExternalTool.Pem(tsa.RawData));
            File.WriteAllText(keyPath, PemKey(tsaKey));
            File.WriteAllText(rootPath, ExternalTool.Pem(tsaRoot.RawData));
            using Process server = StartPython(ExternalTool.Fixture("tsa_server.py"), certPath, keyPath);
            try
            {
                int port = ReadPort(server);
                using var http = new HttpClient();
                var authority = new HttpTimestampAuthority(http, new Uri("http://127.0.0.1:" + port.ToString() + "/"));
                byte[] signed = Sign(
                    File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")),
                    signer,
                    signerKey,
                    Array.Empty<byte[]>(),
                    authority);
                string report = Validate(signed, signer.RawData, null, "entire", "timestamp,tsa=" + rootPath);
                Assert.Contains("timestamp_intact=true", report, StringComparison.Ordinal);
                Assert.Contains("timestamp_trusted=true", report, StringComparison.Ordinal);
            }
            finally
            {
                if (!server.HasExited)
                {
                    server.Kill(entireProcessTree: true);
                    server.WaitForExit();
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void pkcs11_signature_matches_pkcs1_and_pyhanko_and_pdfsig_trust_the_chain()
    {
        string libraryPath = LocatePkcs11();
        string directory = ExternalTool.TempDirectory();
        try
        {
            string config = Path.Combine(directory, "softhsm2.conf");
            string tokens = Path.Combine(directory, "tokens");
            Directory.CreateDirectory(tokens);
            File.WriteAllText(
                config,
                "directories.tokendir = " + tokens.Replace('\\', '/') + Environment.NewLine
                + "objectstore.backend = file" + Environment.NewLine
                + "log.level = ERROR" + Environment.NewLine);
            SetSoftHsmConfig(config);
            string libDir = Path.GetDirectoryName(libraryPath) ?? directory;
            Environment.SetEnvironmentVariable("PATH", libDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
            RunChecked(LocateSoftHsmUtil(libraryPath), "--init-token", "--free", "--label", "pdf-test", "--so-pin", "12345678", "--pin", "12345678");

            var factories = new Pkcs11InteropFactories();
            using IPkcs11Library library = factories.Pkcs11LibraryFactory.LoadPkcs11Library(factories, libraryPath, AppType.SingleThreaded);
            ISlot slot = FindSlot(library, "pdf-test");
            using ISession session = slot.OpenSession(SessionType.ReadWrite);
            session.Login(CKU.CKU_USER, "12345678");
            IObjectAttributeFactory attributes = session.Factories.ObjectAttributeFactory;
            using IMechanism generate = session.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS_KEY_PAIR_GEN);
            session.GenerateKeyPair(
                generate,
                new List<IObjectAttribute>
                {
                    attributes.Create(CKA.CKA_TOKEN, true),
                    attributes.Create(CKA.CKA_VERIFY, true),
                    attributes.Create(CKA.CKA_MODULUS_BITS, 2048),
                    attributes.Create(CKA.CKA_PUBLIC_EXPONENT, new byte[] { 0x01, 0x00, 0x01 }),
                    attributes.Create(CKA.CKA_LABEL, "pdf"),
                },
                new List<IObjectAttribute>
                {
                    attributes.Create(CKA.CKA_TOKEN, true),
                    attributes.Create(CKA.CKA_PRIVATE, true),
                    attributes.Create(CKA.CKA_SENSITIVE, true),
                    attributes.Create(CKA.CKA_SIGN, true),
                    attributes.Create(CKA.CKA_LABEL, "pdf"),
                },
                out IObjectHandle publicKey,
                out IObjectHandle privateKey);
            byte[] modulus = session.GetAttributeValue(publicKey, new List<CKA> { CKA.CKA_MODULUS })[0].GetValueAsByteArray();
            byte[] exponent = session.GetAttributeValue(publicKey, new List<CKA> { CKA.CKA_PUBLIC_EXPONENT })[0].GetValueAsByteArray();
            var signer = new Pkcs11Signer(session, privateKey, modulus, exponent);
            byte[] probe = TestCompat.Sha256(Encoding.ASCII.GetBytes("pkcs11-probe"));
            using (var verifier = RSA.Create())
            {
                verifier.ImportParameters(new RSAParameters { Modulus = TrimInteger(modulus), Exponent = TrimInteger(exponent) });
                Assert.True(verifier.VerifyHash(probe, signer.SignHash(probe), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            }

            using var rootKey = RSA.Create(2048);
            using var intermediateKey = RSA.Create(2048);
            using X509Certificate2 root = CreateAuthority("PKCS11 Root", rootKey, null);
            using X509Certificate2 intermediate = CreateAuthority("PKCS11 Intermediate", intermediateKey, root);
            using var leafPublic = RSA.Create();
            leafPublic.ImportParameters(new RSAParameters { Modulus = TrimInteger(modulus), Exponent = TrimInteger(exponent) });
            using X509Certificate2 leaf = CreateLeaf("PKCS11 Leaf", leafPublic, intermediate);
            signer.Certificate = leaf.RawData;
            signer.Chain = new[] { intermediate.RawData, root.RawData };
            byte[] signed = Sign(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), signer, null);
            string report = Validate(signed, root.RawData, null, "entire", "chain");
            Assert.Contains("intact=true", report, StringComparison.Ordinal);
            Assert.Contains("trusted=true", report, StringComparison.Ordinal);
            Assert.Contains("Signer Certificate Common Name: PKCS11 Leaf", PdfsigAccepts(signed, root.RawData, null, wholeFile: true));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void integrity_stays_distinct_from_the_modification_policy()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            RunChecked("python", ExternalTool.Fixture("certify_samples.py"), directory);
            byte[] approval = File.ReadAllBytes(Path.Combine(directory, "approval.pdf"));
            byte[] locked = File.ReadAllBytes(Path.Combine(directory, "locked.pdf"));
            byte[] fillable = File.ReadAllBytes(Path.Combine(directory, "fillable.pdf"));

            byte[] revised = ReviseTitle(approval, "Certified", "After approval");
            string approvalReport = Validate(revised, null, null, "revision", "docmdp-ok,modification=LTA_UPDATES");
            Assert.Contains("intact=true", approvalReport, StringComparison.Ordinal);
            Assert.Contains("valid=true", approvalReport, StringComparison.Ordinal);

            byte[] tampered = (byte[])approval.Clone();
            tampered[7] = (byte)'4';
            string broken = Validate(tampered, null, null, "entire", "broken");
            Assert.Contains("intact=false", broken, StringComparison.Ordinal);

            using (var stream = new MemoryStream(locked, writable: false))
            using (var document = PdfDocument.Open(stream, leaveOpen: true))
            {
                Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Fill("Name", "Ada"));
                Assert.Throws<PdfCertificationLockedException>(() => document.Metadata.Title = "Changed");
            }

            string lockedReport = Validate(locked, null, null, "entire", null);
            Assert.Contains("intact=true", lockedReport, StringComparison.Ordinal);

            byte[] filled = Fill(fillable, "Ada");
            using (var stream = new MemoryStream(fillable, writable: false))
            using (var document = PdfDocument.Open(stream, leaveOpen: true))
            {
                Assert.Throws<PdfCertificationLockedException>(() => document.Metadata.Title = "Changed");
            }

            string filledReport = Validate(filled, null, null, "revision", "docmdp-ok,modification=FORM_FILLING");
            Assert.Contains("intact=true", filledReport, StringComparison.Ordinal);
            Assert.Contains("valid=true", filledReport, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void pyhanko_fieldmdp_include_and_exclude_lock_the_same_fields_and_allowed_fills_keep_the_signature()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            RunChecked("python", ExternalTool.Fixture("certify_samples.py"), directory, "fieldmdp");
            string[] names = { "Locked", "Open", "Group.Child" };
            var samples = new Dictionary<string, string[]>
            {
                ["fieldmdp-include.pdf"] = new[] { "Locked" },
                ["fieldmdp-exclude.pdf"] = new[] { "Locked" },
                ["fieldmdp-certified.pdf"] = new[] { "Locked" },
                ["fieldmdp-include-parent.pdf"] = new[] { "Group.Child" },
                ["fieldmdp-exclude-parent.pdf"] = new[] { "Locked", "Open" },
            };
            foreach (KeyValuePair<string, string[]> entry in samples)
            {
                string sample = entry.Key;
                byte[] pdf = File.ReadAllBytes(Path.Combine(directory, sample));
                string report = Validate(pdf, null, null, "entire", "fieldmdp=" + string.Join("|", names));
                var lines = new HashSet<string>(report.Replace("\r", string.Empty).Split('\n'), StringComparer.Ordinal);
                foreach (string name in names)
                {
                    string verdict = Array.IndexOf(entry.Value, name) >= 0 ? "locked" : "open";
                    Assert.True(lines.Contains("fieldmdp:" + name + "=" + verdict), sample + "\n" + report);
                }

                byte[] filled;
                using (var source = new MemoryStream(pdf, writable: false))
                using (var document = PdfDocument.Open(source, leaveOpen: true))
                {
                    foreach (string name in names)
                    {
                        if (lines.Contains("fieldmdp:" + name + "=locked"))
                        {
                            Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Fill(name, "Ada"));
                        }
                        else
                        {
                            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill(name, "Ada"));
                        }
                    }

                    if (sample == "fieldmdp-certified.pdf")
                    {
                        Assert.Throws<PdfCertificationLockedException>(() => document.Metadata.Title = "Changed");
                    }

                    var destination = new MemoryStream();
                    document.SaveIncremental(destination, leaveOpen: true);
                    filled = destination.ToArray();
                }

                Assert.True(filled.AsSpan(0, pdf.Length).SequenceEqual(pdf), sample);
                using (var stream = new MemoryStream(filled, writable: false))
                using (var reopened = PdfDocument.Open(stream, leaveOpen: true))
                {
                    foreach (string name in names)
                    {
                        Assert.Equal(Array.IndexOf(entry.Value, name) >= 0 ? null : "Ada", reopened.AcroForm.GetFields(name)[0].Value);
                    }

                    Assert.Equal("FieldMDP", reopened.Metadata.Title);
                }

                string filledReport = Validate(filled, null, null, "revision", "docmdp-ok,modification=FORM_FILLING");
                Assert.Contains("intact=true", filledReport, StringComparison.Ordinal);
                Assert.Contains("valid=true", filledReport, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void pdfsig_and_pyhanko_verify_new_and_existing_field_signatures_and_trust_the_chain_from_the_root()
    {
        using var rootKey = RSA.Create(2048);
        using var intermediateKey = RSA.Create(2048);
        using var leafKey = RSA.Create(2048);
        using X509Certificate2 root = CreateAuthority("Interop Root", rootKey, null);
        using X509Certificate2 intermediate = CreateAuthority("Interop Intermediate", intermediateKey, root);
        using X509Certificate2 leaf = CreateLeaf("Interop Leaf", leafKey, intermediate);
        var signer = new RsaSigner(leafKey, leaf.RawData, new[] { intermediate.RawData, root.RawData });
        var cases = new (string Fixture, string? FieldName, string Expected)[]
        {
            ("pypdf-base.pdf", null, "Signature1"),
            ("pyhanko-sigfield.pdf", "ExistingSig", "ExistingSig"),
        };
        foreach ((string fixture, string? fieldName, string expected) in cases)
        {
            byte[] signed = Sign(File.ReadAllBytes(ExternalTool.Fixture(fixture)), null, fieldName, signer, null, DateTimeOffset.UtcNow);
            List<string> verdict = PdfsigAccepts(signed, root.RawData, null, wholeFile: true);
            Assert.Contains("Signature Field Name: " + expected, verdict);
            Assert.Contains("Signer Certificate Common Name: Interop Leaf", verdict);

            // Trust is a separate result: with no configured root the signature is still valid but not trusted.
            PdfsigAccepts(signed, null, null, wholeFile: true);

            string report = Validate(signed, root.RawData, null, "entire", "chain");
            Assert.Contains("intact=true", report, StringComparison.Ordinal);
            Assert.Contains("trusted=true", report, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void pdfsig_and_pyhanko_verify_a_timestamped_signature_an_encrypted_file_and_a_later_revision()
    {
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            using var rootKey = RSA.Create(2048);
            using var leafKey = RSA.Create(2048);
            using X509Certificate2 root = CreateAuthority("Interop Root", rootKey, null);
            using X509Certificate2 leaf = CreateLeaf("Interop Leaf", leafKey, root);
            var signer = new RsaSigner(leafKey, leaf.RawData, new[] { root.RawData });
            byte[] original = File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf"));

            // pdfsig does not check the embedded RFC 3161 token; pyHanko does.
            byte[] timestamped = Sign(original, null, null, signer, new OpenSslTimestampAuthority(openssl, directory), DateTimeOffset.UtcNow);
            PdfsigAccepts(timestamped, root.RawData, null, wholeFile: true);
            string report = Validate(timestamped, root.RawData, null, "entire", "timestamp,tsa=" + Path.Combine(directory, "root.pem"));
            Assert.Contains("timestamp_intact=true", report, StringComparison.Ordinal);
            Assert.Contains("timestamp_trusted=true", report, StringComparison.Ordinal);

            // The signing time must be real here. pdfsig 26.08.0 treats a CMS signing time at the Unix epoch as missing and
            // falls back to /M, which on an encrypted file it decrypts because the library writes it in plaintext (W-97).
            // The result is not a date, and pdfsig crashes on it.
            byte[] encrypted = Sign(File.ReadAllBytes(ExternalTool.Fixture("qpdf-encrypted.pdf")), "user-secret", null, signer, null, DateTimeOffset.UtcNow);
            PdfsigAccepts(encrypted, root.RawData, "user-secret", wholeFile: true);
            Assert.Contains("intact=true", Validate(encrypted, root.RawData, "user-secret", "entire", null), StringComparison.Ordinal);

            // A later revision leaves the signed ranges as they were; pdfsig reports only that they no longer reach the end.
            byte[] signed = Sign(original, null, null, signer, null, DateTimeOffset.UtcNow);
            byte[] revised = ReviseTitle(signed, "Foreign base", "After signing");
            Assert.Equal(Pdfsig.SignedRanges(signed), Pdfsig.SignedRanges(revised));
            PdfsigAccepts(revised, root.RawData, null, wholeFile: false);
            Assert.Contains("intact=true", Validate(revised, root.RawData, null, "revision", null), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void pdfsig_and_pyhanko_reject_a_tampered_byte_and_a_signature_from_another_key()
    {
        using var key = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        using X509Certificate2 certificate = CreateLeaf("Interop Signer", key, null);
        byte[] original = File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf"));

        byte[] tampered = Sign(original, null, null, new RsaSigner(key, certificate.RawData, Array.Empty<byte[]>()), null, DateTimeOffset.UtcNow);
        Assert.Equal((byte)'3', tampered[7]);
        tampered[7] = (byte)'4';
        (int exitCode, string report) = Pdfsig.Verify(tampered, new[] { certificate.RawData }, null);
        Assert.True(exitCode != 0, report);
        Assert.Contains(Pdfsig.DigestMismatch, Pdfsig.Signatures(report)[0]);
        Assert.DoesNotContain(Pdfsig.Valid, report, StringComparison.Ordinal);
        Validate(tampered, certificate.RawData, null, "entire", "broken");

        // The certificate names one key and the host signs with another: the byte range is intact, the signature is not.
        byte[] mismatched = Sign(original, null, null, new RsaSigner(otherKey, certificate.RawData, Array.Empty<byte[]>()), null, DateTimeOffset.UtcNow);
        (exitCode, report) = Pdfsig.Verify(mismatched, new[] { certificate.RawData }, null);
        Assert.True(exitCode != 0, report);
        Assert.Contains("Signature Validation: Signature is Invalid.", Pdfsig.Signatures(report)[0]);
        Assert.DoesNotContain(Pdfsig.Valid, report, StringComparison.Ordinal);
        Validate(mismatched, certificate.RawData, null, "entire", "invalid");
    }

    // W-14: pdfsig must accept the file's one signature as the library writes it today: adbe.pkcs7.detached, SHA-256,
    // over exactly the /ByteRange in the file, valid, and trusted only when the root is configured.
    private static List<string> PdfsigAccepts(byte[] signed, byte[]? root, string? password, bool wholeFile)
    {
        (int exitCode, string report) = Pdfsig.Verify(signed, root == null ? Array.Empty<byte[]>() : new[] { root }, password);
        List<List<string>> signatures = Pdfsig.Signatures(report);
        Assert.True(exitCode == 0 && signatures.Count == 1, report);
        List<string> signature = signatures[0];
        Assert.Contains("Signature Type: adbe.pkcs7.detached", signature);
        Assert.Contains("Signing Hash Algorithm: SHA-256", signature);
        Assert.Contains(Pdfsig.SignedRanges(signed), signature);
        Assert.Contains(wholeFile ? Pdfsig.WholeFile : Pdfsig.NotWholeFile, signature);
        Assert.Contains(Pdfsig.Valid, signature);
        Assert.Contains(root == null ? Pdfsig.Untrusted : Pdfsig.Trusted, signature);
        return signature;
    }

    private static byte[] Sign(byte[] pdf, X509Certificate2 certificate, RSA key, IReadOnlyList<byte[]> chain, ITimestampAuthority? authority)
    {
        return Sign(pdf, new RsaSigner(key, certificate.RawData, chain), authority);
    }

    private static byte[] Sign(byte[] pdf, IPdfExternalSigner signer, ITimestampAuthority? authority)
    {
        return Sign(pdf, null, null, signer, authority, TestCompat.UnixEpoch);
    }

    private static byte[] Sign(byte[] pdf, string? password, string? fieldName, IPdfExternalSigner signer, ITimestampAuthority? authority, DateTimeOffset signingTime)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        var destination = new MemoryStream();
        document.Sign(
            destination,
            new PdfSignatureOptions { ReservedContentsBytes = 32768, SigningTime = signingTime, FieldName = fieldName },
            authority == null
                ? new CmsDetachedSignatureProvider(signer)
                : new CmsDetachedSignatureProvider(signer, authority),
            leaveOpen: true);
        return destination.ToArray();
    }

    private static byte[] ReviseTitle(byte[] pdf, string expected, string title)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        Assert.Equal(expected, document.Metadata.Title);
        document.Metadata.Title = title;
        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);
        byte[] saved = destination.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        return saved;
    }

    private static byte[] Fill(byte[] pdf, string value)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", value));
        var destination = new MemoryStream();
        document.SaveIncremental(destination, leaveOpen: true);
        byte[] saved = destination.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        return saved;
    }

    private static string Validate(byte[] pdf, byte[]? trust, string? password, string coverage, string? checks)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string pdfPath = Path.Combine(directory, "signed.pdf");
            File.WriteAllBytes(pdfPath, pdf);
            string trustPath = "-";
            if (trust != null)
            {
                trustPath = Path.Combine(directory, "trust.pem");
                File.WriteAllText(trustPath, ExternalTool.Pem(trust));
            }

            var args = new List<string>
            {
                ExternalTool.Fixture("validate_signature.py"),
                pdfPath,
                trustPath,
                password ?? "-",
                coverage,
            };
            if (checks != null)
            {
                args.Add(checks);
            }

            using Process process = StartPython(args.ToArray());
            return ExternalTool.Output(process);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Process StartPython(params string[] args)
    {
        return ExternalTool.Start("python", args);
    }

    private static int ReadPort(Process process)
    {
        string line = process.StandardOutput.ReadLine() ?? string.Empty;
        Assert.True(line.StartsWith("READY ", StringComparison.Ordinal), line);
        return int.Parse(line.Substring(6), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void RunChecked(string fileName, params string[] args)
    {
        using Process process = ExternalTool.Start(fileName, args);
        ExternalTool.Output(process);
    }

    private static void SetSoftHsmConfig(string config)
    {
        Environment.SetEnvironmentVariable("SOFTHSM2_CONF", config);
        if (!ExternalTool.IsWindows)
        {
            // On Unix, Environment.SetEnvironmentVariable changes only the managed copy.
            // The in-process SoftHSM library reads SOFTHSM2_CONF with getenv, so set the native one as well.
            Assert.Equal(0, SetNativeEnvironmentVariable("SOFTHSM2_CONF", config, 1));
        }
    }

    [DllImport("libc", EntryPoint = "setenv")]
    private static extern int SetNativeEnvironmentVariable(string name, string value, int overwrite);

    private static string LocatePkcs11()
    {
        return ExternalTool.Locate("JAYTWO_PKCS11", ExternalTool.IsWindows ? "softhsm2-x64.dll" : "libsofthsm2.so");
    }

    private static string LocateSoftHsmUtil(string libraryPath)
    {
        string? configured = Environment.GetEnvironmentVariable("JAYTWO_SOFTHSM_UTIL");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        string util = ExternalTool.Executable("softhsm2-util");
        string? directory = Path.GetDirectoryName(libraryPath);
        string sibling = Path.GetFullPath(Path.Combine(directory ?? ".", "..", "bin", util));
        if (File.Exists(sibling))
        {
            return sibling;
        }

        string? found = ExternalTool.OnPath(util);
        Assert.True(found != null, util + " was not found beside " + libraryPath + " or on PATH.");
        return found!;
    }

    private static ISlot FindSlot(IPkcs11Library library, string label)
    {
        foreach (ISlot slot in library.GetSlotList(SlotsType.WithTokenPresent))
        {
            if (slot.GetTokenInfo().Label.Trim() == label)
            {
                return slot;
            }
        }

        throw new InvalidOperationException("SoftHSM token " + label + " was not found.");
    }

    private static X509Certificate2 CreateAuthority(string commonName, RSA key, X509Certificate2? issuer)
    {
        var request = new CertificateRequest("CN=" + commonName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-2);
        if (issuer == null)
        {
            return request.CreateSelfSigned(notBefore, DateTimeOffset.UtcNow.AddYears(2));
        }

        X509Certificate2 issued = request.Create(issuer, notBefore, issuer.NotAfter.AddMinutes(-1), Serial());
        X509Certificate2 withKey = issued.CopyWithPrivateKey(key);
        issued.Dispose();
        return withKey;
    }

    private static X509Certificate2 CreateLeaf(string commonName, RSA key, X509Certificate2? issuer)
    {
        var request = new CertificateRequest("CN=" + commonName, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        if (issuer == null)
        {
            return request.CreateSelfSigned(notBefore, DateTimeOffset.UtcNow.AddYears(1));
        }

        return request.Create(issuer, notBefore, issuer.NotAfter.AddMinutes(-1), Serial());
    }

    private static X509Certificate2 CreateTimestampAuthority(RSA key, X509Certificate2 issuer)
    {
        var request = new CertificateRequest("CN=Interop TSA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.8") }, false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), issuer.NotAfter.AddMinutes(-1), Serial());
    }

    private static byte[] Serial()
    {
        byte[] serial = new byte[8];
        TestCompat.FillRandom(serial);
        serial[0] &= 0x7F;
        if (serial[0] == 0)
        {
            serial[0] = 1;
        }

        return serial;
    }

    private static string PemKey(RSA key)
    {
        return TestCompat.Pkcs8PrivateKeyPem(key);
    }

    private static byte[] TrimInteger(byte[] value)
    {
        int start = 0;
        while (start < value.Length - 1 && value[start] == 0)
        {
            start++;
        }

        if (start == 0)
        {
            return value;
        }

        var trimmed = new byte[value.Length - start];
        Buffer.BlockCopy(value, start, trimmed, 0, trimmed.Length);
        return trimmed;
    }

    private sealed class RsaSigner : IPdfExternalSigner
    {
        private readonly RSA _key;

        public RsaSigner(RSA key, byte[] certificate, IReadOnlyList<byte[]> additionalCertificates)
        {
            _key = key;
            CertificateDer = certificate;
            AdditionalCertificates = additionalCertificates;
        }

        public byte[] CertificateDer { get; }

        public IReadOnlyList<byte[]> AdditionalCertificates { get; }

        public byte[] SignHash(ReadOnlySpan<byte> hash)
        {
            return _key.SignHash(hash.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    private sealed class Pkcs11Signer : IPdfExternalSigner
    {
        private readonly ISession _session;
        private readonly IObjectHandle _privateKey;

        public Pkcs11Signer(ISession session, IObjectHandle privateKey, byte[] modulus, byte[] exponent)
        {
            _session = session;
            _privateKey = privateKey;
            Modulus = modulus;
            Exponent = exponent;
            CertificateDer = Array.Empty<byte>();
            Chain = Array.Empty<byte[]>();
        }

        public byte[] Modulus { get; }

        public byte[] Exponent { get; }

        public byte[] Certificate { set => CertificateDer = value; }

        public byte[] CertificateDer { get; private set; }

        public IReadOnlyList<byte[]> Chain { get; set; }

        public IReadOnlyList<byte[]> AdditionalCertificates => Chain;

        public byte[] SignHash(ReadOnlySpan<byte> hash)
        {
            byte[] digestInfo = DerCodec.Sequence(
                DerCodec.Sequence(DerCodec.Oid("2.16.840.1.101.3.4.2.1"), DerCodec.Null()),
                DerCodec.OctetString(hash.ToArray()));
            using IMechanism mechanism = _session.Factories.MechanismFactory.Create(CKM.CKM_RSA_PKCS);
            return _session.Sign(mechanism, _privateKey, digestInfo);
        }
    }
}
