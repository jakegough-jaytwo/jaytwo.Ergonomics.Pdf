using System;
using System.Diagnostics;
using System.IO;

namespace jaytwo.Ergonomics.Pdf.Tests;

// A local RFC 3161 authority: OpenSSL 3 `ts -reply` over a throwaway PKI that CreatePki writes to a directory.
internal sealed class OpenSslTimestampAuthority : ITimestampAuthority
{
    private readonly string _openssl;
    private readonly string _directory;

    public OpenSslTimestampAuthority(string openssl, string directory)
    {
        _openssl = openssl;
        _directory = directory;
        Token = Array.Empty<byte>();
    }

    public string RequestPath => Path.Combine(_directory, "request.tsq");

    public byte[] Token { get; private set; }

    public static string Locate()
    {
        return ExternalTool.Locate("JAYTWO_OPENSSL", ExternalTool.Executable("openssl"));
    }

    // Writes root.pem (the TSA's root), other.pem (an unrelated root), the TSA key and certificate, and ts.cnf.
    public static void CreatePki(string openssl, string directory)
    {
        string config = Path.Combine(directory, "pki.cnf");
        File.WriteAllText(
            config,
            "[req]\ndistinguished_name = dn\nprompt = no\n[dn]\nCN = unused\n"
            + "[v3_ca]\nbasicConstraints = critical,CA:TRUE\nkeyUsage = critical,keyCertSign,cRLSign\nsubjectKeyIdentifier = hash\n"
            + "[v3_tsa]\nbasicConstraints = critical,CA:FALSE\nkeyUsage = critical,digitalSignature\n"
            + "extendedKeyUsage = critical,timeStamping\nsubjectKeyIdentifier = hash\nauthorityKeyIdentifier = keyid\n");
        Root(openssl, directory, config, "root", "/CN=OpenSSL Test Root");
        Root(openssl, directory, config, "other", "/CN=OpenSSL Untrusted Root");
        Checked(openssl, "req", "-new", "-newkey", "rsa:2048", "-nodes", "-config", config, "-subj", "/CN=OpenSSL Test TSA", "-keyout", Path.Combine(directory, "tsa.key"), "-out", Path.Combine(directory, "tsa.csr"));
        Checked(openssl, "x509", "-req", "-in", Path.Combine(directory, "tsa.csr"), "-CA", Path.Combine(directory, "root.pem"), "-CAkey", Path.Combine(directory, "root.key"), "-set_serial", "2", "-days", "2", "-extfile", config, "-extensions", "v3_tsa", "-out", Path.Combine(directory, "tsa.pem"));

        string slashed = directory.Replace('\\', '/');
        File.WriteAllText(Path.Combine(directory, "serial"), "01\n");
        File.WriteAllText(
            Path.Combine(directory, "ts.cnf"),
            "[tsa]\ndefault_tsa = tsa1\n[tsa1]\n"
            + "serial = " + slashed + "/serial\n"
            + "signer_cert = " + slashed + "/tsa.pem\n"
            + "signer_key = " + slashed + "/tsa.key\n"
            + "signer_digest = sha256\ndefault_policy = 1.2.3.4.1\ndigests = sha256\ness_cert_id_alg = sha256\naccuracy = secs:1\n");
    }

    public static string Checked(string fileName, params string[] args)
    {
        using Process process = ExternalTool.Start(fileName, args);
        return ExternalTool.Output(process);
    }

    public byte[] GetTimeStampToken(ReadOnlySpan<byte> timeStampReqDer)
    {
        string tokenPath = Path.Combine(_directory, "token.tst");
        File.WriteAllBytes(RequestPath, timeStampReqDer.ToArray());
        Checked(_openssl, "ts", "-reply", "-config", Path.Combine(_directory, "ts.cnf"), "-queryfile", RequestPath, "-token_out", "-out", tokenPath);
        Token = File.ReadAllBytes(tokenPath);
        return Token;
    }

    private static void Root(string openssl, string directory, string config, string name, string subject)
    {
        Checked(openssl, "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-config", config, "-extensions", "v3_ca", "-subj", subject, "-days", "2", "-keyout", Path.Combine(directory, name + ".key"), "-out", Path.Combine(directory, name + ".pem"));
    }
}
