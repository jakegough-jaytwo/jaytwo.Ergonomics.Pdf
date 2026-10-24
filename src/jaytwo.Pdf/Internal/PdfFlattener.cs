#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using jaytwo.Pdf.AcroForm;

namespace jaytwo.Pdf.Internal;

// Flattens fields: paints their appearances into page content and removes them from the form.
internal sealed class PdfFlattener
{
    // Annotation flags, ISO 32000-1 Table 165.
    private const int AnnotHidden = 1 << 1;
    private const int AnnotNoRotate = 1 << 4;
    private const int AnnotNoView = 1 << 5;

    private readonly PdfObjectStore _store;
    private readonly PdfWriteGate _gate;
    private readonly PdfPageTree _pages;
    private readonly PdfFieldTree _fields;

    internal PdfFlattener(PdfObjectStore store, PdfWriteGate gate, PdfPageTree pages, PdfFieldTree fields)
    {
        _store = store;
        _gate = gate;
        _pages = pages;
        _fields = fields;
    }

    // W3 (spec 7.1 and 7.2): every check, and the choice of each widget's appearance, runs against one read of the field
    // tree before any object is edited, so a refused flatten leaves the document unchanged. Null names flatten every
    // field except signature fields.
    internal void Flatten(IEnumerable<string>? fullNames)
    {
        _store.CheckDisposed();
        HashSet<string>? requested = null;
        if (fullNames != null)
        {
            requested = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in fullNames)
            {
                if (name == null)
                {
                    throw new ArgumentException("A field name cannot be null.", nameof(fullNames));
                }

                if (!requested.Add(name))
                {
                    throw new ArgumentException("A field name appears more than once: " + name, nameof(fullNames));
                }
            }

            if (requested.Count == 0)
            {
                return;
            }
        }

        using var release = _store.ReleaseAfter();
        List<FieldSlot> slots = _fields.CollectFields();
        XfaKind xfa = _fields.ClassifyXfa(slots);
        if (xfa == XfaKind.XfaOnly)
        {
            throw new PdfUnsupportedFormException("The form is XFA-only; the library does not edit XFA.");
        }

        // Spec 7.2: flatten changes page content, which no DocMDP level permits.
        if (_gate.CertificationLevel(slots) != 0)
        {
            throw new PdfCertificationLockedException();
        }

        var selected = new List<FieldSlot>();
        if (requested == null)
        {
            selected.AddRange(slots.Where(slot => slot.FieldType != PdfFieldType.Signature));
        }
        else
        {
            foreach (string name in requested)
            {
                List<FieldSlot> matches = slots.Where(slot => slot.FullName == name).ToList();
                if (matches.Count == 0)
                {
                    throw new PdfFieldNotFoundException(name);
                }

                if (matches.Any(slot => slot.FieldType == PdfFieldType.Signature && _gate.HasSignatureDictionary(slot)))
                {
                    throw new PdfSignatureConflictException("The signature field " + name + " is signed; a signed signature field is never flattened.");
                }

                selected.AddRange(matches);
            }
        }

        if (selected.Count == 0)
        {
            return;
        }

        foreach (string name in selected.Select(slot => slot.FullName).Distinct(StringComparer.Ordinal))
        {
            if (_gate.FieldChangeBlocked(name, slots))
            {
                throw new PdfCertificationLockedException();
            }
        }

        if (!_gate.AllowBreakingSignatures && _gate.HasSignature())
        {
            throw new PdfSignatureConflictException("The document has a signature; flatten changes page content, which validators report as a modification the signature does not permit. Set AllowBreakingSignatures to flatten anyway.");
        }

        // ISO 32000-1 Table 22: changing page content is bit 4, and removing form fields also needs bit 6.
        _gate.RequireModify();
        _gate.RequireAnnotate();
        PdfObject.Dict? acro = _store.ResolveMaybe(_store.CatalogDict().Get("AcroForm")) as PdfObject.Dict;
        if (_store.ResolveMaybe(acro?.Get("NeedAppearances")) is PdfObject.Bool { Value: true })
        {
            throw new PdfUnsupportedFormException("/NeedAppearances is true, so a field has no appearance the library can flatten.");
        }

