namespace jaytwo.Ergonomics.Pdf.Internal;

internal readonly struct XrefEntry
{
    public XrefEntry(byte type, long field1, int field2)
    {
        Type = type;
        Field1 = field1;
        Field2 = field2;
    }

    public byte Type { get; }

    public long Field1 { get; }

    public int Field2 { get; }
}
