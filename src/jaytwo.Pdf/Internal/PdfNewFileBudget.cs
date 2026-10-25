using System;

namespace jaytwo.Pdf.Internal;

internal sealed class PdfNewFileBudget
{
    private readonly PdfNewFileLimits _limits;
    private int _mappedObjects;
    private long _structuralBytes;

    internal PdfNewFileBudget(PdfNewFileLimits limits)
    {
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
    }

    internal int TraversalDepth => _limits.TraversalDepth;

    internal void CountMappedObject()
    {
        _mappedObjects = checked(_mappedObjects + 1);
        if (_mappedObjects > _limits.MappedObjects)
        {
            throw new PdfResourceLimitException("mapped objects");
        }

        AddStructuralBytes(48);
    }

    internal void CountOutputPages(int count)
    {
        if (count < 0 || count > _limits.OutputPages)
        {
            throw new PdfResourceLimitException("output pages");
        }
    }

    internal void AddStructuralBytes(long count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        try
        {
            _structuralBytes = checked(_structuralBytes + count);
        }
        catch (OverflowException)
        {
            throw new PdfResourceLimitException("structural memory");
        }

        if (_structuralBytes > _limits.StructuralBytes)
        {
            throw new PdfResourceLimitException("structural memory");
        }
    }
}
