namespace jaytwo.Pdf.Internal;

internal readonly struct RevisionSection
{
    public RevisionSection(long offset, long endOfSection)
    {
        Offset = offset;
        EndOfSection = endOfSection;
    }

    public long Offset { get; }

    public long EndOfSection { get; }
}
