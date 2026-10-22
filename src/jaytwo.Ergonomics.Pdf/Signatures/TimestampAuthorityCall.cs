using System;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Ergonomics.Pdf.Signatures;

/// <summary>
/// Calls an <see cref="ITimestampAuthority"/> for the providers. A host authority's exception is replaced so its
/// message, data, and inner exception do not leave the library, as the signer's are. <see cref="HttpTimestampAuthority"/>
/// is library code whose exceptions are documented, so they pass through.
/// </summary>
internal static class TimestampAuthorityCall
{
    internal static async ValueTask<byte[]?> GetTokenAsync(ITimestampAuthority authority, byte[] request, CancellationToken cancellationToken)
    {
        if (authority is HttpTimestampAuthority)
        {
            return await authority.GetTimeStampTokenAsync(request, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await authority.GetTimeStampTokenAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception)
        {
            throw new PdfTimestampException("The timestamp authority failed.");
        }
    }
}