        // Viewers show a widget on each page whose /Annots lists it, painting a page's annotations in /Annots order.
        List<PageInfo> pages = _pages.GetPages();
        var widgetPages = new Dictionary<int, List<(int Page, int Order)>>();
        for (int i = 0; i < pages.Count; i++)
        {
            if (_store.Load(pages[i].ObjectId) is PdfObject.Dict page && _store.ResolveMaybe(page.Get("Annots")) is PdfObject.Arr annots)
            {
                for (int order = 0; order < annots.Items.Count; order++)
                {
                    if (annots.Items[order] is PdfObject.Ref reference && _store.IsLive(reference))
                    {
                        if (!widgetPages.TryGetValue(reference.ObjectId, out List<(int Page, int Order)>? onPages))
                        {
                            onPages = new List<(int Page, int Order)>();
                            widgetPages[reference.ObjectId] = onPages;
                        }

                        if (!onPages.Any(item => item.Page == i))
                        {
                            onPages.Add((i, order));
                        }
                    }
                }
            }
        }

        var draws = new List<FlattenDraw>();
        var removedWidgets = new HashSet<int>();
        var removedFields = new HashSet<int>();
        foreach (FieldSlot slot in selected)
        {
            removedWidgets.Add(slot.WidgetObjectId);
            removedFields.Add(slot.WidgetObjectId);
            removedFields.Add(slot.ValueObjectId);
            if (!widgetPages.TryGetValue(slot.WidgetObjectId, out List<(int Page, int Order)>? onPages))
            {
                continue;
            }

            FlattenDraw? draw = PlanFlattenDraw(slot);
            if (draw == null)
            {
                continue;
            }

            foreach ((int pageIndex, int order) in onPages)
            {
                if ((draw.Flags & AnnotNoRotate) != 0 && ((pages[pageIndex].Rotate % 360) + 360) % 360 != 0)
                {
                    throw new PdfUnsupportedFormException("The widget of " + slot.FullName + " sets NoRotate on a rotated page; flatten does not place it.");
                }

                draws.Add(draw.OnPage(pages[pageIndex].ObjectId, order));
            }
        }

        int savedSize = _store.Size;
        var savedDirty = new HashSet<int>(_store.Dirty);
        _store.BeginBackup();
        try
        {
            // Spec 7.3: no UR3 right grants a change to page content. Spec W4: XFA-aware viewers would keep showing the
            // XFA form, flattened fields included, so a flatten of a hybrid form drops /XFA in the same revision.
            _gate.ApplyUsageRights(null, null);
            if (xfa == XfaKind.Hybrid)
            {
                _fields.EnsureAcroForm().Remove("XFA");
            }

            foreach (IGrouping<int, FlattenDraw> page in draws.GroupBy(draw => draw.PageId))
            {
                PaintFlattened(page.Key, page.OrderBy(draw => draw.Order).ToList());
            }

            foreach (PageInfo page in pages)
            {
                RemoveAnnots(page.ObjectId, removedWidgets);
            }

            acro = (PdfObject.Dict)_store.ResolveMaybe(_store.CatalogDict().Get("AcroForm"))!;
            PruneFields(acro, _fields.EnsureAcroForm, "Fields", removedFields, new HashSet<int>());
            acro = (PdfObject.Dict)_store.ResolveMaybe(_store.CatalogDict().Get("AcroForm"))!;
            PruneFields(acro, _fields.EnsureAcroForm, "CO", removedFields, null);
            acro = (PdfObject.Dict)_store.ResolveMaybe(_store.CatalogDict().Get("AcroForm"))!;
            if (_store.ResolveMaybe(acro.Get("Fields")) is not PdfObject.Arr { Items.Count: > 0 } && (acro.Get("XFA") != null || acro.Get("NeedAppearances") != null))
            {
                PdfObject.Dict edited = _fields.EnsureAcroForm();
                edited.Remove("XFA");
                edited.Remove("NeedAppearances");
            }

            // A form XObject drawn by Do is PDF 1.2, as are the widget appearances it paints.
            _store.RaiseVersion(12);
        }
        catch
        {
            _store.Restore(savedSize, _store.InfoId, _store.InfoGen, _store.TrailerId, savedDirty);
            throw;
        }

