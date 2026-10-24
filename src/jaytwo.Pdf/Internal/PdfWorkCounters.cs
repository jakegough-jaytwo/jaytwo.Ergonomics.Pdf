using System;
using System.Threading;

namespace jaytwo.Pdf.Internal;

// W-167 and W-169: the work one document has done since it opened, for measurement by the tests. A document has counters
// only when it opens inside a Collect scope; an ordinary document has none, and every counting site tests for null.
internal sealed class PdfWorkCounters
{
    private static readonly AsyncLocal<bool> CollectingFlag = new AsyncLocal<bool>();

    // Each filter applied to a stream read whole, and the bytes it produced, intermediate stages included.
    internal long FilterStageDecodes { get; private set; }

    internal long FilterStageBytes { get; private set; }

    // Each time an object stream is decoded and parsed, including a re-decode after a release.
    internal long ObjectStreamParses { get; private set; }

    // Object streams released, and the cached objects released with them.
    internal long ObjectStreamEvictions { get; private set; }

    internal long CachedObjectEvictions { get; private set; }

    // Objects parsed on a cache miss, and indirect references followed.
    internal long ObjectLoads { get; private set; }

    internal long ReferenceResolutions { get; private set; }

    // Page tree and field tree nodes visited by the walks.
    internal long PageNodeVisits { get; private set; }

    internal long FieldNodeVisits { get; private set; }

    // Documents opened on this logical flow until the scope is disposed get counters.
    internal static IDisposable Collect()
    {
        CollectingFlag.Value = true;
        return new Scope();
    }

    internal static PdfWorkCounters? ForNewDocument()
    {
        return CollectingFlag.Value ? new PdfWorkCounters() : null;
    }

    internal void CountFilterStage(int bytes)
    {
        FilterStageDecodes++;
        FilterStageBytes += bytes;
    }

    internal void CountObjectStreamParse() => ObjectStreamParses++;

    internal void CountObjectStreamEviction() => ObjectStreamEvictions++;

    internal void CountCachedObjectEviction() => CachedObjectEvictions++;

    internal void CountObjectLoad() => ObjectLoads++;

    internal void CountReferenceResolution() => ReferenceResolutions++;

    internal void CountPageNodeVisit() => PageNodeVisits++;

    internal void CountFieldNodeVisit() => FieldNodeVisits++;

    private sealed class Scope : IDisposable
    {
        public void Dispose()
        {
            CollectingFlag.Value = false;
        }
    }
}
