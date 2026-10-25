using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace jaytwo.Pdf;

/// <summary>Aggregate annotation omissions from a successful new-file operation.</summary>
public sealed class PdfAssemblyResult
{
    private readonly Dictionary<PdfOmissionReason, long> _counts = new Dictionary<PdfOmissionReason, long>();

    internal PdfAssemblyResult()
    {
        Omissions = new ReadOnlyDictionary<PdfOmissionReason, long>(_counts);
    }

    /// <summary>Gets counts by reason, without URLs, objects, or per-page history.</summary>
    public IReadOnlyDictionary<PdfOmissionReason, long> Omissions { get; }

    internal void Omit(PdfOmissionReason reason)
    {
        _counts.TryGetValue(reason, out long count);
        _counts[reason] = checked(count + 1);
    }
}
