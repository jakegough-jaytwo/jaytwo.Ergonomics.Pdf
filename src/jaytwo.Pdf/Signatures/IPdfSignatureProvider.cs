using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Signatures;

/// <summary>
/// Builds the bytes stored in a signature dictionary /Contents value.
/// </summary>
/// <remarks>
/// A provider shall generate the requested signature format using the requested digest and exactly the signed PDF byte sequence supplied by the engine.
/// For a document timestamp, the returned RFC 3161 token shall have a SHA-256 messageImprint of those bytes.
/// The provider shall not substitute another format, digest, or input.
/// The engine does not independently verify arbitrary provider output for format, digest, byte coverage, certificate attributes, or signature policy.
/// </remarks>
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
