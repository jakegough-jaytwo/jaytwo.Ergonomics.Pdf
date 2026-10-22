using System;
using System.IO;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// The byte ranges a signature provider must cover. The /Contents gap is not included.
/// </summary>
public sealed class PdfSignatureContext
{
    private readonly Func<Stream> _openSignedBytes;

    internal PdfSignatureContext(PdfByteRange byteRange, int reservedContentsBytes, DateTimeOffset? signingTime, PdfDigestAlgorithm digestAlgorithm, PdfSignatureFormat format, Func<Stream> openSignedBytes)
    {
        ByteRange = byteRange;
        ReservedContentsBytes = reservedContentsBytes;
        SigningTime = signingTime;
        DigestAlgorithm = digestAlgorithm;
        Format = format;
        _openSignedBytes = openSignedBytes;
    }

    /// <summary>
    /// Gets the absolute byte range that will be written into the signature dictionary.
    /// </summary>
    public PdfByteRange ByteRange { get; }

    /// <summary>
    /// Gets the number of bytes reserved for the signature contents.
    /// </summary>
    public int ReservedContentsBytes { get; }

    /// <summary>
    /// Gets the signing time stored in the signature dictionary /M. Null for a document timestamp.
    /// </summary>
    public DateTimeOffset? SigningTime { get; }

    /// <summary>
    /// Gets the digest from <see cref="PdfSignatureOptions.DigestAlgorithm"/>. Always SHA-256 for a document timestamp.
    /// </summary>
    public PdfDigestAlgorithm DigestAlgorithm { get; }

    /// <summary>
    /// Gets the format from <see cref="PdfSignatureOptions.Format"/>. A CMS provider writes the signed attributes it names, because the
    /// /SubFilter already declares it. Always <see cref="PdfSignatureFormat.AdbePkcs7Detached"/> for a document timestamp.
    /// </summary>
    public PdfSignatureFormat Format { get; }

    /// <summary>
    /// Opens a read-only stream of the bytes that the signature covers.
    /// </summary>
    /// <returns>A stream positioned at the start of the signed bytes. The caller disposes it.</returns>
    public Stream OpenSignedBytes()
    {
        return _openSignedBytes();
    }
}
