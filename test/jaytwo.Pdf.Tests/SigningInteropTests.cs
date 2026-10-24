using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Signatures;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Net.Pkcs11Interop.HighLevelAPI.Factories;
using Xunit;

namespace jaytwo.Pdf.Tests;

public class SigningInteropTests
{
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
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
    [Trait(Lane.Trait, Lane.Interop)]
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

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [MemberData(nameof(SigningAlgorithmTests.Combinations), MemberType = typeof(SigningAlgorithmTests))]
    public void pyhanko_and_pdfsig_trust_every_key_and_digest(string keyKind, PdfDigestAlgorithm digestAlgorithm)
    {
        using var rootKey = RSA.Create(2048);
        using X509Certificate2 root = CreateAuthority("Algorithm Root", rootKey, null);
        using AsymmetricAlgorithm leafKey = TestKeys.Create(keyKind);
        using X509Certificate2 leaf = CreateLeaf("Algorithm Leaf", leafKey, root);
        var signer = new KeySigner(leafKey, leaf.RawData, new[] { root.RawData }, null);
        byte[] signed = Sign(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), signer, digestAlgorithm, null);
        AssertVerifiersAccept(signed, root.RawData, null, keyKind, digestAlgorithm, "Algorithm Leaf");
    }

    // W-70: the RFC 3161 imprint is the SHA-256 of the DER-encoded ECDSA value the SignerInfo stores, whatever the signature's digest.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData(TestKeys.P256, PdfDigestAlgorithm.Sha256)]
    [InlineData(TestKeys.P384, PdfDigestAlgorithm.Sha512)]
    public void pyhanko_trusts_the_timestamp_on_an_ecdsa_signature(string keyKind, PdfDigestAlgorithm digestAlgorithm)
    {
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            using var rootKey = RSA.Create(2048);
            using X509Certificate2 root = CreateAuthority("Algorithm Root", rootKey, null);
            using AsymmetricAlgorithm leafKey = TestKeys.Create(keyKind);
            using X509Certificate2 leaf = CreateLeaf("Algorithm Leaf", leafKey, root);
            var signer = new KeySigner(leafKey, leaf.RawData, new[] { root.RawData }, null);
            byte[] signed = Sign(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), signer, digestAlgorithm, new OpenSslTimestampAuthority(openssl, directory));
            AssertVerifiersAccept(signed, root.RawData, "timestamp,tsa=" + Path.Combine(directory, "root.pem"), keyKind, digestAlgorithm, "Algorithm Leaf");
            string report = Validate(signed, root.RawData, null, "entire", "timestamp,tsa=" + Path.Combine(directory, "root.pem"));
            Assert.Contains("timestamp_intact=true", report, StringComparison.Ordinal);
            Assert.Contains("timestamp_trusted=true", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // W-71: every key and digest as PAdES baseline B-B. pyHanko trusts the signature and its signing-certificate-v2,
    // the EN 319 142-1 checklist in validate_signature.py passes, and pdfsig reports ETSI.CAdES.detached.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [MemberData(nameof(SigningAlgorithmTests.Combinations), MemberType = typeof(SigningAlgorithmTests))]
    public void pyhanko_and_pdfsig_accept_pades_baseline_b_for_every_key_and_digest(string keyKind, PdfDigestAlgorithm digestAlgorithm)
    {
        using var rootKey = RSA.Create(2048);
        using X509Certificate2 root = CreateAuthority("PAdES Root", rootKey, null);
        using AsymmetricAlgorithm leafKey = TestKeys.Create(keyKind);
        using X509Certificate2 leaf = CreateLeaf("PAdES Leaf", leafKey, root);
        var signer = new KeySigner(leafKey, leaf.RawData, new[] { root.RawData }, null);
        byte[] signed = Sign(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), signer, digestAlgorithm, null, PdfSignatureFormat.PadesBaselineB);
        AssertVerifiersAccept(signed, root.RawData, "pades-b", keyKind, digestAlgorithm, "PAdES Leaf", "ETSI.CAdES.detached");
        Assert.Contains("pades=B-B", Validate(signed, root.RawData, null, "entire", "pades-b"), StringComparison.Ordinal);
    }

    // W-71: with an OpenSSL RFC 3161 authority the signature is PAdES baseline B-T. pyHanko trusts the
    // signature-time-stamp under the TSA root, and the checklist finds exactly that one unsigned attribute.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData(TestKeys.Rsa, PdfDigestAlgorithm.Sha256)]
    [InlineData(TestKeys.P256, PdfDigestAlgorithm.Sha384)]
    public void pyhanko_and_pdfsig_accept_pades_baseline_t_with_an_openssl_timestamp(string keyKind, PdfDigestAlgorithm digestAlgorithm)
    {
        string openssl = OpenSslTimestampAuthority.Locate();
        string directory = ExternalTool.TempDirectory();
        try
        {
            OpenSslTimestampAuthority.CreatePki(openssl, directory);
            using var rootKey = RSA.Create(2048);
            using X509Certificate2 root = CreateAuthority("PAdES Root", rootKey, null);
            using AsymmetricAlgorithm leafKey = TestKeys.Create(keyKind);
            using X509Certificate2 leaf = CreateLeaf("PAdES Leaf", leafKey, root);
            var signer = new KeySigner(leafKey, leaf.RawData, new[] { root.RawData }, null);
            byte[] signed = Sign(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), signer, digestAlgorithm, new OpenSslTimestampAuthority(openssl, directory), PdfSignatureFormat.PadesBaselineB);
            string checks = "timestamp,tsa=" + Path.Combine(directory, "root.pem") + ",pades-t";
            AssertVerifiersAccept(signed, root.RawData, checks, keyKind, digestAlgorithm, "PAdES Leaf", "ETSI.CAdES.detached");
            string report = Validate(signed, root.RawData, null, "entire", checks);
            Assert.Contains("timestamp_trusted=true", report, StringComparison.Ordinal);
            Assert.Contains("pades=B-T", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // W-70: RSA through CKM_RSA_PKCS with a host-built DigestInfo, and P-256 and P-384 through CKM_ECDSA
    // on the raw digest, which returns the P1363 r||s value. Each key signs with every digest.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData(TestKeys.Rsa)]
    [InlineData(TestKeys.P256)]
    [InlineData(TestKeys.P384)]
    public async Task pkcs11_keys_sign_every_digest_and_pyhanko_and_pdfsig_trust_the_chain(string keyKind)
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
            Pkcs11Signer signer = keyKind == TestKeys.Rsa ? GenerateRsa(session) : GenerateEcdsa(session, keyKind);
            using AsymmetricAlgorithm publicKey = signer.PublicKey;
            foreach (HashAlgorithmName name in new[] { HashAlgorithmName.SHA256, HashAlgorithmName.SHA384, HashAlgorithmName.SHA512 })
            {
                byte[] probe;
                using (var incremental = IncrementalHash.CreateHash(name))
                {
                    incremental.AppendData(Encoding.ASCII.GetBytes("pkcs11-probe"));
                    probe = incremental.GetHashAndReset();
                }

                byte[] value = await signer.SignHashAsync(probe, name, CancellationToken.None);
                Assert.True(publicKey is RSA rsa
                    ? rsa.VerifyHash(probe, value, name, RSASignaturePadding.Pkcs1)
                    : ((ECDsa)publicKey).VerifyHash(probe, value));
            }

            using var rootKey = RSA.Create(2048);
            using var intermediateKey = RSA.Create(2048);
            using X509Certificate2 root = CreateAuthority("PKCS11 Root", rootKey, null);
            using X509Certificate2 intermediate = CreateAuthority("PKCS11 Intermediate", intermediateKey, root);
            using X509Certificate2 leaf = CreateLeaf("PKCS11 Leaf", publicKey, intermediate);
            signer.Certificate = leaf.RawData;
            signer.Chain = new[] { intermediate.RawData, root.RawData };
            foreach (PdfDigestAlgorithm digestAlgorithm in new[] { PdfDigestAlgorithm.Sha256, PdfDigestAlgorithm.Sha384, PdfDigestAlgorithm.Sha512 })
            {
                byte[] signed = Sign(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), signer, digestAlgorithm, null);
                AssertVerifiersAccept(signed, root.RawData, "chain", keyKind, digestAlgorithm, "PKCS11 Leaf");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void integrity_stays_distinct_from_the_modification_policy()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            RunChecked(ExternalTool.Python(), ExternalTool.Fixture("certify_samples.py"), directory);
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

    // W-77 (spec 7.2): flatten refuses a certified document at P 1 and P 2 even with the opt-in, and a document with an
    // approval signature without it. With the opt-in it flattens every field but the signed one; the signature's bytes
    // stay a prefix, pyHanko finds it intact and valid and reports the change as beyond form filling, and pdfsig
    // verifies it over a range that no longer covers the file.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void opted_in_flatten_after_an_approval_signature_keeps_it_intact_and_verifiers_report_the_modification()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            RunChecked(ExternalTool.Python(), ExternalTool.Fixture("certify_samples.py"), directory);
            byte[] approval = File.ReadAllBytes(Path.Combine(directory, "approval.pdf"));
            foreach (string certified in new[] { "locked.pdf", "fillable.pdf" })
            {
                byte[] pdf = File.ReadAllBytes(Path.Combine(directory, certified));
                using var stream = new MemoryStream(pdf, writable: false);
                using var document = PdfDocument.Open(stream, leaveOpen: true);
                document.AllowBreakingSignatures = true;
                Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Flatten());
            }

            byte[] flattened;
            using (var stream = new MemoryStream(approval, writable: false))
            using (var document = PdfDocument.Open(stream, leaveOpen: true))
            {
                Assert.Throws<PdfSignatureConflictException>(() => document.AcroForm.Flatten());
                Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada"));
                document.AllowBreakingSignatures = true;
                Assert.Throws<PdfSignatureConflictException>(() => document.AcroForm.Flatten(new[] { "PolicySig" }));
                document.AcroForm.Flatten();
                Assert.Equal("PolicySig", Assert.Single(document.AcroForm.Fields).FullName);
                var destination = new MemoryStream();
                document.SaveIncremental(destination, leaveOpen: true);
                flattened = destination.ToArray();
            }

            Assert.True(flattened.AsSpan(0, approval.Length).SequenceEqual(approval));
            string report = Validate(flattened, null, null, "revision", "docmdp-illegal,modification=OTHER");
            Assert.Contains("intact=true", report, StringComparison.Ordinal);
            Assert.Contains("valid=true", report, StringComparison.Ordinal);
            (int exitCode, string pdfsig) = Pdfsig.Verify(flattened, Array.Empty<byte[]>(), null);
            List<string> signature = Assert.Single(Pdfsig.Signatures(pdfsig));
            Assert.True(exitCode == 0, pdfsig);
            Assert.Contains(Pdfsig.Valid, signature);
            Assert.Contains(Pdfsig.NotWholeFile, signature);
            Assert.Contains(Pdfsig.SignedRanges(flattened), signature);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // W-77 (spec 7.2): FieldMDP applies to flatten. With the opt-in, the Include [Locked] lock refuses a flatten of
    // Locked and of every field, and a flatten of Open keeps the signature intact and valid in pyHanko.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void pyhanko_fieldmdp_lock_refuses_a_flatten_of_the_locked_field()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            RunChecked(ExternalTool.Python(), ExternalTool.Fixture("certify_samples.py"), directory, "fieldmdp");
            byte[] include = File.ReadAllBytes(Path.Combine(directory, "fieldmdp-include.pdf"));
            byte[] flattened;
            using (var stream = new MemoryStream(include, writable: false))
            using (var document = PdfDocument.Open(stream, leaveOpen: true))
            {
                document.AllowBreakingSignatures = true;
                Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Flatten(new[] { "Locked" }));
                Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Flatten());
                Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Open", "Ada"));
                document.AcroForm.Flatten(new[] { "Open" });
                var destination = new MemoryStream();
                document.SaveIncremental(destination, leaveOpen: true);
                flattened = destination.ToArray();
            }

            Assert.True(flattened.AsSpan(0, include.Length).SequenceEqual(include));
            string report = Validate(flattened, null, null, "revision", "modification=OTHER");
            Assert.Contains("intact=true", report, StringComparison.Ordinal);
            Assert.Contains("valid=true", report, StringComparison.Ordinal);
            Assert.DoesNotContain("Open", FormValues.Pypdf(flattened).Keys);
            Assert.Contains("Locked", FormValues.Pypdf(flattened).Keys);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void pyhanko_fieldmdp_include_and_exclude_lock_the_same_fields_and_allowed_fills_keep_the_signature()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            RunChecked(ExternalTool.Python(), ExternalTool.Fixture("certify_samples.py"), directory, "fieldmdp");
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

    // W-97: pyHanko encrypts the field names of its FieldMDP lock with the rest of the signature dictionary. The library
    // decrypts them, so the Include lock holds on an encrypted file, and pdfsig still verifies after the allowed fill.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void pyhanko_fieldmdp_lock_on_an_encrypted_file_holds_and_the_allowed_fill_keeps_the_signature()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            RunChecked(ExternalTool.Python(), ExternalTool.Fixture("certify_samples.py"), directory, "fieldmdp-encrypted", CorpusManifest.Qpdf());
            foreach (string sample in new[] { "fieldmdp-include-rc4.pdf", "fieldmdp-include-aes256.pdf" })
            {
                byte[] pdf = File.ReadAllBytes(Path.Combine(directory, sample));
                byte[] filled;
                using (var source = new MemoryStream(pdf, writable: false))
                using (var document = PdfDocument.Open(source, "user", leaveOpen: true))
                {
                    Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Fill("Locked", "Ada"));
                    Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Open", "Ada"));
                    var destination = new MemoryStream();
                    document.SaveIncremental(destination, leaveOpen: true);
                    filled = destination.ToArray();
                }

                // pyHanko 0.38.0 cannot grade the fill: its difference analysis compares a decrypted field name proxy
                // with a str and raises TypeError on an encrypted FieldMDP lock. pdfsig checks the signature instead.
                Assert.True(filled.AsSpan(0, pdf.Length).SequenceEqual(pdf), sample);
                Assert.Contains("intact=true", Validate(pdf, null, "user", "entire", null), StringComparison.Ordinal);
                (int exitCode, string pdfsig) = Pdfsig.Verify(filled, Array.Empty<byte[]>(), "user");
                List<List<string>> signatures = Pdfsig.Signatures(pdfsig);
                Assert.True(exitCode == 0 && signatures.Count == 1, pdfsig);
                Assert.Contains(Pdfsig.SignedRanges(filled), signatures[0]);
                Assert.Contains(Pdfsig.NotWholeFile, signatures[0]);
                Assert.Contains(Pdfsig.Valid, signatures[0]);
                using (var stream = new MemoryStream(filled, writable: false))
                using (var reopened = PdfDocument.Open(stream, "user", leaveOpen: true))
                {
                    Assert.Null(reopened.AcroForm.GetFields("Locked")[0].Value);
                    Assert.Equal("Ada", reopened.AcroForm.GetFields("Open")[0].Value);
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
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
    [Trait(Lane.Trait, Lane.Interop)]
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

            // pdfsig 26.08.0 treats a CMS signing time at the Unix epoch as missing and falls back to /M, which it decrypts.
            // It crashed on the plaintext /M the library wrote before W-97; /M is now encrypted, so the epoch stays.
            byte[] encrypted = Sign(File.ReadAllBytes(ExternalTool.Fixture("qpdf-encrypted.pdf")), "user-secret", null, signer, null, TestCompat.UnixEpoch);
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

    // W-69: SignAsync, with a signer and an authority that complete asynchronously, writes what both verifiers
    // accept: a timestamped signature, and one on a qpdf AES-256 file under the user and the owner password.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public async Task pdfsig_and_pyhanko_verify_signatures_from_sign_async()
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
            var signer = new DeferredSigner(new RsaSigner(leafKey, leaf.RawData, new[] { root.RawData }));
            byte[] original = File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf"));

            byte[] timestamped = await SignAsync(original, null, signer, new DeferredAuthority(new OpenSslTimestampAuthority(openssl, directory)), DateTimeOffset.UtcNow);
            Assert.Contains("Signer Certificate Common Name: Interop Leaf", PdfsigAccepts(timestamped, root.RawData, null, wholeFile: true));
            string report = Validate(timestamped, root.RawData, null, "entire", "timestamp,tsa=" + Path.Combine(directory, "root.pem"));
            Assert.Contains("intact=true", report, StringComparison.Ordinal);
            Assert.Contains("trusted=true", report, StringComparison.Ordinal);
            Assert.Contains("timestamp_intact=true", report, StringComparison.Ordinal);
            Assert.Contains("timestamp_trusted=true", report, StringComparison.Ordinal);

            byte[] encrypted = QpdfEncrypt(original, "256");
            foreach (string password in new[] { "user-secret", "owner-secret" })
            {
                byte[] signed = await SignAsync(encrypted, password, signer, null, TestCompat.UnixEpoch);
                PdfsigAccepts(signed, root.RawData, "user-secret", wholeFile: true);
                string verdict = Validate(signed, root.RawData, "user-secret", "entire", null);
                Assert.Contains("intact=true", verdict, StringComparison.Ordinal);
                Assert.Contains("trusted=true", verdict, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // W-97: qpdf encrypts the base file with each cipher. After Sign under the user or the owner password, pypdf reads
    // every signature dictionary string as passed, the gap the /ByteRange leaves is the plaintext CMS, and pyHanko and
    // pdfsig verify the signature. The CMS signing time is the Unix epoch, so pdfsig reads /M.
    // W-71: the PAdES rows check the same strings and the EN 319 142-1 checklist under each password.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("40", "user-secret", PdfSignatureFormat.AdbePkcs7Detached)]
    [InlineData("128-rc4", "owner-secret", PdfSignatureFormat.AdbePkcs7Detached)]
    [InlineData("128-aes", "user-secret", PdfSignatureFormat.AdbePkcs7Detached)]
    [InlineData("256", "owner-secret", PdfSignatureFormat.AdbePkcs7Detached)]
    [InlineData("128-aes", "owner-secret", PdfSignatureFormat.PadesBaselineB)]
    [InlineData("256", "user-secret", PdfSignatureFormat.PadesBaselineB)]
    public void encrypted_signature_dictionary_strings_read_in_pypdf_and_verify_in_pyhanko_and_pdfsig(string cipher, string password, PdfSignatureFormat format)
    {
        using var rootKey = RSA.Create(2048);
        using var leafKey = RSA.Create(2048);
        using X509Certificate2 root = CreateAuthority("Interop Root", rootKey, null);
        using X509Certificate2 leaf = CreateLeaf("Interop Leaf", leafKey, root);
        var signer = new RsaSigner(leafKey, leaf.RawData, new[] { root.RawData });
        byte[] encrypted = QpdfEncrypt(File.ReadAllBytes(ExternalTool.Fixture("pypdf-base.pdf")), cipher);

        byte[] signed;
        using (var source = new MemoryStream(encrypted, writable: false))
        using (var document = PdfDocument.Open(source, password, leaveOpen: true))
        {
            var destination = new MemoryStream();
            document.Sign(
                destination,
                new PdfSignatureOptions
                {
                    ReservedContentsBytes = 32768,
                    SigningTime = TestCompat.UnixEpoch,
                    Reason = "Approved",
                    Location = "Denver",
                    ContactInfo = "signer@example.com",
                    SignerName = "Interop Leaf",
                    Format = format,
                },
                new CmsDetachedSignatureProvider(signer),
                leaveOpen: true);
            signed = destination.ToArray();
        }

        Assert.True(signed.AsSpan(0, encrypted.Length).SequenceEqual(encrypted));
        string text = Encoding.ASCII.GetString(signed);
        Assert.DoesNotContain("(Approved)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(Interop Leaf)", text, StringComparison.Ordinal);
        Match range = Regex.Match(text, @"/ByteRange\s*\[\s*0+\s+(\d+)\s+(\d+)\s+\d+\s*\]");
        Assert.True(range.Success);
        int gap = int.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture);
        int end = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
        Assert.Matches("^<30[0-9A-F]+>$", text.Substring(gap, end - gap));

        Dictionary<string, JsonElement> strings = SignatureStrings(signed, "user-secret");
        JsonElement signature = Assert.Single(strings).Value;
        Assert.Equal("D:19700101000000Z", signature.GetProperty("M").GetString());
        Assert.Equal("Approved", signature.GetProperty("Reason").GetString());
        Assert.Equal("Denver", signature.GetProperty("Location").GetString());
        Assert.Equal("signer@example.com", signature.GetProperty("ContactInfo").GetString());
        Assert.Equal("Interop Leaf", signature.GetProperty("Name").GetString());

        bool pades = format == PdfSignatureFormat.PadesBaselineB;
        PdfsigAccepts(signed, root.RawData, "user-secret", wholeFile: true, signatureType: pades ? "ETSI.CAdES.detached" : "adbe.pkcs7.detached");
        string report = Validate(signed, root.RawData, "user-secret", "entire", pades ? "pades-b" : null);
        Assert.Contains("intact=true", report, StringComparison.Ordinal);
        if (pades)
        {
            Assert.Contains("pades=B-B", report, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
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

    // W-14: pdfsig must accept the file's one signature as the library writes it: adbe.pkcs7.detached unless named (W-71),
    // SHA-256 unless named, over exactly the /ByteRange in the file, valid, and trusted only when the root is configured.
    private static List<string> PdfsigAccepts(byte[] signed, byte[]? root, string? password, bool wholeFile, string signingHash = "SHA-256", string signatureType = "adbe.pkcs7.detached")
    {
        (int exitCode, string report) = Pdfsig.Verify(signed, root == null ? Array.Empty<byte[]>() : new[] { root }, password);
        List<List<string>> signatures = Pdfsig.Signatures(report);
        Assert.True(exitCode == 0 && signatures.Count == 1, report);
        List<string> signature = signatures[0];
        Assert.Contains("Signature Type: " + signatureType, signature);
        Assert.Contains("Signing Hash Algorithm: " + signingHash, signature);
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

    // W-70: pyHanko and pdfsig both report the digest, and pyHanko the signature mechanism, that the CMS names.
    private static void AssertVerifiersAccept(byte[] signed, byte[] root, string? checks, string keyKind, PdfDigestAlgorithm digestAlgorithm, string commonName, string signatureType = "adbe.pkcs7.detached")
    {
        string bits = digestAlgorithm switch
        {
            PdfDigestAlgorithm.Sha256 => "256",
            PdfDigestAlgorithm.Sha384 => "384",
            _ => "512",
        };
        string report = Validate(signed, root, null, "entire", checks);
        Assert.Contains("md_algorithm=sha" + bits + "\n", report.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("mechanism=sha" + bits + (keyKind == TestKeys.Rsa ? "_rsa" : "_ecdsa") + "\n", report.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("Signer Certificate Common Name: " + commonName, PdfsigAccepts(signed, root, null, wholeFile: true, "SHA-" + bits, signatureType));
    }

    private static byte[] Sign(byte[] pdf, IPdfExternalSigner signer, PdfDigestAlgorithm digestAlgorithm, ITimestampAuthority? authority, PdfSignatureFormat format = PdfSignatureFormat.AdbePkcs7Detached)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, leaveOpen: true);
        var destination = new MemoryStream();
        document.Sign(
            destination,
            new PdfSignatureOptions { ReservedContentsBytes = 32768, SigningTime = authority == null ? TestCompat.UnixEpoch : DateTimeOffset.UtcNow, DigestAlgorithm = digestAlgorithm, Format = format },
            new CmsDetachedSignatureProvider(signer, authority),
            leaveOpen: true);
        return destination.ToArray();
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

    private static async Task<byte[]> SignAsync(byte[] pdf, string? password, IPdfExternalSigner signer, ITimestampAuthority? authority, DateTimeOffset signingTime)
    {
        using var source = new MemoryStream(pdf, writable: false);
        using var document = PdfDocument.Open(source, password, leaveOpen: true);
        var destination = new MemoryStream();
        await document.SignAsync(
            destination,
            new PdfSignatureOptions { ReservedContentsBytes = 32768, SigningTime = signingTime },
            authority == null
                ? new CmsDetachedSignatureProvider(signer)
                : new CmsDetachedSignatureProvider(signer, authority),
            leaveOpen: true);
        return destination.ToArray();
    }

    // qpdf's 40-bit RC4 is revision 2, 128-bit RC4 revision 3, AES-128 revision 4, and AES-256 revision 6.
    private static byte[] QpdfEncrypt(byte[] pdf, string cipher)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string input = Path.Combine(directory, "base.pdf");
            string output = Path.Combine(directory, "encrypted.pdf");
            File.WriteAllBytes(input, pdf);
            var args = new List<string> { "--allow-weak-crypto", "--encrypt", "user-secret", "owner-secret" };
            args.AddRange(cipher switch
            {
                "40" => new[] { "40" },
                "128-rc4" => new[] { "128", "--use-aes=n" },
                "128-aes" => new[] { "128", "--use-aes=y" },
                _ => new[] { "256" },
            });
            args.AddRange(new[] { "--", input, output });
            RunChecked(CorpusManifest.Qpdf(), args.ToArray());
            return File.ReadAllBytes(output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // pypdf's reading of each signature dictionary's text strings (Fixtures/signature_strings.py).
    private static Dictionary<string, JsonElement> SignatureStrings(byte[] pdf, string password)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "signed.pdf");
            File.WriteAllBytes(path, pdf);
            using Process process = StartPython(ExternalTool.Fixture("signature_strings.py"), path, password);
            using JsonDocument document = JsonDocument.Parse(ExternalTool.Output(process));
            return document.RootElement.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
        return ExternalTool.Start(ExternalTool.Python(), args);
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

    private static Pkcs11Signer GenerateRsa(ISession session)
    {
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
            PrivateKeyTemplate(attributes),
            out IObjectHandle publicKey,
            out IObjectHandle privateKey);
        byte[] modulus = session.GetAttributeValue(publicKey, new List<CKA> { CKA.CKA_MODULUS })[0].GetValueAsByteArray();
        byte[] exponent = session.GetAttributeValue(publicKey, new List<CKA> { CKA.CKA_PUBLIC_EXPONENT })[0].GetValueAsByteArray();
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = TrimInteger(modulus), Exponent = TrimInteger(exponent) });
        return new Pkcs11Signer(session, privateKey, CKM.CKM_RSA_PKCS, rsa);
    }

    private static Pkcs11Signer GenerateEcdsa(ISession session, string keyKind)
    {
        bool p256 = keyKind == TestKeys.P256;
        IObjectAttributeFactory attributes = session.Factories.ObjectAttributeFactory;
        using IMechanism generate = session.Factories.MechanismFactory.Create(CKM.CKM_EC_KEY_PAIR_GEN);
        session.GenerateKeyPair(
            generate,
            new List<IObjectAttribute>
            {
                attributes.Create(CKA.CKA_TOKEN, true),
                attributes.Create(CKA.CKA_VERIFY, true),
                attributes.Create(CKA.CKA_EC_PARAMS, DerCodec.Oid(p256 ? "1.2.840.10045.3.1.7" : "1.3.132.0.34")),
                attributes.Create(CKA.CKA_LABEL, "pdf"),
            },
            PrivateKeyTemplate(attributes),
            out IObjectHandle publicKey,
            out IObjectHandle privateKey);

        // CKA_EC_POINT is a DER OCTET STRING around the uncompressed point 04 || X || Y.
        byte[] point = DerCodec.ReadTlv(session.GetAttributeValue(publicKey, new List<CKA> { CKA.CKA_EC_POINT })[0].GetValueAsByteArray()).Content;
        int half = p256 ? 32 : 48;
        Assert.Equal(1 + (half * 2), point.Length);
        Assert.Equal(0x04, point[0]);
        var x = new byte[half];
        var y = new byte[half];
        Buffer.BlockCopy(point, 1, x, 0, half);
        Buffer.BlockCopy(point, 1 + half, y, 0, half);
        var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = p256 ? ECCurve.NamedCurves.nistP256 : ECCurve.NamedCurves.nistP384,
            Q = new ECPoint { X = x, Y = y },
        });
        return new Pkcs11Signer(session, privateKey, CKM.CKM_ECDSA, ecdsa);
    }

    private static List<IObjectAttribute> PrivateKeyTemplate(IObjectAttributeFactory attributes)
    {
        return new List<IObjectAttribute>
        {
            attributes.Create(CKA.CKA_TOKEN, true),
            attributes.Create(CKA.CKA_PRIVATE, true),
            attributes.Create(CKA.CKA_SENSITIVE, true),
            attributes.Create(CKA.CKA_SIGN, true),
            attributes.Create(CKA.CKA_LABEL, "pdf"),
        };
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

    private static X509Certificate2 CreateLeaf(string commonName, AsymmetricAlgorithm key, X509Certificate2? issuer)
    {
        CertificateRequest request = TestKeys.Request("CN=" + commonName, key);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        if (issuer == null)
        {
            return request.CreateSelfSigned(notBefore, DateTimeOffset.UtcNow.AddYears(1));
        }

        // The issuers are RSA. Naming the generator lets an RSA issuer sign an ECDSA leaf.
        using RSA issuerKey = issuer.GetRSAPrivateKey() ?? throw new InvalidOperationException("The issuer has no RSA private key.");
        return request.Create(issuer.SubjectName, X509SignatureGenerator.CreateForRSA(issuerKey, RSASignaturePadding.Pkcs1), notBefore, issuer.NotAfter.AddMinutes(-1), Serial());
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

        public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(_key.SignHash(hash.ToArray(), hashAlgorithm, RSASignaturePadding.Pkcs1));
        }
    }

    // Completes asynchronously, as a cloud key service does.
    private sealed class DeferredSigner : IPdfExternalSigner
    {
        private readonly IPdfExternalSigner _inner;

        public DeferredSigner(IPdfExternalSigner inner)
        {
            _inner = inner;
        }

        public byte[] CertificateDer => _inner.CertificateDer;

        public IReadOnlyList<byte[]> AdditionalCertificates => _inner.AdditionalCertificates;

        public async ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return await _inner.SignHashAsync(hash, hashAlgorithm, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class DeferredAuthority : ITimestampAuthority
    {
        private readonly ITimestampAuthority _inner;

        public DeferredAuthority(ITimestampAuthority inner)
        {
            _inner = inner;
        }

        public async ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return await _inner.GetTimeStampTokenAsync(timeStampReqDer, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class Pkcs11Signer : IPdfExternalSigner
    {
        private readonly ISession _session;
        private readonly IObjectHandle _privateKey;
        private readonly CKM _mechanism;

        public Pkcs11Signer(ISession session, IObjectHandle privateKey, CKM mechanism, AsymmetricAlgorithm publicKey)
        {
            _session = session;
            _privateKey = privateKey;
            _mechanism = mechanism;
            PublicKey = publicKey;
            CertificateDer = Array.Empty<byte>();
            Chain = Array.Empty<byte[]>();
        }

        public AsymmetricAlgorithm PublicKey { get; }

        public byte[] Certificate { set => CertificateDer = value; }

        public byte[] CertificateDer { get; private set; }

        public IReadOnlyList<byte[]> Chain { get; set; }

        public IReadOnlyList<byte[]> AdditionalCertificates => Chain;

        // CKM_RSA_PKCS signs the DigestInfo the host builds. CKM_ECDSA signs the raw digest and returns r || s.
        public ValueTask<byte[]> SignHashAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName hashAlgorithm, CancellationToken cancellationToken)
        {
            byte[] input = hash.ToArray();
            if (_mechanism == CKM.CKM_RSA_PKCS)
            {
                string oid = hashAlgorithm == HashAlgorithmName.SHA512
                    ? "2.16.840.1.101.3.4.2.3"
                    : hashAlgorithm == HashAlgorithmName.SHA384 ? "2.16.840.1.101.3.4.2.2" : "2.16.840.1.101.3.4.2.1";
                input = DerCodec.Sequence(DerCodec.Algorithm(oid), DerCodec.OctetString(input));
            }

            using IMechanism mechanism = _session.Factories.MechanismFactory.Create(_mechanism);
            return new ValueTask<byte[]>(_session.Sign(mechanism, _privateKey, input));
        }
    }
}
