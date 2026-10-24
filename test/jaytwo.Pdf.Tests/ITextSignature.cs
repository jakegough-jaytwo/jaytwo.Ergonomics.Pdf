namespace jaytwo.Pdf.Tests;

// iText's verdict on one signature: PdfPKCS7.VerifySignatureIntegrityAndAuthenticity recomputes the digest over the
// signed ranges and checks the signature value against the signer certificate. No trust or revocation is checked.
internal sealed class ITextSignature
{
    public ITextSignature(bool intact, bool coversWholeDocument, string? reason)
    {
        Intact = intact;
        CoversWholeDocument = coversWholeDocument;
        Reason = reason;
    }

    public bool Intact { get; }

    public bool CoversWholeDocument { get; }

    public string? Reason { get; }
}
