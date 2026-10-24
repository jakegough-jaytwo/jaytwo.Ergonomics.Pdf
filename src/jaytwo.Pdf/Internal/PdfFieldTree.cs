#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using jaytwo.Pdf.AcroForm;

namespace jaytwo.Pdf.Internal;

// Reads the AcroForm field tree into one slot per widget, and creates the AcroForm dictionary when an edit needs one.
internal sealed class PdfFieldTree
{
    private const int FlagReadOnly = 1;
    private const int FlagRequired = 1 << 1;
    private const int FlagMultiline = 1 << 12;
    private const int FlagPassword = 1 << 13;
    private const int FlagRadio = 1 << 15;
    private const int FlagPushbutton = 1 << 16;
    private const int FlagEdit = 1 << 18;
    private const int FlagFileSelect = 1 << 20;
    private const int FlagMultiSelect = 1 << 21;
    private const int FlagComb = 1 << 24;
    private const int FlagRichText = 1 << 25;

    private readonly PdfObjectStore _store;
    private readonly PdfPageTree _pages;

    internal PdfFieldTree(PdfObjectStore store, PdfPageTree pages)
    {
        _store = store;
        _pages = pages;
    }

    internal bool NeedAppearances
    {
        get
        {
            using var release = _store.ReleaseAfter();
            if (_store.CatalogDict().Get("AcroForm") is not PdfObject acro)
            {
                return false;
            }

            return _store.Resolve(acro) is PdfObject.Dict dict && dict.Get("NeedAppearances") is PdfObject.Bool flag && flag.Value;
        }
    }

    internal IReadOnlyList<PdfField> ListFields()
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        var fields = new List<PdfField>();
        foreach (FieldSlot slot in CollectFields())
        {
            fields.Add(new PdfField(
                slot.PartialName,
                slot.FullName,
                slot.WidgetIndex,
                slot.FieldType,
                slot.ButtonKind,
                slot.SelectedValues,
                slot.ExportValue,
                slot.Rect,
                slot.PageNumber,
                slot.PageRotation,
                slot.MediaBox,
                slot.ReadOnly,
                slot.Required,
                slot.Multiline,
                slot.MaxLength,
                slot.Editable,
                slot.MultiSelect,
                slot.Options));
        }