        _store.EndBackup();
    }

    // Spec 7.1: the widget's normal appearance, selected by /AS when /N holds states, and the matrix that ISO 32000-1
    // 12.5.5 uses to map the appearance's transformed /BBox onto /Rect. Null means the widget paints nothing: it is
    // hidden, it has no appearance and its field shows no value, its selected state is Off with no Off appearance, or
    // its box has no area. A value with no appearance to show it throws before anything changes.
    private FlattenDraw? PlanFlattenDraw(FieldSlot slot)
    {
        if (_store.Load(slot.WidgetObjectId) is not PdfObject.Dict widget)
        {
            return null;
        }

        int flags = PdfValues.IntOf(_store.ResolveMaybe(widget.Get("F")));
        if ((flags & (AnnotHidden | AnnotNoView)) != 0)
        {
            return null;
        }

        PdfObject? normal = _store.ResolveMaybe(widget.Get("AP")) is PdfObject.Dict ap ? ap.Get("N") : null;
        PdfObject? resolved = _store.ResolveMaybe(normal);
        if (resolved is PdfObject.Dict { Body: null } states)
        {
            string? state = PdfValues.NameOf(_store.ResolveMaybe(widget.Get("AS")));

            // ISO 32000-1 Table 168 requires /AS here. Without it viewers take a lone state, and otherwise Off.
            if (state == null)
            {
                state = states.Items.Count == 1 ? states.Items[0].Key : "Off";
            }

            normal = states.Get(state);
            resolved = _store.ResolveMaybe(normal);
            if (resolved is not PdfObject.Dict { Body: not null } && state == "Off")
            {
                return null;
            }
        }

        // A form XObject needs a /BBox, and /Matrix defaults to the identity (ISO 32000-1 Table 95). Viewers draw nothing
        // for an appearance that is missing or is not one, which loses only a value the field shows.
        PdfObject.Dict? stream = resolved as PdfObject.Dict;
        string? subtype = PdfValues.NameOf(_store.ResolveMaybe(stream?.Get("Subtype")));
        double[]? bbox = Numbers(stream?.Get("BBox"), 4);
        double[]? matrix = stream?.Get("Matrix") is PdfObject matrixValue ? Numbers(matrixValue, 6) : new double[] { 1, 0, 0, 1, 0, 0 };
        if (normal is not PdfObject.Ref reference
            || stream?.Body == null
            || (subtype != null && subtype != "Form")
            || bbox == null
            || matrix == null)
        {
            if (!ShowsValue(slot))
            {
                return null;
            }

            throw new PdfUnsupportedFormException("The widget of " + slot.FullName + " has no usable appearance to flatten.");
        }

        // ISO 32000-1 12.5.5: transform the /BBox corners by /Matrix and take their bounding box, then scale and translate
        // that box onto /Rect. Do applies /Matrix itself, so the page content concatenates only the second matrix.
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;
        foreach ((double x, double y) in new[] { (bbox[0], bbox[1]), (bbox[2], bbox[1]), (bbox[0], bbox[3]), (bbox[2], bbox[3]) })
        {
            double tx = (matrix[0] * x) + (matrix[2] * y) + matrix[4];
            double ty = (matrix[1] * x) + (matrix[3] * y) + matrix[5];
            minX = Math.Min(minX, tx);
            minY = Math.Min(minY, ty);
            maxX = Math.Max(maxX, tx);
            maxY = Math.Max(maxY, ty);
        }

        PdfRect rect = slot.Rect;
        double left = Math.Min(rect.Llx, rect.Urx);
        double bottom = Math.Min(rect.Lly, rect.Ury);
        double width = Math.Abs(rect.Width);
        double height = Math.Abs(rect.Height);
        if (width <= 0 || height <= 0 || maxX - minX <= 0 || maxY - minY <= 0)
        {
            return null;
        }

        double scaleX = width / (maxX - minX);
        double scaleY = height / (maxY - minY);
        return new FlattenDraw(
            reference.ObjectId,
            reference.GenerationNumber,
            subtype == null,
            new[] { scaleX, 0, 0, scaleY, left - (minX * scaleX), bottom - (minY * scaleY) },
            flags,
            widget.Get("OC"));
    }

    // Whether the field has a value that a viewer would draw, so that a missing appearance would lose it.
    private static bool ShowsValue(FieldSlot slot)
    {
        return slot.FieldType switch
        {
            PdfFieldType.Text => slot.SelectedValues.Any(value => value.Length > 0),
            PdfFieldType.Choice => slot.SelectedValues.Count > 0,
            PdfFieldType.Button => slot.ButtonKind != PdfButtonKind.Push
                && slot.Value != null
                && slot.Value != "Off"
                && (slot.ButtonKind != PdfButtonKind.Radio || slot.ExportValue == null || slot.ExportValue == slot.Value),
            _ => false,
        };
    }

    private double[]? Numbers(PdfObject? value, int count)
    {
        if (_store.ResolveMaybe(value) is not PdfObject.Arr array || array.Items.Count != count)
        {
            return null;
        }

        var numbers = new double[count];
        for (int i = 0; i < count; i++)
        {
            if (!_store.TryNumber(array.Items[i], out numbers[i]) || double.IsNaN(numbers[i]) || double.IsInfinity(numbers[i]))
            {
                return null;
            }
        }

        return numbers;
    }

    // Spec 7.1: the page's existing /Contents is wrapped in q...Q by a stream before it and a stream after it, and the
    // stream after it paints each appearance with Do. The page gets its own /Resources, a copy of the effective one with
    // the appearances added under names it does not use.
    private void PaintFlattened(int pageId, List<FlattenDraw> draws)
    {
        PdfObject.Dict page = (PdfObject.Dict)_store.EditAny(pageId);
        var resources = InheritedResources(pageId) is PdfObject.Dict effective ? (PdfObject.Dict)effective.DeepClone() : new PdfObject.Dict();
        resources.ObjectNumber = 0;
        resources.Generation = 0;
        PdfObject.Dict xobjects = CopyResourceCategory(resources, "XObject");
        PdfObject.Dict? properties = draws.Any(draw => draw.OptionalContent != null) ? CopyResourceCategory(resources, "Properties") : null;
        var names = new Dictionary<int, string>();
        int next = 0;
        var content = new StringBuilder();
        foreach (FlattenDraw draw in draws)
        {
            if (!names.TryGetValue(draw.StreamId, out string? name))
            {
                name = UnusedName(xobjects, "FlatAP", ref next);
                xobjects.Set(name, new PdfObject.Ref(draw.StreamId, draw.StreamGeneration));
                names[draw.StreamId] = name;
                if (draw.NeedsSubtype)
                {
                    PdfObject.Dict stream = (PdfObject.Dict)_store.EditAny(draw.StreamId);
                    stream.Set("Subtype", new PdfObject.Name("Form"));
                }
            }

            if (draw.OptionalContent != null)
            {
                string tag = UnusedName(properties!, "FlatOC", ref next);
                properties!.Set(tag, draw.OptionalContent.DeepClone());
                content.Append("/OC /").Append(tag).Append(" BDC\n");
            }

            content.Append("q");
            foreach (double value in draw.Matrix)
            {
                content.Append(' ').Append(PdfValues.Number(value).Format());
            }

            content.Append(" cm /").Append(name).Append(" Do Q\n");
            if (draw.OptionalContent != null)
            {
                content.Append("EMC\n");
            }
        }

        var parts = new PdfObject.Arr();
        PdfObject? existing = page.Get("Contents");
        PdfObject? existingValue = _store.ResolveMaybe(existing);
        if (existingValue is PdfObject.Arr existingArray)
        {
            parts.Items.AddRange(existingArray.Items.Select(item => item.DeepClone()));
        }
        else if (existing is PdfObject.Ref && existingValue is PdfObject.Dict { Body: not null })
        {
            parts.Items.Add(existing.DeepClone());
        }

        if (parts.Items.Count > 0)
        {
            parts.Items.Insert(0, new PdfObject.Ref(AllocateContent("q\n"), 0));
            content.Insert(0, "\nQ\n");
        }

        parts.Items.Add(new PdfObject.Ref(AllocateContent(content.ToString()), 0));
        page.Set("Contents", parts);
        page.Set("Resources", resources);
    }

    private int AllocateContent(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        var stream = new PdfObject.Dict();
        stream.Set("Length", PdfObject.Num.FromLong(bytes.Length));
        stream.Body = new PdfObject.MemoryBody(bytes);
        return _store.Allocate(stream);
    }

    // ISO 32000-1 Table 30: /Resources is inheritable from the page tree.
    private PdfObject.Dict? InheritedResources(int pageId)
    {
        var seen = new HashSet<int>();
        PdfObject.Dict? node = _store.Load(pageId) as PdfObject.Dict;
        while (node != null && seen.Count <= PdfObjectStore.MaxTraversalDepth)
        {
            if (_store.ResolveMaybe(node.Get("Resources")) is PdfObject.Dict resources)
            {
                return resources;
            }

            if (node.Get("Parent") is not PdfObject.Ref parent || !_store.IsLive(parent) || !seen.Add(parent.ObjectId))
            {
                return null;
            }

            node = _store.Load(parent.ObjectId) as PdfObject.Dict;
        }

        return null;
    }

    private PdfObject.Dict CopyResourceCategory(PdfObject.Dict resources, string category)
    {
        var copy = _store.ResolveMaybe(resources.Get(category)) is PdfObject.Dict existing ? (PdfObject.Dict)existing.DeepClone() : new PdfObject.Dict();
        copy.ObjectNumber = 0;
        copy.Generation = 0;
        resources.Set(category, copy);
        return copy;
    }

    private static string UnusedName(PdfObject.Dict dict, string prefix, ref int next)
    {
        string name;
        do
        {
            name = prefix + next.ToString(CultureInfo.InvariantCulture);
            next++;
        }
        while (dict.Get(name) != null);
        return name;
    }

    private void RemoveAnnots(int pageId, HashSet<int> widgets)
    {
        if (_store.Load(pageId) is not PdfObject.Dict page
            || _store.ResolveMaybe(page.Get("Annots")) is not PdfObject.Arr annots
            || !annots.Items.Any(item => item is PdfObject.Ref reference && widgets.Contains(reference.ObjectId)))
        {
            return;
        }

        PdfObject.Dict owner = page.Get("Annots") is PdfObject.Ref ? page : (PdfObject.Dict)_store.EditAny(pageId);
        _store.EditArray(owner, "Annots").Items.RemoveAll(item => item is PdfObject.Ref reference && widgets.Contains(reference.ObjectId));
    }

    // Removes the flattened fields from the array under `key`. With `seen`, it also descends into /Kids and removes a
    // parent that flattening left without kids, as spec 7.1 removes flattened fields from their parents.
    private bool PruneFields(PdfObject.Dict owner, Func<PdfObject.Dict> editOwner, string key, HashSet<int> removed, HashSet<int>? seen)
    {
        if (_store.ResolveMaybe(owner.Get(key)) is not PdfObject.Arr items || items.Items.Count == 0)
        {
            return false;
        }

        var drop = new HashSet<int>();
        for (int i = 0; i < items.Items.Count; i++)
        {
            if (items.Items[i] is not PdfObject.Ref reference)
            {
                continue;
            }

            if (removed.Contains(reference.ObjectId))
            {
                drop.Add(i);
            }
            else if (seen != null
                && seen.Add(reference.ObjectId)
                && _store.Resolve(reference) is PdfObject.Dict child
                && PruneFields(child, () => (PdfObject.Dict)_store.EditAny(reference.ObjectId), "Kids", removed, seen))
            {
                drop.Add(i);
            }
        }

        if (drop.Count == 0)
        {
            return false;
        }

        PdfObject.Arr edited = _store.EditArray(owner.Get(key) is PdfObject.Ref ? owner : editOwner(), key);
        for (int i = edited.Items.Count - 1; i >= 0; i--)
        {
            if (drop.Contains(i))
            {
                edited.Items.RemoveAt(i);
            }
        }

        return edited.Items.Count == 0;
    }

    // One appearance to paint on one page: the form XObject, the matrix that places it, and the widget's flags and /OC.
    private sealed class FlattenDraw
    {
        public FlattenDraw(int streamId, int streamGeneration, bool needsSubtype, double[] matrix, int flags, PdfObject? optionalContent)
        {
            StreamId = streamId;
            StreamGeneration = streamGeneration;
            NeedsSubtype = needsSubtype;
            Matrix = matrix;
            Flags = flags;
            OptionalContent = optionalContent;
        }

        public int StreamId { get; }

        public int StreamGeneration { get; }

        // The appearance stream omits /Subtype, which Do requires of a form XObject.
        public bool NeedsSubtype { get; }

        public double[] Matrix { get; }

        public int Flags { get; }

        public PdfObject? OptionalContent { get; }

        public int PageId { get; private set; }

        // The widget's position in the page /Annots.
        public int Order { get; private set; }

        public FlattenDraw OnPage(int pageId, int order)
        {
            var copy = new FlattenDraw(StreamId, StreamGeneration, NeedsSubtype, Matrix, Flags, OptionalContent);
            copy.PageId = pageId;
            copy.Order = order;
            return copy;
        }
    }
}
