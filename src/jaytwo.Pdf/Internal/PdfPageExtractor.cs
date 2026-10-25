using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace jaytwo.Pdf.Internal;

internal sealed class PdfPageExtractor
{
    private static readonly string[] InheritedKeys = { "Resources", "MediaBox", "CropBox", "Rotate" };
    private static readonly HashSet<string> PageKeys = new HashSet<string>
    {
        "Type", "Resources", "MediaBox", "CropBox", "Rotate", "Contents", "UserUnit", "Group", "TrimBox", "BleedBox", "ArtBox",
    };

    private static readonly HashSet<string> AnnotationKeys = new HashSet<string>
    {
        "Type", "Subtype", "Rect", "Contents", "NM", "M", "F", "Border", "BS", "C", "CA", "T", "Subj", "CreationDate",
        "Name", "Open", "QuadPoints", "L", "LE", "IC", "Vertices", "InkList", "RD",
    };

    private readonly PdfObjectStore _store;
    private readonly PdfNewFileBudget _budget = new PdfNewFileBudget(new PdfNewFileLimits());
    private int _pageCount;
    private int _nodeCount;

    internal PdfPageExtractor(PdfObjectStore store)
    {
        _store = store;
    }

    internal void Extract(IEnumerable<int> pageNumbers, Stream destination)
    {
        if (pageNumbers == null)
        {
            throw new ArgumentNullException(nameof(pageNumbers));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (!destination.CanWrite || (destination.CanSeek && (destination.Length != 0 || destination.Position != 0)))
        {
            throw new ArgumentException("Extraction requires an empty writable destination.", nameof(destination));
        }

        if (_store.Security != null)
        {
            throw new PdfAssemblyNotSupportedException("Encrypted input is not yet supported for extraction.");
        }

        var order = new List<int>();
        var selected = new Dictionary<int, PdfObject.Dict>();
        foreach (int number in pageNumbers)
        {
            _budget.CountOutputPages(order.Count + 1);
            _budget.AddStructuralBytes(48);
            if (number < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumbers), "Page numbers must be positive and 1-based.");
            }

            order.Add(number);
            selected[number] = null!;
        }

