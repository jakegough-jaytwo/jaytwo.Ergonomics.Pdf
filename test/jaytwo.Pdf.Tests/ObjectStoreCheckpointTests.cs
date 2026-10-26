using System.Linq;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

public sealed class ObjectStoreCheckpointTests
{
    [Fact]
    public void restore_returns_the_store_owned_state_and_keeps_edits_pending_at_the_checkpoint()
    {
        using PdfObjectStore store = Open();
        PdfObject.Dict pending = (PdfObject.Dict)store.EditAny(store.RootId);
        pending.Set("Pending", PdfObject.Num.FromLong(1));
        int size = store.Size;
        int? infoId = store.InfoId;
        PdfObject.Arr? trailerId = store.TrailerId;

        store.BeginBackup();
        PdfObject.Dict edited = (PdfObject.Dict)store.EditAny(store.RootId);
        edited.Set("Failed", PdfObject.Num.FromLong(2));
        int allocated = store.Allocate(PdfObject.Num.FromLong(3));
        store.InfoId = allocated;
        store.InfoGen = 7;
        store.TrailerId = new PdfObject.Arr();
        store.Restore();

        PdfObject.Dict restored = store.CatalogDict();
        Assert.NotNull(restored.Get("Pending"));
        Assert.Null(restored.Get("Failed"));
        Assert.Equal(size, store.Size);
        Assert.False(store.Xref.ContainsKey(allocated));
        Assert.Equal(infoId, store.InfoId);
        Assert.Equal(0, store.InfoGen);
        Assert.Same(trailerId, store.TrailerId);
        Assert.Equal(new[] { store.RootId }, store.Dirty.ToArray());
    }

    [Fact]
    public void ending_the_backup_keeps_the_edits_and_a_later_restore_changes_nothing()
    {
        using PdfObjectStore store = Open();
        store.BeginBackup();
        ((PdfObject.Dict)store.EditAny(store.RootId)).Set("Kept", PdfObject.Num.FromLong(1));
        store.EndBackup();

        store.Restore();

        Assert.NotNull(store.CatalogDict().Get("Kept"));
        Assert.Contains(store.RootId, store.Dirty);
    }

    private static PdfObjectStore Open()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 0 /Kids [] >>");
        var store = new PdfObjectStore(new TestPdfSource(builder.Finish()), true, 1);
        new PdfDocumentLoader(store).InitializeAsync(null, false, default).GetAwaiter().GetResult();
        return store;
    }
}
