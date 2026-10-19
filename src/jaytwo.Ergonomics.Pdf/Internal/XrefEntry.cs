namespace jaytwo.Ergonomics.Pdf.Internal;

// One cross-reference entry packed into 8 bytes, so the merged map costs about 28 bytes a slot instead of 44.
// Bits 62-63 hold the type: 0 free, 1 in use, 2 compressed, and 3 for any other type, which resolves as null.
// Bits 22-61 hold field 1: a file offset (an offset outside 0 to 2^40 - 2 is stored as 2^40 - 1, past any real file)
// or an object-stream number. Bits 0-21 hold field 2, the generation or object-stream index. It saturates above any
// generation the format allows, and every negative value reads back as -1.
internal readonly struct XrefEntry
{
    private const int Field2Bits = 22;
    private const int Field1Bits = 40;
    private const long Field2Mask = (1L << Field2Bits) - 1;
    private const long Field1Mask = (1L << Field1Bits) - 1;

    private readonly long _packed;

    public XrefEntry(byte type, long field1, int field2)
    {
        long code = type <= 2 ? type : 3;
        long first;
        if (type == 1)
        {
            first = field1 < 0 || field1 >= Field1Mask ? Field1Mask : field1;
        }
        else
        {
            first = field1 & Field1Mask;
        }

        long second = field2 < 0 ? Field2Mask : field2 >= Field2Mask ? Field2Mask - 1 : field2;
        _packed = (code << (Field1Bits + Field2Bits)) | (first << Field2Bits) | second;
    }

    public byte Type => (byte)((ulong)_packed >> (Field1Bits + Field2Bits));

    public long Field1 => (_packed >> Field2Bits) & Field1Mask;

    public int Field2 => (_packed & Field2Mask) == Field2Mask ? -1 : (int)(_packed & Field2Mask);
}
