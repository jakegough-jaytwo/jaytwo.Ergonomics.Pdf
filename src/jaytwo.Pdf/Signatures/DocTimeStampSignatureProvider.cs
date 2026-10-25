using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Signatures;

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
    /// <param name="cancellationToken">Passed to the authority.</param>
    /// <returns>The TimeStampToken bytes returned by the authority.</returns>
    /// <exception cref="PdfSignatureTooLargeException">The token is larger than <see cref="PdfSignatureContext.ReservedContentsBytes"/>. It is not parsed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    /// <exception cref="PdfTimestampException">A host authority threw; the message is generic, and the exception has no inner exception. Or the time-stamp token does not match the request by message imprint, digest algorithm, or nonce, or it does not include the TSA signing certificate that its ESS signing-certificate attribute identifies. The request sets certReq. Inclusion is checked; the certificate chain and trust are not.</exception>
    public async ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[] digest;
        using (Stream signed = context.OpenSignedBytes())
        using (SHA256 sha = SHA256.Create())
        {
            digest = sha.ComputeHash(signed);
        }

        byte[] nonce = Rfc3161TimeStampRequest.NewNonce();
        byte[]? request = null;
        try
        {
            request = Rfc3161TimeStampRequest.Create(digest, nonce);
            byte[]? token = await TimestampAuthorityCall.GetTokenAsync(_authority, request, cancellationToken).ConfigureAwait(false);
            if (token != null && token.Length > context.ReservedContentsBytes)
            {
                throw new PdfSignatureTooLargeException(token.Length, context.ReservedContentsBytes);
            }

            Rfc3161TimeStampToken.RequireMatch(token, digest, nonce);
            return token!;
        }
        finally
        {
            Array.Clear(digest, 0, digest.Length);
            Array.Clear(nonce, 0, nonce.Length);
            if (request != null)
            {
                Array.Clear(request, 0, request.Length);
            }
        }
    }
}
