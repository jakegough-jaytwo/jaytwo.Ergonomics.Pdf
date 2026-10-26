using System.Collections.Generic;

namespace jaytwo.Pdf.Internal;

// The store-owned values an operation's failure puts back, beside the per-object backups: the object count, the
// information object, the trailer ID, and the edits that were already pending when the operation began.
internal sealed class PdfObjectStoreCheckpoint
{
    internal PdfObjectStoreCheckpoint(PdfObjectStore store)
    {
        Size = store.Size;
        InfoId = store.InfoId;
        InfoGen = store.InfoGen;
        TrailerId = store.TrailerId;
        Dirty = new HashSet<int>(store.Dirty);
    }

    internal int Size { get; }

    internal int? InfoId { get; }

    internal int InfoGen { get; }

    internal PdfObject.Arr? TrailerId { get; }

    internal HashSet<int> Dirty { get; }
}
