namespace jaytwo.Pdf.Internal;

internal readonly struct PdfToken
{
    public PdfToken(PdfTokenKind kind, string text, byte[] bytes, long integer, double real)
    {
        Kind = kind;
        Text = text;
        Bytes = bytes;
        Integer = integer;
        Real = real;
    }

    public PdfTokenKind Kind { get; }

    public string Text { get; }

    public byte[] Bytes { get; }

    public long Integer { get; }

    public double Real { get; }
}
