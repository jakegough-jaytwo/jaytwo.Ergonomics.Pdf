#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using jaytwo.Pdf.AcroForm;

namespace jaytwo.Pdf.Internal;

// Fills fields: checks every value against one read of the field tree, then applies them under one backup.
internal sealed class PdfFormFiller
{
    private readonly PdfObjectStore _store;
    private readonly PdfWriteGate _gate;
    private readonly PdfFieldTree _fields;
    private readonly PdfFormAppearances _appearances;

    internal PdfFormFiller(PdfObjectStore store, PdfWriteGate gate, PdfFieldTree fields, PdfFormAppearances appearances)
    {
        _store = store;
        _gate = gate;
        _fields = fields;
        _appearances = appearances;
    }

    // W1 and W2: fills are validated in full, against one read of the field tree, before any object is edited, so a
    // refused entry leaves the document unchanged. They then apply in order, exactly as single fills would.
    internal IReadOnlyDictionary<string, PdfAppearanceDisposition> Fill(IEnumerable<KeyValuePair<string, PdfFieldValue>> values)
    {
        _store.CheckDisposed();
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var entries = new List<KeyValuePair<string, PdfFieldValue>>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, PdfFieldValue> entry in values)
        {
            if (entry.Key == null)
            {
                throw new ArgumentException("A field name cannot be null.", nameof(values));
            }

            if (!names.Add(entry.Key))
            {
                throw new ArgumentException("A field name appears more than once: " + entry.Key, nameof(values));
            }

            entries.Add(entry);
        }

        var results = new Dictionary<string, PdfAppearanceDisposition>(StringComparer.Ordinal);
        if (entries.Count == 0)
        {
            return results;
        }

        using var release = _store.ReleaseAfter();
        List<FieldSlot> slots = _fields.CollectFields();
        XfaKind xfa = _fields.ClassifyXfa(slots);
        if (xfa == XfaKind.XfaOnly)
        {
            throw new PdfUnsupportedFormException("The form is XFA-only; the library does not edit XFA.");
        }

        if (_gate.CertificationLevel(slots) == 1)
        {
            throw new PdfCertificationLockedException();
        }

        foreach (KeyValuePair<string, PdfFieldValue> entry in entries)
        {
            if (_gate.FieldChangeBlocked(entry.Key, slots))
            {
                throw new PdfCertificationLockedException();
            }
        }

        // D-48: validators permit no /XFA change after a signature, and keeping /XFA would leave XFA-aware viewers
        // showing stale data, so a signed hybrid form refuses the fill.
        if (xfa == XfaKind.Hybrid && _gate.HasSignature())
        {
            throw new PdfSignatureConflictException("The hybrid XFA form has a signature; a fill must remove /XFA, which validators report as a change the signature does not permit.");
        }

        _gate.RequireFill();
        var byName = new Dictionary<string, List<FieldSlot>>(StringComparer.Ordinal);
        foreach (FieldSlot slot in slots)
        {
            if (!byName.TryGetValue(slot.FullName, out List<FieldSlot>? group))
            {
                group = new List<FieldSlot>();
                byName[slot.FullName] = group;
            }

            group.Add(slot);
        }

        var plans = new List<FillPlan>(entries.Count);
        foreach (KeyValuePair<string, PdfFieldValue> entry in entries)
        {
            if (!byName.TryGetValue(entry.Key, out List<FieldSlot>? matches))
            {
                throw new PdfFieldNotFoundException(entry.Key);
            }

            plans.Add(PlanFill(entry.Key, entry.Value, matches));
        }

