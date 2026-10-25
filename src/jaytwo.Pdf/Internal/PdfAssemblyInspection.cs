using System.Collections.Generic;

namespace jaytwo.Pdf.Internal;

// A5 inspects only retained page structures, never content bytes or the discarded structure tree.
internal sealed class PdfAssemblyInspection
{
    private readonly PdfObjectStore _store;
    private readonly PdfNewFileBudget _budget;
    private readonly HashSet<PdfObject> _resources = new HashSet<PdfObject>();

    internal PdfAssemblyInspection(PdfObjectStore store, PdfNewFileBudget budget)
    {
        _store = store;
        _budget = budget;
    }

    internal void Catalog(PdfObject.Dict catalog)
    {
        RefuseNonempty(catalog, "AF");
        RefuseNonempty(catalog, "OCProperties");
        if (_store.ResolveMaybe(catalog.Get("AcroForm")) is PdfObject.Dict form
            && form.Get("XFA") != null
            && (_store.ResolveMaybe(catalog.Get("NeedsRendering")) is PdfObject.Bool { Value: true }
                || _store.ResolveMaybe(form.Get("Fields")) is not PdfObject.Arr { Items.Count: > 0 }))
        {
            throw new PdfAssemblyNotSupportedException("Dynamic or XFA-only forms cannot be extracted.");
        }
    }

    internal void Page(PdfObject.Dict page)
    {
        RefuseNonempty(page, "AF");
        if (page.Get("Annots") != null)
        {
            foreach (PdfObject annotation in Array(page.Get("Annots")).Items)
            {
                _budget.CountMappedObject();
                RefuseNonempty(Dictionary(annotation), "AF");
            }
        }

        Resources(page.Get("Resources"), 0);
    }

    internal PdfObject.Dict Dictionary(PdfObject? value)
    {
        return _store.ResolveMaybe(value) as PdfObject.Dict
            ?? throw new PdfAssemblyNotSupportedException("An inspected assembly dictionary cannot be resolved.");
    }

    internal PdfObject.Arr Array(PdfObject? value)
    {
        return _store.ResolveMaybe(value) as PdfObject.Arr
            ?? throw new PdfAssemblyNotSupportedException("An inspected assembly array cannot be resolved.");
    }

    private void Resources(PdfObject? value, int depth)
    {
        if (value == null)
        {
            return;
        }

        if (depth > _budget.TraversalDepth)
        {
            throw new PdfResourceLimitException("traversal depth");
        }

        PdfObject resolved = _store.Resolve(value);
        if (_store.IsDangling(value))
        {
            throw new PdfAssemblyNotSupportedException("An inspected resource reference cannot be resolved.");
        }

        if (!_resources.Add(resolved))
        {
            return;
        }

        _budget.CountMappedObject();
        if (resolved is PdfObject.Dict dictionary)
        {
            if (PdfValues.NameOf(_store.ResolveMaybe(dictionary.Get("Type"))) is "Catalog" or "Pages" or "Page" or "Sig")
            {
                throw new PdfAssemblyNotSupportedException("A retained resource refers to a document, page, or signature structure.");
            }

            _budget.AddStructuralBytes(64 + (dictionary.Items.Count * 24L));
            // The whole retained resource graph is structural. Do not open its stream bodies.
            RefuseNonempty(dictionary, "AF");
            foreach (var item in dictionary.Items)
            {
                Resources(item.Value, depth + 1);
            }
        }
        else if (resolved is PdfObject.Arr array)
        {
            _budget.AddStructuralBytes(32 + (array.Items.Count * 8L));
            foreach (PdfObject item in array.Items)
            {
                Resources(item, depth + 1);
            }
        }
        else
        {
            long bytes = resolved is PdfObject.Str text ? text.Raw.Length
                : resolved is PdfObject.Name name ? name.Value.Length * 2L : 0;
            _budget.AddStructuralBytes(24 + bytes);
        }
    }

    private void RefuseNonempty(PdfObject.Dict dictionary, string key)
    {
        PdfObject? raw = dictionary.Get(key);
        if (raw == null)
        {
            return;
        }

        PdfObject? value = _store.ResolveMaybe(raw);
        bool empty = key == "AF"
            ? value is PdfObject.Arr { Items.Count: 0 }
            : value is PdfObject.Dict { Items.Count: 0 };
        if (!empty)
        {
            throw new PdfAssemblyNotSupportedException("Assembly refuses a nonempty or unresolved /" + key + ".");
        }
    }
}
