using System.Security.Cryptography;
using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Signatures;
using Org.BouncyCastle.X509;

namespace jaytwo.Ergonomics.Pdf.Comparison;

// The same key as ErgonomicsRsaSigner, behind iText's external-signature interface. iText passes the DER signed
// attributes, so this signs the message rather than a precomputed hash.
internal sealed class ITextRsaSignature : IExternalSignature
{
    public static IX509Certificate[] Chain()
    {
        return new IX509Certificate[] { new X509CertificateBC(new X509CertificateParser().ReadCertificate(SigningKey.Certificate)) };
    }

    public string GetDigestAlgorithmName()
    {
        return "SHA-256";
    }

    public string GetSignatureAlgorithmName()
    {
        return "RSA";
    }

    public ISignatureMechanismParams? GetSignatureMechanismParameters()
    {
        return null;
    }

    public byte[] Sign(byte[] message)
    {
        return SigningKey.Rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
