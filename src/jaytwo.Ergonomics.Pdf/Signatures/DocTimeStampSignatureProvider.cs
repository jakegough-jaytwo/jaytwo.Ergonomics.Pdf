using System;
using System.IO;
using System.Security.Cryptography;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Builds a document timestamp by hashing the signed ranges and asking a timestamp authority.
/// </summary>
public sealed class DocTimeStampSignatureProvider : IPdfSignatureProvider
{
    private readonly ITimestampAuthority _authority;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocTimeStampSignatureProvider"/> class.
    /// </summary>
    /// <param name="authority">The timestamp authority.</param>
    public DocTimeStampSignatureProvider(ITimestampAuthority authority)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
    }

    /// <summary>
    /// Gets <see cref="PdfSignatureKind.DocTimeStamp"/>.
    /// </summary>
    public PdfSignatureKind Kind => PdfSignatureKind.DocTimeStamp;

    /// <summary>
    /// Creates the time-stamp token for the signed byte ranges.
    /// </summary>
    /// <param name="context">The signed ranges.</param>
    /// <returns>The TimeStampToken bytes returned by the authority.</returns>
    public byte[] CreateContents(PdfSignatureContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        byte[] digest;
        using (Stream signed = context.OpenSignedBytes())
        using (SHA256 sha = SHA256.Create())
        {
            digest = sha.ComputeHash(signed);
        }

        byte[] request = Rfc3161TimeStampRequest.Create(digest);
        return _authority.GetTimeStampToken(request);
    }
}