        // Applying reads objects the checks did not, such as /DR; a failure there restores every edit.
        int savedSize = _store.Size;
        int savedHelv = _appearances.HelvId;
        bool savedSticky = _appearances.StickyNeedAppearances;
        var savedDirty = new HashSet<int>(_store.Dirty);
        _store.BeginBackup();
        try
        {
            // W4: XFA-aware viewers show the XFA data of a hybrid form, not the AcroForm values, so the first fill of an
            // unsigned hybrid form drops /XFA in the same revision. The packets stay in the file, unreferenced. FillIn
            // grants filling fields, not deleting the XFA definition, so that fill also removes /UR3 (spec 7.3, D-48).
            if (xfa == XfaKind.Hybrid)
            {
                _gate.ApplyUsageRights(null, null);
                _fields.EnsureAcroForm().Remove("XFA");
            }
            else
            {
                _gate.ApplyUsageRights("Form", "FillIn");
            }

            foreach (FillPlan plan in plans)
            {
                results[plan.Name] = ApplyFill(plan);
            }

            // Widget /AP and /AS and AcroForm /NeedAppearances are PDF 1.2.
            _store.RaiseVersion(12);
        }
        catch
        {
            _store.Restore(savedSize, _store.InfoId, _store.InfoGen, _store.TrailerId, savedDirty);
            _appearances.HelvId = savedHelv;
            _appearances.StickyNeedAppearances = savedSticky;
            throw;
        }

