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
        "Name", "Open", "AP", "AS", "H", "MK", "QuadPoints", "L", "LE", "IC", "Vertices", "InkList", "RD",
    };

    private readonly PdfObjectStore _store;
    private readonly PdfNewFileBudget _budget;
    private readonly Dictionary<int, int> _sourcePages = new Dictionary<int, int>();
    private int _pageCount;
    private int _nodeCount;

    internal PdfPageExtractor(PdfObjectStore store, PdfNewFileBudget budget)
    {
        _store = store;
        _budget = budget;
    }

    internal static PdfAssemblyResult Assemble(IEnumerable<PdfAssemblyInput> inputs, Stream destination, PdfAssemblyOptions options)
    {
        if (inputs == null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (!destination.CanWrite || (destination.CanSeek && (destination.Length != 0 || destination.Position != 0)))
        {
            throw new ArgumentException("Assembly requires an empty writable destination.", nameof(destination));
        }

        if (options.ExternalLinks is not (PdfExternalLinkMode.PreserveSupported or PdfExternalLinkMode.Drop))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        var budget = new PdfNewFileBudget(new PdfNewFileLimits());
        var writer = new PdfNewFileWriter(budget);
        var pages = new List<PdfObject.Ref>();
        var form = new PdfObject.Dict();
        var parents = new PdfObject.Arr();
        var result = new PdfAssemblyResult();
        int version = 15;
        int occurrence = 0;
        foreach (PdfAssemblyInput input in inputs)
        {
            if (input == null)
            {
                throw new ArgumentException("An input occurrence cannot be null.", nameof(inputs));
            }

            budget.CountMappedObject();
            PdfObjectStore store = input.Document.Engine.AssemblyStore;
            using var release = store.ReleaseAfter();
            if (store.Security is PdfSecurity security)
            {
                if (!options.AllowUnencryptedOutput)
                {
                    throw new PdfAssemblyNotSupportedException("Encrypted input requires AllowUnencryptedOutput.");
                }

                if (security.AuthenticationRole != PdfAuthenticationRole.Owner
                    && (!security.Permissions.CanCopy || !security.Permissions.CanAssemble))
                {
                    throw new PdfEncryptionRestrictedException();
                }
            }

            version = Math.Max(version, store.EffectiveVersion());
            new PdfPageExtractor(store, budget).Import(input.PageNumbers, writer, pages, form, parents, ++occurrence, result, options.ExternalLinks);
        }

        var root = new PdfObject.Dict();
        root.Set("Type", new PdfObject.Name("Catalog"));
        root.Set("Pages", writer.BuildPageTree(pages));
        if (parents.Items.Count > 0)
        {
            form.Set("Fields", parents);
            root.Set("AcroForm", writer.Register(form));
        }

        var info = new PdfObject.Dict();
        info.Set("Producer", new PdfObject.Str(Encoding.ASCII.GetBytes("jaytwo.Pdf"), false));
        var identifier = new PdfObject.Arr(2);
        byte[] id = Guid.NewGuid().ToByteArray();
        identifier.Items.Add(new PdfObject.Str(id, true));
        identifier.Items.Add(new PdfObject.Str(id, true));
        writer.Write(destination, writer.Register(root), writer.Register(info), identifier, version);
        return result;
    }

    private void Import(IEnumerable<int> pageNumbers, PdfNewFileWriter writer, List<PdfObject.Ref> allPages, PdfObject.Dict form, PdfObject.Arr parents, int occurrence, PdfAssemblyResult result, PdfExternalLinkMode externalLinks)
    {
        var order = new List<int>();
        var selected = new Dictionary<int, PdfObject.Dict>();
        foreach (int number in pageNumbers)
        {
            _budget.CountOutputPages(checked(allPages.Count + order.Count + 1));
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

        var copier = new PdfTypedObjectCopier(writer, _budget, Policy);
        var pages = new List<PdfObject.Ref>(order.Count);
        var outputPages = new List<PdfObject.Dict>(order.Count);
        var firstPages = new Dictionary<int, PdfObject.Ref>();
        foreach (int number in order)
        {
            var page = (PdfObject.Dict)copier.CopyDirect(_store, selected[number], "page");
            PdfObject.Ref reference = writer.Register(page);
            pages.Add(reference);
            outputPages.Add(page);
            if (!firstPages.ContainsKey(number))
            {
                firstPages.Add(number, reference);
            }
        }

        var forms = new PdfAssemblyForms(_store, _budget, writer, copier, catalog, form, occurrence, selected.Values);
        var links = new PdfAssemblyLinks(_store, _budget, catalog, _sourcePages, firstPages, externalLinks);
        var appearances = new PdfAssemblyAppearances(_store, _budget);
        for (int index = 0; index < order.Count; index++)
        {
            PdfObject.Dict sourcePage = selected[order[index]];
            if (sourcePage.Get("Annots") == null)
            {
                continue;
            }

            var annotations = new PdfObject.Arr();
            foreach (PdfObject value in inspection.Array(sourcePage.Get("Annots")).Items)
            {
                PdfObject.Dict annotation = inspection.Dictionary(value);
                string? subtype = PdfValues.NameOf(_store.ResolveMaybe(annotation.Get("Subtype")));
                if (subtype == "Widget")
                {
                    if (FieldType(annotation) == "Sig")
                    {
                        result.Omit(PdfOmissionReason.SignatureWidget);
                    }
                    else if (!appearances.Supported(annotation.Get("AP")))
                    {
                        result.Omit(PdfOmissionReason.UnsupportedAppearance);
                    }
                    else
                    {
                        var widget = (PdfObject.Dict)copier.CopyDirect(_store, annotation, "annotation");
                        widget.Remove("T");
                        widget.Set("P", pages[index]);
                        annotations.Items.Add(forms.Widget(annotation, widget));
                    }

                    continue;
                }

                PdfObject.Dict? rebuilt = annotation;
                if (subtype == "Link")
                {
                    rebuilt = links.Rebuild(annotation, order[index], pages[index], out PdfOmissionReason reason);
                    if (rebuilt == null)
                    {
                        result.Omit(reason);
                        continue;
                    }
                }
                else if (subtype is not ("Text" or "Square" or "Circle" or "Line" or "Polygon" or "PolyLine" or "Ink"
                    or "Highlight" or "Underline" or "Squiggly" or "StrikeOut"))
                {
                    result.Omit(PdfOmissionReason.UnsupportedAnnotation);
                    continue;
                }

                if (!appearances.Supported(annotation.Get("AP")))
                {
                    result.Omit(PdfOmissionReason.UnsupportedAppearance);
                    continue;
                }

                var copy = (PdfObject.Dict)copier.CopyDirect(_store, rebuilt, "annotation");
                // Link destinations already name destination objects: never feed them through the source copier.
                if (subtype == "Link")
                {
                    if (rebuilt.Get("Dest") is PdfObject destinationValue)
                    {
                        copy.Set("Dest", destinationValue);
                    }

                    if (rebuilt.Get("A") is PdfObject action)
                    {
                        copy.Set("A", action);
                    }
                }

                copy.Set("P", pages[index]);
                annotations.Items.Add(writer.Register(copy));
            }

            if (annotations.Items.Count > 0)
            {
                outputPages[index].Set("Annots", annotations);
            }
        }

        forms.Complete(parents);
        allPages.AddRange(pages);
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
                _sourcePages[id] = _pageCount;
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