        return fields;
    }

    internal IReadOnlyList<PdfField> GetFields(string fullName)
    {
        _store.CheckDisposed();
        if (fullName == null)
        {
            throw new ArgumentNullException(nameof(fullName));
        }

        using var release = _store.ReleaseAfter();
        var matches = new List<PdfField>();
        foreach (PdfField field in ListFields())
        {
            if (field.FullName == fullName)
            {
                matches.Add(field);
            }
        }

        if (matches.Count == 0)
        {
            throw new PdfFieldNotFoundException(fullName);
        }

        return matches;
    }

    internal IReadOnlyDictionary<string, PdfFieldValue> Export()
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        var values = new Dictionary<string, PdfFieldValue>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (FieldSlot slot in CollectFields())
        {
            if (!seen.Add(slot.FullName))
            {
                continue;
            }

            // R6: one entry per terminal field, read from its first widget as a fill reads it. A password field's value
            // is not meant to be stored in the file (ISO 32000-1 Table 228), and it cannot be filled (W5).
            if (slot.FieldType == PdfFieldType.Text && !slot.Password)
            {
                values[slot.FullName] = PdfFieldValue.FromText(slot.SelectedValues.Count == 0 ? string.Empty : slot.SelectedValues[0]);
            }
            else if (slot.ButtonKind == PdfButtonKind.CheckBox)
            {
                values[slot.FullName] = PdfFieldValue.FromBoolean(slot.Value != null && slot.Value != "Off");
            }
            else if (slot.ButtonKind == PdfButtonKind.Radio)
            {
                values[slot.FullName] = slot.Value == null || slot.Value == "Off" ? PdfFieldValue.None : PdfFieldValue.FromText(slot.Value);
            }
            else if (slot.FieldType == PdfFieldType.Choice)
            {
                values[slot.FullName] = slot.MultiSelect
                    ? PdfFieldValue.FromList(slot.SelectedValues)
                    : slot.SelectedValues.Count == 0 ? PdfFieldValue.None : PdfFieldValue.FromText(slot.SelectedValues[0]);
            }
        }

        return new ReadOnlyDictionary<string, PdfFieldValue>(values);
    }

    internal List<FieldSlot> CollectFields()
    {
        var slots = new List<FieldSlot>();
        PdfObject.Dict catalog = _store.CatalogDict();
        if (_store.ResolveMaybe(catalog.Get("AcroForm")) is not PdfObject.Dict acro)
        {
            return slots;
        }

        if (_store.ResolveMaybe(acro.Get("Fields")) is not PdfObject.Arr fields)
        {
            return slots;
        }

        List<PageInfo> pages = _pages.GetPages();
        var pageIndex = new Dictionary<int, int>();
        var annotPages = new Dictionary<int, int>();
        for (int i = 0; i < pages.Count; i++)
        {
            pageIndex[pages[i].ObjectId] = i + 1;
            if (_store.Load(pages[i].ObjectId) is PdfObject.Dict page && _store.ResolveMaybe(page.Get("Annots")) is PdfObject.Arr annots)
            {
                foreach (PdfObject annot in annots.Items)
                {
                    if (annot is PdfObject.Ref reference && _store.IsLive(reference))
                    {
                        annotPages[reference.ObjectId] = i + 1;
                    }
                }
            }
        }

        var inherited = new Inherited(null, null, 0, null, 0, null, 0, null);
        var seenFields = new HashSet<int>();
        foreach (PdfObject field in fields.Items)
        {
            if (field is PdfObject.Ref reference && _store.Resolve(reference) is PdfObject.Dict dict)
            {
                WalkField(dict, reference.ObjectId, string.Empty, inherited, slots, pages, pageIndex, annotPages, seenFields, 0);
            }
        }

        var counts = new Dictionary<string, int>();
        foreach (FieldSlot slot in slots)
        {
            counts.TryGetValue(slot.FullName, out int index);
            slot.WidgetIndex = index;
            counts[slot.FullName] = index + 1;
        }

        return slots;
    }

    private void WalkField(
        PdfObject.Dict field,
        int fieldId,
        string parentName,
        Inherited inherited,
        List<FieldSlot> slots,
        List<PageInfo> pages,
        Dictionary<int, int> pageIndex,
        Dictionary<int, int> annotPages,
        HashSet<int> seen,
        int depth)
    {
        if (!seen.Add(fieldId))
        {
            throw new PdfParseException(0, "The field tree contains a cycle.");
        }

        if (depth > PdfObjectStore.MaxTraversalDepth)
        {
            throw new PdfParseException(0, "The field tree is too deep.");
        }

        // The value belongs to the field dictionary (ISO 32000-1 12.7.3.1): a top-level entry of /Fields or a node with
        // /T. A kid without /T is a widget annotation, even when its writer copied field entries such as /FT onto it,
        // as LibreOffice does, so it neither holds nor overrides the value.
        bool isField = inherited.ValueOwner == 0 || field.Get("T") != null;
        string? ft = PdfValues.NameOf(field.Get("FT")) ?? inherited.Ft;
        PdfObject? value = (isField ? field.Get("V") : null) ?? inherited.Value;
        int flags = field.Get("Ff") != null ? PdfValues.IntOf(field.Get("Ff")) : inherited.Flags;
        string? da = _store.ValueText(field.Get("DA")) ?? inherited.Da;
        int align = field.Get("Q") != null ? PdfValues.IntOf(field.Get("Q")) : inherited.Align;
        int? maxLen = field.Get("MaxLen") != null ? PdfValues.IntOf(field.Get("MaxLen")) : inherited.MaxLen;
        PdfObject? opt = field.Get("Opt") ?? inherited.Opt;
        int valueOwner = isField ? fieldId : inherited.ValueOwner;

        string? partial = _store.ValueText(field.Get("T"));
        string full = string.IsNullOrEmpty(partial) ? parentName : (parentName.Length == 0 ? partial! : parentName + "." + partial);
        bool widget = PdfValues.NameOf(field.Get("Subtype")) == "Widget" || field.Get("Rect") != null;
        if (_store.ResolveMaybe(field.Get("Kids")) is PdfObject.Arr kids && kids.Items.Count > 0)
        {
            var next = new Inherited(ft, value, flags, da, align, maxLen, valueOwner, opt);
            foreach (PdfObject kid in kids.Items)
            {
                if (kid is PdfObject.Ref reference && _store.Resolve(reference) is PdfObject.Dict child)
                {
                    WalkField(child, reference.ObjectId, full, next, slots, pages, pageIndex, annotPages, seen, depth + 1);
                }
            }

            return;
        }

        if (!widget)
        {
            return;
        }

        int pageNumber = 0;
        if (field.Get("P") is PdfObject.Ref pageRef && _store.IsLive(pageRef) && pageIndex.TryGetValue(pageRef.ObjectId, out int fromParent))
        {
            pageNumber = fromParent;
        }
        else if (annotPages.TryGetValue(fieldId, out int fromAnnots))
        {
            pageNumber = fromAnnots;
        }

        PageInfo page = pageNumber > 0 && pageNumber <= pages.Count ? pages[pageNumber - 1] : default;
        var type = ft switch
        {
            "Btn" => PdfFieldType.Button,
            "Ch" => PdfFieldType.Choice,
            "Sig" => PdfFieldType.Signature,
            _ => PdfFieldType.Text,
        };
        PdfButtonKind button = PdfButtonKind.None;
        if (type == PdfFieldType.Button)
        {
            if ((flags & FlagPushbutton) != 0)
            {
                button = PdfButtonKind.Push;
            }
            else if ((flags & FlagRadio) != 0)
            {
                button = PdfButtonKind.Radio;
            }
            else
            {
                button = PdfButtonKind.CheckBox;
            }
        }

        List<string> selected = ReadSelected(value);
        slots.Add(new FieldSlot
        {
            PartialName = partial ?? (full.Contains('.') ? full.Substring(full.LastIndexOf('.') + 1) : full),
            FullName = full,
            ValueObjectId = valueOwner,
            WidgetObjectId = fieldId,
            FieldType = type,
            ButtonKind = button,
            Value = selected.Count == 0 ? null : selected[0],
            SelectedValues = selected,
            ExportValue = type == PdfFieldType.Button ? OnState(field) : null,
            Rect = _store.RectOf(field.Get("Rect")) ?? new PdfRect(0, 0, 0, 0),
            PageNumber = pageNumber,
            PageRotation = page.Rotate,
            MediaBox = page.MediaBox,
            ReadOnly = (flags & FlagReadOnly) != 0,
            Required = (flags & FlagRequired) != 0,
            Multiline = (flags & FlagMultiline) != 0,
            MaxLength = maxLen,
            Password = (flags & FlagPassword) != 0,
            FileSelect = (flags & FlagFileSelect) != 0,
            Comb = (flags & FlagComb) != 0,
            RichText = (flags & FlagRichText) != 0,
            Editable = type == PdfFieldType.Choice && (flags & FlagEdit) != 0,
            MultiSelect = type == PdfFieldType.Choice && (flags & FlagMultiSelect) != 0,
            Options = type == PdfFieldType.Choice ? ReadOptions(opt) : Array.Empty<PdfChoiceOption>(),
            DefaultAppearance = da,
            Align = align,
        });
    }

    internal PdfObject.Dict EnsureAcroForm()
    {
        PdfObject.Dict catalog = _store.EditCatalog();
        if (catalog.Get("AcroForm") is PdfObject.Ref reference && _store.IsLive(reference))
        {
            return (PdfObject.Dict)_store.EditAny(reference.ObjectId);
        }

        if (catalog.Get("AcroForm") is PdfObject.Dict inline)
        {
            return inline;
        }

        var created = new PdfObject.Dict();
        created.Set("Fields", new PdfObject.Arr());
        int id = _store.Allocate(created);
        catalog.Set("AcroForm", new PdfObject.Ref(id, 0));
        return (PdfObject.Dict)_store.Cache[id];
    }

    // R12: /XFA is a stream or an array of packets (ISO 32000-1 Table 218); neither is decoded here. A dynamic form sets
    // the catalog /NeedsRendering, and viewers that ignore XFA then draw only the placeholder page content, so it is
    // XFA-only even when it carries AcroForm widgets. A form with XFA and no widgets has nothing an AcroForm fill can change.
    internal XfaKind ClassifyXfa(List<FieldSlot> slots)
    {
        PdfObject.Dict catalog = _store.CatalogDict();
        if (_store.ResolveMaybe(catalog.Get("AcroForm")) is not PdfObject.Dict acro
            || _store.ResolveMaybe(acro.Get("XFA")) is not (PdfObject.Arr or PdfObject.Dict { Body: not null }))
        {
            return XfaKind.None;
        }

        bool needsRendering = _store.ResolveMaybe(catalog.Get("NeedsRendering")) is PdfObject.Bool flag && flag.Value;
        return needsRendering || slots.Count == 0 ? XfaKind.XfaOnly : XfaKind.Hybrid;
    }

    private List<string> ReadSelected(PdfObject? value)
    {
        var selected = new List<string>();
        value = _store.ResolveMaybe(value);
        if (value is PdfObject.Arr array)
        {
            foreach (PdfObject item in array.Items)
            {
                string? text = _store.ValueText(item);
                if (text != null)
                {
                    selected.Add(text);
                }
            }

            return selected;
        }

        string? single = _store.ValueText(value);
        if (single != null)
        {
            selected.Add(single);
        }

        return selected;
    }

    private List<PdfChoiceOption> ReadOptions(PdfObject? value)
    {
        var options = new List<PdfChoiceOption>();
        if (_store.ResolveMaybe(value) is not PdfObject.Arr array)
        {
            return options;
        }

        foreach (PdfObject item in array.Items)
        {
            PdfObject resolved = _store.Resolve(item);
            if (resolved is PdfObject.Arr pair && pair.Items.Count > 0)
            {
                string export = _store.ValueText(pair.Items[0]) ?? string.Empty;
                string display = pair.Items.Count > 1 ? _store.ValueText(pair.Items[1]) ?? export : export;
                options.Add(new PdfChoiceOption(export, display));
                continue;
            }

            string single = _store.ValueText(resolved) ?? string.Empty;
            options.Add(new PdfChoiceOption(single, single));
        }

        return options;
    }

    internal string? OnState(PdfObject.Dict widget)
    {
        if (_store.ResolveMaybe(widget.Get("AP")) is not PdfObject.Dict ap)
        {
            return null;
        }

        if (_store.ResolveMaybe(ap.Get("N")) is not PdfObject.Dict normal)
        {
            return null;
        }

        foreach (var item in normal.Items)
        {
            if (item.Key != "Off")
            {
                return item.Key;
            }
        }

        return null;
    }

    private readonly struct Inherited
    {
        public Inherited(string? ft, PdfObject? value, int flags, string? da, int align, int? maxLen, int valueOwner, PdfObject? opt)
        {
            Ft = ft;
            Value = value;
            Flags = flags;
            Da = da;
            Align = align;
            MaxLen = maxLen;
            ValueOwner = valueOwner;
            Opt = opt;
        }

        public string? Ft { get; }

        public PdfObject? Value { get; }

        public int Flags { get; }

        public string? Da { get; }

        public int Align { get; }

        public int? MaxLen { get; }

        public int ValueOwner { get; }

        public PdfObject? Opt { get; }
    }
}