        _store.EndBackup();
        return results;
    }

    private FillPlan PlanFill(string fullName, PdfFieldValue value, List<FieldSlot> matches)
    {
        FieldSlot first = matches[0];
        if (first.ReadOnly)
        {
            throw new PdfFieldReadOnlyException(fullName);
        }

        var plan = new FillPlan(fullName, matches, value);
        if (first.FieldType == PdfFieldType.Text)
        {
            if (first.Password)
            {
                throw new PdfFieldValueException("A password field cannot be filled.");
            }

            if (value.Kind != PdfFieldValueKind.Text)
            {
                throw new PdfFieldValueException("A text field takes a text value.");
            }

            if (first.MaxLength is int max && value.Text!.Length > max)
            {
                throw new PdfFieldValueException("The value exceeds /MaxLen.");
            }

            return plan;
        }

        if (first.ButtonKind == PdfButtonKind.CheckBox)
        {
            if (value.Kind != PdfFieldValueKind.Boolean)
            {
                throw new PdfFieldValueException("A check box takes a Boolean value.");
            }

            foreach (FieldSlot slot in matches)
            {
                if (_store.Load(slot.WidgetObjectId) is PdfObject.Dict widget)
                {
                    string? widgetOn = _fields.OnState(widget);
                    if (value.Boolean == true && plan.OnState != null && widgetOn != null && widgetOn != plan.OnState)
                    {
                        throw new PdfFieldValueException("The check box widgets have different on-state names.");
                    }

                    plan.OnState ??= widgetOn;
                }
            }

            return plan;
        }

        if (first.ButtonKind == PdfButtonKind.Radio)
        {
            if (value.Kind == PdfFieldValueKind.None)
            {
                return plan;
            }

            if (value.Kind != PdfFieldValueKind.Text)
            {
                throw new PdfFieldValueException("A radio takes an on-state name or no value.");
            }

            bool found = false;
            foreach (FieldSlot slot in matches)
            {
                if (slot.ExportValue != null)
                {
                    plan.HasOnStates = true;
                    found |= slot.ExportValue == value.Text;
                }
            }

            if (plan.HasOnStates && !found)
            {
                throw new PdfFieldValueException("The radio export value was not found.");
            }

            return plan;
        }

        if (first.FieldType == PdfFieldType.Choice)
        {
            plan.Items = value.Kind switch
            {
                PdfFieldValueKind.Text => new[] { value.Text! },
                PdfFieldValueKind.List => value.Values,
                PdfFieldValueKind.None => Array.Empty<string>(),
                _ => throw new PdfFieldValueException("A choice field takes export values or no value."),
            };
            if (!first.MultiSelect && plan.Items.Count > 1)
            {
                throw new PdfFieldValueException("The choice field accepts one value.");
            }

            foreach (string item in plan.Items)
            {
                RequireOption(first, item);
            }

            return plan;
        }

        if (first.FieldType == PdfFieldType.Signature)
        {
            throw new PdfFieldValueException("A signature field cannot be filled. Sign it with Sign and FieldName.");
        }

        throw new PdfFieldValueException("A push button has no value to fill.");
    }

    private PdfAppearanceDisposition ApplyFill(FillPlan plan)
    {
        FieldSlot first = plan.Matches[0];
        if (first.FieldType == PdfFieldType.Text)
        {
            return ApplyText(plan.Matches, plan.Value.Text!);
        }

        if (first.ButtonKind == PdfButtonKind.CheckBox)
        {
            return ApplyCheckBox(plan.Matches, plan.Value.Boolean == true, plan.OnState);
        }

        if (first.ButtonKind == PdfButtonKind.Radio)
        {
            return ApplyRadio(plan.Matches, plan.Value.Text, plan.HasOnStates);
        }

        return ApplyChoice(plan.Matches, plan.Items);
    }

    private PdfAppearanceDisposition ApplyText(List<FieldSlot> matches, string value)
    {
        FieldSlot first = matches[0];
        bool simple = first.FieldType == PdfFieldType.Text
            && !first.Comb
            && !first.RichText
            && !first.FileSelect
            && PdfTextCodec.CanEncodeWinAnsi(value)
            && Math.Abs(first.Rect.Width) > 0
            && Math.Abs(first.Rect.Height) > 0;
        foreach (FieldSlot slot in matches)
        {
            PdfObject.Dict owner = (PdfObject.Dict)_store.EditAny(slot.ValueObjectId);
            owner.Set("V", PdfTextCodec.Encode(value));
            PdfObject.Dict widget = slot.WidgetObjectId == slot.ValueObjectId
                ? owner
                : (PdfObject.Dict)_store.EditAny(slot.WidgetObjectId);
            if (!simple)
            {
                widget.Remove("AP");
            }
        }

        if (!simple)
        {
            _appearances.SetNeedAppearances(true);
            return PdfAppearanceDisposition.NeedsViewer;
        }

        int fontId = _appearances.EnsureHelv();
        foreach (FieldSlot slot in matches)
        {
            var appearance = AppearanceBuilder.ParseDefaultAppearance(slot.DefaultAppearance);
            var box = _appearances.AppearanceBox(slot.WidgetObjectId);
            byte[] bytes = AppearanceBuilder.Build(
                value,
                Math.Abs(slot.Rect.Width),
                Math.Abs(slot.Rect.Height),
                appearance.Size,
                slot.Align,
                appearance.Red,
                appearance.Green,
                appearance.Blue,
                slot.Multiline,
                !slot.Multiline,
                box.Background,
                box.Border,
                box.BorderWidth);
            var stream = new PdfObject.Dict();
            stream.Set("Type", new PdfObject.Name("XObject"));
            stream.Set("Subtype", new PdfObject.Name("Form"));
            stream.Set("FormType", PdfObject.Num.FromLong(1));
            stream.Set("BBox", PdfValues.RectArray(new PdfRect(0, 0, Math.Abs(slot.Rect.Width), Math.Abs(slot.Rect.Height))));
            var resources = new PdfObject.Dict();
            var fonts = new PdfObject.Dict();
            fonts.Set("Helv", new PdfObject.Ref(fontId, 0));
            resources.Set("Font", fonts);
            stream.Set("Resources", resources);
            stream.Set("Length", PdfObject.Num.FromLong(bytes.Length));
            stream.Body = new PdfObject.MemoryBody(bytes);
            int appearanceId = _store.Allocate(stream);
            PdfObject.Dict widget = (PdfObject.Dict)_store.EditAny(slot.WidgetObjectId);
            var normal = new PdfObject.Dict();
            normal.Set("N", new PdfObject.Ref(appearanceId, 0));
            widget.Set("AP", normal);
        }

        if (!_appearances.StickyNeedAppearances)
        {
            _appearances.SetNeedAppearances(false);
        }

        return PdfAppearanceDisposition.Generated;
    }

    private PdfAppearanceDisposition ApplyCheckBox(List<FieldSlot> matches, bool isChecked, string? on)
    {
        bool generated = false;
        if (on == null)
        {
            if (!_appearances.TryWriteCheckBoxAppearances(matches))
            {
                SetNameValue(matches, isChecked ? "Yes" : "Off", setAppearanceState: false);
                _appearances.SetNeedAppearances(true);
                return PdfAppearanceDisposition.NeedsViewer;
            }

            on = "Yes";
            generated = true;
        }

        SetNameValue(matches, isChecked ? on : "Off", setAppearanceState: true);
        if (generated && !_appearances.StickyNeedAppearances)
        {
            _appearances.SetNeedAppearances(false);
        }

        return generated ? PdfAppearanceDisposition.Generated : PdfAppearanceDisposition.ExistingAppearanceSelected;
    }

    private PdfAppearanceDisposition ApplyRadio(List<FieldSlot> matches, string? exportValue, bool any)
    {
        if (exportValue == null)
        {
            SetNameValue(matches, "Off", setAppearanceState: true);
            return PdfAppearanceDisposition.ExistingAppearanceSelected;
        }

        if (!any)
        {
            if (matches.Count == 1 && _appearances.TryWriteRadioAppearances(matches, exportValue))
            {
                SetNameValue(matches, exportValue, setAppearanceState: true);
                if (!_appearances.StickyNeedAppearances)
                {
                    _appearances.SetNeedAppearances(false);
                }

                return PdfAppearanceDisposition.Generated;
            }

            SetNameValue(matches, exportValue, setAppearanceState: false);
            _appearances.SetNeedAppearances(true);
            return PdfAppearanceDisposition.NeedsViewer;
        }

        foreach (FieldSlot slot in matches)
        {
            PdfObject.Dict widget = (PdfObject.Dict)_store.EditAny(slot.WidgetObjectId);
            widget.Set("AS", new PdfObject.Name(slot.ExportValue == exportValue ? exportValue : "Off"));
        }

        PdfObject.Dict owner = (PdfObject.Dict)_store.EditAny(matches[0].ValueObjectId);
        owner.Set("V", new PdfObject.Name(exportValue));
        return PdfAppearanceDisposition.ExistingAppearanceSelected;
    }

    private PdfAppearanceDisposition ApplyChoice(List<FieldSlot> matches, IReadOnlyList<string> values)
    {
        FieldSlot first = matches[0];
        foreach (FieldSlot slot in matches)
        {
            PdfObject.Dict owner = (PdfObject.Dict)_store.EditAny(slot.ValueObjectId);
            owner.Remove("I");
            if (values.Count == 0)
            {
                owner.Remove("V");
            }
            else if (first.MultiSelect)
            {
                var array = new PdfObject.Arr();
                foreach (string item in values)
                {
                    array.Items.Add(PdfTextCodec.Encode(item));
                }

                owner.Set("V", array);
            }
            else
            {
                owner.Set("V", PdfTextCodec.Encode(values[0]));
            }

            PdfObject.Dict widget = (PdfObject.Dict)_store.EditAny(slot.WidgetObjectId);
            widget.Remove("AP");
        }

        if (_appearances.TryWriteChoiceAppearance(matches, values))
        {
            return PdfAppearanceDisposition.Generated;
        }

        _appearances.SetNeedAppearances(true);
        return PdfAppearanceDisposition.NeedsViewer;
    }

    private static void RequireOption(FieldSlot slot, string value)
    {
        if (slot.Editable)
        {
            return;
        }

        foreach (PdfChoiceOption option in slot.Options)
        {
            if (option.Value == value)
            {
                return;
            }
        }

        throw new PdfFieldValueException("The choice value is not in /Opt.");
    }

    private void SetNameValue(List<FieldSlot> matches, string name, bool setAppearanceState)
    {
        PdfObject.Dict owner = (PdfObject.Dict)_store.EditAny(matches[0].ValueObjectId);
        owner.Set("V", new PdfObject.Name(name));
        if (!setAppearanceState)
        {
            return;
        }

        foreach (FieldSlot slot in matches)
        {
            PdfObject.Dict widget = (PdfObject.Dict)_store.EditAny(slot.WidgetObjectId);
            widget.Set("AS", new PdfObject.Name(name));
            if (slot.ValueObjectId != slot.WidgetObjectId)
            {
                continue;
            }
        }
    }

    private sealed class FillPlan
    {
        public FillPlan(string name, List<FieldSlot> matches, PdfFieldValue value)
        {
            Name = name;
            Matches = matches;
            Value = value;
        }

        public string Name { get; }

        public List<FieldSlot> Matches { get; }

        public PdfFieldValue Value { get; }

        // A check box's shared on-state name, or null when no widget has one.
        public string? OnState { get; set; }

        // Whether any radio widget has an on-state.
        public bool HasOnStates { get; set; }

        public IReadOnlyList<string> Items { get; set; } = Array.Empty<string>();
    }
}
