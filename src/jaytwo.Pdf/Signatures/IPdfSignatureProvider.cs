using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Signatures;

/// <summary>
/// Builds the bytes stored in a signature dictionary /Contents value.
/// </summary>
public interface IPdfSignatureProvider
{
    /// <summary>
    /// Gets the signature dictionary kind this provider produces.
    /// </summary>
    PdfSignatureKind Kind { get; }

    /// <summary>
    /// Creates the signature contents for the byte ranges in <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The signed ranges and the reserved contents size.</param>
    /// <param name="cancellationToken">Canceled when the caller of the async sign cancels. The synchronous sign passes <see cref="CancellationToken.None"/> and waits for the result.</param>
    /// <returns>The contents bytes, before hexadecimal encoding and zero padding.</returns>
    ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken);
}
