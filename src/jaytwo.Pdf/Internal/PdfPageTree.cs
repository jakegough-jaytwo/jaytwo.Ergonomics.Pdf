using System;
using System.Collections.Generic;

namespace jaytwo.Pdf.Internal;

// The page tree: pages with their inherited boxes and rotation, and setting a page's rotation.
internal sealed class PdfPageTree
{
    private readonly PdfObjectStore _store;
    private readonly PdfWriteGate _gate;

    internal PdfPageTree(PdfObjectStore store, PdfWriteGate gate)
    {
        _store = store;
        _gate = gate;
    }

    internal IReadOnlyList<PdfPage> ListPages()
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        List<PageInfo> pages = GetPages();
        var result = new List<PdfPage>(pages.Count);
        for (int i = 0; i < pages.Count; i++)
        {
            result.Add(new PdfPage(pages[i].ObjectId, i + 1, pages[i].MediaBox, pages[i].CropBox, pages[i].Rotate));
        }

        return result;
    }

    internal int SetPageRotation(int objectId, int rotation)
    {
        _store.CheckDisposed();
        if (rotation % 90 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rotation), "Page rotation must be a multiple of 90 degrees.");
        }

        int normalized = rotation % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        using var release = _store.ReleaseAfter();
        if (_store.Load(objectId) is not PdfObject.Dict page)
        {
            throw new PdfParseException(0, "The page object does not resolve to a dictionary.");
        }

        if (page.Get("Rotate") is PdfObject.Num && PdfValues.IntOf(page.Get("Rotate")) == normalized)
        {
            return normalized;
        }

        _gate.EnsureDocumentChange();
        if (!_gate.AllowBreakingSignatures && _gate.HasSignature())
        {
            throw new PdfSignatureConflictException("The document has a signature; page rotation changes page presentation, which validators report as a modification the signature does not permit. Set AllowBreakingSignatures to rotate anyway.");
        }

        // ISO 32000-1 Table 22: rotation is page assembly (bit 11 for R >= 3; bit 4 for R2).
        _gate.RequireAssemble();
        _gate.ApplyUsageRights(null, null);
        ((PdfObject.Dict)_store.EditAny(objectId)).Set("Rotate", PdfObject.Num.FromLong(normalized));
        return normalized;
    }

    internal List<PageInfo> GetPages()
    {
        var pages = new List<PageInfo>();
        PdfObject.Dict catalog = _store.CatalogDict();
        if (_store.ResolveMaybe(catalog.Get("Pages")) is not PdfObject.Dict root)
        {
            return pages;
        }

        int rootId = catalog.Get("Pages") is PdfObject.Ref reference ? reference.ObjectId : root.ObjectNumber;
        WalkPages(root, rootId, new PdfRect(0, 0, 0, 0), null, 0, pages, new HashSet<int>(), 0);
        return pages;
    }

    private void WalkPages(PdfObject.Dict node, int id, PdfRect media, PdfRect? crop, int rotate, List<PageInfo> pages, HashSet<int> seen, int depth)
    {
        _store.Work?.CountPageNodeVisit();
        if (!seen.Add(id))
        {
            throw new PdfParseException(0, "The page tree contains a cycle.");
        }

        if (depth > PdfObjectStore.MaxTraversalDepth)
        {
            throw new PdfParseException(0, "The page tree is too deep.");
        }

        if (_store.RectOf(node.Get("MediaBox")) is PdfRect box)
        {
            media = box;
        }

        if (_store.RectOf(node.Get("CropBox")) is PdfRect cropBox)
        {
            crop = cropBox;
        }

        if (node.Get("Rotate") != null)
        {
            rotate = PdfValues.IntOf(node.Get("Rotate"));
        }

        string? type = PdfValues.NameOf(node.Get("Type"));
        PdfObject.Arr? kids = _store.ResolveMaybe(node.Get("Kids")) as PdfObject.Arr;
        if (type == "Page" || (kids == null && type != "Pages"))
        {
            pages.Add(new PageInfo(id, media, crop ?? media, rotate));
            return;
        }

        if (kids == null)
        {
            return;
        }

        foreach (PdfObject kid in kids.Items)
        {
            if (kid is PdfObject.Ref reference && _store.Resolve(reference) is PdfObject.Dict child)
            {
                WalkPages(child, reference.ObjectId, media, crop, rotate, pages, seen, depth + 1);
            }
        }
    }
}
