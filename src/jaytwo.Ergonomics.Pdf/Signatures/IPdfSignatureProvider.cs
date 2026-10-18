namespace jaytwo.Ergonomics.Pdf.Signatures;

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
    /// <returns>The contents bytes, before hexadecimal encoding and zero padding.</returns>
    byte[] CreateContents(PdfSignatureContext context);
}
