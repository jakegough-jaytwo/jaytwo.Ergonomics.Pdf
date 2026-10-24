namespace jaytwo.Pdf.Internal;

// W-167: the work one document has done since it opened, for measurement by the tests. Counting changes no behavior.
internal sealed class PdfWorkCounters
{
    // Each filter applied to a stream read whole, and the bytes it produced, intermediate stages included.
    internal long FilterStageDecodes { get; set; }

    internal long FilterStageBytes { get; set; }

    // Each time an object stream is decoded and parsed, including a re-decode after a release.
    internal long ObjectStreamParses { get; set; }

    // Object streams released, and the cached objects released with them.
    internal long ObjectStreamEvictions { get; set; }

    internal long CachedObjectEvictions { get; set; }

    // Objects parsed on a cache miss, and indirect references followed.
    internal long ObjectLoads { get; set; }

    internal long ReferenceResolutions { get; set; }

    // Page tree and field tree nodes visited by the walks.
    internal long PageNodeVisits { get; set; }

    internal long FieldNodeVisits { get; set; }
}