        PdfObject.Dict catalog = _store.CatalogDict();
        var inspection = new PdfAssemblyInspection(_store, _budget);
        inspection.Catalog(catalog);
        Walk(catalog.Get("Pages"), new PdfObject.Dict(), selected, new HashSet<int>(), 0);
        foreach (int number in order)
        {
            if (number > _pageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumbers), "A selected page exceeds the source page count.");
            }
        }

        foreach (PdfObject.Dict page in selected.Values)
        {
            inspection.Page(page);
        }

        var writer = new PdfNewFileWriter(_budget);
        var copier = new PdfTypedObjectCopier(writer, _budget, Policy);
        var pages = new List<PdfObject.Ref>(order.Count);
        foreach (int number in order)
        {
            PdfObject.Dict sourcePage = selected[number];
            var page = (PdfObject.Dict)copier.CopyDirect(_store, sourcePage, "page");
            PdfObject.Ref pageReference = writer.Register(page);
            pages.Add(pageReference);
            if (sourcePage.Get("Annots") != null)
            {
                var annotations = new PdfObject.Arr();
                foreach (PdfObject value in inspection.Array(sourcePage.Get("Annots")).Items)
                {
                    PdfObject.Dict annotation = inspection.Dictionary(value);
                    string? subtype = PdfValues.NameOf(_store.ResolveMaybe(annotation.Get("Subtype")));
                    if (subtype == "Widget")
                    {
                        if (FieldType(annotation) != "Sig")
                        {
                            throw new PdfAssemblyNotSupportedException("Form widgets are not yet supported for extraction.");
                        }

                        continue;
                    }

                    if (subtype is not ("Text" or "Square" or "Circle" or "Line" or "Polygon" or "PolyLine" or "Ink"
                        or "Highlight" or "Underline" or "Squiggly" or "StrikeOut") || annotation.Get("AP") != null)
                    {
                        continue;
                    }

                    var copy = (PdfObject.Dict)copier.CopyDirect(_store, annotation, "annotation");
                    copy.Set("P", pageReference);
                    annotations.Items.Add(writer.Register(copy));
                }

                if (annotations.Items.Count > 0)
                {
                    page.Set("Annots", annotations);
                }
            }
        }

        var root = new PdfObject.Dict();
        root.Set("Type", new PdfObject.Name("Catalog"));
        root.Set("Pages", writer.BuildPageTree(pages));
        var info = new PdfObject.Dict();
        info.Set("Producer", new PdfObject.Str(Encoding.ASCII.GetBytes("jaytwo.Pdf"), false));
        var identifier = new PdfObject.Arr(2);
        byte[] id = Guid.NewGuid().ToByteArray();
        identifier.Items.Add(new PdfObject.Str(id, true));
        identifier.Items.Add(new PdfObject.Str(id, true));
        writer.Write(destination, writer.Register(root), writer.Register(info), identifier, _store.EffectiveVersion());
    }

    private void Walk(PdfObject? value, PdfObject.Dict inherited, Dictionary<int, PdfObject.Dict> selected, HashSet<int> path, int depth)
    {
        if (depth > _budget.TraversalDepth || ++_nodeCount > PdfNewFileLimits.DefaultMappedObjects)
        {
            throw new PdfResourceLimitException("page tree traversal");
        }

        int id = value is PdfObject.Ref reference ? reference.ObjectId : 0;
        if (id == 0 || !path.Add(id))
        {
            throw new PdfParseException(0, "The page tree contains an invalid or cyclic reference.");
        }

        using var release = _store.ReleaseAfter();
        if (_store.ResolveMaybe(value) is not PdfObject.Dict node)
        {
            throw new PdfParseException(0, "A page-tree node is not a dictionary.");
        }

        var properties = new PdfObject.Dict(4);
        foreach (string key in InheritedKeys)
        {
            PdfObject? property = node.Get(key) ?? inherited.Get(key);
            if (property != null)
            {
                properties.Set(key, property);
            }
        }

        if (PdfValues.NameOf(node.Get("Type")) == "Page")
        {
            _pageCount++;
            if (selected.ContainsKey(_pageCount))
            {
                var page = new PdfObject.Dict(node.Items.Count + 4);
                _budget.AddStructuralBytes(64 + (node.Items.Count * 24L));
                foreach (var item in node.Items)
                {
                    page.Set(item.Key, item.Value);
                }

                foreach (var item in properties.Items)
                {
                    page.Set(item.Key, item.Value);
                }

                selected[_pageCount] = page;
            }
        }
        else if (_store.ResolveMaybe(node.Get("Kids")) is PdfObject.Arr kids)
        {
            foreach (PdfObject child in kids.Items)
            {
                Walk(child, properties, selected, path, depth + 1);
            }
        }
        else
        {
            throw new PdfParseException(0, "A page-tree branch has no children.");
        }

        path.Remove(id);
        if (node.Get("Kids") is PdfObject.Ref kidsReference && !_store.Dirty.Contains(kidsReference.ObjectId))
        {
            _store.Cache.Remove(kidsReference.ObjectId);
        }

        // Unselected source pages must not accumulate in the parser cache.
        if (!_store.Dirty.Contains(id))
        {
            _store.Cache.Remove(id);
        }
    }

    private string? FieldType(PdfObject.Dict field)
    {
        var seen = new HashSet<PdfObject.Dict>();
        for (int depth = 0; depth <= _budget.TraversalDepth; depth++)
        {
            if (!seen.Add(field))
            {
                throw new PdfParseException(0, "A widget field parent chain contains a cycle.");
            }

            if (field.Get("FT") != null)
            {
                return PdfValues.NameOf(_store.ResolveMaybe(field.Get("FT")));
            }

            if (_store.ResolveMaybe(field.Get("Parent")) is not PdfObject.Dict parent)
            {
                return null;
            }

            field = parent;
        }

        throw new PdfResourceLimitException("traversal depth");
    }

    private PdfTypedObjectCopier.CopyDecision Policy(PdfTypedObjectCopier.CopyContext context)
    {
        if (context.Structure == "page")
        {
            return PageKeys.Contains(context.Key)
                ? PdfTypedObjectCopier.CopyDecision.Copy("resource")
                : PdfTypedObjectCopier.CopyDecision.Omit();
        }

        if (context.Structure == "annotation")
        {
            return AnnotationKeys.Contains(context.Key)
                ? PdfTypedObjectCopier.CopyDecision.Copy("resource")
                : PdfTypedObjectCopier.CopyDecision.Omit();
        }

        if (PdfValues.NameOf(_store.ResolveMaybe(context.Dictionary.Get("Type"))) is "Catalog" or "Pages" or "Page" or "Sig"
            || PdfValues.NameOf(_store.ResolveMaybe(context.Dictionary.Get("FT"))) == "Sig")
        {
            throw new PdfAssemblyNotSupportedException("A retained resource refers to a document, page, or signature structure.");
        }

        return context.Key is "StructParent" or "StructParents" or "Metadata" or "AF"
            ? PdfTypedObjectCopier.CopyDecision.Omit()
            : PdfTypedObjectCopier.CopyDecision.Copy();
    }
}
