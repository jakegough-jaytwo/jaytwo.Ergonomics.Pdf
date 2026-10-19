using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Ergonomics.Pdf.Tests;

// Test-only shims for the net48 target. On net6.0 and net8.0 each member forwards to the
// .NET API the tests used before the net48 target existed, so behavior there is unchanged.
internal static class TestCompat
{
    public static DateTimeOffset UnixEpoch
    {
        get
        {
#if NETFRAMEWORK
            return new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
#else
            return DateTimeOffset.UnixEpoch;
#endif
        }
    }

    public static Encoding Latin1
    {
        get
        {
#if NETFRAMEWORK
            return Encoding.GetEncoding(28591);
#else
            return Encoding.Latin1;
#endif
        }
    }

    public static string ToHexString(byte[] data)
    {
#if NETFRAMEWORK
        return BitConverter.ToString(data).Replace("-", string.Empty);
#else
        return Convert.ToHexString(data);
#endif
    }

    public static byte[] FromHexString(string hex)
    {
#if NETFRAMEWORK
        if (hex.Length % 2 != 0)
        {
            throw new FormatException("The hex string has an odd length.");
        }

        var data = new byte[hex.Length / 2];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return data;
#else
        return Convert.FromHexString(hex);
#endif
    }

    public static byte[] Sha256(byte[] data)
    {
#if NETFRAMEWORK
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
#else
        return SHA256.HashData(data);
#endif
    }

    public static void FillRandom(byte[] data)
    {
#if NETFRAMEWORK
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(data);
#else
        RandomNumberGenerator.Fill(data);
#endif
    }

    public static long TotalAllocatedBytes()
    {
#if NETFRAMEWORK
        // The AppDomain counter is current as of the last collection, so collect before reading it.
        AppDomain.MonitoringIsEnabled = true;
        GC.Collect();
        return AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
#else
        return GC.GetTotalAllocatedBytes(true);
#endif
    }

    public static byte[] Zlib(byte[] data)
    {
        using var output = new MemoryStream();
#if NETFRAMEWORK
        // RFC 1950: CMF/FLG header, raw deflate, then the big-endian Adler-32 of the input.
        output.WriteByte(0x78);
        output.WriteByte(0xDA);
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }

        uint a = 1;
        uint b = 0;
        for (int i = 0; i < data.Length; i++)
        {
            a = (a + data[i]) % 65521;
            b = (b + a) % 65521;
        }

        uint adler = (b << 16) | a;
        output.WriteByte((byte)(adler >> 24));
        output.WriteByte((byte)(adler >> 16));
        output.WriteByte((byte)(adler >> 8));
        output.WriteByte((byte)adler);
#else
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }
#endif

        return output.ToArray();
    }

    public static string Pkcs8PrivateKeyPem(RSA key)
    {
#if NETFRAMEWORK
        RSAParameters parameters = key.ExportParameters(true);
        byte[] rsaPrivateKey = DerCodec.Sequence(
            DerCodec.Integer(0),
            DerCodec.Integer(parameters.Modulus),
            DerCodec.Integer(parameters.Exponent),
            DerCodec.Integer(parameters.D),
            DerCodec.Integer(parameters.P),
            DerCodec.Integer(parameters.Q),
            DerCodec.Integer(parameters.DP),
            DerCodec.Integer(parameters.DQ),
            DerCodec.Integer(parameters.InverseQ));
        byte[] privateKeyInfo = DerCodec.Sequence(
            DerCodec.Integer(0),
            DerCodec.Sequence(DerCodec.Oid("1.2.840.113549.1.1.1"), DerCodec.Null()),
            DerCodec.OctetString(rsaPrivateKey));
        string base64 = Convert.ToBase64String(privateKeyInfo);
        var builder = new StringBuilder("-----BEGIN PRIVATE KEY-----\n");
        for (int offset = 0; offset < base64.Length; offset += 64)
        {
            builder.Append(base64, offset, Math.Min(64, base64.Length - offset)).Append('\n');
        }

        builder.Append("-----END PRIVATE KEY-----");
        return builder.ToString();
#else
        return new string(PemEncoding.Write("PRIVATE KEY", key.ExportPkcs8PrivateKey()));
#endif
    }

    public static void AddArgument(ProcessStartInfo info, string argument)
    {
#if NETFRAMEWORK
        string quoted = QuoteArgument(argument);
        info.Arguments = info.Arguments.Length == 0 ? quoted : info.Arguments + " " + quoted;
#else
        info.ArgumentList.Add(argument);
#endif
    }

#if NETFRAMEWORK
    public static void Kill(this Process process, bool entireProcessTree)
    {
        if (entireProcessTree)
        {
            var info = new ProcessStartInfo("taskkill.exe", "/PID " + process.Id.ToString() + " /T /F")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process? taskkill = Process.Start(info);
            taskkill?.WaitForExit();
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill.
        }
    }

    public static string Replace(this string text, string oldValue, string newValue, StringComparison comparison)
    {
        var builder = new StringBuilder();
        int start = 0;
        int found;
        while ((found = text.IndexOf(oldValue, start, comparison)) >= 0)
        {
            builder.Append(text, start, found - start).Append(newValue);
            start = found + oldValue.Length;
        }

        return builder.Append(text, start, text.Length - start).ToString();
    }

    public static Task<byte[]> ReadAsByteArrayAsync(this HttpContent content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return content.ReadAsByteArrayAsync();
    }

    // The quoting rules of CommandLineToArgvW, as .NET applies them for ProcessStartInfo.ArgumentList.
    private static string QuoteArgument(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
        {
            return argument;
        }

        var builder = new StringBuilder("\"");
        int backslashes = 0;
        foreach (char c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            builder.Append('\\', c == '"' ? (backslashes * 2) + 1 : backslashes);
            builder.Append(c);
            backslashes = 0;
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
#endif
}
