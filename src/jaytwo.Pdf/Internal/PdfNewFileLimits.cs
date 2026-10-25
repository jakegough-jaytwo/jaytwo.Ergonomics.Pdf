namespace jaytwo.Pdf.Internal;

internal sealed class PdfNewFileLimits
{
    internal const int DefaultMappedObjects = 1000000;
    internal const int DefaultOutputPages = 1000000;
    internal const long DefaultStructuralBytes = 256L * 1024 * 1024;

    internal PdfNewFileLimits(
        int mappedObjects = DefaultMappedObjects,
        int traversalDepth = PdfObjectStore.MaxTraversalDepth,
        int outputPages = DefaultOutputPages,
        long structuralBytes = DefaultStructuralBytes)
    {
        MappedObjects = mappedObjects;
        TraversalDepth = traversalDepth;
        OutputPages = outputPages;
        StructuralBytes = structuralBytes;
    }

    internal int MappedObjects { get; }

    internal int TraversalDepth { get; }

    internal int OutputPages { get; }

    internal long StructuralBytes { get; }
}
