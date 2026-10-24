namespace jaytwo.Pdf.Internal;

internal enum PdfTokenKind
{
    End,
    Integer,
    Real,
    Name,
    LiteralString,
    HexString,
    Keyword,
    ArrayStart,
    ArrayEnd,
    DictStart,
    DictEnd,
}
