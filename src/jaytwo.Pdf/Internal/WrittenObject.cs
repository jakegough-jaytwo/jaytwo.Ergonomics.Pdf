namespace jaytwo.Pdf.Internal;

internal readonly struct WrittenObject
{
    public WrittenObject(int id, long offset, int generation)
    {
        Id = id;
        Offset = offset;
        Generation = generation;
    }

    public int Id { get; }

    public long Offset { get; }

    public int Generation { get; }
}
