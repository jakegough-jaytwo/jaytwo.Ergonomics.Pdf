using System;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Signatures;

/// <summary>
/// Returns an RFC 3161 time-stamp token for a time-stamp request.
/// When <see cref="CmsDetachedSignatureProvider"/> or <see cref="DocTimeStampSignatureProvider"/> calls a host implementation,
/// an exception it throws is replaced with a generic <see cref="PdfTimestampException"/> that has no inner exception,
/// or with a fresh <see cref="OperationCanceledException"/> when the caller's token has been canceled.
/// The exceptions <see cref="HttpTimestampAuthority"/> documents pass through unchanged.
/// </summary>
public interface ITimestampAuthority
{
    /// <summary>
    /// Gets a time-stamp token for the supplied request.
    /// </summary>
    /// <param name="timeStampReqDer">A DER-encoded TimeStampReq.</param>
    /// <param name="cancellationToken">Canceled when the caller of the async sign cancels. The synchronous sign passes <see cref="CancellationToken.None"/>.</param>
    /// <returns>The TimeStampToken ContentInfo, not the status wrapper.</returns>
    ValueTask<byte[]> GetTimeStampTokenAsync(ReadOnlyMemory<byte> timeStampReqDer, CancellationToken cancellationToken);
}
